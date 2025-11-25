using UnityEngine;

public class NoteMover : MonoBehaviour
{
    private Vector3 start;
    private Vector3 end;
    private float duration;
    private float startTime;

    public void Initialize(Vector3 startPos, Vector3 endPos, float travelTime)
    {
        start = startPos;
        end = endPos;
        duration = travelTime;
        startTime = Time.time;
    }

    void Update()
    {
        float elapsed = Time.time - startTime;
        float percent = elapsed / duration;

        if (percent >= 1.0f)
        {
            // Note arrived at target (Player line)
            // Here you might check if Player missed it
            transform.position = end;
            Destroy(gameObject, 0.5f); // Destroy shortly after passing
        }
        else
        {
            transform.position = Vector3.Lerp(start, end, percent);
        }
    }
}