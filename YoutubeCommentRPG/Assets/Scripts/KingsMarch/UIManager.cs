using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    private Canvas mainCanvas;
    private GameObject topBarGo;
    private Text waveText;
    // 戦力ゲージ（味方 vs 敵の比率バー）
    private Image forceGaugeAlly;
    private Image forceGaugeEnemy;
    private Text scoreText;
    private GameObject testModePanel;
    private GameObject levelDebugPanel;
    private Text levelDebugLabel;
    private Unit levelDebugPreviewUnit;
    // 接続ステータス
    private GameObject connectionStatusSlot;
    private Text connectionStatusText;
    private Text listenerInstructionText;
    private Text killLogText;
    private Text phaseAnnouncementText;
    private Button waveStartButton;
    private Button randomPlaceButton;

    private RectTransform queuePanel;
    private List<GameObject> queueSlots = new List<GameObject>();

    private GameObject resultPanel;
    private Text resultText;
    private RectTransform scrollContent;
    private float scrollSpeed = 40f;
    private bool isScrolling = false;

    private Toggle autoSpawnToggle;
    private float autoSpawnTimer;
    private float announcementTimer;

    // 武器選択デバッグ
    private string[] allWeaponNames;
    private int currentWeaponIndex;
    private Text weaponNameText;

    // ゴールド
    private Text goldText;
    private GameObject shopPanel;
    private List<Button> shopButtons = new List<Button>();
    private GameObject npcFormationPanel;

    // YouTube Action Effects
    private Text likeCountText;
    private GameObject superChatPopup;
    private Text superChatPopupText;
    private Image superChatPopupBg;
    private float superChatPopupTimer;

    private Text youtubeStatusText;
    private GameObject youtubePanel;
    private Text viewerRankingText; // 未使用（互換用に残す）
    private RectTransform rankingBar;
    private List<GameObject> rankingSlots = new List<GameObject>();

    // TikTok
    private GameObject tiktokPanel;
    private Text tiktokStatusText;
    private InputField tiktokUsernameInput;
    private Button tiktokConnectButton;

    // Setup connected state buttons
    private GameObject ytNextBtn;
    private GameObject ytLogoutBtn;
    private GameObject ttNextBtn;
    private GameObject ttLogoutBtn;
    private GameObject ytOfflineBtn;
    private GameObject ttOfflineBtn;
    private GameObject tiktokGiftPopup;
    private Text tiktokGiftPopupText;
    private Image tiktokGiftPopupBg;
    private float tiktokGiftPopupTimer;

    // Setup screen scaling
    private GameObject ytDebugStartBtn;
    private GameObject ttDebugStartBtn;
    private bool youtubeSetupMode;
    private bool tiktokSetupMode;

    // Debug panels (mode-specific visibility)
    private GameObject youtubeDebugPanel;
    private GameObject tiktokDebugPanel;

    // Unified Debug Panel
    private GameObject debugPanel;
    private GameObject debugToggleBtn;
    private RectTransform debugScrollContent;

    // Title screen
    private GameObject titlePanel;
    private RectTransform titleLogoRT;
    private CanvasGroup titleLogoCanvasGroup;
    private List<RectTransform> titleClouds = new List<RectTransform>();
    private bool cloudsScattering = false;

    // Wave +1/+10 buttons (TopBar内)
    private GameObject waveAdjustBtns;

    // Hamburger menu
    private GameObject hamburgerBtn;
    private GameObject settingsPanel;

    // Viewer List Panel
    private GameObject viewerListToggleBtn;
    private GameObject viewerListPanel;
    private RectTransform viewerListContent;
    private float viewerListRefreshTimer;
    private List<GameObject> viewerListRows = new List<GameObject>();
    // Viewer Detail Panel
    private GameObject viewerDetailPanel;
    private Text detailStatsText;
    private RawImage detailCharPreview;
    private Camera detailPreviewCam;
    private RenderTexture detailRT;
    private GameObject detailPreviewClone;
    private string currentDetailViewerId;

    // Tutorial
    private int tutorialStep = -1;
    private GameObject tutorialOverlay;
    private bool tutorialEditMode;
    private Vector2[] tutorialPositions;
    private Vector2[] tutorialArrowPositions; // 矢印先端位置（ステップ毎）
    private RectTransform tutorialEditBubbleRT;
    private RectTransform tutorialEditArrowHandleRT; // 矢印先端ハンドル
    private Text tutorialEditPosLabel;
    private bool tutorialDragging;
    private bool tutorialDraggingArrow; // 矢印ドラッグ中フラグ
    private Vector2 tutorialDragOffset;
    private GameObject tutorialArrowGroup;

    // 3Dカメラプレビュー
    private GameObject preview3DPanel;
    private RawImage preview3DImage;
    private GameObject backTo2DBtn;
    private Text followTargetText;

    // YouTube/TikTok profile image cache
    private Dictionary<string, Texture2D> profileImageCache = new Dictionary<string, Texture2D>();
    private HashSet<string> profileImageLoading = new HashSet<string>();

    /// <summary>キャッシュ済みのプロフィール画像を取得。なければnull</summary>
    public Texture2D GetProfileImage(string ownerName)
    {
        if (string.IsNullOrEmpty(ownerName)) return null;
        profileImageCache.TryGetValue(ownerName, out var tex);
        return tex;
    }

    /// <summary>プロフィール画像が未ダウンロードなら開始する</summary>
    public void RequestProfileImage(string ownerName, string url)
    {
        if (string.IsNullOrEmpty(ownerName) || string.IsNullOrEmpty(url)) return;
        if (profileImageCache.ContainsKey(ownerName)) return;
        if (profileImageLoading.Contains(ownerName)) return;
        StartCoroutine(DownloadProfileImage(ownerName, url));
    }

    // デバッグモード判定
    bool IsDebug() => GameManager.Instance != null && GameManager.Instance.debugMode;

    // donguri ピクセルフォント (全UI共通)
    private static Font _donguriFont;

    static Font GetFont()
    {
        if (_donguriFont != null) return _donguriFont;
#if UNITY_EDITOR
        _donguriFont = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/donguri/x10y12pxDonguriDuel.ttf");
#else
        _donguriFont = Resources.Load<Font>("x10y12pxDonguriDuel");
#endif
        if (_donguriFont == null)
            _donguriFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        return _donguriFont;
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        CreateUI();
        Debug.Log("[StreamerKing] UI created successfully.");

        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnPhaseChanged += OnPhaseChanged;
            gm.OnQueueChanged += RefreshQueue;
            OnPhaseChanged(gm.currentPhase);
            RefreshQueue();
        }

        SetupCursorHovers();
    }

    void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnPhaseChanged -= OnPhaseChanged;
            gm.OnQueueChanged -= RefreshQueue;
        }
    }

    /// <summary>Canvas内の全ButtonにPointerEnterイベントを付けてSelectableカーソルにする</summary>
    void SetupCursorHovers()
    {
        var ddc = GetComponent<DragDropController>();
        if (ddc == null || mainCanvas == null) return;

        foreach (var btn in mainCanvas.GetComponentsInChildren<Button>(true))
        {
            var trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener((_) => ddc.RequestSelectableCursor());
            trigger.triggers.Add(entry);
        }
    }

    void Update()
    {
        UpdateHUD();
        UpdateAnnouncement();
        UpdateTestMode();
        UpdateYouTubeStatus();
        UpdateViewerRanking();
        UpdateResultScroll();
        UpdateLikeCount();
        UpdateSuperChatPopup();
        UpdateTikTokStatus();
        UpdateTikTokGiftPopup();
        UpdateConnectionStatus();
        UpdateViewerList();
        UpdateTutorialEditDrag();
        LateUpdate3DPreview();
    }

    // ─── HUD Update ──────────────────────────────────────────────

    void UpdateHUD()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var wm = gm.GetComponent<WaveManager>();

        if (waveText != null)
            waveText.text = $"Wave {gm.currentWaveIndex + 1}";

        // 戦力ゲージ更新
        if (forceGaugeAlly != null)
        {
            int enemies = wm != null ? wm.GetEnemiesRemaining() : 0;
            int allies = 0;
            foreach (var u in gm.GetUnits(Team.Ally))
                if (u != null && !u.isDead) allies++;

            int total = allies + enemies;
            if (total > 0)
            {
                float allyRatio = (float)allies / total;
                float enemyRatio = (float)enemies / total;
                // 味方バー: 左端 → allyRatio まで
                var allyRT = forceGaugeAlly.rectTransform;
                allyRT.anchorMin = new Vector2(0, 0);
                allyRT.anchorMax = new Vector2(allyRatio, 1);
                // 敵バー: (1 - enemyRatio) → 右端
                var enemyRT = forceGaugeEnemy.rectTransform;
                enemyRT.anchorMin = new Vector2(1f - enemyRatio, 0);
                enemyRT.anchorMax = new Vector2(1, 1);
            }

            // 数字表示なし（味方/敵ラベルのみで識別）
        }

        if (goldText != null)
            goldText.text = $"G:{gm.gold}";

        if (scoreText != null)
            scoreText.text = $"Score: {gm.score}";

        // ショップボタンのグレーアウト
        if (shopButtons.Count > 0)
        {
            var types = new[] { UnitType.Warrior, UnitType.Lancer, UnitType.Archer, UnitType.Monk, UnitType.Mage };
            for (int i = 0; i < shopButtons.Count && i < types.Length; i++)
            {
                int cost = GameConfig.GetUnitCost(types[i]);
                shopButtons[i].interactable = gm.gold >= cost;
            }
        }

        if (killLogText != null)
            killLogText.text = string.Join("\n", gm.killLog);
    }

    // ─── Phase Changes ───────────────────────────────────────────

    void OnPhaseChanged(GamePhase phase)
    {
        // タイトル画面の表示/非表示
        if (titlePanel != null)
        {
            titlePanel.SetActive(phase == GamePhase.Title);
            if (phase == GamePhase.Title)
                ResetTitleScreen();
        }

        // HUD（TopBar）はゲーム中のみ表示
        if (topBarGo != null)
            topBarGo.SetActive(phase != GamePhase.Title);

        // デバッグトグルボタンはデバッグモード＆ゲーム中のみ表示
        if (debugToggleBtn != null)
            debugToggleBtn.SetActive(IsDebug() && phase != GamePhase.Title);
        if (phase == GamePhase.Title && debugPanel != null)
            debugPanel.SetActive(false);
        // YouTube/TikTokデバッグセクションのモード別表示
        {
            var gmDbg = GameManager.Instance;
            GameMode mode = gmDbg != null ? gmDbg.currentMode : GameMode.Offline;
            if (youtubeDebugPanel != null)
                youtubeDebugPanel.SetActive(mode == GameMode.YouTube);
            if (tiktokDebugPanel != null)
                tiktokDebugPanel.SetActive(mode == GameMode.TikTok);
        }

        // YouTube/TikTok認証パネルはゲーム開始後は非表示
        if (youtubePanel != null)
            youtubePanel.SetActive(false);
        if (tiktokPanel != null)
            tiktokPanel.SetActive(false);

        // 接続ステータス更新
        UpdateConnectionStatus();

        // キューパネルはタイトル・セットアップ中は非表示、ゲーム開始後に表示
        if (queuePanel != null)
            queuePanel.gameObject.SetActive(phase != GamePhase.Title);

        // タイトル画面ではアナウンスも消す
        if (phase == GamePhase.Title && phaseAnnouncementText != null)
            phaseAnnouncementText.gameObject.SetActive(false);

        if (waveStartButton != null)
            waveStartButton.gameObject.SetActive(phase == GamePhase.Preparation);

        // ショップパネルはゲームちゅうじょうじひょうじ
        if (shopPanel != null)
            shopPanel.SetActive(phase != GamePhase.Title && phase != GamePhase.Result);

        // ランダム配置ボタンは準備中に常時表示（キューにユニットがあれば）
        if (randomPlaceButton != null)
        {
            bool showRandom = phase == GamePhase.Preparation;
            randomPlaceButton.gameObject.SetActive(showRandom);
            // 両ボタン中央揃え
            CenterActionButtons(showRandom);
        }

        // Wave調整ボタンは準備フェーズのみ表示
        if (waveAdjustBtns != null)
            waveAdjustBtns.SetActive(phase == GamePhase.Preparation);

        // NPC陣形ボタンはバトル中のみ表示
        if (npcFormationPanel != null)
            npcFormationPanel.SetActive(phase != GamePhase.Title);

        // 3Dプレビューはバトル中のみ
        if (preview3DPanel != null)
            preview3DPanel.SetActive(phase == GamePhase.Battle);
        if (backTo2DBtn != null && phase != GamePhase.Battle)
            backTo2DBtn.SetActive(false);

        // リスナーリストはTitle以外で表示可能
        if (viewerListToggleBtn != null)
            viewerListToggleBtn.SetActive(phase != GamePhase.Title);
        if (phase == GamePhase.Title)
        {
            if (viewerListPanel != null) viewerListPanel.SetActive(false);
            CloseViewerDetail();
        }

        if (resultPanel != null)
            resultPanel.SetActive(phase == GamePhase.Result);

        switch (phase)
        {
            case GamePhase.Preparation:
                RestoreYouTubePanelPosition(); // オンライン接続画面から戻す
                RestoreTikTokPanelPosition();
                var gm = GameManager.Instance;
                int waveNum = gm != null ? gm.currentWaveIndex + 1 : 1;
                if (waveNum > 1)
                    StartCoroutine(WaveClearSequence(waveNum));
                // Wave 1はモード選択直後なのでアナウンス不要
                RefreshQueue();
                // 初回チュートリアル（少し遅延してPreparation画面を見せてから）
                if (PlayerPrefs.GetInt("TutorialComplete", 0) == 0)
                    StartCoroutine(DelayedStartTutorial());
                break;
            case GamePhase.Battle:
                int wn = GameManager.Instance != null ? GameManager.Instance.currentWaveIndex + 1 : 1;
                int totalEnemies = 0;
                var wm = GameManager.Instance?.GetComponent<WaveManager>();
                if (wm != null) totalEnemies = wm.GetTotalEnemiesInWave();

                if (totalEnemies >= 15)
                    StartCoroutine(WaveStartWarning(wn, totalEnemies));
                else
                    ShowAnnouncement($"Wave {wn} Start!", 2f);

                CameraShake.Shake(0.2f, 0.05f);
                break;
            case GamePhase.Result:
                ShowResult();
                break;
        }
    }

    IEnumerator WaveClearSequence(int nextWaveNum)
    {
        int clearedWave = nextWaveNum - 1;

        if (phaseAnnouncementText != null)
        {
            phaseAnnouncementText.text = $"<color=#FFD700>Wave {clearedWave} Clear!</color>";
            phaseAnnouncementText.fontSize = 72;
            phaseAnnouncementText.gameObject.SetActive(true);
        }
        CameraShake.Shake(0.3f, 0.06f);

        yield return new WaitForSeconds(1.5f);

        if (phaseAnnouncementText != null)
        {
            phaseAnnouncementText.text = $"<size=36><color=#AAA>Wave Bonus +{clearedWave * 1000}</color></size>";
            phaseAnnouncementText.fontSize = 36;
        }

        yield return new WaitForSeconds(1.0f);

        if (phaseAnnouncementText != null)
        {
            phaseAnnouncementText.text = $"Wave {nextWaveNum} じゅんびちゅう";
            phaseAnnouncementText.fontSize = 64;
        }

        yield return new WaitForSeconds(1.5f);

        if (phaseAnnouncementText != null)
            phaseAnnouncementText.gameObject.SetActive(false);
    }

    IEnumerator WaveStartWarning(int waveNum, int enemyCount)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("Warning");
        if (phaseAnnouncementText != null)
        {
            phaseAnnouncementText.text = $"<color=#FF4444>WARNING!</color>";
            phaseAnnouncementText.fontSize = 80;
            phaseAnnouncementText.gameObject.SetActive(true);
        }
        CameraShake.Shake(0.4f, 0.1f);

        yield return new WaitForSeconds(0.8f);

        if (phaseAnnouncementText != null)
        {
            phaseAnnouncementText.text = $"<color=#FF6644>Wave {waveNum}</color>\n<size=32>てき {enemyCount}たい しゅうらい!</size>";
            phaseAnnouncementText.fontSize = 64;
        }

        yield return new WaitForSeconds(1.5f);

        if (phaseAnnouncementText != null)
            phaseAnnouncementText.gameObject.SetActive(false);
    }

    public void ShowAnnouncement(string text, float duration, int fontSize = 64)
    {
        if (phaseAnnouncementText != null)
        {
            phaseAnnouncementText.text = text;
            phaseAnnouncementText.fontSize = fontSize;
            phaseAnnouncementText.gameObject.SetActive(true);
            announcementTimer = duration;
        }
    }

    void UpdateAnnouncement()
    {
        if (announcementTimer > 0)
        {
            announcementTimer -= Time.deltaTime;
            if (announcementTimer <= 0 && phaseAnnouncementText != null)
                phaseAnnouncementText.gameObject.SetActive(false);
        }
    }

    // ─── Result Screen ───────────────────────────────────────────

    void ShowResult()
    {
        if (resultPanel == null || scrollContent == null) return;
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 前回のスクロール内容をクリア
        for (int i = scrollContent.childCount - 1; i >= 0; i--)
            Destroy(scrollContent.GetChild(i).gameObject);

        int finalScore = gm.CalculateFinalScore();
        string rank = gm.GetRank(finalScore);
        string title = "<color=#FF6644>DEFEATED...</color>";
        var castle = FindObjectOfType<Castle>();

        float y = 0; // 下から上に積み上げる（yが大きいほど上）

        // ─── スコア部分 ───
        y += AddScrollText(title, 56, TextAnchor.MiddleCenter, Color.white, y);
        y += 20;
        y += AddScrollText($"Wave {gm.currentWaveIndex} までとうたつ", 40, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f), y);
        y += 30;
        y += AddScrollText($"Wave Bonus    {gm.currentWaveIndex * 1000}", 36, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f), y);
        y += AddScrollText($"Kill Bonus    {gm.totalKills * 100}", 36, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f), y);
        y += AddScrollText($"Castle HP     {(castle != null ? castle.currentHP : 0) * 10}", 36, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f), y);
        y += AddScrollText($"Speed Bonus   {Mathf.Max(0, 3000 - Mathf.RoundToInt(gm.elapsedTime * 5))}", 36, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.8f), y);
        y += 20;
        y += AddScrollText($"TOTAL  {finalScore}", 44, TextAnchor.MiddleCenter, Color.white, y);
        y += AddScrollText($"Rank  {rank}", 52, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f), y);
        y += 60;

        // ─── 視聴者ランキング ───
        if (ViewerStats.Instance != null && ViewerStats.Instance.GetViewerCount() > 0)
        {
            y += AddScrollText("--- さんかしゃランキング ---", 52, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f), y);
            y += 20;

            var ranking = ViewerStats.Instance.GetRanking(100); // 全員表示
            int maxScore = ranking.Count > 0 ? ranking[0].score : 1;

            for (int i = 0; i < ranking.Count; i++)
            {
                var v = ranking[i];
                float ratio = (float)v.score / Mathf.Max(maxScore, 1);

                // アイコンサイズ: 1位が最大、順位が下がるほど小さく（最小50%）
                float iconScale = Mathf.Lerp(0.5f, 1.0f, ratio);
                int baseFontSize = 48;
                int fontSize = Mathf.RoundToInt(baseFontSize * iconScale);
                float rowHeight = 100 * iconScale;

                // メダル
                string medal = "";
                Color nameColor = new Color(0.9f, 0.9f, 0.9f);
                if (i == 0) { medal = "  "; nameColor = new Color(1f, 0.85f, 0.3f); }
                else if (i == 1) { medal = "  "; nameColor = new Color(0.78f, 0.78f, 0.82f); }
                else if (i == 2) { medal = "  "; nameColor = new Color(0.8f, 0.55f, 0.3f); }

                // ランキング行を作成
                string rankNum = $"{i + 1}.";
                y += AddRankingRow(rankNum, v, medal, fontSize, iconScale, nameColor, y);
                y += 5;
            }

            y += 40;

            // MVP
            var mvp = ViewerStats.Instance.GetMVP();
            if (mvp != null)
            {
                y += AddScrollText("MVP", 64, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f), y);
                y += AddScrollText(mvp.ownerName, 72, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f), y);
                y += AddScrollText($"{mvp.score} pt", 52, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.7f), y);
                y += 20;
            }
        }

        y += 60;
        y += AddScrollText("Thanks for Playing!", 56, TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 1f), y);
        y += 100;

        // スクロールコンテンツの高さを設定
        scrollContent.sizeDelta = new Vector2(900, y);
        // 画面下から開始（最初は画面外の下にいる）
        scrollContent.anchoredPosition = new Vector2(0, -Screen.height * 0.5f);

        resultPanel.SetActive(true);
        isScrolling = true;
    }

    float AddScrollText(string text, int fontSize, TextAnchor align, Color color, float yPos)
    {
        float h = fontSize + 10;
        var go = new GameObject("ScrollItem");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(scrollContent, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(0, h);
        var txt = go.AddComponent<Text>();
        txt.font = GetFont();
        txt.text = text;
        txt.fontSize = fontSize;
        txt.alignment = align;
        txt.color = color;
        txt.supportRichText = true;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        return h;
    }

    float AddRankingRow(string rankNum, ViewerData v, string medal, int fontSize, float scale, Color nameColor, float yPos)
    {
        float rowH = Mathf.Max(100 * scale, 60);
        float iconSize = 80 * scale;

        var rowGo = new GameObject("RankRow");
        var rowRT = rowGo.AddComponent<RectTransform>();
        rowRT.SetParent(scrollContent, false);
        rowRT.anchorMin = new Vector2(0, 0);
        rowRT.anchorMax = new Vector2(1, 0);
        rowRT.pivot = new Vector2(0.5f, 0);
        rowRT.anchoredPosition = new Vector2(0, yPos);
        rowRT.sizeDelta = new Vector2(0, rowH);

        // 順位
        var rankGo = new GameObject("Rank");
        var rankRT = rankGo.AddComponent<RectTransform>();
        rankRT.SetParent(rowRT, false);
        rankRT.anchorMin = new Vector2(0, 0);
        rankRT.anchorMax = new Vector2(0, 1);
        rankRT.pivot = new Vector2(0, 0.5f);
        rankRT.anchoredPosition = new Vector2(80, 0);
        rankRT.sizeDelta = new Vector2(100, 0);
        var rankTxt = rankGo.AddComponent<Text>();
        rankTxt.font = GetFont();
        rankTxt.text = medal + rankNum;
        rankTxt.fontSize = fontSize;
        rankTxt.alignment = TextAnchor.MiddleRight;
        rankTxt.color = nameColor;
        rankTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

        // プロフィールアイコン（YouTube/TikTok画像 or 頭文字）
        var iconGo = new GameObject("Icon");
        var iconRT = iconGo.AddComponent<RectTransform>();
        iconRT.SetParent(rowRT, false);
        iconRT.anchorMin = new Vector2(0, 0.5f);
        iconRT.anchorMax = new Vector2(0, 0.5f);
        iconRT.pivot = new Vector2(0.5f, 0.5f);
        iconRT.anchoredPosition = new Vector2(210, 0);
        iconRT.sizeDelta = new Vector2(iconSize, iconSize);
        SetupProfileIcon(iconGo, v, iconSize);

        // 名前
        var nameGo = new GameObject("Name");
        var nameRT = nameGo.AddComponent<RectTransform>();
        nameRT.SetParent(rowRT, false);
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(0, 1);
        nameRT.pivot = new Vector2(0, 0.5f);
        nameRT.anchoredPosition = new Vector2(260, 0);
        nameRT.sizeDelta = new Vector2(300, 0);
        var nameTxt = nameGo.AddComponent<Text>();
        nameTxt.font = GetFont();
        nameTxt.text = v.ownerName;
        nameTxt.fontSize = fontSize;
        nameTxt.alignment = TextAnchor.MiddleLeft;
        nameTxt.color = nameColor;
        nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

        // スコア + 詳細
        var scoreTxt = new GameObject("Score");
        var scoreRT = scoreTxt.AddComponent<RectTransform>();
        scoreRT.SetParent(rowRT, false);
        scoreRT.anchorMin = new Vector2(1, 0);
        scoreRT.anchorMax = new Vector2(1, 1);
        scoreRT.pivot = new Vector2(1, 0.5f);
        scoreRT.anchoredPosition = new Vector2(-60, 0);
        scoreRT.sizeDelta = new Vector2(350, 0);
        var sTxt = scoreTxt.AddComponent<Text>();
        sTxt.font = GetFont();
        sTxt.text = $"{v.score}pt  K:{v.kills} D:{v.damageDealt} H:{v.healAmount}";
        sTxt.fontSize = Mathf.RoundToInt(fontSize * 0.75f);
        sTxt.alignment = TextAnchor.MiddleRight;
        sTxt.color = new Color(0.7f, 0.7f, 0.7f);
        sTxt.horizontalOverflow = HorizontalWrapMode.Overflow;

        return rowH;
    }

    // ─── Queue Display ───────────────────────────────────────────

    void RefreshQueue()
    {
        var gm = GameManager.Instance;
        if (gm == null || queuePanel == null) return;

        foreach (var s in queueSlots)
            if (s != null) Destroy(s);
        queueSlots.Clear();

        var queue = gm.GetQueue();
        for (int i = 0; i < queue.Count; i++)
            queueSlots.Add(CreateQueueSlot(queue[i], i));

        // 接続ステータス枠を右端に配置
        UpdateConnectionStatus();
    }

    /// <summary>プロフィールアイコン表示（YouTube/TikTok画像 → ダウンロード中プレースホルダー → 頭文字アイコン）</summary>
    void SetupProfileIcon(GameObject iconGo, ViewerData v, float size)
    {
        if (!string.IsNullOrEmpty(v.profileImageUrl))
        {
            if (profileImageCache.TryGetValue(v.ownerName, out var tex) && tex != null)
            {
                var rawImg = iconGo.AddComponent<RawImage>();
                rawImg.texture = tex;
                rawImg.raycastTarget = false;
            }
            else
            {
                // ダウンロード中: 頭文字プレースホルダー
                CreateInitialIcon(iconGo, v.ownerName, size);
                if (!profileImageLoading.Contains(v.ownerName))
                    StartCoroutine(DownloadProfileImage(v.ownerName, v.profileImageUrl));
            }
        }
        else
        {
            CreateInitialIcon(iconGo, v.ownerName, size);
        }
    }

    /// <summary>名前の頭文字をカラー背景で表示するフォールバックアイコン</summary>
    void CreateInitialIcon(GameObject parent, string name, float size)
    {
        var img = parent.AddComponent<Image>();
        int hash = string.IsNullOrEmpty(name) ? 0 : name.GetHashCode();
        float h = Mathf.Abs(hash % 360) / 360f;
        img.color = Color.HSVToRGB(h, 0.45f, 0.55f);
        img.raycastTarget = false;

        var txtGo = new GameObject("Initial");
        var txtRT = txtGo.AddComponent<RectTransform>();
        txtRT.SetParent(parent.transform, false);
        txtRT.anchorMin = Vector2.zero;
        txtRT.anchorMax = Vector2.one;
        txtRT.sizeDelta = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.font = GetFont();
        txt.text = !string.IsNullOrEmpty(name) ? name.Substring(0, 1).ToUpper() : "?";
        txt.fontSize = Mathf.RoundToInt(size * 0.55f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.raycastTarget = false;
    }

    GameObject CreateQueueSlot(QueuedUnit qUnit, int index)
    {
        var go = new GameObject($"QueueSlot_{index}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(queuePanel, false);
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.sizeDelta = new Vector2(170, 170);
        rt.anchoredPosition = new Vector2(10 + index * 180, 0);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);

        // Pixel Heroプレビュースプライト表示
        var iconGo = new GameObject("UnitIcon");
        var iconRT = iconGo.AddComponent<RectTransform>();
        iconRT.SetParent(rt, false);
        iconRT.anchorMin = new Vector2(0.5f, 0.55f);
        iconRT.anchorMax = new Vector2(0.5f, 0.55f);
        iconRT.pivot = new Vector2(0.5f, 0.3f);
        iconRT.sizeDelta = new Vector2(300, 300);
        iconRT.anchoredPosition = Vector2.zero;
        var iconImg = iconGo.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        if (qUnit.previewSprite != null)
        {
            iconImg.sprite = qUnit.previewSprite;
        }
        else
        {
            // フォールバック: 色付き背景 + ロール記号
            iconImg.color = GetUnitTypeColor(qUnit.type);
            var iconText = new GameObject("Icon");
            var iconTextRT = iconText.AddComponent<RectTransform>();
            iconTextRT.SetParent(iconRT, false);
            iconTextRT.anchorMin = Vector2.zero;
            iconTextRT.anchorMax = Vector2.one;
            iconTextRT.sizeDelta = Vector2.zero;
            iconTextRT.anchoredPosition = Vector2.zero;
            var iconTxt = iconText.AddComponent<Text>();
            iconTxt.font = GetFont();
            iconTxt.text = GetUnitTypeSymbol(qUnit.type);
            iconTxt.fontSize = 72;
            iconTxt.fontStyle = FontStyle.Bold;
            iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.color = Color.white;
        }

        // Owner name (bottom)
        var nameTextGo = new GameObject("OwnerName");
        var nameRT = nameTextGo.AddComponent<RectTransform>();
        nameRT.SetParent(rt, false);
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(1, 0.28f);
        nameRT.pivot = new Vector2(0.5f, 0.5f);
        nameRT.sizeDelta = Vector2.zero;
        nameRT.anchoredPosition = Vector2.zero;
        var nameTxt = nameTextGo.AddComponent<Text>();
        nameTxt.font = GetFont();
        nameTxt.text = qUnit.ownerName;
        nameTxt.fontSize = 32;
        nameTxt.alignment = TextAnchor.MiddleCenter;
        nameTxt.color = new Color(0.85f, 0.85f, 0.7f);
        nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameTxt.raycastTarget = false;

        // メンバー/スパチャバッジ
        if (ViewerStats.Instance != null)
        {
            var vd = !string.IsNullOrEmpty(qUnit.viewerId)
                ? ViewerStats.Instance.GetStats(qUnit.viewerId)
                : ViewerStats.Instance.GetStatsByName(qUnit.ownerName);
            if (vd != null)
            {
                // スパチャ/ギフトティアの枠色
                if (vd.superChatTier >= 0)
                {
                    Color tierColor = GameConfig.SuperChatTierColors[vd.superChatTier];
                    bg.color = new Color(tierColor.r * 0.4f, tierColor.g * 0.4f, tierColor.b * 0.4f, 0.95f);
                }
                else if (vd.tiktokGiftTier >= 0)
                {
                    Color tierColor = GameConfig.TikTokGiftTierColors[vd.tiktokGiftTier];
                    bg.color = new Color(tierColor.r * 0.4f, tierColor.g * 0.4f, tierColor.b * 0.4f, 0.95f);
                }

                // メンバー/サブスク/チームバッジ
                if (vd.isMember)
                {
                    var badgeGo = new GameObject("MemberBadge");
                    var badgeRT = badgeGo.AddComponent<RectTransform>();
                    badgeRT.SetParent(rt, false);
                    badgeRT.anchorMin = new Vector2(1, 1);
                    badgeRT.anchorMax = new Vector2(1, 1);
                    badgeRT.pivot = new Vector2(1, 1);
                    badgeRT.anchoredPosition = new Vector2(-2, -2);
                    badgeRT.sizeDelta = new Vector2(30, 30);
                    var badgeTxt = badgeGo.AddComponent<Text>();
                    badgeTxt.font = GetFont();
                    badgeTxt.text = "\u2605";
                    badgeTxt.fontSize = 24;
                    badgeTxt.alignment = TextAnchor.MiddleCenter;
                    badgeTxt.color = new Color(1f, 0.85f, 0.2f);
                    badgeTxt.raycastTarget = false;
                }
            }
        }

        // PointerDownでドラッグ開始（onClickだとマウスアップで発火しドラッグできない）
        var trigger = go.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry();
        entry.eventID = EventTriggerType.PointerDown;
        int capturedIdx = index;
        entry.callback.AddListener((data) => OnQueueSlotClicked(capturedIdx));
        trigger.triggers.Add(entry);

        // ホバー時にSelectableカーソル
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((_) =>
        {
            var ddc = GetComponent<DragDropController>();
            if (ddc != null) ddc.RequestSelectableCursor();
        });
        trigger.triggers.Add(enterEntry);

        return go;
    }

    Color GetUnitTypeColor(UnitType type)
    {
        switch (type)
        {
            case UnitType.Warrior: return new Color(0.3f, 0.45f, 0.85f);
            case UnitType.Lancer:  return new Color(0.2f, 0.6f, 0.65f);
            case UnitType.Archer:  return new Color(0.8f, 0.6f, 0.15f);
            case UnitType.Monk:    return new Color(0.3f, 0.7f, 0.35f);
            case UnitType.Mage:    return new Color(0.5f, 0.2f, 0.7f);
            case UnitType.Knight:  return new Color(0.7f, 0.3f, 0.8f);
            default:               return new Color(0.4f, 0.4f, 0.4f);
        }
    }

    string GetUnitTypeSymbol(UnitType type)
    {
        switch (type)
        {
            case UnitType.Warrior: return "W";
            case UnitType.Lancer:  return "L";
            case UnitType.Archer:  return "A";
            case UnitType.Monk:    return "M";
            case UnitType.Mage:    return "\u706B";
            case UnitType.Knight:  return "K";
            default:               return "?";
        }
    }

    void OnQueueSlotClicked(int index)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        bool canPlace = gm.currentPhase == GamePhase.Preparation || gm.currentPhase == GamePhase.Battle;
        if (!canPlace) return;
        var ddc = gm.GetComponent<DragDropController>();
        if (ddc != null) ddc.StartDragFromQueue(index);
    }

    // ─── Test Mode ───────────────────────────────────────────────

    void UpdateTestMode()
    {
        if (autoSpawnToggle == null || !autoSpawnToggle.isOn) return;
        autoSpawnTimer -= Time.deltaTime;
        if (autoSpawnTimer <= 0)
        {
            autoSpawnTimer = GameConfig.AutoSpawnInterval;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                UnitType type = (UnitType)Random.Range(0, 5);
                string[] names = { "Viewer1", "Viewer2", "Viewer3", "TinyFan", "Dragon", "Knight99", "HealBot", "ArrowRain" };
                gm.AddUnitToQueue(type, names[Random.Range(0, names.Length)]);
            }
        }
    }

    void OnTestAddUnit(UnitType type)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.AddUnitToQueue(type, "TestUser");
    }

    void DebugTriggerUltimate(UnitType type)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        // 既存の該当ロールの味方を探す
        Unit target = null;
        foreach (var u in gm.allyUnits)
        {
            if (u != null && !u.isDead && u.unitType == type)
            {
                target = u;
                break;
            }
        }

        // いなければスポーンして即発動
        if (target == null)
        {
            var pos = new Vector3(GameConfig.CastleX + 2f, GameConfig.CastleY, 0);
            target = gm.SpawnUnit(type, Team.Ally, "UltTest", pos);
        }

        if (target != null)
        {
            target.PerformUltimate();
        }
    }

    void OnWeaponPrev()
    {
        if (allWeaponNames == null || allWeaponNames.Length == 0) return;
        currentWeaponIndex = (currentWeaponIndex - 1 + allWeaponNames.Length) % allWeaponNames.Length;
        ApplyWeaponSelection();
    }

    void OnWeaponNext()
    {
        if (allWeaponNames == null || allWeaponNames.Length == 0) return;
        currentWeaponIndex = (currentWeaponIndex + 1) % allWeaponNames.Length;
        ApplyWeaponSelection();
    }

    void ApplyWeaponSelection()
    {
        string name = allWeaponNames[currentWeaponIndex];
        PlayerPrefs.SetString("SpearWeapon", name);
        PlayerPrefs.Save();
        Unit.ClearSpearSpriteCache();
        if (weaponNameText != null)
            weaponNameText.text = name;
    }

    // ─── UI Creation ─────────────────────────────────────────────

    void CreateUI()
    {
        var canvasGo = new GameObject("StreamerKingCanvas");
        mainCanvas = canvasGo.AddComponent<Canvas>();
        mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        mainCanvas.sortingOrder = 100;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f; // 高さ基準で統一（エディタとビルドのUI差異を解消）

        canvasGo.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        CreateTitleScreen();
        CreateHUD();
        CreateWaveStartButton();
        CreateRandomPlaceButton();
        CreateShopPanel();
        CreateNPCFormationPanel();
        CreateQueuePanel();
        CreateKillLog();
        CreateAnnouncement();
        CreateResultPanel();
        CreateYouTubeStatusPanel();
        CreateViewerRankingPanel();
        CreateTikTokStatusPanel();
        CreateDebugPanel();
        CreateHamburgerMenu();
        CreateViewerListPanel();
        Create3DPreviewPanel();
    }

    void CreateTitleScreen()
    {
        titlePanel = new GameObject("TitlePanel");
        var rt = titlePanel.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;

        var bg = titlePanel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.15f, 0.97f);

        // ロゴ画像
        var logoSprite = LoadLogoSprite();
        if (logoSprite != null)
        {
            var logoGo = new GameObject("Logo");
            var logoRT = logoGo.AddComponent<RectTransform>();
            logoRT.SetParent(rt, false);
            logoRT.anchorMin = new Vector2(0.5f, 0.65f);
            logoRT.anchorMax = new Vector2(0.5f, 0.65f);
            logoRT.pivot = new Vector2(0.5f, 0.5f);
            logoRT.anchoredPosition = Vector2.zero;
            logoRT.sizeDelta = new Vector2(1200, 680);
            var logoImg = logoGo.AddComponent<Image>();
            logoImg.sprite = logoSprite;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            titleLogoRT = logoRT;
            titleLogoCanvasGroup = logoGo.AddComponent<CanvasGroup>();

            // ロゴ周辺の金キラキラエフェクト
            StartCoroutine(LogoSparkleEffect(logoRT));
        }

        // 雲エフェクト（左右にフワフワ）
        SpawnTitleClouds(rt);

        // ボタンリスト構築（debugMode/releaseMode に応じて表示するボタンを決定）
        var gm = GameManager.Instance;
        var rMode = gm != null ? gm.releaseMode : ReleaseMode.Both;
        bool showYT = rMode == ReleaseMode.Both || rMode == ReleaseMode.YouTubeOnly;
        bool showTT = rMode == ReleaseMode.Both || rMode == ReleaseMode.TikTokOnly;

        var buttons = new System.Collections.Generic.List<System.Action<float>>();

        if (IsDebug())
        {
            buttons.Add((y) => CreateTitleSwordButton(rt, "OfflineBtn", 3,
                new Vector2(0.5f, y), "Offline Mode",
                new Color(0.2f, 0.5f, 0.25f, 1f),
                () => {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SwordDraw");
                    StartCloudScatterThenAction(() => GameManager.Instance?.StartOfflineMode());
                }));
        }
        if (showYT)
        {
            buttons.Add((y) => CreateTitleSwordButton(rt, "OnlineBtn", 1,
                new Vector2(0.5f, y), "YouTube LIVE Mode",
                new Color(0.15f, 0.35f, 0.65f, 1f),
                () => {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SwordDraw");
                    StartCloudScatterThenAction(() => GameManager.Instance?.StartOnlineMode());
                }));
        }
        if (showTT)
        {
            buttons.Add((y) => CreateTitleSwordButton(rt, "TikTokBtn", 2,
                new Vector2(0.5f, y), "TikTok LIVE Mode",
                new Color(0.6f, 0.2f, 0.5f, 1f),
                () => {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SwordDraw");
                    StartCloudScatterThenAction(() => GameManager.Instance?.StartTikTokMode());
                }));
        }

        // 固定の上端位置から下に詰めて配置（上詰め）
        float topY = 0.30f;
        float spacing = 0.09f;
        for (int i = 0; i < buttons.Count; i++)
        {
            float y = buttons.Count == 1 ? 0.20f : topY - i * spacing;
            buttons[i](y);
        }

        // 終了ボタン（右下）
        var quitGo = MakePanel("QuitBtn", rt,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-20, 20), new Vector2(140, 50), new Color(0.3f, 0.1f, 0.1f, 0.85f));
        var quitBtn = quitGo.gameObject.AddComponent<Button>();
        quitBtn.targetGraphic = quitGo.GetComponent<Image>();
        var qcb = quitBtn.colors;
        qcb.normalColor = new Color(0.3f, 0.1f, 0.1f, 0.85f);
        qcb.highlightedColor = new Color(0.5f, 0.15f, 0.15f, 0.95f);
        qcb.pressedColor = new Color(0.2f, 0.05f, 0.05f, 1f);
        quitBtn.colors = qcb;
        var qnav = quitBtn.navigation;
        qnav.mode = Navigation.Mode.None;
        quitBtn.navigation = qnav;
        quitBtn.onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });
        var quitTxt = MakeText("QuitText", quitGo, "EXIT", 32,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        quitTxt.color = new Color(1f, 0.7f, 0.7f);
        quitTxt.fontStyle = FontStyle.Bold;
        var qOutline = quitTxt.gameObject.AddComponent<Outline>();
        qOutline.effectColor = new Color(0, 0, 0, 0.8f);
        qOutline.effectDistance = new Vector2(2, -2);

        // コピーライト（最下部中央）
        var copyTxt = MakeText("Copyright", rt,
            "\u00a9 2026 Sam's Hetare Eiyuutan / Powered by Unity / Assets: Pixel Heroes (Cainos), Tiny Swords (Pixel Frog)",
            18,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, 6), new Vector2(0, 24), TextAnchor.MiddleCenter);
        copyTxt.color = new Color(0.6f, 0.6f, 0.65f, 0.5f);
    }

    void ResetTitleScreen()
    {
        // ロゴを復元（位置・スケール・透明度）
        if (titleLogoRT != null)
        {
            titleLogoRT.anchoredPosition = Vector2.zero;
            titleLogoRT.localScale = Vector3.one;
        }
        if (titleLogoCanvasGroup != null)
            titleLogoCanvasGroup.alpha = 1f;

        // ボタンを復元（位置・透明度・操作可能）
        if (titlePanel != null)
        {
            foreach (var btn in titlePanel.GetComponentsInChildren<Button>(true))
            {
                btn.interactable = true;
                var btnRT = btn.GetComponent<RectTransform>();
                btnRT.anchoredPosition = new Vector2(10, -8); // CreateTitleSwordButton初期値
                var cg = btn.gameObject.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f;
            }
        }

        // 背景色を復元
        if (titlePanel != null)
        {
            var bg = titlePanel.GetComponent<Image>();
            if (bg != null)
                bg.color = new Color(0.08f, 0.1f, 0.15f, 0.97f);
        }

        // 残存する雲を破棄して再生成
        foreach (var rt in titleClouds)
            if (rt != null) Destroy(rt.gameObject);
        titleClouds.Clear();
        cloudsScattering = false;

        var titleRT = titlePanel.GetComponent<RectTransform>();
        if (titleRT != null)
            SpawnTitleClouds(titleRT);

        // キラキラエフェクト再開
        if (titleLogoRT != null)
            StartCoroutine(LogoSparkleEffect(titleLogoRT));
    }

    void CreateTitleSwordButton(RectTransform parent, string name, int swordIndex,
        Vector2 anchor, string label, Color fallbackColor, UnityEngine.Events.UnityAction onClick)
    {
        var swordSprite = LoadSwordSprite(swordIndex);
        var btnGo = MakePanel(name, parent,
            anchor, anchor, new Vector2(0.5f, 0.5f),
            new Vector2(10, -8), new Vector2(680, 110), Color.white);
        var btnImg = btnGo.GetComponent<Image>();
        if (swordSprite != null)
        {
            btnImg.sprite = swordSprite;
            btnImg.type = Image.Type.Sliced; // 9-slice: 柄固定、刀身伸縮
        }
        else
        {
            btnImg.color = fallbackColor;
        }
        var btn = btnGo.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        btn.colors = cb;
        // EventSystemの自動選択を防止
        var nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        btn.onClick.AddListener(onClick);

        // テキストは刀身部分に左揃え配置（左30%が柄なので0.32から開始）
        var txt = MakeText("Text", btnGo, label, 42,
            new Vector2(0.32f, 0), new Vector2(0.90f, 1), new Vector2(0, 0.5f),
            new Vector2(15, 5), Vector2.zero, TextAnchor.MiddleLeft);
        txt.fontStyle = FontStyle.Bold;
        // 濃い黒縁（Outline + Shadow 2重で太く）
        var outline = txt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0, 0, 0, 1f);
        outline.effectDistance = new Vector2(3, -3);
        var shadow1 = txt.gameObject.AddComponent<Shadow>();
        shadow1.effectColor = new Color(0, 0, 0, 1f);
        shadow1.effectDistance = new Vector2(-2, 2);
        var shadow2 = txt.gameObject.AddComponent<Shadow>();
        shadow2.effectColor = new Color(0, 0, 0, 0.8f);
        shadow2.effectDistance = new Vector2(1, -1);
    }

    IEnumerator LogoSparkleEffect(RectTransform logoRT)
    {
        string symbol = "\u2726"; // ✦ のみ
        float halfW = logoRT.sizeDelta.x * 0.45f;
        float halfH = logoRT.sizeDelta.y * 0.4f;

        while (logoRT != null && logoRT.gameObject.activeInHierarchy)
        {
            // 1フレームに1個生成
            var sparkle = new GameObject("Sparkle");
            var srt = sparkle.AddComponent<RectTransform>();
            srt.SetParent(logoRT, false);

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radiusX = Random.Range(halfW * 0.5f, halfW);
            float radiusY = Random.Range(halfH * 0.4f, halfH);
            srt.anchoredPosition = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            srt.sizeDelta = new Vector2(40, 40);

            var txt = sparkle.AddComponent<Text>();
            txt.font = GetFont();
            txt.text = symbol;
            txt.fontSize = Random.Range(16, 32);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            float gold = Random.Range(0.8f, 1f);
            txt.color = new Color(1f, gold, Random.Range(0.2f, 0.5f), 0f);

            StartCoroutine(AnimateSparkle(sparkle, txt, Random.Range(0.6f, 1.2f)));

            yield return new WaitForSeconds(Random.Range(0.05f, 0.15f));
        }
    }

    IEnumerator AnimateSparkle(GameObject go, Text txt, float lifetime)
    {
        float elapsed = 0f;
        float startSize = Random.Range(0.3f, 0.6f);
        float peakSize = Random.Range(1f, 1.5f);
        Vector2 drift = new Vector2(Random.Range(-15f, 15f), Random.Range(10f, 30f));
        var rt = go.GetComponent<RectTransform>();
        Vector2 startPos = rt.anchoredPosition;
        Color baseColor = txt.color;

        while (elapsed < lifetime)
        {
            float t = elapsed / lifetime;
            // フェードイン→フェードアウト
            float alpha = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
            txt.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha * 0.9f);

            // スケール: 小→大→小
            float scale = Mathf.Lerp(startSize, peakSize, t < 0.4f ? t / 0.4f : 1f - (t - 0.4f) / 0.6f);
            go.transform.localScale = new Vector3(scale, scale, 1f);

            // 上方向にゆっくりドリフト
            rt.anchoredPosition = startPos + drift * t;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(go);
    }

    // ─── Title Clouds ─────────────────────────────────────────

    void SpawnTitleClouds(RectTransform parent)
    {
        titleClouds.Clear();
        int[] cloudIndices = { 1, 2, 3, 4, 5, 6, 7, 8 };

        // 左側に4つ、右側に4つ
        for (int i = 0; i < 8; i++)
        {
            var sprite = LoadCloudSprite(cloudIndices[i % cloudIndices.Length]);
            if (sprite == null) continue;

            var go = new GameObject($"Cloud_{i}");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);

            bool leftSide = i < 4;
            float xRange = leftSide ? Random.Range(-700f, -250f) : Random.Range(250f, 700f);
            float yRange = Random.Range(-250f, 300f);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xRange, yRange);

            float cloudScale = Random.Range(200f, 400f);
            rt.sizeDelta = new Vector2(cloudScale * 1.5f, cloudScale);

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = new Color(1f, 1f, 1f, Random.Range(0.5f, 0.85f));

            // ロゴやボタンの後ろに配置
            rt.SetAsFirstSibling();

            titleClouds.Add(rt);

            // フワフワ浮遊アニメーション
            StartCoroutine(CloudFloatEffect(rt, img));
        }
    }

    IEnumerator CloudFloatEffect(RectTransform rt, Image img)
    {
        Vector2 basePos = rt.anchoredPosition;
        float phaseX = Random.Range(0f, Mathf.PI * 2f);
        float phaseY = Random.Range(0f, Mathf.PI * 2f);
        float speedX = Random.Range(0.15f, 0.35f);
        float speedY = Random.Range(0.2f, 0.5f);
        float ampX = Random.Range(15f, 40f);
        float ampY = Random.Range(8f, 20f);

        while (rt != null && !cloudsScattering)
        {
            float t = Time.time;
            float x = basePos.x + Mathf.Sin(t * speedX + phaseX) * ampX;
            float y = basePos.y + Mathf.Sin(t * speedY + phaseY) * ampY;
            rt.anchoredPosition = new Vector2(x, y);
            yield return null;
        }
    }

    void StartCloudScatterThenAction(System.Action onComplete)
    {
        StartCoroutine(ScatterCloudsAndStartMode(onComplete));
    }

    IEnumerator ScatterCloudsAndStartMode(System.Action onComplete)
    {
        cloudsScattering = true;

        // ボタンを無効化（二重クリック防止）
        var buttons = titlePanel.GetComponentsInChildren<Button>();
        foreach (var btn in buttons)
            btn.interactable = false;

        float duration = 1.0f;
        float elapsed = 0f;

        // 雲の初期位置と飛ぶ方向を記録
        var startPositions = new List<Vector2>();
        var targetOffsets = new List<Vector2>();
        var startAlphas = new List<float>();

        foreach (var rt in titleClouds)
        {
            if (rt == null) continue;
            startPositions.Add(rt.anchoredPosition);
            float dir = rt.anchoredPosition.x < 0 ? -1f : 1f;
            float xOff = dir * Random.Range(800f, 1400f);
            float yOff = Random.Range(-100f, 100f);
            targetOffsets.Add(new Vector2(xOff, yOff));
            var img = rt.GetComponent<Image>();
            startAlphas.Add(img != null ? img.color.a : 0.7f);
        }

        // ロゴの初期状態を記録
        Vector2 logoStartPos = titleLogoRT != null ? titleLogoRT.anchoredPosition : Vector2.zero;
        Vector3 logoStartScale = titleLogoRT != null ? titleLogoRT.localScale : Vector3.one;

        // ボタンの初期位置を記録 + CanvasGroup追加
        var btnRTs = new List<RectTransform>();
        var btnStartPositions = new List<Vector2>();
        var btnCanvasGroups = new List<CanvasGroup>();
        foreach (var btn in buttons)
        {
            var btnRT = btn.GetComponent<RectTransform>();
            btnRTs.Add(btnRT);
            btnStartPositions.Add(btnRT.anchoredPosition);
            var cg = btn.gameObject.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            btnCanvasGroups.Add(cg);
        }

        // タイトル背景の初期色
        var titleBg = titlePanel.GetComponent<Image>();
        Color titleBgStartColor = titleBg != null ? titleBg.color : Color.clear;

        // 雲の解散 + ロゴ上昇 + ボタン下方スライド + 背景フェードアウト
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * t; // EaseInQuad

            // 雲を左右に散らす
            for (int i = 0; i < titleClouds.Count; i++)
            {
                if (titleClouds[i] == null) continue;
                titleClouds[i].anchoredPosition = startPositions[i] + targetOffsets[i] * ease;
                var img = titleClouds[i].GetComponent<Image>();
                if (img != null)
                    img.color = new Color(1f, 1f, 1f, startAlphas[i] * (1f - t));
            }

            // ロゴ: 上に移動 + 拡大 + フェードアウト
            if (titleLogoRT != null)
            {
                float logoEase = Mathf.SmoothStep(0f, 1f, t);
                titleLogoRT.anchoredPosition = logoStartPos + new Vector2(0, 200f * logoEase);
                float s = 1f + 0.4f * logoEase;
                titleLogoRT.localScale = new Vector3(s, s, 1f);
                if (titleLogoCanvasGroup != null)
                    titleLogoCanvasGroup.alpha = 1f - logoEase;
            }

            // ボタン: 下にスライド + フェードアウト
            float btnEase = Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < btnRTs.Count; i++)
            {
                btnRTs[i].anchoredPosition = btnStartPositions[i] + new Vector2(0, -200f * btnEase);
                btnCanvasGroups[i].alpha = 1f - btnEase;
            }

            // 背景: フェードアウト
            if (titleBg != null)
            {
                titleBg.color = new Color(titleBgStartColor.r, titleBgStartColor.g, titleBgStartColor.b,
                    titleBgStartColor.a * (1f - t));
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 雲を破棄
        foreach (var rt in titleClouds)
            if (rt != null) Destroy(rt.gameObject);
        titleClouds.Clear();
        cloudsScattering = false;

        // モード開始（認証画面表示 or ゲーム開始）
        onComplete?.Invoke();
    }

    IEnumerator FadeInCanvasGroup(CanvasGroup cg, float duration)
    {
        cg.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    Sprite LoadCloudSprite(int index)
    {
#if UNITY_EDITOR
        string path = $"Assets/Tiny Swords/Terrain/Decorations/Clouds/Clouds_0{index}.png";
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != UnityEditor.TextureImporterType.Sprite)
            { importer.textureType = UnityEditor.TextureImporterType.Sprite; changed = true; }
            if (changed) importer.SaveAndReimport();
        }
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return Resources.Load<Sprite>($"Cloud_{index}");
#endif
    }

    void CreateHUD()
    {
        var topBar = MakePanel("TopBar", mainCanvas.transform,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1f),
            Vector2.zero, new Vector2(0, 60),
            new Color(0.1f, 0.1f, 0.15f, 0.85f));
        topBarGo = topBar.gameObject;
        topBarGo.SetActive(false); // タイトル画面では非表示

        waveText = MakeText("WaveText", topBar, "Wave 1", 28,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(20, 0), new Vector2(250, 40), TextAnchor.MiddleLeft);

        // Wave +1/+10 調整ボタン（waveTextの右隣、準備フェーズのみ表示）
        {
            var container = new GameObject("WaveAdjustBtns");
            var crt = container.AddComponent<RectTransform>();
            crt.SetParent(topBar, false);
            crt.anchorMin = new Vector2(0, 0.5f);
            crt.anchorMax = new Vector2(0, 0.5f);
            crt.pivot = new Vector2(0, 0.5f);
            crt.anchoredPosition = new Vector2(170, 0);
            crt.sizeDelta = new Vector2(90, 36);
            waveAdjustBtns = container;

            // [+1] ボタン
            CreateTopBarWaveBtn(crt, "+1", 0f, 1);
            // [+10] ボタン
            CreateTopBarWaveBtn(crt, "+10", 45f, 10);

            container.SetActive(false);
        }

        // 戦力ゲージ（味方:青 vs 敵:赤 の比率バー）
        var gaugeBg = MakePanel("ForceGaugeBg", topBar,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(260, 0), new Vector2(300, 32),
            new Color(0.05f, 0.05f, 0.1f, 0.9f));

        // 味方（左から伸びる青バー）
        var allyBar = MakePanel("AllyBar", gaugeBg,
            new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero,
            new Color(0.2f, 0.6f, 1f, 0.9f));
        forceGaugeAlly = allyBar.GetComponent<Image>();

        // 敵（右から伸びる赤バー）
        var enemyBar = MakePanel("EnemyBar", gaugeBg,
            new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
            Vector2.zero, Vector2.zero,
            new Color(1f, 0.3f, 0.2f, 0.9f));
        forceGaugeEnemy = enemyBar.GetComponent<Image>();

        // 味方ラベル（ゲージ左端）
        var allyLabel = MakeText("AllyLabel", gaugeBg, "みかた", 22,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(6, 0), new Vector2(70, 32), TextAnchor.MiddleLeft);
        allyLabel.color = new Color(0.7f, 0.9f, 1f);
        allyLabel.fontStyle = FontStyle.Bold;
        var alLblOutline = allyLabel.gameObject.AddComponent<Outline>();
        alLblOutline.effectColor = new Color(0, 0, 0, 1f);
        alLblOutline.effectDistance = new Vector2(2, -2);

        // 敵ラベル（ゲージ右端）
        var enemyLabel = MakeText("EnemyLabel", gaugeBg, "てき", 22,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-6, 0), new Vector2(50, 32), TextAnchor.MiddleRight);
        enemyLabel.color = new Color(1f, 0.7f, 0.7f);
        enemyLabel.fontStyle = FontStyle.Bold;
        var enLblOutline = enemyLabel.gameObject.AddComponent<Outline>();
        enLblOutline.effectColor = new Color(0, 0, 0, 1f);
        enLblOutline.effectDistance = new Vector2(2, -2);

        // ランキング横並びコンテナ（兵士の右隣）
        var rankGo = new GameObject("RankingBar");
        rankingBar = rankGo.AddComponent<RectTransform>();
        rankingBar.SetParent(topBar.transform, false);
        rankingBar.anchorMin = new Vector2(0, 0);
        rankingBar.anchorMax = new Vector2(1, 1);
        rankingBar.pivot = new Vector2(0, 0.5f);
        rankingBar.offsetMin = new Vector2(700, 0);
        rankingBar.offsetMax = new Vector2(-200, 0);

        // いいねカウンター
        likeCountText = MakeText("LikeCount", topBar, "", 20,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-335, 0), new Vector2(150, 40), TextAnchor.MiddleRight);
        likeCountText.color = new Color(1f, 0.5f, 0.7f);

        // ゴールド表示（Scoreの左、重ならないよう配置）
        goldText = MakeText("GoldText", topBar, "", 24,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-230, 0), new Vector2(100, 40), TextAnchor.MiddleRight);
        goldText.color = new Color(1f, 0.85f, 0.2f);

        scoreText = MakeText("Score", topBar, "Score: 0", 28,
            new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f),
            new Vector2(-65, 0), new Vector2(160, 40), TextAnchor.MiddleRight);
    }

    void CreateWaveStartButton()
    {
        var go = new GameObject("WaveStartButton");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 310);
        rt.sizeDelta = new Vector2(300, 80);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 0.2f, 0.95f);

        waveStartButton = go.AddComponent<Button>();
        waveStartButton.targetGraphic = img;
        var wsCB = waveStartButton.colors;
        wsCB.normalColor = new Color(0.2f, 0.6f, 0.2f, 0.95f);
        wsCB.highlightedColor = new Color(0.25f, 0.7f, 0.25f, 1f);
        wsCB.pressedColor = new Color(0.15f, 0.45f, 0.15f, 1f);
        waveStartButton.colors = wsCB;
        waveStartButton.onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.StartWave();
        });

        var txt = MakeText("Text", rt, "スタート", 32,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        txt.fontStyle = FontStyle.Bold;
        var wsOutline = txt.gameObject.AddComponent<Outline>();
        wsOutline.effectColor = new Color(0, 0, 0, 1f);
        wsOutline.effectDistance = new Vector2(2, -2);
        var wsShadow1 = txt.gameObject.AddComponent<Shadow>();
        wsShadow1.effectColor = new Color(0, 0, 0, 1f);
        wsShadow1.effectDistance = new Vector2(-2, 2);
        var wsShadow2 = txt.gameObject.AddComponent<Shadow>();
        wsShadow2.effectColor = new Color(0, 0, 0, 0.8f);
        wsShadow2.effectDistance = new Vector2(1, 1);
    }

    /// <summary>WaveStartとRandomPlaceを横並び中央揃えに配置</summary>
    void CenterActionButtons(bool showRandom)
    {
        if (waveStartButton == null) return;
        float btnY = 310f; // ショップパネルのうえ
        var wsRT = waveStartButton.GetComponent<RectTransform>();
        if (showRandom && randomPlaceButton != null)
        {
            // 両方表示: 300 + 20gap + 280 = 600 → 各々中央から左右にオフセット
            wsRT.anchoredPosition = new Vector2(-150, btnY);
            var rpRT = randomPlaceButton.GetComponent<RectTransform>();
            rpRT.anchoredPosition = new Vector2(160, btnY);
        }
        else
        {
            // WaveStartのみ: 中央
            wsRT.anchoredPosition = new Vector2(0, btnY);
        }
    }

    void CreateRandomPlaceButton()
    {
        var go = new GameObject("RandomPlaceButton");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(160, 310);
        rt.sizeDelta = new Vector2(280, 80);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.3f, 0.45f, 0.7f, 0.95f);

        randomPlaceButton = go.AddComponent<Button>();
        randomPlaceButton.targetGraphic = img;
        var rpCB = randomPlaceButton.colors;
        rpCB.normalColor = new Color(0.3f, 0.45f, 0.7f, 0.95f);
        rpCB.highlightedColor = new Color(0.35f, 0.55f, 0.8f, 1f);
        rpCB.pressedColor = new Color(0.2f, 0.35f, 0.55f, 1f);
        randomPlaceButton.colors = rpCB;
        randomPlaceButton.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.PlaceAllQueueRandom();
            }
        });

        var btnTxt = MakeText("Text", rt, "まとめてはいち", 28,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        btnTxt.font = GetFont();
        btnTxt.fontStyle = FontStyle.Bold;
        var rpOutline = btnTxt.gameObject.AddComponent<Outline>();
        rpOutline.effectColor = new Color(0, 0, 0, 1f);
        rpOutline.effectDistance = new Vector2(2, -2);
        var rpShadow1 = btnTxt.gameObject.AddComponent<Shadow>();
        rpShadow1.effectColor = new Color(0, 0, 0, 1f);
        rpShadow1.effectDistance = new Vector2(-2, 2);
        var rpShadow2 = btnTxt.gameObject.AddComponent<Shadow>();
        rpShadow2.effectColor = new Color(0, 0, 0, 0.8f);
        rpShadow2.effectDistance = new Vector2(1, 1);
    }


    void CreateTopBarWaveBtn(RectTransform parent, string label, float xOffset, int delta)
    {
        var go = new GameObject($"WaveBtn{label}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0.5f);
        rt.anchorMax = new Vector2(0, 0.5f);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(xOffset, 0);
        rt.sizeDelta = new Vector2(42, 30);

        var img = go.AddComponent<Image>();
        img.color = delta >= 10 ? new Color(0.5f, 0.35f, 0.2f, 0.95f) : new Color(0.3f, 0.5f, 0.3f, 0.95f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            if (GameManager.Instance == null) return;
            GameManager.Instance.currentWaveIndex = Mathf.Max(0, GameManager.Instance.currentWaveIndex + delta);
            if (waveText != null)
                waveText.text = $"Wave {GameManager.Instance.currentWaveIndex + 1}";
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        });

        var txt = MakeText("T", rt, label, 16,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        txt.fontStyle = FontStyle.Bold;
        var ol = txt.gameObject.AddComponent<Outline>();
        ol.effectColor = Color.black;
        ol.effectDistance = new Vector2(1, -1);
    }

    void CreateShopPanel()
    {
        // ショップパネル（がめんしたちゅうおう、キューパネルのうえ）
        var panel = new GameObject("ShopPanel");
        var rt = panel.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0);
        rt.anchorMax = new Vector2(0.5f, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = new Vector2(0, 195);
        rt.sizeDelta = new Vector2(820, 80);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.10f, 0.20f, 0.90f);

        shopPanel = panel;
        shopPanel.SetActive(false);

        // 5つのユニットボタン（2行表示: なまえ + きんがく）
        var types = new[] { UnitType.Warrior, UnitType.Lancer, UnitType.Archer, UnitType.Monk, UnitType.Mage };
        var labels = new[] { "せんし", "やり", "ゆみ", "かいふく", "まほう" };
        float btnW = 150f;
        float gap = 8f;
        float totalW = types.Length * btnW + (types.Length - 1) * gap;
        float startX = -totalW / 2f + btnW / 2f;

        for (int i = 0; i < types.Length; i++)
        {
            int idx = i;
            UnitType unitType = types[i];
            int cost = GameConfig.GetUnitCost(unitType);

            var btnGo = new GameObject($"ShopBtn_{types[i]}");
            var brt = btnGo.AddComponent<RectTransform>();
            brt.SetParent(rt, false);
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(startX + i * (btnW + gap), 0);
            brt.sizeDelta = new Vector2(btnW, 68);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.25f, 0.4f, 0.6f, 0.95f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var cb = btn.colors;
            cb.normalColor = new Color(0.25f, 0.4f, 0.6f, 0.95f);
            cb.highlightedColor = new Color(0.3f, 0.5f, 0.7f, 1f);
            cb.pressedColor = new Color(0.18f, 0.3f, 0.45f, 1f);
            cb.disabledColor = new Color(0.2f, 0.2f, 0.25f, 0.7f);
            btn.colors = cb;

            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                var gm = GameManager.Instance;
                if (gm != null && !gm.BuyUnit(unitType))
                {
                    // ゴールド不足はグレーアウトで防ぐので、ここに来るのは上限超過
                    if (gm.gold >= GameConfig.GetUnitCost(unitType))
                        ShowAnnouncement($"<color=#FF6644>NPCは{GameConfig.MaxAllyUnits}体未満までです！</color>", 1.5f, 36);
                }
            });

            // 上段: ユニット名（白、大きめ）
            var nameTxt = MakeText("NameText", brt, labels[i], 32,
                new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.color = Color.white;
            // 4方向アウトライン（黒枠をしっかり出す）
            for (int d = 0; d < 4; d++)
            {
                var ol = nameTxt.gameObject.AddComponent<Shadow>();
                ol.effectColor = Color.black;
                float dx = (d == 0) ? 3 : (d == 1) ? -3 : 0;
                float dy = (d == 2) ? 3 : (d == 3) ? -3 : 0;
                ol.effectDistance = new Vector2(dx, dy);
            }

            // 下段: コスト（ゴールド色、やや小さめ）
            var costTxt = MakeText("CostText", brt, $"{cost} G", 26,
                new Vector2(0, 0), new Vector2(1, 0.45f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            costTxt.fontStyle = FontStyle.Bold;
            costTxt.color = new Color(1f, 0.85f, 0.2f);
            for (int d = 0; d < 4; d++)
            {
                var ol = costTxt.gameObject.AddComponent<Shadow>();
                ol.effectColor = Color.black;
                float dx = (d == 0) ? 2 : (d == 1) ? -2 : 0;
                float dy = (d == 2) ? 2 : (d == 3) ? -2 : 0;
                ol.effectDistance = new Vector2(dx, dy);
            }

            shopButtons.Add(btn);
        }
    }

    void CreateNPCFormationPanel()
    {
        npcFormationPanel = new GameObject("NPCFormationPanel");
        var rt = npcFormationPanel.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        // ショップパネルの左横に配置
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(10, 200);
        rt.sizeDelta = new Vector2(80, 110);

        var bg = npcFormationPanel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.18f, 0.85f);

        var labels = new[] { "ぜんえい", "こうえい" };
        var stances = new[] { UnitStance.Attack, UnitStance.Defend };
        var colors = new[] {
            new Color(0.7f, 0.2f, 0.2f, 0.95f),
            new Color(0.2f, 0.4f, 0.7f, 0.95f)
        };

        for (int i = 0; i < 2; i++)
        {
            int idx = i;
            var btnGo = new GameObject($"FormBtn_{labels[i]}");
            var brt = btnGo.AddComponent<RectTransform>();
            brt.SetParent(rt, false);
            brt.anchorMin = new Vector2(0, 1);
            brt.anchorMax = new Vector2(1, 1);
            brt.pivot = new Vector2(0.5f, 1);
            brt.anchoredPosition = new Vector2(0, -5 - i * 52);
            brt.sizeDelta = new Vector2(-10, 48);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = colors[i];

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var cb = btn.colors;
            cb.normalColor = colors[i];
            cb.highlightedColor = colors[i] + new Color(0.1f, 0.1f, 0.1f, 0);
            cb.pressedColor = colors[i] * 0.7f;
            btn.colors = cb;
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                GameManager.Instance?.SetNPCFormation(stances[idx]);
            });

            var txt = MakeText("Text", brt, labels[i], 20,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            txt.fontStyle = FontStyle.Bold;
            var ol = txt.gameObject.AddComponent<Outline>();
            ol.effectColor = Color.black;
            ol.effectDistance = new Vector2(1, -1);
        }

        // 常時表示（Title以外）
    }

    void CreateQueuePanel()
    {
        var panel = MakePanel("QueuePanel", mainCanvas.transform,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0f),
            Vector2.zero, new Vector2(0, 195),
            new Color(0.1f, 0.1f, 0.2f, 0.85f));

        queuePanel = panel.GetComponent<RectTransform>();

        MakeText("QueueLabel", panel, "Unit Queue:", 20,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(10, -5), new Vector2(200, 25), TextAnchor.MiddleLeft);

        // タイトル画面では非表示（OnPhaseChangedで表示制御）
        panel.gameObject.SetActive(false);
    }

    void CreateKillLog()
    {
        killLogText = MakeText("KillLog", mainCanvas.transform, "", 16,
            new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-10, -70), new Vector2(400, 150), TextAnchor.UpperRight);

        killLogText.color = new Color(1, 1, 0.8f);
    }

    void CreateAnnouncement()
    {
        phaseAnnouncementText = MakeText("Announcement", mainCanvas.transform, "", 64,
            new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(800, 120), TextAnchor.MiddleCenter);

        phaseAnnouncementText.fontStyle = FontStyle.Bold;
        phaseAnnouncementText.horizontalOverflow = HorizontalWrapMode.Overflow;
        // Outline for readability
        var outline = phaseAnnouncementText.gameObject.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        phaseAnnouncementText.gameObject.SetActive(false);
    }

    void CreateResultPanel()
    {
        resultPanel = new GameObject("ResultPanel");
        var rt = resultPanel.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var bg = resultPanel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.85f);

        // スクロール用コンテナ（画面下から上へ流れる）
        scrollContent = new GameObject("ScrollContent").AddComponent<RectTransform>();
        scrollContent.SetParent(rt, false);
        scrollContent.anchorMin = new Vector2(0.5f, 0);
        scrollContent.anchorMax = new Vector2(0.5f, 0);
        scrollContent.pivot = new Vector2(0.5f, 0);
        scrollContent.sizeDelta = new Vector2(900, 0); // 高さはShowResultで設定
        scrollContent.anchoredPosition = Vector2.zero;

        // リザルトテキスト（互換用に残す、スクロール内の先頭テキストとして使う）
        resultText = null;

        // Play Againボタン（最前面、固定位置）
        var btnGo = new GameObject("BackButton");
        var brt = btnGo.AddComponent<RectTransform>();
        brt.SetParent(rt, false);
        brt.anchorMin = new Vector2(0.5f, 0.03f);
        brt.anchorMax = new Vector2(0.5f, 0.03f);
        brt.pivot = new Vector2(0.5f, 0);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(250, 55);
        btnGo.AddComponent<Image>().color = new Color(0.2f, 0.5f, 0.8f, 1f);
        var bbtn = btnGo.AddComponent<Button>();
        bbtn.onClick.AddListener(() => {
            isScrolling = false;
            GameManager.Instance?.ResetGame();
            resultPanel.SetActive(false);
        });

        MakeText("BtnText", brt, "もういちどプレイ", 22,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        resultPanel.SetActive(false);
    }

    void UpdateResultScroll()
    {
        if (!isScrolling || scrollContent == null) return;
        scrollContent.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;
    }

    void CreateDebugPanel()
    {
        // --- DBGトグルボタン (TopBar内・ハンバーガーの左隣) ---
        debugToggleBtn = new GameObject("DebugToggleBtn");
        var btnRT = debugToggleBtn.AddComponent<RectTransform>();
        btnRT.SetParent(topBarGo.transform, false);
        btnRT.anchorMin = new Vector2(1, 0);
        btnRT.anchorMax = new Vector2(1, 1);
        btnRT.pivot = new Vector2(1, 0.5f);
        btnRT.anchoredPosition = new Vector2(-65, 0);
        btnRT.sizeDelta = new Vector2(55, 0);
        var btnImg = debugToggleBtn.AddComponent<Image>();
        btnImg.color = new Color(0.5f, 0.15f, 0.15f, 0.8f);
        var btn = debugToggleBtn.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var cb = btn.colors;
        cb.normalColor = new Color(0.5f, 0.15f, 0.15f, 0.8f);
        cb.highlightedColor = new Color(0.65f, 0.2f, 0.2f, 0.9f);
        cb.pressedColor = new Color(0.3f, 0.1f, 0.1f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(ToggleDebugPanel);
        MakeText("Text", btnRT, "DBG", 16,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        debugToggleBtn.SetActive(false);

        // --- 統合デバッグパネル (左サイド, スクロール可能) ---
        debugPanel = new GameObject("DebugPanel");
        var panelRT = debugPanel.AddComponent<RectTransform>();
        panelRT.SetParent(mainCanvas.transform, false);
        panelRT.anchorMin = new Vector2(0, 0);
        panelRT.anchorMax = new Vector2(0, 1);
        panelRT.pivot = new Vector2(0, 1);
        panelRT.anchoredPosition = new Vector2(0, -60);
        panelRT.sizeDelta = new Vector2(500, -60);
        debugPanel.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.12f, 0.85f);
        debugPanel.AddComponent<Mask>().showMaskGraphic = true;
        var scroll = debugPanel.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40f;

        var contentGo = new GameObject("Content");
        debugScrollContent = contentGo.AddComponent<RectTransform>();
        debugScrollContent.SetParent(panelRT, false);
        debugScrollContent.anchorMin = new Vector2(0, 1);
        debugScrollContent.anchorMax = new Vector2(1, 1);
        debugScrollContent.pivot = new Vector2(0, 1);
        debugScrollContent.anchoredPosition = Vector2.zero;
        scroll.content = debugScrollContent;

        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 3;
        vlg.padding = new RectOffset(5, 5, 5, 5);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        var csf = contentGo.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // セクション作成
        CreateTestModePanel();
        CreateLevelDebugPanel();
        CreateYouTubeDebugPanel();
        CreateTikTokDebugPanel();

        // チュートリアル操作ボタン
        {
            // リセット
            var resetGo = new GameObject("TutorialResetBtn");
            var resetRT = resetGo.AddComponent<RectTransform>();
            resetRT.SetParent(debugScrollContent, false);
            resetGo.AddComponent<LayoutElement>().preferredHeight = 30;
            resetGo.AddComponent<Image>().color = new Color(0.4f, 0.3f, 0.15f, 0.9f);
            resetGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                ResetTutorialFlag();
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            });
            MakeText("Text", resetRT, "Tutorial Reset", 14,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

            // 位置エディター
            var editGo = new GameObject("TutorialEditBtn");
            var editRT = editGo.AddComponent<RectTransform>();
            editRT.SetParent(debugScrollContent, false);
            editGo.AddComponent<LayoutElement>().preferredHeight = 30;
            editGo.AddComponent<Image>().color = new Color(0.15f, 0.3f, 0.4f, 0.9f);
            editGo.AddComponent<Button>().onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                debugPanel.SetActive(false);
                StartTutorialEditor();
            });
            MakeText("Text", editRT, "Tutorial Edit Pos", 14,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        }

        debugPanel.SetActive(false);
    }

    void AddDebugSectionHeader(string title, Color bgColor)
    {
        var go = new GameObject($"Header_{title}");
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(debugScrollContent, false);
        go.AddComponent<LayoutElement>().preferredHeight = 22;
        go.AddComponent<Image>().color = bgColor;
        MakeText("Title", rt, title, 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter).color = Color.white;
    }

    void ToggleDebugPanel()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        if (debugPanel != null)
            debugPanel.SetActive(!debugPanel.activeSelf);
    }

    void CreateTestModePanel()
    {
        AddDebugSectionHeader("Test Mode", new Color(0.2f, 0.2f, 0.3f, 1f));
        var panel = MakePanel("TestPanel", debugScrollContent,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            Vector2.zero, new Vector2(0, 135),
            new Color(0.15f, 0.15f, 0.2f, 0.9f));
        testModePanel = panel.gameObject;
        testModePanel.AddComponent<LayoutElement>().preferredHeight = 135;
        var panelRT = panel.GetComponent<RectTransform>();

        // ロール別ボタン（上段: ユニット追加）
        string[] roleLabels = { "\u5263", "\u69CD", "\u5F13", "\u56DE\u5FA9", "\u9B54\u6CD5" };
        UnitType[] roleTypes = { UnitType.Warrior, UnitType.Lancer, UnitType.Archer, UnitType.Monk, UnitType.Mage };
        Color[] roleColors = {
            new Color(0.3f, 0.45f, 0.7f, 1f),
            new Color(0.2f, 0.55f, 0.55f, 1f),
            new Color(0.7f, 0.55f, 0.15f, 1f),
            new Color(0.25f, 0.6f, 0.3f, 1f),
            new Color(0.5f, 0.2f, 0.6f, 1f)
        };
        float btnWidth = 50f;
        float topRowY = 18f;
        for (int i = 0; i < roleLabels.Length; i++)
        {
            var btnGo = new GameObject($"Add_{roleLabels[i]}");
            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.SetParent(panelRT, false);
            btnRT.anchorMin = new Vector2(0, 0.5f);
            btnRT.anchorMax = new Vector2(0, 0.5f);
            btnRT.pivot = new Vector2(0, 0.5f);
            btnRT.anchoredPosition = new Vector2(10 + i * (btnWidth + 4), topRowY);
            btnRT.sizeDelta = new Vector2(btnWidth, 35);
            btnGo.AddComponent<Image>().color = roleColors[i];
            UnitType capturedType = roleTypes[i];
            btnGo.AddComponent<Button>().onClick.AddListener(() => OnTestAddUnit(capturedType));
            MakeText("Text", btnRT, roleLabels[i], 16,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        }

        // 必殺技ボタン（下段）
        string[] ultLabels = { "\u5263\u2605", "\u69CD\u2605", "\u5F13\u2605", "\u56DE\u2605", "\u9B54\u2605", "\u9A0E\u2605" };
        UnitType[] ultTypes = { UnitType.Warrior, UnitType.Lancer, UnitType.Archer, UnitType.Monk, UnitType.Mage, UnitType.Knight };
        float botRowY = -22f;
        for (int i = 0; i < ultLabels.Length; i++)
        {
            var ultGo = new GameObject($"Ult_{ultLabels[i]}");
            var ultRT = ultGo.AddComponent<RectTransform>();
            ultRT.SetParent(panelRT, false);
            ultRT.anchorMin = new Vector2(0, 0.5f);
            ultRT.anchorMax = new Vector2(0, 0.5f);
            ultRT.pivot = new Vector2(0, 0.5f);
            ultRT.anchoredPosition = new Vector2(10 + i * (btnWidth + 4), botRowY);
            ultRT.sizeDelta = new Vector2(btnWidth, 30);
            ultGo.AddComponent<Image>().color = new Color(0.6f, 0.25f, 0.15f, 1f);
            UnitType capturedType = ultTypes[i];
            ultGo.AddComponent<Button>().onClick.AddListener(() => DebugTriggerUltimate(capturedType));
            MakeText("Text", ultRT, ultLabels[i], 14,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        }

        var togGo = new GameObject("AutoToggle");
        var togrt = togGo.AddComponent<RectTransform>();
        togrt.SetParent(panelRT, false);
        togrt.anchorMin = new Vector2(0, 0.5f);
        togrt.anchorMax = new Vector2(0, 0.5f);
        togrt.pivot = new Vector2(0, 0.5f);
        togrt.anchoredPosition = new Vector2(340, topRowY);
        togrt.sizeDelta = new Vector2(130, 35);

        var togBg = togGo.AddComponent<Image>();
        togBg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
        autoSpawnToggle = togGo.AddComponent<Toggle>();
        autoSpawnToggle.targetGraphic = togBg;

        var checkGo = new GameObject("Checkmark");
        var chkrt = checkGo.AddComponent<RectTransform>();
        chkrt.SetParent(togrt, false);
        chkrt.anchorMin = Vector2.zero;
        chkrt.anchorMax = Vector2.one;
        chkrt.sizeDelta = Vector2.zero;
        chkrt.anchoredPosition = Vector2.zero;
        var chkTxt = checkGo.AddComponent<Text>();
        chkTxt.font = GetFont();
        chkTxt.text = "Auto Spawn";
        chkTxt.fontSize = 16;
        chkTxt.alignment = TextAnchor.MiddleCenter;
        chkTxt.color = Color.green;
        autoSpawnToggle.graphic = chkTxt;

        // ─── 武器選択行（3行目） ─────────────────────────
        float weaponRowY = -55f;
        allWeaponNames = PixelHeroFactory.GetAllWeaponNames();
        string savedWeapon = PlayerPrefs.GetString("SpearWeapon", "Longsword");
        currentWeaponIndex = 0;
        for (int i = 0; i < allWeaponNames.Length; i++)
        {
            if (allWeaponNames[i] == savedWeapon) { currentWeaponIndex = i; break; }
        }

        // ← ボタン
        var prevGo = new GameObject("WeaponPrev");
        var prevRT = prevGo.AddComponent<RectTransform>();
        prevRT.SetParent(panelRT, false);
        prevRT.anchorMin = new Vector2(0, 0.5f);
        prevRT.anchorMax = new Vector2(0, 0.5f);
        prevRT.pivot = new Vector2(0, 0.5f);
        prevRT.anchoredPosition = new Vector2(10, weaponRowY);
        prevRT.sizeDelta = new Vector2(30, 28);
        prevGo.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 1f);
        prevGo.AddComponent<Button>().onClick.AddListener(OnWeaponPrev);
        MakeText("Text", prevRT, "\u25C0", 16,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // 武器名テキスト
        var nameGo = new GameObject("WeaponName");
        var nameRT = nameGo.AddComponent<RectTransform>();
        nameRT.SetParent(panelRT, false);
        nameRT.anchorMin = new Vector2(0, 0.5f);
        nameRT.anchorMax = new Vector2(0, 0.5f);
        nameRT.pivot = new Vector2(0, 0.5f);
        nameRT.anchoredPosition = new Vector2(44, weaponRowY);
        nameRT.sizeDelta = new Vector2(200, 28);
        nameGo.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.18f, 1f);
        weaponNameText = MakeText("Text", nameRT,
            allWeaponNames.Length > 0 ? allWeaponNames[currentWeaponIndex] : "---", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // → ボタン
        var nextGo = new GameObject("WeaponNext");
        var nextRT = nextGo.AddComponent<RectTransform>();
        nextRT.SetParent(panelRT, false);
        nextRT.anchorMin = new Vector2(0, 0.5f);
        nextRT.anchorMax = new Vector2(0, 0.5f);
        nextRT.pivot = new Vector2(0, 0.5f);
        nextRT.anchoredPosition = new Vector2(248, weaponRowY);
        nextRT.sizeDelta = new Vector2(30, 28);
        nextGo.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 1f);
        nextGo.AddComponent<Button>().onClick.AddListener(OnWeaponNext);
        MakeText("Text", nextRT, "\u25B6", 16,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // 槍ラベル
        MakeText("SpearLabel", panelRT, "\u69CD\u9078\u629E:", 12,
            new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(290, weaponRowY), new Vector2(0, 28), TextAnchor.MiddleLeft);
    }

    void CreateLevelDebugPanel()
    {
        var panel = MakePanel("LevelDebugPanel", debugScrollContent,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            Vector2.zero, new Vector2(0, 240),
            new Color(0.1f, 0.12f, 0.18f, 0.92f));
        levelDebugPanel = panel.gameObject;
        levelDebugPanel.AddComponent<LayoutElement>().preferredHeight = 240;
        var pRT = panel.GetComponent<RectTransform>();

        // タイトル
        MakeText("Title", pRT, "Level & Size Debug", 16,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -5), new Vector2(0, 25), TextAnchor.MiddleCenter).color = Color.yellow;

        // 情報ラベル
        var label = MakeText("Info", pRT, "", 14,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(10, -30), new Vector2(-10, 50), TextAnchor.UpperLeft);
        label.color = Color.white;
        levelDebugLabel = label;

        float btnY = -85f;
        float btnW = 55f;

        // レベル調整ボタン
        MakeText("LvLabel", pRT, "Level:", 14,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(10, btnY), new Vector2(50, 25), TextAnchor.MiddleLeft).color = Color.white;

        string[] lvLabels = { "-10", "-1", "+1", "+10", "+50" };
        int[] lvDeltas = { -10, -1, 1, 10, 50 };
        for (int i = 0; i < lvLabels.Length; i++)
        {
            int delta = lvDeltas[i];
            var b = MakeDebugButton(pRT, lvLabels[i], new Vector2(65 + i * (btnW + 3), btnY), new Vector2(btnW, 25));
            b.onClick.AddListener(() => DebugAdjustLevel(delta));
        }

        // MaxSize 調整
        float row2Y = btnY - 30f;
        MakeText("MsLabel", pRT, "MaxSize:", 14,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(10, row2Y), new Vector2(70, 25), TextAnchor.MiddleLeft).color = Color.white;

        string[] msLabels = { "-1.0", "-0.5", "+0.5", "+1.0" };
        float[] msDeltas = { -1.0f, -0.5f, 0.5f, 1.0f };
        for (int i = 0; i < msLabels.Length; i++)
        {
            float delta = msDeltas[i];
            var b = MakeDebugButton(pRT, msLabels[i], new Vector2(80 + i * (68 + 3), row2Y), new Vector2(68, 25));
            b.onClick.AddListener(() => {
                GameConfig.MaxSizeMultiplier = Mathf.Clamp(GameConfig.MaxSizeMultiplier + delta, 1.0f, 20.0f);
                DebugRefreshPreview();
            });
        }

        // 到達Level 調整
        float row3Y = row2Y - 30f;
        MakeText("AlLabel", pRT, "AtLevel:", 14,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(10, row3Y), new Vector2(70, 25), TextAnchor.MiddleLeft).color = Color.white;

        string[] alLabels = { "-50", "-10", "+10", "+50" };
        int[] alDeltas = { -50, -10, 10, 50 };
        for (int i = 0; i < alLabels.Length; i++)
        {
            int delta = alDeltas[i];
            var b = MakeDebugButton(pRT, alLabels[i], new Vector2(80 + i * (68 + 3), row3Y), new Vector2(68, 25));
            b.onClick.AddListener(() => {
                GameConfig.MaxSizeAtLevel = Mathf.Clamp(GameConfig.MaxSizeAtLevel + delta, 2, 999);
                DebugRefreshPreview();
            });
        }

        // Saveボタン（現在の値をGameConfig.csに書き込む）
        float row4Y = row3Y - 35f;
        var saveBtn = MakeDebugButton(pRT, "SAVE TO CODE", new Vector2(10, row4Y), new Vector2(360, 30));
        saveBtn.GetComponent<Image>().color = new Color(0.15f, 0.45f, 0.2f, 1f);
        var sCb = saveBtn.colors;
        sCb.highlightedColor = new Color(0.2f, 0.55f, 0.25f);
        sCb.pressedColor = new Color(0.1f, 0.35f, 0.15f);
        saveBtn.colors = sCb;
        saveBtn.onClick.AddListener(() => DebugSaveToGameConfig());
    }

    Button MakeDebugButton(RectTransform parent, string label, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(label);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.AddComponent<Image>().color = new Color(0.25f, 0.3f, 0.4f, 1f);
        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.highlightedColor = new Color(0.35f, 0.4f, 0.55f);
        cb.pressedColor = new Color(0.15f, 0.2f, 0.3f);
        btn.colors = cb;
        MakeText("T", rt, label, 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        return btn;
    }

    void DebugAdjustLevel(int delta)
    {
        if (levelDebugPreviewUnit == null) DebugSpawnPreviewUnit();
        if (levelDebugPreviewUnit == null) return;

        int newLevel = Mathf.Clamp(levelDebugPreviewUnit.level + delta, 1, 999);
        // レベルリセットして再適用
        Destroy(levelDebugPreviewUnit.gameObject);
        levelDebugPreviewUnit = null;
        DebugSpawnPreviewUnit(newLevel);
    }

    void DebugSpawnPreviewUnit(int targetLevel = 1)
    {
        // 配置ゾーン中央にプレビュー用ユニット生成
        float cx = (GameConfig.PlacementZoneMinX + GameConfig.PlacementZoneMaxX) / 2f;
        float cy = (GameConfig.PlacementZoneMinY + GameConfig.PlacementZoneMaxY) / 2f;
        var pos = new Vector3(cx, cy, 0);

        var gm = GameManager.Instance;
        if (gm == null) return;

        var unit = gm.SpawnUnit(UnitType.Warrior, Team.Ally, "DebugPreview", pos);
        unit.level = 1;
        unit.originalScale = new Vector3(GameConfig.PixelHeroBaseScale, GameConfig.PixelHeroBaseScale, 1f);
        unit.transform.localScale = unit.originalScale;

        // 指定レベルまでサイズだけ成長させる
        float maxUnitScale = GameConfig.PixelHeroBaseScale * GameConfig.MaxSizeMultiplier;
        float sizeBoost = Mathf.Pow(GameConfig.LevelUpSizeBoost, targetLevel - 1);
        unit.originalScale *= sizeBoost;
        if (unit.originalScale.x > maxUnitScale)
            unit.originalScale = new Vector3(maxUnitScale, maxUnitScale, 1f);
        unit.transform.localScale = unit.originalScale;
        unit.level = targetLevel;
        unit.UpdateNameLabel();

        levelDebugPreviewUnit = unit;
        DebugUpdateLabel();
    }

    void DebugRefreshPreview()
    {
        if (levelDebugPreviewUnit == null) { DebugUpdateLabel(); return; }
        int lv = levelDebugPreviewUnit.level;
        Destroy(levelDebugPreviewUnit.gameObject);
        levelDebugPreviewUnit = null;
        DebugSpawnPreviewUnit(lv);
    }

    void DebugSaveToGameConfig()
    {
#if UNITY_EDITOR
        string path = "Assets/Scripts/KingsMarch/GameConfig.cs";
        string code = System.IO.File.ReadAllText(path);

        // MaxSizeMultiplier の値を置換
        code = System.Text.RegularExpressions.Regex.Replace(code,
            @"public static float MaxSizeMultiplier = [\d.]+f;",
            $"public static float MaxSizeMultiplier = {GameConfig.MaxSizeMultiplier:F1}f;");

        // MaxSizeAtLevel の値を置換
        code = System.Text.RegularExpressions.Regex.Replace(code,
            @"public static int MaxSizeAtLevel = \d+;",
            $"public static int MaxSizeAtLevel = {GameConfig.MaxSizeAtLevel};");

        System.IO.File.WriteAllText(path, code);
        UnityEditor.AssetDatabase.Refresh();

        Debug.Log($"[DebugSave] GameConfig.cs saved: MaxMult={GameConfig.MaxSizeMultiplier:F1}, AtLevel={GameConfig.MaxSizeAtLevel}");

        if (levelDebugLabel != null)
            levelDebugLabel.text += "\nSAVED!";
#else
        Debug.Log("[DebugSave] Save is only available in Editor.");
        if (levelDebugLabel != null)
            levelDebugLabel.text += "\n(Editor only)";
#endif
    }

    void DebugUpdateLabel()
    {
        if (levelDebugLabel == null) return;
        int lv = levelDebugPreviewUnit != null ? levelDebugPreviewUnit.level : 1;
        float scale = levelDebugPreviewUnit != null ? levelDebugPreviewUnit.transform.localScale.x : GameConfig.PixelHeroBaseScale;
        float maxScale = GameConfig.PixelHeroBaseScale * GameConfig.MaxSizeMultiplier;
        levelDebugLabel.text =
            $"Lv: {lv}  Scale: {scale:F3} / {maxScale:F3}\n" +
            $"MaxSize: {GameConfig.MaxSizeMultiplier:F1}x  AtLevel: {GameConfig.MaxSizeAtLevel}  (Boost: {GameConfig.LevelUpSizeBoost:F4})";
    }

    private InputField videoIdInput;
    private Button authButton;
    private Button connectButton;

    void CreateYouTubeStatusPanel()
    {
        var panel = MakePanel("YouTubePanel", mainCanvas.transform,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-10, 120), new Vector2(340, 160),
            new Color(0.1f, 0.1f, 0.15f, 0.92f));
        youtubePanel = panel.gameObject;
        var panelRT = panel.GetComponent<RectTransform>();

        youtubeStatusText = MakeText("YouTubeStatus", panelRT, "", 13,
            new Vector2(0, 0.45f), new Vector2(1, 1), new Vector2(0.5f, 1f),
            new Vector2(10, 0), Vector2.zero, TextAnchor.UpperLeft);

        youtubeStatusText.supportRichText = true;

        // Video ID input (legacy InputField)
        var inputGo = new GameObject("VideoIdInput");
        var inputRT = inputGo.AddComponent<RectTransform>();
        inputRT.SetParent(panelRT, false);
        inputRT.anchorMin = new Vector2(0, 0);
        inputRT.anchorMax = new Vector2(0.55f, 0);
        inputRT.pivot = new Vector2(0, 0);
        inputRT.anchoredPosition = new Vector2(8, 8);
        inputRT.sizeDelta = new Vector2(0, 28);
        var inputBg = inputGo.AddComponent<Image>();
        inputBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        videoIdInput = inputGo.AddComponent<InputField>();

        var inputTextGo = new GameObject("Text");
        var inputTextRT = inputTextGo.AddComponent<RectTransform>();
        inputTextRT.SetParent(inputRT, false);
        inputTextRT.anchorMin = Vector2.zero;
        inputTextRT.anchorMax = Vector2.one;
        inputTextRT.sizeDelta = new Vector2(-10, 0);
        inputTextRT.anchoredPosition = Vector2.zero;
        var inputTxt = inputTextGo.AddComponent<Text>();
        inputTxt.font = GetFont();
        inputTxt.fontSize = 13;
        inputTxt.color = Color.white;
        inputTxt.supportRichText = false;
        videoIdInput.textComponent = inputTxt;

        var phGo = new GameObject("Placeholder");
        var phRT = phGo.AddComponent<RectTransform>();
        phRT.SetParent(inputRT, false);
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.sizeDelta = new Vector2(-10, 0);
        phRT.anchoredPosition = Vector2.zero;
        var phTxt = phGo.AddComponent<Text>();
        phTxt.font = GetFont();
        phTxt.text = "Video ID...";
        phTxt.fontSize = 13;
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.color = new Color(0.5f, 0.5f, 0.5f);
        videoIdInput.placeholder = phTxt;

        // Auth button
        var authGo = new GameObject("AuthButton");
        var authRT = authGo.AddComponent<RectTransform>();
        authRT.SetParent(panelRT, false);
        authRT.anchorMin = new Vector2(0.57f, 0);
        authRT.anchorMax = new Vector2(1, 0);
        authRT.pivot = new Vector2(1, 0);
        authRT.anchoredPosition = new Vector2(-8, 8);
        authRT.sizeDelta = new Vector2(0, 28);
        authGo.AddComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);
        authButton = authGo.AddComponent<Button>();
        authButton.onClick.AddListener(OnAuthButtonClicked);
        MakeText("Text", authRT, "YouTubeにんしょう", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // Connect button
        var connGo = new GameObject("ConnectButton");
        var connRT = connGo.AddComponent<RectTransform>();
        connRT.SetParent(panelRT, false);
        connRT.anchorMin = new Vector2(0, 0);
        connRT.anchorMax = new Vector2(1, 0);
        connRT.pivot = new Vector2(0.5f, 0);
        connRT.anchoredPosition = new Vector2(0, 40);
        connRT.sizeDelta = new Vector2(-16, 24);
        connGo.AddComponent<Image>().color = new Color(0.2f, 0.5f, 0.7f, 1f);
        connectButton = connGo.AddComponent<Button>();
        connectButton.onClick.AddListener(OnConnectButtonClicked);
        MakeText("Text", connRT, "はいしんにせつぞく", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // Debug Start button (hidden during gameplay, shown during setup)
        var debugGo = new GameObject("YTDebugStartBtn");
        var debugRT = debugGo.AddComponent<RectTransform>();
        debugRT.SetParent(panelRT, false);
        debugRT.anchorMin = new Vector2(0, 0);
        debugRT.anchorMax = new Vector2(1, 0);
        debugRT.pivot = new Vector2(0.5f, 1f);
        debugRT.anchoredPosition = new Vector2(0, -8);
        debugRT.sizeDelta = new Vector2(-16, 36);
        debugGo.AddComponent<Image>().color = new Color(0.6f, 0.4f, 0.1f, 1f);
        debugGo.AddComponent<Button>().onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugStartGame();
        });
        MakeText("Text", debugRT, "デバッグでかいし (にんしょうスキップ)", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        ytDebugStartBtn = debugGo;
        debugGo.SetActive(false);

        // 接続済み用: つぎへすすむボタン
        ytNextBtn = CreateSetupActionButton(panelRT, "YTNextBtn", "つぎへすすむ",
            new Color(0.15f, 0.55f, 0.25f, 1f), new Vector2(0, 36), new Vector2(-16, 28),
            () => {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                GameManager.Instance?.DebugStartGame();
            });
        ytNextBtn.SetActive(false);

        // 接続済み用: ログアウトボタン
        ytLogoutBtn = CreateSetupActionButton(panelRT, "YTLogoutBtn", "ログアウト",
            new Color(0.55f, 0.15f, 0.15f, 1f), new Vector2(0, 6), new Vector2(-16, 24),
            () => {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                var yt = FindObjectOfType<YouTubeChatManager>();
                if (yt != null) { yt.isAuthenticated = false; yt.isConnected = false; yt.isConnecting = false; yt.enabled = false; }
                // パネルを閉じてタイトルに戻す
                if (youtubePanel != null) youtubePanel.SetActive(false);
                if (youtubeSetupMode) { ScalePanelContents(youtubePanel, 1f / 3f); youtubeSetupMode = false; }
                GameManager.Instance?.ResetGame();
            });
        ytLogoutBtn.SetActive(false);

        // 接続失敗時用: オフラインでプレイ
        ytOfflineBtn = CreateSetupActionButton(panelRT, "YTOfflineBtn", "オフラインでプレイ",
            new Color(0.4f, 0.4f, 0.45f, 1f), new Vector2(0, -48), new Vector2(-16, 28),
            () => {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SwordDraw");
                if (youtubeSetupMode) { ScalePanelContents(youtubePanel, 1f / 3f); youtubeSetupMode = false; }
                if (youtubePanel != null) youtubePanel.SetActive(false);
                GameManager.Instance?.SwitchToOfflineFromSetup();
            });
        ytOfflineBtn.SetActive(false);
    }

    GameObject CreateSetupActionButton(RectTransform parent, string name, string label, Color bgColor, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = bgColor * 1.2f;
        cb.pressedColor = bgColor * 0.7f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        MakeText("Text", rt, label, 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        return go;
    }

    void OnAuthButtonClicked()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        if (authButton != null) authButton.interactable = false;
        var ytChat = FindObjectOfType<YouTubeChatManager>();
        if (ytChat != null) ytChat.StartOAuth();
    }

    void OnConnectButtonClicked()
    {
        var ytChat = FindObjectOfType<YouTubeChatManager>();
        if (ytChat == null || videoIdInput == null) return;

        string vid = videoIdInput.text.Trim();
        if (vid.Contains("v="))
        {
            int idx = vid.IndexOf("v=") + 2;
            int end = vid.IndexOf("&", idx);
            vid = end > 0 ? vid.Substring(idx, end - idx) : vid.Substring(idx);
        }
        else if (vid.Contains("youtu.be/"))
        {
            int idx = vid.IndexOf("youtu.be/") + 9;
            int end = vid.IndexOf("?", idx);
            vid = end > 0 ? vid.Substring(idx, end - idx) : vid.Substring(idx);
        }

        if (string.IsNullOrEmpty(vid))
        {
            Debug.LogWarning("[YouTubeChat] Video IDを入力してください");
            return;
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        if (connectButton != null) connectButton.interactable = false;
        ytChat.ConnectToStream(vid);
    }

    /// <summary>オンラインモード選択後: YouTube接続パネルを画面中央に拡大表示</summary>
    public void ShowYouTubeSetup()
    {
        if (youtubePanel != null)
        {
            youtubePanel.SetActive(true);
            var rt = youtubePanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            // localScaleではなくsizeDelta+フォントサイズを3倍にする（テキストがぼやけない）
            ScalePanelContents(youtubePanel, 3f);
            youtubeSetupMode = true;

            // 接続済みチェック: 既にConnected → Next/Logoutを表示
            var ytChat = FindObjectOfType<YouTubeChatManager>();
            bool alreadyConnected = ytChat != null && ytChat.isConnected;
            if (videoIdInput != null) videoIdInput.gameObject.SetActive(!alreadyConnected);
            if (authButton != null) { authButton.gameObject.SetActive(!alreadyConnected); authButton.interactable = true; }
            if (connectButton != null) { connectButton.gameObject.SetActive(!alreadyConnected); connectButton.interactable = true; }
            if (ytNextBtn != null) ytNextBtn.SetActive(alreadyConnected);
            if (ytLogoutBtn != null) ytLogoutBtn.SetActive(alreadyConnected);
            if (ytDebugStartBtn != null) ytDebugStartBtn.SetActive(!alreadyConnected && IsDebug());

            // フェードイン
            var cg = youtubePanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = youtubePanel.AddComponent<CanvasGroup>();
            StartCoroutine(FadeInCanvasGroup(cg, 0.5f));
        }
    }

    /// <summary>YouTube接続完了後: パネルを元の位置（右下）に戻す</summary>
    void RestoreYouTubePanelPosition()
    {
        if (youtubePanel == null) return;
        if (youtubeSetupMode)
        {
            ScalePanelContents(youtubePanel, 1f / 3f);
            youtubeSetupMode = false;
        }
        if (ytDebugStartBtn != null) ytDebugStartBtn.SetActive(false);
        var rt = youtubePanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-10, 120);
        rt.sizeDelta = new Vector2(340, 160);
        rt.localScale = Vector3.one;
    }

    void CreateViewerRankingPanel()
    {
        // ランキングはHUD内のrankingBarに横並びで表示するため、パネルは不要
    }

    void UpdateViewerRanking()
    {
        if (rankingBar == null) return;

        // 既存スロットをクリア
        foreach (var s in rankingSlots)
            if (s != null) Destroy(s);
        rankingSlots.Clear();

        if (ViewerStats.Instance == null || ViewerStats.Instance.GetViewerCount() == 0)
            return;

        var ranking = ViewerStats.Instance.GetRanking(5);
        float iconSize = 32f;
        float slotWidth = 140f;

        for (int i = 0; i < ranking.Count; i++)
        {
            var v = ranking[i];

            var slotGo = new GameObject($"RankSlot_{i}");
            var slotRT = slotGo.AddComponent<RectTransform>();
            slotRT.SetParent(rankingBar, false);
            slotRT.anchorMin = new Vector2(0, 0);
            slotRT.anchorMax = new Vector2(0, 1);
            slotRT.pivot = new Vector2(0, 0.5f);
            slotRT.anchoredPosition = new Vector2(i * slotWidth, 0);
            slotRT.sizeDelta = new Vector2(slotWidth, 0);

            // 順位色
            Color nameColor = i == 0 ? new Color(1f, 0.85f, 0.3f) :
                              i == 1 ? new Color(0.78f, 0.78f, 0.82f) :
                              i == 2 ? new Color(0.8f, 0.55f, 0.3f) :
                              new Color(0.85f, 0.85f, 0.85f);

            // プロフィールアイコン（YouTube/TikTok画像 or 頭文字）
            var iconGo = new GameObject("Icon");
            var iconRT = iconGo.AddComponent<RectTransform>();
            iconRT.SetParent(slotRT, false);
            iconRT.anchorMin = new Vector2(0, 0.5f);
            iconRT.anchorMax = new Vector2(0, 0.5f);
            iconRT.pivot = new Vector2(0, 0.5f);
            iconRT.anchoredPosition = new Vector2(0, 0);
            iconRT.sizeDelta = new Vector2(iconSize, iconSize);
            SetupProfileIcon(iconGo, v, iconSize);

            float textOffsetX = iconSize + 4f;

            // 名前（アイコンの右、上寄せ）
            var nameGo = new GameObject("Name");
            var nameRT = nameGo.AddComponent<RectTransform>();
            nameRT.SetParent(slotRT, false);
            nameRT.anchorMin = new Vector2(0, 0.35f);
            nameRT.anchorMax = new Vector2(1, 1);
            nameRT.sizeDelta = Vector2.zero;
            nameRT.offsetMin = new Vector2(textOffsetX, 0);
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.font = GetFont();
            nameTxt.text = $"{i + 1}. {v.ownerName}";
            nameTxt.fontSize = 16;
            nameTxt.alignment = TextAnchor.MiddleLeft;
            nameTxt.color = nameColor;
            nameTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            nameTxt.raycastTarget = false;

            // ポイント（アイコンの右、下寄せ）
            var ptGo = new GameObject("Points");
            var ptRT = ptGo.AddComponent<RectTransform>();
            ptRT.SetParent(slotRT, false);
            ptRT.anchorMin = new Vector2(0, 0);
            ptRT.anchorMax = new Vector2(1, 0.45f);
            ptRT.sizeDelta = Vector2.zero;
            ptRT.offsetMin = new Vector2(textOffsetX, 0);
            var ptTxt = ptGo.AddComponent<Text>();
            ptTxt.font = GetFont();
            ptTxt.text = $"{v.score}pt";
            ptTxt.fontSize = 12;
            ptTxt.alignment = TextAnchor.MiddleLeft;
            ptTxt.color = new Color(nameColor.r, nameColor.g, nameColor.b, 0.7f);
            ptTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
            ptTxt.raycastTarget = false;

            rankingSlots.Add(slotGo);
        }
    }

    IEnumerator DownloadProfileImage(string ownerName, string url)
    {
        profileImageLoading.Add(ownerName);
        // TikTokのURLは配列の場合がある（最初の要素を使用）
        if (url.StartsWith("["))
        {
            int q1 = url.IndexOf('"');
            int q2 = q1 >= 0 ? url.IndexOf('"', q1 + 1) : -1;
            if (q1 >= 0 && q2 > q1)
                url = url.Substring(q1 + 1, q2 - q1 - 1);
        }
        // URLが空や無効な場合はスキップ
        if (string.IsNullOrEmpty(url) || (!url.StartsWith("http://") && !url.StartsWith("https://")))
        {
            Debug.LogWarning($"[UIManager] Invalid profile image URL for {ownerName}: '{url}'");
            profileImageLoading.Remove(ownerName);
            yield break;
        }
        Debug.Log($"[UIManager] Downloading profile image for {ownerName}: {url}");
        using (var req = UnityWebRequestTexture.GetTexture(url))
        {
            req.timeout = 15;
            req.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var tex = DownloadHandlerTexture.GetContent(req);
                profileImageCache[ownerName] = tex;
                Debug.Log($"[UIManager] Profile image loaded for {ownerName} ({tex.width}x{tex.height})");

                // 既存の味方ユニットにもアイコンを適用
                if (GameManager.Instance != null)
                {
                    foreach (var u in GameManager.Instance.allyUnits)
                    {
                        if (u != null && !u.isDead && u.ownerName == ownerName)
                            u.SetFaceIcon(tex);
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[UIManager] Profile image FAILED for {ownerName}: {req.error} (code={req.responseCode}, url={url})");
            }
        }
        profileImageLoading.Remove(ownerName);
    }

    void UpdateYouTubeStatus()
    {
        if (youtubeStatusText == null) return;

        // オフラインモード時はYouTubeステータス非表示
        var gm = GameManager.Instance;
        if (gm != null && gm.currentMode != GameMode.YouTube)
        {
            youtubeStatusText.text = "";
            return;
        }

        var ytChat = FindObjectOfType<YouTubeChatManager>();
        if (ytChat == null)
        {
            youtubeStatusText.text = "<color=#888>YouTube: みせってい</color>";
            return;
        }

        string errorLine = "";
        if (!string.IsNullOrEmpty(ytChat.lastError))
            errorLine = $"\n<color=#FF4444>{ytChat.lastError}</color>";

        if (!ytChat.isAuthenticated)
        {
            youtubeStatusText.text = "<color=#FF6644>YouTube: みにんしょう</color>\n" +
                "<color=#AAA>↓「YouTubeにんしょう」をおしてログイン</color>" + errorLine;
        }
        else if (ytChat.isConnecting)
        {
            youtubeStatusText.text = "<color=#FFAA00>YouTube: せつぞくちゅう...</color>\n" +
                "<color=#AAA>はいしんじょうほうをしゅとくしています</color>";
        }
        else if (!ytChat.isConnected)
        {
            youtubeStatusText.text = "<color=#FFAA00>YouTube: にんしょうずみ</color>\n" +
                "<color=#AAA>↓ Video IDをにゅうりょくして「はいしんにせつぞく」</color>" + errorLine;
        }
        else
        {
            youtubeStatusText.text = $"<color=#00FF88>YouTube: せつぞくかんりょう！</color>\n" +
                $"<color=#AAA>コマンドすう: {ytChat.totalCommands}</color>";
        }

        // セットアップモード中: 接続失敗時にボタン再活性化 + オフラインボタン表示
        if (youtubeSetupMode)
        {
            bool failed = !ytChat.isConnecting && !ytChat.isConnected && !string.IsNullOrEmpty(ytChat.lastError);
            bool idle = !ytChat.isConnecting && !ytChat.isConnected;
            if (idle && authButton != null) authButton.interactable = true;
            if (idle && connectButton != null) connectButton.interactable = true;
            if (ytOfflineBtn != null) ytOfflineBtn.SetActive(failed);
        }
    }

    // ─── YouTube Action Effects UI ───────────────────────────────

    void UpdateLikeCount()
    {
        if (likeCountText == null) return;

        var gm = GameManager.Instance;
        if (gm == null) { likeCountText.text = ""; return; }

        if (gm.currentMode == GameMode.YouTube)
        {
            var ytChat = FindObjectOfType<YouTubeChatManager>();
            if (ytChat == null || !ytChat.isConnected) { likeCountText.text = ""; return; }
            int likes = ytChat.GetCurrentLikeCount();
            if (likes > 0) likeCountText.text = $"\u2665 {likes}";
            else likeCountText.text = "";
        }
        else if (gm.currentMode == GameMode.TikTok)
        {
            var ttChat = FindObjectOfType<TikTokChatManager>();
            if (ttChat == null || !ttChat.isConnected) { likeCountText.text = ""; return; }
            int untilNext = ttChat.GetLikesUntilNextMilestone();
            int milestoneIdx = ttChat.GetCurrentLikeMilestoneIndex();
            if (milestoneIdx >= 0)
            {
                string name = GameConfig.TikTokLikeMilestoneNames[milestoneIdx];
                likeCountText.text = untilNext > 0
                    ? $"\u2665 {name} | つぎまで{untilNext}"
                    : $"\u2665 {name} MAX!";
            }
            else
            {
                likeCountText.text = untilNext > 0 ? $"\u2665 つぎまで{untilNext}" : "";
            }
        }
        else
        {
            likeCountText.text = "";
        }
    }

    public void ShowSuperChatPopup(string viewerName, int tier, string displayAmount)
    {
        if (superChatPopup == null)
            CreateSuperChatPopup();

        Color tierColor = GameConfig.SuperChatTierColors[tier];
        string tierName = GameConfig.SuperChatTierNames[tier];

        superChatPopupBg.color = new Color(tierColor.r * 0.3f, tierColor.g * 0.3f, tierColor.b * 0.3f, 0.95f);
        superChatPopupText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(tierColor)}>" +
            $"\u2605 SUPER CHAT \u2605</color>\n" +
            $"<size=36>{viewerName}</size>\n" +
            $"<color=#{ColorUtility.ToHtmlStringRGB(tierColor)}><size=48>{displayAmount}</size></color>";
        superChatPopupText.color = Color.white;

        superChatPopup.SetActive(true);
        superChatPopupTimer = 3f + tier * 0.5f; // 高ティアほど長く表示

        // 画面フラッシュ（高ティアのみ）
        if (tier >= 2)
            StartCoroutine(ScreenFlash(tierColor, 0.3f));
    }

    void CreateSuperChatPopup()
    {
        superChatPopup = new GameObject("SuperChatPopup");
        var rt = superChatPopup.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.7f);
        rt.anchorMax = new Vector2(0.5f, 0.7f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(600, 160);

        superChatPopupBg = superChatPopup.AddComponent<Image>();
        superChatPopupBg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

        // 枠線
        var outline = superChatPopup.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2, -2);

        superChatPopupText = MakeText("SCText", rt, "", 28,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        superChatPopupText.supportRichText = true;

        superChatPopup.SetActive(false);
    }

    void UpdateSuperChatPopup()
    {
        if (superChatPopup == null || !superChatPopup.activeSelf) return;
        superChatPopupTimer -= Time.deltaTime;

        // フェードアウト
        if (superChatPopupTimer < 0.5f && superChatPopupTimer > 0f)
        {
            float a = superChatPopupTimer / 0.5f;
            if (superChatPopupBg != null)
            {
                Color c = superChatPopupBg.color;
                c.a = 0.95f * a;
                superChatPopupBg.color = c;
            }
            if (superChatPopupText != null)
            {
                Color c = superChatPopupText.color;
                c.a = a;
                superChatPopupText.color = c;
            }
        }

        if (superChatPopupTimer <= 0f)
            superChatPopup.SetActive(false);
    }

    IEnumerator ScreenFlash(Color color, float duration)
    {
        var flashGo = new GameObject("ScreenFlash");
        var flashRT = flashGo.AddComponent<RectTransform>();
        flashRT.SetParent(mainCanvas.transform, false);
        flashRT.anchorMin = Vector2.zero;
        flashRT.anchorMax = Vector2.one;
        flashRT.sizeDelta = Vector2.zero;
        var flashImg = flashGo.AddComponent<Image>();
        flashImg.raycastTarget = false;
        flashImg.color = new Color(color.r, color.g, color.b, 0.4f);

        float t = 0;
        while (t < duration)
        {
            t += Time.deltaTime;
            float a = 0.4f * (1f - t / duration);
            flashImg.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }

        Destroy(flashGo);
    }

    /// <summary>必殺技用スクリーンフラッシュ（2段階: 強→弱フェード）</summary>
    public IEnumerator UltimateScreenFlash(Color color, float duration)
    {
        var flashGo = new GameObject("UltScreenFlash");
        var flashRT = flashGo.AddComponent<RectTransform>();
        flashRT.SetParent(mainCanvas.transform, false);
        flashRT.anchorMin = Vector2.zero;
        flashRT.anchorMax = Vector2.one;
        flashRT.sizeDelta = Vector2.zero;
        var flashImg = flashGo.AddComponent<Image>();
        flashImg.raycastTarget = false;

        // Phase 1: 瞬間的に明るく
        flashImg.color = new Color(color.r, color.g, color.b, 0.6f);
        yield return new WaitForSeconds(0.05f);

        // Phase 2: フェードアウト
        float t = 0;
        float fadeDuration = duration - 0.05f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = 0.6f * (1f - t / fadeDuration);
            flashImg.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }

        Destroy(flashGo);
    }

    // ─── YouTube Debug Panel ─────────────────────────────────────

    void CreateYouTubeDebugPanel()
    {
        var panel = MakePanel("YouTubeDebugPanel", debugScrollContent,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            Vector2.zero, new Vector2(0, 240),
            new Color(0.12f, 0.08f, 0.18f, 0.92f));
        youtubeDebugPanel = panel.gameObject;
        youtubeDebugPanel.AddComponent<LayoutElement>().preferredHeight = 240;
        youtubeDebugPanel.SetActive(false); // モード選択まで非表示
        var panelRT = panel.GetComponent<RectTransform>();

        // タイトル
        MakeText("DebugTitle", panelRT, "YouTube Debug", 16,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -2), new Vector2(0, 24), TextAnchor.MiddleCenter)
            .color = new Color(1f, 0.7f, 1f);

        // Super Chat ティアボタン（5つ横並び）
        MakeText("SCLabel", panelRT, "Super Chat:", 13,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(8, -28), new Vector2(100, 20), TextAnchor.MiddleLeft)
            .color = new Color(0.7f, 0.7f, 0.7f);

        float btnW = 65f;
        float btnH = 30f;
        float startX = 8f;
        float scY = -50f;

        for (int i = 0; i < 5; i++)
        {
            Color tierColor = GameConfig.SuperChatTierColors[i];
            string tierName = GameConfig.SuperChatTierNames[i];
            int capturedTier = i;

            var btnGo = new GameObject($"SC_{tierName}");
            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.SetParent(panelRT, false);
            btnRT.anchorMin = new Vector2(0, 1);
            btnRT.anchorMax = new Vector2(0, 1);
            btnRT.pivot = new Vector2(0, 1);
            btnRT.anchoredPosition = new Vector2(startX + i * (btnW + 4), scY);
            btnRT.sizeDelta = new Vector2(btnW, btnH);

            btnGo.AddComponent<Image>().color = new Color(tierColor.r * 0.5f, tierColor.g * 0.5f, tierColor.b * 0.5f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                GameManager.Instance?.DebugSimulateSuperChat(capturedTier);
            });

            var txt = MakeText("Text", btnRT, tierName, 13,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            txt.color = tierColor;
            txt.fontStyle = FontStyle.Bold;
        }

        // Member ボタン
        MakeText("MemLabel", panelRT, "Member:", 13,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(8, -88), new Vector2(100, 20), TextAnchor.MiddleLeft)
            .color = new Color(0.7f, 0.7f, 0.7f);

        var memBtnGo = new GameObject("DebugMember");
        var memBtnRT = memBtnGo.AddComponent<RectTransform>();
        memBtnRT.SetParent(panelRT, false);
        memBtnRT.anchorMin = new Vector2(0, 1);
        memBtnRT.anchorMax = new Vector2(0, 1);
        memBtnRT.pivot = new Vector2(0, 1);
        memBtnRT.anchoredPosition = new Vector2(8, -110);
        memBtnRT.sizeDelta = new Vector2(170, btnH);
        memBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.45f, 0.15f, 1f);
        memBtnGo.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugSimulateMember();
        });
        var memTxt = MakeText("Text", memBtnRT, "\u2605 New Member", 14,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        memTxt.color = new Color(1f, 0.85f, 0.2f);

        // Like Milestone ボタン
        MakeText("LikeLabel", panelRT, "Like:", 13,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(190, -88), new Vector2(100, 20), TextAnchor.MiddleLeft)
            .color = new Color(0.7f, 0.7f, 0.7f);

        var likeBtnGo = new GameObject("DebugLike");
        var likeBtnRT = likeBtnGo.AddComponent<RectTransform>();
        likeBtnRT.SetParent(panelRT, false);
        likeBtnRT.anchorMin = new Vector2(0, 1);
        likeBtnRT.anchorMax = new Vector2(0, 1);
        likeBtnRT.pivot = new Vector2(0, 1);
        likeBtnRT.anchoredPosition = new Vector2(190, -110);
        likeBtnRT.sizeDelta = new Vector2(170, btnH);
        likeBtnGo.AddComponent<Image>().color = new Color(0.5f, 0.15f, 0.3f, 1f);
        likeBtnGo.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugSimulateLikeMilestone();
        });
        var likeTxt = MakeText("Text", likeBtnRT, "\u2665 Like Milestone", 14,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        likeTxt.color = new Color(1f, 0.5f, 0.7f);

        // 説明テキスト
        MakeText("DebugNote", panelRT, "※ テストよう。みかたユニットがひつようです", 11,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 4), new Vector2(0, 18), TextAnchor.MiddleCenter)
            .color = new Color(0.5f, 0.5f, 0.5f);
    }

    // ─── TikTok UI ─────────────────────────────────────────────

    void CreateTikTokStatusPanel()
    {
        var panel = MakePanel("TikTokPanel", mainCanvas.transform,
            new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-10, 120), new Vector2(340, 140),
            new Color(0.12f, 0.08f, 0.15f, 0.92f));
        tiktokPanel = panel.gameObject;
        tiktokPanel.SetActive(false);
        var panelRT = panel.GetComponent<RectTransform>();

        tiktokStatusText = MakeText("TikTokStatus", panelRT, "", 13,
            new Vector2(0, 0.35f), new Vector2(1, 1), new Vector2(0.5f, 1f),
            new Vector2(10, 0), Vector2.zero, TextAnchor.UpperLeft);

        tiktokStatusText.supportRichText = true;

        // Username input
        var inputGo = new GameObject("UsernameInput");
        var inputRT = inputGo.AddComponent<RectTransform>();
        inputRT.SetParent(panelRT, false);
        inputRT.anchorMin = new Vector2(0, 0);
        inputRT.anchorMax = new Vector2(0.65f, 0);
        inputRT.pivot = new Vector2(0, 0);
        inputRT.anchoredPosition = new Vector2(8, 8);
        inputRT.sizeDelta = new Vector2(0, 28);
        var inputBg = inputGo.AddComponent<Image>();
        inputBg.color = new Color(0.2f, 0.2f, 0.25f, 1f);
        tiktokUsernameInput = inputGo.AddComponent<InputField>();

        var inputTextGo = new GameObject("Text");
        var inputTextRT = inputTextGo.AddComponent<RectTransform>();
        inputTextRT.SetParent(inputRT, false);
        inputTextRT.anchorMin = Vector2.zero;
        inputTextRT.anchorMax = Vector2.one;
        inputTextRT.sizeDelta = new Vector2(-10, 0);
        inputTextRT.anchoredPosition = Vector2.zero;
        var inputTxt = inputTextGo.AddComponent<Text>();
        inputTxt.font = GetFont();
        inputTxt.fontSize = 13;
        inputTxt.color = Color.white;
        inputTxt.supportRichText = false;
        tiktokUsernameInput.textComponent = inputTxt;

        var phGo = new GameObject("Placeholder");
        var phRT = phGo.AddComponent<RectTransform>();
        phRT.SetParent(inputRT, false);
        phRT.anchorMin = Vector2.zero;
        phRT.anchorMax = Vector2.one;
        phRT.sizeDelta = new Vector2(-10, 0);
        phRT.anchoredPosition = Vector2.zero;
        var phTxt = phGo.AddComponent<Text>();
        phTxt.font = GetFont();
        phTxt.text = "@username...";
        phTxt.fontSize = 13;
        phTxt.fontStyle = FontStyle.Italic;
        phTxt.color = new Color(0.5f, 0.5f, 0.5f);
        tiktokUsernameInput.placeholder = phTxt;

        // Connect button
        var connGo = new GameObject("TikTokConnectBtn");
        var connRT = connGo.AddComponent<RectTransform>();
        connRT.SetParent(panelRT, false);
        connRT.anchorMin = new Vector2(0.67f, 0);
        connRT.anchorMax = new Vector2(1, 0);
        connRT.pivot = new Vector2(1, 0);
        connRT.anchoredPosition = new Vector2(-8, 8);
        connRT.sizeDelta = new Vector2(0, 28);
        connGo.AddComponent<Image>().color = new Color(0.6f, 0.15f, 0.4f, 1f);
        tiktokConnectButton = connGo.AddComponent<Button>();
        tiktokConnectButton.onClick.AddListener(OnTikTokConnectClicked);
        MakeText("Text", connRT, "せつぞく", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // Debug Start button (hidden during gameplay, shown during setup)
        var debugGo = new GameObject("TTDebugStartBtn");
        var debugRT = debugGo.AddComponent<RectTransform>();
        debugRT.SetParent(panelRT, false);
        debugRT.anchorMin = new Vector2(0, 0);
        debugRT.anchorMax = new Vector2(1, 0);
        debugRT.pivot = new Vector2(0.5f, 1f);
        debugRT.anchoredPosition = new Vector2(0, -8);
        debugRT.sizeDelta = new Vector2(-16, 36);
        debugGo.AddComponent<Image>().color = new Color(0.6f, 0.4f, 0.1f, 1f);
        debugGo.AddComponent<Button>().onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugStartGame();
        });
        MakeText("Text", debugRT, "デバッグでかいし (せつぞくスキップ)", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        ttDebugStartBtn = debugGo;
        debugGo.SetActive(false);

        // 接続済み用: つぎへすすむボタン
        ttNextBtn = CreateSetupActionButton(panelRT, "TTNextBtn", "つぎへすすむ",
            new Color(0.15f, 0.55f, 0.25f, 1f), new Vector2(0, 36), new Vector2(-16, 28),
            () => {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                GameManager.Instance?.DebugStartGame();
            });
        ttNextBtn.SetActive(false);

        // 接続済み用: ログアウトボタン
        ttLogoutBtn = CreateSetupActionButton(panelRT, "TTLogoutBtn", "ログアウト",
            new Color(0.55f, 0.15f, 0.15f, 1f), new Vector2(0, 6), new Vector2(-16, 24),
            () => {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                var tt = FindObjectOfType<TikTokChatManager>();
                if (tt != null) { tt.Disconnect(); tt.enabled = false; }
                if (tiktokPanel != null) tiktokPanel.SetActive(false);
                if (tiktokSetupMode) { ScalePanelContents(tiktokPanel, 1f / 3f); tiktokSetupMode = false; }
                GameManager.Instance?.ResetGame();
            });
        ttLogoutBtn.SetActive(false);

        // 接続失敗時用: オフラインでプレイ
        ttOfflineBtn = CreateSetupActionButton(panelRT, "TTOfflineBtn", "オフラインでプレイ",
            new Color(0.4f, 0.4f, 0.45f, 1f), new Vector2(0, -48), new Vector2(-16, 28),
            () => {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SwordDraw");
                if (tiktokSetupMode) { ScalePanelContents(tiktokPanel, 1f / 3f); tiktokSetupMode = false; }
                if (tiktokPanel != null) tiktokPanel.SetActive(false);
                GameManager.Instance?.SwitchToOfflineFromSetup();
            });
        ttOfflineBtn.SetActive(false);
    }

    void OnTikTokConnectClicked()
    {
        var ttChat = FindObjectOfType<TikTokChatManager>();
        if (ttChat == null || tiktokUsernameInput == null) return;

        string username = tiktokUsernameInput.text.Trim();
        if (username.StartsWith("@")) username = username.Substring(1);
        if (string.IsNullOrEmpty(username))
        {
            Debug.LogWarning("[TikTok] \u30E6\u30FC\u30B6\u30FC\u30CD\u30FC\u30E0\u3092\u5165\u529B\u3057\u3066\u304F\u3060\u3055\u3044");
            return;
        }
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        if (tiktokConnectButton != null) tiktokConnectButton.interactable = false;
        ttChat.ConnectToStream(username);
    }

    /// <summary>TikTokモード選択後: TikTokパネルを画面中央に拡大表示</summary>
    public void ShowTikTokSetup()
    {
        if (tiktokPanel != null)
        {
            tiktokPanel.SetActive(true);
            var rt = tiktokPanel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            // localScaleではなくsizeDelta+フォントサイズを3倍にする（テキストがぼやけない）
            ScalePanelContents(tiktokPanel, 3f);
            tiktokSetupMode = true;

            // 接続済みチェック: 既にConnected → Next/Logoutを表示
            var ttChat = FindObjectOfType<TikTokChatManager>();
            bool alreadyConnected = ttChat != null && ttChat.isConnected;
            if (tiktokUsernameInput != null) tiktokUsernameInput.gameObject.SetActive(!alreadyConnected);
            if (tiktokConnectButton != null) { tiktokConnectButton.gameObject.SetActive(!alreadyConnected); tiktokConnectButton.interactable = true; }
            if (ttNextBtn != null) ttNextBtn.SetActive(alreadyConnected);
            if (ttLogoutBtn != null) ttLogoutBtn.SetActive(alreadyConnected);
            if (ttDebugStartBtn != null) ttDebugStartBtn.SetActive(!alreadyConnected && IsDebug());

            // フェードイン
            var cg = tiktokPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = tiktokPanel.AddComponent<CanvasGroup>();
            StartCoroutine(FadeInCanvasGroup(cg, 0.5f));
        }
    }

    void RestoreTikTokPanelPosition()
    {
        if (tiktokPanel == null) return;
        if (tiktokSetupMode)
        {
            ScalePanelContents(tiktokPanel, 1f / 3f);
            tiktokSetupMode = false;
        }
        if (ttDebugStartBtn != null) ttDebugStartBtn.SetActive(false);
        var rt = tiktokPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(1, 0);
        rt.anchoredPosition = new Vector2(-10, 120);
        rt.sizeDelta = new Vector2(340, 140);
        rt.localScale = Vector3.one;
    }

    void UpdateTikTokStatus()
    {
        if (tiktokStatusText == null) return;

        var gm = GameManager.Instance;
        if (gm != null && gm.currentMode != GameMode.TikTok)
        {
            tiktokStatusText.text = "";
            return;
        }

        var ttChat = FindObjectOfType<TikTokChatManager>();
        if (ttChat == null)
        {
            tiktokStatusText.text = "<color=#888>TikTok: みせってい</color>";
            return;
        }

        string errorLine = "";
        if (!string.IsNullOrEmpty(ttChat.lastError))
            errorLine = $"\n<color=#FF4444>{ttChat.lastError}</color>";

        if (ttChat.isConnecting)
        {
            tiktokStatusText.text = "<color=#FFAA00>TikTok: せつぞくちゅう...</color>\n" +
                $"<color=#AAA>@{ttChat.tiktokUsername}</color>";
        }
        else if (!ttChat.isConnected)
        {
            tiktokStatusText.text = "<color=#FF6644>TikTok: みせつぞく</color>\n" +
                "<color=#AAA>\u2193 ユーザーネームをにゅうりょくして「せつぞく」</color>" + errorLine;
        }
        else
        {
            tiktokStatusText.text = $"<color=#00FF88>TikTok: せつぞくかんりょう！</color>\n" +
                $"<color=#AAA>@{ttChat.tiktokUsername} | コマンド: {ttChat.totalCommands}</color>";
        }

        // セットアップモード中: 接続失敗時にボタン再活性化 + オフラインボタン表示
        if (tiktokSetupMode)
        {
            bool failed = !ttChat.isConnecting && !ttChat.isConnected && !string.IsNullOrEmpty(ttChat.lastError);
            bool idle = !ttChat.isConnecting && !ttChat.isConnected;
            if (idle && tiktokConnectButton != null) tiktokConnectButton.interactable = true;
            if (ttOfflineBtn != null) ttOfflineBtn.SetActive(failed);
        }
    }

    // ─── 接続ステータス（キュー右端固定枠） ─────────────────────

    void UpdateConnectionStatus()
    {
        var gm = GameManager.Instance;
        if (gm == null || queuePanel == null) return;

        // タイトル画面では非表示
        if (gm.currentPhase == GamePhase.Title)
        {
            if (connectionStatusSlot != null) connectionStatusSlot.SetActive(false);
            return;
        }

        // 接続ステータス枠がなければ作成
        if (connectionStatusSlot == null)
        {
            connectionStatusSlot = new GameObject("ConnectionStatus");
            var rt = connectionStatusSlot.AddComponent<RectTransform>();
            rt.SetParent(queuePanel, false);
            rt.anchorMin = new Vector2(1, 0);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.anchoredPosition = new Vector2(-5, 0);
            rt.sizeDelta = new Vector2(160, -10);

            var bg = connectionStatusSlot.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.15f, 0.9f);
            bg.raycastTarget = false;

            // 雷アイコン
            var iconTxt = MakeText("BoltIcon", rt, "\u26A1", 48,
                new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(60, 60), TextAnchor.MiddleCenter);
            iconTxt.color = new Color(1f, 0.85f, 0.2f);
            iconTxt.raycastTarget = false;

            // ステータステキスト
            connectionStatusText = MakeText("StatusText", rt, "", 20,
                new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(150, 55), TextAnchor.MiddleCenter);

            connectionStatusText.supportRichText = true;
            connectionStatusText.raycastTarget = false;

            // コマンドヒント（接続ステータスの左隣）
            listenerInstructionText = MakeText("CommandHint", queuePanel, "", 28,
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 0.5f),
                new Vector2(-620, 0), new Vector2(420, -6), TextAnchor.MiddleLeft);
            listenerInstructionText.supportRichText = true;
            listenerInstructionText.raycastTarget = false;
            listenerInstructionText.color = new Color(0.9f, 0.85f, 0.6f);
            listenerInstructionText.lineSpacing = 1.1f;
            listenerInstructionText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        connectionStatusSlot.SetActive(true);

        // モード別ステータス
        if (gm.currentMode == GameMode.YouTube)
        {
            var ytChat = FindObjectOfType<YouTubeChatManager>();
            bool connected = ytChat != null && ytChat.enabled && ytChat.isConnected;
            var bg = connectionStatusSlot.GetComponent<Image>();
            if (connected)
            {
                bg.color = new Color(0.05f, 0.12f, 0.05f, 0.9f);
                connectionStatusText.text = "<color=#88FF88>YouTube</color>\n<color=#AAFFAA>\u914D\u4FE1\u4E2D</color>";
            }
            else
            {
                bg.color = new Color(0.12f, 0.08f, 0.05f, 0.9f);
                connectionStatusText.text = "<color=#FFAA44>YouTube</color>\n<color=#FFCC88>\u63A5\u7D9A\u5F85\u3061</color>";
            }
        }
        else if (gm.currentMode == GameMode.TikTok)
        {
            var ttChat = FindObjectOfType<TikTokChatManager>();
            bool connected = ttChat != null && ttChat.isConnected;
            var bg = connectionStatusSlot.GetComponent<Image>();
            if (connected)
            {
                bg.color = new Color(0.05f, 0.12f, 0.05f, 0.9f);
                connectionStatusText.text = "<color=#FF0050>TikTok</color>\n<color=#AAFFAA>\u914D\u4FE1\u4E2D</color>";
            }
            else
            {
                bg.color = new Color(0.12f, 0.08f, 0.05f, 0.9f);
                connectionStatusText.text = "<color=#FF0050>TikTok</color>\n<color=#FFCC88>\u63A5\u7D9A\u5F85\u3061</color>";
            }
        }
        else
        {
            // オフラインモード
            var bg = connectionStatusSlot.GetComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.9f);
            connectionStatusText.text = "<color=#AAAAAA>Offline</color>\n<color=#888888>\u30C7\u30D0\u30C3\u30B0</color>";
        }

        // 雷アイコンの色を接続状態に応じて変更
        var boltIcon = connectionStatusSlot.transform.Find("BoltIcon");
        if (boltIcon != null)
        {
            var boltTxt = boltIcon.GetComponent<Text>();
            if (boltTxt != null)
            {
                bool isConnected = false;
                if (gm.currentMode == GameMode.YouTube)
                {
                    var ytChat = FindObjectOfType<YouTubeChatManager>();
                    isConnected = ytChat != null && ytChat.enabled && ytChat.isConnected;
                }
                else if (gm.currentMode == GameMode.TikTok)
                {
                    var ttChat = FindObjectOfType<TikTokChatManager>();
                    isConnected = ttChat != null && ttChat.isConnected;
                }

                if (isConnected)
                {
                    // 接続中: 雷アイコンが点滅
                    float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 3f);
                    boltTxt.color = new Color(pulse, 0.85f * pulse, 0.2f * pulse);
                }
                else
                {
                    boltTxt.color = new Color(0.5f, 0.5f, 0.5f);
                }
            }
        }

        // コマンドヒント更新
        if (listenerInstructionText != null)
        {
            bool showHint = false;
            if (gm.currentMode == GameMode.Offline)
            {
                showHint = true;
            }
            else if (gm.currentMode == GameMode.YouTube)
            {
                var ytc = FindObjectOfType<YouTubeChatManager>();
                showHint = ytc != null && ytc.enabled && ytc.isConnected;
            }
            else if (gm.currentMode == GameMode.TikTok)
            {
                var ttc = FindObjectOfType<TikTokChatManager>();
                showHint = ttc != null && ttc.isConnected;
            }
            listenerInstructionText.text = showHint
                ? "<color=#FFE080>\u25B6召喚</color> <color=#FFD700>けん やり ゆみ かいふく まほう</color>\n" +
                  "<color=#88CCFF>\u25B6指示</color> <color=#AAEEFF>すすめ さがれ ひっさつ</color>"
                : "";
        }
    }

    public void ShowTikTokGiftPopup(string viewerName, int tier, string displayText, int coins)
    {
        if (tiktokGiftPopup == null)
            CreateTikTokGiftPopup();

        Color tierColor = GameConfig.TikTokGiftTierColors[tier];
        string tierName = GameConfig.TikTokGiftTierNames[tier];

        tiktokGiftPopupBg.color = new Color(tierColor.r * 0.3f, tierColor.g * 0.3f, tierColor.b * 0.3f, 0.95f);
        tiktokGiftPopupText.text = $"<color=#{ColorUtility.ToHtmlStringRGB(tierColor)}>" +
            $"\u2605 GIFT \u2605</color>\n" +
            $"<size=36>{viewerName}</size>\n" +
            $"<color=#{ColorUtility.ToHtmlStringRGB(tierColor)}><size=44>{tierName} ({coins} coins)</size></color>";
        tiktokGiftPopupText.color = Color.white;

        tiktokGiftPopup.SetActive(true);
        tiktokGiftPopupTimer = 3f + tier * 0.5f;

        if (tier >= 3)
            StartCoroutine(ScreenFlash(tierColor, 0.3f));
    }

    void CreateTikTokGiftPopup()
    {
        tiktokGiftPopup = new GameObject("TikTokGiftPopup");
        var rt = tiktokGiftPopup.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = new Vector2(0.5f, 0.7f);
        rt.anchorMax = new Vector2(0.5f, 0.7f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(600, 160);

        tiktokGiftPopupBg = tiktokGiftPopup.AddComponent<Image>();
        tiktokGiftPopupBg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

        var outline = tiktokGiftPopup.AddComponent<Outline>();
        outline.effectColor = Color.white;
        outline.effectDistance = new Vector2(2, -2);

        tiktokGiftPopupText = MakeText("GiftText", rt, "", 28,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        tiktokGiftPopupText.supportRichText = true;

        tiktokGiftPopup.SetActive(false);
    }

    void UpdateTikTokGiftPopup()
    {
        if (tiktokGiftPopup == null || !tiktokGiftPopup.activeSelf) return;
        tiktokGiftPopupTimer -= Time.deltaTime;

        if (tiktokGiftPopupTimer < 0.5f && tiktokGiftPopupTimer > 0f)
        {
            float a = tiktokGiftPopupTimer / 0.5f;
            if (tiktokGiftPopupBg != null)
            {
                Color c = tiktokGiftPopupBg.color;
                c.a = 0.95f * a;
                tiktokGiftPopupBg.color = c;
            }
            if (tiktokGiftPopupText != null)
            {
                Color c = tiktokGiftPopupText.color;
                c.a = a;
                tiktokGiftPopupText.color = c;
            }
        }

        if (tiktokGiftPopupTimer <= 0f)
            tiktokGiftPopup.SetActive(false);
    }

    void CreateTikTokDebugPanel()
    {
        var panel = MakePanel("TikTokDebugPanel", debugScrollContent,
            new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
            Vector2.zero, new Vector2(0, 310),
            new Color(0.15f, 0.05f, 0.12f, 0.92f));
        tiktokDebugPanel = panel.gameObject;
        tiktokDebugPanel.AddComponent<LayoutElement>().preferredHeight = 310;
        tiktokDebugPanel.SetActive(false); // モード選択まで非表示
        var panelRT = panel.GetComponent<RectTransform>();

        MakeText("TTDebugTitle", panelRT, "TikTok Debug", 16,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -2), new Vector2(0, 24), TextAnchor.MiddleCenter)
            .color = new Color(1f, 0.5f, 0.7f);

        // Gift 6-tier buttons
        MakeText("GiftLabel", panelRT, "Gift:", 13,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(8, -28), new Vector2(100, 20), TextAnchor.MiddleLeft)
            .color = new Color(0.7f, 0.7f, 0.7f);

        float btnW = 68f;
        float btnH = 28f;
        float startX = 8f;

        for (int i = 0; i < 6; i++)
        {
            Color tierColor = GameConfig.TikTokGiftTierColors[i];
            string tierName = GameConfig.TikTokGiftTierNames[i];
            int capturedTier = i;

            var btnGo = new GameObject($"TTGift_{tierName}");
            var btnRT = btnGo.AddComponent<RectTransform>();
            btnRT.SetParent(panelRT, false);
            btnRT.anchorMin = new Vector2(0, 1);
            btnRT.anchorMax = new Vector2(0, 1);
            btnRT.pivot = new Vector2(0, 1);
            btnRT.anchoredPosition = new Vector2(startX + i * (btnW + 3), -50f);
            btnRT.sizeDelta = new Vector2(btnW, btnH);

            btnGo.AddComponent<Image>().color = new Color(tierColor.r * 0.4f, tierColor.g * 0.4f, tierColor.b * 0.4f, 1f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
                GameManager.Instance?.DebugSimulateTikTokGift(capturedTier);
            });

            var txt = MakeText("Text", btnRT, tierName, 12,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
            txt.color = tierColor;
            txt.fontStyle = FontStyle.Bold;
        }

        // Team Member button
        float row2Y = -90f;
        MakeText("TeamLabel", panelRT, "Team/Sub:", 13,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(8, row2Y + 12), new Vector2(100, 20), TextAnchor.MiddleLeft)
            .color = new Color(0.7f, 0.7f, 0.7f);

        var teamBtnGo = new GameObject("DebugTeam");
        var teamBtnRT = teamBtnGo.AddComponent<RectTransform>();
        teamBtnRT.SetParent(panelRT, false);
        teamBtnRT.anchorMin = new Vector2(0, 1);
        teamBtnRT.anchorMax = new Vector2(0, 1);
        teamBtnRT.pivot = new Vector2(0, 1);
        teamBtnRT.anchoredPosition = new Vector2(8, row2Y - 10);
        teamBtnRT.sizeDelta = new Vector2(140, btnH);
        teamBtnGo.AddComponent<Image>().color = new Color(0.15f, 0.35f, 0.55f, 1f);
        teamBtnGo.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugSimulateTeamMember();
        });
        MakeText("Text", teamBtnRT, "\u265A Team Join", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter)
            .color = new Color(0.3f, 0.8f, 1f);

        // Subscribe button
        var subBtnGo = new GameObject("DebugSubscribe");
        var subBtnRT = subBtnGo.AddComponent<RectTransform>();
        subBtnRT.SetParent(panelRT, false);
        subBtnRT.anchorMin = new Vector2(0, 1);
        subBtnRT.anchorMax = new Vector2(0, 1);
        subBtnRT.pivot = new Vector2(0, 1);
        subBtnRT.anchoredPosition = new Vector2(155, row2Y - 10);
        subBtnRT.sizeDelta = new Vector2(140, btnH);
        subBtnGo.AddComponent<Image>().color = new Color(0.45f, 0.1f, 0.35f, 1f);
        subBtnGo.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugSimulateSubscriber();
        });
        MakeText("Text", subBtnRT, "\u2666 Subscribe", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter)
            .color = new Color(0.9f, 0.3f, 1f);

        // Like Milestone button
        float row3Y = -140f;
        MakeText("TTLikeLabel", panelRT, "Like/Social:", 13,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(8, row3Y + 12), new Vector2(100, 20), TextAnchor.MiddleLeft)
            .color = new Color(0.7f, 0.7f, 0.7f);

        var likeBtnGo = new GameObject("DebugTTLike");
        var likeBtnRT = likeBtnGo.AddComponent<RectTransform>();
        likeBtnRT.SetParent(panelRT, false);
        likeBtnRT.anchorMin = new Vector2(0, 1);
        likeBtnRT.anchorMax = new Vector2(0, 1);
        likeBtnRT.pivot = new Vector2(0, 1);
        likeBtnRT.anchoredPosition = new Vector2(8, row3Y - 10);
        likeBtnRT.sizeDelta = new Vector2(140, btnH);
        likeBtnGo.AddComponent<Image>().color = new Color(0.5f, 0.15f, 0.3f, 1f);
        likeBtnGo.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugSimulateTikTokLikeMilestone();
        });
        MakeText("Text", likeBtnRT, "\u2665 Like", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter)
            .color = new Color(1f, 0.5f, 0.7f);

        // Follow button
        var followBtnGo = new GameObject("DebugFollow");
        var followBtnRT = followBtnGo.AddComponent<RectTransform>();
        followBtnRT.SetParent(panelRT, false);
        followBtnRT.anchorMin = new Vector2(0, 1);
        followBtnRT.anchorMax = new Vector2(0, 1);
        followBtnRT.pivot = new Vector2(0, 1);
        followBtnRT.anchoredPosition = new Vector2(155, row3Y - 10);
        followBtnRT.sizeDelta = new Vector2(105, btnH);
        followBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.3f, 0.5f, 1f);
        followBtnGo.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugSimulateFollow();
        });
        MakeText("Text", followBtnRT, "Follow", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter)
            .color = new Color(0.7f, 0.85f, 1f);

        // Share button
        var shareBtnGo = new GameObject("DebugShare");
        var shareBtnRT = shareBtnGo.AddComponent<RectTransform>();
        shareBtnRT.SetParent(panelRT, false);
        shareBtnRT.anchorMin = new Vector2(0, 1);
        shareBtnRT.anchorMax = new Vector2(0, 1);
        shareBtnRT.pivot = new Vector2(0, 1);
        shareBtnRT.anchoredPosition = new Vector2(265, row3Y - 10);
        shareBtnRT.sizeDelta = new Vector2(105, btnH);
        shareBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.4f, 0.3f, 1f);
        shareBtnGo.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.DebugSimulateShare();
        });
        MakeText("Text", shareBtnRT, "Share", 13,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter)
            .color = new Color(0.7f, 1f, 0.8f);

        MakeText("TTDebugNote", panelRT, "\u203B \u30C6\u30B9\u30C8\u7528\u3002\u5473\u65B9\u30E6\u30CB\u30C3\u30C8\u304C\u5FC5\u8981\u3067\u3059", 11,
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 4), new Vector2(0, 18), TextAnchor.MiddleCenter)
            .color = new Color(0.5f, 0.5f, 0.5f);
    }

    // Also restore TikTok panel position when entering Preparation
    // (patched into OnPhaseChanged's Preparation case via RestoreYouTubePanelPosition)

    Sprite LoadLogoSprite()
    {
#if UNITY_EDITOR
        string path = "Assets/Resource/Logo.png";
        // インポート設定をSprite用に修正
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer != null && importer.textureType != UnityEditor.TextureImporterType.Sprite)
        {
            importer.textureType = UnityEditor.TextureImporterType.Sprite;
            importer.SaveAndReimport();
        }
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return Resources.Load<Sprite>("Logo");
#endif
    }

    Sprite LoadSwordSprite(int index)
    {
#if UNITY_EDITOR
        string path = $"Assets/Tiny Swords/UI Elements/Swords/Swords {index}.png";
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != UnityEditor.TextureImporterType.Sprite)
            { importer.textureType = UnityEditor.TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != UnityEditor.SpriteImportMode.Single)
            { importer.spriteImportMode = UnityEditor.SpriteImportMode.Single; changed = true; }
            // 9-slice: 左=柄(30%), 右=剣先(12%) を固定、中央の刀身だけ伸縮
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
            {
                int hiltW = Mathf.RoundToInt(tex.width * 0.30f);
                int tipW = Mathf.RoundToInt(tex.width * 0.12f);
                var border = new Vector4(hiltW, 0, tipW, 0);
                if (importer.spriteBorder != border)
                { importer.spriteBorder = border; changed = true; }
            }
            if (changed) importer.SaveAndReimport();
        }
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return Resources.Load<Sprite>($"Sword_{index}");
#endif
    }

    Sprite LoadSpecialPaperSprite()
    {
#if UNITY_EDITOR
        string path = "Assets/Tiny Swords/UI Elements/Papers/SpecialPaper.png";
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != UnityEditor.TextureImporterType.Sprite)
            { importer.textureType = UnityEditor.TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != UnityEditor.SpriteImportMode.Single)
            { importer.spriteImportMode = UnityEditor.SpriteImportMode.Single; changed = true; }
            // 9-slice用ボーダー設定（金枠の内側でスライス）
            if (importer.spriteBorder == Vector4.zero)
            { importer.spriteBorder = new Vector4(64, 64, 64, 64); changed = true; }
            if (changed) importer.SaveAndReimport();
        }
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
#else
        return Resources.Load<Sprite>("SpecialPaper");
