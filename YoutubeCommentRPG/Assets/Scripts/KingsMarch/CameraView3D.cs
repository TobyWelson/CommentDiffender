using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 3Dジオラマビューカメラ。
///
/// 座標系の変換:
/// 2Dゲーム: X=横, Y=縦(画面上下), Z=奥行き(常に0付近)
/// 3Dビュー: X=横(同じ), Y=奥行き(2DのY→3Dの奥行), Z=高さ(上方向)
///
/// カメラのUp方向をZ軸に設定することで、XY平面(Z=0)が「床」として見える。
/// 2Dのスプライト・タイルマップがそのまま3Dの地面になる。
///
/// プレビュー: 俯瞰カメラ（右下小窓）
/// フルスクリーン: キャラ追従三人称カメラ（右クリックで切替）
/// </summary>
public class CameraView3D : MonoBehaviour
{
    public static CameraView3D Instance { get; private set; }
    public static bool is3DFullScreen { get; private set; }

    // カメラ
    private Camera cam3D;
    private Camera mainCamera;
    private CameraShake mainCameraShake;
    private RenderTexture renderTex;
    private GameObject cam3DObj;

    // スカイドーム
    private GameObject skyDome;
    private Material skyDomeMat;

    // 地面拡張（タイルマップ外の床）
    private GameObject groundPlane;

    // 雲
    private GameObject[] cloudQuads;
    private float[] cloudSpeeds;

    // ライト
    private GameObject sunLight;

    // フォローカメラ
    private Unit followTarget;
    private int followIndex = 0;
    private string followTargetName = "";

    // オービットカメラ（三人称操作）
    private float orbitAngleH = 200f;  // 水平角度(度): 0=+X, 90=+Y, 180=-X(味方背後)
    private float orbitAngleV = 18f;   // 垂直角度(度): 水平面からの仰角
    private float orbitDistance = 10f;
    private const float MouseSensitivity = 3f;
    private const float MinAngleV = 3f;
    private const float MaxAngleV = 75f;
    private const float MinDistance = 4f;
    private const float MaxDistance = 25f;

    // レイヤー
    private const int Layer3DOnly = 8;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void Start()
    {
        Create3DCamera();
        CreateSkyDome();
        CreateGroundPlane();
        CreateClouds();
        CreateSunLight();

        cam3DObj.SetActive(false);
        SetWorldObjectsActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (renderTex != null) { renderTex.Release(); Destroy(renderTex); }
    }

    // ===== 生成 =====

    void Create3DCamera()
    {
        mainCamera = Camera.main;
        if (mainCamera != null)
            mainCameraShake = mainCamera.GetComponent<CameraShake>();

        cam3DObj = new GameObject("Camera3D");
        cam3DObj.transform.SetParent(transform);
        cam3D = cam3DObj.AddComponent<Camera>();
        cam3D.fieldOfView = 40f;
        cam3D.nearClipPlane = 0.1f;
        cam3D.farClipPlane = 200f;
        cam3D.depth = 5;
        cam3D.cullingMask = -1;

        renderTex = new RenderTexture(480, 270, 16);
        renderTex.filterMode = FilterMode.Bilinear;
        cam3D.targetTexture = renderTex;

        // 初期位置（俯瞰、Z-up）
        SetPreviewCamera();
    }

    void SetPreviewCamera()
    {
        // Z-up座標系: Y=奥行き, Z=高さ
        // カメラ: 手前(Y<0) + 上空(Z>0) から見下ろす
        cam3D.transform.position = new Vector3(2f, -16f, 16f);
        cam3D.transform.LookAt(new Vector3(4f, 1f, 0f), Vector3.forward);
    }

    void CreateSkyDome()
    {
        // カスタムシェーダー(Cull Front)で球体内面のみ描画。
        // 正スケール + Cull Front = 負スケール + Cull Back と同等だが、
        // 負スケールによるレンダリング異常を回避できる。
        // -90°X回転: パノラマ天頂(Y極) → ゲームの上方向(Z極)

        skyDome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        skyDome.name = "3D_SkyDome";
        skyDome.layer = Layer3DOnly;
        skyDome.transform.SetParent(transform);

        var col = skyDome.GetComponent<Collider>();
        if (col != null) Destroy(col);

        // 正スケール（負スケール不要: Cull Frontで内面描画）
        skyDome.transform.localScale = new Vector3(100f, 100f, 100f);

        // +90°X回転: Y極(パノラマ天頂)→ +Z極(ゲームの上方向)
        skyDome.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        ApplySkyDomeMaterial();

        cam3D.clearFlags = CameraClearFlags.SolidColor;
        cam3D.backgroundColor = new Color(0.5f, 0.7f, 1f);
    }

