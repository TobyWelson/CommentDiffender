using UnityEngine;
using System.Collections;

public class Castle : MonoBehaviour
{
    public Team team = Team.Ally;
    public int maxHP = GameConfig.CastleMaxHP;
    public int currentHP;
    public bool isDestroyed;

    // HP bar references (created at runtime)
    private Transform hpBarBg;
    private Transform hpBarFill;
    private TextMesh hpText;
    private SpriteRenderer castleSr;
    private Vector3 originalPos;
    private Vector3 originalScale;

    public event System.Action OnCastleDestroyed;

    void Awake()
    {
        currentHP = maxHP;
        isDestroyed = false;
    }

    void Start()
    {
        CreateHPBar();
        CreateHPText();
        castleSr = GetComponent<SpriteRenderer>();
        originalPos = transform.position;
        originalScale = transform.localScale;

        // 3Dビュー用コンポーネント
        if (GetComponent<Billboard3D>() == null)
        {
            var bb = gameObject.AddComponent<Billboard3D>();
            bb.cylindrical = true; // 床に立つように見せる（上から見ても地面に潜らない）
            // groundOffset=0: スプライトをz=0(地面)にそのまま配置
        }
        if (GetComponent<BlobShadow3D>() == null) gameObject.AddComponent<BlobShadow3D>();
    }

    public void TakeDamage(int damage)
    {
        if (isDestroyed) return;

        currentHP -= damage;
        DamagePopup.Create(transform.position + Vector3.up * 1.5f, damage, false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("CastleHit");

        // Hit effects
        StartCoroutine(HitShake());
        if (castleSr != null) StartCoroutine(HitFlash());
        CameraShake.Shake(0.15f, 0.08f);

        if (currentHP <= 0)
        {
            currentHP = 0;
            isDestroyed = true;
            StartCoroutine(DestructionSequence());
        }

        UpdateHPBar();
    }

    IEnumerator HitShake()
    {
        for (int i = 0; i < 4; i++)
        {
            transform.position = originalPos + (Vector3)Random.insideUnitCircle * 0.06f;
            yield return new WaitForSeconds(0.03f);
        }
        transform.position = originalPos;
    }

    IEnumerator HitFlash()
    {
        if (castleSr == null) yield break;
        Color orig = castleSr.color;
        castleSr.color = new Color(1f, 0.4f, 0.4f, orig.a);
        yield return new WaitForSeconds(0.1f);
        if (castleSr != null) castleSr.color = orig;
    }

    IEnumerator DestructionSequence()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("CastleDestroy");
        // Big camera shake
        CameraShake.Shake(0.6f, 0.25f);

        // Spawn debris particles
        for (int i = 0; i < 12; i++)
        {
            SpawnDebris(transform.position + (Vector3)Random.insideUnitCircle * 0.8f);
        }

        // Flash and fade
        float duration = 1.5f;
        float timer = duration;
        Vector3 startScale = transform.localScale;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            float t = timer / duration;

            // Shake
            transform.position = originalPos + (Vector3)Random.insideUnitCircle * 0.1f * t;

            // Flash
            if (castleSr != null)
            {
                float flash = Mathf.PingPong(Time.time * 8f, 1f);
                castleSr.color = Color.Lerp(new Color(0.3f, 0.3f, 0.3f, t), new Color(1f, 0.5f, 0.2f, t), flash);
            }

            // Slight sink
            originalPos.y -= 0.2f * Time.deltaTime;

            yield return null;
        }

        if (castleSr != null) castleSr.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
        transform.position = originalPos;

