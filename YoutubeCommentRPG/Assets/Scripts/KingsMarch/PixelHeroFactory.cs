using UnityEngine;
using UnityEngine.U2D.Animation;
using Assets.PixelFantasy.Common.Scripts.CollectionScripts;
using Assets.PixelFantasy.PixelHeroes.Common.Scripts.CharacterScripts;

/// <summary>
/// Pixel Heroes キャラクターをランダム生成するファクトリ。
/// 味方: Human固定 + ロール別武器 + ランダム外見
/// 敵:  非Human種族(Wave統一) + ランダム装備
/// </summary>
public static class PixelHeroFactory
{
    // ─── 武器カテゴリ（ロール別） ──────────────────────────────

    static readonly string[] WarriorWeapons =
    {
        "Sword", "Longsword", "Katana", "LongKatana", "RedKatana", "ShinobuSword",
        "Greatsword", "IronSword", "AssaultSword", "BastardSword", "BlackBroadsword",
        "RustedShortSword", "Saber", "Epee", "Cutlass", "Blade", "Slasher", "Tanto",
        "Axe", "BattleAxe", "SmallAxe", "KitchenAxe", "WoodcutterAxe",
        "Knife", "HunterKnife", "ShortDagger", "MarderDagger", "WideDagger",
        "Cleaver", "Butcher", "Sickle", "SpikedClub", "WoodenClub"
    };

    static readonly string[] WarriorRareWeapons =
    {
        "GiantSword", "GiantBlade", "Executioner", "Punisher", "RoyalLongsword",
        "Greataxe", "MasterGreataxe"
    };

    static readonly string[] LancerWeapons =
    {
        "Halberd", "Lance", "Pitchfork", "SmallPitchfork", "Fork", "Scythe",
        "Bident", "Rake",
        "Hammer", "BattleHammer", "RustedHammer", "SmallDwarfHammer", "BlacksmithHammer",
        "Mace", "RoundMace", "Morgenstern", "Shestoper", "Crusher",
        "Pickaxe", "RustedPickaxe"
    };

    static readonly string[] LancerRareWeapons =
    {
        "GuardianHalberd", "TournamentLance", "DeathScythe", "LargeScythe",
        "GreatHammer", "LargeDwarfHammer", "LargePickaxe"
    };

    static readonly string[] ArcherWeapons =
    {
        "Bow", "ShortBow", "CurvedBow"
    };

    static readonly string[] ArcherRareWeapons =
    {
        "LongBow", "BattleBow"
    };

    static readonly string[] MonkWeapons =
    {
        "BishopStaff", "ArchStaff", "ElderStaff", "HermitStaff", "FlameStaff", "WoodenStuff"
    };

    static readonly string[] MonkRareWeapons =
    {
        "NecromancerStaff", "StormStaff"
    };

    static readonly string[] MageWeapons =
    {
        "MagicWand", "PriestWand", "AmurWand", "BlueWand", "CrystalWand",
        "FireWand", "GreenWand", "NatureWand", "RedWand", "SkullWand", "WaterWand"
    };

    static readonly string[] MageRareWeapons =
    {
        "MasterWand", "GoldenSkepter", "GoldenSkullWand"
    };

    // ─── 敵種族（Human以外、18種） ─────────────────────────────

    public static readonly string[] EnemyRaces =
    {
        "Goblin", "Orc", "Skeleton", "ZombieA", "ZombieB",
        "Elf", "DarkElf", "Lizard", "FireLizard", "Froggy",
        "Merman", "Furry", "Teddy", "Drakosha",
        "Vampire", "Werewolf", "Demon", "Demigod"
    };

    // Wave→種族マッピング（後半ほど強い種族）
    static readonly int[] WaveRaceIndex = { 0, 1, 2, 3, 5, 8, 13, 14, 16, 17 };

    // ─── BossPack1 モンスター ────────────────────────────────────

