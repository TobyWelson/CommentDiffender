using UnityEngine;

public enum GameCursorState { Normal, Selectable, CannotPlace, CanPlace }

public class DragDropController : MonoBehaviour
{
    private Camera mainCam;
    private Unit draggedUnit;
    private bool isDragging;
    private bool dragFromQueue;      // true=キューから, false=配置済み再配置
    private Vector3 dragOriginalPos; // 再配置時の元の位置

    // ドラッグ中ユニットの元情報（キューからのドロップ時にゾーン外→キューへ戻す用）
    private UnitType draggedType;
    private string draggedOwner;
    private string draggedViewerId;
    private HeroAppearance draggedAppearance;
    private Sprite draggedPreviewSprite;

    // Placement zone visuals
    private GameObject zoneRoot;
    private SpriteRenderer placementZoneVisual;
    private GameObject guideTextGo;
    private float guideTextAlpha = 0f;
    private float guideTextTargetAlpha = 0.7f;

    // 初回ヒント
    private GameObject hintBubbleGo;
    private bool hasEverDragged = false;
    private float hintArrowTimer;

    // カーソル
    private Texture2D cursorNormal;
    private Texture2D cursorSelectable;
    private Texture2D cursorCannotPlace;
    private Texture2D cursorCanPlace;
    private GameCursorState currentCursor = (GameCursorState)(-1);
    private bool uiRequestsSelectable; // UIManagerから1フレームだけセットされる

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

    void Start()
    {
        mainCam = Camera.main;
        LoadCursors();
        CreatePlacementZoneVisual();
        CreateHintBubble();
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        bool canDrag = gm.currentPhase == GamePhase.Preparation || gm.currentPhase == GamePhase.Battle;
        bool isPrep = gm.currentPhase == GamePhase.Preparation;

        // ゾーンは準備中・バトル中は常に表示
        if (zoneRoot != null) zoneRoot.SetActive(canDrag);

        // ガイドテキスト: 味方がいなければフェードイン、いればフェードアウト
        if (guideTextGo != null)
        {
            if (!isPrep)
            {
                guideTextGo.SetActive(false);
            }
            else
            {
                guideTextGo.SetActive(true);
                var allies = gm.GetUnits(Team.Ally);
                bool hasAllies = allies != null && allies.Count > 0;
                float target = hasAllies ? 0f : 0.7f;
                guideTextAlpha = Mathf.MoveTowards(guideTextAlpha, target, Time.deltaTime * 2f);
                var tm = guideTextGo.GetComponent<TextMesh>();
                if (tm != null)
                    tm.color = new Color(0.7f, 0.85f, 1f, guideTextAlpha);
            }
        }

        if (!canDrag)
        {
            if (hintBubbleGo != null) hintBubbleGo.SetActive(false);
            return;
        }

        // 初回ヒント: 準備中、キューにユニットがいて、まだ一度もドラッグしていない時
        bool showHint = isPrep && !hasEverDragged && !isDragging
            && gm.GetQueue() != null && gm.GetQueue().Count > 0;
        if (hintBubbleGo != null)
        {
            hintBubbleGo.SetActive(showHint);
            if (showHint) UpdateHintArrow();
        }

        HandleMouseInput();
        UpdateCursor();
    }

    void HandleMouseInput()
    {
        if (mainCam == null) mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        // 配置済みユニットの再配置は準備フェーズのみ
        var gm = GameManager.Instance;
        if (Input.GetMouseButtonDown(0) && !isDragging
            && gm != null && gm.currentPhase == GamePhase.Preparation)
        {
            TryPickUpPlacedUnit(mouseWorld);
        }

        if (isDragging && draggedUnit != null)
        {
            draggedUnit.transform.position = mouseWorld;
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            TryPlaceUnit(mouseWorld);
        }
    }

