using UnityEngine;
// recompile v2
public enum GamePhase { Title, Preparation, Battle, Result }
public enum UnitType { Warrior, Lancer, Archer, Monk, Mage, Knight }
public enum Team { Ally, Enemy }
public enum UnitStance { Attack, Defend, Mid }
public enum GameMode { Offline, YouTube, TikTok }
public enum ReleaseMode { Both, YouTubeOnly, TikTokOnly }

[System.Serializable]
public class UnitStats
{
    public string displayName;
    public UnitType type;
    public int hp;
    public int attack;
    public float attackRange;
    public float attackSpeed;
    public float moveSpeed;
    public float damageReduction;
    public int healAmount;
}

[System.Serializable]
public class HeroAppearance
{
    public string hair;
    public string weapon;
    public string helmet;
    public string armor;
    public string shield;
    public string back;
}

[System.Serializable]
public class QueuedUnit
{
    public UnitType type;
    public string ownerName;
    public string viewerId;
    [System.NonSerialized] public UnityEngine.Sprite previewSprite;
    [System.NonSerialized] public HeroAppearance appearance;
}

[System.Serializable]
public class WaveData
{
    public int warriorCount;
    public int lancerCount;
    public int archerCount;
    public int monkCount;

    public int TotalCount => warriorCount + lancerCount + archerCount + monkCount;
}

public static class GameConfig
{
    // Field layout (world units)
    public const float FieldMinX = -9f;
    public const float FieldMaxX = 19f;
    public const float FieldMinY = -3.8f;
    public const float FieldMaxY = 3.2f;

    // Camera
    public const float CameraX = -3.61f;       // デフォルト位置（準備フェーズ）
    public const float CameraY = 0f;
    public const float CameraZ = -10f;
    public const float CameraOrthoSize = 5.5f;
    public const float CameraMinX = -3.61f;     // 追従時の左限界（城が見える位置）
    public const float CameraMaxX = 14f;         // 追従時の右限界（敵城が見える位置）
    public const float CameraSmoothSpeed = 3f;   // 追従の滑らかさ

    // Castle (味方)
    public const float CastleX = -8f;
    public const float CastleY = 1.0f;
    public const int CastleMaxHP = 500;

    // Enemy Castle (敵陣)
    public const float EnemyCastleX = 17.21f;
    public const float EnemyCastleY = 1.0f;
    public const int EnemyCastleBaseHP = 300;

    public static int GetEnemyCastleHP(int waveIndex)
    {
        return EnemyCastleBaseHP + waveIndex * 50;
    }

    // Zones
    public const float PlacementZoneMinX = -6.5f;
    public const float PlacementZoneMaxX = -0.5f;
    public const float PlacementZoneMinY = -1.5f;
    public const float PlacementZoneMaxY = 2.5f;
    public const float EnemySpawnX = 12f;
    public const float EnemySpawnMinY = -3f;
    public const float EnemySpawnMaxY = 3f;

    // NPC Formation positions (X座標)
    public const float NpcFrontX = 4f;   // 前衛
    public const float NpcMidX = 0f;     // 中衛
    public const float NpcRearX = -6f;   // 後衛

    // Unit collision
    public const float UnitRadius = 0.35f;
    public const float UnitPushForce = 3f;

    // Pixel Heroes base scale (64px sprite, PPU=16 → 4 world units native → 0.4 ≈ 1.6 world units)
    public const float PixelHeroBaseScale = 0.4f;

    // レベルアップ時のサイズ成長（デバッグ調整可能）
    public static float MaxSizeMultiplier = 9.0f;    // 最大サイズ = BaseScale × この値
    public static int MaxSizeAtLevel = 100;           // このレベルでちょうどMaxSizeに到達

    // SizeBoostは自動計算: MaxSizeMultiplier^(1/(MaxSizeAtLevel-1))
    public static float LevelUpSizeBoost =>
        MaxSizeAtLevel > 1 ? Mathf.Pow(MaxSizeMultiplier, 1f / (MaxSizeAtLevel - 1)) : 1f;

