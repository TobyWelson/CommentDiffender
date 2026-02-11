using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject castlePrefab;

    [Header("Pixel Heroes")]
    public RuntimeAnimatorController pixelHeroAnimator;

    [Header("Debug")]
    public bool debugMode = true;

    [Header("Release")]
    public ReleaseMode releaseMode = ReleaseMode.Both;

    [Header("Mode")]
    public GameMode currentMode = GameMode.Offline;

    [Header("Runtime")]
    public GamePhase currentPhase = GamePhase.Title;
    public int currentWaveIndex = 0;
    public int score = 0;
    public int totalKills = 0;
    public float elapsedTime = 0f;

    // Unit tracking
    [System.NonSerialized] public List<Unit> allyUnits = new List<Unit>();
    [System.NonSerialized] public List<Unit> enemyUnits = new List<Unit>();
    private List<QueuedUnit> unitQueue = new List<QueuedUnit>();
    private HashSet<string> ultimateUsedThisWave = new HashSet<string>();

    // Components
    private WaveManager waveManager;
    private UIManager uiManager;
    private DragDropController dragDropController;
    private Castle castle;
    private Castle enemyCastle;

    // Kill log
    public List<string> killLog = new List<string>();

    // Events
    public event System.Action<GamePhase> OnPhaseChanged;
    public event System.Action<QueuedUnit> OnUnitQueued;
    public event System.Action OnQueueChanged;
    public event System.Action OnGoldChanged;

    // ゴールド（つうか）
    [Header("Gold")]
    public int gold = 0;
    private float goldTimer = 0f;

    // Castle damage tracking for score
    private int castleDamageTaken = 0;

    // YouTube Action Effects
    private float likeGlobalBuff = 1f;
    private int currentLikeMilestoneIndex = -1;

    // TikTok Action Effects
    private float tiktokLikeGlobalHPBuff = 1f;
    private float tiktokLikeGlobalATKBuff = 1f;
    private float tiktokLikeGlobalSpeedBuff = 1f;
    private int currentTikTokLikeMilestoneIndex = -1;

    // テスト用チャッター
    private float testChatTimer = 0f;
    private int testChatIndex = 0;
    private static readonly string[] testChatMessages = new[]
    {
        "がんばれー！",
        "草",
        "www",
        "ナイス！",
        "初見です！このゲームどうやって参加するの？",
        "この配信めちゃくちゃ面白いんだけどwww毎回見に来てるよ応援してるからね頑張って！！",
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

#if UNITY_EDITOR
        EditorAutoLoadPrefabs();
#endif

        waveManager = GetComponent<WaveManager>();
        if (waveManager == null) waveManager = gameObject.AddComponent<WaveManager>();

        uiManager = GetComponent<UIManager>();
        if (uiManager == null) uiManager = gameObject.AddComponent<UIManager>();

        dragDropController = GetComponent<DragDropController>();
        if (dragDropController == null) dragDropController = gameObject.AddComponent<DragDropController>();

        if (GetComponent<ViewerStats>() == null) gameObject.AddComponent<ViewerStats>();
        if (GetComponent<YouTubeChatManager>() == null) gameObject.AddComponent<YouTubeChatManager>();
        GetComponent<YouTubeChatManager>().enabled = false; // タイトルでモード選択後に有効化
        if (GetComponent<TikTokChatManager>() == null) gameObject.AddComponent<TikTokChatManager>();
        GetComponent<TikTokChatManager>().enabled = false;
        if (GetComponent<AudioManager>() == null) gameObject.AddComponent<AudioManager>();
        if (GetComponent<CameraView3D>() == null) gameObject.AddComponent<CameraView3D>();
    }

    void Start()
    {
        // Subscribe to core events
        Unit.OnUnitKilled += HandleUnitKilled;
        waveManager.OnAllEnemiesDefeated += HandleAllEnemiesDefeated;

        // Find or create castle
        castle = FindObjectOfType<Castle>();
        if (castle == null)
        {
            var castleGo = new GameObject("Castle");
            castleGo.transform.position = new Vector3(GameConfig.CastleX, GameConfig.CastleY, 0);
            castle = castleGo.AddComponent<Castle>();
            castle.OnCastleDestroyed += HandleCastleDestroyed;

            var sr = castleGo.AddComponent<SpriteRenderer>();
            sr.sprite = LoadCastleSprite();
            sr.sortingOrder = 5;

            if (sr.sprite == null)
            {
                sr.sprite = Unit.CreatePixelSprite();
                sr.color = new Color(0.3f, 0.3f, 0.8f, 1f);
                castleGo.transform.localScale = new Vector3(1.5f, 3f, 1f);
            }
        }
        else
        {
            castle.transform.position = new Vector3(GameConfig.CastleX, GameConfig.CastleY, 0);
            castle.OnCastleDestroyed += HandleCastleDestroyed;
        }

        // Enemy castle (敵陣)
        {
            var ecGo = new GameObject("EnemyCastle");
            ecGo.transform.position = new Vector3(GameConfig.EnemyCastleX, GameConfig.EnemyCastleY, 0);
            enemyCastle = ecGo.AddComponent<Castle>();
            enemyCastle.team = Team.Enemy;
            enemyCastle.maxHP = GameConfig.GetEnemyCastleHP(0);
            enemyCastle.currentHP = enemyCastle.maxHP;
            enemyCastle.OnCastleDestroyed += HandleEnemyCastleDestroyed;

            var ecSr = ecGo.AddComponent<SpriteRenderer>();
            ecSr.sprite = LoadEnemyCastleSprite();
            ecSr.sortingOrder = 5;
            ecSr.flipX = true;

            if (ecSr.sprite == null)
            {
                ecSr.sprite = Unit.CreatePixelSprite();
                ecSr.color = new Color(0.8f, 0.2f, 0.2f, 1f);
                ecGo.transform.localScale = new Vector3(1.5f, 3f, 1f);
            }
        }

        // タイトル画面から開始
        SetPhase(GamePhase.Title);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM("BgmTitle");
    }

    // ─── モード選択 ──────────────────────────────────────

    public void StartOnlineMode()
    {
        currentMode = GameMode.YouTube;
        if (ViewerStats.Instance != null) ViewerStats.Instance.LoadForMode(GameMode.YouTube);
        var ytChat = GetComponent<YouTubeChatManager>();
        if (ytChat != null)
        {
            ytChat.enabled = true;
            ytChat.OnSuperChat += HandleSuperChat;
            ytChat.OnNewMember += HandleNewMember;
            ytChat.OnMemberDetected += HandleMemberDetected;
            ytChat.OnLikeMilestone += HandleLikeMilestone;
            ytChat.OnAuthenticated += OnYouTubeAuthenticated;
            ytChat.OnStreamConnected += OnYouTubeStreamConnected;
        }
        // タイトルを非表示にしてYouTube接続画面を表示（BeginGameは接続後に呼ぶ）
        var ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.ShowYouTubeSetup();
    }

    void OnYouTubeAuthenticated()
    {
        var ytChat = GetComponent<YouTubeChatManager>();
        if (ytChat != null) ytChat.OnAuthenticated -= OnYouTubeAuthenticated;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("QueueAdd");
        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement("<color=#00FF88>にんしょうせいこう!</color>\n<size=32>つぎに Video ID をにゅうりょくしてください</size>", 3f, 48);
    }

    void OnYouTubeStreamConnected()
    {
        var ytChat = GetComponent<YouTubeChatManager>();
        if (ytChat != null) ytChat.OnStreamConnected -= OnYouTubeStreamConnected;
        StartCoroutine(DelayedBeginGame("YouTube"));
    }

    public void StartOfflineMode()
    {
        currentMode = GameMode.Offline;
        if (ViewerStats.Instance != null) ViewerStats.Instance.LoadForMode(GameMode.Offline);
        BeginGame();
    }

    /// <summary>セットアップ画面からオフラインモードへ切り替え</summary>
    public void SwitchToOfflineFromSetup()
    {
        var ytChat = GetComponent<YouTubeChatManager>();
        if (ytChat != null)
        {
            ytChat.OnStreamConnected -= OnYouTubeStreamConnected;
            ytChat.OnAuthenticated -= OnYouTubeAuthenticated;
            ytChat.OnSuperChat -= HandleSuperChat;
            ytChat.OnNewMember -= HandleNewMember;
            ytChat.OnMemberDetected -= HandleMemberDetected;
            ytChat.OnLikeMilestone -= HandleLikeMilestone;
            ytChat.enabled = false;
        }
        var ttChat = GetComponent<TikTokChatManager>();
        if (ttChat != null)
        {
            ttChat.OnStreamConnected -= OnTikTokStreamConnected;
            ttChat.OnGift -= HandleTikTokGift;
            ttChat.OnNewTeamMember -= HandleNewTeamMember;
            ttChat.OnTeamMemberDetected -= HandleTeamMemberDetected;
            ttChat.OnNewSubscriber -= HandleNewSubscriber;
            ttChat.OnSubscriberDetected -= HandleSubscriberDetected;
            ttChat.OnLikeMilestone -= HandleTikTokLikeMilestone;
            ttChat.OnFollow -= HandleFollow;
            ttChat.OnShare -= HandleShare;
            ttChat.Disconnect();
            ttChat.enabled = false;
        }
        currentMode = GameMode.Offline;
        if (ViewerStats.Instance != null) ViewerStats.Instance.LoadForMode(GameMode.Offline);
        BeginGame();
    }

    public void StartTikTokMode()
    {
        currentMode = GameMode.TikTok;
        if (ViewerStats.Instance != null) ViewerStats.Instance.LoadForMode(GameMode.TikTok);
        var ttChat = GetComponent<TikTokChatManager>();
        if (ttChat != null)
        {
            ttChat.enabled = true;
            ttChat.OnGift += HandleTikTokGift;
            ttChat.OnNewTeamMember += HandleNewTeamMember;
            ttChat.OnTeamMemberDetected += HandleTeamMemberDetected;
            ttChat.OnNewSubscriber += HandleNewSubscriber;
            ttChat.OnSubscriberDetected += HandleSubscriberDetected;
            ttChat.OnLikeMilestone += HandleTikTokLikeMilestone;
            ttChat.OnStreamConnected += OnTikTokStreamConnected;
            ttChat.OnFollow += HandleFollow;
            ttChat.OnShare += HandleShare;
        }
        var ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.ShowTikTokSetup();
    }

    void OnTikTokStreamConnected()
    {
        var ttChat = GetComponent<TikTokChatManager>();
        if (ttChat != null) ttChat.OnStreamConnected -= OnTikTokStreamConnected;
        StartCoroutine(DelayedBeginGame("TikTok"));
    }

    System.Collections.IEnumerator DelayedBeginGame(string platform)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("WaveClear");
        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement($"<color=#00FF88>{platform} せつぞくせいこう!</color>\n<size=32>ゲームをかいしします...</size>", 2.5f, 48);
        yield return new WaitForSeconds(2f);
        BeginGame();
    }

    void BeginGame()
    {
        gold = GameConfig.StartingGold;
        goldTimer = 0f;
        OnGoldChanged?.Invoke();

        // セーブデータからWave進行を復元
        if (ViewerStats.Instance != null && ViewerStats.Instance.loadedWaveIndex > 0)
        {
            currentWaveIndex = ViewerStats.Instance.loadedWaveIndex;
            totalKills = ViewerStats.Instance.loadedTotalKills;
            Debug.Log($"[StreamerKing] Restored wave={currentWaveIndex}, kills={totalKills} from save");
        }

        SetPhase(GamePhase.Preparation);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM("BgmPreparation");

#if UNITY_EDITOR
        // デバッグ用スターターユニット（ビルドでは不要）
        AddUnitToQueue(UnitType.Warrior, "Player1");
        AddUnitToQueue(UnitType.Lancer, "Player2");
        AddUnitToQueue(UnitType.Archer, "Player3");
        AddUnitToQueue(UnitType.Monk, "Player4");
#endif
    }

    /// <summary>デバッグ用: 認証/接続をスキップしてゲーム開始</summary>
    public void DebugStartGame()
    {
        // 接続コールバックを解除（二重BeginGame防止）
        var ytChat = GetComponent<YouTubeChatManager>();
        if (ytChat != null) ytChat.OnStreamConnected -= OnYouTubeStreamConnected;
        var ttChat = GetComponent<TikTokChatManager>();
        if (ttChat != null) ttChat.OnStreamConnected -= OnTikTokStreamConnected;
        BeginGame();
    }

    void OnDestroy()
    {
        Unit.OnUnitKilled -= HandleUnitKilled;
        if (waveManager != null)
            waveManager.OnAllEnemiesDefeated -= HandleAllEnemiesDefeated;
        if (enemyCastle != null)
            enemyCastle.OnCastleDestroyed -= HandleEnemyCastleDestroyed;

        var ytChat = GetComponent<YouTubeChatManager>();
        if (ytChat != null)
        {
            ytChat.OnSuperChat -= HandleSuperChat;
            ytChat.OnNewMember -= HandleNewMember;
            ytChat.OnMemberDetected -= HandleMemberDetected;
            ytChat.OnLikeMilestone -= HandleLikeMilestone;
            ytChat.OnAuthenticated -= OnYouTubeAuthenticated;
        }

        var ttChat = GetComponent<TikTokChatManager>();
        if (ttChat != null)
        {
            ttChat.OnGift -= HandleTikTokGift;
            ttChat.OnNewTeamMember -= HandleNewTeamMember;
            ttChat.OnTeamMemberDetected -= HandleTeamMemberDetected;
            ttChat.OnNewSubscriber -= HandleNewSubscriber;
            ttChat.OnSubscriberDetected -= HandleSubscriberDetected;
            ttChat.OnLikeMilestone -= HandleTikTokLikeMilestone;
            ttChat.OnFollow -= HandleFollow;
            ttChat.OnShare -= HandleShare;
        }
    }

    // ─── ゴールド（つうか） ──────────────────────────────────────

    public void AddGold(int amount)
    {
        gold += amount;
        OnGoldChanged?.Invoke();
    }

    public bool SpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        OnGoldChanged?.Invoke();
        return true;
    }

    public bool BuyUnit(UnitType type)
    {
        int cost = GameConfig.GetUnitCost(type);
        if (gold < cost) return false;

        // ショップNPC上限チェック（ownerName==""のキュー + 配置済み合計）
        int npcCount = 0;
        foreach (var q in unitQueue)
            if (string.IsNullOrEmpty(q.ownerName)) npcCount++;
        foreach (var u in allyUnits)
            if (u != null && !u.isDead && string.IsNullOrEmpty(u.ownerName)) npcCount++;
        if (npcCount >= GameConfig.MaxAllyUnits)
        {
            Debug.Log($"[StreamerKing] ショップNPC上限{GameConfig.MaxAllyUnits}体のため購入を拒否");
            return false;
        }

        SpendGold(cost);
        AddUnitToQueue(type, "");
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("CoinGet");
        return true;
    }

    void Update()
    {
        if (currentPhase == GamePhase.Battle)
        {
            elapsedTime += Time.deltaTime;
            CheckEnemiesReachCastle();
            CheckAlliesReachEnemyCastle();
        }

        // ゴールドじかんけいか（バトルちゅうのみ）
        if (currentPhase == GamePhase.Battle)
        {
            goldTimer += Time.deltaTime;
            if (goldTimer >= 1f)
            {
                goldTimer -= 1f;
                AddGold(GameConfig.GoldPerSecond);
            }
        }

        // Clean up destroyed units from lists
        allyUnits.RemoveAll(u => u == null);
        enemyUnits.RemoveAll(u => u == null);

        // テスト用: 生存中の味方1体が定期的にしゃべる（debugMode時のみ）
        if (debugMode)
        {
            testChatTimer -= Time.deltaTime;
            if (testChatTimer <= 0f)
            {
                testChatTimer = 3f;
                string talker = null;
                foreach (var u in allyUnits)
                    if (u != null && !u.isDead) { talker = u.ownerName; break; }
                if (talker != null)
                {
                    ShowViewerSpeech(talker, testChatMessages[testChatIndex % testChatMessages.Length]);
                    testChatIndex++;
                }
            }
        }
    }

    // ─── Phase Management ────────────────────────────────────────

    public void SetPhase(GamePhase phase)
    {
        currentPhase = phase;
        OnPhaseChanged?.Invoke(phase);

        // 3Dカメラプレビュー制御
        var cam3d = GetComponent<CameraView3D>();
        if (cam3d != null)
        {
            if (phase == GamePhase.Battle) cam3d.EnablePreview();
            else cam3d.DisablePreview();
        }
    }

    public void StartWave()
    {
        Debug.Log($"[StreamerKing] StartWave called. Phase={currentPhase}, WaveIndex={currentWaveIndex}");
        if (currentPhase != GamePhase.Preparation) { Debug.LogWarning("[StreamerKing] Not in Preparation phase!"); return; }
        // 無限Waveなのでウェーブ上限チェック不要

        // 味方が1体も配置されていなければ開始不可
        int aliveAllies = 0;
        foreach (var u in allyUnits)
            if (u != null && !u.isDead) aliveAllies++;
        if (aliveAllies == 0)
        {
            var ui = FindObjectOfType<UIManager>();
            if (ui != null) ui.ShowAnnouncement("<color=#FF6644>へいしをはいちしてください!</color>", 2f, 40);
            return;
        }

        SetPhase(GamePhase.Battle);
        ultimateUsedThisWave.Clear();

        // 全味方の必殺技バフを強制解除
        foreach (var u in allyUnits)
            if (u != null && !u.isDead) u.ClearUltimateBuffs();

        // 敵城をリセット（Wave毎にHP増加）
        if (enemyCastle != null)
        {
            enemyCastle.maxHP = GameConfig.GetEnemyCastleHP(currentWaveIndex);
            enemyCastle.ResetCastle();
        }

        waveManager.StartWave(currentWaveIndex);
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySE("WaveStart");
            AudioManager.Instance.PlayBGM("BgmBattle");
        }
        Debug.Log($"[StreamerKing] Wave {currentWaveIndex + 1} started. Queue size={unitQueue.Count}");

        Debug.Log($"[StreamerKing] Allies deployed: {allyUnits.Count}, Queue remaining: {unitQueue.Count}");
    }

    void HandleAllEnemiesDefeated()
    {
        // 敵城が生きている場合 → 雑魚のみ再スポーンして継続（ボスは出さない）
        if (enemyCastle != null && !enemyCastle.isDestroyed)
        {
            waveManager.StartWave(currentWaveIndex, spawnBoss: false);
            return;
        }
        // 敵城なし or 敵城破壊済み → 従来通りWaveクリア
        HandleWaveCleared();
    }

    void HandleWaveCleared()
    {
        // 城が壊れていたらWaveクリアではなくゲームオーバー
        if (castle != null && castle.isDestroyed) return;
        if (currentPhase == GamePhase.Result) return;

        currentWaveIndex++;

        // 生存ユニットにWaveクリア経験値ボーナス
        GrantWaveClearXP();

        // 無限Waveなので勝利判定なし（城が壊されるまで続く）
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySE("WaveClear");
            AudioManager.Instance.PlayBGM("BgmPreparation");
        }
        SetPhase(GamePhase.Preparation);
    }

    void GrantWaveClearXP()
    {
        // Wave番号に応じたボーナスXP（Wave1=20, Wave2=25, Wave3=30, ...）
        int bonus = 20 + currentWaveIndex * 5;
        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead) continue;
            u.AddXP(bonus);
        }
    }

    /// <summary>全生存ユニットのレベルをViewerDataに同期</summary>
    public void SyncUnitLevelsToViewerData()
    {
        if (ViewerStats.Instance == null) return;
        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead || string.IsNullOrEmpty(u.viewerId)) continue;
            ViewerStats.Instance.UpdateBestLevel(u.viewerId, u.level, u.xp);
        }
    }

    /// <summary>セーブ実行（UIから呼ばれる）</summary>
    public void SaveViewerData()
    {
        SyncUnitLevelsToViewerData();
        if (ViewerStats.Instance != null)
        {
            ViewerStats.Instance.Save();
            var ui = FindObjectOfType<UIManager>();
            if (ui != null)
                ui.ShowAnnouncement("<color=#00FF88>セーブしました!</color>", 2f, 48);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("CoinGet");
        }
    }

    /// <summary>ゲームしゅうりょう: 城HPを0にしてゲームオーバー</summary>
    public void ForceGameOver()
    {
        if (currentPhase == GamePhase.Title || currentPhase == GamePhase.Result) return;
        if (castle != null && !castle.isDestroyed)
            castle.TakeDamage(castle.currentHP);
    }

    void HandleCastleDestroyed()
    {
        // ゲームオーバー → セーブデータ削除
        if (ViewerStats.Instance != null)
            ViewerStats.Instance.DeleteSave();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySE("GameOver");
            AudioManager.Instance.PlayBGM("BgmResult");
        }
        SetPhase(GamePhase.Result);
    }

    void CheckEnemiesReachCastle()
    {
        for (int i = enemyUnits.Count - 1; i >= 0; i--)
        {
            var enemy = enemyUnits[i];
            if (enemy == null || enemy.isDead) continue;

            if (enemy.HasReachedCastle())
            {
                // 攻撃速度に基づいて繰り返し城を攻撃
                if (enemy.CanAttackCastle())
                {
                    castle.TakeDamage(enemy.attackPower);
                    castleDamageTaken += enemy.attackPower;
                    enemy.ResetCastleAttackTimer();
                }
            }
        }
    }

    void CheckAlliesReachEnemyCastle()
    {
        if (enemyCastle == null || enemyCastle.isDestroyed) return;
        for (int i = allyUnits.Count - 1; i >= 0; i--)
        {
            var ally = allyUnits[i];
            if (ally == null || ally.isDead) continue;
            if (ally.unitType == UnitType.Monk) continue;
            if (ally.HasReachedEnemyCastle() && ally.CanAttackEnemyCastle())
            {
                enemyCastle.TakeDamage(ally.attackPower);
                ally.ResetEnemyCastleAttackTimer();
            }
        }
    }

    void HandleEnemyCastleDestroyed()
    {
        if (currentPhase != GamePhase.Battle) return;
        // スポーン停止
        waveManager.ForceStopSpawning();
        // 残存敵を全滅
        foreach (var e in enemyUnits)
            if (e != null && !e.isDead) Destroy(e.gameObject);
        enemyUnits.Clear();
        waveManager.enemiesAlive = 0;
        // 即Waveクリア
        HandleWaveCleared();
    }

    // ─── Unit Management ─────────────────────────────────────────

    public List<Unit> GetUnits(Team team)
    {
        return team == Team.Ally ? allyUnits : enemyUnits;
    }

    public Unit SpawnUnit(UnitType type, Team team, string ownerName, Vector3 position, float statScale = 1f, HeroAppearance appearance = null, string viewerId = null, bool isBoss = false)
    {
        GameObject go = null;
        bool isBossMonster = false;

        if (team == Team.Ally && appearance != null)
        {
            go = PixelHeroFactory.CreateAllyHeroFromAppearance(type, appearance);
        }
        else if (team == Team.Ally)
        {
            go = PixelHeroFactory.CreateAllyHero(type);
        }
        else
        {
            string race = PixelHeroFactory.GetRaceForWave(currentWaveIndex);

            if (isBoss)
            {
                go = PixelHeroFactory.CreateBossMonster(currentWaveIndex);
                if (go != null)
                {
                    isBossMonster = true;
                    // BossPack1ボスのスケール設定（通常0.32 → ボスサイズに拡大）
                    float bossScale = 0.5f + currentWaveIndex * 0.02f;
                    go.transform.localScale = new Vector3(bossScale, bossScale, 1f);
                }
            }

            if (go == null)
                go = PixelHeroFactory.CreateEnemyHero(race, currentWaveIndex + 1, isBoss);
        }

        go.transform.position = position;
        go.name = $"{team}_{type}_{ownerName}";

        var unit = go.GetComponent<Unit>();
        if (unit == null) unit = go.AddComponent<Unit>();
        unit.Initialize(type, team, ownerName, statScale);
        unit.isBossMonster = isBossMonster;
        if (isBossMonster)
        {
            // BossPack1: Grounded=trueにしないと落下アニメーションのまま
            // applyRootMotion=falseで物理移動をUnit.csに委任（Root Motionだと真下に落ちる）
            var bossAnim = go.GetComponent<Animator>();
            if (bossAnim != null)
            {
                bossAnim.SetBool("Grounded", true);
                bossAnim.applyRootMotion = false;
            }
        }
        if (isBoss)
        {
            unit.isBoss = true;
            unit.CreateBossAura();
        }
        if (!string.IsNullOrEmpty(viewerId))
            unit.viewerId = viewerId;

        // Add collider if missing
        if (go.GetComponent<Collider2D>() == null)
        {
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = GameConfig.UnitRadius;
        }

        if (team == Team.Ally)
        {
            allyUnits.Add(unit);

            // いいねグローバルバフ（全味方共通）
            if (likeGlobalBuff > 1f) unit.ApplyStatMultiplier(likeGlobalBuff);

            // メンバー/スパチャ/TikTokバフ＋視覚エフェクト
            string vid = !string.IsNullOrEmpty(viewerId) ? viewerId : ownerName;
            if (ViewerStats.Instance != null)
            {
                var vd = ViewerStats.Instance.GetStats(vid);
                if (vd != null)
                {
                    // YouTube buffs
                    if (vd.isMember) unit.ApplyMemberBuff();
                    if (vd.superChatTier >= 0) unit.UpgradeSuperChatTier(vd.superChatTier);

                    // TikTok buffs
                    if (vd.teamLevel > 0)
                    {
                        int tierIdx = ViewerStats.GetTeamLevelTier(vd.teamLevel);
                        unit.ApplyTeamBuff(tierIdx);
                    }
                    if (vd.isSubscriber) unit.ApplySubscriptionBuff();
                    if (vd.tiktokGiftTier >= 0)
                    {
                        unit.UpgradeTikTokGiftTier(vd.tiktokGiftTier);
                        // ティア3+: オーラ効果
                        if (vd.tiktokGiftTier >= 3)
                        {
                            float auraHP = vd.tiktokGiftTier >= 4 ? 1.20f : 1.10f;
                            float auraATK = vd.tiktokGiftTier >= 4 ? 1.15f : 1.10f;
                            unit.SetAura(auraHP, auraATK, GameConfig.KnightAuraRange);
                        }
                        // ティア4+: 自動回復
                        if (vd.tiktokGiftTier >= 4)
                        {
                            unit.enableAutoHeal = true;
                            unit.autoHealRate = unit.maxHP * 0.02f;
                        }
                        // ティア5: 虹色
                        if (vd.tiktokGiftTier >= 5) unit.SetRainbowEffect();
                    }

                    // セーブデータからレベル復元
                    if (vd.bestUnitLevel > 1)
                        unit.RestoreLevel(vd.bestUnitLevel, vd.bestUnitXP);
                }
            }

            // TikTokいいねグローバルバフ
            if (tiktokLikeGlobalHPBuff > 1f)
            {
                unit.maxHP = Mathf.RoundToInt(unit.maxHP * tiktokLikeGlobalHPBuff);
            }
            if (tiktokLikeGlobalATKBuff > 1f)
                unit.attackPower = Mathf.RoundToInt(unit.attackPower * tiktokLikeGlobalATKBuff);
            if (tiktokLikeGlobalSpeedBuff > 1f)
                unit.moveSpeed *= tiktokLikeGlobalSpeedBuff;

            // 新規スポーン時は全バフ適用後にHP満タンにする
            unit.currentHP = unit.maxHP;

            // プロフィール画像をキャラの顔面に貼り付け
            if (UIManager.Instance != null)
            {
                var tex = UIManager.Instance.GetProfileImage(ownerName);
                if (tex != null)
                    unit.SetFaceIcon(tex);
                else if (ViewerStats.Instance != null)
                {
                    // まだDL未完了ならリクエスト（完了時にコールバックで適用される）
                    var vd2 = ViewerStats.Instance.GetStats(vid);
                    if (vd2 != null && !string.IsNullOrEmpty(vd2.profileImageUrl))
                        UIManager.Instance.RequestProfileImage(ownerName, vd2.profileImageUrl);
                }
            }
        }
        else
        {
            enemyUnits.Add(unit);
        }

        return unit;
    }

    // ─── Queue Management ────────────────────────────────────────

    public List<QueuedUnit> GetQueue() => unitQueue;

    /// <summary>
    /// チャットコメントからユニットを判定してキューに追加。
    /// けん/剣=Warrior, やり/槍=Lancer, ゆみ/弓=Archer, かいふく/回復=Monk, まほう/魔法/メイジ=Mage, きし/騎士/ナイト=Knight
    /// </summary>
    public bool TryAddUnitFromChat(string comment, string ownerName, string viewerId = null)
    {
        // Title/Result中はコマンドを無視（蓄積防止）
        if (currentPhase == GamePhase.Title || currentPhase == GamePhase.Result) return false;

        if (string.IsNullOrEmpty(viewerId)) viewerId = ownerName;

        UnitType? type = null;
        if (comment.Contains("剣") || comment.Contains("けん")) type = UnitType.Warrior;
        else if (comment.Contains("槍") || comment.Contains("やり")) type = UnitType.Lancer;
        else if (comment.Contains("弓") || comment.Contains("ゆみ")) type = UnitType.Archer;
        else if (comment.Contains("回復") || comment.Contains("かいふく")) type = UnitType.Monk;
        else if (comment.Contains("魔法") || comment.Contains("まほう") || comment.Contains("メイジ")) type = UnitType.Mage;
        else if (comment.Contains("騎士") || comment.Contains("きし") || comment.Contains("ナイト")) type = UnitType.Knight;

        if (type == null) return false;

        // Knight requires TikTok subscription
        if (type == UnitType.Knight)
        {
            if (ViewerStats.Instance == null || !ViewerStats.Instance.IsSubscriber(viewerId))
            {
                Debug.Log($"[StreamerKing] {ownerName} はサブスクしていないためKnightを召喚できません");
                return false;
            }
        }

        // NPC全体の上限チェック（キュー + 配置済み合計20体未満）
        int totalAlly = unitQueue.Count;
        foreach (var u in allyUnits)
            if (u != null && !u.isDead) totalAlly++;
        if (totalAlly >= GameConfig.MaxAllyUnits)
        {
            Debug.Log($"[StreamerKing] 味方全体が上限{GameConfig.MaxAllyUnits}体に達しているため {ownerName} の召喚を拒否");
            return false;
        }

        // 1リスナーあたりの上限チェック（キュー内 + 配置済み）
        int count = 0;
        foreach (var q in unitQueue)
            if (q.ownerName == ownerName) count++;
        foreach (var u in allyUnits)
            if (u != null && !u.isDead && u.ownerName == ownerName) count++;
        if (count >= GameConfig.MaxUnitsPerViewer)
        {
            Debug.Log($"[StreamerKing] {ownerName} は上限{GameConfig.MaxUnitsPerViewer}体に達しています");
            return false;
        }

        AddUnitToQueue(type.Value, ownerName, viewerId);
        Debug.Log($"[StreamerKing] {ownerName} が {type.Value} を召喚！（コメント: {comment}）");
        return true;
    }

    /// <summary>
    /// チャットコメントからスタンス変更を判定。
    /// せめろ/攻めろ=Attack, さがれ/下がれ=Defend
    /// </summary>
    public bool TryCommandFromChat(string comment, string ownerName, string viewerId = null)
    {
        // Title/Result中はコマンドを無視（蓄積防止）
        if (currentPhase == GamePhase.Title || currentPhase == GamePhase.Result) return false;

        if (string.IsNullOrEmpty(viewerId)) viewerId = ownerName;

        // ─── 必殺技コマンド ─────────────────────────────
        if (comment.Contains("必殺技") || comment.Contains("ひっさつ"))
        {
            return TryUltimate(ownerName, viewerId);
        }

        // ─── スタンスコマンド ────────────────────────────
        UnitStance? newStance = null;
        if (comment.Contains("攻めろ") || comment.Contains("せめろ") || comment.Contains("攻め") || comment.Contains("せめ") || comment.Contains("進め") || comment.Contains("すすめ"))
            newStance = UnitStance.Attack;
        else if (comment.Contains("下がれ") || comment.Contains("さがれ") || comment.Contains("守れ") || comment.Contains("まもれ") || comment.Contains("退け") || comment.Contains("ひけ"))
            newStance = UnitStance.Defend;

        if (newStance == null) return false;

        int changed = 0;
        foreach (var unit in allyUnits)
        {
            if (unit == null || unit.isDead) continue;
            if (unit.ownerName == ownerName)
            {
                unit.stance = newStance.Value;
                if (newStance.Value == UnitStance.Defend) unit.defendTimer = 1.5f;
                changed++;
            }
        }

        if (changed > 0)
        {
            string stanceName = newStance == UnitStance.Attack ? "攻撃" : "防御";
            Debug.Log($"[StreamerKing] {ownerName} のユニット{changed}体が{stanceName}モードに変更");
        }

        return changed > 0;
    }

    /// <summary>NPC（ownerNameが空のユニット）の陣形を変更する</summary>
    public void SetNPCFormation(UnitStance formation)
    {
        int changed = 0;
        foreach (var unit in allyUnits)
        {
            if (unit == null || unit.isDead) continue;
            if (string.IsNullOrEmpty(unit.ownerName))
            {
                unit.stance = formation;
                if (formation == UnitStance.Defend) unit.defendTimer = 1.5f;
                changed++;
            }
        }
        string formName = formation == UnitStance.Attack ? "ぜんえい" : "こうえい";
        Debug.Log($"[StreamerKing] NPC{changed}体を{formName}に変更");
    }

    bool TryUltimate(string ownerName, string viewerId)
    {
        if (currentPhase != GamePhase.Battle) return false;

        // Wave毎に1回まで
        if (ultimateUsedThisWave.Contains(viewerId))
        {
            Debug.Log($"[StreamerKing] {ownerName}: 必殺技失敗 - このWaveで既に使用済み");
            return true;
        }

        // このリスナーの生存ユニットから最高レベルの1体を選ぶ
        Unit bestUnit = null;
        foreach (var unit in allyUnits)
        {
            if (unit == null || unit.isDead || unit.ownerName != ownerName) continue;
            if (bestUnit == null || unit.level > bestUnit.level)
                bestUnit = unit;
        }

        if (bestUnit == null)
        {
            Debug.Log($"[StreamerKing] {ownerName}: 必殺技失敗 - 生存ユニットなし");
            return false;
        }

        // ポイント確認＆消費
        var vs = ViewerStats.Instance;
        if (vs == null) return false;

        int cost = GameConfig.UltimateCost;
        int currentScore = vs.GetScore(viewerId);
        if (currentScore < cost)
        {
            Debug.Log($"[StreamerKing] {ownerName}: 必殺技失敗 - ポイント不足 ({currentScore}/{cost})");
            DamagePopup.CreateText(bestUnit.transform.position + Vector3.up * 0.5f,
                $"PT不足({currentScore}/{cost})", Color.gray);
            return true; // コマンドは認識した（クールダウン対象外にするため true）
        }

        vs.TrySpendScore(viewerId, cost);
        bestUnit.PerformUltimate();
        ultimateUsedThisWave.Add(viewerId);
        Debug.Log($"[StreamerKing] {ownerName} の {bestUnit.unitType} が必殺技発動！(残{currentScore - cost}pt)");
        return true;
    }

    public void AddUnitToQueue(UnitType type, string ownerName, string viewerId = null)
    {
        if (string.IsNullOrEmpty(viewerId)) viewerId = ownerName;
        var qUnit = new QueuedUnit { type = type, ownerName = ownerName, viewerId = viewerId };

        // プレビュースプライト生成（SC/ギフトティアでレア装備率UP）
        int gachaTier = 0;
        if (ViewerStats.Instance != null)
        {
            var vd = ViewerStats.Instance.GetStats(viewerId);
            if (vd != null)
            {
                if (vd.superChatTier >= 0) gachaTier = vd.superChatTier;
                else if (vd.tiktokGiftTier >= 0) gachaTier = vd.tiktokGiftTier;
            }
        }
        HeroAppearance appearance;
        qUnit.previewSprite = PixelHeroFactory.CreatePreviewSprite(type, gachaTier, out appearance);
        qUnit.appearance = appearance;

        unitQueue.Add(qUnit);
        OnUnitQueued?.Invoke(qUnit);
        OnQueueChanged?.Invoke();

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("QueueAdd");

        // 視聴者統計に記録（ゴールド購入ユニットは除外）
        if (ViewerStats.Instance != null && !string.IsNullOrEmpty(ownerName))
            ViewerStats.Instance.RecordSummon(viewerId, ownerName, type);
    }

    /// <summary>ドラッグキャンセル時、元の見た目のままキューに戻す</summary>
    public void ReturnUnitToQueue(UnitType type, string ownerName, string viewerId, HeroAppearance appearance, Sprite previewSprite)
    {
        if (string.IsNullOrEmpty(viewerId)) viewerId = ownerName;
        var qUnit = new QueuedUnit {
            type = type,
            ownerName = ownerName,
            viewerId = viewerId,
            appearance = appearance,
            previewSprite = previewSprite
        };
        unitQueue.Add(qUnit);
        OnUnitQueued?.Invoke(qUnit);
        OnQueueChanged?.Invoke();
    }

    public QueuedUnit DequeueUnit(int index)
    {
        if (index < 0 || index >= unitQueue.Count) return null;
        var unit = unitQueue[index];
        unitQueue.RemoveAt(index);
        OnQueueChanged?.Invoke();
        return unit;
    }

    public void PlaceAllQueueRandom()
    {
        while (unitQueue.Count > 0)
        {
            var q = unitQueue[0];
            float x = Random.Range(GameConfig.PlacementZoneMinX, GameConfig.PlacementZoneMaxX);
            float y = Random.Range(GameConfig.PlacementZoneMinY, GameConfig.PlacementZoneMaxY);
            var pos = new Vector3(x, y, 0);
            SpawnUnit(q.type, Team.Ally, q.ownerName, pos, 1f, q.appearance, q.viewerId);
            unitQueue.RemoveAt(0);
        }
        OnQueueChanged?.Invoke();
    }



    Vector3 FindWalkableSpawnPos(float minX, float maxX, float minY, float maxY)
    {
        // Try random positions in the preferred range first
        for (int i = 0; i < 30; i++)
        {
            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            var pos = new Vector3(x, y, 0);
            if (Unit.IsWalkable(pos)) return pos;
        }

        // Fallback: scan the Ground tilemap for any walkable tile in the left half
        var groundGo = GameObject.Find("Map/TileGrid/Ground");
        if (groundGo != null)
        {
            var tilemap = groundGo.GetComponent<Tilemap>();
            if (tilemap != null)
            {
                tilemap.CompressBounds();
                var bounds = tilemap.cellBounds;
                var candidates = new List<Vector3>();
                foreach (var cellPos in bounds.allPositionsWithin)
                {
                    if (!tilemap.HasTile(cellPos)) continue;
                    Vector3 world = tilemap.CellToWorld(cellPos) + new Vector3(0.5f, 0.5f, 0);
                    // Prefer left half of map (ally side)
                    if (world.x <= 0f)
                        candidates.Add(world);
                }
                if (candidates.Count > 0)
                    return candidates[Random.Range(0, candidates.Count)];
            }
        }

        return new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0);
    }

    // ─── Speech Bubble ──────────────────────────────────────────

    public void ShowViewerSpeech(string ownerName, string message)
    {
        // 同じオーナーのユニットのうち1体だけに吹き出しを表示
        Unit target = null;
        foreach (var unit in allyUnits)
        {
            if (unit == null || unit.isDead) continue;
            if (unit.ownerName == ownerName)
            {
                target = unit;
                break;
            }
        }
        if (target != null)
            target.ShowSpeechBubble(message);
    }

    // ─── YouTube Action Effects ──────────────────────────────────

    void HandleSuperChat(string viewerId, string viewerName, int tier, int jpyAmount, string displayAmount)
    {
        // 1. ViewerDataに記録
        if (ViewerStats.Instance != null)
            ViewerStats.Instance.SetSuperChatTier(viewerId, viewerName, tier, jpyAmount);

        // 2. 既存ユニットをアップグレード
        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead || u.ownerName != viewerName) continue;
            u.UpgradeSuperChatTier(tier);
        }

        // 3. スパチャ = 必ず1体追加 + ティアに応じた追加ユニット
        AddUnitToQueue(UnitType.Warrior, viewerName, viewerId);
        int extra = GameConfig.SuperChatExtraUnits[tier];
        for (int i = 0; i < extra; i++)
        {
            UnitType type = (UnitType)Random.Range(0, 4);
            AddUnitToQueue(type, viewerName, viewerId);
        }

        // 4. エフェクト
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SuperChat");
        CameraShake.Shake(0.2f + tier * 0.1f, 0.05f + tier * 0.03f);

        var ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.ShowSuperChatPopup(viewerName, tier, displayAmount);

        Debug.Log($"[StreamerKing] スーパーチャット! {viewerName} {displayAmount} (Tier {tier})");
    }

    void HandleNewMember(string viewerId, string viewerName)
    {
        HandleMemberDetected(viewerId, viewerName);

        // 新規メンバーは追加ボーナス：即座にユニット1体追加
        UnitType type = (UnitType)Random.Range(0, 4);
        AddUnitToQueue(type, viewerName, viewerId);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("NewMember");
        CameraShake.Shake(0.15f, 0.04f);

        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement(
                $"<color=#FFD700>\u2605 {viewerName} がメンバーに!</color>\n" +
                "<size=28>ステータス+15% / クールダウンはんげん</size>", 4f, 48);

        Debug.Log($"[StreamerKing] 新規メンバー! {viewerName}");
    }

    void HandleMemberDetected(string viewerId, string viewerName)
    {
        // ViewerDataにメンバー登録
        if (ViewerStats.Instance != null)
            ViewerStats.Instance.SetMember(viewerId, viewerName);

        // 既存ユニットにメンバーバフ適用
        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead || u.ownerName != viewerName) continue;
            u.ApplyMemberBuff();
        }
    }

    void HandleLikeMilestone(int milestoneIndex, int likeCount)
    {
        float oldBuff = likeGlobalBuff;
        float newBuff = GameConfig.LikeGlobalBuff[milestoneIndex];
        likeGlobalBuff = newBuff;
        currentLikeMilestoneIndex = milestoneIndex;

        // 全味方ユニットに差分バフ
        float ratio = newBuff / oldBuff;
        if (ratio > 1f)
        {
            foreach (var u in allyUnits)
            {
                if (u != null && !u.isDead)
                    u.ApplyStatMultiplier(ratio);
            }
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("LikeMilestone");
        CameraShake.Shake(0.2f, 0.05f);

        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement(
                $"<color=#FF69B4>\u2665 {likeCount} いいねたっせい!</color>\n" +
                $"<size=28>ぜんへいしステータス {Mathf.RoundToInt((newBuff - 1f) * 100)}% UP!</size>", 3f, 48);

        Debug.Log($"[StreamerKing] いいねマイルストーン! {likeCount} likes, buff={newBuff}x");
    }

    // ─── TikTok Action Effects ──────────────────────────────────

    void HandleTikTokGift(string viewerId, string viewerName, int tier, int coinAmount, string displayText)
    {
        if (ViewerStats.Instance != null)
            ViewerStats.Instance.SetTikTokGiftTier(viewerId, viewerName, tier, coinAmount);

        // 既存ユニットをアップグレード
        bool hasUnit = false;
        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead || u.ownerName != viewerName) continue;
            u.UpgradeTikTokGiftTier(tier);
            hasUnit = true;

            // ティア3+: オーラ効果
            if (tier >= 3)
            {
                float auraHP = tier >= 4 ? 1.20f : 1.10f;
                float auraATK = tier >= 4 ? 1.15f : 1.10f;
                u.SetAura(auraHP, auraATK, GameConfig.KnightAuraRange);
            }
            // ティア4+: 自動回復
            if (tier >= 4)
            {
                u.enableAutoHeal = true;
                u.autoHealRate = u.maxHP * 0.02f;
            }
            // ティア5: 虹色
            if (tier >= 5) u.SetRainbowEffect();
        }

        // ティア0-1: Warrior召喚（1視聴者あたり MaxGiftUnitsPerViewer体 まで）
        if (tier <= 1)
        {
            int viewerUnitCount = 0;
            foreach (var q in unitQueue)
                if (q.ownerName == viewerName) viewerUnitCount++;
            foreach (var u in allyUnits)
                if (u != null && !u.isDead && u.ownerName == viewerName) viewerUnitCount++;

            if (viewerUnitCount < GameConfig.MaxGiftUnitsPerViewer)
            {
                AddUnitToQueue(UnitType.Warrior, viewerName, viewerId);
            }
            else
            {
                // 上限到達: 既存ユニットを強化のみ
                Unit firstUnit = null;
                foreach (var u in allyUnits)
                {
                    if (u == null || u.isDead || u.ownerName != viewerName) continue;
                    u.ApplyStatMultiplier(GameConfig.GiftRepeatBuff);
                    if (firstUnit == null) firstUnit = u;
                }
                if (firstUnit != null)
                    DamagePopup.CreateText(firstUnit.transform.position + Vector3.up * 0.5f,
                        "強化!", new Color(1f, 0.85f, 0.2f));
            }
        }

        // ティア2+: エキストラユニット召喚
        int extra = GameConfig.TikTokGiftExtraUnits[tier];
        for (int i = 0; i < extra; i++)
        {
            UnitType type = (UnitType)Random.Range(0, 4);
            AddUnitToQueue(type, viewerName, viewerId);
        }

        // ティア5: 全軍永続バフ
        if (tier >= 5)
        {
            foreach (var u in allyUnits)
            {
                if (u != null && !u.isDead)
                    u.ApplyStatMultiplier(1.10f);
            }
        }

        // ギフト名はポップアップUIで表示（吹き出しには出さない）

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SuperChat");
        CameraShake.Shake(0.2f + tier * 0.1f, 0.05f + tier * 0.03f);

        var ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.ShowTikTokGiftPopup(viewerName, tier, displayText, coinAmount);

        Debug.Log($"[StreamerKing] TikTokギフト! {viewerName} {displayText} (Tier {tier}, {coinAmount}coins)");
    }

    void HandleNewTeamMember(string viewerId, string viewerName)
    {
        HandleTeamMemberDetected(viewerId, viewerName, 1);

        AddUnitToQueue(UnitType.Warrior, viewerName, viewerId);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("NewMember");
        CameraShake.Shake(0.15f, 0.04f);

        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement(
                $"<color=#00BFFF>\u265A {viewerName} がチームにさんか!</color>\n" +
                "<size=28>Warrior 1たい + チームバフ</size>", 4f, 48);

        Debug.Log($"[StreamerKing] \u65B0\u30C1\u30FC\u30E0\u30E1\u30F3\u30D0\u30FC! {viewerName}");
    }

    void HandleTeamMemberDetected(string viewerId, string viewerName, int teamLevel)
    {
        if (ViewerStats.Instance != null)
            ViewerStats.Instance.SetTeamLevel(viewerId, viewerName, teamLevel);

        int tierIdx = ViewerStats.GetTeamLevelTier(teamLevel);
        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead || u.ownerName != viewerName) continue;
            u.ApplyTeamBuff(tierIdx);
        }
    }

    void HandleNewSubscriber(string viewerId, string viewerName)
    {
        HandleSubscriberDetected(viewerId, viewerName);

        AddUnitToQueue(UnitType.Knight, viewerName, viewerId);

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("NewMember");
        CameraShake.Shake(0.2f, 0.05f);

        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement(
                $"<color=#FF00FF>\u2666 {viewerName} \u304C\u30B5\u30D6\u30B9\u30AF\u30EA\u30D7\u30B7\u30E7\u30F3!</color>\n" +
                "<size=28>Knight 1たい + HP+20% / ATK+15%</size>", 4f, 48);

        Debug.Log($"[StreamerKing] \u65B0\u30B5\u30D6\u30B9\u30AF\u30E9\u30A4\u30D0\u30FC! {viewerName}");
    }

    void HandleSubscriberDetected(string viewerId, string viewerName)
    {
        if (ViewerStats.Instance != null)
            ViewerStats.Instance.SetSubscriber(viewerId, viewerName);

        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead || u.ownerName != viewerName) continue;
            u.ApplySubscriptionBuff();
        }
    }

    void HandleTikTokLikeMilestone(int milestoneIndex, int likeCount)
    {
        float oldHP = tiktokLikeGlobalHPBuff;
        float oldATK = tiktokLikeGlobalATKBuff;
        float oldSpeed = tiktokLikeGlobalSpeedBuff;

        tiktokLikeGlobalHPBuff = GameConfig.TikTokLikeHPBuff[milestoneIndex];
        tiktokLikeGlobalATKBuff = GameConfig.TikTokLikeATKBuff[milestoneIndex];
        tiktokLikeGlobalSpeedBuff = GameConfig.TikTokLikeSpeedBuff[milestoneIndex];
        currentTikTokLikeMilestoneIndex = milestoneIndex;

        // 差分バフを全味方に適用
        float hpRatio = tiktokLikeGlobalHPBuff / oldHP;
        float atkRatio = tiktokLikeGlobalATKBuff / oldATK;
        float speedRatio = tiktokLikeGlobalSpeedBuff / oldSpeed;

        foreach (var u in allyUnits)
        {
            if (u == null || u.isDead) continue;
            if (hpRatio > 1f)
            {
                u.maxHP = Mathf.RoundToInt(u.maxHP * hpRatio);
                u.currentHP = Mathf.Min(u.currentHP + Mathf.RoundToInt(u.maxHP * 0.1f), u.maxHP);
            }
            if (atkRatio > 1f)
                u.attackPower = Mathf.RoundToInt(u.attackPower * atkRatio);
            if (speedRatio > 1f)
                u.moveSpeed *= speedRatio;
        }

        // 城回復
        int castleHeal = GameConfig.TikTokLikeCastleHeal[milestoneIndex];
        if (castleHeal > 0 && castle != null)
        {
            castle.currentHP = Mathf.Min(castle.currentHP + castleHeal, GameConfig.CastleMaxHP);
            DamagePopup.Create(castle.transform.position, castleHeal, true);
        }

        // 無敵時間
        float invincible = GameConfig.TikTokLikeInvincibleSec[milestoneIndex];
        if (invincible > 0f)
        {
            foreach (var u in allyUnits)
            {
                if (u != null && !u.isDead)
                    StartCoroutine(ApplyInvincibility(u, invincible));
            }
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("LikeMilestone");
        CameraShake.Shake(0.3f, 0.06f);

        string milestoneName = GameConfig.TikTokLikeMilestoneNames[milestoneIndex];
        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement(
                $"<color=#FF69B4>\u2665 {likeCount} \u3044\u3044\u306D! {milestoneName}!</color>\n" +
                "<size=28>ぜんへいしパワーアップ!</size>", 3f, 48);

        Debug.Log($"[StreamerKing] TikTok\u3044\u3044\u306D\u30DE\u30A4\u30EB\u30B9\u30C8\u30FC\u30F3! {likeCount} likes, {milestoneName}");
    }

    System.Collections.IEnumerator ApplyInvincibility(Unit unit, float duration)
    {
        if (unit == null || unit.isDead) yield break;
        float originalDR = unit.damageReduction;
        unit.damageReduction = 1f;
        yield return new WaitForSeconds(duration);
        if (unit != null && !unit.isDead)
            unit.damageReduction = originalDR;
    }

    void HandleFollow(string viewerId, string viewerName)
    {
        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement($"<color=#AADDFF>{viewerName} \u304C\u30D5\u30A9\u30ED\u30FC!</color>", 2f, 36);
    }

    void HandleShare(string viewerId, string viewerName)
    {
        var ui = FindObjectOfType<UIManager>();
        if (ui != null)
            ui.ShowAnnouncement($"<color=#AADDFF>{viewerName} \u304C\u30B7\u30A7\u30A2! \u3042\u308A\u304C\u3068\u3046!</color>", 2f, 36);
    }

    // ─── Kill Log ────────────────────────────────────────────────

    void HandleUnitKilled(Unit killer, Unit victim)
    {
        if (victim.team == Team.Enemy)
        {
            totalKills++;
            score += 100;
            waveManager.OnEnemyKilled();
            AddGold(GameConfig.GoldPerKill);
        }

        string killerName = killer != null
            ? $"{killer.ownerName} ({killer.unitType})"
            : "Castle";
        string entry = $"{killerName} defeated {victim.ownerName}'s {victim.unitType}!";
        killLog.Add(entry);
        if (killLog.Count > GameConfig.KillLogMaxEntries)
            killLog.RemoveAt(0);
    }

    // ─── Debug: YouTube Action テスト ────────────────────────────

    string GetRandomAliveAllyOwner()
    {
        var owners = new List<string>();
        foreach (var u in allyUnits)
        {
            if (u != null && !u.isDead && !string.IsNullOrEmpty(u.ownerName))
                if (!owners.Contains(u.ownerName)) owners.Add(u.ownerName);
        }
        // キューの中も探す
        foreach (var q in unitQueue)
            if (!owners.Contains(q.ownerName)) owners.Add(q.ownerName);
        if (owners.Count == 0) return null;
        return owners[Random.Range(0, owners.Count)];
    }

    public void DebugSimulateSuperChat(int tier)
    {
        string viewer = GetRandomAliveAllyOwner();
        if (viewer == null)
            viewer = $"TestViewer_{Random.Range(1, 100)}";
        tier = Mathf.Clamp(tier, 0, GameConfig.SuperChatTierMinJPY.Length - 1);
        int jpyAmount = GameConfig.SuperChatTierMinJPY[tier];
        HandleSuperChat(viewer, viewer, tier, jpyAmount, $"¥{jpyAmount:N0}");
    }

    public void DebugSimulateMember()
    {
        string viewer = GetRandomAliveAllyOwner();
        if (viewer == null)
            viewer = $"TestViewer_{Random.Range(1, 100)}";
        // 既にメンバーの場合は別の視聴者を探す
        if (ViewerStats.Instance != null && ViewerStats.Instance.IsMember(viewer))
        {
            var owners = new List<string>();
            foreach (var u in allyUnits)
                if (u != null && !u.isDead && !string.IsNullOrEmpty(u.ownerName))
                    if (!owners.Contains(u.ownerName) && !ViewerStats.Instance.IsMember(u.ownerName))
                        owners.Add(u.ownerName);
            if (owners.Count > 0)
                viewer = owners[Random.Range(0, owners.Count)];
        }
        HandleNewMember(viewer, viewer);
    }

    public void DebugSimulateLikeMilestone()
    {
        int nextIdx = currentLikeMilestoneIndex + 1;
        if (nextIdx >= GameConfig.LikeMilestones.Length)
        {
            Debug.Log("[Debug] 全いいねマイルストーン達成済み");
            return;
        }
        HandleLikeMilestone(nextIdx, GameConfig.LikeMilestones[nextIdx]);
    }

    // ─── Debug: TikTok Action テスト ────────────────────────────

    public void DebugSimulateTikTokGift(int tier)
    {
        string viewer = GetRandomAliveAllyOwner();
        if (viewer == null) viewer = $"TestViewer_{Random.Range(1, 100)}";
        tier = Mathf.Clamp(tier, 0, GameConfig.TikTokGiftTierMinCoins.Length - 1);
        int coins = GameConfig.TikTokGiftTierMinCoins[tier];
        string name = GameConfig.TikTokGiftTierNames[tier];
        HandleTikTokGift(viewer, viewer, tier, coins, $"{name} ({coins}coins)");
    }

    public void DebugSimulateTeamMember()
    {
        string viewer = GetRandomAliveAllyOwner();
        if (viewer == null) viewer = $"TestViewer_{Random.Range(1, 100)}";
        HandleNewTeamMember(viewer, viewer);
    }

    public void DebugSimulateSubscriber()
    {
        string viewer = GetRandomAliveAllyOwner();
        if (viewer == null) viewer = $"TestViewer_{Random.Range(1, 100)}";
        if (ViewerStats.Instance != null && ViewerStats.Instance.IsSubscriber(viewer))
        {
            var owners = new List<string>();
            foreach (var u in allyUnits)
                if (u != null && !u.isDead && !string.IsNullOrEmpty(u.ownerName))
                    if (!owners.Contains(u.ownerName) && !ViewerStats.Instance.IsSubscriber(u.ownerName))
                        owners.Add(u.ownerName);
            if (owners.Count > 0) viewer = owners[Random.Range(0, owners.Count)];
        }
        HandleNewSubscriber(viewer, viewer);
    }

    public void DebugSimulateTikTokLikeMilestone()
    {
        int nextIdx = currentTikTokLikeMilestoneIndex + 1;
        if (nextIdx >= GameConfig.TikTokLikeMilestones.Length)
        {
            Debug.Log("[Debug] \u5168TikTok\u3044\u3044\u306D\u30DE\u30A4\u30EB\u30B9\u30C8\u30FC\u30F3\u9054\u6210\u6E08\u307F");
            return;
        }
        HandleTikTokLikeMilestone(nextIdx, GameConfig.TikTokLikeMilestones[nextIdx]);
    }

    public void DebugSimulateFollow()
    {
        var name = $"TestFollower_{Random.Range(1, 100)}";
        HandleFollow(name, name);
    }

    public void DebugSimulateShare()
    {
        var name = $"TestSharer_{Random.Range(1, 100)}";
        HandleShare(name, name);
    }

    // ─── Damage Popup ────────────────────────────────────────────

    public void SpawnDamagePopup(Vector3 pos, int amount, bool isHeal)
    {
        DamagePopup.Create(pos, amount, isHeal);
    }

    // ─── Score Calculation ───────────────────────────────────────

    public int CalculateFinalScore()
    {
        int s = 0;

        // Wave clear bonus
        s += currentWaveIndex * 1000;

        // Enemy kill bonus
        s += totalKills * 100;

        // Castle HP remaining bonus
        if (castle != null)
            s += castle.currentHP * 10;

        // Speed bonus
        s += Mathf.Max(0, 3000 - Mathf.RoundToInt(elapsedTime * 5));

        // Ally survival bonus
        int alive = allyUnits.Count(u => u != null && !u.isDead);
        s += alive * 50;

        // No damage bonus (城にダメージなし)
        if (castleDamageTaken == 0)
            s += 5000;

        return s;
    }

    public string GetRank(int finalScore)
    {
        if (finalScore >= 30000) return "S";
        if (finalScore >= 20000) return "A";
        if (finalScore >= 12000) return "B";
        if (finalScore >= 5000) return "C";
        return "D";
    }

    // ─── Game Reset ──────────────────────────────────────────────

    public void ResetGame()
    {
        // Destroy all units
        foreach (var u in allyUnits) { if (u != null) Destroy(u.gameObject); }
        foreach (var u in enemyUnits) { if (u != null) Destroy(u.gameObject); }
        allyUnits.Clear();
        enemyUnits.Clear();
        unitQueue.Clear();

        currentWaveIndex = 0;
        score = 0;
        totalKills = 0;
        elapsedTime = 0f;
        castleDamageTaken = 0;
        gold = 0;
        goldTimer = 0f;
        killLog.Clear();
        likeGlobalBuff = 1f;
        currentLikeMilestoneIndex = -1;
        tiktokLikeGlobalHPBuff = 1f;
        tiktokLikeGlobalATKBuff = 1f;
        tiktokLikeGlobalSpeedBuff = 1f;
        currentTikTokLikeMilestoneIndex = -1;

        if (castle != null) castle.ResetCastle();
        if (enemyCastle != null)
        {
            enemyCastle.maxHP = GameConfig.GetEnemyCastleHP(0);
            enemyCastle.ResetCastle();
        }
        if (ViewerStats.Instance != null) ViewerStats.Instance.ResetAll();

        // YouTubeイベント購読解除
        var ytChat = GetComponent<YouTubeChatManager>();
        if (ytChat != null)
        {
            ytChat.OnSuperChat -= HandleSuperChat;
            ytChat.OnNewMember -= HandleNewMember;
            ytChat.OnMemberDetected -= HandleMemberDetected;
            ytChat.OnStreamConnected -= OnYouTubeStreamConnected;
            ytChat.OnLikeMilestone -= HandleLikeMilestone;
            ytChat.OnAuthenticated -= OnYouTubeAuthenticated;
            ytChat.enabled = false;
        }

        // TikTokイベント購読解除
        var ttChat = GetComponent<TikTokChatManager>();
        if (ttChat != null)
        {
            ttChat.OnGift -= HandleTikTokGift;
            ttChat.OnNewTeamMember -= HandleNewTeamMember;
            ttChat.OnTeamMemberDetected -= HandleTeamMemberDetected;
            ttChat.OnNewSubscriber -= HandleNewSubscriber;
            ttChat.OnSubscriberDetected -= HandleSubscriberDetected;
            ttChat.OnLikeMilestone -= HandleTikTokLikeMilestone;
            ttChat.OnStreamConnected -= OnTikTokStreamConnected;
            ttChat.OnFollow -= HandleFollow;
            ttChat.OnShare -= HandleShare;
            ttChat.enabled = false;
        }

        SetPhase(GamePhase.Title);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayBGM("BgmTitle");
        OnQueueChanged?.Invoke();
    }

    // ─── Castle Sprite ───────────────────────────────────────────

    Sprite LoadCastleSprite()
    {
#if UNITY_EDITOR
        string path = "Assets/Tiny Swords/Buildings/Blue Buildings/Castle.png";
        var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var s in sprites)
        {
            if (s is Sprite sprite) return sprite;
        }
#else
        var sp = Resources.Load<Sprite>("BlueCastle");
        if (sp != null) return sp;
#endif
        return null;
    }

    Sprite LoadEnemyCastleSprite()
    {
#if UNITY_EDITOR
        string path = "Assets/Tiny Swords/Buildings/Red Buildings/Castle.png";
        var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var s in sprites)
        {
            if (s is Sprite sprite) return sprite;
        }
#else
        var sp = Resources.Load<Sprite>("RedCastle");
        if (sp != null) return sp;
#endif
        return null;
    }

    // ─── Editor: アセットパック直接ロード ───────────────────────

#if UNITY_EDITOR
    void EditorAutoLoadPrefabs()
    {
        // Pixel Heroes AnimatorController
        if (pixelHeroAnimator == null)
            pixelHeroAnimator = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/PixelFantasy/PixelHeroes/Common/Animation/Character/Controller.controller");
    }
#endif
}
