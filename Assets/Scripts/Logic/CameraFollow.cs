using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 8f, -8f);
    [Range(0.05f, 1f)] public float smoothDampTime = 0.5f;
    public bool useSmoothFollow = true;

    private Vector3 velocity = Vector3.zero;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desiredPosition = target.position + offset;

        if (useSmoothFollow)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothDampTime);
        }
        else
        {
            transform.position = desiredPosition;
            velocity = Vector3.zero;
        }

        transform.LookAt(target.position);
    }
}