    void TryPickUpPlacedUnit(Vector3 worldPos)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var allies = gm.GetUnits(Team.Ally);
        foreach (var unit in allies)
        {
            if (unit == null || unit.isDead) continue;
            float dist = Vector2.Distance(worldPos, unit.transform.position);
            if (dist < GameConfig.UnitRadius * 2.5f)
            {
                draggedUnit = unit;
                draggedUnit.isBeingDragged = true;
                isDragging = true;
                dragFromQueue = false;
                dragOriginalPos = unit.transform.position;

                { var sr = unit.GetComponentInChildren<SpriteRenderer>();
                  if (sr != null) { var c = sr.color; c.a = 0.6f; sr.color = c; }
                }
                return;
            }
        }
    }

    /// <summary>
    /// UIManagerのキュースロットPointerDownから呼ばれる
    /// </summary>
    public void StartDragFromQueue(int queueIndex)
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        bool canDrag = gm.currentPhase == GamePhase.Preparation || gm.currentPhase == GamePhase.Battle;
        if (!canDrag) return;

        var queue = gm.GetQueue();
        if (queueIndex < 0 || queueIndex >= queue.Count) return;

        var qUnit = queue[queueIndex];
        draggedType = qUnit.type;
        draggedOwner = qUnit.ownerName;
        draggedViewerId = qUnit.viewerId;
        draggedAppearance = qUnit.appearance;
        draggedPreviewSprite = qUnit.previewSprite;

        Vector3 mouseWorld = mainCam != null
            ? mainCam.ScreenToWorldPoint(Input.mousePosition)
            : Vector3.zero;
        mouseWorld.z = 0;

        var unit = gm.SpawnUnit(qUnit.type, Team.Ally, qUnit.ownerName, mouseWorld, 1f, qUnit.appearance, qUnit.viewerId);
        gm.DequeueUnit(queueIndex);

        // 半透明にしてドラッグ中であることを示す
        { var sr = unit.GetComponentInChildren<SpriteRenderer>();
          if (sr != null) { var c = sr.color; c.a = 0.6f; sr.color = c; }
        }

        draggedUnit = unit;
        draggedUnit.isBeingDragged = true;
        isDragging = true;
        dragFromQueue = true;
        hasEverDragged = true;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("UnitPickup");
    }

    bool IsInPlacementZone(Vector3 pos)
    {
        return pos.x >= GameConfig.PlacementZoneMinX &&
               pos.x <= GameConfig.PlacementZoneMaxX &&
               pos.y >= GameConfig.PlacementZoneMinY &&
               pos.y <= GameConfig.PlacementZoneMaxY;
    }

    void TryPlaceUnit(Vector3 worldPos)
    {
        if (draggedUnit == null) { isDragging = false; return; }

        if (IsInPlacementZone(worldPos))
        {
            // ゾーン内 → 配置確定
            draggedUnit.transform.position = new Vector3(worldPos.x, worldPos.y, 0);
            draggedUnit.isBeingDragged = false;
            RestoreAlpha(draggedUnit);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("UnitPlace");
        }
        else if (dragFromQueue)
        {
            // キューからのドラッグでゾーン外 → 元の見た目のままキューに戻す
            var gm = GameManager.Instance;
            if (gm != null)
                gm.ReturnUnitToQueue(draggedType, draggedOwner, draggedViewerId, draggedAppearance, draggedPreviewSprite);
            Destroy(draggedUnit.gameObject);
        }
        else
        {
            // 配置済み再配置でゾーン外 → 元の位置に戻す
            draggedUnit.transform.position = dragOriginalPos;
            draggedUnit.isBeingDragged = false;
            RestoreAlpha(draggedUnit);
        }

        draggedUnit = null;
        isDragging = false;
    }

    void RestoreAlpha(Unit unit)
    {
        if (unit != null)
        {
            var sr = unit.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) { var c = sr.color; c.a = 1f; sr.color = c; }
        }
    }

    // ─── 配置ゾーン表示 ──────────────────────────────

    void CreatePlacementZoneVisual()
    {
        float cx = (GameConfig.PlacementZoneMinX + GameConfig.PlacementZoneMaxX) / 2f;
        float cy = (GameConfig.PlacementZoneMinY + GameConfig.PlacementZoneMaxY) / 2f;
        float w = GameConfig.PlacementZoneMaxX - GameConfig.PlacementZoneMinX;
        float h = GameConfig.PlacementZoneMaxY - GameConfig.PlacementZoneMinY;

        zoneRoot = new GameObject("PlacementZoneRoot");

        // 点線ボーダー（塗りなし）
        var borderGo = new GameObject("PlacementZoneBorder");
        borderGo.transform.SetParent(zoneRoot.transform);
        borderGo.transform.position = new Vector3(cx, cy, 0);
        placementZoneVisual = borderGo.AddComponent<SpriteRenderer>();
        placementZoneVisual.sprite = CreateDashedBorderSprite(w, h);
        placementZoneVisual.color = new Color(0.5f, 0.7f, 1f, 0.5f);
        placementZoneVisual.sortingOrder = -5;

        // ガイドテキスト（大きめ）
        guideTextGo = new GameObject("PlacementGuide");
        guideTextGo.transform.SetParent(zoneRoot.transform);
        guideTextGo.transform.position = new Vector3(cx, cy, 0);
        var tm = guideTextGo.AddComponent<TextMesh>();
        tm.text = "へいしをはいちしてください";
        tm.fontSize = 48;
        tm.characterSize = 0.08f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = new Color(0.7f, 0.85f, 1f, 0f);
        guideTextAlpha = 0f; // フェードインから開始

        {
            var font = GetFont();
            if (font != null)
            {
                tm.font = font;
                tm.GetComponent<MeshRenderer>().material = font.material;
            }
        }
        var mr = guideTextGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 2; // キャラ(sortingOrder=10)より下

        zoneRoot.SetActive(false);
    }

    // ─── 初回ヒント吹き出し ──────────────────────────

    void CreateHintBubble()
    {
        // キューパネルの上あたりに吹き出しを表示
        // キューパネルは画面下部にあるので、ワールド座標で下の方に配置
        float cx = (GameConfig.PlacementZoneMinX + GameConfig.PlacementZoneMaxX) / 2f;
        float zoneTop = GameConfig.PlacementZoneMaxY;

        hintBubbleGo = new GameObject("HintBubble");
        hintBubbleGo.transform.position = new Vector3(cx, zoneTop + 0.7f, 0);

        // 吹き出し背景
        var bgGo = new GameObject("HintBg");
        bgGo.transform.SetParent(hintBubbleGo.transform);
        bgGo.transform.localPosition = Vector3.zero;
        var bgSr = bgGo.AddComponent<SpriteRenderer>();
        bgSr.sprite = Unit.CreatePixelSprite();
        bgSr.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        bgSr.sortingOrder = 95;
        bgGo.transform.localScale = new Vector3(5.5f, 0.7f, 1f);

        // 吹き出し尻尾（下向き三角）
        var tailGo = new GameObject("HintTail");
        tailGo.transform.SetParent(hintBubbleGo.transform);
        tailGo.transform.localPosition = new Vector3(0, -0.45f, 0);
        var tailSr = tailGo.AddComponent<SpriteRenderer>();
        tailSr.sprite = Unit.CreatePixelSprite();
        tailSr.color = new Color(1f, 0.85f, 0.2f, 0.95f);
        tailSr.sortingOrder = 95;
        tailGo.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
        tailGo.transform.localRotation = Quaternion.Euler(0, 0, 45f);

        // テキスト
        var textGo = new GameObject("HintText");
        textGo.transform.SetParent(hintBubbleGo.transform);
        textGo.transform.localPosition = Vector3.zero;
        var tm = textGo.AddComponent<TextMesh>();
        tm.text = "キューのへいしをドラッグしてはいちしよう!";
        tm.fontSize = 40;
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(0.15f, 0.1f, 0f);
        tm.fontStyle = FontStyle.Bold;

        var font = GetFont();
        if (font != null)
        {
            tm.font = font;
            tm.GetComponent<MeshRenderer>().material = font.material;
        }
        var tmMr = textGo.GetComponent<MeshRenderer>();
        if (tmMr != null) tmMr.sortingOrder = 96;

        // 矢印（↓ゾーンに向かって）
        var arrowGo = new GameObject("HintArrow");
        arrowGo.transform.SetParent(hintBubbleGo.transform);
        arrowGo.transform.localPosition = new Vector3(0, -0.85f, 0);
        var arrowTm = arrowGo.AddComponent<TextMesh>();
        arrowTm.text = "▼";
        arrowTm.fontSize = 64;
        arrowTm.characterSize = 0.1f;
        arrowTm.anchor = TextAnchor.MiddleCenter;
        arrowTm.alignment = TextAlignment.Center;
        arrowTm.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        var arrowMr = arrowGo.GetComponent<MeshRenderer>();
        if (arrowMr != null) arrowMr.sortingOrder = 96;

        hintBubbleGo.SetActive(false);
    }

    void UpdateHintArrow()
    {
        // 矢印を上下にふわふわアニメーション
        hintArrowTimer += Time.deltaTime * 3f;
        float bounce = Mathf.Sin(hintArrowTimer) * 0.1f;

        if (hintBubbleGo != null)
        {
            float cx = (GameConfig.PlacementZoneMinX + GameConfig.PlacementZoneMaxX) / 2f;
            float zoneTop = GameConfig.PlacementZoneMaxY;
            hintBubbleGo.transform.position = new Vector3(cx, zoneTop + 0.7f + bounce, 0);
        }
    }

    // ─── カーソル管理 ──────────────────────────────────

