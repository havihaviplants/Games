using UnityEngine;

[DisallowMultipleComponent]
public class SpriteCharacterPresentation2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer primaryRenderer;
    [SerializeField] private Rigidbody2D movementBody;
    [SerializeField] private bool createShadow = true;
    [SerializeField] private bool movementResponsive = true;
    [SerializeField] private float idleBobAmplitude = 0.018f;
    [SerializeField] private float idleBobSpeed = 2.1f;
    [SerializeField] private float moveBobAmplitude = 0.038f;
    [SerializeField] private float moveBobSpeed = 9.5f;
    [SerializeField] private float leanDistance = 0.018f;
    [SerializeField] private float stretchAmount = 0.06f;
    [SerializeField] private float shadowBaseWidth = 0.34f;
    [SerializeField] private float shadowBaseHeight = 0.12f;

    private static Sprite shadowSprite;

    private Transform spriteTransform;
    private SpriteRenderer shadowRenderer;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;
    private Vector3 previousWorldPosition;

    public void Configure(SpriteRenderer renderer, Rigidbody2D body = null, bool responsiveToMovement = true)
    {
        primaryRenderer = renderer;
        movementBody = body;
        movementResponsive = responsiveToMovement;
        CacheReferences();
        EnsureShadow();
    }

    private void Awake()
    {
        CacheReferences();
        EnsureShadow();
    }

    private void OnEnable()
    {
        previousWorldPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (spriteTransform == null)
        {
            CacheReferences();
        }

        if (spriteTransform == null)
        {
            return;
        }

        Vector2 velocity = ResolveVelocity();
        float speed = velocity.magnitude;
        bool moving = movementResponsive && speed > 0.05f;
        float bobSpeed = moving ? moveBobSpeed : idleBobSpeed;
        float bobAmount = moving ? moveBobAmplitude : idleBobAmplitude;
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;

        float horizontalLean = 0f;
        if (moving)
        {
            horizontalLean = Mathf.Clamp(velocity.x * 0.05f, -1f, 1f) * leanDistance;
        }

        spriteTransform.localPosition = baseLocalPosition + new Vector3(horizontalLean, bob, 0f);

        float squash = moving ? Mathf.Abs(Mathf.Sin(Time.time * bobSpeed)) * stretchAmount : 0f;
        spriteTransform.localScale = new Vector3(
            baseLocalScale.x * (1f - (squash * 0.35f)),
            baseLocalScale.y * (1f + squash),
            baseLocalScale.z);

        if (shadowRenderer != null)
        {
            shadowRenderer.transform.localPosition = new Vector3(horizontalLean * 0.45f, baseLocalPosition.y - 0.22f, 0.02f);
            float shadowStretch = moving ? 1f - (squash * 0.45f) : 1f;
            shadowRenderer.transform.localScale = new Vector3(shadowBaseWidth * shadowStretch, shadowBaseHeight * shadowStretch, 1f);
            shadowRenderer.color = new Color(0f, 0f, 0f, moving ? 0.24f : 0.18f);
            shadowRenderer.sortingOrder = primaryRenderer != null ? primaryRenderer.sortingOrder - 1 : 0;
        }

        previousWorldPosition = transform.position;
    }

    private Vector2 ResolveVelocity()
    {
        if (movementBody != null)
        {
            return movementBody.velocity;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - previousWorldPosition;
        return new Vector2(delta.x / deltaTime, delta.y / deltaTime);
    }

    private void CacheReferences()
    {
        if (primaryRenderer == null)
        {
            primaryRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody2D>();
        }

        if (primaryRenderer == null)
        {
            return;
        }

        spriteTransform = primaryRenderer.transform;
        baseLocalPosition = spriteTransform.localPosition;
        baseLocalScale = spriteTransform.localScale;
    }

    private void EnsureShadow()
    {
        if (!createShadow || primaryRenderer == null)
        {
            return;
        }

        Transform existing = primaryRenderer.transform.parent != null
            ? primaryRenderer.transform.parent.Find("CharacterShadow")
            : null;

        if (existing != null)
        {
            shadowRenderer = existing.GetComponent<SpriteRenderer>();
            return;
        }

        GameObject shadowObject = new GameObject("CharacterShadow");
        shadowObject.transform.SetParent(primaryRenderer.transform.parent, false);
        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, 0.18f);
        shadowRenderer.sortingOrder = primaryRenderer.sortingOrder - 1;
    }

    private static Sprite GetShadowSprite()
    {
        if (shadowSprite != null)
        {
            return shadowSprite;
        }

        const int width = 64;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "CharacterShadowSprite",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float radiusX = width * 0.42f;
        float radiusY = height * 0.38f;
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = (x - center.x) / radiusX;
                float ny = (y - center.y) / radiusY;
                float distance = (nx * nx) + (ny * ny);
                float alpha = Mathf.Clamp01(1f - distance);
                pixels[(y * width) + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        shadowSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        shadowSprite.name = "CharacterShadowSprite";
        return shadowSprite;
    }
}
