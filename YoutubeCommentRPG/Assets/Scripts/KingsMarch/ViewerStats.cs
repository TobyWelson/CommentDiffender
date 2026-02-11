using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// 視聴者ごとの貢献度を追跡するシステム。
/// viewerId (YouTube channelId / TikTok userId) をキーとして管理。
/// </summary>
public class ViewerStats : MonoBehaviour
{
    public static ViewerStats Instance { get; private set; }

    // viewerId → ViewerData
    private Dictionary<string, ViewerData> stats = new Dictionary<string, ViewerData>();
    // ownerName → viewerId の逆引き（イベントハンドラ用）
    private Dictionary<string, string> nameToId = new Dictionary<string, string>();

    // Events
    public event System.Action OnStatsChanged;

    private GameMode currentSaveMode = GameMode.Offline;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        // Load()はモード確定後にGameManagerから呼ぶ
    }

    void OnEnable()
    {
        Unit.OnUnitKilled += HandleUnitKilled;
        Unit.OnDamageDealt += HandleDamageDealt;
        Unit.OnHealPerformed += HandleHealPerformed;
    }

    void OnDisable()
    {
        Unit.OnUnitKilled -= HandleUnitKilled;
        Unit.OnDamageDealt -= HandleDamageDealt;
        Unit.OnHealPerformed -= HandleHealPerformed;
    }

    // ─── 記録 ──────────────────────────────────────────────

    public void RecordSummon(string viewerId, string ownerName, UnitType type)
    {
        var data = GetOrCreate(viewerId, ownerName);
        data.summonCount++;
        data.lastUnitType = type;
        OnStatsChanged?.Invoke();
    }

    /// <summary>ownerNameからviewerIdを逆引き（イベントハンドラ用）</summary>
    string ResolveId(string ownerName)
    {
        if (string.IsNullOrEmpty(ownerName)) return null;
        if (nameToId.TryGetValue(ownerName, out string id)) return id;
        // フォールバック: ownerName自体がキー（オフラインモード）
        if (stats.ContainsKey(ownerName)) return ownerName;
        return null;
    }

    void HandleUnitKilled(Unit killer, Unit victim)
    {
        if (killer == null || string.IsNullOrEmpty(killer.ownerName)) return;
        if (victim.team == Team.Enemy)
        {
            string id = !string.IsNullOrEmpty(killer.viewerId) ? killer.viewerId : ResolveId(killer.ownerName);
            if (id == null) return;
            var data = GetOrCreate(id, killer.ownerName);
            data.kills++;
            // 必殺技中はスコア加算しない
            if (!killer.isUltimateActive)
                data.score += 100;
            OnStatsChanged?.Invoke();
        }
    }

    void HandleDamageDealt(Unit attacker, Unit target, int damage)
    {
        if (attacker == null || string.IsNullOrEmpty(attacker.ownerName)) return;
        if (attacker.team != Team.Ally) return;
        if (target.team != attacker.team)
        {
            string id = !string.IsNullOrEmpty(attacker.viewerId) ? attacker.viewerId : ResolveId(attacker.ownerName);
            if (id == null) return;
            var data = GetOrCreate(id, attacker.ownerName);
            data.damageDealt += damage;
            // 必殺技中はスコア加算しない
            if (!attacker.isUltimateActive)
                data.score += damage;
            OnStatsChanged?.Invoke();
        }
    }

    void HandleHealPerformed(Unit healer, Unit target, int amount)
    {
        if (healer == null || string.IsNullOrEmpty(healer.ownerName)) return;
        if (healer.team != Team.Ally) return;
        string id = !string.IsNullOrEmpty(healer.viewerId) ? healer.viewerId : ResolveId(healer.ownerName);
        if (id == null) return;
        var data = GetOrCreate(id, healer.ownerName);
        data.healAmount += amount;
        data.score += amount * 2;
        OnStatsChanged?.Invoke();
    }

    // ─── YouTube Action Effects ─────────────────────────────

    public void SetMember(string viewerId, string ownerName)
    {
        var data = GetOrCreate(viewerId, ownerName);
        data.isMember = true;
        OnStatsChanged?.Invoke();
    }

    public bool IsMember(string viewerId)
    {
        return stats.ContainsKey(viewerId) && stats[viewerId].isMember;
    }

    public void SetProfileImage(string viewerId, string ownerName, string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        var data = GetOrCreate(viewerId, ownerName);
        if (string.IsNullOrEmpty(data.profileImageUrl))
            data.profileImageUrl = url;
    }

    /// <summary>ランキングポイントを消費する。足りなければ false を返す。</summary>
    public bool TrySpendScore(string viewerId, int cost)
    {
        if (!stats.ContainsKey(viewerId)) return false;
        var data = stats[viewerId];
        if (data.score < cost) return false;
        data.score -= cost;
        OnStatsChanged?.Invoke();
        return true;
    }

    public int GetScore(string viewerId)
    {
        return stats.ContainsKey(viewerId) ? stats[viewerId].score : 0;
    }

    public void SetSuperChatTier(string viewerId, string ownerName, int tier, int jpyAmount)
    {
        var data = GetOrCreate(viewerId, ownerName);
        if (tier > data.superChatTier) data.superChatTier = tier;
        data.totalSuperChatJPY += jpyAmount;
        data.score += jpyAmount;
        OnStatsChanged?.Invoke();
    }

    public int GetSuperChatTier(string viewerId)
    {
        return stats.ContainsKey(viewerId) ? stats[viewerId].superChatTier : -1;
    }

    // ─── TikTok Action Effects ──────────────────────────────

    public void SetTeamLevel(string viewerId, string ownerName, int level)
    {
        var data = GetOrCreate(viewerId, ownerName);
        if (level > data.teamLevel) data.teamLevel = level;
        OnStatsChanged?.Invoke();
    }

    public int GetTeamLevel(string viewerId)
    {
        return stats.ContainsKey(viewerId) ? stats[viewerId].teamLevel : 0;
    }

    /// <summary>チームレベル→4段階ティア (0=Lv1-4, 1=Lv5-9, 2=Lv10-17, 3=Lv18+)</summary>
    public static int GetTeamLevelTier(int level)
    {
        if (level >= 18) return 3;
        if (level >= 10) return 2;
        if (level >= 5) return 1;
        return 0;
    }

    public void SetSubscriber(string viewerId, string ownerName)
    {
        var data = GetOrCreate(viewerId, ownerName);
        data.isSubscriber = true;
        OnStatsChanged?.Invoke();
    }

    public bool IsSubscriber(string viewerId)
    {
        return stats.ContainsKey(viewerId) && stats[viewerId].isSubscriber;
    }

    public void SetTikTokGiftTier(string viewerId, string ownerName, int tier, int coins)
    {
        var data = GetOrCreate(viewerId, ownerName);
        if (tier > data.tiktokGiftTier) data.tiktokGiftTier = tier;
        data.totalGiftCoins += coins;
        data.score += coins;
        OnStatsChanged?.Invoke();
    }

    public int GetTikTokGiftTier(string viewerId)
    {
        return stats.ContainsKey(viewerId) ? stats[viewerId].tiktokGiftTier : -1;
    }

    // ─── ユニットレベル ───────────────────────────────────────

    /// <summary>ユニットのレベルが上がった時に最高記録を更新</summary>
    public void UpdateBestLevel(string viewerId, int level, int xp)
    {
        if (!stats.ContainsKey(viewerId)) return;
        var data = stats[viewerId];
        if (level > data.bestUnitLevel || (level == data.bestUnitLevel && xp > data.bestUnitXP))
        {
            data.bestUnitLevel = level;
            data.bestUnitXP = xp;
        }
    }

    /// <summary>デスペナルティ: 1レベル減少</summary>
    public void ApplyDeathPenalty(string viewerId)
    {
        if (!stats.ContainsKey(viewerId)) return;
        var data = stats[viewerId];
        if (data.bestUnitLevel > 1)
        {
            data.bestUnitLevel--;
            data.bestUnitXP = 0;
        }
    }

    // ─── データ取得 ─────────────────────────────────────────

    ViewerData GetOrCreate(string viewerId, string ownerName)
    {
        if (string.IsNullOrEmpty(viewerId)) viewerId = ownerName;
        if (!stats.ContainsKey(viewerId))
            stats[viewerId] = new ViewerData { viewerId = viewerId, ownerName = ownerName };
        else
            stats[viewerId].ownerName = ownerName; // 表示名を最新に更新
        nameToId[ownerName] = viewerId;
        return stats[viewerId];
    }

    /// <summary>viewerIdで取得</summary>
    public ViewerData GetStats(string viewerId)
    {
        return stats.ContainsKey(viewerId) ? stats[viewerId] : null;
    }

    /// <summary>ownerNameで取得（後方互換）</summary>
    public ViewerData GetStatsByName(string ownerName)
    {
        string id = ResolveId(ownerName);
        if (id != null && stats.ContainsKey(id)) return stats[id];
        return null;
    }

    public int GetViewerCount() => stats.Count;

    // ─── ランキング ─────────────────────────────────────────

    public List<ViewerData> GetRanking(int topN = 10)
    {
        return stats.Values
            .OrderByDescending(v => v.score)
            .Take(topN)
            .ToList();
    }

    public List<ViewerData> GetKillRanking(int topN = 5)
    {
        return stats.Values
            .Where(v => v.kills > 0)
            .OrderByDescending(v => v.kills)
            .Take(topN)
            .ToList();
    }

    public ViewerData GetMVP()
    {
        if (stats.Count == 0) return null;
        return stats.Values.OrderByDescending(v => v.score).First();
    }

    // ─── セーブ/ロード ─────────────────────────────────────────

    string GetSavePath(GameMode mode)
    {
        string suffix;
        switch (mode)
        {
            case GameMode.YouTube: suffix = "_youtube"; break;
            case GameMode.TikTok:  suffix = "_tiktok"; break;
            default:               suffix = "_offline"; break;
        }
        return Path.Combine(Application.persistentDataPath, $"viewer_save{suffix}.json");
    }

    string SavePath => GetSavePath(currentSaveMode);

    /// <summary>モード確定後に呼ぶ。既存データをクリアして該当モードのセーブをロード</summary>
    public void LoadForMode(GameMode mode)
    {
        currentSaveMode = mode;
        stats.Clear();
        nameToId.Clear();
        Load();
    }

    public void Save()
    {
        var wrapper = new SaveWrapper();
        wrapper.viewers = stats.Values.ToList();
        // GameManager のWave進行状況も保存
        if (GameManager.Instance != null)
        {
            wrapper.currentWaveIndex = GameManager.Instance.currentWaveIndex;
            wrapper.totalKills = GameManager.Instance.totalKills;
        }
        string json = JsonUtility.ToJson(wrapper, true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[ViewerStats] Saved {stats.Count} viewers, wave={wrapper.currentWaveIndex} to {SavePath}");
    }

    public int loadedWaveIndex { get; private set; } = 0;
    public int loadedTotalKills { get; private set; } = 0;

    public void Load()
    {
        if (!File.Exists(SavePath)) return;
        try
        {
            string json = File.ReadAllText(SavePath);
            var wrapper = JsonUtility.FromJson<SaveWrapper>(json);
            if (wrapper != null && wrapper.viewers != null)
            {
                stats.Clear();
                nameToId.Clear();
                foreach (var vd in wrapper.viewers)
                {
                    string key = !string.IsNullOrEmpty(vd.viewerId) ? vd.viewerId : vd.ownerName;
                    vd.viewerId = key;
                    stats[key] = vd;
                    if (!string.IsNullOrEmpty(vd.ownerName))
                        nameToId[vd.ownerName] = key;
                }
                loadedWaveIndex = wrapper.currentWaveIndex;
                loadedTotalKills = wrapper.totalKills;
                Debug.Log($"[ViewerStats] Loaded {stats.Count} viewers, wave={loadedWaveIndex} from {SavePath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[ViewerStats] Failed to load save: {e.Message}");
        }
    }

    // ─── リセット ───────────────────────────────────────────

    public void ResetAll()
    {
        stats.Clear();
        nameToId.Clear();
        OnStatsChanged?.Invoke();
    }

    /// <summary>セーブデータ削除 + メモリクリア</summary>
    public void DeleteSave()
    {
        string path = SavePath;
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[ViewerStats] Deleted save: {path}");
        }
        loadedWaveIndex = 0;
        loadedTotalKills = 0;
        ResetAll();
    }
}

[System.Serializable]
public class SaveWrapper
{
    public List<ViewerData> viewers = new List<ViewerData>();
    public int currentWaveIndex = 0;
    public int totalKills = 0;
}

[System.Serializable]
public class ViewerData
{
    public string viewerId;
    public string ownerName;
    public int kills;
    public int damageDealt;
    public int healAmount;
    public int summonCount;
    public int score;
    public UnitType lastUnitType;

    // Unit progress
    public int bestUnitLevel = 1;
    public int bestUnitXP = 0;

    // YouTube Action Effects
    public bool isMember;
    public int superChatTier = -1; // -1=なし, 0-4=ティア
    public int totalSuperChatJPY;
    public string profileImageUrl;

    // TikTok Action Effects
    public int teamLevel = 0;        // 0=非チーム, 1-18+
    public bool isSubscriber = false;
    public int tiktokGiftTier = -1;  // -1=なし, 0-5=ティア
    public int totalGiftCoins = 0;
}
