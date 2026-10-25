using UnityEngine;

public class DynamicObstacleMover : MonoBehaviour
{
    [Header("Movement Settings")]
    public float speed = 0.25f;
    public float distance = 4.0f;
    public bool moveHorizontally = true; // True for X-axis, False for Z-axis

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        // Calculate offset using a Sine wave for smooth back-and-forth motion
        float offset = Mathf.Sin(Time.time * speed) * distance;

        if (moveHorizontally)
        {
            transform.position = new Vector3(startPosition.x + offset, startPosition.y, startPosition.z);
        }
        else
        {
            transform.position = new Vector3(startPosition.x, startPosition.y, startPosition.z + offset);
        }
    }
}