#if UNITY_EDITOR
    Texture2D LoadCursorTexture(string path)
    {
        var importer = UnityEditor.AssetImporter.GetAtPath(path) as UnityEditor.TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (!importer.isReadable) { importer.isReadable = true; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (importer.textureCompression != UnityEditor.TextureImporterCompression.Uncompressed)
            { importer.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed; changed = true; }
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
#endif

    void LoadCursors()
    {
#if UNITY_EDITOR
        const string DIR = "Assets/Tiny Swords/UI Elements/Cursors";
        cursorNormal      = LoadCursorTexture($"{DIR}/Cursor_01.png");
        cursorSelectable  = LoadCursorTexture($"{DIR}/Cursor_02.png");
        cursorCannotPlace = LoadCursorTexture($"{DIR}/Cursor_03.png");
        cursorCanPlace    = LoadCursorTexture($"{DIR}/Cursor_04.png");
#else
        cursorNormal      = Resources.Load<Texture2D>("Cursor_01");
        cursorSelectable  = Resources.Load<Texture2D>("Cursor_02");
        cursorCannotPlace = Resources.Load<Texture2D>("Cursor_03");
        cursorCanPlace    = Resources.Load<Texture2D>("Cursor_04");
#endif
        // 起動時にデフォルトカーソルをセット
        SetGameCursor(GameCursorState.Normal);
    }

    void UpdateCursor()
    {
        if (mainCam == null) return;
        Vector3 mouseWorld = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0;

        if (isDragging)
        {
            // ドラッグ中: 配置ゾーン内外でカーソル切り替え
            SetGameCursor(IsInPlacementZone(mouseWorld)
                ? GameCursorState.CanPlace
                : GameCursorState.CannotPlace);
        }
        else if (uiRequestsSelectable)
        {
            // UIManagerからのホバーリクエスト（1フレームで消える）
            SetGameCursor(GameCursorState.Selectable);
            uiRequestsSelectable = false;
        }
        else if (IsHoveringAllyUnit(mouseWorld))
        {
            // 配置済み味方ユニットにホバー中
            SetGameCursor(GameCursorState.Selectable);
        }
        else
        {
            SetGameCursor(GameCursorState.Normal);
        }
    }

    void SetGameCursor(GameCursorState state)
    {
        if (currentCursor == state) return;
        currentCursor = state;

        Texture2D tex;
        Vector2 hotspot;
        switch (state)
        {
            case GameCursorState.Selectable:
                tex = cursorSelectable;
                // 指先 = 上部中央付近
                hotspot = tex != null ? new Vector2(tex.width * 0.35f, 0) : Vector2.zero;
                break;
            case GameCursorState.CannotPlace:
                tex = cursorCannotPlace;
                // 禁止マーク = 中央
                hotspot = tex != null ? new Vector2(tex.width / 2f, tex.height / 2f) : Vector2.zero;
                break;
            case GameCursorState.CanPlace:
                tex = cursorCanPlace;
                // 配置ブラケット = 中央
                hotspot = tex != null ? new Vector2(tex.width / 2f, tex.height / 2f) : Vector2.zero;
                break;
            default:
                tex = cursorNormal;
                // 矢印 = 左上
                hotspot = Vector2.zero;
                break;
        }
        Cursor.SetCursor(tex, hotspot, CursorMode.Auto);
    }

    bool IsHoveringAllyUnit(Vector3 mouseWorld)
    {
        var gm = GameManager.Instance;
        if (gm == null) return false;
        // 準備中のみユニット選択可能（再配置用）
        if (gm.currentPhase != GamePhase.Preparation) return false;

        var allies = gm.GetUnits(Team.Ally);
        foreach (var unit in allies)
        {
            if (unit == null || unit.isDead) continue;
            if (Vector2.Distance(mouseWorld, unit.transform.position) < GameConfig.UnitRadius * 2.5f)
                return true;
        }
        return false;
    }

    /// <summary>UIManagerがホバー中にSelectable カーソルをリクエスト</summary>
    public void RequestSelectableCursor()
    {
        uiRequestsSelectable = true;
    }

    // ─── 点線ボーダースプライト生成 ────────────────────

    static Sprite CreateDashedBorderSprite(float worldW, float worldH)
    {
        // 1px = 0.1 world unit (PPU=10) で適度な解像度
        const int ppu = 10;
        int texW = Mathf.RoundToInt(worldW * ppu);
        int texH = Mathf.RoundToInt(worldH * ppu);
        texW = Mathf.Max(texW, 4);
        texH = Mathf.Max(texH, 4);

        var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // 全体を透明に
        var clear = new Color[texW * texH];
        tex.SetPixels(clear);

        const int borderW = 2; // ボーダー幅(px)
        const int dashLen = 6; // ダッシュ長(px)
        const int gapLen = 4;  // 隙間長(px)
        int cycle = dashLen + gapLen;
        Color white = Color.white;

        // 上辺・下辺
        for (int x = 0; x < texW; x++)
        {
            bool dash = (x % cycle) < dashLen;
            if (!dash) continue;
            for (int b = 0; b < borderW && b < texH; b++)
            {
                tex.SetPixel(x, texH - 1 - b, white); // 上辺
                tex.SetPixel(x, b, white);             // 下辺
            }
        }
        // 左辺・右辺
        for (int y = 0; y < texH; y++)
        {
            bool dash = (y % cycle) < dashLen;
            if (!dash) continue;
            for (int b = 0; b < borderW && b < texW; b++)
            {
                tex.SetPixel(b, y, white);             // 左辺
                tex.SetPixel(texW - 1 - b, y, white);  // 右辺
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, texW, texH),
            new Vector2(0.5f, 0.5f), ppu);
    }
}