    // Timing
    public const float DeathAnimDuration = 0.8f;
    public const float DamagePopupDuration = 1.0f;
    public const float DamagePopupRiseSpeed = 1.5f;
    public const float KillLogDuration = 5.0f;
    public const int KillLogMaxEntries = 5;

    // Test mode
    public const float AutoSpawnInterval = 4.0f;
    public const float ViewerCooldown = 30.0f;
    public const int MaxUnitsPerViewer = 1;
    public const int MaxAllyUnits = 20;       // NPC(チャット召喚)全体の上限
    public const int MaxGiftUnitsPerViewer = 2; // エール(tier0-1)ギフトの召喚上限（1視聴者あたり）
    public const float GiftRepeatBuff = 1.05f;  // 上限到達後のエール1回あたりの強化倍率

    // ─── 必殺技 ──────────────────────────────────────────────
    public const int UltimateCost = 300;    // ランキングポイント消費量

    // ─── ゴールド（つうか） ─────────────────────────────────────
    public const int StartingGold = 5;
    public const int GoldPerSecond = 1;     // じかんけいかしゅうにゅう
    public const int GoldPerKill = 2;       // てきげきはボーナス

    public static int GetUnitCost(UnitType type)
    {
        switch (type)
        {
            case UnitType.Warrior: return 3;
            case UnitType.Lancer:  return 4;
            case UnitType.Archer:  return 4;
            case UnitType.Monk:    return 5;
            case UnitType.Mage:    return 5;
            case UnitType.Knight:  return 8;
            default: return 5;
        }
    }

    // ─── YouTube Action Effects ─────────────────────────────────

    // Super Chat tiers (JPY thresholds)
    public static readonly int[] SuperChatTierMinJPY = { 200, 500, 1000, 5000, 10000 };
    public static readonly string[] SuperChatTierNames = { "Blue", "Green", "Yellow", "Orange", "Red" };
    public static readonly Color[] SuperChatTierColors = new Color[]
    {
        new Color(0.3f, 0.6f, 1f),      // Blue
        new Color(0.3f, 0.9f, 0.4f),    // Green
        new Color(1f, 0.9f, 0.2f),      // Yellow
        new Color(1f, 0.6f, 0.2f),      // Orange
        new Color(1f, 0.2f, 0.2f)       // Red
    };
    public static readonly float[] SuperChatStatBuff = { 1.1f, 1.2f, 1.35f, 1.5f, 2.0f };
    // SC tier → unit color variant (Blue/Purple/Yellow/Red/Black)
    public static readonly string[] SuperChatUnitColorName = { "Blue", "Purple", "Yellow", "Red", "Black" };
    public static readonly int[] SuperChatExtraUnits = { 0, 0, 1, 2, 3 };

    // Membership
    public const float MemberStatBuff = 1.15f;
    public const float MemberCooldownMult = 0.5f;

    // Like milestones (YouTube)
    public static readonly int[] LikeMilestones = { 50, 100, 200, 500, 1000 };
    public static readonly float[] LikeGlobalBuff = { 1.05f, 1.1f, 1.15f, 1.2f, 1.3f };
    public const float LikePollInterval = 30f;

    // ─── TikTok Action Effects ──────────────────────────────────

    // TikTok Gift tiers (diamond/coin thresholds)
    public static readonly int[] TikTokGiftTierMinCoins = { 1, 10, 99, 500, 5000, 44999 };
    public static readonly string[] TikTokGiftTierNames = { "エール", "ブルー", "グリーン", "ゴールド", "レジェンド", "ユニバ" };
    public static readonly Color[] TikTokGiftTierColors = new Color[]
    {
        new Color(1f, 0.6f, 0.8f),     // ピンク (エール)
        new Color(0.3f, 0.6f, 1f),     // 青 (ブルー)
        new Color(0.3f, 0.85f, 0.4f),  // 緑 (グリーン)
        new Color(1f, 0.85f, 0.2f),    // 金 (ゴールド)
        new Color(0.9f, 0.2f, 0.2f),   // 赤 (レジェンド)
        new Color(0.6f, 0.2f, 1f)      // 紫 (ユニバ)
    };
    public static readonly float[] TikTokGiftStatBuff = { 1.1f, 1.2f, 1.5f, 2.0f, 3.0f, 5.0f };
    public static readonly int[] TikTokGiftExtraUnits = { 0, 0, 1, 1, 1, 1 };
    // ティア0-1: 既存ユニットにバフのみ。ティア2+: エリート/英雄/伝説/神話ユニット召喚
    public static readonly string[] TikTokGiftUnitColorName = { "Blue", "Blue", "Yellow", "Red", "Black", "Black" };

