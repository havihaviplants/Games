using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

[ExecuteAlways]
public class IsometricFarmVillageBuilder : MonoBehaviour
{
    [FormerlySerializedAs("villageOffset")]
    [SerializeField] private Vector3 stageOffset = Vector3.zero;
    [SerializeField] private bool alignMainCamera = true;
    [SerializeField] private bool cleanupLegacySceneObjects = true;
    [SerializeField] private bool autoPlayDemo = true;
    [SerializeField] private float demoStepDuration = 2.4f;
    [SerializeField] private bool buildRpgVillageDemo = true;
    [SerializeField] private bool buildKenneyTownDemo = false;

    private readonly Dictionary<Color, Material> colorMaterials = new Dictionary<Color, Material>();
    private readonly Dictionary<string, Texture2D> textureCache = new Dictionary<string, Texture2D>();
    private readonly Dictionary<string, Sprite[]> spriteCache = new Dictionary<string, Sprite[]>();
    private readonly Dictionary<string, TileBase> runtimeTileCache = new Dictionary<string, TileBase>();
    private readonly Dictionary<string, Sprite> generatedSpriteCache = new Dictionary<string, Sprite>();
    private readonly Dictionary<Transform, Vector3> basePositions = new Dictionary<Transform, Vector3>();

    private Transform battleRigRoot;
    private Transform heroRoot;
    private Transform enemyRedRoot;
    private Transform enemyGoldRoot;
    private Transform enemyRearLeftRoot;
    private Transform enemyRearRightRoot;
    private Transform[] cardRoots;
    private SpriteRenderer heroRenderer;
    private SpriteRenderer enemyRedRenderer;
    private SpriteRenderer enemyGoldRenderer;
    private SpriteRenderer enemyRearLeftRenderer;
    private SpriteRenderer enemyRearRightRenderer;
    private Camera cachedCamera;
    private float demoTimer;
    private int lastCardIndex = -1;
    private CardType activeCard = CardType.RapidBloom;
    private ReactionType redReaction = ReactionType.Brace;
    private ReactionType goldReaction = ReactionType.SplitFlank;
    private readonly int[] playerCardHistory = new int[3];
    private string decisionLog = "Art-driven AI demo ready";
    private float battleShake;
    private float actionTimer;
    private const float ActionDuration = 1.05f;

    private const float PixelsPerUnit = 190f;
    private const string HeroSheet = "Pixel art soldier sprite sheet.png";
    private const string EnemyRedSheet = "Medieval knight sprite sheet.png";
    private const string EnemyGoldSheet = "Pixel knight sprite sheet in four views.png";
    private const string KeyArt = "Twilight duel in a village arena.png";
    private const string StageArt = "Isometric stone platform with lanterns.png";
    private const string RpgFolder = "kenney_rpg-base";
    private const string KenneyFolder = "kenney_isometric-buildings";

    private enum CardType
    {
        RapidBloom,
        CrescentDrive,
        BreakerSigil
    }

    private enum ReactionType
    {
        Brace,
        Sidestep,
        SplitFlank,
        CounterLunge,
        Retreat
    }

    private enum SceneKind
    {
        StartHub,
        Blacksmith,
        PetStorage,
        Warehouse,
        Village1,
        Village2,
        Village3,
        HuntingVillage,
        Village1HuntA,
        Village1HuntB,
        Village2HuntA,
        Village2HuntB,
        Village3HuntA,
        Village3HuntB
    }

    private readonly struct WeightedReaction
    {
        public WeightedReaction(ReactionType reaction, float weight)
        {
            Reaction = reaction;
            Weight = weight;
        }

        public ReactionType Reaction { get; }
        public float Weight { get; }
    }

    private readonly struct PondEdgePlacement
    {
        public PondEdgePlacement(TileBase tile, float rotationZ)
        {
            Tile = tile;
            RotationZ = rotationZ;
        }

        public TileBase Tile { get; }
        public float RotationZ { get; }
    }

    private void OnEnable()
    {
        BuildVillage();
    }

    private void OnValidate()
    {
        BuildVillage();
    }

    [ContextMenu("Rebuild Art Demo")]
    public void BuildVillage()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (cleanupLegacySceneObjects)
        {
            CleanupSceneRoots();
        }

        Transform existing = transform.Find("GeneratedVillage");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        Transform root = new GameObject("GeneratedVillage").transform;
        root.SetParent(transform, false);
        root.localPosition = stageOffset;

        if (buildRpgVillageDemo && Directory.Exists(Path.Combine(Application.dataPath, RpgFolder, "PNG")))
        {
            BuildRpgVillage(root);
        }
        else if (buildKenneyTownDemo && Directory.Exists(Path.Combine(Application.dataPath, KenneyFolder, "PNG")))
        {
            BuildKenneyTown(root);
        }
        else
        {
            BuildBackdrop(root);
            battleRigRoot = null;
            BuildStage(root);
            BuildCharacters(root);
            BuildCards(root);
        }

        CacheBasePositions(root);
        ConfigureLighting();

        if (alignMainCamera)
        {
            float focusYOffset = buildRpgVillageDemo ? 0.1f : buildKenneyTownDemo ? 0.9f : -0.2f;
            ConfigureCamera(root.position + new Vector3(0f, focusYOffset, 0f));
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            if (transform.Find("GeneratedVillage") == null)
            {
                BuildVillage();
            }

            FaceSpritesToCamera();
            return;
        }

        if (buildRpgVillageDemo)
        {
            return;
        }

        if (heroRoot == null || enemyRedRoot == null || enemyGoldRoot == null)
        {
            Transform root = transform.Find("GeneratedVillage");
            if (root != null)
            {
                CacheBasePositions(root);
            }
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            TriggerCard(CardType.RapidBloom);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            TriggerCard(CardType.CrescentDrive);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            TriggerCard(CardType.BreakerSigil);
        }

        if (autoPlayDemo)
        {
            demoTimer += Time.deltaTime;
            if (demoTimer >= demoStepDuration)
            {
                demoTimer = 0f;
                TriggerCard((CardType)((lastCardIndex + 1) % 3));
            }
        }

