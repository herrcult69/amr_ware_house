using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Sensor;

// Attach to laser_link. Local Unity +Z is forward, +Y is up.
// Ideal instantaneous planar scan; /clock remains owned by the odometry publisher.
[DisallowMultipleComponent]
public class PlanarLaserScanPublisher : MonoBehaviour
{
    [Range(2, 361)] public int rayCount = 19;
    [Range(1f, 270f)] public float fieldOfView = 90f;
    [Min(0f)] public float rangeMin = 0.05f;
    [Min(0.01f)] public float rangeMax = 10f;
    [Min(1f)] public float publishHz = 10f;
    public LayerMask obstacleMask = Physics.DefaultRaycastLayers;

    private ROSConnection ros;
    private double previousScanTime;
    private uint sequence;

    void Start()
    {
        if (rangeMin < 0f || rangeMax <= rangeMin || publishHz <= 0f)
        {
            Debug.LogError("[LaserScan] Require 0 <= Range Min < Range Max and Publish Hz > 0.", this);
            enabled = false;
            return;
        }
        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<LaserScanMsg>("/scan");
        previousScanTime = Time.timeAsDouble;
    }

    void LateUpdate()
    {
        double now = Time.timeAsDouble;
        double elapsed = now - previousScanTime;
        if (elapsed <= 0 || elapsed < 1.0 / Math.Max(1f, publishHz)) return;
        if (rangeMin < 0f || rangeMax <= rangeMin) return;

        int count = Mathf.Clamp(rayCount, 2, 361);
        float span = Mathf.Clamp(fieldOfView, 1f, 270f) * Mathf.Deg2Rad;
        float angleMin = -span / 2f;
        float angleStep = span / (count - 1);
        var scan = new LaserScanMsg();
        uint seconds = (uint)Math.Floor(now);
        scan.header.seq = sequence++;
        scan.header.stamp = new TimeMsg(seconds, (uint)((now - seconds) * 1000000000.0));
        scan.header.frame_id = "laser_link";
        scan.angle_min = angleMin;
        scan.angle_max = angleMin + (count - 1) * angleStep;
        scan.angle_increment = angleStep;
        scan.time_increment = 0f; // All rays sample the same physics state.
        scan.scan_time = (float)elapsed;
        scan.range_min = rangeMin;
        scan.range_max = rangeMax;
        scan.ranges = new float[count]; // Fresh array: do not mutate an enqueued message.
        scan.intensities = new float[0];

        for (int i = 0; i < count; i++)
        {
            // Đổi góc ROS sang Unity: 
            // Rad2Deg: đổi radian sang độ.
            // Dấu -: đổi chiều quay để giữ đúng trái–phải.
            float rosAngle = angleMin + i * angleStep;
            float unityAngle = -rosAngle * Mathf.Rad2Deg;


            Vector3 direction = Quaternion.AngleAxis(unityAngle, transform.up)
                * transform.forward;
            bool detected = Physics.Raycast(transform.position, direction,
                out RaycastHit hit, rangeMax, obstacleMask, QueryTriggerInteraction.Ignore);

            // Too-close returns are invalid, not evidence of free space.
            scan.ranges[i] = !detected ? float.PositiveInfinity
                : hit.distance < rangeMin ? float.NegativeInfinity : hit.distance;
            float drawnDistance = detected ? hit.distance : rangeMax;
            Debug.DrawRay(transform.position, direction * drawnDistance,
                detected ? Color.green : Color.red, (float)elapsed);
        }

        ros.Publish("/scan", scan);
        previousScanTime = now;
    }
}
