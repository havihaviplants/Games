using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownPlayerController2D : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.8f;

    private Rigidbody2D body;
    private SpriteRenderer visual;
    private Vector2 movement;
    private Sprite[] directionalSprites;
    private FacingDirection facing = FacingDirection.Down;

    public Rigidbody2D MovementBody => body;

    private enum FacingDirection
    {
        Down = 0,
        Up = 1,
        Left = 2,
        Right = 3
    }

    public void Configure(SpriteRenderer renderer)
    {
        visual = renderer;
    }

    public void Configure(SpriteRenderer renderer, Sprite[] sprites)
    {
        visual = renderer;
        directionalSprites = sprites;
        ApplyFacingSprite();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (visual == null)
        {
            visual = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        movement = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (movement.sqrMagnitude > 1f)
        {
            movement.Normalize();
        }

        if (visual != null)
        {
            UpdateFacingFromMovement();
            ApplyFacingSprite();
            visual.sortingOrder = 500 + Mathf.RoundToInt(-transform.position.y * 100f);
        }
    }

    private void FixedUpdate()
    {
        body.MovePosition(body.position + (movement * moveSpeed * Time.fixedDeltaTime));
    }

    private void UpdateFacingFromMovement()
    {
        if (movement.sqrMagnitude < 0.001f)
        {
            return;
        }

        if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
        {
            facing = movement.x >= 0f ? FacingDirection.Right : FacingDirection.Left;
        }
        else
        {
            facing = movement.y >= 0f ? FacingDirection.Up : FacingDirection.Down;
        }
    }

    private void ApplyFacingSprite()
    {
        if (visual == null || directionalSprites == null || directionalSprites.Length < 4)
        {
            return;
        }

        visual.flipX = false;
        visual.sprite = facing switch
        {
            FacingDirection.Up => directionalSprites[1],
            FacingDirection.Left => directionalSprites[2],
            FacingDirection.Right => directionalSprites[3],
            _ => directionalSprites[0]
        };
    }
}
