using UnityEngine;
using Unity.Robotics.ROSTCPConnector;
using RosMessageTypes.Geometry;
using RosMessageTypes.Std;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class StackerController : MonoBehaviour
{
    [Header("Articulation Bodies")]
    public ArticulationBody leftWheel;
    public ArticulationBody rightWheel;
    public ArticulationBody forkCarriage;

    [Header("Robot Physical Dimensions")]
    public float wheelRadius = 0.075f; // meters
    public float trackWidth = 0.44f;   // meters (distance between wheels)
    public float maxLiftHeight = 0.40f; // meters

    [Header("Drive Tuning")]
    public float wheelDamping = 1000f;
    public float wheelForceLimit = 5000f;
    public float liftStiffness = 50000f;
    public float liftDamping = 1000f;
    public float liftForceLimit = 2000f;

    [Header("ROS Topics")]
    public string cmdVelTopic = "/cmd_vel";
    public string liftCmdTopic = "/lift_cmd";

    [Header("Manual Keyboard Speed (Editor Testing)")]
    public float manualSpeed = 0.8f;     // m/s
    public float manualTurnRate = 1.5f;  // rad/s
    public float manualLiftSpeed = 0.15f; // m/s

    private ROSConnection ros;
    private float targetLiftPosition = 0.0f;
    private bool manualOverrideActive = false;

    void Start()
    {
        FindArticulationBodies();
        ConfigureJoints();
        ConfigureFriction();

        // Connect to ROS
        ros = ROSConnection.GetOrCreateInstance();
        ros.Subscribe<TwistMsg>(cmdVelTopic, OnCmdVelReceived);
        ros.Subscribe<Float32Msg>(liftCmdTopic, OnLiftCmdReceived);

        Debug.Log("[StackerController] Initialized! Friction tuned (casters=0, wheels=1), Keyboard (WASD + R/F) active.");
    }

    void FindArticulationBodies()
    {
        if (leftWheel == null)
            leftWheel = transform.Find("base_footprint/base_link/left_wheel_link")?.GetComponent<ArticulationBody>();
        if (rightWheel == null)
            rightWheel = transform.Find("base_footprint/base_link/right_wheel_link")?.GetComponent<ArticulationBody>();
        if (forkCarriage == null)
            forkCarriage = transform.Find("base_footprint/base_link/mast_link/fork_carriage_link")?.GetComponent<ArticulationBody>();
    }

    void ConfigureFriction()
    {
        // 1. Frictionless material for Casters (so they glide freely like ice skates)
        PhysicMaterial frictionlessMat = new PhysicMaterial("FrictionlessCaster")
        {
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounciness = 0f,
            bounceCombine = PhysicMaterialCombine.Minimum
        };

        var frontCaster = transform.Find("base_footprint/base_link/front_caster_link")?.GetComponentInChildren<Collider>();
        if (frontCaster != null) frontCaster.material = frictionlessMat;

        var rearCaster = transform.Find("base_footprint/base_link/rear_caster_link")?.GetComponentInChildren<Collider>();
        if (rearCaster != null) rearCaster.material = frictionlessMat;

        // 2. High-grip material for Drive Wheels (ensures strong traction)
        PhysicMaterial wheelMat = new PhysicMaterial("WheelGrip")
        {
            dynamicFriction = 1.0f,
            staticFriction = 1.0f,
            frictionCombine = PhysicMaterialCombine.Maximum,
            bounciness = 0f
        };

        var leftCol = leftWheel?.GetComponentInChildren<Collider>();
        if (leftCol != null) leftCol.material = wheelMat;

        var rightCol = rightWheel?.GetComponentInChildren<Collider>();
        if (rightCol != null) rightCol.material = wheelMat;
    }

    public void ConfigureJoints()
    {
        // Ensure the root ArticulationBody is mobile (not anchored to world)
        var rootAB = GetComponentInChildren<ArticulationBody>();
        if (rootAB != null && rootAB.isRoot)
        {
            rootAB.immovable = false;
        }

        // Auto-configure Left Wheel as Velocity Drive
        if (leftWheel != null)
        {
            var drive = leftWheel.xDrive;
            drive.stiffness = 0f;
            drive.damping = wheelDamping;
            drive.forceLimit = wheelForceLimit;
            leftWheel.xDrive = drive;
        }

        // Auto-configure Right Wheel as Velocity Drive
        if (rightWheel != null)
        {
            var drive = rightWheel.xDrive;
            drive.stiffness = 0f;
            drive.damping = wheelDamping;
            drive.forceLimit = wheelForceLimit;
            rightWheel.xDrive = drive;
        }

        // Auto-configure Fork Carriage as Position Drive
        if (forkCarriage != null)
        {
            var drive = forkCarriage.xDrive;
            drive.stiffness = liftStiffness;
            drive.damping = liftDamping;
            drive.forceLimit = liftForceLimit;
            forkCarriage.xDrive = drive;
        }
    }

    void Update()
    {
        float forwardInput = 0f;
        float turnInput = 0f;
        float liftInput = 0f;

        // 1. Read input using Unity New Input System if active
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) forwardInput += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) forwardInput -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) turnInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turnInput += 1f;
            if (kb.rKey.isPressed) liftInput += 1f;
            if (kb.fKey.isPressed) liftInput -= 1f;
        }