#endif
    }

    // ─── UI Helpers ──────────────────────────────────────────────

    /// <summary>パネルの子要素のサイズ・位置・フォントを指定倍率でスケーリング（localScaleを使わずクリアなテキスト表示）</summary>
    void ScalePanelContents(GameObject panel, float factor)
    {
        var rootRT = panel.GetComponent<RectTransform>();
        rootRT.sizeDelta *= factor;
        foreach (var rt in panel.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt == rootRT) continue;
            rt.anchoredPosition *= factor;
            rt.sizeDelta *= factor;
        }
        foreach (var text in panel.GetComponentsInChildren<Text>(true))
            text.fontSize = Mathf.RoundToInt(text.fontSize * factor);
    }

    // ─── Hamburger Menu ─────────────────────────────────────────

    void CreateHamburgerMenu()
    {
        // ☰ ボタン（TopBar右端、Scoreと同じ高さ）
        hamburgerBtn = new GameObject("HamburgerBtn");
        var btnRT = hamburgerBtn.AddComponent<RectTransform>();
        btnRT.SetParent(topBarGo.transform, false);
        btnRT.anchorMin = new Vector2(1, 0);
        btnRT.anchorMax = new Vector2(1, 1);
        btnRT.pivot = new Vector2(1, 0.5f);
        btnRT.anchoredPosition = new Vector2(-5, 0);
        btnRT.sizeDelta = new Vector2(55, 0); // TopBarの高さに合わせる

        var btnImg = hamburgerBtn.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.15f, 0.2f, 0.7f);

        var btn = hamburgerBtn.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var cb = btn.colors;
        cb.normalColor = new Color(0.15f, 0.15f, 0.2f, 0.7f);
        cb.highlightedColor = new Color(0.25f, 0.25f, 0.35f, 0.9f);
        cb.pressedColor = new Color(0.05f, 0.05f, 0.1f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(ToggleSettingsPanel);

        var btnTxt = MakeText("Icon", btnRT, "\u2630", 36,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        btnTxt.color = Color.white;

        // 設定パネル（画面中央、SpecialPaper背景）
        settingsPanel = new GameObject("SettingsPanel");
        var panelRT = settingsPanel.AddComponent<RectTransform>();
        panelRT.SetParent(mainCanvas.transform, false);
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.anchoredPosition = Vector2.zero;
        panelRT.sizeDelta = new Vector2(850, 750);

        var panelImg = settingsPanel.AddComponent<Image>();
        var paperSprite = LoadSpecialPaperSprite();
        if (paperSprite != null)
        {
            panelImg.sprite = paperSprite;
            panelImg.type = Image.Type.Sliced;
            panelImg.color = Color.white;
        }
        else
        {
            panelImg.color = new Color(0.15f, 0.12f, 0.08f, 0.95f);
        }

        float padL = 60f;  // 左パディング（9-sliceボーダー分）
        float padT = -70f; // 上パディング（9-sliceボーダー分）
        float yPos = padT;
        float contentW = 700f; // パネル内コンテンツ幅

        // ─── SE音量 ───
        var seLabel = MakeText("SELabel", panelRT, "SE Volume", 22,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(padL, yPos), new Vector2(150, 30), TextAnchor.MiddleLeft);
        seLabel.color = new Color(0.8f, 0.8f, 0.9f);

        float seInit = AudioManager.Instance != null ? AudioManager.Instance.seVolume : 0.7f;
        var seSlider = CreateSlider("SESlider", panelRT,
            new Vector2(padL + 150, yPos - 5), new Vector2(500, 25), seInit,
            v => { if (AudioManager.Instance != null) AudioManager.Instance.SetSEVolume(v); });

        yPos -= 45f;

        // ─── BGM音量 ───
        var bgmLabel = MakeText("BGMLabel", panelRT, "BGM Volume", 22,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(padL, yPos), new Vector2(150, 30), TextAnchor.MiddleLeft);
        bgmLabel.color = new Color(0.8f, 0.8f, 0.9f);

        float bgmInit = AudioManager.Instance != null ? AudioManager.Instance.bgmVolume : 0.5f;
        var bgmSlider = CreateSlider("BGMSlider", panelRT,
            new Vector2(padL + 150, yPos - 5), new Vector2(500, 25), bgmInit,
            v => { if (AudioManager.Instance != null) AudioManager.Instance.SetBGMVolume(v); });

        yPos -= 50f;

        // ─── セーブボタン ───
        var saveBtnGo = MakePanel("SaveBtn", panelRT,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(padL, yPos), new Vector2(contentW, 40), new Color(0.2f, 0.5f, 0.7f, 1f));
        var saveBtnComp = saveBtnGo.gameObject.AddComponent<Button>();
        saveBtnComp.targetGraphic = saveBtnGo.GetComponent<Image>();
        var savCB = saveBtnComp.colors;
        savCB.normalColor = new Color(0.2f, 0.5f, 0.7f, 1f);
        savCB.highlightedColor = new Color(0.25f, 0.6f, 0.8f, 1f);
        savCB.pressedColor = new Color(0.15f, 0.4f, 0.55f, 1f);
        saveBtnComp.colors = savCB;
        saveBtnComp.onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            GameManager.Instance?.SaveViewerData();
        });
        var saveTxt = MakeText("Text", saveBtnGo, "セーブ", 22,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        saveTxt.fontStyle = FontStyle.Bold;
        yPos -= 50f;

        // ─── リセットボタン ───
        var resetBtnGo = MakePanel("ResetBtn", panelRT,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(padL, yPos), new Vector2(contentW, 40), new Color(0.6f, 0.15f, 0.15f, 1f));
        var resetBtnComp = resetBtnGo.gameObject.AddComponent<Button>();
        resetBtnComp.targetGraphic = resetBtnGo.GetComponent<Image>();
        var rstCB = resetBtnComp.colors;
        rstCB.normalColor = new Color(0.6f, 0.15f, 0.15f, 1f);
        rstCB.highlightedColor = new Color(0.75f, 0.2f, 0.2f, 1f);
        rstCB.pressedColor = new Color(0.4f, 0.1f, 0.1f, 1f);
        resetBtnComp.colors = rstCB;
        resetBtnComp.onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            settingsPanel.SetActive(false);
            GameManager.Instance?.ResetGame();
        });
        var resetTxt = MakeText("Text", resetBtnGo, "Reset Game", 22,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        resetTxt.fontStyle = FontStyle.Bold;
        yPos -= 55f;

        // ─── 区切り線 ───
        var divider = MakePanel("Divider", panelRT,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(padL, yPos), new Vector2(contentW, 2), new Color(0.5f, 0.45f, 0.3f));
        yPos -= 15f;

        // ─── ゲームしゅうりょうボタン（リセットからはなしてはいち） ───
        var endBtnGo = MakePanel("EndGameBtn", panelRT,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(padL, yPos), new Vector2(contentW, 40), new Color(0.35f, 0.10f, 0.35f, 1f));
        var endBtnComp = endBtnGo.gameObject.AddComponent<Button>();
        endBtnComp.targetGraphic = endBtnGo.GetComponent<Image>();
        var endCB = endBtnComp.colors;
        endCB.normalColor = new Color(0.35f, 0.10f, 0.35f, 1f);
        endCB.highlightedColor = new Color(0.45f, 0.15f, 0.45f, 1f);
        endCB.pressedColor = new Color(0.25f, 0.08f, 0.25f, 1f);
        endBtnComp.colors = endCB;
        endBtnComp.onClick.AddListener(() => {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            settingsPanel.SetActive(false);
            GameManager.Instance?.ForceGameOver();
        });
        var endTxt = MakeText("Text", endBtnGo, "ゲームしゅうりょう", 22,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        endTxt.fontStyle = FontStyle.Bold;
        yPos -= 55f;

        // ─── Tips（スクロール可能） ───
        var tipsTitle = MakeText("TipsTitle", panelRT, "Tips", 24,
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(padL, yPos), new Vector2(contentW, 30), TextAnchor.MiddleLeft);
        tipsTitle.fontStyle = FontStyle.Bold;
        tipsTitle.color = new Color(1f, 0.9f, 0.5f);
        yPos -= 35f;

        // ScrollView
        var scrollGo = new GameObject("TipsScroll");
        var scrollRT = scrollGo.AddComponent<RectTransform>();
        scrollRT.SetParent(panelRT, false);
        scrollRT.anchorMin = new Vector2(0, 1);
        scrollRT.anchorMax = new Vector2(0, 1);
        scrollRT.pivot = new Vector2(0, 1);
        scrollRT.anchoredPosition = new Vector2(padL, yPos);
        float scrollH = 750 + yPos - 80; // 残りスペース（下ボーダー分引く）
        scrollRT.sizeDelta = new Vector2(contentW, scrollH);
        var scrollImg = scrollGo.AddComponent<Image>();
        scrollImg.color = new Color(0, 0, 0, 0.01f); // ほぼ透明（マスク用）
        scrollGo.AddComponent<Mask>().showMaskGraphic = false;
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50f;

        // Content
        var contentGo = new GameObject("Content");
        var contentRT = contentGo.AddComponent<RectTransform>();
        contentRT.SetParent(scrollRT, false);
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0, 1);
        contentRT.anchoredPosition = Vector2.zero;

        scroll.content = contentRT;

        string tips =
            "<color=#FFE080>【ゴールドシステム】</color>\n" +
            "じかんけいかで 1G/びょう、てきげきはで +2G かくとく！\n" +
            "がめんしたのショップでユニットをこうにゅうできます。\n" +
            "  W:3G / L:4G / A:4G / M:5G / Ma:5G\n\n" +
            "<color=#FFE080>【コマンドいちらん】</color>\n" +
            "チャットでいかをにゅうりょくするとへいしをしょうかん！\n" +
            "  けん \u2192 ウォリアー（きんせつ・バランスがた）\n" +
            "  やり \u2192 ランサー（きんせつ・リーチがた）\n" +
            "  ゆみ \u2192 アーチャー（えんきょりこうげき）\n" +
            "  かいふく \u2192 モンク（かいふく＆しえん）\n" +
            "  まほう \u2192 メイジ（えんきょりまほうこうげき）\n\n" +
            "<color=#FFE080>【スパチャ / ギフトのおんけい】</color>\n" +
            "なげてくれたきんがくにおうじてへいしがきょうか！\n" +
            "  ・ステータスUP（さいだい2\uFF5E5ばい）\n" +
            "  ・からだがおおきくなる\n" +
            "  ・じょうきオーラがしゅつげん\n" +
            "  ・こうがくギフトでついかのへいしもしょうかん\n\n" +
            "<color=#FFE080>【メンバーシップ / サブスク】</color>\n" +
            "  ・ステータス 1.15\uFF5E1.20ばい\n" +
            "  ・クールダウンたんしゅく\n" +
            "  ・きし(ナイト)ユニットかいほう（サブスクげんてい）\n\n" +
            "<color=#FFE080>【いいねマイルストーン】</color>\n" +
            "みんなのいいねがいっていすうにたっするとぜんぐんきょうか！\n" +
            "  50 / 200 / 500 / 1000 / 3000\n" +
            "  \u2192 だんかいてきにHP・ATK・スピードUP";

        var tipsText = MakeText("TipsText", contentRT, tips, 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(5, 0), new Vector2(-10, 0), TextAnchor.UpperLeft);

        tipsText.color = new Color(0.85f, 0.85f, 0.9f);
        tipsText.lineSpacing = 1.2f;
        tipsText.verticalOverflow = VerticalWrapMode.Overflow;

        // テキストの高さに合わせてContentサイズ調整
        Canvas.ForceUpdateCanvases();
        float textH = tipsText.preferredHeight + 20;
        contentRT.sizeDelta = new Vector2(0, textH);

        settingsPanel.SetActive(false);
    }

    void ToggleSettingsPanel()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    Slider CreateSlider(string name, Transform parent, Vector2 anchoredPos, Vector2 size,
        float initVal, UnityEngine.Events.UnityAction<float> onChange)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        // Background
        var bgGo = new GameObject("Background");
        var bgRT = bgGo.AddComponent<RectTransform>();
        bgRT.SetParent(rt, false);
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        var bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);

        // Fill Area
        var fillAreaGo = new GameObject("FillArea");
        var fillAreaRT = fillAreaGo.AddComponent<RectTransform>();
        fillAreaRT.SetParent(rt, false);
        fillAreaRT.anchorMin = Vector2.zero;
        fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = Vector2.zero;
        fillAreaRT.offsetMax = Vector2.zero;

        var fillGo = new GameObject("Fill");
        var fillRT = fillGo.AddComponent<RectTransform>();
        fillRT.SetParent(fillAreaRT, false);
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = Vector2.one;
        fillRT.sizeDelta = Vector2.zero;
        var fillImg = fillGo.AddComponent<Image>();
        fillImg.color = new Color(0.4f, 0.7f, 1f);

        // Handle Area
        var handleAreaGo = new GameObject("HandleSlideArea");
        var handleAreaRT = handleAreaGo.AddComponent<RectTransform>();
        handleAreaRT.SetParent(rt, false);
        handleAreaRT.anchorMin = Vector2.zero;
        handleAreaRT.anchorMax = Vector2.one;
        handleAreaRT.offsetMin = Vector2.zero;
        handleAreaRT.offsetMax = Vector2.zero;

        var handleGo = new GameObject("Handle");
        var handleRT = handleGo.AddComponent<RectTransform>();
        handleRT.SetParent(handleAreaRT, false);
        handleRT.sizeDelta = new Vector2(20, 0);
        var handleImg = handleGo.AddComponent<Image>();
        handleImg.color = Color.white;

        // Slider component
        var slider = go.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = initVal;
        slider.onValueChanged.AddListener(onChange);

        return slider;
    }

    // ─── Viewer List Panel ─────────────────────────────────────

    void CreateViewerListPanel()
    {
        // トグルボタン（左上、TopBar下）
        viewerListToggleBtn = new GameObject("ViewerListToggle");
        var tbRT = viewerListToggleBtn.AddComponent<RectTransform>();
        tbRT.SetParent(mainCanvas.transform, false);
        tbRT.anchorMin = new Vector2(0, 1);
        tbRT.anchorMax = new Vector2(0, 1);
        tbRT.pivot = new Vector2(0, 1);
        tbRT.anchoredPosition = new Vector2(5, -65);
        tbRT.sizeDelta = new Vector2(40, 40);
        var tbImg = viewerListToggleBtn.AddComponent<Image>();
        tbImg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        var tbBtn = viewerListToggleBtn.AddComponent<Button>();
        tbBtn.targetGraphic = tbImg;
        var tbc = tbBtn.colors;
        tbc.normalColor = new Color(0.1f, 0.1f, 0.2f, 0.8f);
        tbc.highlightedColor = new Color(0.2f, 0.2f, 0.35f, 0.9f);
        tbc.pressedColor = new Color(0.05f, 0.05f, 0.1f, 1f);
        tbBtn.colors = tbc;
        tbBtn.onClick.AddListener(ToggleViewerList);
        var tbTxt = MakeText("Icon", tbRT, "\u2630", 28,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        tbTxt.color = new Color(0.8f, 0.9f, 1f);
        viewerListToggleBtn.SetActive(false);

        // リストパネル（左サイド、TopBar下～QueuePanel上）
        viewerListPanel = new GameObject("ViewerListPanel");
        var plRT = viewerListPanel.AddComponent<RectTransform>();
        plRT.SetParent(mainCanvas.transform, false);
        plRT.anchorMin = new Vector2(0, 0);
        plRT.anchorMax = new Vector2(0, 1);
        plRT.pivot = new Vector2(0, 1);
        plRT.anchoredPosition = new Vector2(0, -65);
        plRT.sizeDelta = new Vector2(270, -260); // -65 top - 195 bottom
        var plImg = viewerListPanel.AddComponent<Image>();
        plImg.color = new Color(0.05f, 0.05f, 0.12f, 0.88f);

        // ヘッダ
        var header = MakeText("Header", plRT, "LISTENERS", 20,
            new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
            new Vector2(0, -2), new Vector2(0, 28), TextAnchor.MiddleCenter);
        header.color = new Color(0.7f, 0.8f, 1f);

        // ScrollRect + Mask
        var scrollGo = new GameObject("Scroll");
        var scrollRT = scrollGo.AddComponent<RectTransform>();
        scrollRT.SetParent(plRT, false);
        scrollRT.anchorMin = new Vector2(0, 0);
        scrollRT.anchorMax = new Vector2(1, 1);
        scrollRT.pivot = new Vector2(0, 1);
        scrollRT.offsetMin = new Vector2(4, 4);
        scrollRT.offsetMax = new Vector2(-4, -32);
        scrollGo.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        scrollGo.AddComponent<Mask>().showMaskGraphic = false;
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 50f;

        // Content
        var contentGo = new GameObject("Content");
        viewerListContent = contentGo.AddComponent<RectTransform>();
        viewerListContent.SetParent(scrollRT, false);
        viewerListContent.anchorMin = new Vector2(0, 1);
        viewerListContent.anchorMax = new Vector2(1, 1);
        viewerListContent.pivot = new Vector2(0, 1);
        viewerListContent.anchoredPosition = Vector2.zero;
        viewerListContent.sizeDelta = new Vector2(0, 0);
        scroll.content = viewerListContent;

        viewerListPanel.SetActive(false);

        // トグルボタンをパネルより前面に（クリック受付のため）
        viewerListToggleBtn.transform.SetAsLastSibling();

        // 詳細パネル
        CreateViewerDetailPanel();
    }

    void CreateViewerDetailPanel()
    {
        viewerDetailPanel = new GameObject("ViewerDetailPanel");
        var rt = viewerDetailPanel.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = new Vector2(275, -30);
        rt.sizeDelta = new Vector2(320, -260);
        var img = viewerDetailPanel.AddComponent<Image>();
        img.color = new Color(0.08f, 0.06f, 0.15f, 0.92f);

        // キャラプレビューエリア（上部）
        var previewBg = MakePanel("PreviewBg", rt,
            new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
            new Vector2(0, -8), new Vector2(150, 150), new Color(0.02f, 0.02f, 0.06f, 1f));

        var rawImgGo = new GameObject("CharPreview");
        var rawRT = rawImgGo.AddComponent<RectTransform>();
        rawRT.SetParent(previewBg, false);
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.sizeDelta = Vector2.zero;
        detailCharPreview = rawImgGo.AddComponent<RawImage>();
        detailCharPreview.color = Color.white;

        // RenderTexture + Camera
        detailRT = new RenderTexture(256, 256, 16);
        detailRT.filterMode = FilterMode.Point;
        detailCharPreview.texture = detailRT;

        var camGo = new GameObject("DetailPreviewCam");
        camGo.transform.position = new Vector3(100, 100, -10);
        detailPreviewCam = camGo.AddComponent<Camera>();
        detailPreviewCam.orthographic = true;
        detailPreviewCam.orthographicSize = 2.5f;
        detailPreviewCam.targetTexture = detailRT;
        detailPreviewCam.clearFlags = CameraClearFlags.SolidColor;
        detailPreviewCam.backgroundColor = new Color(0.02f, 0.02f, 0.06f, 0f);
        detailPreviewCam.cullingMask = -1;
        detailPreviewCam.depth = -10;
        detailPreviewCam.enabled = false;

        // ステータステキスト（下部）
        detailStatsText = MakeText("StatsText", rt, "", 17,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0),
            new Vector2(10, 8), new Vector2(-20, -170), TextAnchor.UpperLeft);
        detailStatsText.color = new Color(0.9f, 0.9f, 0.95f);
        detailStatsText.lineSpacing = 1.2f;

        // 閉じるボタン
        var closeGo = new GameObject("CloseBtn");
        var closeRT = closeGo.AddComponent<RectTransform>();
        closeRT.SetParent(rt, false);
        closeRT.anchorMin = new Vector2(1, 1);
        closeRT.anchorMax = new Vector2(1, 1);
        closeRT.pivot = new Vector2(1, 1);
        closeRT.anchoredPosition = new Vector2(-4, -4);
        closeRT.sizeDelta = new Vector2(32, 32);
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.6f, 0.15f, 0.15f, 0.9f);
        var closeBtn = closeGo.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(CloseViewerDetail);
        var closeTxt = MakeText("X", closeRT, "\u00D7", 24,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        closeTxt.color = Color.white;

        viewerDetailPanel.SetActive(false);
    }

    void ToggleViewerList()
    {
        if (viewerListPanel == null) return;
        bool show = !viewerListPanel.activeSelf;
        viewerListPanel.SetActive(show);
        if (show) RefreshViewerList();
        if (!show) CloseViewerDetail();
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
    }

    void UpdateViewerList()
    {
        if (viewerListPanel == null || !viewerListPanel.activeSelf) return;
        viewerListRefreshTimer -= Time.deltaTime;
        if (viewerListRefreshTimer <= 0f)
        {
            viewerListRefreshTimer = 2f;
            RefreshViewerList();
        }
    }

    void RefreshViewerList()
    {
        if (ViewerStats.Instance == null || viewerListContent == null) return;

        var ranking = ViewerStats.Instance.GetRanking(50);
        var gm = GameManager.Instance;

        // 行数の調整
        while (viewerListRows.Count < ranking.Count)
        {
            var row = CreateViewerListRow(viewerListRows.Count);
            viewerListRows.Add(row);
        }
        for (int i = ranking.Count; i < viewerListRows.Count; i++)
            viewerListRows[i].SetActive(false);

        const float rowH = 32f;
        viewerListContent.sizeDelta = new Vector2(0, ranking.Count * rowH);

        for (int i = 0; i < ranking.Count; i++)
        {
            var vd = ranking[i];
            var row = viewerListRows[i];
            row.SetActive(true);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchoredPosition = new Vector2(0, -i * rowH);

            // ユニット数
            int unitCount = 0;
            if (gm != null)
                foreach (var u in gm.allyUnits)
                    if (u != null && !u.isDead && u.viewerId == vd.viewerId) unitCount++;

            // テキスト更新
            var txt = row.GetComponentInChildren<Text>();
            if (txt != null)
            {
                string levelStr = vd.bestUnitLevel > 1 ? $"Lv.{vd.bestUnitLevel}" : "";
                string unitStr = unitCount > 0 ? $"x{unitCount}" : "";
                txt.text = $"{vd.ownerName}  <color=#88ccff>{levelStr}</color>  <color=#ffdd44>{vd.score}pt</color>  {unitStr}";
            }

            // 選択中ハイライト
            var bg = row.GetComponent<Image>();
            if (bg != null)
                bg.color = (currentDetailViewerId == vd.viewerId)
                    ? new Color(0.2f, 0.3f, 0.5f, 0.6f)
                    : new Color(0, 0, 0, (i % 2 == 0) ? 0.15f : 0.05f);
        }
    }

    GameObject CreateViewerListRow(int index)
    {
        var row = new GameObject($"Row_{index}");
        var rt = row.AddComponent<RectTransform>();
        rt.SetParent(viewerListContent, false);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(0, 32);

        var img = row.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.1f);

        var btn = row.AddComponent<Button>();
        btn.targetGraphic = img;
        var bc = btn.colors;
        bc.normalColor = img.color;
        bc.highlightedColor = new Color(0.15f, 0.2f, 0.4f, 0.5f);
        bc.pressedColor = new Color(0.1f, 0.15f, 0.3f, 0.7f);
        btn.colors = bc;

        int capturedIdx = index;
        btn.onClick.AddListener(() => OnViewerRowClicked(capturedIdx));

        var txt = MakeText("Text", rt, "", 16,
            new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 0.5f),
            new Vector2(8, 0), new Vector2(-16, 0), TextAnchor.MiddleLeft);
        txt.color = new Color(0.9f, 0.9f, 0.95f);
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        return row;
    }

    void OnViewerRowClicked(int index)
    {
        if (ViewerStats.Instance == null) return;
        var ranking = ViewerStats.Instance.GetRanking(50);
        if (index < 0 || index >= ranking.Count) return;
        ShowViewerDetail(ranking[index].viewerId);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
    }

    static string GetUnitClassName(UnitType type)
    {
        switch (type)
        {
            case UnitType.Warrior: return "せんし";
            case UnitType.Lancer: return "やりつかい";
            case UnitType.Archer: return "ゆみつかい";
            case UnitType.Monk: return "そうりょ";
            case UnitType.Mage: return "まほうつかい";
            case UnitType.Knight: return "きし";
            default: return type.ToString();
        }
    }

    void ShowViewerDetail(string viewerId)
    {
        if (viewerDetailPanel == null || ViewerStats.Instance == null) return;

        var vd = ViewerStats.Instance.GetStats(viewerId);
        if (vd == null) return;

        currentDetailViewerId = viewerId;
        viewerDetailPanel.SetActive(true);

        // ユニット検索
        Unit targetUnit = null;
        var gm = GameManager.Instance;
        if (gm != null)
            foreach (var u in gm.allyUnits)
                if (u != null && !u.isDead && u.viewerId == viewerId) { targetUnit = u; break; }

        // キャラアニメプレビュー
        SetupDetailPreview(targetUnit);

        // ステータステキスト
        string stats = $"<color=#ffdd88><b>{vd.ownerName}</b></color>\n";
        if (targetUnit != null)
        {
            string className = GetUnitClassName(targetUnit.unitType);
            stats += $"しょくぎょう: <color=#ffcc66>{className}</color>  Lv.{targetUnit.level}\n";
            stats += $"HP: {targetUnit.currentHP}/{targetUnit.maxHP}\n";
            stats += $"ATK: {targetUnit.attackPower}  Range: {targetUnit.attackRange:F1}\n";
            stats += $"Speed: {targetUnit.moveSpeed:F1}  AtkSpd: {targetUnit.attackSpeed:F1}s\n";
        }
        else
        {
            stats += $"Best Lv.{vd.bestUnitLevel}\n";
            stats += "(ユニットなし)\n\n";
        }
        stats += $"\n<color=#aaccff>--- Record ---</color>\n";
        stats += $"Score: <color=#ffdd44>{vd.score}</color>\n";
        stats += $"Kills: {vd.kills}  Damage: {vd.damageDealt}\n";
        stats += $"Heals: {vd.healAmount}  Summons: {vd.summonCount}\n";

        // YouTube/TikTok状態
        if (vd.isMember) stats += "<color=#44ff44>YouTube Member</color>\n";
        if (vd.superChatTier >= 0)
            stats += $"<color=#ff8844>SC Tier {vd.superChatTier} (\u00A5{vd.totalSuperChatJPY:N0})</color>\n";
        if (vd.isSubscriber) stats += "<color=#ff44ff>TikTok Subscriber</color>\n";
        if (vd.tiktokGiftTier >= 0)
            stats += $"<color=#ffaa22>Gift Tier {vd.tiktokGiftTier} ({vd.totalGiftCoins}coins)</color>\n";
        if (vd.teamLevel > 0) stats += $"Team Lv.{vd.teamLevel}\n";

        if (detailStatsText != null) detailStatsText.text = stats;

        // リスト行ハイライト更新
        RefreshViewerList();
    }

    void SetupDetailPreview(Unit unit)
    {
        // 旧クローン破棄
        if (detailPreviewClone != null)
        {
            Destroy(detailPreviewClone);
            detailPreviewClone = null;
        }

        if (unit == null || detailPreviewCam == null)
        {
            detailPreviewCam.enabled = false;
            return;
        }

        // ユニットをクローン
        detailPreviewClone = Instantiate(unit.gameObject);
        detailPreviewClone.name = "DetailPreviewClone";
        detailPreviewClone.transform.position = new Vector3(100, 100, 0);
        // レベルアップ等のサイズ変動を無視して常にデフォルトサイズで表示
        float baseS = GameConfig.PixelHeroBaseScale * 2.5f;
        detailPreviewClone.transform.localScale = new Vector3(baseS, baseS, 1f);

        // 不要コンポーネントを無効化
        var cloneUnit = detailPreviewClone.GetComponent<Unit>();
        if (cloneUnit != null) { cloneUnit.enabled = false; Destroy(cloneUnit); }
        var cols = detailPreviewClone.GetComponentsInChildren<Collider2D>();
        foreach (var c in cols) c.enabled = false;

        // テキスト系を削除
        var meshes = detailPreviewClone.GetComponentsInChildren<TextMesh>();
        foreach (var m in meshes) Destroy(m.gameObject);

        // Animator Idle
        var anim = detailPreviewClone.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.SetBool("Idle", true);
            anim.SetBool("Run", false);
            anim.SetBool("Die", false);
        }

        // flipXをリセット
        var sr = detailPreviewClone.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.flipX = false;

        detailPreviewCam.enabled = true;
    }

    void CloseViewerDetail()
    {
        if (viewerDetailPanel != null) viewerDetailPanel.SetActive(false);
        currentDetailViewerId = null;
        if (detailPreviewClone != null)
        {
            Destroy(detailPreviewClone);
            detailPreviewClone = null;
        }
        if (detailPreviewCam != null) detailPreviewCam.enabled = false;
    }

    // ─── Tutorial ─────────────────────────────────────────────

    static readonly string[] TutorialTexts = new string[]
    {
        "Streamer King へようこそ！\nチャットコマンドでユニットを 召喚 し\n 敵 の 城 を 攻 め 落 とそう！",
        " 視聴者 がチャットで「けん」「ゆみ」「かいふく」などと\nうつとここにユニットが 並 びます",
        "ゴールドで NPC ユニットを 購入 できます\n 視聴者 がいないときも 戦力 を 補強 しよう",
        " 準備 ができたらスタートボタンで\nWave 開始 ！ 敵 が 攻 めてきます",
        "チュートリアル 完了 ！\nがんばって 敵 の 城 を 落 とそう！"
    };

    // デフォルト位置（anchor=center 0.5,0.5 基準）
    static readonly Vector2[] TutorialDefaultPositions = new Vector2[]
    {
        new Vector2(0, 0),
        new Vector2(0, -300),
        new Vector2(0, -180),
        new Vector2(0, -60),
        new Vector2(0, 0)
    };

    void LoadTutorialPositions()
    {
        tutorialPositions = new Vector2[5];
        tutorialArrowPositions = new Vector2[5];
        for (int i = 0; i < 5; i++)
        {
            tutorialPositions[i] = TutorialDefaultPositions[i];
            tutorialArrowPositions[i] = new Vector2(float.MaxValue, float.MaxValue); // 未設定マーカー
        }

        string json = PlayerPrefs.GetString("TutorialPositions", "");
        if (!string.IsNullOrEmpty(json))
        {
            var data = JsonUtility.FromJson<TutorialPosData>(json);
            if (data != null && data.x != null && data.x.Length == 5)
            {
                for (int i = 0; i < 5; i++)
                    tutorialPositions[i] = new Vector2(data.x[i], data.y[i]);
            }
            // 矢印位置（保存されていれば）
            if (data != null && data.ax != null && data.ax.Length == 5)
            {
                for (int i = 0; i < 5; i++)
                    tutorialArrowPositions[i] = new Vector2(data.ax[i], data.ay[i]);
            }
        }
    }

    void SaveTutorialPositions()
    {
        var data = new TutorialPosData
        {
            x = new float[5], y = new float[5],
            ax = new float[5], ay = new float[5]
        };
        for (int i = 0; i < 5; i++)
        {
            data.x[i] = tutorialPositions[i].x;
            data.y[i] = tutorialPositions[i].y;
            data.ax[i] = tutorialArrowPositions[i].x;
            data.ay[i] = tutorialArrowPositions[i].y;
        }
        PlayerPrefs.SetString("TutorialPositions", JsonUtility.ToJson(data));
        PlayerPrefs.Save();

        string log = "[StreamerKing] Tutorial positions saved:";
        for (int i = 0; i < 5; i++)
        {
            string arrowInfo = tutorialArrowPositions[i].x < 9999f
                ? $" arrow:({tutorialArrowPositions[i].x:F0},{tutorialArrowPositions[i].y:F0})"
                : " arrow:auto";
            log += $"\n  Step{i}: bubble({tutorialPositions[i].x:F0},{tutorialPositions[i].y:F0}){arrowInfo}";
        }
        Debug.Log(log);
    }

    [System.Serializable]
    class TutorialPosData { public float[] x; public float[] y; public float[] ax; public float[] ay; }

    IEnumerator DelayedStartTutorial()
    {
        yield return new WaitForSeconds(1.0f);
        var gm = GameManager.Instance;
        if (gm == null || gm.currentPhase != GamePhase.Preparation) yield break;
        Debug.Log("[StreamerKing] Starting tutorial");
        StartTutorial();
    }

    public void ResetTutorialFlag()
    {
        PlayerPrefs.DeleteKey("TutorialComplete");
        PlayerPrefs.Save();
        Debug.Log("[StreamerKing] Tutorial flag reset");
    }

    // ─── 通常チュートリアル ───

    void StartTutorial()
    {
        tutorialEditMode = false;
        tutorialStep = 0;
        LoadTutorialPositions();
        CreateTutorialOverlay();
        ShowTutorialStep(0);
    }

    void CreateTutorialOverlay()
    {
        tutorialOverlay = new GameObject("TutorialOverlay");
        var rt = tutorialOverlay.AddComponent<RectTransform>();
        rt.SetParent(mainCanvas.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        var overlayImg = tutorialOverlay.AddComponent<Image>();
        overlayImg.color = new Color(0, 0, 0, 0.15f);
        overlayImg.raycastTarget = true;
        rt.SetAsLastSibling();
    }

    void ClearOverlayChildren()
    {
        var overlayRT = tutorialOverlay.GetComponent<RectTransform>();
        for (int i = overlayRT.childCount - 1; i >= 0; i--)
            Destroy(overlayRT.GetChild(i).gameObject);
    }

    void ShowTutorialStep(int step)
    {
        tutorialStep = step;
        ClearOverlayChildren();

        Vector2 pos = tutorialPositions[step];
        bool pointerDown = step >= 1 && step <= 3;
        string btnLabel = step == 4 ? "閉じる" : "次へ";

        CreateTutorialBubble(tutorialOverlay.GetComponent<RectTransform>(),
            pos, TutorialTexts[step], pointerDown, btnLabel, false);
    }

    void CreateTutorialBubble(RectTransform parent, Vector2 pos,
        string text, bool pointerDown, string btnLabel, bool editMode)
    {
        // 吹き出しコンテナ（anchor常にcenter）
        var bubbleGo = new GameObject("TutorialBubble");
        var bubbleRT = bubbleGo.AddComponent<RectTransform>();
        bubbleRT.SetParent(parent, false);
        bubbleRT.anchorMin = new Vector2(0.5f, 0.5f);
        bubbleRT.anchorMax = new Vector2(0.5f, 0.5f);
        bubbleRT.pivot = new Vector2(0.5f, 0.5f);
        bubbleRT.anchoredPosition = pos;
        bubbleRT.sizeDelta = new Vector2(560, 200);

        if (editMode) tutorialEditBubbleRT = bubbleRT;

        // 背景パネル
        var bgImg = bubbleGo.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.95f);

        // テキスト
        var txtGo = new GameObject("Text");
        var txtRT = txtGo.AddComponent<RectTransform>();
        txtRT.SetParent(bubbleRT, false);
        txtRT.anchorMin = new Vector2(0.05f, 0.25f);
        txtRT.anchorMax = new Vector2(0.95f, 0.95f);
        txtRT.sizeDelta = Vector2.zero;
        txtRT.anchoredPosition = Vector2.zero;
        var txt = txtGo.AddComponent<Text>();
        txt.font = GetFont();
        txt.text = text;
        txt.fontSize = 28;
        txt.color = new Color(0.1f, 0.1f, 0.1f);
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.lineSpacing = 1.4f;

        // ターゲットUIへの矢印（通常モード・エディターモード両方で表示）
        CreateTutorialArrow(parent, bubbleRT);

        if (editMode)
        {
            // エディットモード: ボタンなし（Update()でマウスドラッグ処理）
        }
        else
        {
            // 通常モード: 操作ボタン
            // 「次へ」/「閉じる」ボタン（右下）
            var nextBtnGo = new GameObject("NextBtn");
            var nextRT = nextBtnGo.AddComponent<RectTransform>();
            nextRT.SetParent(bubbleRT, false);
            nextRT.anchorMin = new Vector2(1f, 0f);
            nextRT.anchorMax = new Vector2(1f, 0f);
            nextRT.pivot = new Vector2(1f, 0f);
            nextRT.anchoredPosition = new Vector2(-12, 10);
            nextRT.sizeDelta = new Vector2(100, 40);
            var nextImg = nextBtnGo.AddComponent<Image>();
            nextImg.color = new Color(0.2f, 0.5f, 0.9f);
            var nextBtn = nextBtnGo.AddComponent<Button>();
            nextBtn.targetGraphic = nextImg;
            var nav = nextBtn.navigation;
            nav.mode = Navigation.Mode.None;
            nextBtn.navigation = nav;
            nextBtn.onClick.AddListener(NextTutorialStep);

            var nextTxt = new GameObject("Text").AddComponent<Text>();
            nextTxt.transform.SetParent(nextRT, false);
            var ntr = nextTxt.GetComponent<RectTransform>();
            ntr.anchorMin = Vector2.zero; ntr.anchorMax = Vector2.one; ntr.sizeDelta = Vector2.zero;
            nextTxt.font = GetFont(); nextTxt.text = btnLabel;
            nextTxt.fontSize = 22; nextTxt.color = Color.white;
            nextTxt.alignment = TextAnchor.MiddleCenter;

            // 「スキップ」ボタン（右上、最終ステップ以外）
            if (tutorialStep < 4)
            {
                var skipGo = new GameObject("SkipBtn");
                var skipRT = skipGo.AddComponent<RectTransform>();
                skipRT.SetParent(bubbleRT, false);
                skipRT.anchorMin = new Vector2(1f, 1f);
                skipRT.anchorMax = new Vector2(1f, 1f);
                skipRT.pivot = new Vector2(1f, 1f);
                skipRT.anchoredPosition = new Vector2(-8, -4);
                skipRT.sizeDelta = new Vector2(80, 28);
                var skipImg = skipGo.AddComponent<Image>();
                skipImg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                var skipBtn = skipGo.AddComponent<Button>();
                skipBtn.targetGraphic = skipImg;
                var sn = skipBtn.navigation; sn.mode = Navigation.Mode.None; skipBtn.navigation = sn;
                skipBtn.onClick.AddListener(SkipTutorial);

                var skipTxt = new GameObject("Text").AddComponent<Text>();
                skipTxt.transform.SetParent(skipRT, false);
                var str = skipTxt.GetComponent<RectTransform>();
                str.anchorMin = Vector2.zero; str.anchorMax = Vector2.one; str.sizeDelta = Vector2.zero;
                skipTxt.font = GetFont(); skipTxt.text = "スキップ";
                skipTxt.fontSize = 16; skipTxt.color = Color.white;
                skipTxt.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    void NextTutorialStep()
    {
        if (tutorialStep >= 4) { CompleteTutorial(); return; }
        ShowTutorialStep(tutorialStep + 1);
    }

    void SkipTutorial() { CompleteTutorial(); }

    void CompleteTutorial()
    {
        tutorialStep = -1;
        tutorialDragging = false;
        tutorialDraggingArrow = false;
        tutorialEditBubbleRT = null;
        tutorialEditArrowHandleRT = null;
        tutorialEditPosLabel = null;
        tutorialArrowGroup = null;
        if (tutorialOverlay != null) { Destroy(tutorialOverlay); tutorialOverlay = null; }
        if (!tutorialEditMode)
        {
            PlayerPrefs.SetInt("TutorialComplete", 1);
            PlayerPrefs.Save();
        }
        tutorialEditMode = false;
    }

    // ─── チュートリアル位置エディター ───

    void StartTutorialEditor()
    {
        tutorialEditMode = true;
        tutorialStep = 0;
        LoadTutorialPositions();
        CreateTutorialOverlay();
        ShowTutorialEditorStep(0);
    }

    void ShowTutorialEditorStep(int step)
    {
        tutorialStep = step;
        tutorialDraggingArrow = false;
        tutorialEditArrowHandleRT = null;
        ClearOverlayChildren();

        var overlayRT = tutorialOverlay.GetComponent<RectTransform>();
        Vector2 pos = tutorialPositions[step];
        bool pointerDown = step >= 1 && step <= 3;

        // 吹き出し（ドラッグ可能）
        CreateTutorialBubble(overlayRT, pos, TutorialTexts[step], pointerDown, "", true);

        // 矢印先端ハンドル（矢印があるステップのみ）
        Vector2 arrowEnd = GetArrowEndPos(overlayRT);
        if (arrowEnd.x < 9999f)
        {
            var handleGo = new GameObject("ArrowHandle");
            tutorialEditArrowHandleRT = handleGo.AddComponent<RectTransform>();
            tutorialEditArrowHandleRT.SetParent(overlayRT, false);
            tutorialEditArrowHandleRT.anchorMin = new Vector2(0.5f, 0.5f);
            tutorialEditArrowHandleRT.anchorMax = new Vector2(0.5f, 0.5f);
            tutorialEditArrowHandleRT.pivot = new Vector2(0.5f, 0.5f);
            tutorialEditArrowHandleRT.anchoredPosition = arrowEnd;
            tutorialEditArrowHandleRT.sizeDelta = new Vector2(30, 30);
            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            handleImg.raycastTarget = false;
            // ●マーク
            var markTxt = new GameObject("Mark").AddComponent<Text>();
            markTxt.transform.SetParent(tutorialEditArrowHandleRT, false);
            var mrt = markTxt.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero; mrt.anchorMax = Vector2.one; mrt.sizeDelta = Vector2.zero;
            markTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            markTxt.text = "\u25CE"; // ◎
            markTxt.fontSize = 22;
            markTxt.color = new Color(0.3f, 0.15f, 0f);
            markTxt.alignment = TextAnchor.MiddleCenter;
        }

        // --- エディターUI（オーバーレイ上部に固定） ---
        var barGo = new GameObject("EditorBar");
        var barRT = barGo.AddComponent<RectTransform>();
        barRT.SetParent(overlayRT, false);
        barRT.anchorMin = new Vector2(0f, 1f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0, 50);
        barGo.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        // ステップ表示
        string hasArrow = arrowEnd.x < 9999f ? " [矢印あり]" : "";
        MakeText("StepLabel", barRT, $"Step {step + 1} / {TutorialTexts.Length}{hasArrow}", 22,
            new Vector2(0, 0), new Vector2(0.3f, 1), new Vector2(0, 0.5f),
            new Vector2(15, 0), Vector2.zero, TextAnchor.MiddleLeft);

        // 座標表示（吹き出し + 矢印）
        string posStr = $"吹({pos.x:F0},{pos.y:F0})";
        if (arrowEnd.x < 9999f)
            posStr += $" 矢({arrowEnd.x:F0},{arrowEnd.y:F0})";
        tutorialEditPosLabel = MakeText("PosLabel", barRT, posStr, 16,
            new Vector2(0.3f, 0), new Vector2(0.6f, 1), new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        tutorialEditPosLabel.color = new Color(0.7f, 0.9f, 1f);

        // 「セーブ＆つぎへ」/「セーブ＆おわり」ボタン
        bool isLast = step >= TutorialTexts.Length - 1;
        string saveBtnText = isLast ? "セーブ＆おわり" : "セーブ＆つぎへ";
        var saveBtnGo = new GameObject("SaveNextBtn");
        var saveBtnRT = saveBtnGo.AddComponent<RectTransform>();
        saveBtnRT.SetParent(barRT, false);
        saveBtnRT.anchorMin = new Vector2(1f, 0f);
        saveBtnRT.anchorMax = new Vector2(1f, 1f);
        saveBtnRT.pivot = new Vector2(1f, 0.5f);
        saveBtnRT.anchoredPosition = new Vector2(-10, 0);
        saveBtnRT.sizeDelta = new Vector2(200, -10);
        saveBtnGo.AddComponent<Image>().color = new Color(0.2f, 0.6f, 0.3f, 1f);
        var saveBtn = saveBtnGo.AddComponent<Button>();
        saveBtn.targetGraphic = saveBtnGo.GetComponent<Image>();
        var sn2 = saveBtn.navigation; sn2.mode = Navigation.Mode.None; saveBtn.navigation = sn2;
        saveBtn.onClick.AddListener(OnEditorSaveNext);
        MakeText("Text", saveBtnRT, saveBtnText, 20,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);

        // 「キャンセル」ボタン
        var cancelGo = new GameObject("CancelBtn");
        var cancelRT = cancelGo.AddComponent<RectTransform>();
        cancelRT.SetParent(barRT, false);
        cancelRT.anchorMin = new Vector2(1f, 0f);
        cancelRT.anchorMax = new Vector2(1f, 1f);
        cancelRT.pivot = new Vector2(1f, 0.5f);
        cancelRT.anchoredPosition = new Vector2(-220, 0);
        cancelRT.sizeDelta = new Vector2(130, -10);
        cancelGo.AddComponent<Image>().color = new Color(0.5f, 0.2f, 0.2f, 1f);
        var cancelBtn = cancelGo.AddComponent<Button>();
        cancelBtn.targetGraphic = cancelGo.GetComponent<Image>();
        var cn = cancelBtn.navigation; cn.mode = Navigation.Mode.None; cancelBtn.navigation = cn;
        cancelBtn.onClick.AddListener(OnEditorCancel);
        MakeText("Text", cancelRT, "キャンセル", 18,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
    }

    void OnEditorSaveNext()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        // 現在のステップの吹き出し位置を保存
        if (tutorialEditBubbleRT != null)
            tutorialPositions[tutorialStep] = tutorialEditBubbleRT.anchoredPosition;
        // 矢印先端位置を保存
        if (tutorialEditArrowHandleRT != null)
            tutorialArrowPositions[tutorialStep] = tutorialEditArrowHandleRT.anchoredPosition;

        if (tutorialStep >= TutorialTexts.Length - 1)
        {
            // 全ステップ完了 → 保存して閉じる
            SaveTutorialPositions();
            CompleteTutorial();
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("WaveClear");
        }
        else
        {
            ShowTutorialEditorStep(tutorialStep + 1);
        }
    }

    void OnEditorCancel()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
        CompleteTutorial();
    }

    // ─── チュートリアル矢印 ───

    RectTransform GetTutorialTargetRT()
    {
        switch (tutorialStep)
        {
            case 1: return queuePanel;
            case 2: return shopPanel != null ? shopPanel.GetComponent<RectTransform>() : null;
            case 3: return waveStartButton != null ? waveStartButton.GetComponent<RectTransform>() : null;
            default: return null;
        }
    }

    // 矢印先端の位置を取得（保存済み or ターゲットUIから自動算出）
    Vector2 GetArrowEndPos(RectTransform overlayRT)
    {
        if (tutorialArrowPositions != null && tutorialArrowPositions[tutorialStep].x < 9999f)
            return tutorialArrowPositions[tutorialStep];
        // 未設定→ターゲットUIから自動算出
        var targetRT = GetTutorialTargetRT();
        if (targetRT == null) return new Vector2(float.MaxValue, float.MaxValue);
        return (Vector2)overlayRT.InverseTransformPoint(targetRT.position);
    }

    void CreateTutorialArrow(RectTransform parent, RectTransform bubbleRT)
    {
        // 既存の矢印グループを破棄
        if (tutorialArrowGroup != null) Destroy(tutorialArrowGroup);
        tutorialArrowGroup = null;

        Vector2 arrowEnd = GetArrowEndPos(parent);
        if (arrowEnd.x > 9999f) return; // このステップは矢印なし

        Vector2 bubblePos = bubbleRT.anchoredPosition;
        Vector2 dir = arrowEnd - bubblePos;
        float dist = dir.magnitude;
        if (dist < 10f) return;
        dir.Normalize();

        // グループコンテナ作成
        tutorialArrowGroup = new GameObject("TutorialArrowGroup");
        var groupRT = tutorialArrowGroup.AddComponent<RectTransform>();
        groupRT.SetParent(parent, false);
        groupRT.anchorMin = Vector2.zero; groupRT.anchorMax = Vector2.one;
        groupRT.sizeDelta = Vector2.zero;
        var cg = tutorialArrowGroup.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false;
        cg.interactable = false;

        // バブル端から開始
        float bubbleRadius = bubbleRT.sizeDelta.y * 0.5f + 5f;
        if (bubbleRadius >= dist - 20f) bubbleRadius = 10f;
        Vector2 startPos = bubblePos + dir * bubbleRadius;
        Vector2 endPos = arrowEnd;
        float lineLen = Vector2.Distance(startPos, endPos);
        if (lineLen < 5f) { Destroy(tutorialArrowGroup); tutorialArrowGroup = null; return; }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Color arrowColor = new Color(1f, 0.85f, 0.2f, 0.95f);

        // 矢印の棒
        var lineGo = new GameObject("ArrowLine");
        var lineRT = lineGo.AddComponent<RectTransform>();
        lineRT.SetParent(groupRT, false);
        lineRT.anchorMin = new Vector2(0.5f, 0.5f);
        lineRT.anchorMax = new Vector2(0.5f, 0.5f);
        lineRT.pivot = new Vector2(0f, 0.5f);
        lineRT.anchoredPosition = startPos;
        lineRT.sizeDelta = new Vector2(lineLen, 4f);
        lineRT.localRotation = Quaternion.Euler(0, 0, angle);
        var lineImg = lineGo.AddComponent<Image>();
        lineImg.color = arrowColor;
        lineImg.raycastTarget = false;

        // 矢じり（2本の短い線で「>」形状）
        float wingLen = 18f;
        float wingAngle = 30f;
        for (int i = 0; i < 2; i++)
        {
            float sign = i == 0 ? 1f : -1f;
            float waDeg = angle + 180f + sign * wingAngle;
            var wingGo = new GameObject($"ArrowWing{i}");
            var wingRT = wingGo.AddComponent<RectTransform>();
            wingRT.SetParent(groupRT, false);
            wingRT.anchorMin = new Vector2(0.5f, 0.5f);
            wingRT.anchorMax = new Vector2(0.5f, 0.5f);
            wingRT.pivot = new Vector2(0f, 0.5f);
            wingRT.anchoredPosition = endPos;
            wingRT.sizeDelta = new Vector2(wingLen, 4f);
            wingRT.localRotation = Quaternion.Euler(0, 0, waDeg);
            var wingImg = wingGo.AddComponent<Image>();
            wingImg.color = arrowColor;
            wingImg.raycastTarget = false;
        }
    }

    void UpdateTutorialEditDrag()
    {
        if (!tutorialEditMode || tutorialEditBubbleRT == null || tutorialOverlay == null) return;

        var overlayRT = tutorialOverlay.GetComponent<RectTransform>();

        if (Input.GetMouseButtonDown(0))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRT, Input.mousePosition, null, out Vector2 localMouse);

            // 矢印ハンドルを先に判定（小さいので優先）
            if (tutorialEditArrowHandleRT != null)
            {
                var aPos = tutorialEditArrowHandleRT.anchoredPosition;
                float aHalf = 20f; // 判定範囲を少し広め
                if (localMouse.x >= aPos.x - aHalf && localMouse.x <= aPos.x + aHalf &&
                    localMouse.y >= aPos.y - aHalf && localMouse.y <= aPos.y + aHalf)
                {
                    tutorialDraggingArrow = true;
                    tutorialDragOffset = aPos - localMouse;
                    return;
                }
            }

            // バブルの矩形判定
            var bPos = tutorialEditBubbleRT.anchoredPosition;
            var bSize = tutorialEditBubbleRT.sizeDelta;
            float halfW = bSize.x * 0.5f, halfH = bSize.y * 0.5f;
            if (localMouse.x >= bPos.x - halfW && localMouse.x <= bPos.x + halfW &&
                localMouse.y >= bPos.y - halfH && localMouse.y <= bPos.y + halfH)
            {
                tutorialDragging = true;
                tutorialDragOffset = bPos - localMouse;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            tutorialDragging = false;
            tutorialDraggingArrow = false;
        }

        // 矢印ハンドルドラッグ中
        if (tutorialDraggingArrow && Input.GetMouseButton(0) && tutorialEditArrowHandleRT != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRT, Input.mousePosition, null, out Vector2 lp);
            tutorialEditArrowHandleRT.anchoredPosition = lp + tutorialDragOffset;
            // 座標ラベル更新
            UpdateEditorPosLabel();
            // 矢印をリアルタイム更新（先端位置を一時的に反映）
            tutorialArrowPositions[tutorialStep] = tutorialEditArrowHandleRT.anchoredPosition;
            CreateTutorialArrow(overlayRT, tutorialEditBubbleRT);
        }

        // バブルドラッグ中
        if (tutorialDragging && Input.GetMouseButton(0))
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                overlayRT, Input.mousePosition, null, out Vector2 lp);
            tutorialEditBubbleRT.anchoredPosition = lp + tutorialDragOffset;
            UpdateEditorPosLabel();
            CreateTutorialArrow(overlayRT, tutorialEditBubbleRT);
        }
    }

    void UpdateEditorPosLabel()
    {
        if (tutorialEditPosLabel == null) return;
        var bp = tutorialEditBubbleRT.anchoredPosition;
        string s = $"吹({bp.x:F0},{bp.y:F0})";
        if (tutorialEditArrowHandleRT != null)
        {
            var ap = tutorialEditArrowHandleRT.anchoredPosition;
            s += $" 矢({ap.x:F0},{ap.y:F0})";
        }
        tutorialEditPosLabel.text = s;
    }

    // ─── 3D Camera Preview ──────────────────────────────────────

    void Create3DPreviewPanel()
    {
        // 右下にプレビューパネル（320x180）
        preview3DPanel = new GameObject("3DPreviewPanel");
        var panelRT = preview3DPanel.AddComponent<RectTransform>();
        panelRT.SetParent(mainCanvas.transform, false);
        panelRT.anchorMin = new Vector2(1, 0);
        panelRT.anchorMax = new Vector2(1, 0);
        panelRT.pivot = new Vector2(1, 0);
        panelRT.anchoredPosition = new Vector2(-20, 20);
        panelRT.sizeDelta = new Vector2(328, 188); // 320+8border, 180+8border

        // フレーム背景
        var frameBg = preview3DPanel.AddComponent<Image>();
        frameBg.color = new Color(0.1f, 0.1f, 0.15f, 0.9f);

        // RawImage（RenderTexture表示）
        var rawGo = new GameObject("Preview3DRaw");
        var rawRT = rawGo.AddComponent<RectTransform>();
        rawRT.SetParent(panelRT, false);
        rawRT.anchorMin = Vector2.zero;
        rawRT.anchorMax = Vector2.one;
        rawRT.offsetMin = new Vector2(4, 4);
        rawRT.offsetMax = new Vector2(-4, -4);

        preview3DImage = rawGo.AddComponent<RawImage>();
        preview3DImage.color = Color.white;

        // ボタン化（クリックでフルスクリーン切替）
        var btn = rawGo.AddComponent<Button>();
        btn.targetGraphic = preview3DImage;
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.9f, 0.9f, 1f, 1f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            CameraView3D.Instance?.ToggleFullScreen();
        });

        // 「3D」ラベル
        var labelText = MakeText("3DLabel", panelRT, "3D",
            18, Vector2.one, Vector2.one, Vector2.one,
            new Vector2(-8, -6), new Vector2(40, 24), TextAnchor.UpperRight);
        labelText.color = new Color(1f, 0.9f, 0.4f, 0.9f);

        // 初期非表示
        preview3DPanel.SetActive(false);

        // 「2Dにもどす」ボタン（フルスクリーン時のみ表示）
        var backGo = new GameObject("BackTo2DBtn");
        var backRT = backGo.AddComponent<RectTransform>();
        backRT.SetParent(mainCanvas.transform, false);
        backRT.anchorMin = new Vector2(1, 1);
        backRT.anchorMax = new Vector2(1, 1);
        backRT.pivot = new Vector2(1, 1);
        backRT.anchoredPosition = new Vector2(-20, -20);
        backRT.sizeDelta = new Vector2(160, 44);

        var backBg = backGo.AddComponent<Image>();
        backBg.color = new Color(0.15f, 0.15f, 0.2f, 0.85f);

        var backBtnComp = backGo.AddComponent<Button>();
        backBtnComp.targetGraphic = backBg;
        backBtnComp.onClick.AddListener(() =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("ButtonClick");
            CameraView3D.Instance?.ToggleFullScreen();
        });

        var backLabel = MakeText("BackLabel", backRT, "2Dにもどす",
            22, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, TextAnchor.MiddleCenter);
        backLabel.color = Color.white;

        backTo2DBtn = backGo;
        backTo2DBtn.SetActive(false);

        // フォロー対象名テキスト（右上、backTo2Dの下）
        followTargetText = MakeText("FollowTargetText", mainCanvas.transform, "",
            24, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1),
            new Vector2(-20, -70), new Vector2(300, 36), TextAnchor.MiddleRight);
        followTargetText.color = new Color(1f, 1f, 0.8f, 0.95f);

        // 背景パネル
        var ftBg = new GameObject("FollowTargetBg");
        var ftBgRT = ftBg.AddComponent<RectTransform>();
        ftBgRT.SetParent(followTargetText.transform, false);
        ftBgRT.anchorMin = Vector2.zero;
        ftBgRT.anchorMax = Vector2.one;
        ftBgRT.offsetMin = new Vector2(-8, -4);
        ftBgRT.offsetMax = new Vector2(8, 4);
        ftBg.transform.SetAsFirstSibling();
        var ftBgImg = ftBg.AddComponent<Image>();
        ftBgImg.color = new Color(0.1f, 0.1f, 0.15f, 0.7f);
        ftBgImg.raycastTarget = false;

        // 操作説明テキスト
        var helpText = MakeText("3DHelpText", followTargetText.transform, "右ドラッグ:視点  中クリック/Tab:切替  スクロール:ズーム",
            14, Vector2.zero, Vector2.one, new Vector2(1, 1),
            new Vector2(0, -32), new Vector2(400, 24), TextAnchor.MiddleRight);
        helpText.color = new Color(0.8f, 0.8f, 0.8f, 0.7f);

        followTargetText.gameObject.SetActive(false);
    }

    /// <summary>CameraView3Dからのフルスクリーン切替通知</summary>
    public void On3DFullScreenChanged(bool isFullScreen)
    {
        // プレビューパネル非表示 / 表示
        if (preview3DPanel != null) preview3DPanel.SetActive(!isFullScreen);
        // 「2Dにもどす」ボタン + フォロー対象名
        if (backTo2DBtn != null) backTo2DBtn.SetActive(isFullScreen);
        if (followTargetText != null)
        {
            followTargetText.gameObject.SetActive(isFullScreen);
            if (isFullScreen && CameraView3D.Instance != null)
                followTargetText.text = CameraView3D.Instance.FollowTargetName;
        }
    }

    public void UpdateFollowTargetName(string name)
    {
        if (followTargetText != null)
        {
            followTargetText.text = name;
            followTargetText.gameObject.SetActive(!string.IsNullOrEmpty(name) && CameraView3D.is3DFullScreen);
        }
    }

    void LateUpdate3DPreview()
    {
        // RenderTextureをRawImageに反映
        if (preview3DImage != null && CameraView3D.Instance != null && preview3DPanel.activeSelf)
        {
            var tex = CameraView3D.Instance.PreviewTexture;
            if (tex != null && preview3DImage.texture != tex)
                preview3DImage.texture = tex;
        }
    }

    // ─── Helpers ────────────────────────────────────────────────

    RectTransform MakePanel(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        go.AddComponent<Image>().color = color;
        return rt;
    }

    Text MakeText(string name, Transform parent, string text, int fontSize,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta, TextAnchor alignment)
    {
        var go = new GameObject(name);
        var rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        var t = go.AddComponent<Text>();
        t.font = GetFont();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = alignment;
        t.color = Color.white;
        t.supportRichText = true;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}

