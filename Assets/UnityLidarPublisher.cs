using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Sensor;
using RosMessageTypes.Std;
using RosMessageTypes.BuiltinInterfaces;

public class UnityLidarPublisher : MonoBehaviour
{
    [Header("ROS")]
    public string topicName = "/scan";
    public string frameId = "laser_link";

    [Header("LiDAR")]
    public float minRange = 0.10f;
    public float maxRange = 8.0f;

    // Number of rays around the robot.
    // 360 = one ray per degree.
    public int numberOfRays = 360;

    // Full 360 degree scan.
    public float fieldOfView = 360.0f;

    // Height of the simulated laser.
    // The actual transform position is controlled by this GameObject.
    public LayerMask obstacleLayers = ~0;

    [Header("Publishing")]
    public float publishRate = 10.0f;

    private ROSConnection ros;
    private float publishTimer;

    void Start()
    {
        ros = ROSConnection.GetOrCreateInstance();

        ros.RegisterPublisher<LaserScanMsg>(topicName);

        publishTimer = 0f;
    }

    void Update()
    {
        publishTimer += Time.deltaTime;

        if (publishTimer >= 1.0f / publishRate)
        {
            PublishScan();
            publishTimer = 0f;
        }
    }

    void PublishScan()
    {
        LaserScanMsg scan = new LaserScanMsg();
        scan.header = new HeaderMsg();

        scan.header.frame_id = frameId;

        double simTime = Time.timeAsDouble;
        uint secs = (uint)Math.Floor(simTime);
        uint nsecs = (uint)((simTime - secs) * 1e9);
        scan.header.stamp = new TimeMsg(secs, nsecs);

        // ROS uses radians.
        scan.angle_min = -Mathf.PI;
        scan.angle_max = Mathf.PI;

        scan.angle_increment =
            (scan.angle_max - scan.angle_min) / (numberOfRays - 1);

        scan.time_increment = 0.0f;
        scan.scan_time = 1.0f / publishRate;

        scan.range_min = minRange;
        scan.range_max = maxRange;

        scan.ranges = new float[numberOfRays];

        for (int i = 0; i < numberOfRays; i++)
        {
            float angleDeg =
                -fieldOfView / 2.0f +
                i * (fieldOfView / (numberOfRays - 1));

            float angleRad = angleDeg * Mathf.Deg2Rad;

            // Unity:
            // +Z = forward
            // +X = right
            // Vector3 direction =
            //     transform.forward * Mathf.Cos(angleRad) +
            //     transform.right * Mathf.Sin(angleRad);

            Vector3 direction = 
                transform.forward * Mathf.Cos(angleRad) -
                transform.right * Mathf.Sin(angleRad);

            direction.Normalize();

            Ray ray = new Ray(
                transform.position,
                direction
            );

            // float drawLength = maxRange; 

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxRange,
                    obstacleLayers))
            {
                float distance = hit.distance;

                if (distance < minRange)
                    distance = minRange;

                scan.ranges[i] = distance;
                // drawLength = distance;
            }
            else
            {
                // ROS LaserScan convention:
                // infinity means nothing detected.
                scan.ranges[i] = float.PositiveInfinity;
            }
            // Debug.DrawRay(ray.origin, ray.direction * drawLength, Color.green, 1.0f / publishRate);
        }

        ros.Publish(topicName, scan);
    }
}