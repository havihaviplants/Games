using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SoldierUnit : MonoBehaviour
{
    [Header("Palette")]
    [SerializeField] private Color uniformColor = new Color(0.2f, 0.45f, 0.26f);
    [SerializeField] private Color skinColor = new Color(0.91f, 0.78f, 0.62f);
    [SerializeField] private Color gearColor = new Color(0.16f, 0.16f, 0.18f);

    [Header("Motion")]
    [SerializeField] private bool animateIdle = true;
    [SerializeField] private float idleBobHeight = 0.05f;
    [SerializeField] private float idleBobSpeed = 2.2f;
    [SerializeField] private float lookSweepAngle = 10f;
    [SerializeField] private float lookSweepSpeed = 1.5f;

    private Transform rootVisual;
    private Vector3 rootLocalPosition;
    private Quaternion rootLocalRotation;

    private void Reset()
    {
        BuildSoldier();
    }

    private void Awake()
    {
        CacheVisualRoot();

        if (rootVisual == null)
        {
            BuildSoldier();
        }
    }

    private void OnValidate()
    {
        BuildSoldier();
    }

    private void Update()
    {
        if (!animateIdle || rootVisual == null)
        {
            return;
        }

        float bobOffset = Mathf.Sin(Time.time * idleBobSpeed) * idleBobHeight;
        float sweep = Mathf.Sin(Time.time * lookSweepSpeed) * lookSweepAngle;

        rootVisual.localPosition = rootLocalPosition + new Vector3(0f, bobOffset, 0f);
        rootVisual.localRotation = rootLocalRotation * Quaternion.Euler(0f, sweep, 0f);
    }

    [ContextMenu("Rebuild Soldier")]
    public void BuildSoldier()
    {
        RemoveOldVisuals();

        rootVisual = new GameObject("VisualRoot").transform;
        rootVisual.SetParent(transform, false);
        rootLocalPosition = Vector3.zero;
        rootLocalRotation = Quaternion.identity;

        Transform hips = CreatePart("Hips", PrimitiveType.Cube, rootVisual, new Vector3(0f, 0.95f, 0f), new Vector3(0.55f, 0.32f, 0.28f), uniformColor);
        Transform torso = CreatePart("Torso", PrimitiveType.Cube, rootVisual, new Vector3(0f, 1.42f, 0f), new Vector3(0.75f, 0.72f, 0.34f), uniformColor);
        Transform chestRig = CreatePart("Vest", PrimitiveType.Cube, torso, new Vector3(0f, 0.04f, 0.16f), new Vector3(0.8f, 0.8f, 0.18f), gearColor);
        chestRig.localRotation = Quaternion.Euler(8f, 0f, 0f);

        CreatePart("Head", PrimitiveType.Sphere, rootVisual, new Vector3(0f, 2.02f, 0f), new Vector3(0.42f, 0.48f, 0.42f), skinColor);
        Transform helmet = CreatePart("Helmet", PrimitiveType.Cylinder, rootVisual, new Vector3(0f, 2.18f, 0f), new Vector3(0.34f, 0.12f, 0.34f), gearColor);
        helmet.localRotation = Quaternion.Euler(0f, 0f, 90f);

        CreatePart("LeftArm", PrimitiveType.Capsule, rootVisual, new Vector3(-0.58f, 1.42f, 0f), new Vector3(0.18f, 0.42f, 0.18f), uniformColor);
        CreatePart("RightArm", PrimitiveType.Capsule, rootVisual, new Vector3(0.58f, 1.42f, 0f), new Vector3(0.18f, 0.42f, 0.18f), uniformColor);
        CreatePart("LeftLeg", PrimitiveType.Capsule, rootVisual, new Vector3(-0.18f, 0.46f, 0f), new Vector3(0.22f, 0.56f, 0.22f), uniformColor);
        CreatePart("RightLeg", PrimitiveType.Capsule, rootVisual, new Vector3(0.18f, 0.46f, 0f), new Vector3(0.22f, 0.56f, 0.22f), uniformColor);

        Transform rifle = CreatePart("Rifle", PrimitiveType.Cube, rootVisual, new Vector3(0.42f, 1.22f, 0.32f), new Vector3(0.12f, 0.12f, 1.1f), gearColor);
        rifle.localRotation = Quaternion.Euler(75f, 18f, 0f);

        CreatePart("Backpack", PrimitiveType.Cube, rootVisual, new Vector3(0f, 1.42f, -0.24f), new Vector3(0.52f, 0.58f, 0.22f), gearColor);

        rootLocalPosition = rootVisual.localPosition;
        rootLocalRotation = rootVisual.localRotation;
    }

    private Transform CreatePart(string partName, PrimitiveType primitiveType, Transform parent, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(primitiveType);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = CreateMaterial(color, partName + "Mat");
        }

        Collider collider = part.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        return part.transform;
    }

    private Material CreateMaterial(Color color, string materialName)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        return material;
    }

    private void CacheVisualRoot()
    {
        rootVisual = transform.Find("VisualRoot");
        if (rootVisual != null)
        {
            rootLocalPosition = rootVisual.localPosition;
            rootLocalRotation = rootVisual.localRotation;
        }
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
    [MenuItem("GameObject/3D Object/Demo Soldier", false, 10)]
    private static void CreateSoldierFromMenu()
    {
        GameObject soldier = new GameObject("Soldier");
        SoldierUnit unit = soldier.AddComponent<SoldierUnit>();
        unit.BuildSoldier();

        if (Selection.activeTransform != null)
        {
            soldier.transform.SetParent(Selection.activeTransform, true);
            soldier.transform.localPosition = Vector3.zero;
        }

        Undo.RegisterCreatedObjectUndo(soldier, "Create Demo Soldier");
        Selection.activeGameObject = soldier;
    }
#endif
}
