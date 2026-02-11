using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 3Dフルスクリーン時にオブジェクトをカメラ方向に向けるビルボード。
/// view-oriented: オブジェクトの+Z面がカメラを直接向く。
/// childCylindricalTargets: 子テキスト等のビルボード（同一LateUpdateで順序保証）。
/// </summary>
public class Billboard3D : MonoBehaviour
{
    /// <summary>true=Z軸のみ回転(城など床に立つオブジェクト用), false=フルビルボード</summary>
    public bool cylindrical = false;

    /// <summary>3Dモードで床から浮かせるZ方向オフセット（スプライト半分の高さ）</summary>
    public float groundOffset = 0f;

    /// <summary>
    /// 子オブジェクトでシリンドリカルビルボードを適用する対象。
    /// 親のLateUpdate内で処理するため、実行順序の問題を回避。
    /// </summary>
    [System.NonSerialized] public List<Transform> childCylindricalTargets = new List<Transform>();

    void LateUpdate()
    {
        if (!CameraView3D.is3DFullScreen) return;

        var cam = CameraView3D.Instance?.Camera3D;
        if (cam == null) return;

        if (cylindrical)
        {
            // Z軸シリンドリカル: 水平方向のみカメラを向き、直立を維持
            Vector3 dirToCamera = cam.transform.position - transform.position;
            dirToCamera.z = 0f; // 水平成分のみ
            if (dirToCamera.sqrMagnitude > 0.001f)
            {
                dirToCamera.Normalize();
                transform.rotation = Quaternion.LookRotation(dirToCamera, Vector3.forward);
            }
        }
        else
        {
            // View-oriented: カメラを直接向く（+Z面がカメラ方向）
            Vector3 dirToCamera = cam.transform.position - transform.position;
            if (dirToCamera.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dirToCamera, cam.transform.up);
        }

        // 子テキストのシリンドリカルビルボード（同一LateUpdate内で親の回転後に実行→順序保証）
        for (int i = childCylindricalTargets.Count - 1; i >= 0; i--)
        {
            var target = childCylindricalTargets[i];
            if (target == null) { childCylindricalTargets.RemoveAt(i); continue; }
            if (!target.gameObject.activeSelf) continue;
            Vector3 dir = cam.transform.position - target.position;
            dir.z = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                dir.Normalize();
                target.rotation = Quaternion.LookRotation(dir, Vector3.forward);
            }
        }

        // 地面オフセット: スプライト底辺がZ=0(床)に接するように持ち上げる
        if (groundOffset > 0f)
        {
            Vector3 pos = transform.position;
            pos.z = groundOffset;
            transform.position = pos;
        }
    }

    public void ResetRotation()
    {
        transform.rotation = Quaternion.identity;
        // 子ターゲットもリセット
        foreach (var target in childCylindricalTargets)
        {
            if (target != null) target.localRotation = Quaternion.identity;
        }
        // 3D→2D復帰時にZ座標を元に戻す
        if (groundOffset > 0f)
        {
            Vector3 pos = transform.position;
            pos.z = 0f;
            transform.position = pos;
        }
    }
}