    /// <summary>スカイドームのマテリアルを(再)生成して適用</summary>
    void ApplySkyDomeMaterial()
    {
        if (skyDome == null) return;

        Texture2D panoTex = Resources.Load<Texture2D>("Skybox_Panorama");

        // Custom/SkyDomeInside: Cull Front + ZWrite On（内面のみ描画）
        Shader shader = Shader.Find("Custom/SkyDomeInside");
        if (shader == null)
        {
            Debug.LogWarning("[CameraView3D] Custom/SkyDomeInside not found, falling back to Unlit/Texture");
            shader = Shader.Find("Unlit/Texture");
        }
        if (shader == null)
        {
            Debug.LogWarning("[CameraView3D] Unlit/Texture not found, falling back to Sprites/Default");
            shader = Shader.Find("Sprites/Default");
        }

        skyDomeMat = new Material(shader);
        skyDomeMat.renderQueue = 1000;

        if (panoTex != null)
        {
            skyDomeMat.mainTexture = panoTex;
            // Cull Front（裏面描画）ではテクスチャが水平反転するため補正
            skyDomeMat.mainTextureScale = new Vector2(-1f, 1f);
            skyDomeMat.mainTextureOffset = new Vector2(1f, 0f);
        }
        else
        {
            Debug.LogWarning("[CameraView3D] Skybox_Panorama texture not found in Resources");
        }

        var rend = skyDome.GetComponent<Renderer>();
        rend.material = skyDomeMat;
        rend.shadowCastingMode = ShadowCastingMode.Off;
        rend.receiveShadows = false;

        Debug.Log($"[CameraView3D] SkyDome material applied: shader={shader?.name}, texture={panoTex != null}");
    }

    void CreateGroundPlane()
    {
        // タイルマップ外の拡張床（タイルマップはZ=0にあり、そのまま3D床になる）
        groundPlane = GameObject.CreatePrimitive(PrimitiveType.Quad);
        groundPlane.name = "3D_Ground";
        groundPlane.layer = Layer3DOnly;
        groundPlane.transform.SetParent(transform);

        // XY平面(Z=-0.1) = スプライト/タイルマップの少し下
        // Quad default: +Z方向を向く = 上空のカメラから見える
        groundPlane.transform.rotation = Quaternion.identity;
        groundPlane.transform.position = new Vector3(5f, 0f, -0.1f);
        groundPlane.transform.localScale = new Vector3(60f, 30f, 1f);

        var col = groundPlane.GetComponent<Collider>();
        if (col != null) Destroy(col);

        var grassTex = GenerateGrassTexture(128, 128);
        var mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = grassTex;
        mat.mainTextureScale = new Vector2(12f, 6f);
        groundPlane.GetComponent<Renderer>().material = mat;
    }

