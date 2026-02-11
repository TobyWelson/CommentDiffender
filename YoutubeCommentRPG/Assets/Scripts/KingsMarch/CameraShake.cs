using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// カメラ制御: マウスドラッグで自由移動、ホイールでズーム。
/// シェイク機能付き。メインカメラに自動アタッチされる。
/// </summary>
public class CameraShake : MonoBehaviour
{
    private static CameraShake _instance;
    private static readonly Vector3 DefaultPos = new Vector3(
        GameConfig.CameraX, GameConfig.CameraY, GameConfig.CameraZ);

    private float shakeDuration;
    private float shakeIntensity;

    // マウスドラッグ移動
    private Vector3 dragOrigin;
    private bool isDragging;
    private float currentX;
    private float currentY;

    // ズーム
    private Camera cam;
    private float targetOrthoSize;
    private const float MinOrthoSize = 2f;
    private const float MaxOrthoSize = 12f;
    private const float ZoomSpeed = 1.5f;

    static CameraShake GetInstance()
    {
        if (_instance != null) return _instance;
        var cam = Camera.main;
        if (cam == null) return null;
        _instance = cam.GetComponent<CameraShake>();
        if (_instance == null) _instance = cam.gameObject.AddComponent<CameraShake>();
        return _instance;
    }

    void Awake()
    {
        currentX = GameConfig.CameraX;
        currentY = GameConfig.CameraY;
        transform.position = DefaultPos;
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographicSize = GameConfig.CameraOrthoSize;
            targetOrthoSize = cam.orthographicSize;
        }
    }

    public static void Shake(float duration, float intensity)
    {
        var inst = GetInstance();
        if (inst == null) return;
        if (duration > inst.shakeDuration)
        {
            inst.shakeDuration = duration;
            inst.shakeIntensity = intensity;
        }
    }

    void LateUpdate()
    {
        HandleMouseDrag();
        HandleZoom();

        Vector3 basePos = new Vector3(currentX, currentY, GameConfig.CameraZ);

        if (shakeDuration > 0)
        {
            shakeDuration -= Time.deltaTime;
            float t = Mathf.Clamp01(shakeDuration / 0.5f);
            Vector2 offset = Random.insideUnitCircle * shakeIntensity * t;
            transform.position = basePos + (Vector3)offset;
        }
        else
        {
            transform.position = basePos;
        }
    }

    void HandleMouseDrag()
    {
        // 右クリック or 中クリックでドラッグ移動
        if (Input.GetMouseButtonDown(2) || Input.GetMouseButtonDown(1))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
        }

        if (Input.GetMouseButtonUp(2) || Input.GetMouseButtonUp(1))
        {
            isDragging = false;
        }

        if (isDragging && (Input.GetMouseButton(2) || Input.GetMouseButton(1)))
        {
            Vector3 currentMouseWorld = cam.ScreenToWorldPoint(Input.mousePosition);
            Vector3 diff = dragOrigin - currentMouseWorld;
            currentX += diff.x;
            currentY += diff.y;

            // フィールド範囲内に制限
            currentX = Mathf.Clamp(currentX, GameConfig.CameraMinX, GameConfig.CameraMaxX);
            currentY = Mathf.Clamp(currentY, GameConfig.FieldMinY, GameConfig.FieldMaxY);
        }
    }

    void HandleZoom()
    {
        // UI上にマウスがある場合はズームしない（ScrollRectとの競合防止）
        bool overUI = UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (!overUI)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetOrthoSize -= scroll * ZoomSpeed;
                targetOrthoSize = Mathf.Clamp(targetOrthoSize, MinOrthoSize, MaxOrthoSize);
            }
        }

        if (cam != null)
        {
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetOrthoSize, Time.deltaTime * 10f);
        }
    }
}
