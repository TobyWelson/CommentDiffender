using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    public int currentWave = 0; // 0-indexed, display as +1
    public int totalWaves => int.MaxValue; // 無限Wave
    public int enemiesRemainingToSpawn;
    public int enemiesAlive;
    public bool isSpawning;

    private Queue<UnitType> spawnQueue = new Queue<UnitType>();
    private float spawnTimer;
    private bool bossSpawned; // ボス出現済みフラグ（Wave毎にリセット）

    // Burst spawning
    private int burstCount;
    private int currentBurst;
    private int enemiesPerBurst;
    private int enemiesSpawnedInBurst;
    private bool isBurstActive;
    private float burstPauseTimer;

    public event System.Action OnAllEnemiesDefeated;
    public event System.Action<int> OnWaveStarted; // wave number (1-indexed)

    public void StartWave(int waveIndex, bool spawnBoss = true)
    {
        currentWave = waveIndex;
        Debug.Log($"[WaveManager] StartWave({waveIndex}, boss={spawnBoss}) called.");

        WaveData data = GameConfig.GetWaveData(waveIndex);
        BuildSpawnQueue(data);
        enemiesRemainingToSpawn = spawnQueue.Count;
        enemiesAlive = 0;
        isSpawning = true;
        spawnTimer = 0f;
        bossSpawned = !spawnBoss; // falseなら最初からspawn済み扱い→ボス出ない

        // Burst setup
        burstCount = GameConfig.GetBurstCount(waveIndex);
        currentBurst = 0;
        enemiesPerBurst = Mathf.CeilToInt((float)spawnQueue.Count / Mathf.Max(burstCount, 1));
        enemiesSpawnedInBurst = 0;
        isBurstActive = true;
        burstPauseTimer = 0f;

        Debug.Log($"[WaveManager] Spawn queue built: {spawnQueue.Count} enemies, {burstCount} bursts ({enemiesPerBurst}/burst). isSpawning={isSpawning}");
        OnWaveStarted?.Invoke(waveIndex + 1);
    }

    void BuildSpawnQueue(WaveData data)
    {
        spawnQueue.Clear();
        var list = new List<UnitType>();

        for (int i = 0; i < data.warriorCount; i++) list.Add(UnitType.Warrior);
        for (int i = 0; i < data.lancerCount; i++) list.Add(UnitType.Lancer);
        for (int i = 0; i < data.archerCount; i++) list.Add(UnitType.Archer);
        for (int i = 0; i < data.monkCount; i++) list.Add(UnitType.Monk);

        // Shuffle for varied spawn order
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }

        foreach (var t in list)
            spawnQueue.Enqueue(t);
    }

    void Update()
    {
        if (!isSpawning) return;

        if (isBurstActive)
        {
            // バースト中: 高速スポーン
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && spawnQueue.Count > 0)
            {
                SpawnNextEnemy();
                enemiesSpawnedInBurst++;
                spawnTimer = GameConfig.BurstIntraInterval;

                // バースト完了チェック
                if (enemiesSpawnedInBurst >= enemiesPerBurst || spawnQueue.Count == 0)
                {
                    isBurstActive = false;
                    currentBurst++;
                    enemiesSpawnedInBurst = 0;
                    burstPauseTimer = GameConfig.BurstInterInterval;
                    Debug.Log($"[WaveManager] Burst {currentBurst}/{burstCount} complete. Next burst in {burstPauseTimer}s. Queue remaining={spawnQueue.Count}");
                }
            }
        }
        else
        {
            // バースト間: 待機
            burstPauseTimer -= Time.deltaTime;
            if (burstPauseTimer <= 0f && spawnQueue.Count > 0)
            {
                isBurstActive = true;
                spawnTimer = 0f;
                Debug.Log($"[WaveManager] Starting burst {currentBurst + 1}/{burstCount}. Queue={spawnQueue.Count}");
            }
        }

        // スポーン完了チェック
        if (spawnQueue.Count == 0 && isBurstActive)
        {
            isSpawning = false;
        }
    }

    void SpawnNextEnemy()
    {
        UnitType type = spawnQueue.Dequeue();
        enemiesRemainingToSpawn--;

        // 敵城の位置からスポーン
        float spawnX = GameConfig.EnemyCastleX;
        Vector3 spawnPos = Vector3.zero;
        bool found = false;
        for (int i = 0; i < 30; i++)
        {
            float y = Random.Range(GameConfig.EnemySpawnMinY, GameConfig.EnemySpawnMaxY);
            var candidate = new Vector3(spawnX, y, 0);
            if (Unit.IsWalkable(candidate)) { spawnPos = candidate; found = true; break; }
        }
        if (!found) spawnPos = new Vector3(spawnX, 0, 0);

        float scale = GameConfig.GetWaveScaling(currentWave);

        // ボス判定: Wave毎に1体、最初のスポーンをボスに
        bool isBoss = !bossSpawned && GameConfig.HasBoss(currentWave);
        if (isBoss)
        {
            scale = GameConfig.GetBossScale(currentWave);
            bossSpawned = true;
        }

        var gm = GameManager.Instance;
        if (gm != null)
        {
            var unit = gm.SpawnUnit(type, Team.Enemy, "Enemy", spawnPos, scale, isBoss: isBoss);
            enemiesAlive++;
        }
        else
        {
            Debug.LogError("[WaveManager] GameManager.Instance is null!");
        }
    }

    public void OnEnemyKilled()
    {
        enemiesAlive--;
        if (enemiesAlive <= 0 && spawnQueue.Count == 0)
        {
            enemiesAlive = 0;
            OnAllEnemiesDefeated?.Invoke();
        }
    }

    public void ForceStopSpawning()
    {
        spawnQueue.Clear();
        enemiesRemainingToSpawn = 0;
        isSpawning = false;
        isBurstActive = false;
        currentBurst = 0;
    }

    public int GetTotalEnemiesInWave()
    {
        return GameConfig.GetWaveData(currentWave).TotalCount + (GameConfig.HasBoss(currentWave) ? 0 : 0);
    }

    public int GetEnemiesRemaining()
    {
        return enemiesAlive + spawnQueue.Count;
    }
}
