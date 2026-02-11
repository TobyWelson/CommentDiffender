using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    private TextMeshPro tmp;
    private float timer;
    private Color startColor;

    public static DamagePopup Create(Vector3 position, int amount, bool isHeal)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = position + Vector3.up * 0.5f;

        var popup = go.AddComponent<DamagePopup>();
        popup.tmp = go.AddComponent<TextMeshPro>();
        popup.tmp.text = isHeal ? $"+{amount}" : $"-{amount}";
        popup.tmp.fontSize = 4f;
        popup.tmp.alignment = TextAlignmentOptions.Center;
        popup.tmp.sortingOrder = 100;
        popup.startColor = isHeal ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.3f, 0.3f);
        popup.tmp.color = popup.startColor;
        popup.tmp.fontStyle = FontStyles.Bold;
        popup.timer = GameConfig.DamagePopupDuration;

        // Slight random horizontal offset
        go.transform.position += Vector3.right * Random.Range(-0.2f, 0.2f);

        return popup;
    }

    public static DamagePopup CreateText(Vector3 position, string text, Color color)
    {
        var go = new GameObject("DamagePopup");
        go.transform.position = position + Vector3.up * 0.5f;

        var popup = go.AddComponent<DamagePopup>();
        popup.tmp = go.AddComponent<TextMeshPro>();
        popup.tmp.text = text;
        popup.tmp.fontSize = 3.5f;
        popup.tmp.alignment = TextAlignmentOptions.Center;
        popup.tmp.sortingOrder = 100;
        popup.startColor = color;
        popup.tmp.color = color;
        popup.tmp.fontStyle = FontStyles.Bold;
        popup.timer = GameConfig.DamagePopupDuration * 1.5f;

        return popup;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        // 3Dフルスクリーン時: Z方向(上)に上昇 + カメラを向く
        if (CameraView3D.is3DFullScreen)
        {
            transform.position += Vector3.forward * GameConfig.DamagePopupRiseSpeed * Time.deltaTime;
            var cam = CameraView3D.Instance?.Camera3D;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
        }
        else
        {
            transform.position += Vector3.up * GameConfig.DamagePopupRiseSpeed * Time.deltaTime;
        }

        float alpha = Mathf.Clamp01(timer / GameConfig.DamagePopupDuration);
        tmp.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

        if (timer <= 0f)
            Destroy(gameObject);
    }
}