/// <summary>Legacy Text用の文字間スペース（BaseMeshEffectで頂点を加工）</summary>
public class LetterSpacing : UnityEngine.UI.BaseMeshEffect
{
    public float spacing = 1.5f;
    public float kanjiExtra = 2.5f;
    private string glyphText;

    public override void ModifyMesh(UnityEngine.UI.VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0) return;

        var text = GetComponent<UnityEngine.UI.Text>();
        if (text != null) glyphText = text.text.Replace("\n", "");

        var verts = new System.Collections.Generic.List<UIVertex>();
        vh.GetUIVertexStream(verts);

        int glyphCount = verts.Count / 6;
        if (glyphCount <= 1) return;

        float lineY = verts[0].position.y;
        int lineStart = 0;

        for (int i = 1; i < glyphCount; i++)
        {
            float charY = verts[i * 6].position.y;
            if (Mathf.Abs(charY - lineY) > 2f)
            {
                ApplyLine(verts, lineStart, i);
                lineStart = i;
                lineY = charY;
            }
        }
        ApplyLine(verts, lineStart, glyphCount);

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }

    void ApplyLine(System.Collections.Generic.List<UIVertex> verts, int start, int end)
    {
        int count = end - start;
        if (count <= 1) return;

        // 各文字間のスペーシングを計算（漢字の隣は広め）
        float totalGap = 0;
        for (int i = start; i < end - 1; i++)
            totalGap += GapAt(i);

        float halfTotal = totalGap * 0.5f;
        float cumOffset = -halfTotal;
        for (int i = start; i < end; i++)
        {
            for (int j = 0; j < 6; j++)
            {
                int idx = i * 6 + j;
                var v = verts[idx];
                v.position += new Vector3(cumOffset, 0, 0);
                verts[idx] = v;
            }
            if (i < end - 1) cumOffset += GapAt(i);
        }
    }

    float GapAt(int glyphIdx)
    {
        if (glyphText == null || glyphIdx >= glyphText.Length || glyphIdx + 1 >= glyphText.Length)
            return spacing;
        char a = glyphText[glyphIdx], b = glyphText[glyphIdx + 1];
        if (IsKanji(a) || IsKanji(b)) return spacing + kanjiExtra;
        return spacing;
    }

    static bool IsKanji(char c)
    {
        return (c >= '\u4E00' && c <= '\u9FFF') || (c >= '\u3400' && c <= '\u4DBF');
    }
}