    // BossPack1 全プレハブ（色はランダム）
    static readonly string[][] AllBosses =
    {
        new[] { "Dragon", "GreenDragon" }, new[] { "Dragon", "BlueDragon" }, new[] { "Dragon", "RedDragon" },
        new[] { "Rex", "GreenRex" }, new[] { "Rex", "BlueRex" }, new[] { "Rex", "RedRex" },
        new[] { "Ogre", "GreeenOgre" }, new[] { "Ogre", "RedOgre" }, new[] { "Ogre", "PurpleOgre" },
        new[] { "Troll", "GreenTroll" }, new[] { "Troll", "BlueTroll" },
        new[] { "Werewolf", "RedWerevolf" }, new[] { "Werewolf", "BrownWerewolf" }, new[] { "Werewolf", "BlackWerewolf" },
        new[] { "MegaPumpkin", "GreenMegaPumpkin" }, new[] { "MegaPumpkin", "PurpleMegaPumpkin" }, new[] { "MegaPumpkin", "YellowMegaPumpkin" },
        new[] { "Troll", "BlackTroll" }
    };

    // ─── キャッシュ ────────────────────────────────────────────

    private static SpriteCollection cachedCollection;

    static SpriteCollection GetSpriteCollection()
    {
        if (cachedCollection == null)
            cachedCollection = Resources.Load<SpriteCollection>("SpriteCollection");
        return cachedCollection;
    }

    // ─── 公開メソッド ──────────────────────────────────────────

    /// <summary>味方ヒーロー生成（Human固定、ロール別武器、ランダム外見）</summary>
    public static GameObject CreateAllyHero(UnitType role, int gachaTier = 0, int startLevel = 1)
    {
        return CreateAllyHeroInternal(role, gachaTier, startLevel, null);
    }

    /// <summary>保存済み外見で味方ヒーロー生成（キューからのドラッグ用）</summary>
    public static GameObject CreateAllyHeroFromAppearance(UnitType role, HeroAppearance appearance, int startLevel = 1)
    {
        return CreateAllyHeroInternal(role, 0, startLevel, appearance);
    }

    static GameObject CreateAllyHeroInternal(UnitType role, int gachaTier, int startLevel, HeroAppearance appearance)
    {
        var go = BuildCharacterObject("AllyHero");
        var builder = go.GetComponent<CharacterBuilder>();

        // Human固定
        builder.Body = "Human";
        builder.Head = "Human";
        builder.Eyes = "Human";
        builder.Ears = "Human";
        builder.Arms = "Human";

        if (appearance != null)
        {
            // 保存済み外見を適用
            builder.Hair = appearance.hair;
            builder.Weapon = appearance.weapon;
            builder.Helmet = appearance.helmet;
            builder.Armor = appearance.armor;
            builder.Shield = appearance.shield;
            builder.Back = appearance.back;
        }
        else
        {
            // ランダム生成
            builder.RandomizeHumanAppearance();
            builder.Weapon = PickWeaponForRole(role, gachaTier);

            int emptyChance = Mathf.Max(0, 20 - gachaTier * 5);
            builder.Helmet = RandomizeFromLayer(builder, "Helmet", emptyChance);
            builder.Armor = RandomizeFromLayer(builder, "Armor", emptyChance);

            if (builder.Weapon.Contains("Bow"))
            {
                builder.Shield = "";
                builder.Back = "LeatherQuiver";
            }
            else
            {
                int shieldEmpty = Mathf.Max(10, 50 - gachaTier * 10);
                builder.Shield = RandomizeFromLayer(builder, "Shield", shieldEmpty);
            }
        }

        // テクスチャ合成 → アクティブ化
        builder.Rebuild();
        go.SetActive(true);

        // サイズ（ベーススケール × レベル補正）
        float scale = GameConfig.PixelHeroBaseScale * GetScaleForLevel(startLevel);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        return go;
    }

    /// <summary>敵ヒーロー生成（非Human種族統一、装備ランダム）</summary>
    public static GameObject CreateEnemyHero(string race, int wave = 1, bool isBoss = false)
    {
        var go = BuildCharacterObject("EnemyHero");
        var builder = go.GetComponent<CharacterBuilder>();

        // 種族統一
        builder.Body = race;
        builder.Head = race;
        builder.Eyes = race;
        builder.Ears = race;
        builder.Arms = race;

        // 装備ランダム
        builder.RandomizeEquipment();

        // テクスチャ合成 → アクティブ化
        builder.Rebuild();
        go.SetActive(true);

        // ベーススケール適用（ボスは大きく）
        float baseScale = GameConfig.PixelHeroBaseScale;
        if (isBoss)
        {
            float bossMultiplier = 1.5f + (wave - 1) * 0.05f;
            baseScale *= bossMultiplier;
        }
        go.transform.localScale = new Vector3(baseScale, baseScale, 1f);

        return go;
    }

