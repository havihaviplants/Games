using UnityEngine;

public class Dog2DController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool faceMoveDirection = true;

    private Dog2DUnit dogVisual;

    private void Awake()
    {
        dogVisual = GetComponent<Dog2DUnit>();
    }

    private void Update()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");

        Vector3 move = new Vector3(moveX, moveY, 0f);

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        transform.position += move * moveSpeed * Time.deltaTime;

        if (faceMoveDirection && Mathf.Abs(move.x) > 0.01f)
        {
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * Mathf.Sign(move.x);
            transform.localScale = scale;
        }
    }
}
