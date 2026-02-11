using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class Unit : MonoBehaviour
{
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

    [Header("Identity")]
    public UnitType unitType;
    public Team team;
    public string ownerName = "";
    public string viewerId = "";

    [Header("Stats")]
    public int maxHP;
    public int currentHP;
    public int attackPower;
    public float attackRange;
    public float attackSpeed;
    public float moveSpeed;
    public float damageReduction;
    public int healAmount;

    // バフ前のベースステータス（必殺技バフ復元用）
    [System.NonSerialized] public int baseAttackPower;
    [System.NonSerialized] public float baseMoveSpeed;
    [System.NonSerialized] public float baseDamageReduction;
    [System.NonSerialized] public bool hasUltimateBuff;

    [Header("Level")]
    public int level = 1;
    public int xp = 0;

    [Header("State")]
    public bool isDead;
    [System.NonSerialized] public bool isUltimateActive;  // 必殺技中はスコア加算無効
    public bool isInQueue = true;
    public UnitStance stance = UnitStance.Attack;
    [System.NonSerialized] public float defendTimer = 0f;
    public bool isBeingDragged = false;

    // Internal
    private float attackTimer;
    private float deathTimer;
    private SpriteRenderer sr;
    private Animator anim;
    private bool facingRight = true;
    private bool hasLoggedFirstUpdate = false;
    [System.NonSerialized] public Vector3 originalScale;

    // Animation (Pixel Heroes: Bool params + Trigger params)
    private string desiredAnim = "idle";       // "idle" or "run"
    private float attackAnimLock = 0f;         // Prevents anim state change during attack

    // Death
    private const float DEATH_DURATION = 0.8f;
    private Vector3 knockbackDir;

    // Boss knockback attack
    [System.NonSerialized] public bool isBoss = false;
    [System.NonSerialized] public bool isBossMonster = false; // BossPack1モンスター（アニメーション別系統）
    private float knockbackAttackCooldown = 0f;
    private float stunTimer = 0f;
    private bool isKnockedBack = false;

    // Ground tilemap (cached)
    private static Tilemap _groundTilemap;

    // HP bar child objects
    private Transform hpBarBg;
    private Transform hpBarFill;
    private TextMesh nameLabel;
    private TextMesh nameShadow;

    // Face icon (profile image overlay)
    private SpriteRenderer faceIconSr;

    // Speech bubble
    private GameObject speechBubbleGo;
    private TextMesh speechText;
    private SpriteRenderer speechBg;
    // speechTail削除: hukidasi画像に尻尾が含まれるため不要
    private float speechTimer;

    // YouTube Action Effects
    public int superChatTier = -1;
    public bool isMember;
    private SpriteRenderer glowSr;
    private SpriteRenderer glowSrInner;
    private float dotSpawnTimer;
    private TextMesh memberBadge;

    // TikTok Action Effects
    public int tiktokGiftTier = -1;
    public bool isSubscriber;
    public bool hasTeamBuff;
    private TextMesh teamBadge;
    private TextMesh subscriberBadge;

    private float auraHPBuff;
    private float auraATKBuff;
    private float auraRange;
    private float auraTickTimer;
    public bool enableAutoHeal;
    public float autoHealRate;
    private bool isRainbow;

    // Events
    public static event System.Action<Unit, Unit, int> OnDamageDealt;   // attacker, target, damage
    public static event System.Action<Unit, Unit> OnUnitKilled;         // killer, victim
    public static event System.Action<Unit, Unit, int> OnHealPerformed; // healer, target, amount

    public void Initialize(UnitType type, Team t, string owner, float statScale = 1f)
    {
        unitType = type;
        team = t;
        ownerName = owner;

        var stats = GameConfig.GetBaseStats(type);
        maxHP = Mathf.RoundToInt(stats.hp * statScale);
        currentHP = maxHP;
        attackPower = Mathf.RoundToInt(stats.attack * statScale);
        attackRange = stats.attackRange;
        attackSpeed = stats.attackSpeed;
        moveSpeed = stats.moveSpeed;
        damageReduction = stats.damageReduction;
        healAmount = Mathf.RoundToInt(stats.healAmount * statScale);

        // ベース値記録（バフ復元用）
        baseAttackPower = attackPower;
        baseMoveSpeed = moveSpeed;
        baseDamageReduction = damageReduction;
        hasUltimateBuff = false;

        isDead = false;
        isInQueue = false;
        attackTimer = 0f;
        deathTimer = 0f;
        xp = 0;

        // 敵はstatScaleからレベルを算出（5%刻み → Lv+1）
        if (t == Team.Enemy && statScale > 1f)
            level = 1 + Mathf.FloorToInt((statScale - 1f) / 0.05f);
        else
            level = 1;

        // Pixel Heroes: SpriteRendererはBody子オブジェクトにある
        sr = GetComponentInChildren<SpriteRenderer>();
        anim = GetComponent<Animator>();
        originalScale = transform.localScale;

        // 敵はstatScaleに応じて体を大きくする（味方に近い温度感、気持ち大きめ）
        if (team == Team.Enemy && statScale > 1f)
        {
            // 味方のレベルアップ成長(1.01^n)に近い感覚で、+1.5%ずつ成長
            float enemyLevel = 1f + (statScale - 1f) / 0.05f; // statScale→疑似レベル
            float sizeBonus = Mathf.Pow(1.015f, enemyLevel - 1f);
            // 上限: 基準の2.2倍（味方の2倍上限より気持ち大きめ）
            float maxEnemyScale = GameConfig.PixelHeroBaseScale * 2.2f;
            float targetScale = originalScale.x * sizeBonus;
            if (targetScale > maxEnemyScale)
                sizeBonus = maxEnemyScale / originalScale.x;
            transform.localScale = originalScale * sizeBonus;
            originalScale = transform.localScale;
        }

        // Enemies face left (sprite default is facing right)
        if (team == Team.Enemy && sr != null)
        {
            sr.flipX = true;
            facingRight = false;
        }

        // Log animator status for debugging
        if (anim == null)
            Debug.LogWarning($"[Unit] {gameObject.name}: No Animator component!");
        else if (anim.runtimeAnimatorController == null)
            Debug.LogWarning($"[Unit] {gameObject.name}: Animator has no controller!");
        else
            Debug.Log($"[Unit] {gameObject.name}: Animator OK, controller={anim.runtimeAnimatorController.name}");

        CreateHPBar();
        CreateNameLabel();

        // 3Dビュー用コンポーネント
        gameObject.AddComponent<Billboard3D>();
        gameObject.AddComponent<BlobShadow3D>();
    }

    void Update()
    {
        if (isDead)
        {
            HandleDeath();
            return;
        }
        if (isInQueue) return;

        // Boss knockback cooldown
        if (knockbackAttackCooldown > 0f) knockbackAttackCooldown -= Time.deltaTime;

        // Stun / knockback: skip AI
        if (isKnockedBack) { DriveAnimation(); UpdateHPBar(); return; }
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            SetDesiredAnim("idle");
            DriveAnimation();
            UpdateHPBar();
            UpdateSpeechBubble();
            return;
        }

        if (!hasLoggedFirstUpdate)
        {
            hasLoggedFirstUpdate = true;
            Debug.Log($"[Unit] {gameObject.name} first AI update. type={unitType}, team={team}, pos={transform.position}, anim={anim != null}");
        }

        attackTimer -= Time.deltaTime;

        if (unitType == UnitType.Monk)
            UpdateMonkAI();
        else
            UpdateCombatAI();

        // Drive animation every frame
        DriveAnimation();

        ApplySeparation();
        ClampToField();
        UpdateHPBar();
        UpdateFaceIcon();
        UpdateSpeechBubble();
        Update3DTextFlip();
        UpdateGlow();
        UpdateAura();

        // Auto-heal (Legend+ tier gift)
        if (enableAutoHeal && currentHP < maxHP)
        {
            currentHP = Mathf.Min(maxHP, currentHP + Mathf.RoundToInt(autoHealRate * Time.deltaTime));
        }

        // Rainbow color cycle (Universe tier gift)
        if (isRainbow && sr != null)
        {
            float h = Mathf.Repeat(Time.time * 0.3f, 1f);
            sr.color = Color.HSVToRGB(h, 0.6f, 1f);
        }
    }

    // ─── Combat AI ───────────────────────────────────────────────

    void SetDesiredAnim(string action)
    {
        desiredAnim = action;
    }

    void UpdateCombatAI()
    {
        Unit target = FindNearestEnemy();

        if (target != null)
        {
            float dist = Vector2.Distance(transform.position, target.transform.position);

            if (dist <= attackRange)
            {
                if (attackTimer <= 0f)
                {
                    // Boss knockback attack: 20% chance with cooldown
                    if (isBoss && knockbackAttackCooldown <= 0f && Random.value < 0.2f)
                    {
                        PerformKnockbackAttack(target);
                    }
                    else
                    {
                        PerformAttack(target);
                    }
                    attackTimer = attackSpeed;
                }
                SetDesiredAnim("idle");
            }
            else if (team == Team.Ally && stance == UnitStance.Defend)
            {
                // 後衛: 1.5秒後退 → タイマー切れでAttackに自動復帰
                defendTimer -= Time.deltaTime;
                if (defendTimer > 0f)
                {
                    MoveToward(new Vector2(transform.position.x - 2f, transform.position.y));
                    SetDesiredAnim("run");
                }
                else
                {
                    stance = UnitStance.Attack;
                }
            }
            else
            {
                MoveToward(target.transform.position);
                SetDesiredAnim("run");
            }
        }
        else
        {
            if (team == Team.Enemy)
            {
                Vector2 castlePos = new Vector2(GameConfig.CastleX, transform.position.y);
                MoveToward(castlePos);
                SetDesiredAnim("run");
            }
            else if (team == Team.Ally && stance == UnitStance.Defend)
            {
                float retreatX = GameConfig.NpcRearX;
                if (transform.position.x > retreatX + 0.3f)
                {
                    MoveToward(new Vector2(retreatX, transform.position.y));
                    SetDesiredAnim("run");
                }
                else
                {
                    stance = UnitStance.Attack; // 到着→自動で前衛復帰
                }
            }
            else if (team == Team.Ally && GameManager.Instance != null && GameManager.Instance.currentPhase == GamePhase.Battle)
            {
                // バトル中のみ敵城へ向かう（到達済みなら停止して攻撃）
                if (HasReachedEnemyCastle())
                {
                    SetDesiredAnim("idle");
                }
                else
                {
                    Vector2 enemyCastlePos = new Vector2(GameConfig.EnemyCastleX, transform.position.y);
                    MoveToward(enemyCastlePos);
                    SetDesiredAnim("run");
                }
            }
            else
            {
                SetDesiredAnim("idle");
            }
        }
    }

    void UpdateMonkAI()
    {
        Unit healTarget = FindMostDamagedAlly();

        if (healTarget != null)
        {
            float dist = Vector2.Distance(transform.position, healTarget.transform.position);

            if (dist <= attackRange)
            {
                if (attackTimer <= 0f)
                {
                    PerformHeal(healTarget);
                    attackTimer = attackSpeed;
                }
                SetDesiredAnim("idle");
            }
            else if (team == Team.Ally && stance == UnitStance.Defend)
            {
                float retreatX = GameConfig.NpcRearX;
                if (transform.position.x > retreatX + 0.3f)
                {
                    MoveToward(new Vector2(retreatX, transform.position.y));
                    SetDesiredAnim("run");
                }
                else
                {
                    stance = UnitStance.Attack;
                }
            }
            else
            {
                MoveToward(healTarget.transform.position);
                SetDesiredAnim("run");
            }
        }
        else
        {
            if (team == Team.Enemy)
            {
                Vector2 castlePos = new Vector2(GameConfig.CastleX, transform.position.y);
                MoveToward(castlePos);
                SetDesiredAnim("run");
            }
            else if (team == Team.Ally && stance == UnitStance.Defend)
            {
                float retreatX = GameConfig.NpcRearX;
                if (transform.position.x > retreatX + 0.3f)
                {
                    MoveToward(new Vector2(retreatX, transform.position.y));
                    SetDesiredAnim("run");
                }
                else
                {
                    stance = UnitStance.Attack;
                }
            }
            else if (team == Team.Ally && GameManager.Instance != null && GameManager.Instance.currentPhase == GamePhase.Battle)
            {
                // バトル中のみ敵城へ向かう（到達済みなら停止して攻撃）
                if (HasReachedEnemyCastle())
                {
                    SetDesiredAnim("idle");
                }
                else
                {
                    Vector2 enemyCastlePos = new Vector2(GameConfig.EnemyCastleX, transform.position.y);
                    MoveToward(enemyCastlePos);
                    SetDesiredAnim("run");
                }
            }
            else
            {
                SetDesiredAnim("idle");
            }
        }
    }

    // ─── Movement ────────────────────────────────────────────────

    static Tilemap GetGroundTilemap()
    {
        if (_groundTilemap != null) return _groundTilemap;
        var groundGo = GameObject.Find("Map/TileGrid/Ground");
        if (groundGo != null) _groundTilemap = groundGo.GetComponent<Tilemap>();
        return _groundTilemap;
    }

    public static bool IsWalkable(Vector3 worldPos)
    {
        var ground = GetGroundTilemap();
        if (ground == null) return true; // no tilemap = no restriction
        Vector3Int cell = ground.WorldToCell(worldPos);
        return ground.HasTile(cell);
    }

    void MoveToward(Vector2 target)
    {
        Vector2 dir = (target - (Vector2)transform.position).normalized;
        Vector3 newPos = transform.position + (Vector3)(dir * moveSpeed * Time.deltaTime);

        // 敵は地形制限なしで自由移動（右端スポーン→城へ直進）
        if (team == Team.Enemy)
        {
            transform.position = newPos;
        }
        else
        {
            bool onGround = IsWalkable(transform.position);

            if (!onGround)
            {
                transform.position = newPos;
            }
            else if (IsWalkable(newPos))
            {
                transform.position = newPos;
            }
            else
            {
                Vector3 slideX = transform.position + new Vector3(dir.x * moveSpeed * Time.deltaTime, 0, 0);
                if (IsWalkable(slideX))
                {
                    transform.position = slideX;
                }
                else
                {
                    Vector3 slideY = transform.position + new Vector3(0, dir.y * moveSpeed * Time.deltaTime, 0);
                    if (IsWalkable(slideY))
                        transform.position = slideY;
                }
            }
        }

        // Face direction of movement
        if (sr != null)
        {
            sr.flipX = dir.x < 0;
            facingRight = dir.x >= 0;
        }
    }

    void ClampToField()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, GameConfig.FieldMinX, GameConfig.FieldMaxX);
        pos.y = Mathf.Clamp(pos.y, GameConfig.FieldMinY, GameConfig.FieldMaxY);

        // Also ensure we're on walkable ground
        if (!IsWalkable(pos))
            pos = transform.position; // revert if clamped into non-walkable

        transform.position = pos;
    }

    void ApplySeparation()
    {
        if (isBeingDragged) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        var allies = gm.GetUnits(team);
        foreach (var other in allies)
        {
            if (other == null || other == this || other.isDead) continue;
            Vector2 diff = (Vector2)transform.position - (Vector2)other.transform.position;
            float dist = diff.magnitude;
            if (dist < GameConfig.UnitRadius * 2f && dist > 0.01f)
            {
                Vector2 push = diff.normalized * GameConfig.UnitPushForce * Time.deltaTime;
                transform.position += (Vector3)push;
            }
        }

        // 味方は準備フェーズ中のみゾーン内に留まる（戦闘中は自由移動）
        if (team == Team.Ally && gm.currentPhase == GamePhase.Preparation)
        {
            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, GameConfig.PlacementZoneMinX, GameConfig.PlacementZoneMaxX);
            pos.y = Mathf.Clamp(pos.y, GameConfig.PlacementZoneMinY, GameConfig.PlacementZoneMaxY);
            transform.position = pos;
        }
    }

    // ─── Combat ──────────────────────────────────────────────────

    void PerformAttack(Unit target)
    {
        // SE: ロール別
        if (AudioManager.Instance != null)
        {
            if (unitType == UnitType.Archer) AudioManager.Instance.PlaySE("ArrowShot");
            else if (unitType == UnitType.Mage) AudioManager.Instance.PlaySE("MagicFire");
            else AudioManager.Instance.PlaySE("SwordHit");
        }

        // 攻撃アニメーション
        if (anim != null)
        {
            if (isBossMonster)
            {
                // BossPack1: Attack / Attack2 / Attack3
                string[] bossAtks = { "Attack", "Attack2", "Attack3" };
                anim.SetTrigger(bossAtks[Random.Range(0, bossAtks.Length)]);
            }
            else if (unitType == UnitType.Archer)
                anim.SetTrigger("Shot");
            else if (unitType == UnitType.Monk || unitType == UnitType.Mage)
                anim.SetTrigger("Fire");
            else
                anim.SetTrigger(Random.value > 0.5f ? "Slash" : "Jab");
        }
        attackAnimLock = 0.4f;

        // Mage: 火球を飛ばしてからダメージ（遠距離攻撃演出）
        if (unitType == UnitType.Mage)
        {
            StartCoroutine(FireballAttack(target, attackPower));
        }
        // Lancer: 貫通攻撃（メインターゲット背後の敵にも当たる）
        else if (unitType == UnitType.Lancer)
        {
            int damage = attackPower;
            target.TakeDamage(damage, this);
            OnDamageDealt?.Invoke(this, target, damage);
            AddXP(damage);
            // 貫通: ターゲットの後方にいる敵にも半減ダメージ
            var gm2 = GameManager.Instance;
            if (gm2 != null)
            {
                float pierceRange = attackRange + 1.0f;
                int pierceDmg = Mathf.Max(1, damage / 2);
                foreach (var e in gm2.GetUnits(team == Team.Ally ? Team.Enemy : Team.Ally))
                {
                    if (e == null || e.isDead || e == target) continue;
                    // ターゲットと同じ方向で、ターゲットより奥にいる敵
                    float dir = facingRight ? 1f : -1f;
                    float dist = (e.transform.position.x - transform.position.x) * dir;
                    float targetDist = (target.transform.position.x - transform.position.x) * dir;
                    if (dist > targetDist && dist <= pierceRange &&
                        Mathf.Abs(e.transform.position.y - transform.position.y) <= 0.8f)
                    {
                        e.TakeDamage(pierceDmg, this);
                        OnDamageDealt?.Invoke(this, e, pierceDmg);
                        AddXP(pierceDmg);
                    }
                }
            }
        }
        else
        {
            int damage = attackPower;
            target.TakeDamage(damage, this);
            OnDamageDealt?.Invoke(this, target, damage);
            AddXP(damage); // 与ダメージ → 経験値
        }
    }

    void PerformHeal(Unit target)
    {
        int amount = Mathf.Min(healAmount, target.maxHP - target.currentHP);
        if (amount <= 0) return;
        target.currentHP += amount;
        DamagePopup.Create(target.transform.position, amount, true);
        OnHealPerformed?.Invoke(this, target, amount);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("Heal");

        // 回復量 → 経験値
        AddXP(amount);

        // Pixel Heroes: Fire trigger で回復アニメーション
        if (anim != null) anim.SetTrigger("Fire");
        attackAnimLock = 0.4f;
    }

    // ─── Boss Knockback Attack ────────────────────────────────

    void PerformKnockbackAttack(Unit target)
    {
        knockbackAttackCooldown = 5f;

        // SE + animation
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("BossSmash");
        if (anim != null) anim.SetTrigger(isBossMonster ? "Attack" : "Slash");
        attackAnimLock = 0.6f;

        // Damage (same as normal attack)
        int damage = attackPower;
        target.TakeDamage(damage, this);
        OnDamageDealt?.Invoke(this, target, damage);

        // Camera shake
        CameraShake.Shake(0.3f, 0.1f);

        // Launch knockback coroutine
        if (!target.isDead)
            StartCoroutine(KnockbackFly(target));
    }

    System.Collections.IEnumerator KnockbackFly(Unit target)
    {
        if (target == null || target.isDead) yield break;

        target.isKnockedBack = true;
        Vector3 startPos = target.transform.position;

        // Impact VFX at hit point
        StartCoroutine(KnockbackImpactVFX(startPos));

        float flyDuration = 0.4f;
        float flyDistance = 6f;
        float speed = flyDistance / flyDuration;
        float elapsed = 0f;
        var scattered = new HashSet<Unit>();

        while (elapsed < flyDuration && target != null && !target.isDead)
        {
            elapsed += Time.deltaTime;
            target.transform.position += Vector3.left * speed * Time.deltaTime;

            // Scatter allies along the path
            var gm = GameManager.Instance;
            if (gm != null)
            {
                foreach (var ally in gm.GetUnits(Team.Ally))
                {
                    if (ally == null || ally.isDead || ally == target || scattered.Contains(ally)) continue;
                    if (ally.isKnockedBack) continue;
                    float dist = Vector2.Distance(target.transform.position, ally.transform.position);
                    if (dist < 0.8f)
                    {
                        scattered.Add(ally);
                        // Push in random Y direction
                        float yDir = (ally.transform.position.y > target.transform.position.y) ? 1f : -1f;
                        if (Mathf.Abs(ally.transform.position.y - target.transform.position.y) < 0.1f)
                            yDir = Random.value > 0.5f ? 1f : -1f;
                        Vector2 scatterDir = new Vector2(-0.3f, yDir).normalized;
                        StartCoroutine(KnockbackScatter(ally, scatterDir));
                    }
                }
            }
            yield return null;
        }

        // Dust VFX at landing point
        if (target != null)
        {
            StartCoroutine(KnockbackDustVFX(target.transform.position));
            target.isKnockedBack = false;
            target.stunTimer = 0.5f;
            target.SetDesiredAnim("idle");
        }
    }

    System.Collections.IEnumerator KnockbackScatter(Unit unit, Vector2 dir)
    {
        if (unit == null || unit.isDead) yield break;

        unit.stunTimer = 0.3f;
        if (unit.anim != null) unit.anim.SetTrigger("Hit");

        float duration = 0.2f;
        float distance = 1.5f;
        float speed = distance / duration;
        float elapsed = 0f;

        while (elapsed < duration && unit != null && !unit.isDead)
        {
            elapsed += Time.deltaTime;
            unit.transform.position += (Vector3)(dir * speed * Time.deltaTime);
            yield return null;
        }
    }

    System.Collections.IEnumerator KnockbackImpactVFX(Vector3 pos)
    {
        Color color = new Color(1f, 0.95f, 0.7f);
        for (int i = 0; i < 8; i++)
        {
            var p = new GameObject("KBImpact");
            p.transform.position = pos;
            var pSr = p.AddComponent<SpriteRenderer>();
            pSr.sprite = CreatePixelSprite();
            float brightness = Random.Range(0.8f, 1f);
            pSr.color = new Color(
                Mathf.Min(1f, color.r * brightness),
                Mathf.Min(1f, color.g * brightness),
                Mathf.Min(1f, color.b * brightness), 0.95f);
            pSr.sortingOrder = 25;
            float size = Random.Range(0.08f, 0.15f);
            p.transform.localScale = new Vector3(size, size, 1f);

            float angle = (360f / 8) * i * Mathf.Deg2Rad + Random.Range(-0.3f, 0.3f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            StartCoroutine(UltBurstParticleFly(p, pSr, dir, Random.Range(3f, 5f), 1.5f));
        }
        yield break;
    }

    System.Collections.IEnumerator KnockbackDustVFX(Vector3 pos)
    {
        Color dustColor = new Color(0.6f, 0.45f, 0.25f, 0.8f);
        for (int i = 0; i < 4; i++)
        {
            var p = new GameObject("KBDust");
            p.transform.position = pos + new Vector3(Random.Range(-0.3f, 0.3f), -0.2f, 0);
            var pSr = p.AddComponent<SpriteRenderer>();
            pSr.sprite = CreatePixelSprite();
            pSr.color = dustColor;
            pSr.sortingOrder = 24;
            float size = Random.Range(0.1f, 0.18f);
            p.transform.localScale = new Vector3(size, size, 1f);

            float angle = Random.Range(60f, 120f) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            StartCoroutine(UltBurstParticleFly(p, pSr, dir, Random.Range(1f, 2.5f), 1f));
        }
        yield break;
    }

    // ─── Damage ─────────────────────────────────────────────────

    public void TakeDamage(int damage, Unit attacker)
    {
        if (isDead) return;

        if (damageReduction > 0f)
            damage = Mathf.RoundToInt(damage * (1f - damageReduction));
        damage = Mathf.Max(1, damage);

        currentHP -= damage;
        DamagePopup.Create(transform.position, damage, false);

        // 被ダメージ → 経験値（味方のみ）
        if (team == Team.Ally)
            AddXP(damage);

        // Pixel Heroes: Hit trigger で被ダメアニメーション
        if (anim != null && attackAnimLock <= 0f)
            anim.SetTrigger("Hit");

        // Flash red
        if (sr != null)
            StartCoroutine(DamageFlash());

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die(attacker);
        }
    }

    System.Collections.IEnumerator DamageFlash()
    {
        if (sr == null) yield break;
        Color orig = sr.color;
        sr.color = new Color(1f, 0.3f, 0.3f, orig.a);
        yield return new WaitForSeconds(0.1f);
        if (sr != null && !isDead)
            sr.color = orig;
    }

    // ─── Mage Fire Effects ─────────────────────────────────────

    System.Collections.IEnumerator FireballAttack(Unit target, int damage)
    {
        Vector3 startPos = transform.position + new Vector3(facingRight ? 0.3f : -0.3f, 0.2f, 0);
        Vector3 endPos = target != null ? target.transform.position : startPos + Vector3.right * 3f;

        // 火球本体
        var fireball = MakeVFX("Fireball");
        fireball.transform.position = startPos;
        var fbSr = fireball.AddComponent<SpriteRenderer>();
        fbSr.sprite = CreatePixelSprite();
        fbSr.color = new Color(1f, 0.7f, 0.1f);
        fbSr.sortingOrder = 20;
        fireball.transform.localScale = new Vector3(0.25f, 0.25f, 1f);

        // 火球の芯（白）
        var core = new GameObject("Core");
        core.transform.SetParent(fireball.transform);
        core.transform.localPosition = Vector3.zero;
        var coreSr = core.AddComponent<SpriteRenderer>();
        coreSr.sprite = CreatePixelSprite();
        coreSr.color = new Color(1f, 1f, 0.8f);
        coreSr.sortingOrder = 21;
        core.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        float flyTime = 0.25f;
        float elapsed = 0f;
        float trailTimer = 0f;

        while (elapsed < flyTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flyTime;

            // ターゲット追従（動いている敵にも当たる）
            if (target != null && !target.isDead)
                endPos = target.transform.position;

            fireball.transform.position = Vector3.Lerp(startPos, endPos, t);

            // 火球のサイズ揺れ
            float pulse = 0.25f + 0.05f * Mathf.Sin(elapsed * 30f);
            fireball.transform.localScale = new Vector3(pulse, pulse, 1f);

            // 軌跡パーティクル
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.03f;
                SpawnFireTrail(fireball.transform.position);
            }

            yield return null;
        }

        // 着弾: ダメージ + 炎上エフェクト
        if (target != null && !target.isDead)
        {
            target.TakeDamage(damage, this);
            OnDamageDealt?.Invoke(this, target, damage);
            AddXP(damage); // 与ダメージ → 経験値
            target.StartCoroutine(target.BurnEffect());
        }

        // 着弾爆発
        StartCoroutine(FireExplosion(endPos));

        Destroy(fireball);
    }

    void SpawnFireTrail(Vector3 pos)
    {
        var trail = new GameObject("FireTrail");
        trail.transform.position = pos + new Vector3(
            Random.Range(-0.05f, 0.05f), Random.Range(-0.05f, 0.05f), 0);
        var trSr = trail.AddComponent<SpriteRenderer>();
        trSr.sprite = CreatePixelSprite();
        trSr.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        trSr.sortingOrder = 19;
        float size = Random.Range(0.08f, 0.15f);
        trail.transform.localScale = new Vector3(size, size, 1f);
        StartCoroutine(FadeAndDestroy(trail, trSr, 0.2f));
    }

    System.Collections.IEnumerator FireExplosion(Vector3 pos)
    {
        // 爆発パーティクル（8方向に飛び散る火花）
        for (int i = 0; i < 8; i++)
        {
            var spark = new GameObject("FireSpark");
            spark.transform.position = pos;
            var spSr = spark.AddComponent<SpriteRenderer>();
            spSr.sprite = CreatePixelSprite();
            spSr.color = new Color(1f, Random.Range(0.3f, 0.8f), 0.1f, 0.9f);
            spSr.sortingOrder = 20;
            float size = Random.Range(0.06f, 0.12f);
            spark.transform.localScale = new Vector3(size, size, 1f);

            float angle = i * 45f * Mathf.Deg2Rad + Random.Range(-0.3f, 0.3f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            StartCoroutine(SparkFly(spark, spSr, dir));
        }
        yield break;
    }

    System.Collections.IEnumerator SparkFly(GameObject spark, SpriteRenderer spSr, Vector3 dir)
    {
        float speed = Random.Range(1.5f, 3f);
        float life = Random.Range(0.15f, 0.3f);
        float elapsed = 0f;
        Vector3 startPos = spark.transform.position;

        while (elapsed < life && spark != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            spark.transform.position = startPos + dir * speed * elapsed;
            float alpha = 1f - t;
            float size = spark.transform.localScale.x * (1f - Time.deltaTime * 3f);
            spark.transform.localScale = new Vector3(
                Mathf.Max(0.02f, size), Mathf.Max(0.02f, size), 1f);
            spSr.color = new Color(spSr.color.r, spSr.color.g, 0.1f, alpha);
            yield return null;
        }
        if (spark != null) Destroy(spark);
    }

    System.Collections.IEnumerator BurnEffect()
    {
        if (sr == null || isDead) yield break;

        // 炎上フラッシュ（オレンジ点滅）
        Color orig = sr.color;
        float burnDuration = 0.6f;
        float elapsed = 0f;
        float particleTimer = 0f;

        while (elapsed < burnDuration && sr != null && !isDead)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / burnDuration;

            // オレンジ↔元色の点滅
            float flash = Mathf.PingPong(elapsed * 8f, 1f);
            sr.color = Color.Lerp(orig, new Color(1f, 0.5f, 0.1f, orig.a), flash * (1f - t));

            // 炎パーティクル
            particleTimer -= Time.deltaTime;
            if (particleTimer <= 0f)
            {
                particleTimer = 0.08f;
                SpawnBurnParticle(transform.position);
            }

            yield return null;
        }

        if (sr != null && !isDead) sr.color = orig;
    }

    void SpawnBurnParticle(Vector3 pos)
    {
        var flame = new GameObject("BurnFlame");
        flame.transform.position = pos + new Vector3(
            Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.3f), 0);
        var fSr = flame.AddComponent<SpriteRenderer>();
        fSr.sprite = CreatePixelSprite();
        fSr.color = new Color(1f, Random.Range(0.3f, 0.7f), 0f, 0.9f);
        fSr.sortingOrder = 15;
        float size = Random.Range(0.06f, 0.12f);
        flame.transform.localScale = new Vector3(size, size, 1f);
        StartCoroutine(FadeAndDestroy(flame, fSr, 0.35f, true));
    }

    System.Collections.IEnumerator FadeAndDestroy(GameObject go, SpriteRenderer goSr,
        float lifetime, bool rise = false)
    {
        float elapsed = 0f;
        Vector3 startPos = go.transform.position;
        float riseSpeed = rise ? Random.Range(0.8f, 1.5f) : 0f;

        while (elapsed < lifetime && go != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            goSr.color = new Color(goSr.color.r, goSr.color.g, goSr.color.b, 1f - t);
            float s = go.transform.localScale.x * (1f - Time.deltaTime * 2f);
            go.transform.localScale = new Vector3(Mathf.Max(0.01f, s), Mathf.Max(0.01f, s), 1f);
            if (rise) go.transform.position = startPos + VFXUp() * riseSpeed * elapsed;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    void Die(Unit killer)
    {
        isDead = true;
        deathTimer = DEATH_DURATION;

        // 吹き出しが表示中なら同オーナーの生存ユニットに移譲
        if (speechBubbleGo != null && speechBubbleGo.activeSelf && speechText != null)
        {
            string bubbleMsg = speechText.text;
            float remainTime = speechTimer;
            speechBubbleGo.SetActive(false);
            if (team == Team.Ally && GameManager.Instance != null)
            {
                foreach (var u in GameManager.Instance.allyUnits)
                {
                    if (u == null || u == this || u.isDead || u.ownerName != ownerName) continue;
                    u.ShowSpeechBubble(bubbleMsg);
                    break;
                }
            }
        }

        // Pixel Heroes: Die bool で死亡アニメーション
        if (anim != null) anim.SetBool("Die", true);

        OnUnitKilled?.Invoke(killer, this);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("UnitDeath");

        // キルボーナスXP（倒した敵のmaxHPの半分）
        if (killer != null && !killer.isDead && killer.team == Team.Ally)
            killer.AddXP(maxHP / 2);

        // デスペナルティ: 味方ユニット死亡時にレベル1減少
        if (team == Team.Ally && !string.IsNullOrEmpty(viewerId) && ViewerStats.Instance != null)
            ViewerStats.Instance.ApplyDeathPenalty(viewerId);

        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Knockback direction (away from killer, or default left/right)
        if (killer != null)
            knockbackDir = (transform.position - killer.transform.position).normalized;
        else
            knockbackDir = (team == Team.Ally) ? Vector3.left : Vector3.right;
    }

    // ─── レベルアップ ──────────────────────────────────

    /// <summary>レベルアップに必要なXP（レベルが上がるほど少し増える）</summary>
    int XPForNextLevel() => 50 + level * level;

    /// <summary>経験値を加算（ダメージ/被ダメ/回復量）</summary>
    public void AddXP(int amount)
    {
        if (isDead || amount <= 0) return;
        // Archer: 遠距離安全圏からのXP稼ぎを抑制（半減）
        if (unitType == UnitType.Archer)
            amount = Mathf.Max(1, amount / 2);
        // XP量キャップ（異常値防止）
        amount = Mathf.Min(amount, 500);
        xp += amount;
        // 1回のAddXPで最大5レベルまで上昇
        int levelsGained = 0;
        while (xp >= XPForNextLevel() && levelsGained < 5)
        {
            xp -= XPForNextLevel();
            LevelUp();
            levelsGained++;
        }
        // 余剰XPがあっても次回持ち越し（溢れない）
        if (xp >= XPForNextLevel())
            xp = XPForNextLevel() - 1;
    }

    void LevelUp()
    {
        level++;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("LevelUp");

        // ステータス微増（+8%ずつ）
        float boost = 1.08f;
        maxHP = Mathf.RoundToInt(maxHP * boost);
        currentHP = Mathf.Min(currentHP + Mathf.RoundToInt(maxHP * 0.3f), maxHP); // HP30%回復
        attackPower = Mathf.RoundToInt(attackPower * boost);
        baseAttackPower = attackPower; // ベース値も更新
        if (healAmount > 0)
            healAmount = Mathf.RoundToInt(healAmount * boost);

        // 体がほんの少し大きくなる（上限: 基準のMaxSizeMultiplier倍）
        float maxUnitScale = GameConfig.PixelHeroBaseScale * GameConfig.MaxSizeMultiplier;
        if (originalScale.x < maxUnitScale)
        {
            float sizeBoost = GameConfig.LevelUpSizeBoost;
            originalScale *= sizeBoost;
            if (originalScale.x > maxUnitScale)
                originalScale = new Vector3(maxUnitScale, maxUnitScale, 1f);
            transform.localScale = originalScale;
        }

        // レベルアップエフェクト（白フラッシュ）
        if (sr != null)
            StartCoroutine(LevelUpFlash());

        UpdateNameLabel();

        DamagePopup.CreateText(transform.position + Vector3.up * 0.3f,
            $"Lv.{level}!", new Color(1f, 0.9f, 0.3f));
        Debug.Log($"[Unit] {ownerName} の {unitType} が Lv.{level} にレベルアップ！");

        // ViewerStatsに最高レベルを記録
        if (team == Team.Ally && !string.IsNullOrEmpty(viewerId) && ViewerStats.Instance != null)
            ViewerStats.Instance.UpdateBestLevel(viewerId, level, xp);
    }

    /// <summary>セーブデータからレベルを復元する</summary>
    public void RestoreLevel(int savedLevel, int savedXP)
    {
        if (savedLevel <= 1) return;

        // レベル1からsavedLevelまでのステータスを一括適用
        float boost = Mathf.Pow(1.08f, savedLevel - 1);
        maxHP = Mathf.RoundToInt(maxHP * boost);
        currentHP = maxHP;
        attackPower = Mathf.RoundToInt(attackPower * boost);
        baseAttackPower = attackPower;
        if (healAmount > 0)
            healAmount = Mathf.RoundToInt(healAmount * boost);

        // 体サイズ成長
        float maxUnitScale = GameConfig.PixelHeroBaseScale * GameConfig.MaxSizeMultiplier;
        float sizeBoost = Mathf.Pow(GameConfig.LevelUpSizeBoost, savedLevel - 1);
        originalScale *= sizeBoost;
        if (originalScale.x > maxUnitScale)
            originalScale = new Vector3(maxUnitScale, maxUnitScale, 1f);
        transform.localScale = originalScale;

        level = savedLevel;
        xp = savedXP;

        UpdateNameLabel();
        Debug.Log($"[Unit] {ownerName} の {unitType} をレベル {savedLevel} (XP:{savedXP}) で復元");
    }

    System.Collections.IEnumerator LevelUpFlash()
    {
        Color orig = sr.color;
        sr.color = Color.white;
        yield return new WaitForSeconds(0.15f);
        if (sr != null && !isDead) sr.color = orig;
    }

    void HandleDeath()
    {
        deathTimer -= Time.deltaTime;
        float t = Mathf.Clamp01(deathTimer / DEATH_DURATION);

        // Phase 1 (t > 0.5): red flash + knockback
        // Phase 2 (t <= 0.5): fade out + shrink + fall
        if (t > 0.5f)
        {
            // Knockback
            float knockbackSpeed = 3f * (t - 0.5f) * 2f;
            transform.position += knockbackDir * knockbackSpeed * Time.deltaTime;

            // Red flash
            if (sr != null)
            {
                float flash = Mathf.PingPong(Time.time * 12f, 1f);
                sr.color = Color.Lerp(Color.white, new Color(1f, 0.2f, 0.2f), flash);
            }

            // Keep original scale
            transform.localScale = originalScale;
        }
        else
        {
            // Fade out + shrink + sink
            float fadeT = t / 0.5f; // 1 -> 0
            if (sr != null)
            {
                Color c = sr.color;
                c = Color.white;
                c.a = fadeT;
                sr.color = c;
            }
            if (faceIconSr != null)
            {
                var fc = faceIconSr.color;
                fc.a = fadeT;
                faceIconSr.color = fc;
            }

            // Shrink and rotate
            transform.localScale = originalScale * (0.3f + 0.7f * fadeT);
            transform.Rotate(0, 0, (team == Team.Ally ? -1f : 1f) * 120f * Time.deltaTime);

            // Sink
            transform.position += Vector3.down * 0.5f * Time.deltaTime;
        }

        if (deathTimer <= 0f)
            Destroy(gameObject);
    }

    // ─── Targeting ───────────────────────────────────────────────

    Unit FindNearestEnemy()
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;

        Team enemyTeam = (team == Team.Ally) ? Team.Enemy : Team.Ally;
        var enemies = gm.GetUnits(enemyTeam);

        Unit nearest = null;
        float minDist = float.MaxValue;

        foreach (var u in enemies)
        {
            if (u == null || u.isDead) continue;
            float dist = Vector2.Distance(transform.position, u.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = u;
            }
        }
        return nearest;
    }

    Unit FindMostDamagedAlly()
    {
        var gm = GameManager.Instance;
        if (gm == null) return null;

        var allies = gm.GetUnits(team);
        Unit best = null;
        int maxMissing = 0;

        foreach (var u in allies)
        {
            if (u == null || u.isDead || u == this) continue;
            int missing = u.maxHP - u.currentHP;
            if (missing > maxMissing)
            {
                maxMissing = missing;
                best = u;
            }
        }
        return best;
    }

    // ─── Animation ───────────────────────────────────────────────

    /// <summary>
    /// Pixel Heroes アニメーション駆動。
    /// Bool パラメータ (Idle, Run, Die) で状態制御。
    /// 攻撃/回復/被ダメは Trigger で直接発火（PerformAttack等から）。
    /// </summary>
    void DriveAnimation()
    {
        if (anim == null || anim.runtimeAnimatorController == null) return;

        // 攻撃アニメーション中はBool変更しない（Triggerアニメを中断させないため）
        if (attackAnimLock > 0f)
        {
            attackAnimLock -= Time.deltaTime;
            return;
        }

        bool isRunning = desiredAnim == "run";
        anim.SetBool("Run", isRunning);
        anim.SetBool("Idle", !isRunning);
    }

    // ─── HP Bar & Name Label ─────────────────────────────────────

    void CreateHPBar()
    {
        // PixelHeroBaseScale=0.4で描画されるため、ローカル座標は大きめに設定
        // 幅1.8 × 高さ0.2 → ワールド空間で約 0.72 × 0.08
        var bgGo = new GameObject("HPBarBG");
        bgGo.transform.SetParent(transform);
        bgGo.transform.localPosition = new Vector3(0, -0.6f, 0);
        var bgSr = bgGo.AddComponent<SpriteRenderer>();
        bgSr.sprite = CreatePixelSprite();
        bgSr.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        bgSr.sortingOrder = 50;
        bgGo.transform.localScale = new Vector3(1.8f, 0.2f, 1f);
        hpBarBg = bgGo.transform;

        var fillGo = new GameObject("HPBarFill");
        fillGo.transform.SetParent(transform);
        fillGo.transform.localPosition = new Vector3(0, -0.6f, 0);
        var fillSr = fillGo.AddComponent<SpriteRenderer>();
        fillSr.sprite = CreatePixelSprite();
        fillSr.color = new Color(0.2f, 0.9f, 0.2f, 0.9f);
        fillSr.sortingOrder = 51;
        fillGo.transform.localScale = new Vector3(1.8f, 0.2f, 1f);
        hpBarFill = fillGo.transform;
    }

    void CreateNameLabel()
    {
        string displayName = string.IsNullOrEmpty(ownerName) ? "NPC" : ownerName;

        var labelGo = new GameObject("NameLabel");
        labelGo.transform.SetParent(transform);
        labelGo.transform.localPosition = new Vector3(0, 4.0f, 0);
        nameLabel = labelGo.AddComponent<TextMesh>();
        nameLabel.font = GetFont();
        nameLabel.GetComponent<MeshRenderer>().material = GetFont().material;
        nameLabel.text = displayName;
        nameLabel.fontSize = 36;
        nameLabel.characterSize = 0.1f;
        nameLabel.anchor = TextAnchor.MiddleCenter;
        nameLabel.alignment = TextAlignment.Center;
        nameLabel.color = team == Team.Ally
            ? new Color(1f, 1f, 0.8f)
            : new Color(1f, 0.7f, 0.7f);

        var mr = labelGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 52;

        var shadowGo = new GameObject("NameShadow");
        shadowGo.transform.SetParent(transform);
        shadowGo.transform.localPosition = new Vector3(0.01f, 3.99f, 0);
        nameShadow = shadowGo.AddComponent<TextMesh>();
        nameShadow.font = GetFont();
        nameShadow.GetComponent<MeshRenderer>().material = GetFont().material;
        nameShadow.text = displayName;
        nameShadow.fontSize = 36;
        nameShadow.characterSize = 0.1f;
        nameShadow.anchor = TextAnchor.MiddleCenter;
        nameShadow.alignment = TextAlignment.Center;
        nameShadow.color = new Color(0, 0, 0, 0.7f);
        var shadowMr = shadowGo.GetComponent<MeshRenderer>();
        if (shadowMr != null) shadowMr.sortingOrder = 51;

        // 3Dモード用: 親のBillboard3Dに子ターゲットとして登録
        var bb = GetComponent<Billboard3D>();
        if (bb != null)
        {
            bb.childCylindricalTargets.Add(labelGo.transform);
            bb.childCylindricalTargets.Add(shadowGo.transform);
        }

        UpdateNameLabel();
    }

    public void UpdateNameLabel()
    {
        if (nameLabel == null) return;
        string text;
        if (team == Team.Enemy)
            text = $"Lv.{level}";
        else
        {
            string displayName = string.IsNullOrEmpty(ownerName) ? "NPC" : ownerName;
            text = $"{displayName} Lv.{level}";
        }
        nameLabel.text = text;
        if (nameShadow != null) nameShadow.text = text;

        // ラベルサイズ制限（親スケールを打ち消す）
        float clamp = GetUIScaleClamp();
        float labelY = 4.0f * clamp;
        nameLabel.transform.localPosition = new Vector3(0, labelY, 0);
        nameLabel.characterSize = 0.1f * clamp;
        if (nameShadow != null)
        {
            nameShadow.transform.localPosition = new Vector3(0.01f * clamp, labelY - 0.01f * clamp, 0);
            nameShadow.characterSize = 0.1f * clamp;
        }
    }

    /// <summary>3Dビルボード時のテキスト左右反転を補正（+Z=カメラ方向でTextMeshが裏返る）</summary>
    void Update3DTextFlip()
    {
        if (!CameraView3D.is3DFullScreen)
        {
            // 2D復帰: 3Dで設定したX反転をリセット
            if (nameLabel != null && nameLabel.transform.localScale.x < 0)
                nameLabel.transform.localScale = Vector3.one;
            if (nameShadow != null && nameShadow.transform.localScale.x < 0)
                nameShadow.transform.localScale = Vector3.one;
            if (speechText != null && speechText.transform.localScale.x < 0)
                speechText.transform.localScale = Vector3.one;
            return;
        }
        // 3D: テキストのX反転
        if (nameLabel != null)
            nameLabel.transform.localScale = new Vector3(-1f, 1f, 1f);
        if (nameShadow != null)
            nameShadow.transform.localScale = new Vector3(-1f, 1f, 1f);
        if (speechText != null)
            speechText.transform.localScale = new Vector3(-1f, 1f, 1f);
    }

    // ─── Face Icon (Profile Image) ──────────────────────────

    /// <summary>リスナーのプロフィール画像をキャラの顔面に貼り付ける</summary>
    public void SetFaceIcon(Texture2D tex)
    {
        if (tex == null || faceIconSr != null) return;

        var iconGo = new GameObject("FaceIcon");
        iconGo.transform.SetParent(transform);
        // キャラの顔面位置（ローカル座標: 顔は上半分、約y=2.0付近）
        iconGo.transform.localPosition = new Vector3(0, 2.0f, -0.01f);

        faceIconSr = iconGo.AddComponent<SpriteRenderer>();
        // Texture2Dからスプライトを生成（正方形にクロップ）
        int size = Mathf.Min(tex.width, tex.height);
        var sprite = Sprite.Create(tex,
            new Rect((tex.width - size) / 2f, (tex.height - size) / 2f, size, size),
            new Vector2(0.5f, 0.5f), size); // PPU = size → 1ワールドユニット
        faceIconSr.sprite = sprite;
        faceIconSr.sortingOrder = sr != null ? sr.sortingOrder + 2 : 12;

        UpdateFaceIcon();
    }

    void UpdateFaceIcon()
    {
        if (faceIconSr == null) return;
        float clamp = GetUIScaleClamp();
        // アイコンサイズ: 1.2ローカルユニット（キャラの頭のサイズ程度）
        float iconSize = 1.2f * clamp;
        faceIconSr.transform.localScale = new Vector3(iconSize, iconSize, 1f);
        faceIconSr.transform.localPosition = new Vector3(0, 2.0f * clamp, -0.01f);
        // SpriteRendererのsortingOrderを同期
        if (sr != null) faceIconSr.sortingOrder = sr.sortingOrder + 2;
    }

    // ─── YouTube Action Buffs ────────────────────────────────

    /// <summary>ステータスに乗算バフを適用（HP回復付き）</summary>
    public void ApplyStatMultiplier(float mult)
    {
        if (mult <= 1f) return;
        maxHP = Mathf.RoundToInt(maxHP * mult);
        currentHP = Mathf.Min(currentHP + Mathf.RoundToInt(maxHP * 0.3f), maxHP);
        attackPower = Mathf.RoundToInt(attackPower * mult);
        baseAttackPower = attackPower; // 恒久バフなのでベースも更新
        if (healAmount > 0) healAmount = Mathf.RoundToInt(healAmount * mult);
    }

    /// <summary>スーパーチャットティアをアップグレード</summary>
    public void UpgradeSuperChatTier(int newTier)
    {
        if (newTier < 0) return;
        if (newTier <= superChatTier) return;

        // 差分バフを適用
        float oldBuff = superChatTier >= 0 ? GameConfig.SuperChatStatBuff[superChatTier] : 1f;
        float newBuff = GameConfig.SuperChatStatBuff[newTier];
        ApplyStatMultiplier(newBuff / oldBuff);

        // 体を大きくする（差分のみ適用、上限あり）
        float maxUnitScale = GameConfig.PixelHeroBaseScale * GameConfig.MaxSizeMultiplier;
        float oldSize = superChatTier >= 0 ? (1f + superChatTier * 0.1f) : 1f;
        float newSize = 1f + newTier * 0.1f;
        if (newSize > oldSize && originalScale.x < maxUnitScale)
        {
            float ratio = newSize / oldSize;
            originalScale *= ratio;
            if (originalScale.x > maxUnitScale)
                originalScale = new Vector3(maxUnitScale, maxUnitScale, 1f);
            transform.localScale = originalScale;
        }

        superChatTier = newTier;

        // グロウエフェクト（ティアカラーで光るオーラ）
        CreateSuperChatGlow();
    }

    /// <summary>メンバーバフを適用</summary>
    public void ApplyMemberBuff()
    {
        if (isMember) return;
        isMember = true;
        ApplyStatMultiplier(GameConfig.MemberStatBuff);
        CreateMemberBadge();
    }

    // ─── TikTok Action Buffs ────────────────────────────────

    /// <summary>TikTokギフトティアをアップグレード</summary>
    public void UpgradeTikTokGiftTier(int newTier)
    {
        if (newTier < 0) return;
        if (newTier <= tiktokGiftTier) return;

        // 差分バフを適用
        float oldBuff = tiktokGiftTier >= 0 ? GameConfig.TikTokGiftStatBuff[tiktokGiftTier] : 1f;
        float newBuff = GameConfig.TikTokGiftStatBuff[newTier];
        ApplyStatMultiplier(newBuff / oldBuff);

        // 体を大きくする（上限あり）
        float maxUnitScale = GameConfig.PixelHeroBaseScale * GameConfig.MaxSizeMultiplier;
        float oldSize = tiktokGiftTier >= 0 ? (1f + tiktokGiftTier * 0.1f) : 1f;
        float newSize = 1f + newTier * 0.1f;
        if (newSize > oldSize && originalScale.x < maxUnitScale)
        {
            float ratio = newSize / oldSize;
            originalScale *= ratio;
            if (originalScale.x > maxUnitScale)
                originalScale = new Vector3(maxUnitScale, maxUnitScale, 1f);
            transform.localScale = originalScale;
        }

        tiktokGiftTier = newTier;

        // グロウエフェクト（ティアカラーで光るオーラ）
        CreateTikTokGiftGlow();
    }

    void CreateTikTokGiftGlow()
    {
        if (tiktokGiftTier < 0) return;
        Color c = GameConfig.TikTokGiftTierColors[tiktokGiftTier];
        c.a = 1f;
        CreateOrUpdateAuraGlow(c);
    }

    /// <summary>チームバフを適用</summary>
    public void ApplyTeamBuff(int levelTier)
    {
        if (hasTeamBuff) return;
        hasTeamBuff = true;
        float hpBuff = GameConfig.TeamHPBuff[levelTier];
        float atkBuff = GameConfig.TeamATKBuff[levelTier];
        maxHP = Mathf.RoundToInt(maxHP * hpBuff);
        currentHP = Mathf.Min(currentHP + Mathf.RoundToInt(maxHP * 0.2f), maxHP);
        attackPower = Mathf.RoundToInt(attackPower * atkBuff);
        baseAttackPower = attackPower;
        CreateTeamBadge();
    }

    void CreateTeamBadge()
    {
        if (teamBadge != null) return;
        var badgeGo = new GameObject("TeamBadge");
        badgeGo.transform.SetParent(transform);
        badgeGo.transform.localPosition = new Vector3(0.25f, 0.45f, 0);
        teamBadge = badgeGo.AddComponent<TextMesh>();
        teamBadge.font = GetFont();
        teamBadge.GetComponent<MeshRenderer>().material = GetFont().material;
        teamBadge.text = "\u265A"; // ♚
        teamBadge.fontSize = 28;
        teamBadge.characterSize = 0.08f;
        teamBadge.anchor = TextAnchor.MiddleCenter;
        teamBadge.color = new Color(0.3f, 0.8f, 1f);
        var mr = badgeGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 53;
    }

    /// <summary>サブスクリプションバフを適用</summary>
    public void ApplySubscriptionBuff()
    {
        if (isSubscriber) return;
        isSubscriber = true;
        maxHP = Mathf.RoundToInt(maxHP * GameConfig.SubscriptionHPBuff);
        currentHP = Mathf.Min(currentHP + Mathf.RoundToInt(maxHP * 0.2f), maxHP);
        attackPower = Mathf.RoundToInt(attackPower * GameConfig.SubscriptionATKBuff);
        baseAttackPower = attackPower;
        CreateSubscriptionBadge();
    }

    void CreateSubscriptionBadge()
    {
        if (subscriberBadge != null) return;
        var badgeGo = new GameObject("SubBadge");
        badgeGo.transform.SetParent(transform);
        badgeGo.transform.localPosition = new Vector3(-0.25f, 0.55f, 0);
        subscriberBadge = badgeGo.AddComponent<TextMesh>();
        subscriberBadge.font = GetFont();
        subscriberBadge.GetComponent<MeshRenderer>().material = GetFont().material;
        subscriberBadge.text = "\u2666"; // ♦
        subscriberBadge.fontSize = 28;
        subscriberBadge.characterSize = 0.08f;
        subscriberBadge.anchor = TextAnchor.MiddleCenter;
        subscriberBadge.color = new Color(0.9f, 0.3f, 1f);
        var mr = badgeGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 53;
    }

    /// <summary>オーラ効果を設定（ゴールド以上のギフト）</summary>
    public void SetAura(float hpBuff, float atkBuff, float range)
    {
        auraHPBuff = hpBuff;
        auraATKBuff = atkBuff;
        auraRange = range;
        // オーラの視覚表示は glowSr（蒸気オーラ）に統合。正方形表示は廃止
    }

    void UpdateAura()
    {
        if (auraRange <= 0f) return;

        // 3秒ごとに周囲ユニットにHP回復
        auraTickTimer -= Time.deltaTime;
        if (auraTickTimer <= 0f)
        {
            auraTickTimer = 3f;
            var gm = GameManager.Instance;
            if (gm == null) return;
            var allies = gm.GetUnits(team);
            foreach (var u in allies)
            {
                if (u == null || u == this || u.isDead) continue;
                if (Vector2.Distance(transform.position, u.transform.position) <= auraRange)
                {
                    if (auraHPBuff > 1f)
                    {
                        int hpAdd = Mathf.RoundToInt(u.maxHP * (auraHPBuff - 1f) * 0.1f);
                        u.currentHP = Mathf.Min(u.currentHP + hpAdd, u.maxHP);
                    }
                }
            }
        }
    }

    /// <summary>虹色エフェクトを有効化（ユニバティア）</summary>
    public void SetRainbowEffect()
    {
        isRainbow = true;
    }

    void CreateSuperChatGlow()
    {
        if (superChatTier < 0) return;
        Color c = GameConfig.SuperChatTierColors[superChatTier];
        c.a = 1f;
        CreateOrUpdateAuraGlow(c);
    }

    /// <summary>2層オーラ生成（内側=白、外側=ティアカラー蒸気ノイズ）</summary>
    /// <summary>ボス用の禍々しいオーラを生成</summary>
    public void CreateBossAura()
    {
        CreateOrUpdateAuraGlow(new Color(0.6f, 0.1f, 0.8f, 0.9f)); // 暗紫色
    }

    void CreateOrUpdateAuraGlow(Color color)
    {
        // 内側レイヤー: 白い体のシルエット（ソリッド）
        if (glowSrInner == null)
        {
            var innerGo = new GameObject("AuraGlowInner");
            innerGo.transform.SetParent(transform);
            innerGo.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            glowSrInner = innerGo.AddComponent<SpriteRenderer>();
            glowSrInner.sortingOrder = sr != null ? sr.sortingOrder - 1 : 9;
            innerGo.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            var innerShader = Shader.Find("GUI/Text Shader");
            if (innerShader != null)
                glowSrInner.material = new Material(innerShader);
        }
        if (sr != null)
        {
            glowSrInner.sprite = sr.sprite;
            glowSrInner.flipX = sr.flipX;
        }
        glowSrInner.color = Color.white;

        // 外側レイヤー: ティアカラー蒸気ノイズ
        if (glowSr == null)
        {
            var glowGo = new GameObject("AuraGlow");
            glowGo.transform.SetParent(transform);
            glowGo.transform.localPosition = new Vector3(0f, -0.3f, 0f);
            glowSr = glowGo.AddComponent<SpriteRenderer>();
            glowSr.sortingOrder = sr != null ? sr.sortingOrder - 2 : 8;
            glowGo.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
            var shader = Shader.Find("KingsMarch/AuraSteam");
            if (shader == null) shader = Shader.Find("GUI/Text Shader");
            glowSr.material = new Material(shader);
        }
        if (sr != null)
        {
            glowSr.sprite = sr.sprite;
            glowSr.flipX = sr.flipX;
        }
        glowSr.color = color;
    }

    void CreateMemberBadge()
    {
        if (memberBadge != null) return;
        var badgeGo = new GameObject("MemberBadge");
        badgeGo.transform.SetParent(transform);
        badgeGo.transform.localPosition = new Vector3(-0.25f, 0.45f, 0);
        memberBadge = badgeGo.AddComponent<TextMesh>();
        memberBadge.text = "\u2605"; // ★
        memberBadge.fontSize = 28;
        memberBadge.characterSize = 0.08f;
        memberBadge.anchor = TextAnchor.MiddleCenter;
        memberBadge.color = new Color(1f, 0.85f, 0.2f);
        var mr = badgeGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 53;
    }

    void UpdateGlow()
    {
        if (glowSr == null && glowSrInner == null) return;

        // ティアカラー取得（YouTube SC or TikTok Gift）
        Color tierColor;
        if (superChatTier >= 0)
            tierColor = GameConfig.SuperChatTierColors[superChatTier];
        else if (tiktokGiftTier >= 0)
            tierColor = GameConfig.TikTokGiftTierColors[tiktokGiftTier];
        else
            return;

        float t = Time.time;

        // 毎フレームスプライト同期（アニメ追従 + ソートオーダー同期）
        if (sr != null)
        {
            if (glowSrInner != null)
            {
                glowSrInner.sprite = sr.sprite;
                glowSrInner.flipX = sr.flipX;
                glowSrInner.sortingOrder = sr.sortingOrder - 1;
            }
            if (glowSr != null)
            {
                glowSr.sprite = sr.sprite;
                glowSr.flipX = sr.flipX;
                glowSr.sortingOrder = sr.sortingOrder - 2;
            }
        }

        // 内側レイヤー: 白の微小スケール揺れ
        if (glowSrInner != null)
        {
            float innerPulse = 1.15f + 0.02f * Mathf.Sin(t * 2.5f);
            glowSrInner.transform.localScale = new Vector3(innerPulse, innerPulse, 1f);
            float innerOffsetY = -(innerPulse - 1f) * 0.7f;
            glowSrInner.transform.localPosition = new Vector3(0f, innerOffsetY, 0f);
            glowSrInner.color = Color.white;
        }

        // 外側レイヤー: ティアカラー蒸気ノイズ
        if (glowSr != null)
        {
            tierColor.a = 1f;
            glowSr.color = tierColor;
            float outerPulse = 1.35f + 0.06f * Mathf.Sin(t * 1.8f);
            glowSr.transform.localScale = new Vector3(outerPulse, outerPulse, 1f);
            float outerOffsetY = -(outerPulse - 1f) * 0.7f;
            glowSr.transform.localPosition = new Vector3(0f, outerOffsetY, 0f);
        }

        // 浮遊ドット生成
        dotSpawnTimer -= Time.deltaTime;
        if (dotSpawnTimer <= 0f)
        {
            dotSpawnTimer = 0.15f;
            SpawnAuraDot(tierColor);
        }
    }

    void SpawnAuraDot(Color color)
    {
        var dotGo = new GameObject("AuraDot");
        dotGo.transform.SetParent(transform);
        // 体周辺のランダム位置（オブジェクト空間: x[-1.5,1.5], y[-0.5,2.5]）
        float spawnX = Random.Range(-1.5f, 1.5f);
        float spawnY = Random.Range(-0.5f, 2.5f);
        dotGo.transform.localPosition = new Vector3(spawnX, spawnY, 0f);

        var dotSr = dotGo.AddComponent<SpriteRenderer>();
        dotSr.sprite = CreatePixelSprite();
        float dotSize = Random.Range(0.2f, 0.4f);
        dotGo.transform.localScale = new Vector3(dotSize, dotSize, 1f);
        color.a = 0.9f;
        dotSr.color = color;
        dotSr.sortingOrder = sr != null ? sr.sortingOrder + 1 : 11;

        StartCoroutine(AnimateAuraDot(dotGo, dotSr, color));
    }

    System.Collections.IEnumerator AnimateAuraDot(GameObject dot, SpriteRenderer dotSr, Color color)
    {
        float lifetime = Random.Range(0.6f, 1.0f);
        float elapsed = 0f;
        float driftX = Random.Range(-0.3f, 0.3f);
        float riseSpeed = Random.Range(0.8f, 1.5f);
        Vector3 startPos = dot.transform.localPosition;
        float startSize = dot.transform.localScale.x;

        while (elapsed < lifetime && dot != null)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / lifetime;

            // 上昇 + 横揺れ
            dot.transform.localPosition = startPos + new Vector3(
                driftX * progress,
                riseSpeed * elapsed,
                0f
            );

            // フェードアウト + 縮小
            float alpha = (1f - progress) * 0.9f;
            float size = startSize * (1f - progress * 0.5f);
            dot.transform.localScale = new Vector3(size, size, 1f);
            color.a = alpha;
            dotSr.color = color;

            yield return null;
        }

        if (dot != null) Destroy(dot);
    }

    /// <summary>親スケールを打ち消して最大サイズを制限する倍率</summary>
    float GetUIScaleClamp()
    {
        float parentScale = transform.localScale.x;
        if (parentScale <= 0.01f) return 1f;
        // 基準スケール(0.4)の2倍(=0.8)を上限
        float maxScale = GameConfig.PixelHeroBaseScale * 2f;
        if (parentScale > maxScale)
            return maxScale / parentScale;
        return 1f;
    }

    void UpdateHPBar()
    {
        if (hpBarFill == null) return;
        float ratio = (float)currentHP / maxHP;
        float fullWidth = 1.8f;
        float clamp = GetUIScaleClamp();
        hpBarFill.localScale = new Vector3(fullWidth * ratio * clamp, 0.2f * clamp, 1f);

        var fillSr = hpBarFill.GetComponent<SpriteRenderer>();
        if (fillSr != null)
        {
            if (ratio > 0.5f)
                fillSr.color = Color.Lerp(Color.yellow, new Color(0.2f, 0.9f, 0.2f), (ratio - 0.5f) * 2f);
            else
                fillSr.color = Color.Lerp(new Color(0.9f, 0.2f, 0.2f), Color.yellow, ratio * 2f);
        }

        float offset = -(fullWidth * clamp - fullWidth * clamp * ratio) / 2f;
        hpBarFill.localPosition = new Vector3(offset, -0.6f * clamp, 0);

        if (hpBarBg != null)
        {
            hpBarBg.localScale = new Vector3(fullWidth * clamp, 0.2f * clamp, 1f);
            hpBarBg.localPosition = new Vector3(0, -0.6f * clamp, 0);
        }
    }

    // ─── Speech Bubble ────────────────────────────────────────

    public void ShowSpeechBubble(string message)
    {
        if (isDead) return;

        // 29文字まで表示、30文字目以降は…
        if (message.Length > 29)
            message = message.Substring(0, 29) + "\u2026";

        if (speechBubbleGo == null)
            CreateSpeechBubble();

        // 1行あたりの最大文字数で自動改行
        const int maxCharsPerLine = 15;
        string wrapped = WrapText(message, maxCharsPerLine);

        speechText.text = wrapped;
        speechTimer = Mathf.Max(4f, message.Length * 0.05f);
        speechBubbleGo.SetActive(true);

        // 吹き出しは体に比例するが成長率を抑制（40%の成長率）
        float ps2 = Mathf.Max(transform.localScale.x, 0.1f);
        float dampInv2 = Mathf.Pow(ps2, -0.6f);
        speechBubbleGo.transform.localScale = new Vector3(dampInv2, dampInv2, 1f);

        // テキスト内容から吹き出しサイズを計算
        float bgWidth, bgHeight;
        var textMr = speechText.GetComponent<MeshRenderer>();
        float ws = Mathf.Max(speechBubbleGo.transform.lossyScale.x, 0.01f);
        bool rotClean = speechBubbleGo.transform.rotation == Quaternion.identity;
        if (rotClean && textMr != null && textMr.bounds.size.magnitude > 0.01f)
        {
            // 2D: boundsから実測（回転なしなら正確）
            bgWidth = textMr.bounds.size.x / ws * 1.15f + 0.8f;
            bgHeight = textMr.bounds.size.y / ws + 0.6f;
        }
        else
        {
            // 3D回転中 or bounds未初期化: 文字数ベースで推定
            int maxLineLen2 = 0; int curLineLen = 0; int lineCount2 = 1;
            foreach (char c in wrapped)
            {
                if (c == '\n') { lineCount2++; curLineLen = 0; }
                else { curLineLen++; if (curLineLen > maxLineLen2) maxLineLen2 = curLineLen; }
            }
            bgWidth = Mathf.Max(1.5f, maxLineLen2 * 0.35f + 0.8f);
            bgHeight = Mathf.Max(0.8f, lineCount2 * 0.5f + 0.6f);
        }
        bgWidth = Mathf.Max(1.5f, bgWidth);
        bgHeight = Mathf.Max(0.8f, bgHeight);
        // スプライトのネイティブサイズで正規化してテキストにフィット
        float sprW = speechBg.sprite != null ? speechBg.sprite.bounds.size.x : 1f;
        float sprH = speechBg.sprite != null ? speechBg.sprite.bounds.size.y : 1f;
        speechBg.transform.localScale = new Vector3(bgWidth / sprW, bgHeight / sprH, 1f);

        // hukidasi画像に尻尾が含まれるため個別の尻尾オブジェクトは不要
    }

    string WrapText(string text, int maxCharsPerLine)
    {
        if (text.Length <= maxCharsPerLine) return text;
        var sb = new System.Text.StringBuilder();
        int count = 0;
        foreach (char c in text)
        {
            if (c == '\n') { sb.Append(c); count = 0; continue; }
            if (count >= maxCharsPerLine) { sb.Append('\n'); count = 0; }
            sb.Append(c);
            count++;
        }
        return sb.ToString();
    }

    static Sprite _hukidasiSprite;
    static Sprite GetHukidasiSprite()
    {
        if (_hukidasiSprite == null)
            _hukidasiSprite = Resources.Load<Sprite>("hukidasi");
        return _hukidasiSprite;
    }

    void CreateSpeechBubble()
    {
        speechBubbleGo = new GameObject("SpeechBubble");
        speechBubbleGo.transform.SetParent(transform, false);
        // キャラの頭上に配置 (スプライト上端 y=3.5 + クリアランス)
        speechBubbleGo.transform.localPosition = new Vector3(0, 5.0f, 0);

        // 吹き出し背景（hukidasi画像、尻尾付き）
        var bgGo = new GameObject("BubbleBg");
        bgGo.transform.SetParent(speechBubbleGo.transform);
        bgGo.transform.localPosition = Vector3.zero;
        speechBg = bgGo.AddComponent<SpriteRenderer>();
        speechBg.sprite = GetHukidasiSprite();
        speechBg.color = Color.white;
        speechBg.sortingOrder = 98;
        // 初期スケールはShowSpeechBubbleで動的に設定

        // テキスト（黒文字）
        var textGo = new GameObject("BubbleText");
        textGo.transform.SetParent(speechBubbleGo.transform);
        textGo.transform.localPosition = new Vector3(0, 0.1f, 0);
        speechText = textGo.AddComponent<TextMesh>();
        speechText.font = GetFont();
        speechText.GetComponent<MeshRenderer>().material = GetFont().material;
        speechText.text = "";
        speechText.fontSize = 28;
        speechText.characterSize = 0.07f;
        speechText.anchor = TextAnchor.MiddleCenter;
        speechText.alignment = TextAlignment.Center;
        speechText.color = Color.black;
        var mr = textGo.GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 99;

        speechBubbleGo.SetActive(false);
    }

    void UpdateSpeechBubble()
    {
        if (speechBubbleGo == null || !speechBubbleGo.activeSelf) return;

        // 吹き出しは体に比例するが成長率を抑制（40%の成長率）
        float ps = Mathf.Max(transform.localScale.x, 0.1f);
        float dampInv = Mathf.Pow(ps, -0.6f); // world size = ps^0.4（緩やかに成長）
        speechBubbleGo.transform.localScale = new Vector3(dampInv, dampInv, 1f);

        speechTimer -= Time.deltaTime;

        // Fade out in last 0.5s
        if (speechTimer < 0.5f && speechTimer > 0f)
        {
            float alpha = speechTimer / 0.5f;
            if (speechBg != null)
            {
                speechBg.color = new Color(1f, 1f, 1f, alpha);
            }
            if (speechText != null)
            {
                speechText.color = new Color(0f, 0f, 0f, alpha);
            }
        }

        if (speechTimer <= 0f)
        {
            speechBubbleGo.SetActive(false);
            // Reset colors
            if (speechBg != null) speechBg.color = Color.white;
            if (speechText != null) speechText.color = Color.black;
        }
    }

    // ─── Utility ─────────────────────────────────────────────────

    static Sprite _pixelSprite;
    public static Sprite CreatePixelSprite()
    {
        if (_pixelSprite != null) return _pixelSprite;
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _pixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        return _pixelSprite;
    }

    static Sprite _swordBladeSprite;
    public static Sprite CreateSwordBladeSprite()
    {
        if (_swordBladeSprite != null) return _swordBladeSprite;
        int w = 10, h = 48;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var pixels = new Color[w * h];
        float cx = (w - 1) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            float halfWidth;
            if (y < 2)        halfWidth = 4.5f;   // 鍔（つば）
            else if (y < 5)    halfWidth = Mathf.Lerp(4.5f, 2.8f, (y - 2) / 3f);
            else if (y < 38)   halfWidth = Mathf.Lerp(2.8f, 2.2f, (y - 5) / 33f);
            else               halfWidth = Mathf.Lerp(2.2f, 0f, (y - 38) / 10f);

            for (int x = 0; x < w; x++)
            {
                float dx = Mathf.Abs(x - cx);
                if (dx > halfWidth) continue;
                float edgeT = dx / Mathf.Max(halfWidth, 0.1f);
                float bright = 1f - edgeT * 0.3f;
                float a = Mathf.Clamp01(halfWidth - dx + 0.5f);
                pixels[y * w + x] = new Color(1f, bright, bright * 0.9f, a);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply();
        _swordBladeSprite = Sprite.Create(tex, new Rect(0, 0, w, h),
            new Vector2(0.5f, 0.03f), 16);
        return _swordBladeSprite;
    }

    static Sprite _magicCircleSprite;
    public static Sprite CreateMagicCircleSprite()
    {
        if (_magicCircleSprite != null) return _magicCircleSprite;
        int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        // 全ピクセル透明で初期化
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Color.clear);

        int rays = 6; // 六芒星
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - c) / c;
                float dy = (y - c) / c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float a = 0f;

                // 外リング (r=0.85~0.95)
                float outerRing = 1f - Mathf.Abs(dist - 0.9f) / 0.06f;
                a = Mathf.Max(a, Mathf.Clamp01(outerRing));

                // 内リング (r=0.55~0.62)
                float innerRing = 1f - Mathf.Abs(dist - 0.58f) / 0.05f;
                a = Mathf.Max(a, Mathf.Clamp01(innerRing) * 0.8f);

                // 中心小リング (r=0.2~0.26)
                float coreRing = 1f - Mathf.Abs(dist - 0.23f) / 0.04f;
                a = Mathf.Max(a, Mathf.Clamp01(coreRing) * 0.6f);

                // 放射線 (6本)
                for (int r = 0; r < rays; r++)
                {
                    float rayAngle = r * Mathf.PI * 2f / rays;
                    float angleDiff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, rayAngle * Mathf.Rad2Deg));
                    float rayStrength = 1f - angleDiff / 3f; // 3度の幅
                    if (rayStrength > 0f && dist > 0.25f && dist < 0.88f)
                        a = Mathf.Max(a, rayStrength * 0.7f);
                }

                // 六芒星（三角形2つ、外リングに内接）
                for (int t = 0; t < 2; t++)
                {
                    float offset = t * Mathf.PI / rays;
                    for (int s = 0; s < rays / 2; s++)
                    {
                        float a1 = offset + s * Mathf.PI * 2f / (rays / 2);
                        float a2 = offset + ((s + 1) % (rays / 2)) * Mathf.PI * 2f / (rays / 2);
                        // 外リング上の2点を結ぶ線
                        float r1 = 0.85f;
                        Vector2 p1 = new Vector2(Mathf.Cos(a1) * r1, Mathf.Sin(a1) * r1);
                        Vector2 p2 = new Vector2(Mathf.Cos(a2) * r1, Mathf.Sin(a2) * r1);
                        Vector2 px = new Vector2(dx, dy);
                        // 点と線分の距離
                        Vector2 seg = p2 - p1;
                        float tParam = Mathf.Clamp01(Vector2.Dot(px - p1, seg) / seg.sqrMagnitude);
                        float lineDist = (px - (p1 + seg * tParam)).magnitude;
                        float lineStr = 1f - lineDist / 0.04f;
                        a = Mathf.Max(a, Mathf.Clamp01(lineStr) * 0.75f);
                    }
                }

                // 外リング上の頂点にドット
                for (int r = 0; r < rays; r++)
                {
                    float dotAngle = r * Mathf.PI * 2f / rays;
                    float dotX = Mathf.Cos(dotAngle) * 0.9f;
                    float dotY = Mathf.Sin(dotAngle) * 0.9f;
                    float dotDist = Mathf.Sqrt((dx - dotX) * (dx - dotX) + (dy - dotY) * (dy - dotY));
                    float dotStr = 1f - dotDist / 0.07f;
                    a = Mathf.Max(a, Mathf.Clamp01(dotStr) * 0.9f);
                }

                // 中心グロー（ふんわり）
                float glow = Mathf.Clamp01(1f - dist / 0.2f) * 0.4f;
                a = Mathf.Max(a, glow);

                if (a > 0.01f)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _magicCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        return _magicCircleSprite;
    }


    private float castleAttackTimer = 0f;
    private float enemyCastleAttackTimer = 0f;

    public bool HasReachedCastle()
    {
        return team == Team.Enemy && !isDead && transform.position.x <= GameConfig.CastleX + 1f;
    }

    public bool CanAttackCastle()
    {
        castleAttackTimer -= Time.deltaTime;
        return castleAttackTimer <= 0f;
    }

    public void ResetCastleAttackTimer()
    {
        castleAttackTimer = 1f / attackSpeed;
        if (anim != null) anim.SetTrigger(isBossMonster ? "Attack" : "Slash");
        attackAnimLock = 0.4f;
    }

    // ─── 敵城攻撃（味方→敵城） ──────────────────────────────────

    public bool HasReachedEnemyCastle()
    {
        return team == Team.Ally && !isDead && transform.position.x >= GameConfig.EnemyCastleX - 1f;
    }

    public bool CanAttackEnemyCastle()
    {
        enemyCastleAttackTimer -= Time.deltaTime;
        return enemyCastleAttackTimer <= 0f;
    }

    public void ResetEnemyCastleAttackTimer()
    {
        enemyCastleAttackTimer = 1f / attackSpeed;
        if (anim != null) anim.SetTrigger("Slash");
        attackAnimLock = 0.4f;
    }

    // ─── 必殺技 ──────────────────────────────────────────────

    public void PerformUltimate()
    {
        if (isDead) return;
        StartCoroutine(UltimateCoroutine());
    }

    System.Collections.IEnumerator UltimateCoroutine()
    {
        var gm = GameManager.Instance;
        if (gm == null) yield break;

        isUltimateActive = true; // 必殺技中はスコア加算無効

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SuperChat");

        string skillName;
        switch (unitType)
        {
            case UnitType.Warrior:
                skillName = "四連烈斬";
                DamagePopup.CreateText(transform.position + Vector3.up * 0.8f, skillName, new Color(1f, 0.6f, 0.1f));
                Unit wTarget = null;
                float wClosest = float.MaxValue;
                foreach (var e in gm.GetUnits(Team.Enemy))
                {
                    if (e == null || e.isDead) continue;
                    float d = Vector2.Distance(transform.position, e.transform.position);
                    if (d < wClosest) { wClosest = d; wTarget = e; }
                }
                if (wTarget == null || wTarget.isDead) break;
                // 溜め: 魔法陣 + 集中線
                StartCoroutine(UltimateChargeAura(new Color(1f, 0.3f, 0.1f, 0.6f), 0.7f, 2.5f));
                StartCoroutine(SpawnSpeedLines(new Color(1f, 0.5f, 0.1f), 0.7f));
                yield return new WaitForSeconds(0.7f);
                // 高速クロス4連撃
                yield return StartCoroutine(WarriorCrossSlash(wTarget));
                break;

            case UnitType.Lancer:
                skillName = "貫通突進";
                DamagePopup.CreateText(transform.position + Vector3.up * 0.8f, skillName, new Color(0.2f, 0.8f, 1f));
                if (anim != null) anim.SetTrigger("Jab");
                attackAnimLock = 1.0f;
                // 溜め: 魔法陣 + 集中線
                StartCoroutine(UltimateChargeAura(new Color(0.2f, 0.6f, 1f, 0.6f), 0.7f, 2.5f));
                StartCoroutine(SpawnSpeedLines(new Color(0.3f, 0.7f, 1f), 0.7f));
                yield return new WaitForSeconds(0.7f);
                // ソニックブーム + フラッシュ
                StartCoroutine(SpawnSonicBoom(transform.position, new Color(0.3f, 0.8f, 1f)));
                CameraShake.Shake(0.3f, 0.1f);
                // 前方に高速突進しながらダメージ + 残像エフェクト
                {
                    float chargeDistance = 5f;
                    float chargeSpeed = 18f;
                    float traveled = 0f;
                    float afterimageTimer = 0f;
                    var hitSet = new System.Collections.Generic.HashSet<Unit>();
                    while (traveled < chargeDistance && !isDead)
                    {
                        float step = chargeSpeed * Time.deltaTime;
                        transform.position += Vector3.right * step;
                        traveled += step;
                        afterimageTimer -= Time.deltaTime;
                        if (afterimageTimer <= 0f)
                        {
                            afterimageTimer = 0.03f;
                            StartCoroutine(SpawnAfterimage(transform.position, new Color(0.3f, 0.7f, 1f, 0.6f)));
                            SpawnUltimateTrail(transform.position, new Color(0.2f, 0.8f, 1f));
                        }
                        foreach (var e in gm.GetUnits(Team.Enemy))
                        {
                            if (e == null || e.isDead || hitSet.Contains(e)) continue;
                            if (Vector2.Distance(transform.position, e.transform.position) <= 0.8f)
                            {
                                int dmg = attackPower * 4;
                                e.TakeDamage(dmg, this);
                                OnDamageDealt?.Invoke(this, e, dmg);
                                hitSet.Add(e);
                                CameraShake.Shake(0.1f, 0.06f);
                                StartCoroutine(UltimateRadialBurst(e.transform.position, 8, new Color(0.4f, 0.8f, 1f), 1.5f));
                                StartCoroutine(SpawnGroundGlow(e.transform.position, new Color(0.2f, 0.6f, 1f)));
                            }
                        }
                        yield return null;
                    }
                    // 突進終了: 大爆発
                    CameraShake.Shake(0.3f, 0.12f);
                    StartCoroutine(UltimateRadialBurst(transform.position, 16, new Color(0.3f, 0.8f, 1f), 2.5f));
                    StartCoroutine(UltimateShockwave(transform.position, new Color(0.4f, 0.9f, 1f, 0.7f), 2.0f));
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("BossSmash");
                }
                break;

            case UnitType.Archer:
                skillName = "天槍の雨";
                DamagePopup.CreateText(transform.position + Vector3.up * 0.8f, skillName, new Color(0.4f, 1f, 0.4f));
                if (anim != null) anim.SetTrigger("Shot");
                attackAnimLock = 1.2f;
                // 溜め: 魔法陣 + 集中線
                StartCoroutine(UltimateChargeAura(new Color(0.3f, 1f, 0.3f, 0.5f), 0.7f, 2.5f));
                StartCoroutine(SpawnSpeedLines(new Color(0.4f, 1f, 0.4f), 0.7f));
                yield return new WaitForSeconds(0.7f);
                // 槍が降り注ぐ演出（5波 + ランダム散布 + 地面発光）
                {
                    var enemies = gm.GetUnits(Team.Enemy);
                    int spearWaves = 5;
                    for (int wave = 0; wave < spearWaves; wave++)
                    {
                        CameraShake.Shake(0.15f, 0.05f);
                        if (anim != null && wave % 2 == 0) anim.SetTrigger("Shot");
                        foreach (var e in enemies)
                        {
                            if (e == null || e.isDead) continue;
                            if (Vector2.Distance(transform.position, e.transform.position) <= attackRange * 1.5f)
                            {
                                // 本命槍 + ランダム散布槍
                                StartCoroutine(SpawnFallingSpear(e.transform.position));
                                StartCoroutine(SpawnFallingSpear(e.transform.position + new Vector3(Random.Range(-0.8f, 0.8f), 0, 0)));
                                if (wave == spearWaves - 1)
                                {
                                    int dmg = Mathf.RoundToInt(attackPower * 1.2f);
                                    e.TakeDamage(dmg, this);
                                    OnDamageDealt?.Invoke(this, e, dmg);
                                    // 最終波: 地面発光
                                    StartCoroutine(SpawnGroundGlow(e.transform.position, new Color(0.3f, 1f, 0.3f)));
                                }
                            }
                        }
                        yield return new WaitForSeconds(0.15f);
                    }
                    CameraShake.Shake(0.25f, 0.08f);
                }
                break;

            case UnitType.Monk:
                skillName = "聖なる祈り";
                DamagePopup.CreateText(transform.position + Vector3.up * 0.8f, skillName, new Color(1f, 1f, 0.5f));
                if (anim != null) anim.SetTrigger("Fire");
                attackAnimLock = 1.0f;
                // 溜め: 魔法陣 + 集中線
                StartCoroutine(UltimateChargeAura(new Color(1f, 1f, 0.4f, 0.5f), 0.7f, 3.0f));
                StartCoroutine(SpawnSpeedLines(new Color(1f, 1f, 0.6f), 0.7f));
                yield return new WaitForSeconds(0.7f);
                CameraShake.Shake(0.3f, 0.06f);
                // 聖なる十字パターン
                StartCoroutine(SpawnHolyCross(transform.position, new Color(1f, 1f, 0.5f)));
                // 味方全体を healAmount×3 回復 + 3秒被ダメ50%軽減 + 光柱エフェクト
                foreach (var a in gm.GetUnits(Team.Ally))
                {
                    if (a == null || a.isDead) continue;
                    int heal = Mathf.Min(healAmount * 3, a.maxHP - a.currentHP);
                    if (heal > 0)
                    {
                        a.currentHP += heal;
                        DamagePopup.Create(a.transform.position, heal, true);
                        OnHealPerformed?.Invoke(this, a, heal);
                    }
                    a.StartCoroutine(a.UltimateDefenseBuff(3f, 0.5f));
                    // 各味方に光の柱 + 放射 + 地面発光
                    StartCoroutine(SpawnHealPillar(a.transform.position, new Color(1f, 1f, 0.5f, 0.7f)));
                    StartCoroutine(UltimateRadialBurst(a.transform.position, 6, new Color(0.8f, 1f, 0.4f), 1.2f));
                    StartCoroutine(SpawnGroundGlow(a.transform.position, new Color(1f, 1f, 0.4f), 1.0f));
                }
                // 拡大リング波
                StartCoroutine(UltimateShockwave(transform.position, new Color(1f, 1f, 0.5f, 0.6f), 4f));
                break;

            case UnitType.Mage:
                skillName = "隕石落下";
                DamagePopup.CreateText(transform.position + Vector3.up * 0.8f, skillName, new Color(1f, 0.3f, 0.8f));
                if (anim != null) anim.SetTrigger("Fire");
                attackAnimLock = 1.5f;
                // 詠唱: 魔法陣 + 吸引パーティクル + 集中線
                StartCoroutine(UltimateChargeAura(new Color(0.8f, 0.2f, 1f, 0.6f), 0.7f, 3.5f));
                StartCoroutine(UltimateMageChant(transform.position));
                StartCoroutine(SpawnSpeedLines(new Color(0.8f, 0.3f, 1f), 0.7f, 16));
                yield return new WaitForSeconds(0.7f);
                // 画面暗転フラッシュ
                // 各敵位置に隕石を時間差で落下
                {
                    var enemies = gm.GetUnits(Team.Enemy);
                    int meteorCount = 0;
                    foreach (var e in enemies)
                    {
                        if (e == null || e.isDead) continue;
                        Vector3 meteorPos = e.transform.position;
                        StartCoroutine(SpawnMeteorImpact(meteorPos));
                        // 着弾後の残留炎
                        StartCoroutine(SpawnLingeringFireDelayed(meteorPos, 0.5f));
                        meteorCount++;
                        if (meteorCount % 3 == 0)
                            yield return new WaitForSeconds(0.08f);
                    }
                    yield return new WaitForSeconds(0.4f);
                    CameraShake.Shake(0.6f, 0.2f);
                    // ダメージ適用 + 地面発光
                    foreach (var e in enemies)
                    {
                        if (e == null || e.isDead) continue;
                        int dmg = attackPower * 5;
                        e.TakeDamage(dmg, this);
                        OnDamageDealt?.Invoke(this, e, dmg);
                        StartCoroutine(SpawnGroundGlow(e.transform.position, new Color(1f, 0.3f, 0.1f), 1.2f));
                    }
                    // 追加: 最後にもう一発画面フラッシュ
                }
                break;

            case UnitType.Knight:
                skillName = "聖剣一閃";
                DamagePopup.CreateText(transform.position + Vector3.up * 0.8f, skillName, new Color(1f, 0.85f, 0f));
                // 溜め: 魔法陣 + 集中線
                StartCoroutine(UltimateChargeAura(new Color(1f, 0.85f, 0f, 0.7f), 0.7f, 2.5f));
                StartCoroutine(SpawnSpeedLines(new Color(1f, 0.9f, 0.3f), 0.7f, 16));
                yield return new WaitForSeconds(0.7f);
                // 巨大光剣で薙ぎ払い
                yield return StartCoroutine(KnightHolySwordSweep());
                break;

            default:
                yield break;
        }

        isUltimateActive = false; // 必殺技終了
    }

    // ─── VFX 3D Billboard ──────────────────────────────────────

    /// <summary>VFXオブジェクト生成: 3Dフルスクリーン時はBillboard3Dを自動付与</summary>
    static GameObject MakeVFX(string name)
    {
        var go = new GameObject(name);
        if (CameraView3D.is3DFullScreen)
            go.AddComponent<Billboard3D>();
        return go;
    }

    /// <summary>VFX用「上方向」: 3Dフルスクリーン時はZ方向(=上)</summary>
    static Vector3 VFXUp()
    {
        return CameraView3D.is3DFullScreen ? Vector3.forward : Vector3.up;
    }

    // ─── 必殺技 VFX ヘルパー ────────────────────────────────────

    /// <summary>溜め中の集中線（スピードライン）</summary>
    System.Collections.IEnumerator SpawnSpeedLines(Color color, float duration, int count = 12)
    {
        float interval = duration / count;
        for (int i = 0; i < count; i++)
        {
            if (isDead) yield break;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(2.5f, 4f);
            Vector3 start = transform.position + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0);
            StartCoroutine(SpeedLineFly(start, transform.position, color));
            yield return new WaitForSeconds(interval);
        }
    }

    System.Collections.IEnumerator SpeedLineFly(Vector3 from, Vector3 to, Color color)
    {
        var line = new GameObject("SpeedLine");
        line.transform.position = from;
        var lSr = line.AddComponent<SpriteRenderer>();
        lSr.sprite = CreatePixelSprite();
        lSr.color = new Color(color.r, color.g, color.b, 0.7f);
        lSr.sortingOrder = 30;
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null) lSr.material = new Material(shader);

        Vector2 dir = (to - from).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        line.transform.rotation = Quaternion.Euler(0, 0, angle);
        line.transform.localScale = new Vector3(1.5f, 0.04f, 1f);

        float life = 0.15f;
        float elapsed = 0f;
        while (elapsed < life && line != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            line.transform.position = Vector3.Lerp(from, to, t * t);
            line.transform.localScale = new Vector3(1.5f * (1f - t), 0.04f * (1f - t * 0.5f), 1f);
            lSr.color = new Color(color.r, color.g, color.b, 0.7f * (1f - t));
            yield return null;
        }
        if (line != null) Destroy(line);
    }

    /// <summary>ヒットストップ（一瞬止まる）</summary>
    System.Collections.IEnumerator HitStop(float duration)
    {
        float saved = Time.timeScale;
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = saved;
    }

    /// <summary>ソニックブーム（突進開始時の衝撃波コーン）</summary>
    System.Collections.IEnumerator SpawnSonicBoom(Vector3 pos, Color color)
    {
        int ringCount = 3;
        for (int r = 0; r < ringCount; r++)
        {
            var ring = new GameObject("SonicRing");
            ring.transform.position = pos;
            var rSr = ring.AddComponent<SpriteRenderer>();
            rSr.sprite = CreateMagicCircleSprite();
            rSr.color = new Color(color.r, color.g, color.b, 0.6f);
            rSr.sortingOrder = 26;
            var shader = Shader.Find("GUI/Text Shader");
            if (shader != null) rSr.material = new Material(shader);
            ring.transform.localScale = Vector3.one * 0.1f;
            StartCoroutine(SonicRingExpand(ring, rSr, color));
            yield return new WaitForSeconds(0.06f);
        }
    }

    System.Collections.IEnumerator SonicRingExpand(GameObject ring, SpriteRenderer rSr, Color color)
    {
        float life = 0.35f;
        float elapsed = 0f;
        while (elapsed < life && ring != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            float s = Mathf.Lerp(0.1f, 3f, t);
            ring.transform.localScale = new Vector3(s, s * 0.5f, 1f); // 楕円で横に広がる
            rSr.color = new Color(color.r, color.g, color.b, 0.6f * (1f - t));
            yield return null;
        }
        if (ring != null) Destroy(ring);
    }

    /// <summary>地面発光エフェクト</summary>
    System.Collections.IEnumerator SpawnGroundGlow(Vector3 pos, Color color, float radius = 0.8f)
    {
        var glow = MakeVFX("GroundGlow");
        glow.transform.position = pos;
        var gSr = glow.AddComponent<SpriteRenderer>();
        gSr.sprite = CreateMagicCircleSprite();
        gSr.color = new Color(color.r, color.g, color.b, 0.5f);
        gSr.sortingOrder = 4;
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null) gSr.material = new Material(shader);

        float life = 0.5f;
        float elapsed = 0f;
        while (elapsed < life && glow != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            float s = Mathf.Lerp(0.1f, radius, Mathf.Min(t * 3f, 1f));
            glow.transform.localScale = new Vector3(s, s * 0.4f, 1f);
            glow.transform.Rotate(0, 0, 60f * Time.deltaTime);
            gSr.color = new Color(color.r, color.g, color.b, 0.5f * (1f - t));
            yield return null;
        }
        if (glow != null) Destroy(glow);
    }

    /// <summary>光柱エフェクト（上に伸びる太い光）</summary>
    System.Collections.IEnumerator SpawnLightPillar(Vector3 pos, Color color, float height = 6f)
    {
        var pillar = MakeVFX("LightPillar");
        pillar.transform.position = pos;
        var pSr = pillar.AddComponent<SpriteRenderer>();
        pSr.sprite = CreatePixelSprite();
        pSr.color = new Color(color.r, color.g, color.b, 0.8f);
        pSr.sortingOrder = 27;
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null) pSr.material = new Material(shader);

        float life = 0.8f;
        float elapsed = 0f;
        while (elapsed < life && pillar != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            float h = Mathf.Lerp(0f, height, Mathf.Min(t * 4f, 1f));
            float w = 0.6f * (1f - t * 0.3f);
            pillar.transform.localScale = new Vector3(w, h, 1f);
            pillar.transform.position = pos + VFXUp() * h * 0.5f;
            float a = t < 0.6f ? 0.8f : 0.8f * (1f - (t - 0.6f) / 0.4f);
            pSr.color = new Color(color.r, color.g, color.b, Mathf.Max(0, a));
            yield return null;
        }
        if (pillar != null) Destroy(pillar);
    }

    /// <summary>聖なる十字パターン</summary>
    System.Collections.IEnumerator SpawnHolyCross(Vector3 pos, Color color)
    {
        // 縦と横の光線
        for (int axis = 0; axis < 2; axis++)
        {
            var line = MakeVFX("HolyCross");
            line.transform.position = pos;
            var lSr = line.AddComponent<SpriteRenderer>();
            lSr.sprite = CreatePixelSprite();
            lSr.color = new Color(color.r, color.g, color.b, 0.6f);
            lSr.sortingOrder = 26;
            var shader = Shader.Find("GUI/Text Shader");
            if (shader != null) lSr.material = new Material(shader);
            if (axis == 0)
                line.transform.localScale = new Vector3(0.12f, 0.1f, 1f);
            else
                line.transform.localScale = new Vector3(0.1f, 0.12f, 1f);
            StartCoroutine(HolyCrossExpand(line, lSr, axis == 0));
        }
        yield break;
    }

    System.Collections.IEnumerator HolyCrossExpand(GameObject line, SpriteRenderer lSr, bool isVertical)
    {
        float life = 0.7f;
        float elapsed = 0f;
        Color c = lSr.color;
        while (elapsed < life && line != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            float extend = Mathf.Lerp(0.1f, 8f, Mathf.Min(t * 3f, 1f));
            if (isVertical)
                line.transform.localScale = new Vector3(0.12f * (1f - t * 0.3f), extend, 1f);
            else
                line.transform.localScale = new Vector3(extend, 0.12f * (1f - t * 0.3f), 1f);
            lSr.color = new Color(c.r, c.g, c.b, 0.6f * (1f - t));
            yield return null;
        }
        if (line != null) Destroy(line);
    }

    System.Collections.IEnumerator SpawnLingeringFireDelayed(Vector3 pos, float delay)
    {
        yield return new WaitForSeconds(delay);
        StartCoroutine(SpawnLingeringFire(pos));
    }

    /// <summary>残留炎エフェクト</summary>
    System.Collections.IEnumerator SpawnLingeringFire(Vector3 pos, float duration = 1.5f)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            var flame = new GameObject("LingerFlame");
            flame.transform.position = pos + new Vector3(
                Random.Range(-0.5f, 0.5f), Random.Range(-0.1f, 0.4f), 0);
            var fSr = flame.AddComponent<SpriteRenderer>();
            fSr.sprite = CreatePixelSprite();
            fSr.color = new Color(1f, Random.Range(0.2f, 0.6f), 0f, 0.7f * (1f - t));
            fSr.sortingOrder = 15;
            float s = Random.Range(0.06f, 0.14f) * (1f - t * 0.5f);
            flame.transform.localScale = new Vector3(s, s, 1f);
            StartCoroutine(FadeAndDestroy(flame, fSr, 0.4f, true));
            yield return new WaitForSeconds(0.05f);
        }
    }

    /// <summary>溜め演出: 拡大するオーラ円</summary>
    System.Collections.IEnumerator UltimateChargeAura(Color color, float duration, float maxRadius)
    {
        var auraGo = MakeVFX("UltChargeAura");
        auraGo.transform.position = transform.position;
        var auraSr = auraGo.AddComponent<SpriteRenderer>();
        auraSr.sprite = CreateMagicCircleSprite();
        auraSr.color = color;
        auraSr.sortingOrder = sr != null ? sr.sortingOrder - 1 : 9;
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null) auraSr.material = new Material(shader);

        float elapsed = 0f;
        float rotSpeed = 120f; // 度/秒
        while (elapsed < duration && auraGo != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float r = Mathf.Lerp(0.1f, maxRadius, t);
            auraGo.transform.localScale = new Vector3(r, r, 1f);
            auraGo.transform.position = transform.position;
            auraGo.transform.Rotate(0, 0, rotSpeed * Time.deltaTime);
            float a = color.a * (1f - t * 0.3f);
            auraSr.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        if (auraGo != null) Destroy(auraGo);
    }

    /// <summary>放射状パーティクル爆発</summary>
    System.Collections.IEnumerator UltimateRadialBurst(Vector3 center, int count, Color color, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            var p = new GameObject("UltBurst");
            p.transform.position = center;
            var pSr = p.AddComponent<SpriteRenderer>();
            pSr.sprite = CreatePixelSprite();
            float brightness = Random.Range(0.7f, 1f);
            pSr.color = new Color(
                Mathf.Min(1f, color.r * brightness + 0.2f),
                Mathf.Min(1f, color.g * brightness + 0.1f),
                Mathf.Min(1f, color.b * brightness),
                0.95f);
            pSr.sortingOrder = 25;
            float size = Random.Range(0.1f, 0.2f);
            p.transform.localScale = new Vector3(size, size, 1f);

            float angle = (360f / count) * i * Mathf.Deg2Rad + Random.Range(-0.2f, 0.2f);
            Vector3 dir = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
            float speed = Random.Range(3f, 6f);
            StartCoroutine(UltBurstParticleFly(p, pSr, dir, speed, radius));
        }
        yield break;
    }

    System.Collections.IEnumerator UltBurstParticleFly(GameObject p, SpriteRenderer pSr, Vector3 dir, float speed, float maxDist)
    {
        float life = maxDist / speed;
        float elapsed = 0f;
        Vector3 startPos = p.transform.position;
        while (elapsed < life && p != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            p.transform.position = startPos + dir * speed * elapsed;
            float alpha = 1f - t;
            float s = p.transform.localScale.x * (1f - Time.deltaTime * 4f);
            p.transform.localScale = new Vector3(Mathf.Max(0.02f, s), Mathf.Max(0.02f, s), 1f);
            pSr.color = new Color(pSr.color.r, pSr.color.g, pSr.color.b, alpha);
            yield return null;
        }
        if (p != null) Destroy(p);
    }

    /// <summary>衝撃波リング（拡大する円）</summary>
    System.Collections.IEnumerator UltimateShockwave(Vector3 center, Color color, float maxRadius)
    {
        // 複数のリングピクセルで円を描く
        int ringCount = 24;
        var ringParts = new GameObject[ringCount];
        var ringSrs = new SpriteRenderer[ringCount];
        for (int i = 0; i < ringCount; i++)
        {
            var rp = new GameObject("ShockRing");
            rp.transform.position = center;
            var rpSr = rp.AddComponent<SpriteRenderer>();
            rpSr.sprite = CreatePixelSprite();
            rpSr.color = color;
            rpSr.sortingOrder = 24;
            rp.transform.localScale = new Vector3(0.12f, 0.12f, 1f);
            ringParts[i] = rp;
            ringSrs[i] = rpSr;
        }

        float duration = 0.4f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float r = Mathf.Lerp(0.2f, maxRadius, t);
            float alpha = color.a * (1f - t);
            for (int i = 0; i < ringCount; i++)
            {
                if (ringParts[i] == null) continue;
                float angle = (360f / ringCount) * i * Mathf.Deg2Rad;
                ringParts[i].transform.position = center + new Vector3(
                    Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0);
                ringSrs[i].color = new Color(color.r, color.g, color.b, alpha);
            }
            yield return null;
        }
        for (int i = 0; i < ringCount; i++)
            if (ringParts[i] != null) Destroy(ringParts[i]);
    }

    /// <summary>Lancer残像エフェクト</summary>
    // ─── Warrior Ultimate: Cross 4-Slash ─────────────────────
    // 右上→左下に突進、瞬間移動して左上→右下に突進 ×2 (角度違い)
    System.Collections.IEnumerator WarriorCrossSlash(Unit target)
    {
        if (target == null || target.isDead) yield break;

        // ターゲット位置を記録（死んでも全4撃出し切る）
        Vector3 lastTargetPos = target.transform.position;

        Vector2[] dirs = {
            new Vector2(-1f, -1f).normalized,   // ↙ 45°
            new Vector2(1f, -1f).normalized,    // ↘ 45°
            new Vector2(-1f, -0.4f).normalized, // ↙ 浅め（水平寄り）
            new Vector2(1f, -0.4f).normalized   // ↘ 浅め（水平寄り）
        };
        float slashDist = 2.2f;
        float slashSpeed = 32f;
        Color slashColor = new Color(1f, 0.5f, 0.1f);
        Vector3 savedPos = transform.position;

        for (int i = 0; i < 4; i++)
        {
            if (isDead) break;
            bool isFinal = (i == 3);

            // 生きていれば最新位置を追跡
            if (target != null && !target.isDead)
                lastTargetPos = target.transform.position;

            Vector3 tPos = lastTargetPos;
            Vector2 dir = dirs[i];

            // 瞬間移動: ターゲットの斬撃方向の逆側にワープ
            transform.position = tPos - (Vector3)(dir * slashDist);
            // 向き: 常にターゲット側を向く
            if (sr != null) sr.flipX = (tPos.x < transform.position.x);

            if (anim != null) anim.SetTrigger("Slash");
            attackAnimLock = 0.15f;

            // 突進（ターゲットを貫通して反対側まで）
            float totalDist = slashDist * 2f;
            float traveled = 0f;
            float trailTimer = 0f;
            bool hitDone = false;

            while (traveled < totalDist && !isDead)
            {
                float step = slashSpeed * Time.deltaTime;
                transform.position += (Vector3)(dir * step);
                traveled += step;

                // 残像
                trailTimer -= Time.deltaTime;
                if (trailTimer <= 0f)
                {
                    trailTimer = 0.02f;
                    StartCoroutine(SpawnAfterimage(transform.position, new Color(1f, 0.4f, 0.1f, 0.5f)));
                }

                // ヒット判定（通過時、ターゲットが生きている場合のみ）
                if (!hitDone && target != null && !target.isDead &&
                    Vector2.Distance(transform.position, target.transform.position) < 0.8f)
                {
                    hitDone = true;
                    int dmg = isFinal ? attackPower * 3 : attackPower * 2;
                    target.TakeDamage(dmg, this);
                    OnDamageDealt?.Invoke(this, target, dmg);
                }

                // 斬撃ライン（ダメージ有無に関わらず中間で必ず出す）
                if (!hitDone && Vector2.Distance(transform.position, tPos) < 0.8f)
                {
                    hitDone = true;
                }

                yield return null;
            }

            // 斬撃通過後にVFX
            StartCoroutine(SlashLineVFX(tPos, dir, slashColor, isFinal ? 2.5f : 1.8f));
            StartCoroutine(SpawnGroundGlow(tPos, slashColor, 0.6f));
            if (isFinal)
            {
                // ヒットストップ + 大爆発
                StartCoroutine(HitStop(0.08f));
                CameraShake.Shake(0.5f, 0.2f);
                StartCoroutine(UltimateRadialBurst(tPos, 16, new Color(1f, 0.6f, 0.1f), 2.5f));
                StartCoroutine(UltimateShockwave(tPos, new Color(1f, 0.5f, 0.1f, 0.8f), 3f));
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("BossSmash");
            }
            else
            {
                CameraShake.Shake(0.12f, 0.06f);
                StartCoroutine(UltimateRadialBurst(tPos, 6, new Color(1f, 0.5f, 0.2f), 1.2f));
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SwordHit");
            }

            // X字の2撃目は間を詰める、X間は少し長めに待つ
            if (!isFinal)
            {
                bool isSecondOfPair = (i % 2 == 0);
                yield return new WaitForSeconds(isSecondOfPair ? 0.03f : 0.12f);
            }
        }

        // 元の位置に戻る
        yield return new WaitForSeconds(0.1f);
        if (!isDead) transform.position = savedPos;
    }

    // ─── Knight Ultimate: Holy Sword Sweep ─────────────────────
    System.Collections.IEnumerator KnightHolySwordSweep()
    {
        var gm = GameManager.Instance;
        if (gm == null) yield break;

        if (anim != null) anim.SetTrigger("Slash");
        attackAnimLock = 1.5f;
        var bladeShader = Shader.Find("GUI/Text Shader");

        // === 光剣の生成 ===
        var swordRoot = MakeVFX("HolySword");
        Vector3 rootOffset = new Vector3(0.2f, 0.5f, 0);
        swordRoot.transform.position = transform.position + rootOffset;

        // 刀身（剣型スプライト）
        var blade = new GameObject("Blade");
        blade.transform.SetParent(swordRoot.transform);
        blade.transform.localPosition = Vector3.zero;
        var bladeSr = blade.AddComponent<SpriteRenderer>();
        bladeSr.sprite = CreateSwordBladeSprite();
        bladeSr.color = new Color(1f, 0.95f, 0.8f, 0.95f);
        bladeSr.sortingOrder = 40;
        blade.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
        if (bladeShader != null) bladeSr.material = new Material(bladeShader);

        // 刀身のグロー（外側の光）
        var glow = new GameObject("BladeGlow");
        glow.transform.SetParent(swordRoot.transform);
        glow.transform.localPosition = Vector3.zero;
        var glowSr = glow.AddComponent<SpriteRenderer>();
        glowSr.sprite = CreateSwordBladeSprite();
        glowSr.color = new Color(1f, 0.85f, 0.2f, 0.4f);
        glowSr.sortingOrder = 39;
        glow.transform.localScale = new Vector3(2.8f, 2.2f, 1f);
        if (bladeShader != null) glowSr.material = new Material(bladeShader);

        // 柄の魔法陣
        var hilt = new GameObject("Hilt");
        hilt.transform.SetParent(swordRoot.transform);
        hilt.transform.localPosition = Vector3.zero;
        var hiltSr = hilt.AddComponent<SpriteRenderer>();
        hiltSr.sprite = CreateMagicCircleSprite();
        hiltSr.color = new Color(1f, 0.9f, 0.3f, 0.8f);
        hiltSr.sortingOrder = 41;
        hilt.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
        if (bladeShader != null) hiltSr.material = new Material(bladeShader);

        // === 構え ===
        float startAngle = 60f;    // 上に掲げる（左上方向）
        float endAngle = -120f;    // 右下まで振り下ろす（前方の敵を薙ぐ）
        swordRoot.transform.rotation = Quaternion.Euler(0, 0, startAngle);
        swordRoot.transform.localScale = Vector3.one * 0.3f;

        // 出現（拡大）
        float appearTime = 0.15f;
        float elapsed = 0f;
        while (elapsed < appearTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / appearTime;
            swordRoot.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t);
            hilt.transform.Rotate(0, 0, 200f * Time.deltaTime);
            yield return null;
        }

        yield return new WaitForSeconds(0.08f);

        // === 薙ぎ払い ===
        if (anim != null) anim.SetTrigger("Slash");
        CameraShake.Shake(0.5f, 0.18f);
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("BossSmash");

        float sweepDuration = 0.25f;
        elapsed = 0f;
        var hitEnemies = new System.Collections.Generic.HashSet<Unit>();
        float trailTimer = 0f;
        int afterimageIdx = 0;
        float[] afterimageAt = { 0.12f, 0.32f, 0.52f };

        while (elapsed < sweepDuration && swordRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / sweepDuration;
            float eased = 1f - (1f - t) * (1f - t);
            float angle = Mathf.Lerp(startAngle, endAngle, eased);
            swordRoot.transform.rotation = Quaternion.Euler(0, 0, angle);
            swordRoot.transform.position = transform.position + rootOffset;

            // 残像生成（3つ）
            if (afterimageIdx < afterimageAt.Length && t >= afterimageAt[afterimageIdx])
            {
                afterimageIdx++;
                StartCoroutine(SpawnSwordAfterimage(
                    swordRoot.transform.position, angle, 0.5f));
            }

            // 剣先の軌跡パーティクル
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.02f;
                float rad = angle * Mathf.Deg2Rad;
                Vector3 tipPos = swordRoot.transform.position +
                    new Vector3(Mathf.Cos(rad + Mathf.PI * 0.5f),
                                Mathf.Sin(rad + Mathf.PI * 0.5f), 0) * 5.5f;
                StartCoroutine(SpawnSwordTrail(tipPos, new Color(1f, 0.9f, 0.3f)));
            }

            // ダメージ判定
            foreach (var e in gm.GetUnits(Team.Enemy))
            {
                if (e == null || e.isDead || hitEnemies.Contains(e)) continue;
                float rad2 = angle * Mathf.Deg2Rad;
                Vector2 swordDir = new Vector2(
                    Mathf.Cos(rad2 + Mathf.PI * 0.5f),
                    Mathf.Sin(rad2 + Mathf.PI * 0.5f));
                Vector2 toEnemy = (Vector2)(e.transform.position - swordRoot.transform.position);
                float proj = Vector2.Dot(toEnemy, swordDir);
                if (proj < 0.5f || proj > 7f) continue;
                Vector2 perp = toEnemy - swordDir * proj;
                if (perp.magnitude > 1.5f) continue;

                hitEnemies.Add(e);
                int dmg = attackPower * 4;
                e.TakeDamage(dmg, this);
                OnDamageDealt?.Invoke(this, e, dmg);
                CameraShake.Shake(0.08f, 0.05f);
                StartCoroutine(UltimateRadialBurst(e.transform.position, 8,
                    new Color(1f, 0.85f, 0.2f), 1.5f));
                StartCoroutine(SpawnGroundGlow(e.transform.position,
                    new Color(1f, 0.85f, 0f)));
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("SwordHit");
            }

            yield return null;
        }

        // === 衝撃波 ===
        StartCoroutine(HitStop(0.06f));
        StartCoroutine(UltimateShockwave(transform.position,
            new Color(1f, 0.9f, 0.3f, 0.7f), 4f));
        Vector2 sweepDir = new Vector2(1f, -0.5f).normalized;
        StartCoroutine(SlashLineVFX(transform.position + Vector3.right * 3f,
            sweepDir, new Color(1f, 0.85f, 0f), 8f));

        // === フェードアウト ===
        float fadeTime = 0.3f;
        elapsed = 0f;
        while (elapsed < fadeTime && swordRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeTime;
            float alpha = 1f - t;
            if (bladeSr != null) bladeSr.color = new Color(1f, 0.95f, 0.8f, 0.95f * alpha);
            if (glowSr != null) glowSr.color = new Color(1f, 0.85f, 0.2f, 0.4f * alpha);
            if (hiltSr != null) hiltSr.color = new Color(1f, 0.9f, 0.3f, 0.8f * alpha);
            hilt.transform.Rotate(0, 0, 100f * Time.deltaTime);
            yield return null;
        }

        if (swordRoot != null) Destroy(swordRoot);
    }

    /// <summary>光剣の残像（指定角度で留まってフェードアウト）</summary>
    System.Collections.IEnumerator SpawnSwordAfterimage(Vector3 pos, float angle, float startAlpha)
    {
        var afterRoot = MakeVFX("SwordAfterimage");
        afterRoot.transform.position = pos;
        afterRoot.transform.rotation = Quaternion.Euler(0, 0, angle);

        var bladeShader = Shader.Find("GUI/Text Shader");

        // 残像の刀身
        var ab = new GameObject("AfterBlade");
        ab.transform.SetParent(afterRoot.transform);
        ab.transform.localPosition = Vector3.zero;
        var abSr = ab.AddComponent<SpriteRenderer>();
        abSr.sprite = CreateSwordBladeSprite();
        abSr.color = new Color(1f, 0.9f, 0.4f, startAlpha);
        abSr.sortingOrder = 37;
        ab.transform.localScale = new Vector3(2.0f, 2.0f, 1f);
        if (bladeShader != null) abSr.material = new Material(bladeShader);

        // 残像のグロー
        var ag = new GameObject("AfterGlow");
        ag.transform.SetParent(afterRoot.transform);
        ag.transform.localPosition = Vector3.zero;
        var agSr = ag.AddComponent<SpriteRenderer>();
        agSr.sprite = CreateSwordBladeSprite();
        agSr.color = new Color(1f, 0.8f, 0.1f, startAlpha * 0.4f);
        agSr.sortingOrder = 36;
        ag.transform.localScale = new Vector3(2.8f, 2.2f, 1f);
        if (bladeShader != null) agSr.material = new Material(bladeShader);

        float life = 0.3f;
        float elapsed = 0f;
        while (elapsed < life && afterRoot != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            float a = startAlpha * (1f - t);
            abSr.color = new Color(1f, 0.9f, 0.4f, a);
            agSr.color = new Color(1f, 0.8f, 0.1f, a * 0.4f);
            yield return null;
        }
        if (afterRoot != null) Destroy(afterRoot);
    }

    System.Collections.IEnumerator SpawnSwordTrail(Vector3 pos, Color color)
    {
        var trail = new GameObject("SwordTrail");
        trail.transform.position = pos + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
        var tSr = trail.AddComponent<SpriteRenderer>();
        tSr.sprite = CreatePixelSprite();
        tSr.color = new Color(color.r, color.g, color.b, 0.8f);
        tSr.sortingOrder = 38;
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null) tSr.material = new Material(shader);
        float size = Random.Range(0.1f, 0.25f);
        trail.transform.localScale = new Vector3(size, size, 1f);

        float life = 0.25f;
        float elapsed = 0f;
        while (elapsed < life && trail != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            tSr.color = new Color(color.r, color.g, color.b, 0.8f * (1f - t));
            float s = size * (1f + t * 0.5f);
            trail.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (trail != null) Destroy(trail);
    }

    /// <summary>斬撃ラインVFX（白→色にフェード）</summary>
    System.Collections.IEnumerator SlashLineVFX(Vector3 center, Vector2 dir, Color color, float length)
    {
        var lineGo = MakeVFX("SlashLine");
        lineGo.transform.position = center;
        var lineSr = lineGo.AddComponent<SpriteRenderer>();
        lineSr.sprite = CreatePixelSprite();
        lineSr.color = new Color(1f, 1f, 1f, 0.95f);
        lineSr.sortingOrder = 50;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        lineGo.transform.rotation = Quaternion.Euler(0, 0, angle);
        lineGo.transform.localScale = new Vector3(length, 0.1f, 1f);

        float life = 0.3f;
        float elapsed = 0f;
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            lineSr.color = Color.Lerp(
                new Color(1f, 1f, 1f, 0.95f),
                new Color(color.r, color.g, color.b, 0f), t);
            lineGo.transform.localScale = new Vector3(
                length * (1f + t * 0.3f),
                0.1f * (1f - t * 0.7f), 1f);
            yield return null;
        }
        Destroy(lineGo);
    }

    System.Collections.IEnumerator SpawnAfterimage(Vector3 pos, Color tint)
    {
        if (sr == null) yield break;
        var afterGo = MakeVFX("Afterimage");
        afterGo.transform.position = pos;
        afterGo.transform.localScale = transform.localScale;
        var afterSr = afterGo.AddComponent<SpriteRenderer>();
        afterSr.sprite = sr.sprite;
        afterSr.flipX = sr.flipX;
        afterSr.color = tint;
        afterSr.sortingOrder = sr.sortingOrder - 1;
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null) afterSr.material = new Material(shader);

        float life = 0.3f;
        float elapsed = 0f;
        while (elapsed < life && afterGo != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            afterSr.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - t));
            float s = 1f + t * 0.3f;
            afterGo.transform.localScale = transform.localScale * s;
            yield return null;
        }
        if (afterGo != null) Destroy(afterGo);
    }

    /// <summary>Lancer突進軌跡パーティクル</summary>
    void SpawnUltimateTrail(Vector3 pos, Color color)
    {
        var trail = new GameObject("UltTrail");
        trail.transform.position = pos + new Vector3(
            Random.Range(-0.1f, 0.1f), Random.Range(-0.3f, 0.3f), 0);
        var trSr = trail.AddComponent<SpriteRenderer>();
        trSr.sprite = CreatePixelSprite();
        trSr.color = new Color(color.r, color.g, color.b, 0.8f);
        trSr.sortingOrder = 19;
        float size = Random.Range(0.08f, 0.18f);
        trail.transform.localScale = new Vector3(size, size, 1f);
        StartCoroutine(FadeAndDestroy(trail, trSr, 0.25f, true));
    }

    /// <summary>Archer必殺技: Pixel Heroesの槍が空から降って地面に突き刺さる</summary>
    static Sprite _cachedSpearSprite;
    static string _cachedSpearWeaponName;

    /// <summary>武器スプライト変更時にキャッシュクリア</summary>
    public static void ClearSpearSpriteCache()
    {
        _cachedSpearSprite = null;
        _cachedSpearWeaponName = null;
    }

    System.Collections.IEnumerator SpawnFallingSpear(Vector3 targetPos)
    {
        Vector3 startPos = targetPos + new Vector3(Random.Range(-0.3f, 0.3f), 5f + Random.Range(0f, 1.5f), 0);
        Vector3 endPos = targetPos + new Vector3(Random.Range(-0.15f, 0.15f), 0, 0);

        var spear = MakeVFX("FallingSpear");
        spear.transform.position = startPos;
        Destroy(spear, 2.5f); // 安全装置: 何があっても2.5秒で消える
        var spSr = spear.AddComponent<SpriteRenderer>();
        spSr.sortingOrder = 22;

        // PlayerPrefsから選択武器を読み込み
        string weaponName = PlayerPrefs.GetString("SpearWeapon", "Longsword");
        if (_cachedSpearSprite == null || _cachedSpearWeaponName != weaponName)
        {
            _cachedSpearWeaponName = weaponName;
            _cachedSpearSprite = PixelHeroFactory.ExtractWeaponSprite(weaponName);
        }

        if (_cachedSpearSprite != null)
        {
            spSr.sprite = _cachedSpearSprite;
            spear.transform.rotation = Quaternion.Euler(0, 0, -90f + Random.Range(-12f, 12f));
            spear.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
        }
        else
        {
            spSr.sprite = CreatePixelSprite();
            spSr.color = new Color(0.8f, 0.7f, 0.5f, 0.9f);
            spear.transform.localScale = new Vector3(0.04f, 0.15f, 1f);
        }

        // 落下アニメーション（加速イージング）
        float fallTime = 0.2f;
        float elapsed = 0f;
        while (elapsed < fallTime && spear != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallTime;
            float eased = t * t;
            spear.transform.position = Vector3.Lerp(startPos, endPos, eased);
            yield return null;
        }

        if (spear == null) yield break;

        // 着弾: 地面に突き刺さる
        spear.transform.position = endPos;
        CameraShake.Shake(0.05f, 0.02f);
        StartCoroutine(UltimateRadialBurst(endPos, 3, new Color(0.8f, 0.8f, 0.5f), 0.5f));
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("Hit");

        // 1秒間地面に刺さったまま
        yield return new WaitForSeconds(1.0f);

        if (spear == null) yield break;

        // 光の粒子になって消える演出
        int particleCount = 8;
        for (int i = 0; i < particleCount; i++)
        {
            var particle = new GameObject("SpearParticle");
            particle.transform.position = spear.transform.position + new Vector3(
                Random.Range(-0.2f, 0.2f), Random.Range(-0.3f, 0.5f), 0);
            var pSr = particle.AddComponent<SpriteRenderer>();
            pSr.sprite = CreatePixelSprite();
            pSr.color = new Color(1f, 1f, 0.7f, 0.9f);
            pSr.sortingOrder = 23;
            var shader = Shader.Find("GUI/Text Shader");
            if (shader != null) pSr.material = new Material(shader);
            float s = Random.Range(0.06f, 0.12f);
            particle.transform.localScale = new Vector3(s, s, 1f);
            StartCoroutine(SpearDissolveParticle(particle, pSr));
        }

        // 槍本体をフェードアウト
        float fadeDur = 0.3f;
        float fadeEl = 0f;
        Color origColor = spSr.color;
        while (fadeEl < fadeDur && spear != null)
        {
            fadeEl += Time.deltaTime;
            float t = fadeEl / fadeDur;
            spSr.color = new Color(origColor.r, origColor.g, origColor.b, 1f - t);
            yield return null;
        }
        if (spear != null) Destroy(spear);
    }

    /// <summary>槍消滅時の光パーティクル（上方に浮遊しながら縮小して消える）</summary>
    System.Collections.IEnumerator SpearDissolveParticle(GameObject p, SpriteRenderer pSr)
    {
        float life = 0.5f + Random.Range(0f, 0.3f);
        float elapsed = 0f;
        Vector3 startPos = p.transform.position;
        float vx = Random.Range(-0.4f, 0.4f);
        float vy = Random.Range(0.8f, 2.0f);
        Color startColor = pSr.color;
        float startScale = p.transform.localScale.x;

        while (elapsed < life && p != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            p.transform.position = startPos + new Vector3(vx * t, vy * t, 0);
            pSr.color = new Color(startColor.r, startColor.g, startColor.b, startColor.a * (1f - t));
            float s = startScale * (1f - t);
            p.transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (p != null) Destroy(p);
    }

    /// <summary>Monk光の柱エフェクト</summary>
    System.Collections.IEnumerator SpawnHealPillar(Vector3 basePos, Color color)
    {
        // 細い光の柱を下から上に伸ばす
        var pillar = MakeVFX("HealPillar");
        pillar.transform.position = basePos;
        var pilSr = pillar.AddComponent<SpriteRenderer>();
        pilSr.sprite = CreatePixelSprite();
        pilSr.color = color;
        pilSr.sortingOrder = 23;
        var shader = Shader.Find("GUI/Text Shader");
        if (shader != null) pilSr.material = new Material(shader);

        float duration = 0.6f;
        float elapsed = 0f;
        while (elapsed < duration && pillar != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // 柱: 幅狭く、高さが伸びてからフェード
            float h = Mathf.Lerp(0.1f, 4f, Mathf.Min(t * 3f, 1f));
            float w = 0.3f * (1f - t * 0.5f);
            pillar.transform.localScale = new Vector3(w, h, 1f);
            pillar.transform.position = basePos + VFXUp() * h * 0.5f;
            float alpha = t < 0.5f ? color.a : color.a * (1f - (t - 0.5f) * 2f);
            pilSr.color = new Color(color.r, color.g, color.b, Mathf.Max(0, alpha));
            // 光の粒子
            if (Random.value < 0.3f)
            {
                var sparkGo = new GameObject("HealSpark");
                sparkGo.transform.position = basePos + new Vector3(
                    Random.Range(-0.3f, 0.3f), Random.Range(0, h), 0);
                var spSr = sparkGo.AddComponent<SpriteRenderer>();
                spSr.sprite = CreatePixelSprite();
                spSr.color = new Color(1f, 1f, 0.8f, 0.8f);
                spSr.sortingOrder = 24;
                float s = Random.Range(0.05f, 0.1f);
                sparkGo.transform.localScale = new Vector3(s, s, 1f);
                StartCoroutine(FadeAndDestroy(sparkGo, spSr, 0.3f, true));
            }
            yield return null;
        }
        if (pillar != null) Destroy(pillar);
    }

    /// <summary>Mage詠唱パーティクル（吸引される粒子）</summary>
    System.Collections.IEnumerator UltimateMageChant(Vector3 center)
    {
        float duration = 0.55f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 周囲からパーティクルが中心に吸い寄せられる
            for (int i = 0; i < 2; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(2f, 3.5f);
                Vector3 startPos = center + new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist, 0);
                var p = new GameObject("ChantParticle");
                p.transform.position = startPos;
                var pSr = p.AddComponent<SpriteRenderer>();
                pSr.sprite = CreatePixelSprite();
                pSr.color = new Color(0.8f, 0.3f, 1f, 0.8f);
                pSr.sortingOrder = 22;
                float sz = Random.Range(0.08f, 0.15f);
                p.transform.localScale = new Vector3(sz, sz, 1f);
                StartCoroutine(ChantParticleFly(p, pSr, center));
            }
            yield return new WaitForSeconds(0.03f);
        }
    }

    System.Collections.IEnumerator ChantParticleFly(GameObject p, SpriteRenderer pSr, Vector3 target)
    {
        float life = 0.35f;
        float elapsed = 0f;
        Vector3 startPos = p.transform.position;
        while (elapsed < life && p != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / life;
            p.transform.position = Vector3.Lerp(startPos, target, t * t);
            pSr.color = new Color(pSr.color.r, pSr.color.g, pSr.color.b, 0.8f * (1f - t * 0.5f));
            float s = p.transform.localScale.x * (1f - Time.deltaTime * 2f);
            p.transform.localScale = new Vector3(Mathf.Max(0.02f, s), Mathf.Max(0.02f, s), 1f);
            yield return null;
        }
        if (p != null) Destroy(p);
    }

    /// <summary>Mage隕石落下: 斜め上空から巨大隕石が落下 → 着弾波動</summary>
    System.Collections.IEnumerator SpawnMeteorImpact(Vector3 targetPos)
    {
        // 右上の空から斜めに落下（角度ランダム）
        float offsetX = Random.Range(3f, 5f);
        float offsetY = Random.Range(5f, 7f);
        Vector3 startPos = targetPos + new Vector3(offsetX, offsetY, 0);

        // 着弾前の影（地面に暗い円が広がる予兆）
        var shadow = new GameObject("MeteorShadow");
        shadow.transform.position = targetPos;
        var shSr = shadow.AddComponent<SpriteRenderer>();
        shSr.sprite = CreatePixelSprite();
        shSr.color = new Color(0, 0, 0, 0.5f);
        shSr.sortingOrder = 5;
        shadow.transform.localScale = Vector3.zero;
        StartCoroutine(MeteorShadowGrow(shadow, shSr, 0.4f));

        // 隕石本体（大きな火球）
        var meteor = MakeVFX("Meteor");
        meteor.transform.position = startPos;

        // 外殻（赤橙の炎）
        var mSr = meteor.AddComponent<SpriteRenderer>();
        mSr.sprite = CreatePixelSprite();
        mSr.color = new Color(1f, 0.3f, 0.05f, 1f);
        mSr.sortingOrder = 28;
        float baseSize = 0.6f;
        meteor.transform.localScale = new Vector3(baseSize, baseSize, 1f);

        // 中間層（明るいオレンジ）
        var mid = new GameObject("MeteorMid");
        mid.transform.SetParent(meteor.transform);
        mid.transform.localPosition = Vector3.zero;
        var midSr = mid.AddComponent<SpriteRenderer>();
        midSr.sprite = CreatePixelSprite();
        midSr.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        midSr.sortingOrder = 29;
        mid.transform.localScale = new Vector3(0.6f, 0.6f, 1f);

        // 芯（白熱）
        var core = new GameObject("MeteorCore");
        core.transform.SetParent(meteor.transform);
        core.transform.localPosition = Vector3.zero;
        var cSr = core.AddComponent<SpriteRenderer>();
        cSr.sprite = CreatePixelSprite();
        cSr.color = new Color(1f, 1f, 0.85f, 1f);
        cSr.sortingOrder = 30;
        core.transform.localScale = new Vector3(0.3f, 0.3f, 1f);

        // 落下（0.35秒、加速カーブ）
        float fallTime = 0.35f;
        float elapsed = 0f;
        float trailTimer = 0f;
        while (elapsed < fallTime && meteor != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fallTime;
            float accel = t * t; // 加速（放物線的に速くなる）
            meteor.transform.position = Vector3.Lerp(startPos, targetPos, accel);

            // 脈動 + 落下中にやや大きくなる
            float grow = baseSize + 0.15f * t;
            float pulse = grow + 0.05f * Mathf.Sin(elapsed * 35f);
            meteor.transform.localScale = new Vector3(pulse, pulse, 1f);

            // 炎トレイル（頻繁に、大きめ）
            trailTimer -= Time.deltaTime;
            if (trailTimer <= 0f)
            {
                trailTimer = 0.018f;
                SpawnMeteorTrail(meteor.transform.position, t);
            }
            yield return null;
        }

        // === 着弾 ===
        if (meteor != null)
        {
            Destroy(meteor);
            if (shadow != null) Destroy(shadow);
            CameraShake.Shake(0.25f, 0.12f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySE("CastleDestroy");

            // 着弾フラッシュ（白→橙）
            StartCoroutine(MeteorImpactFlash(targetPos));
            // 爆発パーティクル
            StartCoroutine(FireExplosion(targetPos));
            // 波動リング（地面を広がる衝撃波）
            StartCoroutine(MeteorShockwave(targetPos));
            // 破片飛散
            StartCoroutine(MeteorDebris(targetPos));
        }
    }

    /// <summary>隕石の影（着弾予兆）</summary>
    System.Collections.IEnumerator MeteorShadowGrow(GameObject shadow, SpriteRenderer shSr, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && shadow != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float s = Mathf.Lerp(0.1f, 0.8f, t);
            shadow.transform.localScale = new Vector3(s, s * 0.4f, 1f); // 楕円
            shSr.color = new Color(0, 0, 0, 0.3f + 0.3f * t);
            yield return null;
        }
        if (shadow != null) Destroy(shadow);
    }

    /// <summary>隕石の炎トレイル（大きめ、尾を引く）</summary>
    void SpawnMeteorTrail(Vector3 pos, float progress)
    {
        var trail = new GameObject("MeteorTrail");
        trail.transform.position = pos + new Vector3(
            Random.Range(-0.08f, 0.08f), Random.Range(-0.05f, 0.1f), 0);
        var trSr = trail.AddComponent<SpriteRenderer>();
        trSr.sprite = CreatePixelSprite();
        // 落下するにつれオレンジ→赤に
        float r = 1f;
        float g = Mathf.Lerp(0.6f, 0.2f, progress);
        float b = 0.1f;
        trSr.color = new Color(r, g, b, 0.9f);
        trSr.sortingOrder = 25;
        float size = Random.Range(0.12f, 0.25f);
        trail.transform.localScale = new Vector3(size, size, 1f);
        StartCoroutine(FadeAndDestroy(trail, trSr, Random.Range(0.15f, 0.3f)));
    }

    /// <summary>着弾フラッシュ（巨大な白い光が一瞬広がって消える）</summary>
    System.Collections.IEnumerator MeteorImpactFlash(Vector3 pos)
    {
        var flash = MakeVFX("ImpactFlash");
        flash.transform.position = pos;
        var fSr = flash.AddComponent<SpriteRenderer>();
        fSr.sprite = CreatePixelSprite();
        fSr.color = new Color(1f, 0.95f, 0.8f, 1f);
        fSr.sortingOrder = 35;

        float duration = 0.2f;
        float elapsed = 0f;
        while (elapsed < duration && flash != null)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float s = Mathf.Lerp(0.5f, 2.5f, t);
            flash.transform.localScale = new Vector3(s, s, 1f);
            fSr.color = new Color(1f, Mathf.Lerp(0.95f, 0.4f, t), Mathf.Lerp(0.8f, 0.1f, t), 1f - t);
            yield return null;
        }
        if (flash != null) Destroy(flash);
    }

    /// <summary>衝撃波リング（着弾から広がる波動）</summary>
    System.Collections.IEnumerator MeteorShockwave(Vector3 center)
    {
        // 2重リング（内側:白、外側:橙）
        for (int ring = 0; ring < 2; ring++)
        {
            float delay = ring * 0.06f;
            if (delay > 0) yield return new WaitForSeconds(delay);

            int segments = 24;
            var ringParts = new List<GameObject>();
            var ringSrs = new List<SpriteRenderer>();
            Color ringColor = ring == 0
                ? new Color(1f, 0.9f, 0.7f, 0.9f)
                : new Color(1f, 0.4f, 0.15f, 0.7f);

            for (int i = 0; i < segments; i++)
            {
                var dot = new GameObject("ShockDot");
                dot.transform.position = center;
                var dSr = dot.AddComponent<SpriteRenderer>();
                dSr.sprite = CreatePixelSprite();
                dSr.color = ringColor;
                dSr.sortingOrder = 32 - ring;
                float sz = ring == 0 ? 0.12f : 0.08f;
                dot.transform.localScale = new Vector3(sz, sz, 1f);
                ringParts.Add(dot);
                ringSrs.Add(dSr);
            }

            StartCoroutine(ExpandShockwaveRing(ringParts, ringSrs, center, segments,
                ring == 0 ? 3.5f : 4.5f, ring == 0 ? 0.35f : 0.45f));
        }
    }

    System.Collections.IEnumerator ExpandShockwaveRing(List<GameObject> parts, List<SpriteRenderer> srs,
        Vector3 center, int segments, float maxRadius, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float radius = Mathf.Lerp(0.2f, maxRadius, t);
            float alpha = 1f - t;

            for (int i = 0; i < segments; i++)
            {
                if (parts[i] == null) continue;
                float angle = (float)i / segments * Mathf.PI * 2f;
                parts[i].transform.position = center + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius * 0.5f, 0); // Y圧縮で地面波動感
                var c = srs[i].color;
                c.a = alpha * 0.8f;
                srs[i].color = c;
                float s = Mathf.Lerp(0.12f, 0.04f, t);
                parts[i].transform.localScale = new Vector3(s, s, 1f);
            }
            yield return null;
        }
        foreach (var p in parts)
            if (p != null) Destroy(p);
    }

    /// <summary>着弾時の岩破片飛散</summary>
    System.Collections.IEnumerator MeteorDebris(Vector3 pos)
    {
        for (int i = 0; i < 10; i++)
        {
            var debris = new GameObject("MeteorDebris");
            debris.transform.position = pos;
            var dSr = debris.AddComponent<SpriteRenderer>();
            dSr.sprite = CreatePixelSprite();
            // 茶〜灰色のランダム岩色
            float v = Random.Range(0.25f, 0.5f);
            dSr.color = new Color(v + 0.1f, v, v - 0.05f, 1f);
            dSr.sortingOrder = 26;
            float sz = Random.Range(0.06f, 0.14f);
            debris.transform.localScale = new Vector3(sz, sz, 1f);

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(2f, 5f);
            float upSpeed = Random.Range(2f, 4f);
            Vector3 dir = new Vector3(Mathf.Cos(angle) * speed, upSpeed, 0);
            StartCoroutine(DebrisFly(debris, dSr, dir));
        }
        yield break;
    }

    System.Collections.IEnumerator DebrisFly(GameObject debris, SpriteRenderer dSr, Vector3 velocity)
    {
        float life = Random.Range(0.4f, 0.7f);
        float elapsed = 0f;
        float gravity = 12f;
        while (elapsed < life && debris != null)
        {
            elapsed += Time.deltaTime;
            velocity.y -= gravity * Time.deltaTime;
            debris.transform.position += velocity * Time.deltaTime;
            debris.transform.Rotate(0, 0, 300f * Time.deltaTime);
            float t = elapsed / life;
            var c = dSr.color;
            c.a = 1f - t;
            dSr.color = c;
            yield return null;
        }
        if (debris != null) Destroy(debris);
    }

    /// <summary>Monk必殺技: 被ダメ軽減バフ</summary>
    System.Collections.IEnumerator UltimateDefenseBuff(float duration, float reduction)
    {
        damageReduction = Mathf.Min(baseDamageReduction + reduction, 0.9f);
        yield return new WaitForSeconds(duration);
        damageReduction = baseDamageReduction;
    }

    /// <summary>Knight必殺技: 無敵+ATK倍+速度倍</summary>
    System.Collections.IEnumerator KnightUltimateBuff(float duration)
    {
        hasUltimateBuff = true;
        attackPower = baseAttackPower * 2;
        moveSpeed = baseMoveSpeed * 2f;
        damageReduction = 1f; // 無敵

        yield return new WaitForSeconds(duration);

        // 常にベース値に復元（死亡中でも復元しておく）
        attackPower = baseAttackPower;
        moveSpeed = baseMoveSpeed;
        damageReduction = baseDamageReduction;
        hasUltimateBuff = false;
    }

    /// <summary>必殺技バフを強制解除（Wave開始時などに呼ぶ）</summary>
    public void ClearUltimateBuffs()
    {
        if (hasUltimateBuff)
        {
            attackPower = baseAttackPower;
            moveSpeed = baseMoveSpeed;
            damageReduction = baseDamageReduction;
            hasUltimateBuff = false;
        }
        // damageReductionもベースに戻す（Monk防御バフ対応）
        damageReduction = baseDamageReduction;
    }

    System.Collections.IEnumerator UltimateFlash()
    {
        Color orig = sr.color;
        // 5回高速フラッシュ（白→金→白→金→爆発白）
        Color[] flashColors = {
            Color.white,
            new Color(1f, 0.9f, 0.3f),
            Color.white,
            new Color(1f, 0.85f, 0.1f),
            Color.white
        };
        for (int i = 0; i < flashColors.Length; i++)
        {
            sr.color = flashColors[i];
            yield return new WaitForSeconds(0.05f);
            if (sr == null || isDead) yield break;
            sr.color = orig;
            yield return new WaitForSeconds(0.04f);
            if (sr == null || isDead) yield break;
        }
    }
}