        actionTimer = Mathf.Max(0f, actionTimer - Time.deltaTime);
        AnimateActors(Time.time);
        FaceSpritesToCamera();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || buildRpgVillageDemo)
        {
            return;
        }

        GUI.color = new Color(1f, 1f, 1f, 0.96f);
        GUI.Box(new Rect(18f, 18f, 470f, 120f), "Reactive AI Card Demo");
        GUI.Label(new Rect(34f, 50f, 430f, 24f), "1 Rapid Bloom   2 Crescent Drive   3 Breaker Sigil");
        GUI.Label(new Rect(34f, 76f, 430f, 24f), $"Current skill: {CardLabel(activeCard)}");
        GUI.Label(new Rect(34f, 102f, 430f, 24f), $"AI log: {decisionLog}");

        float buttonY = Screen.height - 92f;
        float buttonWidth = 180f;
        float gap = 16f;
        float startX = (Screen.width - ((buttonWidth * 3f) + (gap * 2f))) * 0.5f;

        if (GUI.Button(new Rect(startX, buttonY, buttonWidth, 50f), "Rapid Bloom"))
        {
            TriggerCard(CardType.RapidBloom);
        }

        if (GUI.Button(new Rect(startX + buttonWidth + gap, buttonY, buttonWidth, 50f), "Crescent Drive"))
        {
            TriggerCard(CardType.CrescentDrive);
        }

        if (GUI.Button(new Rect(startX + ((buttonWidth + gap) * 2f), buttonY, buttonWidth, 50f), "Breaker Sigil"))
        {
            TriggerCard(CardType.BreakerSigil);
        }
    }

    private void BuildBackdrop(Transform parent)
    {
        Texture2D keyArt = LoadTexture(KeyArt);
        if (keyArt != null)
        {
            Sprite sprite = CreateFullSprite("FullBackdrop", keyArt);
            SpriteRenderer background = CreateSprite("Backdrop", sprite, new Vector3(0f, 0.2f, 2.8f), Vector3.one * 1.62f, parent, -50);
            background.color = Color.white;
        }
        else
        {
            CreateFlat("SkyFallback", new Vector3(0f, 0.2f, 2.6f), new Vector3(18f, 10f, 0.08f), new Color(0.98f, 0.91f, 0.80f), parent, 0, -40);
        }

        CreateFlat("BottomVignette", new Vector3(0f, -4.85f, 2.3f), new Vector3(18f, 2.25f, 0.08f), new Color(0.16f, 0.13f, 0.12f), parent, 0, 40);
        CreateFlat("BottomGlow", new Vector3(0f, -4.05f, 2.29f), new Vector3(16f, 1.2f, 0.08f), new Color(0.38f, 0.28f, 0.18f), parent, 0, 41);
    }

    private void BuildStage(Transform parent)
    {
        Texture2D stageArt = LoadTexture(StageArt);
        if (stageArt != null)
        {
            Sprite sprite = CreateFullSprite("StageSprite", stageArt);
            SpriteRenderer renderer = CreateSprite("StageBackdrop", sprite, new Vector3(0f, -2.05f, 1.12f), Vector3.one * 0.92f, parent, 5);
            renderer.color = Color.white;
        }
        else
        {
            CreateFlat("BattleFocusShadow", new Vector3(0.05f, -2.08f, 1.18f), new Vector3(8.0f, 2.7f, 0.08f), new Color(0.30f, 0.24f, 0.20f), parent, 0, 5);
            CreateFlat("BattleFocusGlow", new Vector3(0f, -1.90f, 1.16f), new Vector3(7.0f, 2.15f, 0.08f), new Color(0.83f, 0.73f, 0.58f), parent, 0, 6);
            CreateFlat("BattleFocusInner", new Vector3(0f, -1.90f, 1.15f), new Vector3(5.7f, 1.55f, 0.08f), new Color(0.62f, 0.52f, 0.41f), parent, 0, 7);
        }

        CreateFlat("BattleFocusFront", new Vector3(0f, -2.92f, 1.14f), new Vector3(5.5f, 0.38f, 0.08f), new Color(0.62f, 0.52f, 0.40f), parent, 0, 36);
    }

    private void BuildCharacters(Transform parent)
    {
        heroRoot = CreateGroup("Hero", new Vector3(0f, -1.88f, 1f), parent);
        enemyRedRoot = CreateGroup("EnemyRed", new Vector3(-2.75f, -1.02f, 1f), parent);
        enemyGoldRoot = CreateGroup("EnemyGold", new Vector3(2.75f, -1.02f, 1f), parent);
        enemyRearLeftRoot = null;
        enemyRearRightRoot = null;

        heroRenderer = CreateSpriteCharacter(heroRoot, HeroSheet, 0, 32);
        enemyRedRenderer = CreateSpriteCharacter(enemyRedRoot, EnemyRedSheet, 0, 30);
        enemyGoldRenderer = CreateSpriteCharacter(enemyGoldRoot, EnemyGoldSheet, 0, 31);
        enemyRearLeftRenderer = null;
        enemyRearRightRenderer = null;

        if (heroRenderer != null)
        {
            heroRenderer.flipX = false;
        }

        if (enemyRedRenderer != null)
        {
            enemyRedRenderer.flipX = false;
        }

        if (enemyGoldRenderer != null)
        {
            enemyGoldRenderer.flipX = true;
        }

        CreateFlat("HeroShadow", heroRoot.localPosition + new Vector3(0f, -0.62f, -0.02f), new Vector3(0.92f, 0.16f, 0.04f), new Color(0.25f, 0.20f, 0.17f), parent, 0, 20);
        CreateFlat("RedShadow", enemyRedRoot.localPosition + new Vector3(0f, -0.62f, -0.02f), new Vector3(0.88f, 0.14f, 0.04f), new Color(0.25f, 0.20f, 0.17f), parent, 0, 20);
        CreateFlat("GoldShadow", enemyGoldRoot.localPosition + new Vector3(0f, -0.62f, -0.02f), new Vector3(0.88f, 0.14f, 0.04f), new Color(0.25f, 0.20f, 0.17f), parent, 0, 20);
    }

    private void BuildCards(Transform parent)
    {
        cardRoots = new Transform[3];
        Color[] bodyColors =
        {
            new Color(0.28f, 0.54f, 0.95f),
            new Color(0.92f, 0.46f, 0.23f),
            new Color(0.95f, 0.76f, 0.23f)
        };

        for (int i = 0; i < 3; i++)
        {
            float x = -2.7f + (i * 2.7f);
            float y = i == 1 ? -4.88f : -4.66f;
            Transform card = CreateGroup($"Card_{i}", new Vector3(x, y, 0.8f), parent);
            cardRoots[i] = card;
            CreateFlat("Body", Vector3.zero, new Vector3(1.52f, 0.66f, 0.06f), new Color(0.10f, 0.09f, 0.10f), card, 0, 50);
            CreateFlat("Inset", new Vector3(0f, 0.02f, -0.01f), new Vector3(1.38f, 0.50f, 0.06f), bodyColors[i], card, 0, 51);
            CreateFlat("Badge", new Vector3(-0.56f, 0f, -0.02f), new Vector3(0.26f, 0.26f, 0.06f), new Color(1f, 0.96f, 0.86f), card, 0, 52);
            CreateFlat("SkillPip", new Vector3(0.58f, 0f, -0.02f), new Vector3(0.16f, 0.16f, 0.06f), new Color(0.18f, 0.14f, 0.12f), card, 0, 52);
        }
    }

    private void BuildKenneyTown(Transform parent)
    {
        cardRoots = null;
        heroRoot = null;
        enemyRedRoot = null;
        enemyGoldRoot = null;
        battleRigRoot = null;

        CreateFlat("KenneySky", new Vector3(0f, 0.3f, 3f), new Vector3(28f, 16f, 0.08f), new Color(0.31f, 0.52f, 0.78f), parent, 0f, -100);
        CreateFlat("KenneyGroundFade", new Vector3(0f, -5.3f, 2.9f), new Vector3(24f, 2.8f, 0.08f), new Color(0.18f, 0.24f, 0.32f), parent, 0f, -20);

        Transform town = CreateGroup("KenneyTown", new Vector3(0f, 0.05f, 0f), parent);

        int rows = 10;
        int cols = 11;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int tileId = 102;
                bool centralRoad = row == 4 || row == 5 || col == 5;
                bool plaza = row >= 3 && row <= 6 && col >= 4 && col <= 6;
                bool outerEdge = row == 0 || row == rows - 1 || col == 0 || col == cols - 1;
                bool innerKeepRing = row >= 1 && row <= 3 && col >= 3 && col <= 7;

                if (plaza)
                {
                    tileId = 120;
                }
                else if (innerKeepRing)
                {
                    tileId = 94;
                }
                else if (centralRoad)
                {
                    tileId = 127;
                }
                else if (outerEdge)
                {
                    tileId = 94;
                }

                CreateKenneyTileSprite(town, row, col, tileId, 10 + (row * 12) + col);
            }
        }

        for (int col = 1; col < cols - 1; col++)
        {
            if (col == 5)
            {
                continue;
            }

            CreateKenneyTileSprite(town, 0, col, 114, 140 + col, new Vector3(0f, 0.02f, 0f));
            CreateKenneyTileSprite(town, rows - 1, col, 106, 160 + col, new Vector3(0f, 0.02f, 0f));
        }

        for (int row = 1; row < rows - 1; row++)
        {
            CreateKenneyTileSprite(town, row, 0, 25, 180 + row, new Vector3(0f, 0.02f, 0f));
            CreateKenneyTileSprite(town, row, cols - 1, 113, 200 + row, new Vector3(0f, 0.02f, 0f));
        }

        CreateKenneyTileSprite(town, 0, 0, 99, 230);
        CreateKenneyTileSprite(town, 0, cols - 1, 123, 231);
        CreateKenneyTileSprite(town, rows - 1, 0, 1, 232);
        CreateKenneyTileSprite(town, rows - 1, cols - 1, 116, 233);

        CreateKenneyTileSprite(town, rows - 1, 5, 117, 240, new Vector3(-0.36f, -0.04f, 0f));
        CreateKenneyTileSprite(town, rows - 1, 5, 127, 241, new Vector3(0.36f, -0.04f, 0f));
        CreateKenneyTileSprite(town, rows - 2, 5, 127, 242, new Vector3(0f, -0.02f, 0f));

        CreateKenneyTileSprite(town, 1, 3, 106, 250);
        CreateKenneyTileSprite(town, 1, 4, 114, 251);
        CreateKenneyTileSprite(town, 1, 5, 25, 252);
        CreateKenneyTileSprite(town, 1, 6, 113, 253);
        CreateKenneyTileSprite(town, 1, 7, 99, 254);

        CreateKenneyTileSprite(town, 2, 4, 25, 260);
        CreateKenneyTileSprite(town, 2, 5, 106, 261);
        CreateKenneyTileSprite(town, 2, 6, 113, 262);
        CreateKenneyTileSprite(town, 3, 5, 99, 263);

        CreateKenneyTileSprite(town, 6, 2, 117, 270);
        CreateKenneyTileSprite(town, 6, 8, 116, 271);
        CreateKenneyTileSprite(town, 7, 3, 1, 272);
        CreateKenneyTileSprite(town, 7, 7, 123, 273);
        CreateKenneyTileSprite(town, 8, 4, 99, 274);
        CreateKenneyTileSprite(town, 8, 6, 106, 275);

        CreateKenneyTileSprite(town, 4, 3, 118, 280, new Vector3(0f, 0.06f, -0.01f));
        CreateKenneyTileSprite(town, 6, 4, 127, 281, new Vector3(0f, 0.06f, -0.01f));
        CreateKenneyTileSprite(town, 6, 6, 94, 282, new Vector3(0f, 0.02f, -0.01f));

        CreateKenneyBanner(town, 0, 2, new Color(0.82f, 0.20f, 0.18f), 320);
        CreateKenneyBanner(town, 0, 8, new Color(0.82f, 0.20f, 0.18f), 321);
        CreateKenneyBanner(town, 2, 3, new Color(0.95f, 0.84f, 0.35f), 322);
        CreateKenneyBanner(town, 2, 7, new Color(0.95f, 0.84f, 0.35f), 323);

        CreateKenneyStall(town, 5, 3, new Color(0.93f, 0.44f, 0.31f), 340);
        CreateKenneyStall(town, 5, 7, new Color(0.28f, 0.62f, 0.88f), 341);
        CreateKenneyStall(town, 7, 5, new Color(0.94f, 0.74f, 0.23f), 342);

        CreateKenneyTree(town, 8, 2, 350);
        CreateKenneyTree(town, 8, 8, 351);
        CreateKenneyTree(town, 1, 1, 352);
        CreateKenneyTree(town, 1, 9, 353);

        BuildKenneyUnits(town);
    }

    private void BuildRpgVillage(Transform parent)
    {
        SceneKind sceneKind = GetCurrentSceneKind();

        cardRoots = null;
        battleRigRoot = null;
        heroRoot = null;
        enemyRedRoot = null;
        enemyGoldRoot = null;

        CreateFlat("RpgSkyFallback", new Vector3(0f, 4.35f, 3f), new Vector3(34f, 8f, 0.08f), new Color(0.44f, 0.74f, 0.90f), parent, 0f, -100);

        Transform village = CreateGroup("RpgTilemapVillage", Vector3.zero, parent);
        Grid grid = village.gameObject.AddComponent<Grid>();
        grid.cellLayout = GridLayout.CellLayout.Rectangle;
        float tileWorldSize = GetRpgTileWorldSize();
        grid.cellSize = new Vector3(tileWorldSize, tileWorldSize, 1f);
        grid.cellGap = Vector3.zero;

        Tilemap groundMap = CreateTilemapLayer(village, "Ground", -20);
        Tilemap pathMap = CreateTilemapLayer(village, "Path", -10);
        Tilemap waterMap = CreateTilemapLayer(village, "Water", -5, true);
        Tilemap detailMap = CreateTilemapLayer(village, "Details", 5);
        Tilemap obstacleMap = CreateTilemapLayer(village, "Obstacles", 25, true);

        BuildSceneTilemaps(sceneKind, groundMap, pathMap, waterMap, detailMap, obstacleMap);
        BuildScenePortals(parent, grid, sceneKind, detailMap, obstacleMap);
        BuildRpgActors(parent, grid, sceneKind);
    }

    private SceneKind GetCurrentSceneKind()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName switch
        {
            "BlacksmithScene" => SceneKind.Blacksmith,
            "PetStorageScene" => SceneKind.PetStorage,
            "WarehouseScene" => SceneKind.Warehouse,
            "Village1Scene" => SceneKind.Village1,
            "Village2Scene" => SceneKind.Village2,
            "Village3Scene" => SceneKind.Village3,
            "HuntingVillageScene" => SceneKind.HuntingVillage,
            "Village1HuntAScene" => SceneKind.Village1HuntA,
            "Village1HuntBScene" => SceneKind.Village1HuntB,
            "Village2HuntAScene" => SceneKind.Village2HuntA,
            "Village2HuntBScene" => SceneKind.Village2HuntB,
            "Village3HuntAScene" => SceneKind.Village3HuntA,
            "Village3HuntBScene" => SceneKind.Village3HuntB,
            _ => SceneKind.StartHub
        };
    }

    private void BuildSceneTilemaps(SceneKind sceneKind, Tilemap groundMap, Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        BuildRpgGroundTilemap(groundMap);

        switch (sceneKind)
        {
            case SceneKind.Blacksmith:
                BuildBlacksmithLayout(pathMap, detailMap, obstacleMap);
                break;
            case SceneKind.PetStorage:
                BuildPetStorageLayout(pathMap, detailMap, obstacleMap);
                break;
            case SceneKind.Warehouse:
                BuildWarehouseLayout(pathMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village1:
                BuildVillageOneLayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village2:
                BuildVillageTwoLayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village3:
                BuildVillageThreeLayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.HuntingVillage:
                BuildHuntingVillageLayout(pathMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village1HuntA:
                BuildVillageOneHuntALayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village1HuntB:
                BuildVillageOneHuntBLayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village2HuntA:
                BuildVillageTwoHuntALayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village2HuntB:
                BuildVillageTwoHuntBLayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village3HuntA:
                BuildVillageThreeHuntALayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            case SceneKind.Village3HuntB:
                BuildVillageThreeHuntBLayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
            default:
                BuildStartHubLayout(pathMap, waterMap, detailMap, obstacleMap);
                break;
        }

        EnsureScenePlayable(sceneKind, pathMap, waterMap, detailMap, obstacleMap);
    }

    private void BuildStartHubLayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        BuildRpgPathTilemap(pathMap);
        PaintFilledRect(pathMap, new RectInt(-7, 2, 9, 3), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(4, 2, 11, 3), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(10, -6, 3, 10), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-2, -7, 7, 3), "rpgTile024.png");

        BuildRpgPondTilemap(waterMap, detailMap);
        BuildRpgDetailTilemap(detailMap);
        BuildRpgObstacleTilemap(obstacleMap, detailMap);

        PlaceBuilding(obstacleMap, new Vector3Int(1, 3, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(6, 3, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(11, 3, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(13, -5, 0));
    }

    private void BuildBlacksmithLayout(Tilemap pathMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-6, -6, 13, 10), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-1, -10, 3, 5), "rpgTile024.png");

        PlaceBuilding(obstacleMap, new Vector3Int(-2, 1, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(2, 1, 0));
        PlaceRock(obstacleMap, -4, 0);
        PlaceRock(obstacleMap, -3, 0);
        PlaceRock(obstacleMap, 5, 0);
        PlaceRock(obstacleMap, 6, 0);
        PlaceFence(obstacleMap, new Vector3Int(-5, 4, 0), 12, true);
        PlaceFence(obstacleMap, new Vector3Int(-5, -2, 0), 12, true);

        PaintFlowerCluster(detailMap, new Vector3Int(-4, -4, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(5, -4, 0));
    }

    private void BuildPetStorageLayout(Tilemap pathMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-5, -8, 10, 4), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-2, -4, 4, 8), "rpgTile024.png");

        PlaceFence(obstacleMap, new Vector3Int(-7, 5, 0), 14, true);
        PlaceFence(obstacleMap, new Vector3Int(-7, -1, 0), 14, true);
        PlaceFence(obstacleMap, new Vector3Int(-7, 5, 0), 6, false);
        PlaceFence(obstacleMap, new Vector3Int(6, 5, 0), 6, false);
        PlaceTree(obstacleMap, -4, 7, "rpgTile179.png");
        PlaceTree(obstacleMap, 4, 7, "rpgTile195.png");
        PlaceRock(obstacleMap, -1, 2);
        PlaceRock(obstacleMap, 1, 2);

        PaintFlowerCluster(detailMap, new Vector3Int(-3, 3, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(3, 3, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(0, 0, 0));
    }

    private void BuildWarehouseLayout(Tilemap pathMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-7, -7, 15, 12), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-1, -10, 3, 4), "rpgTile024.png");

        PlaceBuilding(obstacleMap, new Vector3Int(-5, 1, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(-1, 1, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(3, 1, 0));
        PlaceRock(obstacleMap, -5, -2);
        PlaceRock(obstacleMap, -4, -2);
        PlaceRock(obstacleMap, 4, -2);
        PlaceRock(obstacleMap, 5, -2);
        PlaceFence(obstacleMap, new Vector3Int(-6, 5, 0), 13, true);

        PaintFlowerCluster(detailMap, new Vector3Int(-6, -4, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(6, -4, 0));
    }

    private void BuildVillageOneLayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-8, -8, 17, 4), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-1, -4, 4, 12), "rpgTile024.png");
        BuildRpgPondTilemap(waterMap, detailMap);

        PlaceBuilding(obstacleMap, new Vector3Int(4, 1, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(8, 1, 0));
        PlaceTree(obstacleMap, -7, 8, "rpgTile195.png");
        PlaceTree(obstacleMap, 12, 8, "rpgTile179.png");
        PaintFlowerCluster(detailMap, new Vector3Int(5, -2, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(8, -2, 0));
    }

    private void BuildVillageTwoLayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-12, -2, 24, 3), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(9, -9, 3, 12), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-10, -9, 4, 8), "rpgTile024.png");

        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(-10f, 5f), 3.8f);

        PlaceBuilding(obstacleMap, new Vector3Int(-3, 2, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(2, 2, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(12, -6, 0));
        PlaceTree(obstacleMap, 14, 7, "rpgTile179.png");
        PlaceTree(obstacleMap, 7, 7, "rpgTile195.png");
        PlaceRock(obstacleMap, 6, -4);
        PlaceRock(obstacleMap, 7, -4);
        PaintFlowerCluster(detailMap, new Vector3Int(-6, 3, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(6, 4, 0));
    }

    private void BuildVillageThreeLayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-10, -8, 20, 4), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-2, -4, 5, 11), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(7, 1, 7, 3), "rpgTile024.png");

        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(-12f, 4f), 3.4f);

        PlaceBuilding(obstacleMap, new Vector3Int(5, 2, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(10, 2, 0));
        PlaceTree(obstacleMap, -8, 7, "rpgTile179.png");
        PlaceTree(obstacleMap, -5, 8, "rpgTile195.png");
        PlaceRock(obstacleMap, 2, -2);
        PlaceRock(obstacleMap, 3, -2);
        PaintFlowerCluster(detailMap, new Vector3Int(-3, 4, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(8, -1, 0));
    }

    private void BuildHuntingVillageLayout(Tilemap pathMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-4, -9, 8, 14), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-10, -2, 20, 3), "rpgTile024.png");

        PlaceTree(obstacleMap, -10, 8, "rpgTile179.png");
        PlaceTree(obstacleMap, -7, 7, "rpgTile195.png");
        PlaceTree(obstacleMap, 8, 8, "rpgTile179.png");
        PlaceTree(obstacleMap, 11, 7, "rpgTile195.png");
        PlaceBuilding(obstacleMap, new Vector3Int(-1, 2, 0));
        PlaceRock(obstacleMap, -6, -4);
        PlaceRock(obstacleMap, -5, -4);
        PlaceRock(obstacleMap, 5, -4);
        PlaceRock(obstacleMap, 6, -4);
        PlaceFence(obstacleMap, new Vector3Int(-8, 3, 0), 4, true);
        PlaceFence(obstacleMap, new Vector3Int(4, 3, 0), 4, true);

        PaintFlowerCluster(detailMap, new Vector3Int(-3, -1, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(3, -1, 0));
    }

    private void EnsureScenePlayable(SceneKind sceneKind, Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        Vector3Int startCell = GetDefaultSpawnCell(sceneKind);
        EnsureWalkableNode(pathMap, waterMap, detailMap, obstacleMap, startCell, 2, 2);
        EnsureWalkableNode(pathMap, waterMap, detailMap, obstacleMap, GetNpcSpawnCell(sceneKind), 1, 1);
        EnsureWalkableNode(pathMap, waterMap, detailMap, obstacleMap, GetGoldNpcSpawnCell(sceneKind), 1, 1);

        switch (sceneKind)
        {
            case SceneKind.StartHub:
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(12, -1, 0));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(0, 1, 0));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(6, 1, 0));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(15, 6, 0));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(15, 2, 0));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(15, -2, 0));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(2, -7, 0));
                break;
            case SceneKind.Blacksmith:
            case SceneKind.PetStorage:
            case SceneKind.Warehouse:
            case SceneKind.HuntingVillage:
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(0, -8, 0));
                break;
            case SceneKind.Village1:
            case SceneKind.Village2:
            case SceneKind.Village3:
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(0, -8, 0));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, GetVillageHuntPortalCell(sceneKind, true));
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, GetVillageHuntPortalCell(sceneKind, false));
                break;
            case SceneKind.Village1HuntA:
            case SceneKind.Village1HuntB:
            case SceneKind.Village2HuntA:
            case SceneKind.Village2HuntB:
            case SceneKind.Village3HuntA:
            case SceneKind.Village3HuntB:
                ConnectImportantCells(pathMap, waterMap, detailMap, obstacleMap, startCell, new Vector3Int(0, -8, 0));
                break;
        }
    }

    private void ConnectImportantCells(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap, Vector3Int fromCell, Vector3Int toCell)
    {
        EnsureWalkableNode(pathMap, waterMap, detailMap, obstacleMap, fromCell, 1, 1);
        EnsureWalkableNode(pathMap, waterMap, detailMap, obstacleMap, toCell, 1, 2);

        CarveWalkableRect(pathMap, waterMap, detailMap, obstacleMap, GetInclusiveRect(fromCell, new Vector3Int(toCell.x, fromCell.y, 0), 1));
        CarveWalkableRect(pathMap, waterMap, detailMap, obstacleMap, GetInclusiveRect(new Vector3Int(toCell.x, fromCell.y, 0), toCell, 1));
    }

    private void EnsureWalkableNode(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap, Vector3Int center, int radiusX, int radiusY)
    {
        CarveWalkableRect(
            pathMap,
            waterMap,
            detailMap,
            obstacleMap,
            new RectInt(center.x - radiusX, center.y - radiusY, (radiusX * 2) + 1, (radiusY * 2) + 1));
    }

    private RectInt GetInclusiveRect(Vector3Int fromCell, Vector3Int toCell, int padding)
    {
        int xMin = Mathf.Min(fromCell.x, toCell.x) - padding;
        int yMin = Mathf.Min(fromCell.y, toCell.y) - padding;
        int xMax = Mathf.Max(fromCell.x, toCell.x) + padding;
        int yMax = Mathf.Max(fromCell.y, toCell.y) + padding;
        return new RectInt(xMin, yMin, (xMax - xMin) + 1, (yMax - yMin) + 1);
    }

    private void CarveWalkableRect(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap, RectInt rect)
    {
        PaintFilledRect(pathMap, rect, "rpgTile024.png");

        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (waterMap != null)
                {
                    waterMap.SetTile(cell, null);
                }

                if (detailMap != null)
                {
                    detailMap.SetTile(cell, null);
                }

                if (obstacleMap != null)
                {
                    obstacleMap.SetTile(cell, null);
                }
            }
        }
    }

    private void BuildVillageOneHuntALayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-2, -10, 4, 14), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-10, -1, 20, 3), "rpgTile024.png");
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(8f, 6f), 3.3f);

        PlaceTree(obstacleMap, -9, 7, "rpgTile195.png");
        PlaceTree(obstacleMap, -7, 8, "rpgTile179.png");
        PlaceTree(obstacleMap, 11, -4, "rpgTile195.png");
        PlaceTree(obstacleMap, 12, 5, "rpgTile179.png");
        PlaceRock(obstacleMap, 4, -4);
        PlaceRock(obstacleMap, 5, -4);
        PlaceFence(obstacleMap, new Vector3Int(-8, 4, 0), 5, true);
        PaintFlowerCluster(detailMap, new Vector3Int(-6, -4, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(7, 2, 0));
    }

    private void BuildVillageOneHuntBLayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-12, -8, 24, 3), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(7, -8, 3, 14), "rpgTile024.png");
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(-8f, 4f), 2.8f);
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(-3f, 7f), 2.1f);

        PlaceTree(obstacleMap, -12, 6, "rpgTile179.png");
        PlaceTree(obstacleMap, -10, 8, "rpgTile195.png");
        PlaceTree(obstacleMap, 12, 4, "rpgTile179.png");
        PlaceTree(obstacleMap, 12, -1, "rpgTile195.png");
        PlaceRock(obstacleMap, -3, -4);
        PlaceRock(obstacleMap, -2, -4);
        PlaceFence(obstacleMap, new Vector3Int(2, 5, 0), 6, false);
        PaintFlowerCluster(detailMap, new Vector3Int(5, 4, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(9, -3, 0));
    }

    private void BuildVillageTwoHuntALayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-11, -10, 22, 4), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-2, -6, 4, 14), "rpgTile024.png");
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(8f, 7f), 2.6f);

        PlaceBuilding(obstacleMap, new Vector3Int(4, 1, 0));
        PlaceRock(obstacleMap, -7, 1);
        PlaceRock(obstacleMap, -6, 1);
        PlaceRock(obstacleMap, -5, 1);
        PlaceTree(obstacleMap, -10, 8, "rpgTile179.png");
        PlaceTree(obstacleMap, 10, 8, "rpgTile195.png");
        PlaceFence(obstacleMap, new Vector3Int(-9, -2, 0), 7, true);
        PaintFlowerCluster(detailMap, new Vector3Int(6, -4, 0));
    }

    private void BuildVillageTwoHuntBLayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-12, -1, 24, 3), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-9, -8, 3, 10), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(8, -8, 3, 10), "rpgTile024.png");
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(0f, 6f), 3.4f);

        PlaceTree(obstacleMap, -12, 7, "rpgTile179.png");
        PlaceTree(obstacleMap, -8, 8, "rpgTile195.png");
        PlaceTree(obstacleMap, 8, 8, "rpgTile179.png");
        PlaceTree(obstacleMap, 12, 7, "rpgTile195.png");
        PlaceRock(obstacleMap, -2, -5);
        PlaceRock(obstacleMap, 2, -5);
        PlaceFence(obstacleMap, new Vector3Int(-4, -4, 0), 9, true);
        PaintFlowerCluster(detailMap, new Vector3Int(-6, 3, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(6, 3, 0));
    }

    private void BuildVillageThreeHuntALayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-2, -10, 4, 16), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-10, 3, 20, 3), "rpgTile024.png");
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(10f, -2f), 2.5f);

        PlaceBuilding(obstacleMap, new Vector3Int(-8, 5, 0));
        PlaceBuilding(obstacleMap, new Vector3Int(4, 5, 0));
        PlaceRock(obstacleMap, -6, -3);
        PlaceRock(obstacleMap, 6, -3);
        PlaceTree(obstacleMap, -11, -1, "rpgTile179.png");
        PlaceTree(obstacleMap, -10, 8, "rpgTile195.png");
        PlaceTree(obstacleMap, 11, 8, "rpgTile179.png");
        PaintFlowerCluster(detailMap, new Vector3Int(0, 0, 0));
    }

    private void BuildVillageThreeHuntBLayout(Tilemap pathMap, Tilemap waterMap, Tilemap detailMap, Tilemap obstacleMap)
    {
        PaintFilledRect(pathMap, new RectInt(-12, -8, 25, 3), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(10, -8, 3, 15), "rpgTile024.png");
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(-10f, 6f), 2.7f);
        BuildOffsetPondTilemap(waterMap, detailMap, new Vector2(-4f, 8f), 2.2f);

        PlaceTree(obstacleMap, -13, 5, "rpgTile195.png");
        PlaceTree(obstacleMap, -12, -1, "rpgTile179.png");
        PlaceTree(obstacleMap, 7, 7, "rpgTile195.png");
        PlaceTree(obstacleMap, 13, 5, "rpgTile179.png");
        PlaceRock(obstacleMap, 3, -4);
        PlaceRock(obstacleMap, 4, -4);
        PlaceRock(obstacleMap, 5, -4);
        PlaceFence(obstacleMap, new Vector3Int(6, 0, 0), 7, false);
        PaintFlowerCluster(detailMap, new Vector3Int(11, -2, 0));
    }

    private void BuildOffsetPondTilemap(Tilemap waterMap, Tilemap detailMap, Vector2 pondCenter, float pondRadius)
    {
        Vector2 pondRadii = new Vector2(pondRadius, pondRadius);
        RectInt bounds = GetEllipseCellBounds(pondCenter, pondRadii, 2);

        TileBase waterTile = GetSolidColorTile("pond-water-flat", new Color(0.40f, 0.73f, 0.94f), Tile.ColliderType.Grid);

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                if (!CellIntersectsEllipse(x, y, pondCenter, pondRadii))
                {
                    continue;
                }

                if (!CellIsFullyInsideEllipse(x, y, pondCenter, pondRadii))
                {
                    continue;
                }

                Vector3Int cell = new Vector3Int(x, y, 0);
                waterMap.SetTile(cell, waterTile);
                waterMap.SetTransformMatrix(cell, Matrix4x4.identity);
            }
        }

        BuildPondEdgeSprites(detailMap, pondCenter, pondRadius);
    }

    private void BuildScenePortals(Transform parent, Grid grid, SceneKind sceneKind, Tilemap detailMap, Tilemap obstacleMap)
    {
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "Start", GetDefaultSpawnCell(sceneKind));

        switch (sceneKind)
        {
            case SceneKind.Blacksmith:
            case SceneKind.PetStorage:
            case SceneKind.Warehouse:
            case SceneKind.HuntingVillage:
                CreateReturnPortal(parent, grid, detailMap, obstacleMap, sceneKind);
                break;
            case SceneKind.Village1:
            case SceneKind.Village2:
            case SceneKind.Village3:
                CreateVillageBranchPortals(parent, grid, detailMap, obstacleMap, sceneKind);
                break;
            case SceneKind.Village1HuntA:
            case SceneKind.Village1HuntB:
            case SceneKind.Village2HuntA:
            case SceneKind.Village2HuntB:
            case SceneKind.Village3HuntA:
            case SceneKind.Village3HuntB:
                CreateNestedHuntReturnPortals(parent, grid, detailMap, obstacleMap, sceneKind);
                break;
            default:
                CreateStartHubPortals(parent, grid, detailMap, obstacleMap);
                break;
        }
    }

    private void CreateStartHubPortals(Transform parent, Grid grid, Tilemap detailMap, Tilemap obstacleMap)
    {
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromBlacksmith", new Vector3Int(10, -3, 0));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromPetStorage", new Vector3Int(0, -1, 0));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromWarehouse", new Vector3Int(5, -1, 0));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromVillage1", new Vector3Int(13, 5, 0));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromVillage2", new Vector3Int(13, 1, 0));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromVillage3", new Vector3Int(13, -3, 0));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromHuntingVillage", new Vector3Int(2, -5, 0));

        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Smithy", new Vector3Int(12, -1, 0), new Color(0.93f, 0.54f, 0.24f), "BlacksmithScene", "FromHub");
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Pets", new Vector3Int(0, 1, 0), new Color(0.60f, 0.84f, 0.54f), "PetStorageScene", "FromHub");
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Storage", new Vector3Int(6, 1, 0), new Color(0.78f, 0.65f, 0.38f), "WarehouseScene", "FromHub");
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Village 1", new Vector3Int(15, 6, 0), new Color(0.95f, 0.77f, 0.34f), "Village1Scene", "FromHub");
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Village 2", new Vector3Int(15, 2, 0), new Color(0.42f, 0.76f, 0.95f), "Village2Scene", "FromHub");
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Village 3", new Vector3Int(15, -2, 0), new Color(0.90f, 0.48f, 0.65f), "Village3Scene", "FromHub");
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Hunt", new Vector3Int(2, -7, 0), new Color(0.84f, 0.31f, 0.26f), "HuntingVillageScene", "FromHub");
    }

    private void CreateReturnPortal(Transform parent, Grid grid, Tilemap detailMap, Tilemap obstacleMap, SceneKind sceneKind)
    {
        string spawnId = sceneKind switch
        {
            SceneKind.Blacksmith => "FromBlacksmith",
            SceneKind.PetStorage => "FromPetStorage",
            SceneKind.Warehouse => "FromWarehouse",
            SceneKind.Village1 => "FromVillage1",
            SceneKind.Village2 => "FromVillage2",
            SceneKind.Village3 => "FromVillage3",
            SceneKind.HuntingVillage => "FromHuntingVillage",
            _ => "Start"
        };

        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromHub", GetSceneReturnSpawnCell(sceneKind));
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Back", new Vector3Int(0, -8, 0), new Color(0.96f, 0.93f, 0.62f), "SampleScene", spawnId);
    }

    private void CreateVillageBranchPortals(Transform parent, Grid grid, Tilemap detailMap, Tilemap obstacleMap, SceneKind sceneKind)
    {
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromHub", GetSceneReturnSpawnCell(sceneKind));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromHuntA", GetVillageHuntReturnSpawnCell(sceneKind, true));
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromHuntB", GetVillageHuntReturnSpawnCell(sceneKind, false));

        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Back", new Vector3Int(0, -8, 0), new Color(0.96f, 0.93f, 0.62f), "SampleScene", GetHubReturnSpawnId(sceneKind));
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Hunt A", GetVillageHuntPortalCell(sceneKind, true), GetVillageHuntPortalColor(sceneKind, true), GetVillageHuntSceneName(sceneKind, true), "FromVillage");
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Hunt B", GetVillageHuntPortalCell(sceneKind, false), GetVillageHuntPortalColor(sceneKind, false), GetVillageHuntSceneName(sceneKind, false), "FromVillage");
    }

    private void CreateNestedHuntReturnPortals(Transform parent, Grid grid, Tilemap detailMap, Tilemap obstacleMap, SceneKind sceneKind)
    {
        CreateSpawnPoint(parent, grid, detailMap, obstacleMap, "FromVillage", GetSceneReturnSpawnCell(sceneKind));
        CreatePortalStation(parent, grid, detailMap, obstacleMap, "Back", new Vector3Int(0, -8, 0), new Color(0.93f, 0.88f, 0.56f), GetParentVillageSceneName(sceneKind), GetParentVillageReturnSpawnId(sceneKind));
    }

    private void CreateSpawnPoint(Transform parent, Grid grid, Tilemap detailMap, Tilemap obstacleMap, string spawnId, Vector3Int cell)
    {
        ClearWalkablePocket(detailMap, obstacleMap, cell, 1, 1);
        Vector3 worldPosition = grid.GetCellCenterWorld(cell);
        Transform spawn = CreateGroup($"Spawn_{spawnId}", parent.InverseTransformPoint(worldPosition), parent);
        SceneSpawnPoint2D marker = spawn.gameObject.AddComponent<SceneSpawnPoint2D>();
        marker.Configure(spawnId);
    }

    private void CreatePortalStation(Transform parent, Grid grid, Tilemap detailMap, Tilemap obstacleMap, string label, Vector3Int cell, Color color, string destinationScene, string destinationSpawnId)
    {
        ClearWalkablePocket(detailMap, obstacleMap, cell, 1, 2);
        Vector3 worldPosition = grid.GetCellCenterWorld(cell);
        Transform portal = CreateGroup($"Portal_{label.Replace(" ", string.Empty)}", parent.InverseTransformPoint(worldPosition), parent);

        SpriteRenderer baseRenderer = CreateSprite(
            "PortalBase",
            GetSolidColorSprite($"portal-base::{label}", Color.white),
            new Vector3(0f, -0.18f, 0f),
            new Vector3(1.05f, 0.42f, 1f),
            portal,
            540);
        baseRenderer.color = new Color(color.r, color.g, color.b, 0.90f);

        SpriteRenderer archRenderer = CreateSprite(
            "PortalArch",
            GetSolidColorSprite($"portal-arch::{label}", Color.white),
            new Vector3(0f, 0.22f, 0f),
            new Vector3(0.42f, 0.92f, 1f),
            portal,
            541);
        archRenderer.color = color;

        BoxCollider2D trigger = portal.gameObject.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.offset = new Vector2(0f, -0.06f);
        trigger.size = new Vector2(0.9f, 0.86f);

        ScenePortal2D portalTrigger = portal.gameObject.AddComponent<ScenePortal2D>();
        portalTrigger.Configure(destinationScene, destinationSpawnId);

        CreatePortalLabel(portal, label);
    }

    private void CreatePortalLabel(Transform parent, string label)
    {
        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = new Vector3(0f, 0.72f, 0f);

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = label;
        textMesh.characterSize = 0.11f;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(0.18f, 0.14f, 0.08f, 1f);
        textMesh.fontSize = 32;
    }

    private Vector3Int GetDefaultSpawnCell(SceneKind sceneKind)
    {
        return sceneKind switch
        {
            SceneKind.Blacksmith => new Vector3Int(0, -6, 0),
            SceneKind.PetStorage => new Vector3Int(0, -7, 0),
            SceneKind.Warehouse => new Vector3Int(0, -7, 0),
            SceneKind.Village1 => new Vector3Int(0, -7, 0),
            SceneKind.Village2 => new Vector3Int(10, -7, 0),
            SceneKind.Village3 => new Vector3Int(0, -7, 0),
            SceneKind.HuntingVillage => new Vector3Int(0, -7, 0),
            SceneKind.Village1HuntA => new Vector3Int(0, -5, 0),
            SceneKind.Village1HuntB => new Vector3Int(7, -5, 0),
            SceneKind.Village2HuntA => new Vector3Int(0, -5, 0),
            SceneKind.Village2HuntB => new Vector3Int(-8, -4, 0),
            SceneKind.Village3HuntA => new Vector3Int(0, -5, 0),
            SceneKind.Village3HuntB => new Vector3Int(10, -4, 0),
            _ => new Vector3Int(-6, -2, 0)
        };
    }

    private Vector3Int GetNpcSpawnCell(SceneKind sceneKind)
    {
        return sceneKind switch
        {
            SceneKind.Blacksmith => new Vector3Int(3, -1, 0),
            SceneKind.PetStorage => new Vector3Int(0, 2, 0),
            SceneKind.Warehouse => new Vector3Int(4, -1, 0),
            SceneKind.Village1 => new Vector3Int(5, -2, 0),
            SceneKind.Village2 => new Vector3Int(3, 2, 0),
            SceneKind.Village3 => new Vector3Int(6, 1, 0),
            SceneKind.HuntingVillage => new Vector3Int(-1, 1, 0),
            SceneKind.Village1HuntA => new Vector3Int(-5, -1, 0),
            SceneKind.Village1HuntB => new Vector3Int(4, -3, 0),
            SceneKind.Village2HuntA => new Vector3Int(4, -2, 0),
            SceneKind.Village2HuntB => new Vector3Int(-5, -3, 0),
            SceneKind.Village3HuntA => new Vector3Int(-4, 1, 0),
            SceneKind.Village3HuntB => new Vector3Int(6, -2, 0),
            _ => new Vector3Int(-3, -2, 0)
        };
    }

    private Vector3Int GetGoldNpcSpawnCell(SceneKind sceneKind)
    {
        return sceneKind switch
        {
            SceneKind.Blacksmith => new Vector3Int(-3, -1, 0),
            SceneKind.PetStorage => new Vector3Int(3, 2, 0),
            SceneKind.Warehouse => new Vector3Int(-4, -1, 0),
            SceneKind.Village1 => new Vector3Int(8, -2, 0),
            SceneKind.Village2 => new Vector3Int(-2, 2, 0),
            SceneKind.Village3 => new Vector3Int(10, 1, 0),
            SceneKind.HuntingVillage => new Vector3Int(2, 1, 0),
            SceneKind.Village1HuntA => new Vector3Int(7, 1, 0),
            SceneKind.Village1HuntB => new Vector3Int(10, 2, 0),
            SceneKind.Village2HuntA => new Vector3Int(-4, -2, 0),
            SceneKind.Village2HuntB => new Vector3Int(5, -3, 0),
            SceneKind.Village3HuntA => new Vector3Int(5, 1, 0),
            SceneKind.Village3HuntB => new Vector3Int(-2, -2, 0),
            _ => new Vector3Int(-1, -2, 0)
        };
    }

    private Vector3Int GetSceneReturnSpawnCell(SceneKind sceneKind)
    {
        return sceneKind switch
        {
            SceneKind.Village2 => new Vector3Int(10, -7, 0),
            SceneKind.Village1HuntA => new Vector3Int(0, -5, 0),
            SceneKind.Village1HuntB => new Vector3Int(7, -5, 0),
            SceneKind.Village2HuntA => new Vector3Int(0, -5, 0),
            SceneKind.Village2HuntB => new Vector3Int(-8, -4, 0),
            SceneKind.Village3HuntA => new Vector3Int(0, -5, 0),
            SceneKind.Village3HuntB => new Vector3Int(10, -4, 0),
            _ => new Vector3Int(0, -6, 0)
        };
    }

    private void ClearWalkablePocket(Tilemap detailMap, Tilemap obstacleMap, Vector3Int center, int radiusX, int radiusY)
    {
        for (int x = center.x - radiusX; x <= center.x + radiusX; x++)
        {
            for (int y = center.y - radiusY; y <= center.y + radiusY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (detailMap != null)
                {
                    detailMap.SetTile(cell, null);
                }

                if (obstacleMap != null)
                {
                    obstacleMap.SetTile(cell, null);
                }
            }
        }
    }

    private string GetHubReturnSpawnId(SceneKind sceneKind)
    {
        return sceneKind switch
        {
            SceneKind.Village1 => "FromVillage1",
            SceneKind.Village2 => "FromVillage2",
            SceneKind.Village3 => "FromVillage3",
            _ => "Start"
        };
    }

    private string GetVillageHuntSceneName(SceneKind sceneKind, bool firstHunt)
    {
        return sceneKind switch
        {
            SceneKind.Village1 => firstHunt ? "Village1HuntAScene" : "Village1HuntBScene",
            SceneKind.Village2 => firstHunt ? "Village2HuntAScene" : "Village2HuntBScene",
            SceneKind.Village3 => firstHunt ? "Village3HuntAScene" : "Village3HuntBScene",
            _ => "SampleScene"
        };
    }

    private string GetParentVillageSceneName(SceneKind sceneKind)
    {
        return sceneKind switch
        {
            SceneKind.Village1HuntA => "Village1Scene",
            SceneKind.Village1HuntB => "Village1Scene",
            SceneKind.Village2HuntA => "Village2Scene",
            SceneKind.Village2HuntB => "Village2Scene",
            SceneKind.Village3HuntA => "Village3Scene",
            SceneKind.Village3HuntB => "Village3Scene",
            _ => "SampleScene"
        };
    }

    private string GetParentVillageReturnSpawnId(SceneKind sceneKind)
    {
        return sceneKind switch
        {
            SceneKind.Village1HuntA => "FromHuntA",
            SceneKind.Village1HuntB => "FromHuntB",
            SceneKind.Village2HuntA => "FromHuntA",
            SceneKind.Village2HuntB => "FromHuntB",
            SceneKind.Village3HuntA => "FromHuntA",
            SceneKind.Village3HuntB => "FromHuntB",
            _ => "FromHub"
        };
    }

    private Vector3Int GetVillageHuntPortalCell(SceneKind sceneKind, bool firstHunt)
    {
        return sceneKind switch
        {
            SceneKind.Village1 => firstHunt ? new Vector3Int(-6, 3, 0) : new Vector3Int(6, 3, 0),
            SceneKind.Village2 => firstHunt ? new Vector3Int(-10, -5, 0) : new Vector3Int(12, 2, 0),
            SceneKind.Village3 => firstHunt ? new Vector3Int(-7, 6, 0) : new Vector3Int(11, -1, 0),
            _ => new Vector3Int(0, 0, 0)
        };
    }

    private Vector3Int GetVillageHuntReturnSpawnCell(SceneKind sceneKind, bool firstHunt)
    {
        return sceneKind switch
        {
            SceneKind.Village1 => firstHunt ? new Vector3Int(-4, 1, 0) : new Vector3Int(4, 1, 0),
            SceneKind.Village2 => firstHunt ? new Vector3Int(-8, -4, 0) : new Vector3Int(10, 1, 0),
            SceneKind.Village3 => firstHunt ? new Vector3Int(-5, 4, 0) : new Vector3Int(9, 1, 0),
            _ => new Vector3Int(0, -6, 0)
        };
    }

    private Color GetVillageHuntPortalColor(SceneKind sceneKind, bool firstHunt)
    {
        return sceneKind switch
        {
            SceneKind.Village1 => firstHunt ? new Color(0.58f, 0.83f, 0.43f) : new Color(0.36f, 0.77f, 0.84f),
            SceneKind.Village2 => firstHunt ? new Color(0.90f, 0.61f, 0.28f) : new Color(0.53f, 0.71f, 0.94f),
            SceneKind.Village3 => firstHunt ? new Color(0.82f, 0.46f, 0.38f) : new Color(0.67f, 0.57f, 0.90f),
            _ => new Color(0.9f, 0.9f, 0.9f)
        };
    }

    private Tilemap CreateTilemapLayer(Transform parent, string layerName, int sortingOrder, bool addCollider = false)
    {
        GameObject go = new GameObject(layerName);
        go.transform.SetParent(parent, false);

        Tilemap tilemap = go.AddComponent<Tilemap>();
        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
        renderer.sortingOrder = sortingOrder;
        renderer.mode = TilemapRenderer.Mode.Individual;

        if (addCollider)
        {
            Rigidbody2D body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;

            TilemapCollider2D collider = go.AddComponent<TilemapCollider2D>();
            collider.usedByComposite = true;

            go.AddComponent<CompositeCollider2D>();
        }

        return tilemap;
    }

    private void BuildRpgGroundTilemap(Tilemap groundMap)
    {
        for (int x = -18; x <= 18; x++)
        {
            for (int y = -10; y <= 10; y++)
            {
                string tileName = ((x + y) & 3) switch
                {
                    0 => "rpgTile039.png",
                    1 => "rpgTile040.png",
                    2 => "rpgTile022.png",
                    _ => "rpgTile021.png"
                };

                groundMap.SetTile(new Vector3Int(x, y, 0), GetRuntimeTile(tileName));
            }
        }
    }

    private void BuildRpgPathTilemap(Tilemap pathMap)
    {
        PaintFilledRect(pathMap, new RectInt(8, -10, 11, 4), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(14, -6, 4, 17), "rpgTile024.png");
        PaintFilledRect(pathMap, new RectInt(-2, -2, 7, 3), "rpgTile024.png");
    }

    private void BuildRpgPondTilemap(Tilemap waterMap, Tilemap detailMap)
    {
        Vector2 pondCenter = new Vector2(-12f, 1f);
        float pondRadius = 4.6f;
        Vector2 pondRadii = new Vector2(pondRadius, pondRadius);
        RectInt bounds = GetEllipseCellBounds(pondCenter, pondRadii, 2);

        waterMap.ClearAllTiles();
        detailMap.ClearAllTiles();

        TileBase waterTile = GetSolidColorTile("pond-water-flat", new Color(0.40f, 0.73f, 0.94f), Tile.ColliderType.Grid);

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                if (!CellIntersectsEllipse(x, y, pondCenter, pondRadii))
                {
                    continue;
                }

                if (!CellIsFullyInsideEllipse(x, y, pondCenter, pondRadii))
                {
                    continue;
                }

                Vector3Int cell = new Vector3Int(x, y, 0);
                waterMap.SetTile(cell, waterTile);
                waterMap.SetTransformMatrix(cell, Matrix4x4.identity);
            }
        }

        BuildPondEdgeSprites(detailMap, pondCenter, pondRadius);
    }

    private void BuildRpgDetailTilemap(Tilemap detailMap)
    {
        PaintFlowerCluster(detailMap, new Vector3Int(-3, 0, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(1, -1, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(0, 1, 0));
        PaintFlowerCluster(detailMap, new Vector3Int(-4, -4, 0));

        detailMap.SetTile(new Vector3Int(5, -2, 0), GetRuntimeTile("rpgTile222.png"));
        detailMap.SetTileFlags(new Vector3Int(5, -2, 0), TileFlags.None);
        detailMap.SetColor(new Vector3Int(5, -2, 0), new Color(0.95f, 0.72f, 0.28f, 1f));
        detailMap.SetTile(new Vector3Int(6, -1, 0), GetRuntimeTile("rpgTile222.png"));
        detailMap.SetTileFlags(new Vector3Int(6, -1, 0), TileFlags.None);
        detailMap.SetColor(new Vector3Int(6, -1, 0), new Color(0.95f, 0.72f, 0.28f, 1f));
        detailMap.SetTile(new Vector3Int(-5, 5, 0), GetRuntimeTile("rpgTile222.png"));
        detailMap.SetTileFlags(new Vector3Int(-5, 5, 0), TileFlags.None);
        detailMap.SetColor(new Vector3Int(-5, 5, 0), new Color(0.95f, 0.72f, 0.28f, 1f));
    }

    private RectInt GetEllipseCellBounds(Vector2 center, Vector2 radii, int padding)
    {
        int xMin = Mathf.FloorToInt(center.x - radii.x) - padding;
        int xMax = Mathf.CeilToInt(center.x + radii.x) + padding;
        int yMin = Mathf.FloorToInt(center.y - radii.y) - padding;
        int yMax = Mathf.CeilToInt(center.y + radii.y) + padding;
        return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
    }

    private bool CellIntersectsEllipse(int cellX, int cellY, Vector2 center, Vector2 radii)
    {
        for (int sx = 0; sx < 3; sx++)
        {
            for (int sy = 0; sy < 3; sy++)
            {
                float wx = cellX - 0.5f + (sx * 0.5f);
                float wy = cellY - 0.5f + (sy * 0.5f);
                if (IsInsideEllipse(wx, wy, center, radii))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool CellIsFullyInsideEllipse(int cellX, int cellY, Vector2 center, Vector2 radii)
    {
        for (int sx = 0; sx < 3; sx++)
        {
            for (int sy = 0; sy < 3; sy++)
            {
                float wx = cellX - 0.5f + (sx * 0.5f);
                float wy = cellY - 0.5f + (sy * 0.5f);
                if (!IsInsideEllipse(wx, wy, center, radii))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool IsPondBoundaryCell(int cellX, int cellY, Vector2 center, Vector2 radii)
    {
        if (!IsCellCenterInsideEllipse(cellX, cellY, center, radii))
        {
            return false;
        }

        return !IsCellCenterInsideEllipse(cellX + 1, cellY, center, radii)
            || !IsCellCenterInsideEllipse(cellX - 1, cellY, center, radii)
            || !IsCellCenterInsideEllipse(cellX, cellY + 1, center, radii)
            || !IsCellCenterInsideEllipse(cellX, cellY - 1, center, radii);
    }

    private bool IsCellCenterInsideEllipse(int cellX, int cellY, Vector2 center, Vector2 radii)
    {
        return IsInsideEllipse(cellX, cellY, center, radii);
    }

    private void BuildPondEdgeSprites(Tilemap detailMap, Vector2 pondCenterCell, float pondRadiusCell)
    {
        Transform existing = detailMap.transform.Find("PondEdgeSprites");
        if (existing != null)
        {
            DestroyImmediate(existing.gameObject);
        }

        Transform edgeRoot = CreateGroup("PondEdgeSprites", Vector3.zero, detailMap.transform);
        Sprite shorelineSprite = GetPondShorelineSprite();
        if (shorelineSprite == null)
        {
            return;
        }

        float arcLength = Mathf.Max(0.1f, shorelineSprite.bounds.size.x * 0.78f);
        float worldRadius = pondRadiusCell * GetRpgTileWorldSize();
        int segmentCount = Mathf.Max(18, Mathf.CeilToInt((2f * Mathf.PI * worldRadius) / arcLength));
        float angleStep = 360f / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float angleDeg = i * angleStep;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector2 outward = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
            Vector2 cellPosition = pondCenterCell + (outward * pondRadiusCell);
            Vector3 localPosition = detailMap.layoutGrid.CellToLocalInterpolated(new Vector3(cellPosition.x, cellPosition.y, 0f));

            // Pull the sprite slightly inward so the shoreline hugs the lake edge instead of floating outside.
            localPosition -= new Vector3(outward.x, outward.y, 0f) * (shorelineSprite.bounds.size.y * 0.18f);

            SpriteRenderer renderer = CreateSprite(
                $"PondEdge_{i}",
                shorelineSprite,
                localPosition + new Vector3(0f, 0f, -0.01f),
                Vector3.one * 1.04f,
                edgeRoot,
                7);

            float zRotation = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg + 90f;
            renderer.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        }
    }

    private Sprite GetPondShorelineSprite()
    {
        const string cacheKey = "pond-shoreline-sprite";
        if (generatedSpriteCache.TryGetValue(cacheKey, out Sprite cached))
        {
            return cached;
        }

        Sprite source = LoadRpgSprite("rpgTile045.png");
        if (source == null)
        {
            return null;
        }

        Texture2D texture = source.texture;
        Rect rect = source.rect;
        Sprite sprite = Sprite.Create(
            texture,
            rect,
            new Vector2(0.5f, 0.14f),
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        generatedSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private Vector2 GetPondOutwardDirection(Vector3Int cell, Vector2 pondCenter)
    {
        Vector2 cellCenter = new Vector2(cell.x, cell.y);
        Vector2 direction = (cellCenter - pondCenter).normalized;
        return direction == Vector2.zero ? Vector2.up : direction;
    }

    private PondEdgePlacement GetPondEdgePlacement(Vector3Int cell, Vector2 pondCenter)
    {
        Vector2 outward = GetPondOutwardDirection(cell, pondCenter);
        float angleDeg = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg;
        int octant = Mathf.RoundToInt(angleDeg / 45f);
        octant = ((octant % 8) + 8) % 8;

        switch (octant)
        {
            case 0:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile046.png"), 180f); // east
            case 1:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile044.png"), 180f); // north-east
            case 2:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile045.png"), 180f); // north
            case 3:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile044.png"), 90f); // north-west
            case 4:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile046.png"), 0f); // west
            case 5:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile044.png"), 0f); // south-west
            case 6:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile045.png"), 0f); // south
            default:
                return new PondEdgePlacement(GetRuntimeTile("rpgTile044.png"), 270f); // south-east
        }
    }

    private bool IsInsideEllipse(float x, float y, Vector2 center, Vector2 radii)
    {
        return EllipseValue(x, y, center, radii) <= 1f;
    }

    private float EllipseValue(float x, float y, Vector2 center, Vector2 radii)
    {
        float nx = (x - center.x) / radii.x;
        float ny = (y - center.y) / radii.y;
        return (nx * nx) + (ny * ny);
    }

    private void PaintFlowerCluster(Tilemap detailMap, Vector3Int center)
    {
        detailMap.SetTile(center, GetRuntimeTile("rpgTile222.png"));
        detailMap.SetTileFlags(center, TileFlags.None);
        detailMap.SetColor(center, new Color(0.95f, 0.72f, 0.28f, 1f));

        Vector3Int[] petals =
        {
            center + Vector3Int.up,
            center + Vector3Int.down,
            center + Vector3Int.left,
            center + Vector3Int.right
        };

        foreach (Vector3Int petal in petals)
        {
            detailMap.SetTile(petal, GetRuntimeTile("rpgTile222.png"));
            detailMap.SetTileFlags(petal, TileFlags.None);
            detailMap.SetColor(petal, Color.white);
        }
    }

    private void BuildRpgObstacleTilemap(Tilemap obstacleMap, Tilemap detailMap)
    {
        PlaceTree(obstacleMap, -4, 8, "rpgTile179.png");
        PlaceTree(obstacleMap, 2, 8, "rpgTile195.png");
        PlaceTree(obstacleMap, 10, 8, "rpgTile179.png");
        PlaceTree(obstacleMap, 15, 8, "rpgTile195.png");
        PlaceTree(obstacleMap, 11, -5, "rpgTile179.png");

        PlaceRock(obstacleMap, -6, 6);
        PlaceRock(obstacleMap, -6, -4);
        PlaceRock(obstacleMap, 8, -1);
        PlaceRock(obstacleMap, 9, -1);
        PlaceRock(obstacleMap, 10, -1);
        PlaceRock(obstacleMap, 11, -1);

        PlaceBuilding(obstacleMap, new Vector3Int(14, -10, 0));
        PlaceFence(obstacleMap, new Vector3Int(-9, 6, 0), 5, true);
        PlaceFence(obstacleMap, new Vector3Int(-5, 5, 0), 3, false);
        PlaceFence(obstacleMap, new Vector3Int(8, 6, 0), 4, false);
        PlaceFence(obstacleMap, new Vector3Int(10, 3, 0), 3, true);
        PlaceFence(obstacleMap, new Vector3Int(6, 2, 0), 3, true);
    }

    private void PlaceTree(Tilemap obstacleMap, int x, int y, string tileName)
    {
        obstacleMap.SetTile(new Vector3Int(x, y, 0), GetRuntimeTile(tileName, Tile.ColliderType.Grid));
    }

    private void PlaceRock(Tilemap obstacleMap, int x, int y)
    {
        obstacleMap.SetTile(new Vector3Int(x, y, 0), GetRuntimeTile("rpgTile222.png", Tile.ColliderType.Grid));
        obstacleMap.SetTileFlags(new Vector3Int(x, y, 0), TileFlags.None);
        obstacleMap.SetColor(new Vector3Int(x, y, 0), new Color(0.55f, 0.60f, 0.72f, 1f));
    }

    private void PlaceBuilding(Tilemap obstacleMap, Vector3Int bottomLeft)
    {
        obstacleMap.SetTile(bottomLeft + new Vector3Int(0, 0, 0), GetRuntimeTile("rpgTile056.png", Tile.ColliderType.Grid));
        obstacleMap.SetTile(bottomLeft + new Vector3Int(1, 0, 0), GetRuntimeTile("rpgTile056.png", Tile.ColliderType.Grid));
        obstacleMap.SetTile(bottomLeft + new Vector3Int(0, 1, 0), GetRuntimeTile("rpgTile057.png", Tile.ColliderType.Grid));
        obstacleMap.SetTile(bottomLeft + new Vector3Int(1, 1, 0), GetRuntimeTile("rpgTile058.png", Tile.ColliderType.Grid));
        obstacleMap.SetTile(bottomLeft + new Vector3Int(0, 2, 0), GetRuntimeTile("rpgTile130.png", Tile.ColliderType.Grid));
        obstacleMap.SetTile(bottomLeft + new Vector3Int(1, 2, 0), GetRuntimeTile("rpgTile130.png", Tile.ColliderType.Grid));
        obstacleMap.SetTile(bottomLeft + new Vector3Int(1, 0, 0), GetRuntimeTile("rpgTile170.png", Tile.ColliderType.Grid));
    }

    private void PlaceFence(Tilemap detailMap, Vector3Int startCell, int length, bool horizontal)
    {
        for (int i = 0; i < length; i++)
        {
            Vector3Int cell = startCell + (horizontal ? new Vector3Int(i, 0, 0) : new Vector3Int(0, -i, 0));
            detailMap.SetTile(cell, GetRuntimeTile("rpgTile222.png", Tile.ColliderType.Grid));
            detailMap.SetTileFlags(cell, TileFlags.None);
            detailMap.SetColor(cell, new Color(0.57f, 0.39f, 0.23f, 1f));
        }
    }

    private void BuildRpgActors(Transform parent, Grid grid, SceneKind sceneKind)
    {
        Vector3 playerWorld = grid.GetCellCenterWorld(GetDefaultSpawnCell(sceneKind));
        heroRoot = CreateGroup("Hero", parent.InverseTransformPoint(playerWorld), parent);
        heroRenderer = CreateSpriteCharacter(heroRoot, HeroSheet, 0, 500);

        if (heroRenderer != null)
        {
            FitSpriteRendererToHeight(heroRenderer, 0.54f);
        }

        Rigidbody2D body = heroRoot.gameObject.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        CapsuleCollider2D capsule = heroRoot.gameObject.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(0.22f, 0.18f);
        capsule.offset = new Vector2(0f, -0.12f);

        TopDownPlayerController2D controller = heroRoot.gameObject.AddComponent<TopDownPlayerController2D>();
        controller.Configure(heroRenderer, LoadSheetSprites(HeroSheet));

        Vector3 npcWorld = grid.GetCellCenterWorld(GetNpcSpawnCell(sceneKind));
        enemyRedRoot = CreateGroup("EnemyRed", parent.InverseTransformPoint(npcWorld), parent);
        enemyRedRenderer = CreateSpriteCharacter(enemyRedRoot, EnemyRedSheet, 2, 480);
        if (enemyRedRenderer != null)
        {
            FitSpriteRendererToHeight(enemyRedRenderer, 0.50f);
            enemyRedRenderer.flipX = true;
        }

        Vector3 goldNpcWorld = grid.GetCellCenterWorld(GetGoldNpcSpawnCell(sceneKind));
        enemyGoldRoot = CreateGroup("EnemyGold", parent.InverseTransformPoint(goldNpcWorld), parent);
        enemyGoldRenderer = CreateSpriteCharacter(enemyGoldRoot, EnemyGoldSheet, 2, 478);
        if (enemyGoldRenderer != null)
        {
            FitSpriteRendererToHeight(enemyGoldRenderer, 0.50f);
        }

        ApplyPendingSpawn(parent);
        AttachCameraFollow(heroRoot);
    }

    private void FitSpriteRendererToHeight(SpriteRenderer renderer, float targetHeight)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return;
        }

        float spriteHeight = renderer.sprite.bounds.size.y;
        if (spriteHeight <= 0.0001f)
        {
            return;
        }

        float uniformScale = targetHeight / spriteHeight;
        renderer.transform.localScale = Vector3.one * uniformScale;
    }

    private void ApplyPendingSpawn(Transform parent)
    {
        if (!Application.isPlaying || heroRoot == null)
        {
            return;
        }

        string spawnId = SceneWarpState.ConsumePendingSpawn();
        if (string.IsNullOrEmpty(spawnId))
        {
            return;
        }

        SceneSpawnPoint2D[] spawnPoints = parent.GetComponentsInChildren<SceneSpawnPoint2D>(true);
        foreach (SceneSpawnPoint2D spawnPoint in spawnPoints)
        {
            if (spawnPoint.SpawnId != spawnId)
            {
                continue;
            }

            heroRoot.position = spawnPoint.transform.position;

            Rigidbody2D body = heroRoot.GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.position = spawnPoint.transform.position;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            return;
        }
    }

    private void AttachCameraFollow(Transform target)
    {
        cachedCamera = Camera.main;
        if (cachedCamera == null)
        {
            cachedCamera = FindAnyObjectByType<Camera>();
        }

        if (cachedCamera == null)
        {
            return;
        }

        CameraFollow2D follow = cachedCamera.GetComponent<CameraFollow2D>();
        if (follow == null)
        {
            follow = cachedCamera.gameObject.AddComponent<CameraFollow2D>();
        }

        follow.SetTarget(target);
    }

    private void PaintFilledRect(Tilemap tilemap, RectInt rect, string tileName)
    {
        TileBase tile = GetRuntimeTile(tileName);
        for (int x = rect.xMin; x < rect.xMax; x++)
        {
            for (int y = rect.yMin; y < rect.yMax; y++)
            {
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    private void BuildRpgReferenceGround(Transform parent)
    {
        for (int x = -16; x <= 16; x++)
        {
            for (int y = -8; y <= 8; y++)
            {
                float px = x * 0.82f;
                float py = y * 0.82f;
                string grassTile = (x + y) % 3 == 0 ? "rpgTile039.png" : (x + y) % 3 == 1 ? "rpgTile040.png" : "rpgTile022.png";
                CreateRpgSprite(parent, grassTile, new Vector3(px, py, 0f), 1.30f, -20 + y);
            }
        }

        BuildRpgRoadPatch(parent, new Vector2(5.4f, 0.8f), new Vector2(2.5f, 9.0f), 40);
        BuildRpgRoadPatch(parent, new Vector2(0.1f, -5.1f), new Vector2(9.4f, 2.3f), 70);
        BuildRpgRoadPatch(parent, new Vector2(-1.1f, -0.35f), new Vector2(3.1f, 1.9f), 95);

        ScatterRpgFlowers(parent, new Vector2(-0.4f, 0.8f), 9, 112);
        ScatterRpgFlowers(parent, new Vector2(1.4f, -1.0f), 7, 128);
        ScatterRpgFlowers(parent, new Vector2(-4.4f, -0.8f), 5, 140);
    }

    private void BuildRpgRoadPatch(Transform parent, Vector2 center, Vector2 size, int sortingBase)
    {
        int columns = Mathf.CeilToInt(size.x / 0.82f);
        int rows = Mathf.CeilToInt(size.y / 0.82f);
        float startX = center.x - ((columns - 1) * 0.41f);
        float startY = center.y - ((rows - 1) * 0.41f);

        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                float px = startX + (x * 0.82f);
                float py = startY + (y * 0.82f);
                bool edgeLeft = x == 0;
                bool edgeRight = x == columns - 1;
                bool edgeBottom = y == 0;
                bool edgeTop = y == rows - 1;

                SpriteRenderer tile = null;
                if (edgeLeft && !edgeTop && !edgeBottom)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile025.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, false, false);
                }
                else if (edgeRight && !edgeTop && !edgeBottom)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile025.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, true, false);
                }
                else if (edgeTop && !edgeLeft && !edgeRight)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile041.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, false, false);
                }
                else if (edgeBottom && !edgeLeft && !edgeRight)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile041.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, false, true);
                }
                else if (edgeLeft && edgeTop)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile043.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, false, false);
                }
                else if (edgeRight && edgeTop)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile043.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, true, false);
                }
                else if (edgeLeft && edgeBottom)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile043.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, false, true);
                }
                else if (edgeRight && edgeBottom)
                {
                    tile = CreateRpgSpriteVariant(parent, "rpgTile043.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y, true, true);
                }
                else
                {
                    tile = CreateRpgSprite(parent, "rpgTile024.png", new Vector3(px, py, -0.01f), 1.30f, sortingBase + y);
                }

                if (tile != null && ((x + y) % 5 == 0))
                {
                    CreateFlat($"RoadDot_{x}_{y}", new Vector3(px + 0.07f, py + 0.02f, -0.03f), new Vector3(0.10f, 0.10f, 0.04f), new Color(0.55f, 0.36f, 0.21f), parent, 0f, sortingBase + rows + y);
                }
            }
        }
    }

    private void BuildRpgReferencePond(Transform parent)
    {
        Vector2 center = new Vector2(-7.0f, 0.4f);
        Vector2 radii = new Vector2(2.4f, 2.0f);

        for (int x = -4; x <= 4; x++)
        {
            for (int y = -4; y <= 4; y++)
            {
                float px = center.x + (x * 0.62f);
                float py = center.y + (y * 0.58f);
                float nx = x / radii.x;
                float ny = y / radii.y;
                float distance = (nx * nx) + (ny * ny);
                if (distance > 1.16f)
                {
                    continue;
                }

                Color waterColor = distance > 0.84f ? new Color(0.39f, 0.70f, 0.90f) : new Color(0.46f, 0.77f, 0.96f);
                CreateFlat($"PondCell_{x}_{y}", new Vector3(px, py, -0.02f), new Vector3(0.64f, 0.60f, 0.04f), waterColor, parent, 0f, 150 + y);
            }
        }

        for (int i = -3; i <= 3; i++)
        {
            CreateRpgSpriteVariant(parent, "rpgTile045.png", new Vector3(center.x + (i * 0.62f), center.y + 1.82f, -0.03f), 1.05f, 170 + i, false, false);
            CreateRpgSpriteVariant(parent, "rpgTile045.png", new Vector3(center.x + (i * 0.62f), center.y - 1.82f, -0.03f), 1.05f, 176 + i, false, true);
        }

        for (int i = -2; i <= 2; i++)
        {
            CreateRpgSpriteVariant(parent, "rpgTile046.png", new Vector3(center.x + 2.44f, center.y + (i * 0.58f), -0.03f), 1.05f, 184 + i, false, false);
            CreateRpgSpriteVariant(parent, "rpgTile046.png", new Vector3(center.x - 2.44f, center.y + (i * 0.58f), -0.03f), 1.05f, 190 + i, true, false);
        }

        CreateFenceBench(parent, new Vector3(-4.9f, 1.35f, -0.01f), 205);
        CreateFlat("PondSign", new Vector3(-5.5f, -1.8f, -0.01f), new Vector3(0.34f, 0.22f, 0.04f), new Color(0.58f, 0.39f, 0.23f), parent, 0f, 206);
    }

    private void BuildRpgReferenceFences(Transform parent)
    {
        CreateFenceLine(parent, new Vector3(-2.6f, 2.6f, -0.01f), 5, true, 240);
        CreateFenceLine(parent, new Vector3(-0.2f, 1.9f, -0.01f), 3, false, 246);
        CreateFenceLine(parent, new Vector3(5.6f, 2.6f, -0.01f), 4, false, 252);
        CreateFenceLine(parent, new Vector3(6.9f, 1.2f, -0.01f), 3, true, 258);
        CreateFenceLine(parent, new Vector3(3.6f, 0.8f, -0.01f), 3, true, 264);
    }

    private void BuildRpgReferenceTrees(Transform parent)
    {
        CreateRpgTree(parent, new Vector3(-2.6f, 3.15f, 0f), 300);
        CreateRpgTree(parent, new Vector3(0.4f, 3.15f, 0f), 301);
        CreateRpgTree(parent, new Vector3(4.7f, 3.05f, 0f), 302);
        CreateRpgTree(parent, new Vector3(7.3f, 3.15f, 0f), 303);
        CreateRpgTree(parent, new Vector3(6.8f, -2.2f, 0f), 304);
        CreateRpgBush(parent, new Vector3(1.1f, 0.25f, -0.01f), 320);
        CreateRpgBush(parent, new Vector3(3.4f, -2.7f, -0.01f), 321);
    }

    private void BuildRpgReferenceProps(Transform parent)
    {
        BuildRpgHouse(parent, new Vector3(7.2f, -4.2f, 0f), 340);

        CreateRpgRock(parent, new Vector3(-4.6f, 2.6f, -0.01f), 360);
        CreateRpgRock(parent, new Vector3(-4.1f, -2.6f, -0.01f), 361);
        CreateRpgRock(parent, new Vector3(6.0f, -1.1f, -0.01f), 362);

        for (int i = 0; i < 6; i++)
        {
            float x = 6.4f + (i * 0.38f);
            float y = -0.4f + Mathf.Sin(i * 0.7f) * 0.22f;
            CreateRpgRock(parent, new Vector3(x, y, -0.01f), 370 + i);
        }

        CreateFlat("SmallSlime", new Vector3(5.8f, 0.2f, -0.01f), new Vector3(0.42f, 0.32f, 0.04f), new Color(0.43f, 0.70f, 0.94f), parent, 0f, 390);
        CreateFlat("SmallSlimeEyeL", new Vector3(5.72f, 0.25f, -0.02f), new Vector3(0.05f, 0.05f, 0.03f), Color.black, parent, 0f, 391);
        CreateFlat("SmallSlimeEyeR", new Vector3(5.88f, 0.25f, -0.02f), new Vector3(0.05f, 0.05f, 0.03f), Color.black, parent, 0f, 391);
    }

    private void BuildRpgReferenceCharacters(Transform parent)
    {
        heroRoot = CreateGroup("Hero", new Vector3(-3.55f, -1.55f, 0f), parent);
        enemyRedRoot = CreateGroup("EnemyRed", new Vector3(-1.75f, -1.8f, 0f), parent);
        enemyGoldRoot = null;

        heroRenderer = CreateSpriteCharacter(heroRoot, HeroSheet, 0, 430);
        enemyRedRenderer = CreateSpriteCharacter(enemyRedRoot, EnemyRedSheet, 2, 431);
        enemyGoldRenderer = null;

        if (heroRenderer != null)
        {
            heroRenderer.transform.localScale = Vector3.one * 0.78f;
            heroRenderer.flipX = false;
        }

        if (enemyRedRenderer != null)
        {
            enemyRedRenderer.transform.localScale = Vector3.one * 0.76f;
            enemyRedRenderer.flipX = true;
        }
    }

    private void ScatterRpgFlowers(Transform parent, Vector2 center, int count, int sortingBase)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i) / count;
            float radius = 0.25f + ((i % 3) * 0.22f);
            Vector3 position = new Vector3(center.x + (Mathf.Cos(angle) * radius), center.y + (Mathf.Sin(angle) * radius * 0.7f), -0.01f);
            CreateFlat($"FlowerCore_{i}", position, new Vector3(0.08f, 0.08f, 0.03f), new Color(0.97f, 0.75f, 0.28f), parent, 0f, sortingBase + i);
            CreateFlat($"FlowerPetalA_{i}", position + new Vector3(0.08f, 0f, 0f), new Vector3(0.08f, 0.08f, 0.03f), Color.white, parent, 0f, sortingBase + i + 1);
            CreateFlat($"FlowerPetalB_{i}", position + new Vector3(-0.08f, 0f, 0f), new Vector3(0.08f, 0.08f, 0.03f), Color.white, parent, 0f, sortingBase + i + 1);
            CreateFlat($"FlowerPetalC_{i}", position + new Vector3(0f, 0.08f, 0f), new Vector3(0.08f, 0.08f, 0.03f), Color.white, parent, 0f, sortingBase + i + 1);
            CreateFlat($"FlowerPetalD_{i}", position + new Vector3(0f, -0.08f, 0f), new Vector3(0.08f, 0.08f, 0.03f), Color.white, parent, 0f, sortingBase + i + 1);
        }
    }

    private void CreateFenceLine(Transform parent, Vector3 start, int segments, bool horizontal, int sortingBase)
    {
        for (int i = 0; i < segments; i++)
        {
            Vector3 offset = horizontal ? new Vector3(i * 0.72f, 0f, 0f) : new Vector3(0f, -i * 0.72f, 0f);
            Vector3 position = start + offset;
            CreateFlat($"FencePostA_{sortingBase}_{i}", position + (horizontal ? new Vector3(-0.18f, 0f, 0f) : new Vector3(0f, 0.18f, 0f)), new Vector3(0.14f, 0.34f, 0.04f), new Color(0.45f, 0.31f, 0.18f), parent, 0f, sortingBase + i);
            CreateFlat($"FencePostB_{sortingBase}_{i}", position + (horizontal ? new Vector3(0.18f, 0f, 0f) : new Vector3(0f, -0.18f, 0f)), new Vector3(0.14f, 0.34f, 0.04f), new Color(0.45f, 0.31f, 0.18f), parent, 0f, sortingBase + i);
            CreateFlat($"FenceRail_{sortingBase}_{i}", position, horizontal ? new Vector3(0.46f, 0.12f, 0.03f) : new Vector3(0.12f, 0.46f, 0.03f), new Color(0.57f, 0.39f, 0.23f), parent, 0f, sortingBase + i + 1);
        }
    }

    private void CreateFenceBench(Transform parent, Vector3 center, int sortingBase)
    {
        CreateFenceLine(parent, center + new Vector3(-0.35f, 0.24f, 0f), 2, true, sortingBase);
        CreateFlat("BenchSeat", center + new Vector3(0f, -0.15f, 0f), new Vector3(0.88f, 0.16f, 0.03f), new Color(0.58f, 0.39f, 0.23f), parent, 0f, sortingBase + 4);
        CreateFlat("BenchLegL", center + new Vector3(-0.22f, -0.32f, 0f), new Vector3(0.10f, 0.20f, 0.03f), new Color(0.43f, 0.29f, 0.17f), parent, 0f, sortingBase + 5);
        CreateFlat("BenchLegR", center + new Vector3(0.22f, -0.32f, 0f), new Vector3(0.10f, 0.20f, 0.03f), new Color(0.43f, 0.29f, 0.17f), parent, 0f, sortingBase + 5);
    }

    private void CreateRpgBush(Transform parent, Vector3 position, int sortingOrder)
    {
        CreateFlat($"BushBase_{sortingOrder}", position, new Vector3(0.86f, 0.52f, 0.04f), new Color(0.34f, 0.66f, 0.24f), parent, 0f, sortingOrder);
        CreateFlat($"BushTop_{sortingOrder}", position + new Vector3(0f, 0.12f, -0.01f), new Vector3(0.72f, 0.42f, 0.04f), new Color(0.40f, 0.76f, 0.28f), parent, 0f, sortingOrder + 1);
    }

    private void CreateRpgRock(Transform parent, Vector3 position, int sortingOrder)
    {
        CreateFlat($"RockBody_{sortingOrder}", position, new Vector3(0.42f, 0.34f, 0.04f), new Color(0.47f, 0.52f, 0.63f), parent, 0f, sortingOrder);
        CreateFlat($"RockShade_{sortingOrder}", position + new Vector3(-0.06f, -0.04f, -0.01f), new Vector3(0.20f, 0.12f, 0.03f), new Color(0.34f, 0.38f, 0.47f), parent, 0f, sortingOrder + 1);
    }

    private void BuildRpgPath(Transform parent)
    {
        for (int i = -7; i <= 7; i++)
        {
            CreateRpgSprite(parent, "rpgTile007.png", new Vector3(i * 0.78f, -3.4f, 0f), 1.65f, 60 + i + 10);
        }

        for (int i = -5; i <= 5; i++)
        {
            CreateRpgSprite(parent, "rpgTile007.png", new Vector3(i * 0.78f, -2.4f, 0f), 1.65f, 80 + i + 10);
            CreateRpgSprite(parent, "rpgTile007.png", new Vector3(i * 0.78f, 2.4f, 0f), 1.65f, 100 + i + 10);
        }

        for (int i = -2; i <= 2; i++)
        {
            CreateRpgSprite(parent, "rpgTile021.png", new Vector3(i * 0.82f, -0.3f, 0f), 1.65f, 110 + i + 10);
            CreateRpgSprite(parent, "rpgTile021.png", new Vector3(i * 0.82f, 0.9f, 0f), 1.65f, 120 + i + 10);
        }

        for (int x = -2; x <= 2; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                CreateRpgSprite(parent, "rpgTile047.png", new Vector3(x * 0.82f, y * 0.82f + 0.35f, -0.01f), 1.22f, 135 + ((y + 1) * 10) + x + 3);
            }
        }
    }

    private void BuildRpgLakeAndBridge(Transform parent)
    {
        CreateFlat("LakeBase", new Vector3(0f, -4.65f, -0.02f), new Vector3(12.8f, 2.0f, 0.04f), new Color(0.34f, 0.77f, 0.88f), parent, 0f, 20);
        for (int i = -6; i <= 6; i++)
        {
            CreateRpgSprite(parent, "rpgTile045.png", new Vector3(i * 0.78f, -3.95f, -0.01f), 1.65f, 25 + i + 10);
        }

        for (int i = -1; i <= 1; i++)
        {
            CreateRpgSprite(parent, "rpgTile024.png", new Vector3(i * 0.78f, -3.35f, 0f), 1.65f, 40 + i + 10);
            CreateRpgFence(parent, new Vector3(i * 0.78f, -3.1f, 0f), 50 + i + 10);
        }
    }

    private void BuildRpgGate(Transform parent, Vector3 center, int sortingBase)
    {
        Transform gate = CreateGroup("RpgGate", center, parent);
        CreateRpgSprite(gate, "rpgTile056.png", new Vector3(-1.8f, 0f, 0f), 2.2f, sortingBase);
        CreateRpgSprite(gate, "rpgTile056.png", new Vector3(1.8f, 0f, 0f), 2.2f, sortingBase + 1);
        CreateRpgSprite(gate, "rpgTile214.png", new Vector3(0f, -0.15f, -0.01f), 1.5f, sortingBase + 2);
        CreateRpgSprite(gate, "rpgTile130.png", new Vector3(-1.8f, 0.5f, -0.02f), 1.5f, sortingBase + 3);
        CreateRpgSprite(gate, "rpgTile130.png", new Vector3(1.8f, 0.5f, -0.02f), 1.5f, sortingBase + 4);
    }

    private void BuildRpgChapel(Transform parent, Vector3 center, int sortingBase)
    {
        Transform chapel = CreateGroup("RpgChapel", center, parent);
        CreateRpgSprite(chapel, "rpgTile056.png", new Vector3(0f, -0.28f, 0f), 3.8f, sortingBase);
        CreateRpgSprite(chapel, "rpgTile057.png", new Vector3(-1.05f, -0.02f, -0.01f), 2.1f, sortingBase + 1);
        CreateRpgSprite(chapel, "rpgTile058.png", new Vector3(1.05f, -0.02f, -0.01f), 2.1f, sortingBase + 1);
        CreateRpgSprite(chapel, "rpgTile130.png", new Vector3(0f, 0.74f, -0.02f), 2.6f, sortingBase + 2);
        CreateRpgSprite(chapel, "rpgTile170.png", new Vector3(0f, -0.16f, -0.03f), 1.45f, sortingBase + 3);
        CreateRpgSprite(chapel, "rpgTile188.png", new Vector3(0f, 0.16f, -0.03f), 1.05f, sortingBase + 3);
    }

    private void BuildRpgHouse(Transform parent, Vector3 center, int sortingBase)
    {
        Transform house = CreateGroup("RpgHouse", center, parent);
        CreateRpgSprite(house, "rpgTile056.png", new Vector3(0f, -0.22f, 0f), 2.6f, sortingBase);
        CreateRpgSprite(house, "rpgTile130.png", new Vector3(0f, 0.42f, -0.02f), 1.8f, sortingBase + 1);
        CreateRpgSprite(house, "rpgTile170.png", new Vector3(0f, -0.14f, -0.03f), 1.05f, sortingBase + 2);
    }

    private void BuildRpgShopRow(Transform parent, float x, float y, int sortingBase)
    {
        for (int i = -1; i <= 1; i++)
        {
            Transform shop = CreateGroup($"RpgShop_{sortingBase}_{i}", new Vector3(x + (i * 1.45f), y + (i == 0 ? 0.1f : 0f), 0f), parent);
            CreateRpgSprite(shop, "rpgTile056.png", new Vector3(0f, -0.12f, 0f), 1.45f, sortingBase + (i * 5) + 10);
            CreateRpgSprite(shop, "rpgTile130.png", new Vector3(0f, 0.22f, -0.02f), 1.05f, sortingBase + (i * 5) + 11);
            CreateRpgSprite(shop, "rpgTile170.png", new Vector3(0f, -0.08f, -0.03f), 0.72f, sortingBase + (i * 5) + 12);
            CreateRpgFence(shop, new Vector3(0f, -0.55f, 0f), sortingBase + (i * 5) + 13);
            CreateRpgBarrel(shop, new Vector3(0.34f, -0.26f, 0f), sortingBase + (i * 5) + 14);
        }
    }

    private void CreateRpgTree(Transform parent, Vector3 position, int sortingOrder)
    {
        string tile = sortingOrder % 2 == 0 ? "rpgTile179.png" : "rpgTile195.png";
        CreateRpgSprite(parent, tile, position, 1.25f, sortingOrder);
    }

    private void CreateRpgFence(Transform parent, Vector3 position, int sortingOrder)
    {
        CreateRpgSprite(parent, "rpgTile021.png", position, 0.95f, sortingOrder);
    }

    private void CreateRpgBarrel(Transform parent, Vector3 position, int sortingOrder)
    {
        CreateRpgSprite(parent, "rpgTile222.png", position, 0.9f, sortingOrder);
    }

    private SpriteRenderer CreateRpgSprite(Transform parent, string fileName, Vector3 position, float scale, int sortingOrder)
    {
        Sprite sprite = LoadRpgSprite(fileName);
        if (sprite == null)
        {
            return null;
        }

        return CreateSprite(fileName, sprite, position, Vector3.one * scale, parent, sortingOrder);
    }

    private SpriteRenderer CreateRpgSpriteVariant(Transform parent, string fileName, Vector3 position, float scale, int sortingOrder, bool flipX, bool flipY)
    {
        SpriteRenderer renderer = CreateRpgSprite(parent, fileName, position, scale, sortingOrder);
        if (renderer == null)
        {
            return null;
        }

        renderer.flipX = flipX;
        renderer.flipY = flipY;
        return renderer;
    }

    private void BuildKenneyUnits(Transform parent)
    {
        heroRoot = CreateGroup("Hero", GridToIsoPosition(6, 5) + new Vector3(0f, 0.16f, 0f), parent);
        enemyRedRoot = CreateGroup("EnemyRed", GridToIsoPosition(5, 4) + new Vector3(-0.16f, 0.24f, 0f), parent);
        enemyGoldRoot = CreateGroup("EnemyGold", GridToIsoPosition(5, 6) + new Vector3(0.16f, 0.24f, 0f), parent);

        heroRenderer = CreateSpriteCharacter(heroRoot, HeroSheet, 0, 420);
        enemyRedRenderer = CreateSpriteCharacter(enemyRedRoot, EnemyRedSheet, 2, 418);
        enemyGoldRenderer = CreateSpriteCharacter(enemyGoldRoot, EnemyGoldSheet, 2, 419);

        if (heroRenderer != null)
        {
            heroRenderer.flipX = false;
            heroRenderer.transform.localScale = Vector3.one * 0.72f;
        }

        if (enemyRedRenderer != null)
        {
            enemyRedRenderer.flipX = false;
            enemyRedRenderer.transform.localScale = Vector3.one * 0.72f;
        }

        if (enemyGoldRenderer != null)
        {
            enemyGoldRenderer.flipX = true;
            enemyGoldRenderer.transform.localScale = Vector3.one * 0.72f;
        }
    }

    private void CreateKenneyBanner(Transform parent, int row, int col, Color clothColor, int sortingOrder)
    {
        Transform banner = CreateGroup($"Banner_{row}_{col}", GridToIsoPosition(row, col) + new Vector3(0f, 0.45f, -0.01f), parent);
        CreateFlat("Pole", new Vector3(0f, -0.08f, 0f), new Vector3(0.08f, 0.72f, 0.04f), new Color(0.55f, 0.40f, 0.22f), banner, 0f, sortingOrder);
        CreateFlat("Cloth", new Vector3(0.18f, 0.12f, -0.01f), new Vector3(0.32f, 0.22f, 0.04f), clothColor, banner, -8f, sortingOrder + 1);
    }

    private void CreateKenneyStall(Transform parent, int row, int col, Color awningColor, int sortingOrder)
    {
        Transform stall = CreateGroup($"Stall_{row}_{col}", GridToIsoPosition(row, col) + new Vector3(0f, 0.18f, -0.02f), parent);
        CreateFlat("Base", new Vector3(0f, -0.04f, 0f), new Vector3(0.44f, 0.28f, 0.04f), new Color(0.72f, 0.65f, 0.56f), stall, 0f, sortingOrder);
        CreateFlat("Roof", new Vector3(0f, 0.16f, -0.01f), new Vector3(0.52f, 0.18f, 0.04f), awningColor, stall, 0f, sortingOrder + 1);
    }

    private void CreateKenneyTree(Transform parent, int row, int col, int sortingOrder)
    {
        Transform tree = CreateGroup($"KenneyTree_{row}_{col}", GridToIsoPosition(row, col) + new Vector3(0f, 0.12f, -0.02f), parent);
        CreateFlat("Trunk", new Vector3(0f, -0.06f, 0f), new Vector3(0.10f, 0.28f, 0.04f), new Color(0.48f, 0.34f, 0.20f), tree, 0f, sortingOrder);
        CreateFlat("Leaves", new Vector3(0f, 0.14f, -0.01f), new Vector3(0.30f, 0.24f, 0.04f), new Color(0.46f, 0.74f, 0.36f), tree, 0f, sortingOrder + 1);
    }

    private SpriteRenderer CreateSpriteCharacter(Transform parent, string fileName, int poseIndex, int sortingOrder)
    {
        Sprite[] poses = LoadSheetSprites(fileName);
        if (poses == null || poses.Length == 0)
        {
            return null;
        }

        SpriteRenderer renderer = CreateSprite("CharacterSprite", poses[Mathf.Clamp(poseIndex, 0, poses.Length - 1)], Vector3.zero, Vector3.one * 1.12f, parent, sortingOrder);
        return renderer;
    }

    private SpriteRenderer CreateSprite(string name, Sprite sprite, Vector3 localPosition, Vector3 localScale, Transform parent, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private SpriteRenderer CreateKenneyTileSprite(Transform parent, int row, int col, int tileId, int sortingOrder, Vector3? extraOffset = null)
    {
        Sprite sprite = LoadKenneySprite(tileId);
        if (sprite == null)
        {
            return null;
        }

        Vector3 localPosition = GridToIsoPosition(row, col) + (extraOffset ?? Vector3.zero);
        SpriteRenderer renderer = CreateSprite($"Kenney_{tileId}_{row}_{col}", sprite, localPosition, Vector3.one * 1.1f, parent, sortingOrder);
        return renderer;
    }

    private Vector3 GridToIsoPosition(int row, int col)
    {
        float stepX = 1.36f;
        float stepY = 0.68f;
        float x = (col - row) * stepX * 0.5f;
        float y = -(col + row) * stepY * 0.5f;
        return new Vector3(x, y, 0f);
    }

    private void BuildHouseSilhouette(Transform parent, Vector3 center, float scale)
    {
        Transform house = CreateGroup("HouseSilhouette", center, parent);
        CreateFlat("Roof", new Vector3(0f, 0.56f * scale, 0f), new Vector3(2.65f * scale, 1.35f * scale, 0.06f), new Color(0.73f, 0.46f, 0.33f), house, 0, -30);
        CreateFlat("Wall", new Vector3(0f, -0.28f * scale, -0.01f), new Vector3(2.1f * scale, 1.45f * scale, 0.06f), new Color(0.94f, 0.88f, 0.78f), house, 0, -31);
        CreateFlat("Door", new Vector3(0f, -0.62f * scale, -0.02f), new Vector3(0.42f * scale, 0.72f * scale, 0.06f), new Color(0.58f, 0.38f, 0.23f), house, 0, -32);
    }

    private void BuildTreeSilhouette(Transform parent, Vector3 center, float scale)
    {
        Transform tree = CreateGroup("TreeSilhouette", center, parent);
        CreateFlat("Trunk", new Vector3(0f, -0.58f * scale, 0f), new Vector3(0.42f * scale, 1.6f * scale, 0.06f), new Color(0.53f, 0.35f, 0.21f), tree, 0, -29);
        CreateFlat("Leaves", new Vector3(0f, 0.55f * scale, -0.01f), new Vector3(2.05f * scale, 1.75f * scale, 0.06f), new Color(0.50f, 0.76f, 0.35f), tree, 0, -28);
    }

    private void BuildLanternSilhouette(Transform parent, Vector3 center)
    {
        Transform lantern = CreateGroup("Lantern", center, parent);
        CreateFlat("Pole", new Vector3(0f, -0.35f, 0f), new Vector3(0.15f, 0.9f, 0.05f), new Color(0.54f, 0.39f, 0.24f), lantern, 0, -27);
        CreateFlat("Lamp", new Vector3(0f, 0.28f, -0.01f), new Vector3(0.32f, 0.38f, 0.05f), new Color(0.97f, 0.83f, 0.44f), lantern, 0, -26);
    }

    private GameObject CreateFlat(string name, Vector3 localPosition, Vector3 localScale, Color color, Transform parent, float zRotation, int sortingOrder)
    {
        GameObject flat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flat.name = name;
        flat.transform.SetParent(parent, false);
        flat.transform.localPosition = localPosition;
        flat.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        flat.transform.localScale = localScale;

        Collider collider = flat.GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }

        Renderer renderer = flat.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = GetColorMaterial(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
        }

        return flat;
    }

    private void CleanupSceneRoots()
    {
        Scene activeScene = gameObject.scene;
        if (!activeScene.IsValid())
        {
            return;
        }

        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            if (root == gameObject)
            {
                continue;
            }

            if (root.CompareTag("MainCamera") || root.name == "Directional Light" || root.name == "Global Volume")
            {
                continue;
            }

            DestroyImmediate(root);
        }
    }

    private void CacheBasePositions(Transform root)
    {
        basePositions.Clear();
        heroRoot = root.Find("Hero");
        enemyRedRoot = root.Find("EnemyRed");
        enemyGoldRoot = root.Find("EnemyGold");
        enemyRearLeftRoot = root.Find("EnemyRearLeft");
        enemyRearRightRoot = root.Find("EnemyRearRight");
        CacheTransformRecursive(root);
    }

    private void CacheTransformRecursive(Transform current)
    {
        basePositions[current] = current.localPosition;
        for (int i = 0; i < current.childCount; i++)
        {
            CacheTransformRecursive(current.GetChild(i));
        }
    }

    private void TriggerCard(CardType card)
    {
        activeCard = card;
        lastCardIndex = (int)card;
        playerCardHistory[(int)card]++;
        redReaction = ChooseReaction(card, 0);
        goldReaction = ChooseReaction(card, 1);
        battleShake = 1f;
        actionTimer = ActionDuration;

        SetSpriteForCard(heroRenderer, HeroSheet, HeroPoseForCard(card));
        SetSpriteForReaction(enemyRedRenderer, EnemyRedSheet, redReaction);
        SetSpriteForReaction(enemyGoldRenderer, EnemyGoldSheet, goldReaction);

        decisionLog = $"{CardLabel(card)} -> Hero {HeroActionLabel(card)}, Red {ReactionLabel(redReaction)}, Gold {ReactionLabel(goldReaction)}";
    }

    private string HeroActionLabel(CardType card)
    {
        switch (card)
        {
            case CardType.RapidBloom:
                return "slash";
            case CardType.CrescentDrive:
                return "lunge";
            case CardType.BreakerSigil:
                return "breaker stance";
            default:
                return "ready";
        }
    }

    private int HeroPoseForCard(CardType card)
    {
        switch (card)
        {
            case CardType.RapidBloom:
                return 1;
            case CardType.CrescentDrive:
                return 4;
            case CardType.BreakerSigil:
                return 2;
            default:
                return 0;
        }
    }

    private Vector3 HeroActionOffset(CardType card, float normalizedTime)
    {
        float arc = Mathf.Sin(normalizedTime * Mathf.PI);
        switch (card)
        {
            case CardType.RapidBloom:
                return new Vector3(0.48f * arc, 0.12f * arc, 0f);
            case CardType.CrescentDrive:
                return new Vector3(-0.38f * arc, 0.22f * arc, 0f);
            case CardType.BreakerSigil:
                return new Vector3(0f, 0.34f * arc, 0f);
            default:
                return Vector3.zero;
        }
    }

    private float HeroActionTilt(CardType card, float normalizedTime)
    {
        float arc = Mathf.Sin(normalizedTime * Mathf.PI);
        switch (card)
        {
            case CardType.RapidBloom:
                return -14f * arc;
            case CardType.CrescentDrive:
                return 16f * arc;
            case CardType.BreakerSigil:
                return -8f * arc;
            default:
                return 0f;
        }
    }

    private void SetSpriteForCard(SpriteRenderer renderer, string fileName, int poseIndex)
    {
        if (renderer == null)
        {
            return;
        }

        Sprite[] poses = LoadSheetSprites(fileName);
        if (poses == null || poses.Length == 0)
        {
            return;
        }

        renderer.sprite = poses[Mathf.Clamp(poseIndex, 0, poses.Length - 1)];
    }

    private void SetSpriteForReaction(SpriteRenderer renderer, string fileName, ReactionType reaction)
    {
        if (renderer == null)
        {
            return;
        }

        Sprite[] poses = LoadSheetSprites(fileName);
        if (poses == null || poses.Length == 0)
        {
            return;
        }

        int poseIndex = ReactionPoseIndex(reaction);
        renderer.sprite = poses[Mathf.Clamp(poseIndex, 0, poses.Length - 1)];
    }

    private int ReactionPoseIndex(ReactionType reaction)
    {
        switch (reaction)
        {
            case ReactionType.Brace:
                return 2;
            case ReactionType.Sidestep:
                return 3;
            case ReactionType.SplitFlank:
                return 1;
            case ReactionType.CounterLunge:
                return 4;
            case ReactionType.Retreat:
                return 5;
            default:
                return 0;
        }
    }

    private ReactionType ChooseReaction(CardType card, int soldierIndex)
    {
        List<WeightedReaction> options = new List<WeightedReaction>
        {
            new WeightedReaction(ReactionType.Brace, 1.0f),
            new WeightedReaction(ReactionType.Sidestep, 1.0f),
            new WeightedReaction(ReactionType.SplitFlank, 0.9f),
            new WeightedReaction(ReactionType.CounterLunge, 0.8f),
            new WeightedReaction(ReactionType.Retreat, 0.6f)
        };

        switch (card)
        {
            case CardType.RapidBloom:
                Boost(options, ReactionType.Brace, soldierIndex == 0 ? 1.65f : 0.75f);
                Boost(options, ReactionType.Sidestep, soldierIndex == 1 ? 0.95f : 0.25f);
                Boost(options, ReactionType.SplitFlank, soldierIndex == 1 ? 1.05f : 0.45f);
                break;
            case CardType.CrescentDrive:
                Boost(options, ReactionType.Sidestep, soldierIndex == 0 ? 1.1f : 1.55f);
                Boost(options, ReactionType.Retreat, soldierIndex == 0 ? 0.7f : 1.05f);
                Boost(options, ReactionType.Brace, soldierIndex == 0 ? 0.4f : 0.1f);
                break;
            case CardType.BreakerSigil:
                Boost(options, ReactionType.CounterLunge, soldierIndex == 0 ? 1.3f : 1.55f);
                Boost(options, ReactionType.Brace, soldierIndex == 0 ? 0.8f : 0.2f);
                Boost(options, ReactionType.Sidestep, soldierIndex == 1 ? 0.65f : 0.25f);
                break;
        }

        if (playerCardHistory[(int)card] > 1)
        {
            Boost(options, ReactionType.CounterLunge, 0.4f);
            Boost(options, ReactionType.Sidestep, 0.3f);
        }

        if (soldierIndex == 0)
        {
            Boost(options, ReactionType.Brace, 0.45f);
        }
        else
        {
            Boost(options, ReactionType.SplitFlank, 0.5f);
            Boost(options, ReactionType.Sidestep, 0.25f);
        }

        ReactionType otherReaction = soldierIndex == 0 ? goldReaction : redReaction;
        Boost(options, otherReaction, -0.25f);

        for (int i = 0; i < options.Count; i++)
        {
            options[i] = new WeightedReaction(options[i].Reaction, Mathf.Max(0.12f, options[i].Weight + Random.Range(-0.12f, 0.48f)));
        }

        float total = 0f;
        for (int i = 0; i < options.Count; i++)
        {
            total += options[i].Weight;
        }

        float roll = Random.value * total;
        float cumulative = 0f;
        for (int i = 0; i < options.Count; i++)
        {
            cumulative += options[i].Weight;
            if (roll <= cumulative)
            {
                return options[i].Reaction;
            }
        }

        return options[options.Count - 1].Reaction;
    }

    private void Boost(List<WeightedReaction> options, ReactionType reaction, float amount)
    {
        for (int i = 0; i < options.Count; i++)
        {
            if (options[i].Reaction == reaction)
            {
                options[i] = new WeightedReaction(reaction, options[i].Weight + amount);
                return;
            }
        }
    }

    private void AnimateActors(float timeValue)
    {
        float heroBob = Mathf.Sin(timeValue * 3.2f) * 0.05f;
        float enemyBob = Mathf.Sin(timeValue * 2.4f) * 0.045f;
        battleShake = Mathf.MoveTowards(battleShake, 0f, Time.deltaTime * 2.1f);
        float actionNormalized = actionTimer > 0f ? 1f - (actionTimer / ActionDuration) : 0f;
        Vector3 heroActionOffset = actionTimer > 0f ? HeroActionOffset(activeCard, actionNormalized) : Vector3.zero;

        SetAnimatedPosition(heroRoot, new Vector3(0f, heroBob, 0f) + heroActionOffset);
        SetAnimatedPosition(enemyRedRoot, ReactionOffset(redReaction, true) + new Vector3(0f, enemyBob, 0f));
        SetAnimatedPosition(enemyGoldRoot, ReactionOffset(goldReaction, false) + new Vector3(0f, -enemyBob, 0f));

        if (heroRoot != null)
        {
            float baseTilt = activeCard == CardType.CrescentDrive ? -3f : activeCard == CardType.BreakerSigil ? 2f : -1f;
            heroRoot.localRotation = Quaternion.Euler(0f, 0f, baseTilt + (actionTimer > 0f ? HeroActionTilt(activeCard, actionNormalized) : 0f));
        }

        if (enemyRedRoot != null)
        {
            enemyRedRoot.localRotation = Quaternion.Euler(0f, 0f, ReactionTilt(redReaction, true));
        }

        if (enemyGoldRoot != null)
        {
            enemyGoldRoot.localRotation = Quaternion.Euler(0f, 0f, ReactionTilt(goldReaction, false));
        }

        HighlightActiveCard();
    }

    private void SetAnimatedPosition(Transform target, Vector3 additiveOffset)
    {
        if (target == null || !basePositions.TryGetValue(target, out Vector3 basePosition))
        {
            return;
        }

        target.localPosition = basePosition + additiveOffset;
    }

    private Vector3 ReactionOffset(ReactionType reaction, bool isLeft)
    {
        float side = isLeft ? -1f : 1f;
        switch (reaction)
        {
            case ReactionType.Brace:
                return new Vector3(-0.08f * side, -0.04f, 0f);
            case ReactionType.Sidestep:
                return new Vector3(0.42f * side, 0.03f, 0f);
            case ReactionType.SplitFlank:
                return new Vector3(0.58f * side, 0.04f, 0f);
            case ReactionType.CounterLunge:
                return new Vector3(isLeft ? 0.26f : -0.26f, 0.08f, 0f);
            case ReactionType.Retreat:
                return new Vector3(0.14f * side, 0.18f, 0f);
            default:
                return Vector3.zero;
        }
    }

    private float ReactionTilt(ReactionType reaction, bool isLeft)
    {
        float side = isLeft ? 1f : -1f;
        switch (reaction)
        {
            case ReactionType.Brace:
                return 1f * side;
            case ReactionType.Sidestep:
                return 3f * side;
            case ReactionType.SplitFlank:
                return 5f * side;
            case ReactionType.CounterLunge:
                return -4f * side;
            case ReactionType.Retreat:
                return -2f * side;
            default:
                return 0f;
        }
    }

    private void HighlightActiveCard()
    {
        if (cardRoots == null)
        {
            return;
        }

        for (int i = 0; i < cardRoots.Length; i++)
        {
            Transform card = cardRoots[i];
            if (card == null || !basePositions.TryGetValue(card, out Vector3 basePosition))
            {
                continue;
            }

            float lift = i == (int)activeCard ? 0.20f : 0f;
            float sideOffset = i == 0 ? 0.03f : i == 2 ? 0.03f : 0f;
            card.localPosition = basePosition + new Vector3(0f, lift + sideOffset, 0f);
            card.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    private string CardLabel(CardType card)
    {
        switch (card)
        {
            case CardType.RapidBloom:
                return "Rapid Bloom";
            case CardType.CrescentDrive:
                return "Crescent Drive";
            case CardType.BreakerSigil:
                return "Breaker Sigil";
            default:
                return card.ToString();
        }
    }

    private string ReactionLabel(ReactionType reaction)
    {
        switch (reaction)
        {
            case ReactionType.Brace:
                return "Brace";
            case ReactionType.Sidestep:
                return "Sidestep";
            case ReactionType.SplitFlank:
                return "Split Flank";
            case ReactionType.CounterLunge:
                return "Counter Lunge";
            case ReactionType.Retreat:
                return "Retreat";
            default:
                return reaction.ToString();
        }
    }

    private void ConfigureLighting()
    {
        Light light = FindAnyObjectByType<Light>();
        if (light == null)
        {
            return;
        }

        light.color = new Color(1f, 0.95f, 0.88f);
        light.intensity = 0.9f;
        light.transform.rotation = Quaternion.Euler(16f, -10f, 0f);
    }

    private void ConfigureCamera(Vector3 focusPoint)
    {
        cachedCamera = Camera.main;
        if (cachedCamera == null)
        {
            cachedCamera = FindAnyObjectByType<Camera>();
        }

        if (cachedCamera == null)
        {
            return;
        }

        cachedCamera.transform.position = focusPoint + new Vector3(0f, 0f, -10f);
        cachedCamera.transform.rotation = Quaternion.identity;
        cachedCamera.orthographic = true;
        cachedCamera.orthographicSize = buildRpgVillageDemo ? 6.8f : buildKenneyTownDemo ? 8.9f : 5.3f;
        cachedCamera.clearFlags = CameraClearFlags.SolidColor;
        cachedCamera.backgroundColor = buildRpgVillageDemo ? new Color(0.38f, 0.68f, 0.87f) : buildKenneyTownDemo ? new Color(0.31f, 0.52f, 0.78f) : new Color(0.97f, 0.92f, 0.80f);
    }

    private Transform CreateGroup(string name, Vector3 localPosition, Transform parent)
    {
        Transform group = new GameObject(name).transform;
        group.SetParent(parent, false);
        group.localPosition = localPosition;
        return group;
    }

    private void FaceSpritesToCamera()
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return;
        }

        FaceRootSprite(heroRoot);
        FaceRootSprite(enemyRedRoot);
        FaceRootSprite(enemyGoldRoot);
    }

    private void FaceRootSprite(Transform root)
    {
        if (root == null || root.childCount == 0)
        {
            return;
        }

        Transform spriteTransform = root.GetChild(0);
        spriteTransform.rotation = Quaternion.LookRotation(cachedCamera.transform.forward, Vector3.up);
    }

    private Material GetColorMaterial(Color color)
    {
        if (colorMaterials.TryGetValue(color, out Material material))
        {
            return material;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        material = new Material(shader)
        {
            color = color,
            hideFlags = HideFlags.HideAndDontSave
        };

        colorMaterials[color] = material;
        return material;
    }

    private Texture2D LoadTexture(string fileName)
    {
        if (textureCache.TryGetValue(fileName, out Texture2D cached))
        {
            return cached;
        }

        string fullPath = Path.Combine(Application.dataPath, "Art", fileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        bool pixelSharp = IsPixelArtTexture(fileName);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = fileName,
            filterMode = pixelSharp ? FilterMode.Point : FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.LoadImage(bytes, false);
        textureCache[fileName] = texture;
        return texture;
    }

    private bool IsPixelArtTexture(string fileName)
    {
        return fileName.EndsWith(".png")
            && fileName != KeyArt
            && fileName != StageArt;
    }

    private Sprite LoadKenneySprite(int tileId)
    {
        string fileName = $"buildingTiles_{tileId:000}.png";
        string cacheKey = $"kenney::{fileName}";
        if (spriteCache.TryGetValue(cacheKey, out Sprite[] cached) && cached.Length > 0)
        {
            return cached[0];
        }

        string fullPath = Path.Combine(Application.dataPath, KenneyFolder, "PNG", fileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = cacheKey,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.LoadImage(bytes, false);

        Sprite sprite = CreateFullSprite(fileName, texture);
        spriteCache[cacheKey] = new[] { sprite };
        return sprite;
    }

    private Sprite LoadRpgSampleSprite(string fileName)
    {
        string cacheKey = $"rpgsample::{fileName}";
        if (spriteCache.TryGetValue(cacheKey, out Sprite[] cached) && cached.Length > 0)
        {
            return cached[0];
        }

        string fullPath = Path.Combine(Application.dataPath, RpgFolder, fileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = cacheKey,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.LoadImage(bytes, false);

        Sprite sprite = CreateFullSprite(fileName, texture);
        spriteCache[cacheKey] = new[] { sprite };
        return sprite;
    }

    private Sprite LoadRpgSprite(string fileName)
    {
        string cacheKey = $"rpg::{fileName}";
        if (spriteCache.TryGetValue(cacheKey, out Sprite[] cached) && cached.Length > 0)
        {
            return cached[0];
        }

        string fullPath = Path.Combine(Application.dataPath, RpgFolder, "PNG", fileName);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        byte[] bytes = File.ReadAllBytes(fullPath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = cacheKey,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.LoadImage(bytes, false);

        Sprite sprite = CreateFullSprite(fileName, texture);
        spriteCache[cacheKey] = new[] { sprite };
        return sprite;
    }

    private Sprite CreateFullSprite(string spriteName, Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
    }

    private TileBase GetRuntimeTile(string fileName, Tile.ColliderType colliderType = Tile.ColliderType.None, bool flipX = false, bool flipY = false)
    {
        string cacheKey = $"{fileName}|{colliderType}|{flipX}|{flipY}";
        if (runtimeTileCache.TryGetValue(cacheKey, out TileBase cached))
        {
            return cached;
        }

        Sprite sprite = LoadRpgSprite(fileName);
        if (sprite == null)
        {
            return null;
        }

        if (flipX || flipY)
        {
            sprite = CreateFlippedSprite(sprite, cacheKey, flipX, flipY);
        }

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = colliderType;
        runtimeTileCache[cacheKey] = tile;
        return tile;
    }

    private TileBase GetSolidColorTile(string cacheKey, Color color, Tile.ColliderType colliderType = Tile.ColliderType.None)
    {
        string finalKey = $"solid::{cacheKey}|{colliderType}";
        if (runtimeTileCache.TryGetValue(finalKey, out TileBase cached))
        {
            return cached;
        }

        Sprite sprite = GetSolidColorSprite(finalKey, color);
        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = colliderType;
        runtimeTileCache[finalKey] = tile;
        return tile;
    }

    private float GetRpgTileWorldSize()
    {
        Sprite sprite = LoadRpgSprite("rpgTile024.png");
        if (sprite == null)
        {
            return 64f / PixelsPerUnit;
        }

        return sprite.bounds.size.x;
    }

    private Sprite GetSolidColorSprite(string cacheKey, Color color)
    {
        if (generatedSpriteCache.TryGetValue(cacheKey, out Sprite cached))
        {
            return cached;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = cacheKey,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        generatedSpriteCache[cacheKey] = sprite;
        return sprite;
    }

    private Sprite CreateFlippedSprite(Sprite source, string spriteName, bool flipX, bool flipY)
    {
        Rect rect = source.rect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        Color[] pixels = source.texture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
        Color[] flipped = new Color[pixels.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int srcX = flipX ? width - 1 - x : x;
                int srcY = flipY ? height - 1 - y : y;
                flipped[(y * width) + x] = pixels[(srcY * width) + srcX];
            }
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            filterMode = source.texture.filterMode,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(flipped);
        texture.Apply(false, false);

        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    private Sprite[] LoadSheetSprites(string fileName)
    {
        if (spriteCache.TryGetValue(fileName, out Sprite[] cached))
        {
            return cached;
        }

        Texture2D texture = LoadTexture(fileName);
        if (texture == null)
        {
            return null;
        }

        GetSheetLayout(fileName, texture, out int columns, out int rows);
        int cellWidth = texture.width / columns;
        int cellHeight = texture.height / rows;
        Sprite[] sprites = new Sprite[columns * rows];

        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                Rect rect = new Rect(column * cellWidth, texture.height - ((row + 1) * cellHeight), cellWidth, cellHeight);
                sprites[index] = CreateProcessedPoseSprite(texture, rect, $"{fileName}_{index}");
                index++;
            }
        }

        spriteCache[fileName] = sprites;
        return sprites;
    }

    private void GetSheetLayout(string fileName, Texture2D texture, out int columns, out int rows)
    {
        switch (fileName)
        {
            case HeroSheet:
            case EnemyRedSheet:
            case EnemyGoldSheet:
            case "Pixel art swordswoman sprite sheet.png":
            case "Pixel art warrior sprite sheet.png":
            case "Medieval knight sprite sheet (1).png":
            case "Medieval golden-armored spear soldier sprite sheet.png":
                columns = 2;
                rows = 2;
                break;
            default:
                if (texture.width == texture.height)
                {
                    columns = 2;
                    rows = 2;
                }
                else
                {
                    columns = 3;
                    rows = 2;
                }
                break;
        }
    }

    private Sprite CreateProcessedPoseSprite(Texture2D source, Rect rect, string spriteName)
    {
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        Color[] sourcePixels = source.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height);
        Color[] processedPixels = new Color[sourcePixels.Length];
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            float y01 = (float)y / Mathf.Max(1, height - 1);
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                Color pixel = sourcePixels[idx];

                if (pixel.a < 0.01f)
                {
                    processedPixels[idx] = pixel;
                    continue;
                }

                Color.RGBToHSV(pixel, out float hue, out float saturation, out float value);
                bool inTileZone = y01 < 0.38f;
                bool stoneLike = saturation < 0.22f && value > 0.28f && value < 0.86f;
                bool warmStone = hue > 0.06f && hue < 0.15f && saturation < 0.30f && value > 0.42f;

                if (inTileZone && (stoneLike || warmStone))
                {
                    pixel.a = 0f;
                }

                processedPixels[idx] = pixel;

                if (pixel.a > 0.08f)
                {
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }

        Texture2D processed = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        processed.SetPixels(processedPixels);
        processed.Apply(false, false);

        if (maxX <= minX || maxY <= minY)
        {
            return Sprite.Create(
                processed,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.08f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        int padding = 12;
        int cropX = Mathf.Max(0, minX - padding);
        int cropY = Mathf.Max(0, minY - padding);
        int cropW = Mathf.Min(width - cropX, (maxX - minX) + 1 + (padding * 2));
        int cropH = Mathf.Min(height - cropY, (maxY - minY) + 1 + (padding * 2));

        return Sprite.Create(
            processed,
            new Rect(cropX, cropY, cropW, cropH),
            new Vector2(0.5f, 0.08f),
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
    }
}
