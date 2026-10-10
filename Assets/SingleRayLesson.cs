using UnityEngine;

// Learning probe: a configurable fan of rays around this object's local +Z axis.
// Keep the original class name so the existing scene component stays connected.
// Assumes scene scale is 1 Unity unit = 1 metre. Does not control the robot.
public class SingleRayLesson : MonoBehaviour
{
    [Min(0.01f)] public float maxDistance = 10f;
    public LayerMask obstacleMask = Physics.DefaultRaycastLayers;
    [Range(2, 361)] public int rayCount = 19;
    [Range(1f, 270f)] public float fieldOfView = 90f;
    public bool logEachRay = false;
    // Runtime measurements, ordered from left to right (Unity yaw convention).
    // Infinity means no hit within maxDistance. Refilled every Update.
    public float[] ranges;
    private float nextLogTime;

    void Update()
    {
        Vector3 origin = transform.position;
        bool shouldLog = Time.time >= nextLogTime;
        if (shouldLog) nextLogTime = Time.time + 1f;
        int count = Mathf.Clamp(rayCount, 2, 361);
        float span = Mathf.Clamp(fieldOfView, 1f, 270f);
        float angleMin = -span / 2f;
        float angleStep = span / (count - 1); // angular resolution -> 3 laser create only 2 gaps btw them
        if (ranges == null || ranges.Length != count) ranges = new float[count];
        int hitCount = 0;

        for (int i = 0; i < count; i++)
        {
            // Tạo hướng tia và đo 
            // -45°, -40°, -35°, ... 0°, ... +35°, +40°, +45°
            float angle = angleMin + i * angleStep;
            // Unity yaw: negative turns left, positive turns right.
            Vector3 direction = Quaternion.AngleAxis(angle, transform.up)
                * transform.forward;

            bool detected = Physics.Raycast(
                origin, direction, out RaycastHit hit, maxDistance,
                obstacleMask, QueryTriggerInteraction.Ignore);
            ranges[i] = detected ? hit.distance : float.PositiveInfinity;
            if (detected) hitCount++;

            float drawnDistance = detected ? hit.distance : maxDistance;
            Debug.DrawRay(origin, direction * drawnDistance,
                detected ? Color.green : Color.red);

            if (!shouldLog || !logEachRay) continue;
            if (detected)
                Debug.Log($"[RayFan] Ray {i} ({angle:F1} deg): Hit {hit.collider.name}: {hit.distance:F3} m", this);
            else
                Debug.Log($"[RayFan] Ray {i} ({angle:F1} deg): No hit within {maxDistance:F3} m", this);
        }
        if (shouldLog)
            Debug.Log($"[RayFan] Rays={count}, FOV={span:F1} deg, Step={angleStep:F2} deg, Hits={hitCount}", this);
    }
}
