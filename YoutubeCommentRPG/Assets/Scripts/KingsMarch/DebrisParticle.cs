using UnityEngine;

/// <summary>
/// 城破壊時に飛び散る破片パーティクル。
/// </summary>
public class DebrisParticle : MonoBehaviour
{
    private Vector2 velocity;
    private float rotSpeed;
    private float lifetime = 1.5f;
    private SpriteRenderer sr;

    void Start()
    {
        velocity = Random.insideUnitCircle.normalized * Random.Range(2f, 5f);
        velocity.y = Mathf.Abs(velocity.y) * 0.8f + 1f; // bias upward
        rotSpeed = Random.Range(-360f, 360f);
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0) { Destroy(gameObject); return; }

        // Gravity
        velocity.y -= 6f * Time.deltaTime;
        transform.position += (Vector3)velocity * Time.deltaTime;
        transform.Rotate(0, 0, rotSpeed * Time.deltaTime);

        // Fade out
        if (sr != null)
        {
            float alpha = Mathf.Clamp01(lifetime / 0.5f);
            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }
}