    // TikTok Team (Fan Club) level tiers: [Lv1-4, Lv5-9, Lv10-17, Lv18+]
    public static readonly float[] TeamHPBuff = { 1.10f, 1.15f, 1.20f, 1.25f };
    public static readonly float[] TeamATKBuff = { 1.05f, 1.08f, 1.10f, 1.12f };
    public static readonly float[] TeamCooldownSec = { 25f, 25f, 20f, 18f };

    // TikTok Subscription
    public const float SubscriptionHPBuff = 1.20f;
    public const float SubscriptionATKBuff = 1.15f;
    public const float SubscriptionCooldown = 20f;

    // TikTok Like milestones (multi-tap, higher thresholds)
    public static readonly int[] TikTokLikeMilestones = { 50, 200, 500, 1000, 3000 };
    public static readonly string[] TikTokLikeMilestoneNames = { "おうえんのちから", "せいえんのなみ", "たみのいのり", "おうのかご", "はおうのいし" };
    public static readonly float[] TikTokLikeHPBuff = { 1.05f, 1.05f, 1.10f, 1.15f, 1.20f };
    public static readonly float[] TikTokLikeATKBuff = { 1.0f, 1.10f, 1.10f, 1.15f, 1.20f };
    public static readonly float[] TikTokLikeSpeedBuff = { 1.0f, 1.0f, 1.0f, 1.10f, 1.15f };
    public static readonly int[] TikTokLikeCastleHeal = { 0, 0, 0, 100, 200 };
    public static readonly float[] TikTokLikeInvincibleSec = { 0, 0, 0, 0, 3f };

    // Knight (TikTok subscriber-only unit)
    public const float KnightHPMult = 1.3f;
    public const float KnightATKMult = 1.2f;
    public const float KnightAuraHPBuff = 1.05f;
    public const float KnightAuraRange = 2f;

    // Wave stat scaling (動的計算、無限Wave対応)
    public static float GetWaveScaling(int waveIndex)
    {
        // Wave 1-2: 1.0, Wave 3-10: +0.1ずつ, Wave 11+: +0.2ずつ（少数精鋭）
        if (waveIndex <= 1) return 1.0f;
        if (waveIndex <= 9) return 1.0f + (waveIndex - 1) * 0.1f;
        // Wave 10: 1.9, Wave 11: 2.1, Wave 15: 2.9, Wave 20: 3.9, Wave 25: 4.9
        float base10 = 1.0f + 8 * 0.1f; // 1.8 at wave 9 (0-indexed)
        return base10 + (waveIndex - 9) * 0.2f;
    }

    // Enemy spawn stagger (seconds between each enemy spawn within a wave)
    public const float EnemySpawnInterval = 0.6f;

    // Burst spawning
    public const float BurstIntraInterval = 0.05f;   // バースト内の微小間隔
    public const float BurstInterInterval = 5.0f;     // バースト間の待機時間
    public const int MaxBurstCount = 8;

    public static int GetBurstCount(int waveIndex)
    {
        return Mathf.Min(1 + waveIndex / 3, MaxBurstCount);
    }

