using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.45f, -10f);
    [SerializeField] private float smoothTime = 8f;
    [SerializeField] private float lookAheadDistance = 0.42f;
    [SerializeField] private float lookAheadResponse = 5.5f;

    private Transform target;
    private Rigidbody2D targetBody;
    private Vector3 currentLookAhead;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetBody = target != null ? target.GetComponent<Rigidbody2D>() : null;
        currentLookAhead = Vector3.zero;
        if (target != null)
        {
            transform.position = target.position + offset;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector2 velocity = targetBody != null ? targetBody.velocity : Vector2.zero;
        Vector3 desiredLookAhead = new Vector3(
            Mathf.Clamp(velocity.x, -1f, 1f) * lookAheadDistance,
            Mathf.Clamp(velocity.y, -1f, 1f) * lookAheadDistance * 0.35f,
            0f);
        float lookBlend = 1f - Mathf.Exp(-lookAheadResponse * Time.deltaTime);
        currentLookAhead = Vector3.Lerp(currentLookAhead, desiredLookAhead, lookBlend);

        float blend = 1f - Mathf.Exp(-smoothTime * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target.position + offset + currentLookAhead, blend);
    }
}
