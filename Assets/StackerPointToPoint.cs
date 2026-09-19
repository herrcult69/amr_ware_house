using UnityEngine;

/// <summary>
/// Milestone 1 controller: drive the stacker to one world-space point without obstacle avoidance.
/// The robot is moved through StackerController, never by changing a visual or prefab-container transform.
/// </summary>
public class StackerPointToPoint : MonoBehaviour
{
    [Header("References")]
    public StackerController stackerController;
    public ArticulationBody baseLink;
    public Transform poseSource;

    [Header("Destination")]
    public Vector3 destinationPosition = new Vector3(3f, 0f, 0f);
    public bool startOnPlay = false;
    public float positionTolerance = 0.08f;
    public float headingTolerance = 4f;

    [Header("Motion")]
    public float linearSpeed = 0.5f;
    public float turnSpeed = 0.8f;
    public float headingGain = 2f;

    private bool navigating;
    private bool destinationReported;

    public bool IsNavigating => navigating;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (startOnPlay)
        {
            SetDestination(destinationPosition);
        }
    }

    private void FixedUpdate()
    {
        if (!navigating)
        {
            return;
        }

        if (!HasValidReferences())
        {
            StopNavigation();
            return;
        }

        Vector3 currentPosition = GetPosePosition();
        Vector3 planarOffset = destinationPosition - currentPosition;
        planarOffset.y = 0f;

        if (planarOffset.magnitude <= positionTolerance)
        {
            StopNavigation();
            if (!destinationReported)
            {
                destinationReported = true;
                Debug.Log("[StackerPointToPoint] Destination reached.");
            }
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(GetPoseForward(), Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            StopNavigation();
            Debug.LogError("[StackerPointToPoint] The pose source has no usable forward direction.");
            return;
        }

        forward.Normalize();
        Vector3 targetDirection = planarOffset.normalized;
        float headingError = Vector3.SignedAngle(forward, targetDirection, Vector3.up);
        float angularCommand = Mathf.Clamp(-headingError * Mathf.Deg2Rad * headingGain, -turnSpeed, turnSpeed);

        if (Mathf.Abs(headingError) > headingTolerance)
        {
            stackerController.SetWheelVelocities(0f, angularCommand);
            return;
        }

        stackerController.SetWheelVelocities(linearSpeed, angularCommand);
    }

    public void SetDestination(Vector3 worldPosition)
    {
        ResolveReferences();
        if (!HasValidReferences())
        {
            Debug.LogError("[StackerPointToPoint] Assign a StackerController and a base_link pose source.");
            return;
        }

        destinationPosition = worldPosition;
        destinationReported = false;
        navigating = true;
    }

    public void StopNavigation()
    {
        navigating = false;
        if (stackerController != null)
        {
            stackerController.SetWheelVelocities(0f, 0f);
        }
    }

    private void ResolveReferences()
    {
        if (stackerController == null)
        {
            stackerController = GetComponent<StackerController>();
        }

        if (baseLink == null && stackerController != null)
        {
            ArticulationBody[] articulationBodies = stackerController.GetComponentsInChildren<ArticulationBody>();
            foreach (ArticulationBody body in articulationBodies)
            {
                if (body.name == "base_link")
                {
                    baseLink = body;
                    break;
                }
            }
        }

        if (poseSource == null && baseLink != null)
        {
            poseSource = baseLink.transform;
        }
    }

    private bool HasValidReferences()
    {
        return stackerController != null && poseSource != null;
    }

    private Vector3 GetPosePosition()
    {
        return poseSource.position;
    }

    private Vector3 GetPoseForward()
    {
        return poseSource.forward;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(destinationPosition, positionTolerance);
        Gizmos.DrawLine(destinationPosition + Vector3.up * 0.05f, destinationPosition + Vector3.up * 0.5f);
    }
}
