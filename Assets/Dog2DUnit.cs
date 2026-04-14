using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Dog2DUnit : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] private Color furColor = new Color(0.76f, 0.57f, 0.34f);
    [SerializeField] private Color bellyColor = new Color(0.94f, 0.86f, 0.68f);
    [SerializeField] private Color noseColor = new Color(0.12f, 0.1f, 0.1f);
    [SerializeField] private Color collarColor = new Color(0.82f, 0.18f, 0.18f);

    [Header("Animation")]
    [SerializeField] private bool animateIdle = true;
    [SerializeField] private float bounceAmount = 0.06f;
    [SerializeField] private float bounceSpeed = 3f;
    [SerializeField] private float tailSwingAngle = 28f;
    [SerializeField] private float tailSwingSpeed = 7f;
    [SerializeField] private float earFlopAngle = 8f;

    private Transform visualRoot;
    private Transform tail;
    private Transform leftEar;
    private Transform rightEar;
    private Vector3 baseRootPosition;
    private Quaternion baseTailRotation;
    private Quaternion baseLeftEarRotation;
    private Quaternion baseRightEarRotation;

    private static Sprite squareSprite;

    private void Reset()
    {
        BuildDog();
    }

    private void Awake()
    {
        CacheParts();

        if (visualRoot == null)
        {
            BuildDog();
        }
    }

    private void OnValidate()
    {
        BuildDog();
    }

    private void Update()
    {
        if (!animateIdle || visualRoot == null)
        {
            return;
        }

        float bounce = Mathf.Sin(Time.time * bounceSpeed) * bounceAmount;
        visualRoot.localPosition = baseRootPosition + new Vector3(0f, bounce, 0f);

        if (tail != null)
        {
            float tailAngle = Mathf.Sin(Time.time * tailSwingSpeed) * tailSwingAngle;
            tail.localRotation = baseTailRotation * Quaternion.Euler(0f, 0f, tailAngle);
        }

        if (leftEar != null)
        {
            float earAngle = Mathf.Sin(Time.time * bounceSpeed * 0.75f) * earFlopAngle;
            leftEar.localRotation = baseLeftEarRotation * Quaternion.Euler(0f, 0f, earAngle);
        }

        if (rightEar != null)
        {
            float earAngle = Mathf.Sin(Time.time * bounceSpeed * 0.75f + 0.5f) * earFlopAngle;
            rightEar.localRotation = baseRightEarRotation * Quaternion.Euler(0f, 0f, -earAngle);
        }
    }

    [ContextMenu("Rebuild Dog")]
    public void BuildDog()
    {
        RemoveOldVisuals();

        visualRoot = new GameObject("VisualRoot").transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        Transform body = CreatePart("Body", visualRoot, new Vector2(0f, 0f), new Vector2(1.45f, 0.78f), furColor, 0);
        CreatePart("Belly", visualRoot, new Vector2(0f, -0.12f), new Vector2(0.82f, 0.42f), bellyColor, 1);

        Transform head = CreatePart("Head", visualRoot, new Vector2(0.92f, 0.28f), new Vector2(0.72f, 0.62f), furColor, 2);
        CreatePart("Snout", head, new Vector2(0.38f, -0.06f), new Vector2(0.42f, 0.24f), bellyColor, 3);
        CreatePart("Nose", head, new Vector2(0.55f, -0.04f), new Vector2(0.1f, 0.1f), noseColor, 4);
        CreatePart("Eye", head, new Vector2(0.18f, 0.12f), new Vector2(0.08f, 0.14f), noseColor, 4);

        leftEar = CreatePart("LeftEar", head, new Vector2(-0.12f, 0.34f), new Vector2(0.16f, 0.34f), new Color(0.42f, 0.25f, 0.14f), 1);
        rightEar = CreatePart("RightEar", head, new Vector2(0.16f, 0.34f), new Vector2(0.16f, 0.34f), new Color(0.42f, 0.25f, 0.14f), 1);
        leftEar.localRotation = Quaternion.Euler(0f, 0f, 18f);
        rightEar.localRotation = Quaternion.Euler(0f, 0f, -12f);

        Transform collar = CreatePart("Collar", visualRoot, new Vector2(0.55f, 0.02f), new Vector2(0.16f, 0.44f), collarColor, 2);
        collar.localRotation = Quaternion.Euler(0f, 0f, 90f);
        CreatePart("Tag", visualRoot, new Vector2(0.7f, -0.16f), new Vector2(0.12f, 0.12f), new Color(0.95f, 0.82f, 0.18f), 3);

        CreatePart("FrontLegA", body, new Vector2(0.38f, -0.56f), new Vector2(0.2f, 0.62f), furColor, -1);
        CreatePart("FrontLegB", body, new Vector2(0.1f, -0.56f), new Vector2(0.2f, 0.62f), furColor, -2);
        CreatePart("BackLegA", body, new Vector2(-0.42f, -0.56f), new Vector2(0.22f, 0.62f), furColor, -1);
        CreatePart("BackLegB", body, new Vector2(-0.68f, -0.54f), new Vector2(0.22f, 0.58f), furColor, -2);

        tail = CreatePart("Tail", body, new Vector2(-0.9f, 0.22f), new Vector2(0.18f, 0.62f), furColor, -3);
        tail.localRotation = Quaternion.Euler(0f, 0f, 42f);

        CreatePart("Shadow", visualRoot, new Vector2(0f, -0.78f), new Vector2(1.1f, 0.18f), new Color(0f, 0f, 0f, 0.18f), -10);

        baseRootPosition = visualRoot.localPosition;
        baseTailRotation = tail != null ? tail.localRotation : Quaternion.identity;
        baseLeftEarRotation = leftEar != null ? leftEar.localRotation : Quaternion.identity;
        baseRightEarRotation = rightEar != null ? rightEar.localRotation : Quaternion.identity;
    }

    private Transform CreatePart(string partName, Transform parent, Vector2 localPosition, Vector2 localScale, Color color, int sortingOrder)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        part.transform.localScale = new Vector3(localScale.x, localScale.y, 1f);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSquareSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;

        return part.transform;
    }

    private void CacheParts()
    {
        visualRoot = transform.Find("VisualRoot");
        tail = visualRoot != null ? visualRoot.Find("Body/Tail") : null;
        leftEar = visualRoot != null ? visualRoot.Find("Head/LeftEar") : null;
        rightEar = visualRoot != null ? visualRoot.Find("Head/RightEar") : null;

        if (visualRoot != null)
        {
            baseRootPosition = visualRoot.localPosition;
        }

        if (tail != null)
        {
            baseTailRotation = tail.localRotation;
        }

        if (leftEar != null)
        {
            baseLeftEarRotation = leftEar.localRotation;
        }

        if (rightEar != null)
        {
            baseRightEarRotation = rightEar.localRotation;
        }
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
        {
            return squareSprite;
        }

        squareSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        squareSprite.name = "Dog2DSquareSprite";
        return squareSprite;
    }

    private void RemoveOldVisuals()
    {
        Transform existing = transform.Find("VisualRoot");
        if (existing == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(existing.gameObject);
        }
        else
        {
            DestroyImmediate(existing.gameObject);
        }
    }

#if UNITY_EDITOR
    [MenuItem("GameObject/2D Object/Demo Dog", false, 10)]
    private static void CreateDogFromMenu()
    {
        GameObject dog = new GameObject("Dog");
        Dog2DUnit unit = dog.AddComponent<Dog2DUnit>();
        unit.BuildDog();

        if (Selection.activeTransform != null)
        {
            dog.transform.SetParent(Selection.activeTransform, false);
        }

        Undo.RegisterCreatedObjectUndo(dog, "Create Demo Dog");
        Selection.activeGameObject = dog;
    }
#endif
}
