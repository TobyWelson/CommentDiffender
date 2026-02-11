#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using System.Linq;

public class KingsMarchSetup : EditorWindow
{
    private const string PREFAB_FOLDER = "Assets/Prefabs/KingsMarch";
    private const string SCENE_PATH = "Assets/Scenes/KingsMarch.unity";
    private const string TINY_SWORDS_UNITS = "Assets/Tiny Swords/Units";
    private const string TINY_SWORDS_PAWN = "Assets/Tiny Swords/Pawn and Resources/Pawn";
    private const string TINY_SWORDS_BUILDINGS = "Assets/Tiny Swords/Buildings";

    [MenuItem("Tools/Streamer King/Full Setup")]
    public static void FullSetup()
    {
        EnsureFolders();
        CreateUnitPrefabs();
        FixAnimationLooping();
        SetupScene();
        EditorUtility.DisplayDialog("Streamer King", "Setup complete! Press Play to test.", "OK");
    }

    [MenuItem("Tools/Streamer King/Fix Animation Looping")]
    public static void FixAnimationLooping()
    {
        string[] animFolders = {
            $"{TINY_SWORDS_UNITS}/Blue Units",
            $"{TINY_SWORDS_UNITS}/Red Units",
            $"{TINY_SWORDS_UNITS}/Purple Units",
            $"{TINY_SWORDS_UNITS}/Yellow Units",
            $"{TINY_SWORDS_UNITS}/Black Units",
            TINY_SWORDS_PAWN
        };

        int fixedCount = 0;
        foreach (var folder in animFolders)
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                bool shouldLoop = fileName.Contains("Idle") || fileName.Contains("Run");
                if (!shouldLoop) continue;

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                if (settings.loopTime) continue;

                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                fixedCount++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[StreamerKing] Fixed looping on {fixedCount} animation clips.");
    }

    [MenuItem("Tools/Streamer King/Create Prefabs Only")]
    public static void CreatePrefabsOnly()
    {
        EnsureFolders();
        CreateUnitPrefabs();
        EditorUtility.DisplayDialog("Streamer King", "Prefabs created!", "OK");
    }

