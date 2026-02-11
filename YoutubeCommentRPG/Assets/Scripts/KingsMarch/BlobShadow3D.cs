using UnityEngine;

/// <summary>
/// ユニット/城の足元に丸い影を表示（3Dビュー専用）。
/// Z-up座標系: XY平面(Z=0)が床。影はZ=-0.05に配置。
/// 親の回転に影響されないようワールド空間で配置。
/// </summary>
public class BlobShadow3D : MonoBehaviour
{
    private GameObject shadowQuad;
    private static Texture2D sharedShadowTex;
    private static Material sharedShadowMat;

    private const float ShadowZ = -0.05f;
    private const int Layer3DOnly = 8;

    void Start()
    {
        CreateShadow();
    }

    void CreateShadow()
    {
        if (sharedShadowTex == null)
            sharedShadowTex = GenerateShadowTexture(64);
        if (sharedShadowMat == null)
        {
            sharedShadowMat = new Material(Shader.Find("Sprites/Default"));
            sharedShadowMat.mainTexture = sharedShadowTex;
            sharedShadowMat.color = new Color(0f, 0f, 0f, 0.4f);
        }

        shadowQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        shadowQuad.name = "BlobShadow";
        shadowQuad.layer = Layer3DOnly;
        // 親にしない → Billboard3Dの回転に影響されない
        // OnDestroyで手動クリーンアップ

        var col = shadowQuad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        shadowQuad.GetComponent<Renderer>().material = sharedShadowMat;
        shadowQuad.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shadowQuad.GetComponent<Renderer>().receiveShadows = false;

        shadowQuad.SetActive(false);
    }

    static Texture2D GenerateShadowTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float center = size / 2f;
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }

    void LateUpdate()
    {
        if (shadowQuad == null) return;

        bool cam3DActive = CameraView3D.Instance != null &&
                           CameraView3D.Instance.Camera3D != null &&
                           CameraView3D.Instance.Camera3D.gameObject.activeSelf;
        shadowQuad.SetActive(cam3DActive);
        if (!cam3DActive) return;

        Vector3 worldPos = transform.position;

        // XY平面上に配置（Z=-0.05、床の少し上）
        shadowQuad.transform.position = new Vector3(worldPos.x, worldPos.y, ShadowZ);
        shadowQuad.transform.rotation = Quaternion.identity;

        // ワールドスケール直接設定（親の回転影響なし）
        float unitScale = transform.lossyScale.x;
        shadowQuad.transform.localScale = new Vector3(unitScale * 1.5f, unitScale * 0.8f, 1f);
    }

    void OnDestroy()
    {
        if (shadowQuad != null) Destroy(shadowQuad);
    }
}
