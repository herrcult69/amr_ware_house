using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Explicit batch migration/validation only; never runs automatically on import.
public static class XStackMigration
{
    const string PrefabPath = "Assets/Prefabs/xstack_amr.prefab";
    const string ScenePath = "Assets/Scenes/Milestone_1.unity";
    static readonly string ReportDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.diagnostics/xstack"));
    static string Number(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
    static Transform Link(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);

    public static void ConfigureAndValidate()
    {
        Require(Application.isBatchMode, "Migration is batch-only; close the interactive editor first.");
        Directory.CreateDirectory(ReportDir);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            int robotLayer = LayerMask.NameToLayer("RobotBody");
            Require(robotLayer >= 0, "RobotBody layer missing.");
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = robotLayer;
            // This demonstration payload is not part of the unloaded robot envelope.
            var payload = root.transform.Find("StandardPallet");
            if (payload != null)
            {
                payload.gameObject.SetActive(false);
                foreach (var t in payload.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0;
            }
            var controller = root.GetComponent<StackerController>();
            Require(controller != null, "XStack controller missing.");
            controller.leftWheel = Link(root, "left_wheel_link").GetComponent<ArticulationBody>();
            controller.rightWheel = Link(root, "right_wheel_link").GetComponent<ArticulationBody>();
            controller.forkCarriage = Link(root, "fork_carriage_link").GetComponent<ArticulationBody>();
            controller.wheelRadius = 0.075f;
            controller.trackWidth = 0.40f;
            controller.minLiftHeight = -0.02f;
            controller.maxLiftHeight = 0.75f;
            controller.enableHeadingHold = false;
            controller.disableRearCaster = false;
            controller.solverIterations = 32;
            controller.solverVelocityIterations = 8;
            var odom = root.GetComponent<PlanarOdometryPublisher>();
            odom.robotBase = Link(root, "drive_centre");
            odom.childFrameId = "drive_center";
            odom.publishHz = 20;
            var laser = Link(root, "laser_link");
            var scan = laser.GetComponent<PlanarLaserScanPublisher>() ?? laser.gameObject.AddComponent<PlanarLaserScanPublisher>();
            scan.rayCount = 181;
            scan.fieldOfView = 180;
            scan.rangeMin = 0.05f;
            scan.rangeMax = 10;
            scan.publishHz = 10;
            scan.obstacleMask = Physics.DefaultRaycastLayers & ~(1 << robotLayer);
            var preview = odom.robotBase.GetComponent<FootprintPreview>() ?? odom.robotBase.gameObject.AddComponent<FootprintPreview>();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        // Measure local collision geometry in the axle frame, without changing the saved pose.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var reference = Link(instance, "drive_centre");
        float front = 0, rear = 0, width = 0;
        int count = 0;
        var report = new StringBuilder("Unloaded XStack collider bounds relative to drive_centre; ROS x=Unity z, ROS y=-Unity x.\n");
        foreach (var collider in Link(instance, "base_link").GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            // Edit-mode articulation Collider.bounds can retain an old physics pose.
            // Transform the actual local collision geometry into the axle frame instead.
            Bounds bounds;
            if (collider is BoxCollider box) bounds = new Bounds(box.center, box.size);
            else if (collider is SphereCollider sphere) bounds = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null) bounds = mesh.sharedMesh.bounds;
            else throw new InvalidOperationException("Unsupported collider geometry: " + collider.name);
            Require(bounds.size.sqrMagnitude > 0, "Empty collider bounds: " + collider.name);
            count++;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                p = reference.InverseTransformPoint(collider.transform.TransformPoint(p));
                front = Mathf.Max(front, p.z);
                rear = Mathf.Max(rear, -p.z);
                width = Mathf.Max(width, Mathf.Abs(p.x));
            }
            report.AppendLine(collider.name + ": " + bounds.ToString("F4"));
        }
        Require(count > 0, "No colliders measured.");
        front = Mathf.Ceil(front * 100) / 100;
        rear = Mathf.Ceil(rear * 100) / 100;
        width = Mathf.Ceil(width * 100) / 100;
        Require(front < 1 && rear < 1 && width < 0.5f, "Unexpected XStack envelope; inspect geometry before saving.");
        report.AppendLine($"Colliders={count}; front={Number(front)}, rear={Number(rear)}, halfWidth={Number(width)}, padding=0.02");
        File.WriteAllText(Path.Combine(ReportDir, "footprint.txt"), report.ToString());
        root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var preview = Link(root, "drive_centre").GetComponent<FootprintPreview>();
            preview.front = front;
            preview.rear = rear;
            preview.halfWidth = width;
            preview.padding = 0.02f;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        string config = "# Measured from XStack prefab colliders; unloaded, ground-projected axle origin.\n" +
            "robot_base_frame: drive_center\nfootprint: [[" + Number(front) + ", " + Number(width) + "], [-" + Number(rear) + ", " + Number(width) +
            "], [-" + Number(rear) + ", -" + Number(width) + "], [" + Number(front) + ", -" + Number(width) + "]]\nfootprint_padding: 0.02\n";
        File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath,
            "../../CollaborativeAMRStockerization/catkin_ws/src/amr_navigation/config/robot_xstack.yaml")), config);

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (var animator in UnityEngine.Object.FindObjectsOfType<Animator>(true))
        {
            if (!animator.transform.root.name.StartsWith("DynamicObstacle", StringComparison.Ordinal)) continue;
            animator.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
        }
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Validate();
    }

    public static void Validate()
    {
        Require(Application.isBatchMode, "Validation is batch-only.");
        Directory.CreateDirectory(ReportDir);
        var report = new StringBuilder();
        foreach (string scene in new[] {ScenePath, "Assets/Scenes/WarehouseTraining.unity", "Assets/Scenes/SampleScene.unity"})
        {
            EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
            bool xstack = scene == ScenePath;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Transform>(true))
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + scene + "/" + t.name);
                Require(!PrefabUtility.IsPrefabAssetMissing(t.gameObject), "Missing prefab: " + scene + "/" + t.name);
            }
            var publishers = UnityEngine.Object.FindObjectsOfType<PlanarOdometryPublisher>().Where(p => p.enabled).ToArray();
            Require(publishers.Length == 1, "Expected one odometry/clock publisher in " + scene);
            var odom = publishers[0];
            Require(odom.robotBase != null, "Missing odometry pose reference.");
            Require(odom.childFrameId == (xstack ? "drive_center" : "base_footprint"), "Wrong odometry frame in " + scene);
            var scans = UnityEngine.Object.FindObjectsOfType<PlanarLaserScanPublisher>().Where(p => p.enabled).ToArray();
            Require(scans.Length == 1, "Expected one scan publisher in " + scene);
            var controller = UnityEngine.Object.FindObjectsOfType<StackerController>().Single();
            Require(!controller.enableHeadingHold && !controller.disableRearCaster, "Unvalidated motion options enabled.");
            Require(Mathf.Abs(controller.trackWidth - (xstack ? 0.40f : 0.44f)) < 0.0001f, "Wrong wheel spacing.");
            controller.ConfigureJoints();
            Require(controller.GetComponentsInChildren<ArticulationBody>().All(b => b.solverIterations == 32 && b.solverVelocityIterations == 8), "Wrong solver profile.");
            if (xstack)
            {
                Require(controller.leftWheel != null && controller.rightWheel != null && controller.forkCarriage != null, "Explicit joint references missing.");
                Require(scans[0].rayCount == 181 && scans[0].fieldOfView == 180, "Wrong scan geometry.");
                Require((scans[0].obstacleMask.value & (1 << LayerMask.NameToLayer("RobotBody"))) == 0, "Scan includes RobotBody.");
                Require(Link(controller.gameObject, "base_link").GetComponentsInChildren<Collider>(true).All(c => c.gameObject.layer == LayerMask.NameToLayer("RobotBody")), "Robot collider on wrong layer.");
                Require(UnityEngine.Object.FindObjectsOfType<Animator>().All(a => !a.transform.root.name.StartsWith("DynamicObstacle", StringComparison.Ordinal) || !a.enabled), "Dynamic animation enabled.");
            }
            report.AppendLine("PASS: " + scene + " publishers, frames, references, wheel spacing and solver settings.");
        }
        Require(Mathf.Abs(Time.fixedDeltaTime - 0.01f) < 0.000001f, "Wrong physics timestep.");
        File.WriteAllText(Path.Combine(ReportDir, "unity-validation.txt"), report.ToString());
        Debug.Log(report.ToString());
    }

    // Offline physics smoke test: no Play Mode, ROS connection or scene saving.
    public static void ValidateMotion()
    {
        Require(Application.isBatchMode, "Motion validation is batch-only.");
        Directory.CreateDirectory(ReportDir);
        var originalMode = Physics.simulationMode;
        var report = new StringBuilder("scene,command,forward_m,ros_yaw_rad,stopped_speed_mps,base_height_m,tilt_deg,pass\n");
        bool passed = true;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            foreach (string scene in new[] {ScenePath, "Assets/Scenes/WarehouseTraining.unity"})
            foreach (var command in new[] {new Vector2(0.10f, 0), new Vector2(-0.10f, 0), new Vector2(0, 0.20f), new Vector2(0, -0.20f), new Vector2(0.03f, 0)})
            {
                var loaded = EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                var physics = loaded.GetPhysicsScene();
                var controller = UnityEngine.Object.FindObjectsOfType<StackerController>().Single();
                controller.SendMessage("FindArticulationBodies");
                controller.ConfigureJoints();
                controller.SendMessage("ConfigureFriction");
                controller.SetLiftPosition(0);
                controller.SetWheelVelocities(0, 0);
                Physics.SyncTransforms();
                for (int i = 0; i < 200; i++) physics.Simulate(0.01f);
                var odom = UnityEngine.Object.FindObjectsOfType<PlanarOdometryPublisher>().Single();
                Vector3 start = odom.robotBase.position;
                float yaw = odom.robotBase.eulerAngles.y;
                for (int i = 0; i < 300; i++)
                {
                    controller.SetWheelVelocities(command.x, command.y);
                    physics.Simulate(0.01f);
                }
                Vector3 travel = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0)) * (odom.robotBase.position - start);
                float angle = -Mathf.DeltaAngle(yaw, odom.robotBase.eulerAngles.y) * Mathf.Deg2Rad;
                controller.SetWheelVelocities(0, 0);
                for (int i = 0; i < 200; i++) physics.Simulate(0.01f);
                var body = Link(controller.gameObject, "base_link").GetComponent<ArticulationBody>();
                float tilt = Vector3.Angle(body.transform.up, Vector3.up);
                bool ok = (command.x == 0 ? angle * Mathf.Sign(command.y) > 0.12f : travel.z * Mathf.Sign(command.x) > Mathf.Abs(command.x) * 0.6f)
                    && body.velocity.magnitude < 0.03f && tilt < 8;
                passed &= ok;
                report.AppendLine(string.Join(",", scene, command.ToString("F2").Replace(",", ";"),
                    Number(travel.z), Number(angle), Number(body.velocity.magnitude), Number(body.transform.position.y), Number(tilt), ok));
                File.WriteAllText(Path.Combine(ReportDir, "motion.csv"), report.ToString());
            }
        }
        finally { Physics.simulationMode = originalMode; }
        Require(passed, "Motion smoke test failed; inspect motion.csv before navigation.");
        Debug.Log("PASS: XStack and legacy offline motion smoke tests.");
    }
}
