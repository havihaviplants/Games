using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float smoothTime = 8f;

    private Transform target;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
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

        float blend = 1f - Mathf.Exp(-smoothTime * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, target.position + offset, blend);
    }
}