    /// <summary>BossPack1からボスモンスターを生成（Wave統一カラー準拠）</summary>
    public static GameObject CreateBossMonster(int waveIndex)
    {
        // 全BossPack1からランダム選出
        var pick = AllBosses[Random.Range(0, AllBosses.Length)];
        string folder = pick[0];
        string prefabName = pick[1];

#if UNITY_EDITOR
        string path = $"Assets/PixelFantasy/PixelMonsters/BossPack1/{folder}/{prefabName}.prefab";
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning($"[BossMonster] Prefab not found: {path}"); return null; }

        var go = Object.Instantiate(prefab);
        go.name = prefabName;

        // 不要コンポーネント除去
        // RequireComponent依存チェーン: MonsterControls→MonsterController2D→Rigidbody2D/Collider2D
        // まずRigidbody2Dを即座に無効化（重力落下防止）、その後依存順に削除
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>()) { rb.simulated = false; rb.bodyType = RigidbodyType2D.Kinematic; }
        var mctrl = go.GetComponent("MonsterControls") as MonoBehaviour;
        if (mctrl != null) Object.DestroyImmediate(mctrl);
        var mc2d = go.GetComponent("MonsterController2D") as MonoBehaviour;
        if (mc2d != null) Object.DestroyImmediate(mc2d);
        var manim = go.GetComponent("MonsterAnimation") as MonoBehaviour;
        if (manim != null) Object.DestroyImmediate(manim);
        var monster = go.GetComponent("Monster") as MonoBehaviour;
        if (monster != null) Object.DestroyImmediate(monster);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>()) Object.DestroyImmediate(rb);
        foreach (var col in go.GetComponentsInChildren<Collider2D>()) Object.DestroyImmediate(col);

        Debug.Log($"[BossMonster] Created {prefabName} (wave={waveIndex})");
        return go;
#else
        // ビルド用: Resources/BossPack1 から読み込み
        var prefab = Resources.Load<GameObject>($"BossPack1/{folder}/{prefabName}");
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab);
        go.name = prefabName;
        // まずRigidbody2Dを即座に無効化（Object.Destroyは次フレームまで遅延するため）
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>()) { rb.simulated = false; rb.bodyType = RigidbodyType2D.Kinematic; }
        var mctrl = go.GetComponent("MonsterControls") as MonoBehaviour;
        if (mctrl != null) Object.Destroy(mctrl);
        var mc2d = go.GetComponent("MonsterController2D") as MonoBehaviour;
        if (mc2d != null) Object.Destroy(mc2d);
        var manim = go.GetComponent("MonsterAnimation") as MonoBehaviour;
        if (manim != null) Object.Destroy(manim);
        var monster = go.GetComponent("Monster") as MonoBehaviour;
        if (monster != null) Object.Destroy(monster);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody2D>()) Object.Destroy(rb);
        foreach (var col in go.GetComponentsInChildren<Collider2D>()) Object.Destroy(col);
        return go;
