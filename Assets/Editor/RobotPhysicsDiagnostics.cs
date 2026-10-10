using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Read-only, opt-in scene inspection. Requests contain only the word "snapshot".
[InitializeOnLoad]
public static class RobotPhysicsDiagnostics
{
    static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../UserSettings/RobotPhysicsDiagnostics"));
    static double nextPoll;

    static RobotPhysicsDiagnostics()
    {
        EditorApplication.update += Poll;
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "ready.txt"), DateTime.UtcNow.ToString("O"));
    }

    static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling) return;
        nextPoll = EditorApplication.timeSinceStartup + 1;
        string request = Path.Combine(Folder, "request.txt");
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim();
        File.Delete(request);
        if (command == "snapshot") SaveSnapshot();
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static string V(Vector3 v) => v.ToString("F6");

    public static void CaptureSavedWarehouse()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Saved-scene capture is batch-only; use the menu to inspect the live scene.");
        EditorSceneManager.OpenScene("Assets/Scenes/WarehouseTraining.unity", OpenSceneMode.Single);
        SaveSnapshot();
    }

    public static void ValidateConfiguredWarehouse()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Configuration validation is batch-only.");
        EditorSceneManager.OpenScene("Assets/Scenes/WarehouseTraining.unity", OpenSceneMode.Single);
        var controllers = UnityEngine.Object.FindObjectsOfType<StackerController>();
        if (controllers.Length != 1) throw new InvalidOperationException("Expected exactly one warehouse robot.");
        controllers[0].ConfigureJoints();
        foreach (var body in controllers[0].GetComponentsInChildren<ArticulationBody>())
            if (body.solverIterations != 32 || body.solverVelocityIterations != 8)
                throw new InvalidOperationException("Unexpected solver configuration on " + body.name);
        if (Mathf.Abs(Time.fixedDeltaTime - 0.01f) > 0.000001f)
            throw new InvalidOperationException("Expected 0.01 s Fixed Timestep.");
        SaveSnapshot();
        File.WriteAllText(Path.Combine(Folder, "validation.txt"), "PASS: 32/8 solver on every robot articulation; timestep 0.01 s. Scene not saved.\n");
    }

    [MenuItem("Tools/Robot/Save Physics Snapshot")]
    public static void SaveSnapshot()
    {
        try
        {
            var text = new StringBuilder();
            text.AppendLine($"UTC={DateTime.UtcNow:O} scene={SceneManager.GetActiveScene().path} playing={EditorApplication.isPlaying} paused={EditorApplication.isPaused}");
            text.AppendLine($"dt={Time.fixedDeltaTime} solver={Physics.defaultSolverIterations}/{Physics.defaultSolverVelocityIterations} contactOffset={Physics.defaultContactOffset} gravity={V(Physics.gravity)}");
            var controllers = UnityEngine.Object.FindObjectsOfType<StackerController>(true);
            foreach (var controller in controllers)
            {
                text.AppendLine($"CONTROLLER {PathOf(controller.transform)} enabled={controller.enabled} radius={controller.wheelRadius} track={controller.trackWidth} damping={controller.wheelDamping} force={controller.wheelForceLimit}");
                foreach (var body in controller.GetComponentsInChildren<ArticulationBody>(true))
                {
                    var d = body.xDrive;
                    string jointV = body.dofCount > 0 ? body.jointVelocity[0].ToString("F6") : "n/a";
                    text.AppendLine($"BODY {PathOf(body.transform)} root={body.isRoot} immovable={body.immovable} mass={body.mass} joint={body.jointType} solver={body.solverIterations}/{body.solverVelocityIterations} sleep={body.sleepThreshold} sleeping={body.IsSleeping()} pos={V(body.transform.position)} rotation={V(body.transform.eulerAngles)} scale={V(body.transform.lossyScale)} velocity={V(body.velocity)} angular={V(body.angularVelocity)} jointVelocity={jointV} inertia={V(body.inertiaTensor)} damping={body.linearDamping}/{body.angularDamping} friction={body.jointFriction} targetDeg={d.targetVelocity} stiffness={d.stiffness} driveDamping={d.damping} forceLimit={d.forceLimit}");
                }
                var robotColliders = controller.GetComponentsInChildren<Collider>(true);
                var bounds = new Bounds(controller.transform.position, Vector3.zero);
                foreach (var col in robotColliders) if(col.enabled) bounds.Encapsulate(col.bounds);
                bounds.Expand(1f);
                var nearby = UnityEngine.Object.FindObjectsOfType<Collider>(true)
                    .Where(c => !c.transform.IsChildOf(controller.transform) && c.enabled && c.bounds.Intersects(bounds)).ToArray();
                foreach (var col in robotColliders.Concat(nearby))
                {
                    var material = col.sharedMaterial;
                    string mat = material == null ? "None" : $"{material.name} friction={material.staticFriction}/{material.dynamicFriction} combine={material.frictionCombine}";
                    text.AppendLine($"COLLIDER {PathOf(col.transform)} type={col.GetType().Name} active={col.gameObject.activeInHierarchy} enabled={col.enabled} trigger={col.isTrigger} center={V(col.bounds.center)} extents={V(col.bounds.extents)} offset={col.contactOffset} material={mat}");
                }
                foreach (var a in robotColliders.Where(c=>c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy))
                foreach (var b in nearby.Where(c=>!c.isTrigger && c.gameObject.activeInHierarchy))
                {
                    if (!a.bounds.Intersects(b.bounds) || Physics.GetIgnoreCollision(a,b)) continue;
                    if (Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out Vector3 direction,out float distance))
                        text.AppendLine($"OVERLAP (geometric, not contact force) {PathOf(a.transform)} <-> {PathOf(b.transform)} depth={distance:F6} direction={V(direction)}");
                }
            }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "snapshot.txt"), text.ToString());
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(Folder, "error.txt"), e.ToString()); }
    }
}
