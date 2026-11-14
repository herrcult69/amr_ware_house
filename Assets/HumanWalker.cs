using UnityEngine;

/// <summary>
/// Walks a kinematic Rigidbody back and forth between its start position and start + pathEnd at constant speed,
/// pausing at each end. The two leg transforms swing in opposite phase so the lidar sees a real two-leg gait.
/// Obstacle only: it is seen through /scan, no ROS-TCP topic involved.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HumanWalker : MonoBehaviour
{
    [Tooltip("Path end relative to the start position, world axes (m).")]
    public Vector3 pathEnd = new Vector3(4f, 0f, 0f);
    public float speed = 1.0f;          // m/s
    public float pauseTime = 1.0f;      // s at each end

    [Header("Gait")]
    public Transform legLeft;
    public Transform legRight;
    public float legSwing = 0.25f;      // +/- m along the walking direction
    public float strideHz = 1.0f;       // full gait cycles per second

    Rigidbody rb;
    Vector3 a, b;
    bool towardB = true;
    float pauseLeft;
    float gaitPhase;
    Vector3 legLeftRest, legRightRest;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        a = transform.position;
        b = a + pathEnd;
        if (legLeft) legLeftRest = legLeft.localPosition;
        if (legRight) legRightRest = legRight.localPosition;
        Face(b - a);
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (pauseLeft > 0f)
        {
            pauseLeft -= dt;
            SwingLegs(false);
            return;
        }

        Vector3 target = towardB ? b : a;
        Vector3 pos = rb.position;
        Vector3 step = Vector3.MoveTowards(pos, target, speed * dt);
        rb.MovePosition(step);
        gaitPhase += strideHz * dt;
        SwingLegs(true);

        if ((target - step).sqrMagnitude < 1e-6f)
        {
            towardB = !towardB;
            pauseLeft = pauseTime;
            Face((towardB ? b : a) - target);
        }
    }

    void Face(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return;
        // Snap while paused (or at start): the legs then swing along the new walking direction.
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    void SwingLegs(bool walking)
    {
        // Legs are children of the root, which faces the walking direction, so local z is the walking direction.
        float s = walking ? Mathf.Sin(gaitPhase * 2f * Mathf.PI) * legSwing : 0f;
        if (legLeft) legLeft.localPosition = legLeftRest + new Vector3(0f, 0f, s);
        if (legRight) legRight.localPosition = legRightRest + new Vector3(0f, 0f, -s);
    }

    void OnDrawGizmos()
    {
        Vector3 from = Application.isPlaying ? a : transform.position;
        Vector3 to = Application.isPlaying ? b : transform.position + pathEnd;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawWireSphere(from, 0.1f);
        Gizmos.DrawWireSphere(to, 0.1f);
    }
}