#endif
    }

    /// <summary>Wave番号から敵種族名を取得</summary>
    public static string GetRaceForWave(int waveIndex)
    {
        int idx = Mathf.Clamp(waveIndex, 0, WaveRaceIndex.Length - 1);
        return EnemyRaces[WaveRaceIndex[idx]];
    }

    /// <summary>レベル→スケール変換</summary>
    public static float GetScaleForLevel(int level)
    {
        // Lv1=1.0, Lv10=1.27, Lv20=1.57
        return 1.0f + (level - 1) * 0.03f;
    }

    /// <summary>キューアイコン用プレビュースプライト生成（Idle_0フレーム抽出）+ 外見保存</summary>
    public static Sprite CreatePreviewSprite(UnitType role, int gachaTier, out HeroAppearance appearance)
    {
        var go = BuildCharacterObject("PreviewHero");
        var builder = go.GetComponent<CharacterBuilder>();

        builder.Body = "Human";
        builder.Head = "Human";
        builder.Eyes = "Human";
        builder.Ears = "Human";
        builder.Arms = "Human";
        builder.RandomizeHumanAppearance();
        builder.Weapon = PickWeaponForRole(role, gachaTier);

        int emptyChance = Mathf.Max(0, 20 - gachaTier * 5);
        builder.Helmet = RandomizeFromLayer(builder, "Helmet", emptyChance);
        builder.Armor = RandomizeFromLayer(builder, "Armor", emptyChance);

        if (builder.Weapon.Contains("Bow"))
        {
            builder.Shield = "";
            builder.Back = "LeatherQuiver";
        }
        else
        {
            int shieldEmpty = Mathf.Max(10, 50 - gachaTier * 10);
            builder.Shield = RandomizeFromLayer(builder, "Shield", shieldEmpty);
        }

        // 外見をキャプチャ（スポーン時に同じキャラを再現するため）
        appearance = new HeroAppearance
        {
            hair = builder.Hair,
            weapon = builder.Weapon,
            helmet = builder.Helmet,
            armor = builder.Armor,
            shield = builder.Shield,
            back = builder.Back
        };

        builder.Rebuild();

        // Idle_0フレーム抽出 (Layout: x=0, y=832, 64x64)
        Sprite preview = ExtractIdleSprite(builder.Texture);

        Object.Destroy(go);
        return preview;
    }

    /// <summary>合成テクスチャからIdle_0フレームをSprite化</summary>
    static Sprite ExtractIdleSprite(Texture2D compositeTexture)
    {
        if (compositeTexture == null) return null;

        // Idle_0: { 0, 832, 64, 64, 32, 8 } from CharacterBuilder.Layout
        int x = 0, y = 832, w = 64, h = 64;
        var pixels = compositeTexture.GetPixels(x, y, w, h);
        var frameTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        frameTex.filterMode = FilterMode.Point;
        frameTex.SetPixels(pixels);
        frameTex.Apply();

        return Sprite.Create(frameTex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 64);
    }

    // ─── 内部メソッド ──────────────────────────────────────────

    /// <summary>GameObject階層構築（Character.prefab不使用）</summary>
    static GameObject BuildCharacterObject(string name)
    {
        var go = new GameObject(name);
        // Awake()でRebuild()が走るのを防ぐため、コンポーネント追加前に非アクティブ化
        go.SetActive(false);

        // Body子オブジェクト（SpriteRenderer + SpriteLibrary + SpriteResolver）
        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(go.transform, false);

        var sr = bodyGo.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        bodyGo.AddComponent<SpriteLibrary>();
        bodyGo.AddComponent<SpriteResolver>();

        // Character (Pixel Heroes)
        // OnValidate()がAddComponent時に発火しNPEが出るためログを一時抑制
        Debug.unityLogger.logEnabled = false;
        var character = go.AddComponent<Character>();
        Debug.unityLogger.logEnabled = true;
        character.Body = sr;
        character.Firearm = new Firearm();

        // Animator
        var animator = go.AddComponent<Animator>();
        character.Animator = animator;
        var ctrl = GetAnimatorController();
        if (ctrl != null) animator.runtimeAnimatorController = ctrl;

        // CharacterBuilder（RebuildOnStart=falseで手動Rebuild制御）
        var builder = go.AddComponent<CharacterBuilder>();
        builder.SpriteCollection = GetSpriteCollection();
        builder.Character = character;
        builder.RebuildOnStart = false;

        // CharacterBuilderBase のstring型フィールドは初期値nullのものがある
        // BuildLayers()内の `if (field != "")` チェックはnullを通してしまうため
        // 全フィールドを明示的に初期化
        builder.Cape = "";
        builder.Firearm = "";
        builder.Mask = "";
        builder.Horns = "";
        builder.Back = "";
        builder.Hair = "";
        builder.Mouth = "";
        builder.Armor = "";
        builder.Helmet = "";
        builder.Weapon = "";
        builder.Shield = "";

        return go;
    }

    /// <summary>AnimatorControllerを取得（GameManager経由 or EditorAutoLoad）</summary>
    static RuntimeAnimatorController GetAnimatorController()
    {
        // GameManagerに事前ロードされたコントローラーを使用
        if (GameManager.Instance != null && GameManager.Instance.pixelHeroAnimator != null)
            return GameManager.Instance.pixelHeroAnimator;

#if UNITY_EDITOR
        // エディタではAssetDatabaseから直接ロード
        return UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/PixelFantasy/PixelHeroes/Common/Animation/Character/Controller.controller");
#else
        return Resources.Load<RuntimeAnimatorController>("PixelHeroController");
#endif
    }

    /// <summary>ロール別武器をランダム選択（ティアでレア確率UP）</summary>
    static string PickWeaponForRole(UnitType role, int gachaTier)
    {
        string[] normal;
        string[] rare;

        switch (role)
        {
            case UnitType.Lancer:
                normal = LancerWeapons;
                rare = LancerRareWeapons;
                break;
            case UnitType.Archer:
                normal = ArcherWeapons;
                rare = ArcherRareWeapons;
                break;
            case UnitType.Monk:
                normal = MonkWeapons;
                rare = MonkRareWeapons;
                break;
            case UnitType.Mage:
                normal = MageWeapons;
                rare = MageRareWeapons;
                break;
            default: // Warrior, Knight
                normal = WarriorWeapons;
                rare = WarriorRareWeapons;
                break;
        }

        // ティア0: レア10%, ティア1: 20%, ティア2: 40%, ティア3: 60%, ティア4+: 80%
        int rareChance = 10 + gachaTier * 15;
        rareChance = Mathf.Min(rareChance, 80);

        if (Random.Range(0, 100) < rareChance && rare.Length > 0)
            return rare[Random.Range(0, rare.Length)];
        else
            return normal[Random.Range(0, normal.Length)];
    }

    // ─── 武器スプライト抽出（VFXエフェクト用） ─────────────────

    static Sprite _cachedWeaponSprite;
    static string _cachedWeaponName;

    /// <summary>武器テクスチャからJabフレームを抽出してSprite化（VFX用）</summary>
    public static Sprite ExtractWeaponSprite(string weaponName)
    {
        if (_cachedWeaponSprite != null && _cachedWeaponName == weaponName) return _cachedWeaponSprite;

        var collection = GetSpriteCollection();
        if (collection == null) return null;

        System.Collections.Generic.List<Texture2D> textures = null;
        foreach (var layer in collection.Layers)
        {
            if (layer.Name == "Weapon") { textures = layer.Textures; break; }
        }
        if (textures == null) return null;

        Texture2D weaponTex = null;
        foreach (var t in textures)
        {
            if (t.name == weaponName) { weaponTex = t; break; }
        }
        if (weaponTex == null) return null;

        // Jab_2フレーム（槍を突き出したポーズ）: x=128, y=384, 64x64
        int fx = 128, fy = 384, fw = 64, fh = 64;
        Color[] pixels;
        try { pixels = weaponTex.GetPixels(fx, fy, fw, fh); }
        catch { return null; }

        // 非透明ピクセルのバウンディングボックスを計算してオートクロップ
        int minX = fw, minY = fh, maxX = -1, maxY = -1;
        for (int y = 0; y < fh; y++)
            for (int x = 0; x < fw; x++)
                if (pixels[y * fw + x].a > 0.01f)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }

        if (maxX < 0) return null;

        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        var cropped = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        cropped.filterMode = FilterMode.Point;
        cropped.SetPixels(weaponTex.GetPixels(fx + minX, fy + minY, cw, ch));
        cropped.Apply();

        _cachedWeaponName = weaponName;
        _cachedWeaponSprite = Sprite.Create(cropped, new Rect(0, 0, cw, ch), new Vector2(0.5f, 0.5f), 16);
        return _cachedWeaponSprite;
    }

    /// <summary>Weaponレイヤーの全武器名を取得</summary>
    public static string[] GetAllWeaponNames()
    {
        var collection = GetSpriteCollection();
        if (collection == null) return new string[0];
        foreach (var layer in collection.Layers)
        {
            if (layer.Name == "Weapon")
            {
                var names = new string[layer.Textures.Count];
                for (int i = 0; i < layer.Textures.Count; i++)
                    names[i] = layer.Textures[i].name;
                return names;
            }
        }
        return new string[0];
    }

    /// <summary>レイヤーからランダム選択（emptyChanceで空装備の確率制御）</summary>
    static string RandomizeFromLayer(CharacterBuilder builder, string layerName, int emptyChance)
    {
        var collection = builder.SpriteCollection;
        if (collection == null) return "";

        System.Collections.Generic.List<Texture2D> textures = null;
        foreach (var layer in collection.Layers)
        {
            if (layer.Name == layerName)
            {
                textures = layer.Textures;
                break;
            }
        }

        if (textures == null || textures.Count == 0) return "";
        if (Random.Range(0, 100) < emptyChance) return "";

        return textures[Random.Range(0, textures.Count)].name;
    }
}
