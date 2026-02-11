using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 8bitサウンド管理シングルトン。
/// SE・BGMをSoundGeneratorでコード生成し、名前で再生。
/// 差し替え: AudioManager.Instance.SetClip("SwordHit", yourClip) で上書き可能。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private AudioSource sfxSource;
    private AudioSource bgmSource;
    private Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    private string currentBgmName;
    private Coroutine fadeCoroutine;

    [Range(0f, 1f)] public float seVolume = 0.7f;
    [Range(0f, 1f)] public float bgmVolume = 0.5f;

    // SE同時再生制限
    private const int MaxConcurrentSE = 6;          // 全SE合計の同時再生上限
    private const float MinSEInterval = 0.05f;      // 同一SEの最低再生間隔(秒)
    private Dictionary<string, float> seLastPlayTime = new Dictionary<string, float>();
    private int activeSECount;
    private float activeSEDecayTimer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;

        GenerateAllSounds();
    }

    void GenerateAllSounds()
    {
        // SE
        clips["SwordDraw"] = SoundGenerator.GenSwordDraw();
        clips["SwordHit"] = SoundGenerator.GenSwordHit();
        clips["ArrowShot"] = SoundGenerator.GenArrowShot();
        clips["Heal"] = SoundGenerator.GenHeal();
        clips["MagicFire"] = SoundGenerator.GenMagicFire();
        clips["LevelUp"] = SoundGenerator.GenLevelUp();
        clips["UnitDeath"] = SoundGenerator.GenUnitDeath();
        clips["CastleHit"] = SoundGenerator.GenCastleHit();
        clips["CastleDestroy"] = SoundGenerator.GenCastleDestroy();
        clips["WaveStart"] = SoundGenerator.GenWaveStart();
        clips["WaveClear"] = SoundGenerator.GenWaveClear();
        clips["Victory"] = SoundGenerator.GenVictory();
        clips["GameOver"] = SoundGenerator.GenGameOver();
        clips["UnitPlace"] = SoundGenerator.GenUnitPlace();
        clips["UnitPickup"] = SoundGenerator.GenUnitPickup();
        clips["ButtonClick"] = SoundGenerator.GenButtonClick();
        clips["QueueAdd"] = SoundGenerator.GenQueueAdd();
        clips["Warning"] = SoundGenerator.GenWarning();
        clips["CoinGet"] = SoundGenerator.GenCoinGet();
        clips["SuperChat"] = SoundGenerator.GenSuperChat();
        clips["NewMember"] = SoundGenerator.GenNewMember();
        clips["LikeMilestone"] = SoundGenerator.GenLikeMilestone();
        clips["BossSmash"] = SoundGenerator.GenBossSmash();

        // BGM
        clips["BgmTitle"] = SoundGenerator.GenBgmTitle();
        clips["BgmPreparation"] = SoundGenerator.GenBgmPreparation();
        clips["BgmBattle"] = SoundGenerator.GenBgmBattle();
        clips["BgmResult"] = SoundGenerator.GenBgmResult();

        Debug.Log($"[AudioManager] {clips.Count} sounds generated.");
    }

    void Update()
    {
        // 同時再生カウンタを時間経過で減衰
        activeSEDecayTimer += Time.deltaTime;
        if (activeSEDecayTimer >= 0.08f)
        {
            activeSEDecayTimer = 0f;
            if (activeSECount > 0) activeSECount--;
        }
    }

    // ─── 再生API ──────────────────────────────────────

    public void PlaySE(string name)
    {
        if (!clips.TryGetValue(name, out var clip))
        {
            Debug.LogWarning($"[AudioManager] SE not found: {name}");
            return;
        }

        // 同一SEの最低間隔チェック
        float now = Time.unscaledTime;
        if (seLastPlayTime.TryGetValue(name, out float lastTime))
        {
            if (now - lastTime < MinSEInterval) return;
        }

        // 全SE同時再生上限チェック
        if (activeSECount >= MaxConcurrentSE) return;

        // 同時再生数に応じて音量を下げる（重なるほど静かに）
        float vol = seVolume;
        if (activeSECount > 2)
            vol *= Mathf.Lerp(1f, 0.3f, (activeSECount - 2f) / (MaxConcurrentSE - 2f));

        sfxSource.PlayOneShot(clip, vol);
        seLastPlayTime[name] = now;
        activeSECount++;
    }

    public void PlayBGM(string name, float fadeTime = 0.5f)
    {
        if (currentBgmName == name && bgmSource.isPlaying) return;

        if (!clips.TryGetValue(name, out var clip))
        {
            Debug.LogWarning($"[AudioManager] BGM not found: {name}");
            return;
        }

        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(CrossFadeBGM(clip, name, fadeTime));
    }

    public void StopBGM(float fadeTime = 0.5f)
    {
        if (!bgmSource.isPlaying) return;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = StartCoroutine(FadeOutBGM(fadeTime));
    }

    IEnumerator CrossFadeBGM(AudioClip newClip, string newName, float fadeTime)
    {
        // フェードアウト
        if (bgmSource.isPlaying)
        {
            float startVol = bgmSource.volume;
            float t = 0;
            while (t < fadeTime * 0.5f)
            {
                t += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVol, 0f, t / (fadeTime * 0.5f));
                yield return null;
            }
        }

        // 切り替え
        bgmSource.clip = newClip;
        bgmSource.volume = 0f;
        bgmSource.Play();
        currentBgmName = newName;

        // フェードイン
        float t2 = 0;
        while (t2 < fadeTime * 0.5f)
        {
            t2 += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, bgmVolume, t2 / (fadeTime * 0.5f));
            yield return null;
        }
        bgmSource.volume = bgmVolume;
        fadeCoroutine = null;
    }

    IEnumerator FadeOutBGM(float fadeTime)
    {
        float startVol = bgmSource.volume;
        float t = 0;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(startVol, 0f, t / fadeTime);
            yield return null;
        }
        bgmSource.Stop();
        currentBgmName = null;
        fadeCoroutine = null;
    }

    // ─── 差し替えAPI ─────────────────────────────────

    /// <summary>
    /// 指定した名前のクリップを外部AudioClipで差し替え。
    /// </summary>
    public void SetClip(string name, AudioClip clip)
    {
        clips[name] = clip;
    }

    public void SetSEVolume(float vol)
    {
        seVolume = Mathf.Clamp01(vol);
    }

    public void SetBGMVolume(float vol)
    {
        bgmVolume = Mathf.Clamp01(vol);
        if (bgmSource.isPlaying && fadeCoroutine == null)
            bgmSource.volume = bgmVolume;
    }
}
