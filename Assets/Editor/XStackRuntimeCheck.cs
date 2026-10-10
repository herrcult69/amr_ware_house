using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Opt-in batch Play Mode for bridge validation. Never runs on a normal editor launch.
[InitializeOnLoad]
public static class XStackRuntimeCheck
{
    const string Key = "XStackRuntimeCheck.Active";
    static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.diagnostics/xstack"));
    static double nextSnapshot;
    static XStackRuntimeCheck() { EditorApplication.update += Tick; }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch-only integration check.");
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Key, true);
        SessionState.SetString(Key + ".Deadline", DateTime.UtcNow.AddMinutes(10).Ticks.ToString());
        EditorSceneManager.OpenScene("Assets/Scenes/Milestone_1.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!Application.isBatchMode || !SessionState.GetBool(Key, false)) return;
        bool expired = DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(Key + ".Deadline", "0"));
        if (expired || File.Exists(Path.Combine(Folder, "stop-runtime.txt")))
        {
            var controller = UnityEngine.Object.FindObjectOfType<StackerController>();
            if (controller != null) controller.SetWheelVelocities(0, 0);
            SessionState.SetBool(Key, false);
            EditorApplication.Exit(expired ? 1 : 0);
            return;
        }
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextSnapshot) return;
        nextSnapshot = EditorApplication.timeSinceStartup + 0.5;
        var odom = UnityEngine.Object.FindObjectOfType<PlanarOdometryPublisher>();
        var scan = UnityEngine.Object.FindObjectOfType<PlanarLaserScanPublisher>();
        if (odom == null || scan == null) return;
        float[] expected = new float[3];
        for (int i = 0; i < 3; i++)
        {
            Vector3 direction = Quaternion.AngleAxis(scan.fieldOfView * (0.5f - i * 0.5f), scan.transform.up) * scan.transform.forward;
            expected[i] = Physics.Raycast(scan.transform.position, direction, out RaycastHit hit, scan.rangeMax,
                scan.obstacleMask, QueryTriggerInteraction.Ignore) ? hit.distance : -1;
        }
        string f(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        Vector3 p = odom.robotBase.position;
        string json = "{\"sim_time\":" + Time.timeAsDouble.ToString("R", CultureInfo.InvariantCulture) +
            ",\"unity_position\":[" + f(p.x) + "," + f(p.y) + "," + f(p.z) + "],\"unity_yaw\":" + f(odom.robotBase.eulerAngles.y) +
            ",\"scan_expected\":[" + string.Join(",", Array.ConvertAll(expected, f)) + "]}";
        File.WriteAllText(Path.Combine(Folder, "runtime.json"), json);
    }
}