        OnCastleDestroyed?.Invoke();
    }

    void SpawnDebris(Vector3 pos)
    {
        var go = new GameObject("Debris");
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Unit.CreatePixelSprite();
        sr.color = new Color(0.5f, 0.4f, 0.3f);
        sr.sortingOrder = 45;
        go.transform.localScale = new Vector3(
            Random.Range(0.15f, 0.35f),
            Random.Range(0.15f, 0.35f), 1f);
        go.AddComponent<DebrisParticle>();
    }

    public void ResetCastle()
    {
        StopAllCoroutines();
        currentHP = maxHP;
        isDestroyed = false;
        // 城の位置とスケールを初期値に復元
        float cx = team == Team.Ally ? GameConfig.CastleX : GameConfig.EnemyCastleX;
        float cy = team == Team.Ally ? GameConfig.CastleY : GameConfig.EnemyCastleY;
        originalPos = new Vector3(cx, cy, 0);
        transform.position = originalPos;
        transform.localScale = originalScale;
        transform.rotation = Quaternion.identity;
        if (castleSr != null) castleSr.color = Color.white;
        if (hpBarBg != null) hpBarBg.gameObject.SetActive(true);
        if (hpBarFill != null) hpBarFill.gameObject.SetActive(true);
        UpdateHPBar();
    }

    void CreateHPBar()
    {
        float barWidth = 1.8f;
        float barHeight = 0.15f;
        float yOffset = -1.8f;

        // Background
        var bgGo = new GameObject("CastleHPBarBG");
        bgGo.transform.SetParent(transform);
        bgGo.transform.localPosition = new Vector3(0, yOffset, 0);
        var bgSr = bgGo.AddComponent<SpriteRenderer>();
        bgSr.sprite = Unit.CreatePixelSprite();
        bgSr.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        bgSr.sortingOrder = 40;
        bgGo.transform.localScale = new Vector3(barWidth + 0.1f, barHeight + 0.05f, 1f);
        hpBarBg = bgGo.transform;

        // Fill
        var fillGo = new GameObject("CastleHPBarFill");
        fillGo.transform.SetParent(transform);
        fillGo.transform.localPosition = new Vector3(0, yOffset, 0);
        var fillSr = fillGo.AddComponent<SpriteRenderer>();
        fillSr.sprite = Unit.CreatePixelSprite();
        fillSr.color = team == Team.Enemy
            ? new Color(1f, 0.3f, 0.2f, 0.9f)
            : new Color(0.1f, 0.7f, 1f, 0.9f);
        fillSr.sortingOrder = 41;
        fillGo.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        hpBarFill = fillGo.transform;
    }

    void CreateHPText()
    {
        var textGo = new GameObject("CastleHPText");
        textGo.transform.SetParent(transform);
        textGo.transform.localPosition = new Vector3(0, -2.2f, 0);
        hpText = textGo.AddComponent<TextMesh>();
        hpText.fontSize = 28;
        hpText.characterSize = 0.08f;
        hpText.anchor = TextAnchor.MiddleCenter;
        hpText.alignment = TextAlignment.Center;
        hpText.color = Color.white;
        hpText.text = $"{currentHP} / {maxHP}";

        var mr = textGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 42;
    }

    void UpdateHPBar()
    {
        if (hpBarFill == null) return;

        float ratio = (float)currentHP / maxHP;
        float barWidth = 1.8f;
        hpBarFill.localScale = new Vector3(barWidth * ratio, 0.15f, 1f);

        float offset = -(barWidth - barWidth * ratio) / 2f;
        float yOffset = -1.8f;
        hpBarFill.localPosition = new Vector3(offset, yOffset, 0);

        // Color
        var fillSr = hpBarFill.GetComponent<SpriteRenderer>();
        if (fillSr != null)
        {
            if (team == Team.Enemy)
            {
                fillSr.color = Color.Lerp(new Color(0.4f, 0.1f, 0.1f), new Color(1f, 0.3f, 0.2f), ratio);
            }
            else
            {
                if (ratio > 0.5f)
                    fillSr.color = Color.Lerp(Color.yellow, new Color(0.1f, 0.7f, 1f), (ratio - 0.5f) * 2f);
                else
                    fillSr.color = Color.Lerp(Color.red, Color.yellow, ratio * 2f);
            }
        }

        if (hpText != null)
            hpText.text = $"{currentHP} / {maxHP}";
    }
}