    [MenuItem("Tools/Streamer King/Reassign Prefabs")]
    public static void ReassignPrefabs()
    {
        var gm = Object.FindObjectOfType<GameManager>();
        if (gm == null)
        {
            EditorUtility.DisplayDialog("Streamer King", "GameManager not found in scene!", "OK");
            return;
        }
        AssignAllPrefabs(gm);
        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Streamer King",
            "Prefabs reassigned to existing GameManager!\nDon't forget to save the scene.", "OK");
    }

    [MenuItem("Tools/Streamer King/Setup Scene Only")]
    public static void SetupSceneOnly()
    {
        SetupScene();
    }

    [MenuItem("Tools/Streamer King/Rebuild Map Only")]
    public static void RebuildMapOnly()
    {
        if (!EditorUtility.DisplayDialog("Streamer King",
            "WARNING: This will DELETE the existing map and recreate it from scratch.\nAll painted tiles will be lost!\n\nAre you sure?",
            "Delete & Rebuild", "Cancel"))
            return;

        foreach (var name in new[] { "Map", "Background_Ground", "Background_Road" })
        {
            var obj = GameObject.Find(name);
            if (obj != null) DestroyImmediate(obj);
        }

        CreateBackground();
        Debug.Log("[StreamerKing] Map rebuilt with Tilemap ground!");
    }

    [MenuItem("Tools/Streamer King/Add Water Layer")]
    public static void AddWaterLayer()
    {
        var map = GameObject.Find("Map");
        if (map == null) { Debug.LogError("[StreamerKing] Map not found in scene!"); return; }

        var gridGo = map.transform.Find("TileGrid");
        if (gridGo == null) { Debug.LogError("[StreamerKing] TileGrid not found under Map!"); return; }

        if (gridGo.Find("Water") != null)
        {
            Debug.LogWarning("[StreamerKing] Water layer already exists!");
            return;
        }

        var waterGo = new GameObject("Water");
        waterGo.transform.SetParent(gridGo);
        waterGo.transform.SetAsFirstSibling();
        waterGo.AddComponent<Tilemap>();
        var waterRenderer = waterGo.AddComponent<TilemapRenderer>();
        waterRenderer.sortingOrder = -15;

        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[StreamerKing] Water layer added!");
    }

    [MenuItem("Tools/Streamer King/Create Tile Palette")]
    public static void CreateTilePalette()
    {
        const string PALETTE_FOLDER = "Assets/Tile Palettes";
        const string PALETTE_PATH = PALETTE_FOLDER + "/KingsMarch Palette.prefab";
        const string TILEMAP_SETTINGS = "Assets/Tiny Swords/Terrain/Tileset/Tilemap Settings";
        const string SLICED = TILEMAP_SETTINGS + "/Sliced Tiles";

        if (!AssetDatabase.IsValidFolder(PALETTE_FOLDER))
            AssetDatabase.CreateFolder("Assets", "Tile Palettes");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PALETTE_PATH) != null)
            AssetDatabase.DeleteAsset(PALETTE_PATH);

        var paletteGo = new GameObject("KingsMarch Palette");
        var grid = paletteGo.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        var layerGo = new GameObject("Layer1");
        layerGo.transform.SetParent(paletteGo.transform);
        var tilemap = layerGo.AddComponent<Tilemap>();
        layerGo.AddComponent<TilemapRenderer>();

        int tileCount = 0;
        int row = 0;

        for (int color = 1; color <= 5; color++)
        {
            for (int i = 0; i < 44; i++)
            {
                string path = $"{SLICED}/Tilemap_color{color}_{i}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile != null)
                {
                    tilemap.SetTile(new Vector3Int(i, -row, 0), tile);
                    tileCount++;
                }
            }
            row++;
        }

        var waterBg = AssetDatabase.LoadAssetAtPath<TileBase>($"{TILEMAP_SETTINGS}/Water Background color.asset");
        if (waterBg != null) { tilemap.SetTile(new Vector3Int(0, -row, 0), waterBg); tileCount++; }
        var shadow = AssetDatabase.LoadAssetAtPath<TileBase>($"{TILEMAP_SETTINGS}/Shadow.asset");
        if (shadow != null) { tilemap.SetTile(new Vector3Int(1, -row, 0), shadow); tileCount++; }
        var animWater = AssetDatabase.LoadAssetAtPath<TileBase>($"{TILEMAP_SETTINGS}/Water Tile animated.asset");
        if (animWater != null) { tilemap.SetTile(new Vector3Int(2, -row, 0), animWater); tileCount++; }

        PrefabUtility.SaveAsPrefabAsset(paletteGo, PALETTE_PATH);
        DestroyImmediate(paletteGo);

        var gridPalette = ScriptableObject.CreateInstance<GridPalette>();
        gridPalette.name = "Palette Settings";
        gridPalette.cellSizing = GridPalette.CellSizing.Automatic;
        AssetDatabase.AddObjectToAsset(gridPalette, PALETTE_PATH);
        AssetDatabase.ImportAsset(PALETTE_PATH);

        Debug.Log($"[StreamerKing] Tile Palette created: {tileCount} tiles at {PALETTE_PATH}");
        EditorUtility.DisplayDialog("Streamer King",
            $"Tile Palette created! ({tileCount} tiles)\n\n" +
            "Open: Window > 2D > Tile Palette\n" +
            "Select 'KingsMarch Palette' from dropdown", "OK");
    }

    static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PREFAB_FOLDER))
            AssetDatabase.CreateFolder("Assets/Prefabs", "KingsMarch");
    }

    // ─── Prefab Creation ─────────────────────────────────────────

    static void CreateUnitPrefabs()
    {
        // Ally unit color variants (5 colors × 4 types = 20 prefabs)
        string[] colorFolders = { "Blue Units", "Red Units", "Purple Units", "Yellow Units", "Black Units" };
        string[] colorNames = { "Blue", "Red", "Purple", "Yellow", "Black" };
        string[] types = { "Warrior", "Lancer", "Archer", "Monk" };

        for (int c = 0; c < colorFolders.Length; c++)
        {
            for (int t = 0; t < types.Length; t++)
            {
                CreateSingleUnitPrefab(
                    $"{TINY_SWORDS_UNITS}/{colorFolders[c]}/{types[t]}",
                    colorNames[c], types[t], (UnitType)t,
                    $"{types[t]} {colorNames[c]} Animations");
            }
        }

        // Pawn prefab for enemies (Red Pawn)
        CreatePawnPrefab("Red");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StreamerKing] All prefabs created (20 unit variants + 1 Pawn).");
    }

    static void CreateSingleUnitPrefab(string unitFolder, string colorName, string typeName,
        UnitType unitType, string animFolderName)
    {
        Sprite idleSprite = FindIdleSprite(unitFolder, typeName);

        string animFolder = $"{unitFolder}/{animFolderName}";
        RuntimeAnimatorController animCtrl = FindAnimatorController(animFolder, unitFolder);

        var go = new GameObject($"{colorName}_{typeName}");

        var sr = go.AddComponent<SpriteRenderer>();
        if (idleSprite != null)
        {
            sr.sprite = idleSprite;
            sr.sortingOrder = 10;
        }
        else
        {
            sr.sprite = CreateFallbackSprite(Team.Ally);
            sr.sortingOrder = 10;
            Debug.LogWarning($"[StreamerKing] No sprite found for {colorName} {typeName}, using fallback");
        }

        if (animCtrl != null)
        {
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;
        }
        else
        {
            Debug.LogWarning($"[StreamerKing] No animator found for {colorName} {typeName} in {animFolder}");
        }

        var unit = go.AddComponent<Unit>();
        unit.unitType = unitType;
        unit.team = Team.Ally;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = GameConfig.UnitRadius;
        col.isTrigger = true;

        if (idleSprite != null)
        {
            float targetHeight = 1.6f;
            float spriteHeight = idleSprite.bounds.size.y;
            if (spriteHeight > 0)
                go.transform.localScale = Vector3.one * (targetHeight / spriteHeight);
        }
        else
        {
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        }

        string prefabPath = $"{PREFAB_FOLDER}/{colorName}_{typeName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);
        Debug.Log($"[StreamerKing] Created prefab: {prefabPath}");
    }

    static void CreatePawnPrefab(string colorName)
    {
        string pawnFolder = $"{TINY_SWORDS_PAWN}/{colorName} Pawn";
        string animFolder = $"{pawnFolder}/Pawn {colorName} Animations";

        // Find idle sprite
        Sprite idleSprite = null;
        string idlePath = $"{pawnFolder}/Pawn_Idle.png";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(idlePath).OfType<Sprite>().ToArray();
        if (sprites.Length > 0) idleSprite = sprites[0];

        RuntimeAnimatorController animCtrl = FindAnimatorController(animFolder, pawnFolder);

        var go = new GameObject($"{colorName}_Pawn");

        var sr = go.AddComponent<SpriteRenderer>();
        if (idleSprite != null)
        {
            sr.sprite = idleSprite;
            sr.sortingOrder = 10;
        }
        else
        {
            sr.sprite = CreateFallbackSprite(Team.Enemy);
            sr.sortingOrder = 10;
            Debug.LogWarning($"[StreamerKing] No sprite found for {colorName} Pawn");
        }

        if (animCtrl != null)
        {
            var anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = animCtrl;
        }
        else
        {
            Debug.LogWarning($"[StreamerKing] No animator found for {colorName} Pawn in {animFolder}");
        }

        var unit = go.AddComponent<Unit>();
        unit.unitType = UnitType.Warrior; // default, overridden at runtime
        unit.team = Team.Enemy;

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = GameConfig.UnitRadius;
        col.isTrigger = true;

        if (idleSprite != null)
        {
            float targetHeight = 1.6f;
            float spriteHeight = idleSprite.bounds.size.y;
            if (spriteHeight > 0)
                go.transform.localScale = Vector3.one * (targetHeight / spriteHeight);
        }
        else
        {
            go.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
        }

        string prefabPath = $"{PREFAB_FOLDER}/{colorName}_Pawn.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);
        Debug.Log($"[StreamerKing] Created Pawn prefab: {prefabPath}");
    }

    static Sprite FindIdleSprite(string unitFolder, string typeName)
    {
        string[] candidates;
        if (typeName == "Monk")
            candidates = new[] { "Idle" };
        else
            candidates = new[] { $"{typeName}_Idle", "Idle" };

        foreach (var candidate in candidates)
        {
            string[] guids = AssetDatabase.FindAssets(candidate, new[] { unitFolder });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;
                if (path.Contains("Animation")) continue;

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .ToArray();

                if (sprites.Length > 0)
                    return sprites[0];
            }
        }
        return null;
    }

    static RuntimeAnimatorController FindAnimatorController(string animFolder, string unitFolder)
    {
        if (AssetDatabase.IsValidFolder(animFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { animFolder });
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        // Fallback: search in the unit folder itself
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { unitFolder });
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    static Sprite CreateFallbackSprite(Team team)
    {
        var tex = new Texture2D(32, 32);
        Color col = team == Team.Ally ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.3f, 0.3f);
        for (int x = 0; x < 32; x++)
            for (int y = 0; y < 32; y++)
            {
                float dx = x - 16f, dy = y - 16f;
                if (dx * dx + dy * dy < 16 * 16)
                    tex.SetPixel(x, y, col);
                else
                    tex.SetPixel(x, y, Color.clear);
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
    }

    // ─── Assign Prefabs ──────────────────────────────────────────

    static void AssignAllPrefabs(GameManager gm)
    {
        // Pixel Heroes AnimatorController
        gm.pixelHeroAnimator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/PixelFantasy/PixelHeroes/Common/Animation/Character/Controller.controller");

        Debug.Log("[StreamerKing] Pixel Heroes assets assigned to GameManager.");
    }

    // ─── Scene Setup ─────────────────────────────────────────────

    static void SetupScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0, 0, -10);
            cam.orthographic = true;
            cam.orthographicSize = 5.5f;
            cam.backgroundColor = new Color(0.15f, 0.25f, 0.1f);
        }

        CreateBackground();
        CreateCastle();
        CreateGameManager();

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        Debug.Log($"[StreamerKing] Scene saved to {SCENE_PATH}");
    }

    static void CreateBackground()
    {
        var mapRoot = new GameObject("Map");
        var gridGo = new GameObject("TileGrid");
        gridGo.transform.SetParent(mapRoot.transform);
        var grid = gridGo.AddComponent<Grid>();
        grid.cellSize = new Vector3(1f, 1f, 0f);

        var waterGo = new GameObject("Water");
        waterGo.transform.SetParent(gridGo.transform);
        waterGo.AddComponent<Tilemap>();
        waterGo.AddComponent<TilemapRenderer>().sortingOrder = -15;

        var groundGo = new GameObject("Ground");
        groundGo.transform.SetParent(gridGo.transform);
        groundGo.AddComponent<Tilemap>();
        groundGo.AddComponent<TilemapRenderer>().sortingOrder = -10;

        var decoGo = new GameObject("Decoration");
        decoGo.transform.SetParent(gridGo.transform);
        decoGo.AddComponent<Tilemap>();
        decoGo.AddComponent<TilemapRenderer>().sortingOrder = -5;
    }

    static void CreateCastle()
    {
        var castleGo = new GameObject("Castle");
        castleGo.transform.position = new Vector3(GameConfig.CastleX, GameConfig.CastleY, 0);
        castleGo.AddComponent<Castle>();

        var sr = castleGo.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;

        string castlePath = $"{TINY_SWORDS_BUILDINGS}/Blue Buildings/Castle.png";
        var sprites = AssetDatabase.LoadAllAssetsAtPath(castlePath).OfType<Sprite>().ToArray();
        if (sprites.Length > 0)
        {
            sr.sprite = sprites[0];
            float targetHeight = 3f;
            float spriteHeight = sr.sprite.bounds.size.y;
            if (spriteHeight > 0)
                castleGo.transform.localScale = Vector3.one * (targetHeight / spriteHeight);
        }
        else
        {
            sr.sprite = CreatePixelSprite();
            sr.color = new Color(0.3f, 0.3f, 0.7f, 1f);
            castleGo.transform.localScale = new Vector3(1.5f, 3f, 1f);
        }
    }

    static void CreateGameManager()
    {
        var gmGo = new GameObject("GameManager");
        var gm = gmGo.AddComponent<GameManager>();
        gmGo.AddComponent<WaveManager>();
        gmGo.AddComponent<UIManager>();
        gmGo.AddComponent<DragDropController>();
        gmGo.AddComponent<YouTubeChatManager>();

        AssignAllPrefabs(gm);
    }

    static Sprite CreatePixelSprite()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }
}
#endif
