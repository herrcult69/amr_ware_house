using System;
using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.BuiltinInterfaces;
using RosMessageTypes.Geometry;
using RosMessageTypes.Nav;
using RosMessageTypes.Tf2;
using RosMessageTypes.Rosgraph;

/// <summary>
/// Lesson 1: ideal planar odometry from Unity ground-truth pose, not wheel encoders.
/// Publishes /clock; ROS /use_sim_time must be true. Assumes Unity +Z is robot forward.
/// Attach once and assign the moving base_link Transform in this project's prefab.
/// Its planar projection represents the ROS base_footprint; the Unity object named
/// base_footprint is a stationary wrapper without an ArticulationBody.
/// </summary>
public class PlanarOdometryPublisher : MonoBehaviour
{
    [Tooltip("Assign the moving base_link, not the stationary base_footprint wrapper. Its planar pose represents ROS base_footprint.")]
    public Transform robotBase;
    [Min(1f)] public float publishHz = 20f;

    private ROSConnection ros;
    private Vector3 origin;
    private Quaternion originYaw;
    private Vector3 previousPosition;
    private float previousYaw;
    private double previousTime;
    private bool initialized;

    void Start()
    {
        if (robotBase == null)
        {
            Debug.LogError("[Odometry] Assign the moving base_link to Robot Base.", this);
            enabled = false;
            return;
        }

        if (robotBase.name == "base_footprint" && robotBase.GetComponent<ArticulationBody>() == null)
        {
            Debug.LogError("[Odometry] This base_footprint is a stationary wrapper. Assign its moving base_link child to Robot Base instead.", this);
            enabled = false;
            return;
        }

        ros = ROSConnection.GetOrCreateInstance();
        ros.RegisterPublisher<OdometryMsg>("/odom");
        ros.RegisterPublisher<TFMessageMsg>("/tf");
        ros.RegisterPublisher<ClockMsg>("/clock");
    }

    void LateUpdate()
    {
        double now = Time.timeAsDouble;
        // Capture the actual moving link, not the stationary prefab wrapper.
        if (!initialized)
        {
            origin = robotBase.position;
            originYaw = Quaternion.Euler(0f, robotBase.eulerAngles.y, 0f);
            previousPosition = origin;
            previousYaw = robotBase.eulerAngles.y;
            previousTime = now;
            initialized = true;
            return;
        }

        double dt = now - previousTime;
        if (dt < 1.0 / Math.Max(1f, publishHz)) return;

        Vector3 position = robotBase.position;
        float yaw = robotBase.eulerAngles.y;
        Vector3 relative = Quaternion.Inverse(originYaw) * (position - origin);
        // Unity: X right, Y up, Z forward. ROS: X forward, Y left, Z up.
        double heading = -Mathf.DeltaAngle(originYaw.eulerAngles.y, yaw) * Mathf.Deg2Rad;
        var rotation = new QuaternionMsg(0, 0, Math.Sin(heading / 2), Math.Cos(heading / 2));
        Vector3 bodyVelocity = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0))
            * ((position - previousPosition) / (float)dt);
        double angularZ = -Mathf.DeltaAngle(previousYaw, yaw) * Mathf.Deg2Rad / dt;

        double seconds = now;
        uint sec = (uint)Math.Floor(seconds);
        var stamp = new TimeMsg(sec, (uint)((seconds - sec) * 1000000000.0));

        var odom = new OdometryMsg();
        odom.header.frame_id = "odom";
        odom.header.stamp = stamp;
        odom.child_frame_id = "base_footprint";
        odom.pose.pose.position = new PointMsg(relative.z, -relative.x, 0);
        odom.pose.pose.orientation = rotation;
        odom.twist.twist.linear = new Vector3Msg(bodyVelocity.z, -bodyVelocity.x, 0);
        odom.twist.twist.angular = new Vector3Msg(0, 0, angularZ);
        // Zero covariance is intentional for this ideal ground-truth lesson only.

        var transformMessage = new TransformStampedMsg();
        transformMessage.header.frame_id = "odom";
        transformMessage.header.stamp = stamp;
        transformMessage.child_frame_id = "base_footprint";
        transformMessage.transform.translation = new Vector3Msg(relative.z, -relative.x, 0);
        transformMessage.transform.rotation = rotation;

        ros.Publish("/clock", new ClockMsg(stamp));
        ros.Publish("/odom", odom);
        ros.Publish("/tf", new TFMessageMsg(new[] { transformMessage }));
        previousPosition = position;
        previousYaw = yaw;
        previousTime = now;
    }
}