#endif

        // 2. Fallback to Legacy Input System if active
#if ENABLE_LEGACY_INPUT_MANAGER
        try
        {
            forwardInput += Input.GetAxis("Vertical");
            turnInput += Input.GetAxis("Horizontal");
            if (Input.GetKey(KeyCode.R)) liftInput += 1f;
            if (Input.GetKey(KeyCode.F)) liftInput -= 1f;
        }
        catch {}
#endif

        // Handle keyboard driving
        if (Mathf.Abs(forwardInput) > 0.01f || Mathf.Abs(turnInput) > 0.01f)
        {
            manualOverrideActive = true;
            float linearVel = forwardInput * manualSpeed;
            float angularVel = -turnInput * manualTurnRate; // negative so D/Right turns clockwise
            SetWheelVelocities(linearVel, angularVel);
        }
        else if (manualOverrideActive)
        {
            manualOverrideActive = false;
            SetWheelVelocities(0f, 0f);
        }

        // Handle keyboard lift
        if (Mathf.Abs(liftInput) > 0.01f)
        {
            targetLiftPosition = Mathf.Clamp(targetLiftPosition + liftInput * manualLiftSpeed * Time.deltaTime, 0f, maxLiftHeight);
            SetLiftPosition(targetLiftPosition);
        }
    }

    // ROS 1 /cmd_vel Callback
    void OnCmdVelReceived(TwistMsg msg)
    {
        if (!manualOverrideActive)
        {
            SetWheelVelocities((float)msg.linear.x, (float)msg.angular.z);
        }
    }

    // ROS 1 /lift_cmd Callback
    void OnLiftCmdReceived(Float32Msg msg)
    {
        targetLiftPosition = Mathf.Clamp(msg.data, 0f, maxLiftHeight);
        SetLiftPosition(targetLiftPosition);
    }

    // Differential Drive Kinematics: (linear, angular) -> (wheel angular speeds)
    public void SetWheelVelocities(float linearX, float angularZ)
    {
        float leftLinearSpeed = linearX - (angularZ * trackWidth / 2.0f);
        float rightLinearSpeed = linearX + (angularZ * trackWidth / 2.0f);

        // Convert linear speed (m/s) to angular speed (deg/s) for Unity ArticulationBody
        float leftDegPerSec = (leftLinearSpeed / wheelRadius) * Mathf.Rad2Deg;
        float rightDegPerSec = (rightLinearSpeed / wheelRadius) * Mathf.Rad2Deg;

        if (leftWheel != null)
        {
            var drive = leftWheel.xDrive;
            drive.targetVelocity = leftDegPerSec;
            leftWheel.xDrive = drive;
        }

        if (rightWheel != null)
        {
            var drive = rightWheel.xDrive;
            drive.targetVelocity = rightDegPerSec;
            rightWheel.xDrive = drive;
        }
    }

    public void SetLiftPosition(float height)
    {
        if (forkCarriage != null)
        {
            var drive = forkCarriage.xDrive;
            drive.target = height;
            forkCarriage.xDrive = drive;
        }
    }
}