    public static UnitStats GetBaseStats(UnitType type)
    {
        switch (type)
        {
            case UnitType.Warrior:
                return new UnitStats
                {
                    displayName = "Warrior", type = UnitType.Warrior,
                    hp = 100, attack = 20, attackRange = 0.6f,
                    attackSpeed = 1.0f, moveSpeed = 2.0f
                };
            case UnitType.Lancer:
                return new UnitStats
                {
                    displayName = "Lancer", type = UnitType.Lancer,
                    hp = 200, attack = 8, attackRange = 0.6f,
                    attackSpeed = 1.5f, moveSpeed = 1.5f,
                    damageReduction = 0.3f
                };
            case UnitType.Archer:
                return new UnitStats
                {
                    displayName = "Archer", type = UnitType.Archer,
                    hp = 50, attack = 10, attackRange = 3.5f,
                    attackSpeed = 1.6f, moveSpeed = 1.8f
                };
            case UnitType.Monk:
                return new UnitStats
                {
                    displayName = "Monk", type = UnitType.Monk,
                    hp = 70, attack = 0, attackRange = 2.0f,
                    attackSpeed = 2.0f, moveSpeed = 1.5f,
                    healAmount = 10
                };
            case UnitType.Mage:
                return new UnitStats
                {
                    displayName = "Mage", type = UnitType.Mage,
                    hp = 60, attack = 18, attackRange = 3.0f,
                    attackSpeed = 1.5f, moveSpeed = 1.5f
                };
            case UnitType.Knight:
                return new UnitStats
                {
                    displayName = "Knight", type = UnitType.Knight,
                    hp = (int)(100 * KnightHPMult), attack = (int)(20 * KnightATKMult),
                    attackRange = 0.6f,
                    attackSpeed = 1.0f, moveSpeed = 1.8f
                };
            default:
                return new UnitStats();
        }
    }

    // Wave definitions (Wave 1-10 は固定、11以降は自動生成)
    private static readonly WaveData[] BaseWaves = new WaveData[]
    {
        new WaveData { warriorCount = 3 },                                                          // Wave 1
        new WaveData { warriorCount = 5 },                                                          // Wave 2
        new WaveData { warriorCount = 3, lancerCount = 2 },                                         // Wave 3
        new WaveData { warriorCount = 4, archerCount = 3 },                                         // Wave 4
        new WaveData { lancerCount = 3, archerCount = 4, monkCount = 1 },                           // Wave 5
        new WaveData { warriorCount = 6, lancerCount = 2, archerCount = 2 },                        // Wave 6
        new WaveData { warriorCount = 4, lancerCount = 4, archerCount = 4 },                        // Wave 7
        new WaveData { lancerCount = 5, archerCount = 5, monkCount = 2 },                           // Wave 8
        new WaveData { warriorCount = 8, lancerCount = 4, archerCount = 4, monkCount = 2 },         // Wave 9
        new WaveData { warriorCount = 10, lancerCount = 6, archerCount = 6, monkCount = 4 },        // Wave 10
    };

    /// <summary>Wave数は無限。固定Wave以降は自動生成。</summary>
    public static WaveData GetWaveData(int waveIndex)
    {
        if (waveIndex < BaseWaves.Length) return BaseWaves[waveIndex];

        // Wave 11以降: 段階的に増加（上限20）+ ランダム構成
        int extra = waveIndex - BaseWaves.Length + 1;
        int baseCount = Mathf.Min(10 + extra * 2, 20); // 12,14,...20(cap)
        int warriors = Mathf.RoundToInt(baseCount * 0.35f);
        int lancers  = Mathf.RoundToInt(baseCount * 0.25f);
        int archers  = Mathf.RoundToInt(baseCount * 0.25f);
        int monks    = Mathf.RoundToInt(baseCount * 0.15f);

        return new WaveData
        {
            warriorCount = warriors,
            lancerCount = lancers,
            archerCount = archers,
            monkCount = monks
        };
    }

    /// <summary>ボス出現判定 (Wave 3以降、3Wave毎 + 一定確率)</summary>
    public static bool HasBoss(int waveIndex)
    {
        return waveIndex >= 2 && (waveIndex % 3 == 2);
    }

    /// <summary>ボスのスケール倍率</summary>
    public static float GetBossScale(int waveIndex)
    {
        return 1.5f + waveIndex * 0.08f;
    }
}