    Texture2D GenerateGrassTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Repeat;
        float scale = 20f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float n1 = Mathf.PerlinNoise(x / scale, y / scale);
                float n2 = Mathf.PerlinNoise(x / (scale * 0.5f) + 100, y / (scale * 0.5f) + 100);
                float n = n1 * 0.7f + n2 * 0.3f;
                float r = Mathf.Lerp(0.18f, 0.35f, n);
                float g = Mathf.Lerp(0.45f, 0.65f, n);
                float b = Mathf.Lerp(0.12f, 0.25f, n);
                if (Random.value < 0.03f) { g += 0.1f; r -= 0.05f; }
                tex.SetPixel(x, y, new Color(r, g, b, 1f));
            }
        }
        tex.Apply();
        return tex;
    }

    void CreateClouds()
    {
        cloudQuads = new GameObject[8];
        cloudSpeeds = new float[8];

        for (int i = 0; i < 8; i++)
        {
            var cloudTex = Resources.Load<Texture2D>($"Cloud_{i + 1}");
            if (cloudTex == null) continue;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"3D_Cloud_{i}";
            quad.layer = Layer3DOnly;
            quad.transform.SetParent(transform);

            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mat = new Material(Shader.Find("Sprites/Default"));
            mat.mainTexture = cloudTex;
            mat.color = new Color(1f, 1f, 1f, 0.6f);
            quad.GetComponent<Renderer>().material = mat;
            quad.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            quad.GetComponent<Renderer>().receiveShadows = false;

            // Z-up: 雲は高Z(上空), XY方向に散らばる
            float x = Random.Range(-10f, 25f);
            float y = Random.Range(-5f, 15f);
            float z = Random.Range(12f, 20f); // 上空
            quad.transform.position = new Vector3(x, y, z);
            quad.transform.localScale = Vector3.one * Random.Range(4f, 8f);

            cloudQuads[i] = quad;
            cloudSpeeds[i] = Random.Range(0.15f, 0.4f);
        }
    }

    void CreateSunLight()
    {
        sunLight = new GameObject("3D_SunLight");
        sunLight.layer = Layer3DOnly;
        sunLight.transform.SetParent(transform);

        var light = sunLight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.8f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;

        // Z-up: 太陽は上(+Z)から照らす
        sunLight.transform.forward = new Vector3(0.3f, 0.2f, -0.8f).normalized;
    }

    // ===== 更新 =====

    void LateUpdate()
    {
        if (cam3DObj == null || !cam3DObj.activeSelf) return;

        // スカイドームをカメラに追従（常にカメラを包む）
        if (skyDome != null && cam3D != null)
        {
            skyDome.transform.position = cam3D.transform.position;

            // マテリアルがドメインリロード等で失われた場合に再生成
            if (skyDome.activeSelf)
            {
                var rend = skyDome.GetComponent<Renderer>();
                if (rend != null && (rend.sharedMaterial == null ||
                    rend.sharedMaterial.shader == null ||
                    rend.sharedMaterial.mainTexture == null))
                {
                    ApplySkyDomeMaterial();
                }
            }
        }

        UpdateClouds();
        UpdateCloudBillboards();

        if (is3DFullScreen)
        {
            HandleFollowInput();
            UpdateFollowCamera();
        }
        else
        {
            SetPreviewCamera();
        }
    }

    void UpdateClouds()
    {
        if (cloudQuads == null) return;
        for (int i = 0; i < cloudQuads.Length; i++)
        {
            if (cloudQuads[i] == null) continue;
            var pos = cloudQuads[i].transform.position;
            pos.x += cloudSpeeds[i] * Time.deltaTime;
            if (pos.x > 35f) pos.x = -15f;
            cloudQuads[i].transform.position = pos;
        }
    }

    void UpdateCloudBillboards()
    {
        if (cam3D == null || cloudQuads == null) return;
        for (int i = 0; i < cloudQuads.Length; i++)
        {
            if (cloudQuads[i] == null) continue;
            // Z-upでカメラを向く
            Vector3 dir = cam3D.transform.position - cloudQuads[i].transform.position;
            if (dir.sqrMagnitude < 0.01f) continue;
            cloudQuads[i].transform.rotation = Quaternion.LookRotation(-dir, Vector3.forward);
        }
    }

    // ===== フォローカメラ =====

    void HandleFollowInput()
    {
        // 右ドラッグ = オービット操作（三人称カメラ標準）
        if (Input.GetMouseButton(1))
        {
            float dx = Input.GetAxis("Mouse X") * MouseSensitivity;
            float dy = Input.GetAxis("Mouse Y") * MouseSensitivity;
            orbitAngleH -= dx;
            orbitAngleV += dy;
            orbitAngleV = Mathf.Clamp(orbitAngleV, MinAngleV, MaxAngleV);
        }

        // 中クリック or Tab = キャラ切替
        if (Input.GetMouseButtonDown(2) || Input.GetKeyDown(KeyCode.Tab))
            CycleFollowTarget();

        // スクロール = ズーム（距離）
        bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        if (!overUI)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                orbitDistance -= scroll * 3f;
                orbitDistance = Mathf.Clamp(orbitDistance, MinDistance, MaxDistance);
            }
        }
    }

    void UpdateFollowCamera()
    {
        if (followTarget == null || followTarget.isDead)
            CycleFollowTarget();
        if (followTarget == null)
        {
            SetPreviewCamera();
            return;
        }

        // 注視点: ユニット位置の少し上（Z-upで高さ方向）
        Vector3 lookAt = followTarget.transform.position;
        lookAt.z = 0.8f;

        // オービット: 球面座標 → カメラ位置
        float radH = orbitAngleH * Mathf.Deg2Rad;
        float radV = orbitAngleV * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Cos(radV) * Mathf.Cos(radH),
            Mathf.Cos(radV) * Mathf.Sin(radH),
            Mathf.Sin(radV)
        ) * orbitDistance;

        Vector3 targetCamPos = lookAt + offset;

        // スムーズ追従
        cam3D.transform.position = Vector3.Lerp(cam3D.transform.position, targetCamPos, Time.deltaTime * 6f);

        // Z-upでターゲットを向く
        Vector3 dir = lookAt - cam3D.transform.position;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.forward);
            cam3D.transform.rotation = Quaternion.Slerp(cam3D.transform.rotation, targetRot, Time.deltaTime * 8f);
        }
    }

    void CycleFollowTarget()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var alive = new List<Unit>();
        foreach (var u in gm.allyUnits)
            if (u != null && !u.isDead) alive.Add(u);

        if (alive.Count == 0)
        {
            followTarget = null;
            followTargetName = "";
            NotifyFollowTargetChanged();
            return;
        }

        followIndex = (followIndex + 1) % alive.Count;
        followTarget = alive[followIndex];

        string name = string.IsNullOrEmpty(followTarget.ownerName) ? "NPC" : followTarget.ownerName;
        followTargetName = $"{name} Lv.{followTarget.level}";
        NotifyFollowTargetChanged();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySE("ButtonClick");
    }

    void SelectFirstFollowTarget()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        foreach (var u in gm.allyUnits)
        {
            if (u != null && !u.isDead)
            {
                followTarget = u;
                followIndex = 0;
                string name = string.IsNullOrEmpty(u.ownerName) ? "NPC" : u.ownerName;
                followTargetName = $"{name} Lv.{u.level}";
                NotifyFollowTargetChanged();
                return;
            }
        }
        followTarget = null;
        followTargetName = "";
    }

    void NotifyFollowTargetChanged()
    {
        var uiMgr = GetComponent<UIManager>();
        if (uiMgr != null) uiMgr.UpdateFollowTargetName(followTargetName);
    }

    // ===== 表示切替 =====

    public void EnablePreview()
    {
        if (cam3DObj == null) return;
        cam3DObj.SetActive(true);
        SetWorldObjectsActive(true);

        cam3D.targetTexture = renderTex;
        cam3D.rect = new Rect(0, 0, 1, 1);
        cam3D.fieldOfView = 40f;
        is3DFullScreen = false;

        if (mainCamera != null)
            mainCamera.cullingMask &= ~(1 << Layer3DOnly);
    }

    public void DisablePreview()
    {
        if (is3DFullScreen) ExitFullScreen();
        if (cam3DObj != null) cam3DObj.SetActive(false);
        SetWorldObjectsActive(false);
    }

    public void ToggleFullScreen()
    {
        if (is3DFullScreen)
            ExitFullScreen();
        else
            EnterFullScreen();
    }

    void EnterFullScreen()
    {
        is3DFullScreen = true;

        cam3D.targetTexture = null;
        cam3D.rect = new Rect(0, 0, 1, 1);
        cam3D.depth = 10;
        cam3D.fieldOfView = 50f;

        // 2Dカメラ系を完全無効化（分断）
        if (mainCamera != null) mainCamera.enabled = false;
        if (mainCameraShake != null) mainCameraShake.enabled = false;

        SelectFirstFollowTarget();

        var uiMgr = GetComponent<UIManager>();
        if (uiMgr != null) uiMgr.On3DFullScreenChanged(true);
    }

    void ExitFullScreen()
    {
        is3DFullScreen = false;

        cam3D.targetTexture = renderTex;
        cam3D.depth = 5;
        cam3D.fieldOfView = 40f;

        // 2Dカメラ系を復帰
        if (mainCamera != null) mainCamera.enabled = true;
        if (mainCameraShake != null) mainCameraShake.enabled = true;

        ResetAllBillboards();

        followTarget = null;
        followTargetName = "";

        var uiMgr = GetComponent<UIManager>();
        if (uiMgr != null) uiMgr.On3DFullScreenChanged(false);
    }

    void ResetAllBillboards()
    {
        var billboards = FindObjectsOfType<Billboard3D>();
        foreach (var bb in billboards)
            bb.ResetRotation();
    }

    void SetWorldObjectsActive(bool active)
    {
        if (skyDome != null) skyDome.SetActive(active);
        if (groundPlane != null) groundPlane.SetActive(active);
        if (sunLight != null) sunLight.SetActive(active);
        if (cloudQuads != null)
            foreach (var c in cloudQuads)
                if (c != null) c.SetActive(active);
    }

    // ===== 公開 =====

    public RenderTexture PreviewTexture => renderTex;
    public Camera Camera3D => cam3D;
    public string FollowTargetName => followTargetName;
}
