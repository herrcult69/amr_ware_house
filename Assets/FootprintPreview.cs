using UnityEngine;

// Attach to the moving pose reference: legacy base_link or XStack drive_centre.
// Draws the same padded rectangle as the selected ROS robot profile.
// Local Unity +Z = ROS +X; local Unity -X = ROS +Y. Unit scale required.
public class FootprintPreview : MonoBehaviour
{
    public float front = 0.35f;
    public float rear = 0.90f;
    public float halfWidth = 0.35f;
    public float padding = 0.02f;

    void OnDrawGizmos()
    {
        float w = halfWidth + padding;
        float f = front + padding;
        float r = rear + padding;
        Vector3[] corners = {
            new Vector3(-w, 0, f), new Vector3(-w, 0, -r),
            new Vector3(w, 0, -r), new Vector3(w, 0, f)
        };
        Gizmos.color = Color.cyan;
        for (int i = 0; i < corners.Length; i++)
            Gizmos.DrawLine(transform.TransformPoint(corners[i]),
                transform.TransformPoint(corners[(i + 1) % corners.Length]));
    }
}
