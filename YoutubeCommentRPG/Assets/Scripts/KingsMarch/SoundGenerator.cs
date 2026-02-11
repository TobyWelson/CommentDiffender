using UnityEngine;

/// <summary>
/// 8bitレトロサウンドをコードで生成するユーティリティ。
/// 全SE・BGMをAudioClip.Create()で作成。差し替え時はAudioManager.clips[name]を上書き。
/// </summary>
public static class SoundGenerator
{
    const int SampleRate = 22050; // ファミコン風の低サンプルレート

    // ─── 基本波形 ─────────────────────────────────────

    static float Square(float phase, float duty = 0.5f)
    {
        return (phase % 1f) < duty ? 0.5f : -0.5f;
    }

    static float Triangle(float phase)
    {
        float t = phase % 1f;
        return t < 0.5f ? (4f * t - 1f) : (3f - 4f * t);
    }

    static float Noise()
    {
        return Random.Range(-0.5f, 0.5f);
    }

    static float Sin(float phase)
    {
        return Mathf.Sin(phase * 2f * Mathf.PI) * 0.5f;
    }

    // ─── AudioClip生成 ─────────────────────────────────

    static AudioClip MakeClip(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // ─── 音符ヘルパー ─────────────────────────────────

    // MIDIノート番号 → 周波数
    static float NoteFreq(int note)
    {
        return 440f * Mathf.Pow(2f, (note - 69) / 12f);
    }

    // C4=60, D4=62, E4=64, F4=65, G4=67, A4=69, B4=71
    // C5=72, D5=74, E5=76, F5=77, G5=79, A5=81

    static void AddTone(float[] samples, int startSample, int lengthSamples,
        float freq, float volume, System.Func<float, float> wave)
    {
        for (int i = 0; i < lengthSamples && (startSample + i) < samples.Length; i++)
        {
            float t = (float)i / SampleRate;
            float phase = freq * t;
            float env = 1f;
            // 簡易ADSR: アタック10ms, リリース最後20%
            float attackSamples = SampleRate * 0.01f;
            if (i < attackSamples) env = i / attackSamples;
            float relStart = lengthSamples * 0.8f;
            if (i > relStart) env *= 1f - (i - relStart) / (lengthSamples - relStart);
            samples[startSample + i] += wave(phase) * volume * env;
        }
    }

    static void AddNoise(float[] samples, int startSample, int lengthSamples, float volume)
    {
        for (int i = 0; i < lengthSamples && (startSample + i) < samples.Length; i++)
        {
            float env = 1f - (float)i / lengthSamples; // 減衰
            samples[startSample + i] += Noise() * volume * env;
        }
    }

    // ─── SE生成 ──────────────────────────────────────

    public static AudioClip GenSwordDraw()
    {
        // シャキーン！抜刀音: 高音の金属スライド → キラキラ残響
        int len = (int)(SampleRate * 0.45f);
        var s = new float[len];
        // フェーズ1: 金属スライド (0～0.12秒) ピッチ急上昇
        int slide = (int)(SampleRate * 0.12f);
        for (int i = 0; i < slide; i++)
        {
            float t = (float)i / slide;
            float freq = 800f + 3200f * t * t; // 800→4000Hz急上昇
            float phase = freq * (float)i / SampleRate;
            float env = 0.3f + 0.5f * t;
            s[i] += Square(phase, 0.15f) * env * 0.4f;
            s[i] += Sin(phase * 2.01f) * env * 0.2f; // 倍音
            s[i] += Noise() * 0.15f * (1f - t * 0.5f); // 摩擦ノイズ
        }
        // フェーズ2: キーン金属残響 (0.08～0.4秒)
        int ringStart = (int)(SampleRate * 0.08f);
        int ringLen = len - ringStart;
        for (int i = 0; i < ringLen && (ringStart + i) < len; i++)
        {
            float t = (float)i / ringLen;
            float env = (1f - t) * (1f - t); // 二次減衰
            float phase = (float)(ringStart + i) / SampleRate;
            s[ringStart + i] += Sin(3600f * phase) * env * 0.35f;
            s[ringStart + i] += Sin(4800f * phase) * env * 0.15f; // 高倍音
            s[ringStart + i] += Sin(2400f * phase) * env * 0.1f;  // 低倍音
        }
        // フェーズ3: キラキラ (0.05～0.25秒)
        int sparkStart = (int)(SampleRate * 0.05f);
        int sparkLen = (int)(SampleRate * 0.2f);
        for (int i = 0; i < sparkLen && (sparkStart + i) < len; i++)
        {
            float t = (float)i / sparkLen;
            float env = Mathf.Sin(t * Mathf.PI) * 0.2f;
            float phase = (float)(sparkStart + i) / SampleRate;
            s[sparkStart + i] += Triangle(5200f * phase) * env;
        }
        // クリップ防止
        for (int i = 0; i < len; i++)
            s[i] = Mathf.Clamp(s[i], -0.8f, 0.8f);
        return MakeClip("SwordDraw", s);
    }

    public static AudioClip GenSwordHit()
    {
        int len = (int)(SampleRate * 0.15f);
        var s = new float[len];
        // 短い矩形波バースト + ノイズ
        AddTone(s, 0, len / 2, 200f, 0.4f, p => Square(p, 0.25f));
        AddNoise(s, 0, len, 0.35f);
        // ピッチ下降
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / SampleRate;
            float freq = 300f - 200f * t / 0.15f;
            float phase = freq * t;
            s[i] += Square(phase, 0.25f) * 0.2f * (1f - (float)i / len);
        }
        return MakeClip("SwordHit", s);
    }

    public static AudioClip GenArrowShot()
    {
        int len = (int)(SampleRate * 0.2f);
        var s = new float[len];
        // 高音のピュン（上昇→下降）
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float freq = t < 0.3f
                ? Mathf.Lerp(800f, 1600f, t / 0.3f)
                : Mathf.Lerp(1600f, 400f, (t - 0.3f) / 0.7f);
            float phase = freq * (float)i / SampleRate;
            float env = 1f - t;
            s[i] = Square(phase, 0.25f) * 0.3f * env;
        }
        return MakeClip("ArrowShot", s);
    }

    public static AudioClip GenMagicFire()
    {
        int len = (int)(SampleRate * 0.25f);
        var s = new float[len];
        // 低音ノイズバースト（着火）
        AddNoise(s, 0, len / 3, 0.35f);
        // 中音スウィープ上昇（ファイアボール飛翔）
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float freq = Mathf.Lerp(150f, 600f, t);
            float phase = freq * (float)i / SampleRate;
            float env = (t < 0.2f) ? t / 0.2f : Mathf.Max(0f, 1f - (t - 0.2f) / 0.8f);
            s[i] += Square(phase, 0.3f) * 0.25f * env;
        }
        // 短い爆発（着弾）
        int burstStart = len * 2 / 3;
        int burstLen = len - burstStart;
        AddNoise(s, burstStart, burstLen, 0.4f);
        AddTone(s, burstStart, burstLen, 120f, 0.2f, p => Square(p, 0.25f));
        return MakeClip("MagicFire", s);
    }

    public static AudioClip GenHeal()
    {
        int len = (int)(SampleRate * 0.4f);
        var s = new float[len];
        // 上昇アルペジオ C-E-G-C
        int noteLen = len / 4;
        float[] freqs = { NoteFreq(72), NoteFreq(76), NoteFreq(79), NoteFreq(84) };
        for (int n = 0; n < 4; n++)
            AddTone(s, n * noteLen, noteLen, freqs[n], 0.3f, p => Triangle(p));
        return MakeClip("Heal", s);
    }

    public static AudioClip GenLevelUp()
    {
        int len = (int)(SampleRate * 0.6f);
        var s = new float[len];
        // ファンファーレ風: C-E-G-C(高い) の矩形波
        int noteLen = len / 5;
        float[] freqs = { NoteFreq(60), NoteFreq(64), NoteFreq(67), NoteFreq(72), NoteFreq(72) };
        for (int n = 0; n < 5; n++)
        {
            int dur = n == 4 ? noteLen * 2 : noteLen;
            AddTone(s, n * noteLen, dur, freqs[n], 0.3f, p => Square(p));
        }
        // 装飾: 三角波のハーモニー
        AddTone(s, noteLen * 3, noteLen * 2, NoteFreq(76), 0.15f, p => Triangle(p));
        return MakeClip("LevelUp", s);
    }

    public static AudioClip GenUnitDeath()
    {
        int len = (int)(SampleRate * 0.4f);
        var s = new float[len];
        // 下降音
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float freq = Mathf.Lerp(400f, 80f, t);
            float phase = freq * (float)i / SampleRate;
            s[i] = Square(phase, 0.5f) * 0.3f * (1f - t * 0.7f);
        }
        AddNoise(s, len / 2, len / 2, 0.15f);
        return MakeClip("UnitDeath", s);
    }

    public static AudioClip GenCastleHit()
    {
        int len = (int)(SampleRate * 0.25f);
        var s = new float[len];
        // 低音ドン
        AddTone(s, 0, len, 60f, 0.5f, p => Square(p));
        AddTone(s, 0, len / 2, 120f, 0.3f, p => Triangle(p));
        AddNoise(s, 0, len / 3, 0.3f);
        return MakeClip("CastleHit", s);
    }

    public static AudioClip GenCastleDestroy()
    {
        int len = (int)(SampleRate * 1.0f);
        var s = new float[len];
        // 長い爆発: ノイズ + 低音下降
        AddNoise(s, 0, len, 0.4f);
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float freq = Mathf.Lerp(100f, 30f, t);
            float phase = freq * (float)i / SampleRate;
            s[i] += Square(phase) * 0.3f * (1f - t);
        }
        return MakeClip("CastleDestroy", s);
    }

    public static AudioClip GenWaveStart()
    {
        int len = (int)(SampleRate * 0.4f);
        var s = new float[len];
        // 短いファンファーレ: G-C(高)
        int half = len / 2;
        AddTone(s, 0, half, NoteFreq(67), 0.35f, p => Square(p));
        AddTone(s, half, half, NoteFreq(72), 0.35f, p => Square(p));
        AddTone(s, half, half, NoteFreq(76), 0.2f, p => Triangle(p));
        return MakeClip("WaveStart", s);
    }

    public static AudioClip GenWaveClear()
    {
        int len = (int)(SampleRate * 0.8f);
        var s = new float[len];
        // 明るい上昇: C-E-G-C
        int noteLen = len / 4;
        float[] notes = { NoteFreq(60), NoteFreq(64), NoteFreq(67), NoteFreq(72) };
        for (int n = 0; n < 4; n++)
        {
            int dur = n == 3 ? noteLen * 2 : noteLen;
            AddTone(s, n * noteLen, dur, notes[n], 0.3f, p => Square(p));
            AddTone(s, n * noteLen, dur, notes[n] * 2f, 0.1f, p => Triangle(p));
        }
        return MakeClip("WaveClear", s);
    }

    public static AudioClip GenVictory()
    {
        int len = (int)(SampleRate * 1.5f);
        var s = new float[len];
        // 勝利ファンファーレ: C-C-C-E---G---C(高)---
        int unit = len / 10;
        AddTone(s, 0, unit, NoteFreq(60), 0.3f, p => Square(p));
        AddTone(s, unit, unit, NoteFreq(60), 0.3f, p => Square(p));
        AddTone(s, unit * 2, unit, NoteFreq(60), 0.3f, p => Square(p));
        AddTone(s, unit * 3, unit * 2, NoteFreq(64), 0.35f, p => Square(p));
        AddTone(s, unit * 5, unit * 2, NoteFreq(67), 0.35f, p => Square(p));
        AddTone(s, unit * 7, unit * 3, NoteFreq(72), 0.4f, p => Square(p));
        // ハーモニー
        AddTone(s, unit * 7, unit * 3, NoteFreq(76), 0.2f, p => Triangle(p));
        AddTone(s, unit * 7, unit * 3, NoteFreq(67), 0.15f, p => Triangle(p));
        return MakeClip("Victory", s);
    }

    public static AudioClip GenGameOver()
    {
        int len = (int)(SampleRate * 1.2f);
        var s = new float[len];
        // 下降メロディ: E-D-C-B(低)-A(低)
        int noteLen = len / 5;
        float[] notes = { NoteFreq(64), NoteFreq(62), NoteFreq(60), NoteFreq(59), NoteFreq(57) };
        for (int n = 0; n < 5; n++)
        {
            int dur = n == 4 ? noteLen * 2 : noteLen;
            AddTone(s, n * noteLen, dur, notes[n], 0.3f, p => Square(p, 0.25f));
            AddTone(s, n * noteLen, dur, notes[n] / 2f, 0.15f, p => Triangle(p));
        }
        return MakeClip("GameOver", s);
    }

    public static AudioClip GenUnitPlace()
    {
        int len = (int)(SampleRate * 0.08f);
        var s = new float[len];
        AddTone(s, 0, len, 600f, 0.3f, p => Square(p, 0.25f));
        return MakeClip("UnitPlace", s);
    }

    public static AudioClip GenUnitPickup()
    {
        int len = (int)(SampleRate * 0.1f);
        var s = new float[len];
        // ヒュッ 上昇
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float freq = Mathf.Lerp(400f, 1200f, t);
            s[i] = Square(freq * (float)i / SampleRate, 0.25f) * 0.25f * (1f - t * 0.5f);
        }
        return MakeClip("UnitPickup", s);
    }

    public static AudioClip GenButtonClick()
    {
        int len = (int)(SampleRate * 0.05f);
        var s = new float[len];
        AddTone(s, 0, len, 1000f, 0.25f, p => Square(p, 0.5f));
        return MakeClip("ButtonClick", s);
    }

    public static AudioClip GenQueueAdd()
    {
        int len = (int)(SampleRate * 0.15f);
        var s = new float[len];
        // チャリン: 高音2音
        int half = len / 2;
        AddTone(s, 0, half, NoteFreq(76), 0.25f, p => Square(p, 0.25f));
        AddTone(s, half, half, NoteFreq(79), 0.25f, p => Square(p, 0.25f));
        return MakeClip("QueueAdd", s);
    }

    public static AudioClip GenWarning()
    {
        int len = (int)(SampleRate * 0.6f);
        var s = new float[len];
        // アラーム: 2音交互
        int cycle = SampleRate / 6; // 6Hzで交互
        for (int i = 0; i < len; i++)
        {
            float freq = ((i / cycle) % 2 == 0) ? 800f : 600f;
            float phase = freq * (float)i / SampleRate;
            float env = 1f - (float)i / len * 0.3f;
            s[i] = Square(phase, 0.5f) * 0.3f * env;
        }
        return MakeClip("Warning", s);
    }

    public static AudioClip GenCoinGet()
    {
        int len = (int)(SampleRate * 0.12f);
        var s = new float[len];
        // 短いチャリン: 金属高音2音
        int half = len / 2;
        AddTone(s, 0, half, NoteFreq(84), 0.3f, p => Square(p, 0.125f));
        AddTone(s, half, half, NoteFreq(88), 0.3f, p => Square(p, 0.125f));
        AddTone(s, 0, len, NoteFreq(96), 0.08f, p => Sin(p));
        return MakeClip("CoinGet", s);
    }

    // ─── YouTube Action SE ──────────────────────────────

    public static AudioClip GenSuperChat()
    {
        int len = (int)(SampleRate * 0.7f);
        var s = new float[len];
        // キラキラ上昇アルペジオ: C-E-G-B-C(高い)-E(高い)
        int noteLen = len / 6;
        float[] freqs = { NoteFreq(72), NoteFreq(76), NoteFreq(79), NoteFreq(83), NoteFreq(84), NoteFreq(88) };
        for (int n = 0; n < 6; n++)
        {
            int dur = n == 5 ? noteLen * 2 : noteLen;
            AddTone(s, n * noteLen, dur, freqs[n], 0.25f, p => Square(p, 0.25f));
            AddTone(s, n * noteLen, dur, freqs[n] * 2f, 0.1f, p => Triangle(p)); // キラキラオクターブ上
        }
        // コインチャリン装飾
        AddTone(s, 0, len / 4, NoteFreq(96), 0.08f, p => Square(p, 0.125f));
        return MakeClip("SuperChat", s);
    }

    public static AudioClip GenNewMember()
    {
        int len = (int)(SampleRate * 0.5f);
        var s = new float[len];
        // 歓迎ファンファーレ: G-B-D(高)-G(高)
        int noteLen = len / 4;
        float[] freqs = { NoteFreq(67), NoteFreq(71), NoteFreq(74), NoteFreq(79) };
        for (int n = 0; n < 4; n++)
        {
            int dur = n == 3 ? noteLen * 2 : noteLen;
            AddTone(s, n * noteLen, dur, freqs[n], 0.3f, p => Triangle(p));
            AddTone(s, n * noteLen, dur, freqs[n], 0.15f, p => Square(p));
        }
        return MakeClip("NewMember", s);
    }

    public static AudioClip GenLikeMilestone()
    {
        int len = (int)(SampleRate * 0.5f);
        var s = new float[len];
        // ハートチャイム: 明るいベル風2音
        int half = len / 2;
        AddTone(s, 0, half, NoteFreq(79), 0.3f, p => Sin(p)); // ベル風サイン波
        AddTone(s, 0, half, NoteFreq(84), 0.15f, p => Sin(p));
        AddTone(s, half, half, NoteFreq(84), 0.3f, p => Sin(p));
        AddTone(s, half, half, NoteFreq(88), 0.15f, p => Sin(p));
        // 装飾キラキラ
        AddTone(s, 0, len, NoteFreq(91), 0.06f, p => Square(p, 0.125f));
        return MakeClip("LikeMilestone", s);
    }

    public static AudioClip GenBossSmash()
    {
        int len = (int)(SampleRate * 0.3f);
        var s = new float[len];
        // 重い低音ドン + 衝撃ノイズ + ピッチ下降
        AddTone(s, 0, len, 50f, 0.55f, p => Square(p));
        AddTone(s, 0, len / 2, 100f, 0.35f, p => Triangle(p));
        AddNoise(s, 0, len / 2, 0.4f);
        for (int i = 0; i < len; i++)
        {
            float t = (float)i / len;
            float freq = 80f - 50f * t;
            float phase = freq * ((float)i / SampleRate);
            s[i] += Square(phase, 0.3f) * 0.25f * (1f - t);
        }
        return MakeClip("BossSmash", s);
    }

    // ─── BGM生成 ──────────────────────────────────────

    public static AudioClip GenBgmPreparation()
    {
        // 準備フェーズ: 穏やかな異世界の村 G major 95BPM 8小節 (16bit SFC風)
        float bpm = 95f;
        float beatLen = 60f / bpm;
        int totalBeats = 32;
        float duration = beatLen * totalBeats;
        int len = (int)(SampleRate * duration);
        var s = new float[len];

        int noteDur = (int)(beatLen * SampleRate * 0.85f);

        // ─── メロディ（三角波 温かい音色）──────
        // G major: G4=67, A4=69, B4=71, C5=72, D5=74, E5=76
        int[] melody = {
            67, -1, 71, -1,   74, -1, 72, 71,   // G4 . B4 . | D5 . C5 B4
            64, -1, 67, -1,   69, -1, -1, -1,   // E4 . G4 . | A4 (whole)
            71, -1, 74, -1,   76, -1, 74, 72,   // B4 . D5 . | E5 . D5 C5
            71, 69, 67, 69,   67, -1, -1, -1    // B4 A4 G4 A4 | G4 (whole)
        };
        for (int i = 0; i < melody.Length; i++)
        {
            if (melody[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, noteDur, NoteFreq(melody[i]), 0.18f, p => Triangle(p));
        }

        // ─── カウンターメロディ（矩形波25%）──────
        int[] counter = {
            59, -1, 62, -1,   67, -1, 64, 62,
            60, -1, 62, -1,   64, -1, -1, -1,
            62, -1, 67, -1,   72, -1, 71, 69,
            67, 64, 62, 64,   59, -1, -1, -1
        };
        for (int i = 0; i < counter.Length; i++)
        {
            if (counter[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, noteDur, NoteFreq(counter[i]), 0.10f, p => Square(p, 0.25f));
        }

        // ─── ストリングスパッド（三角波 持続コード）──────
        // G, G, Em, D, G, Am, C, D
        int barLen = (int)(beatLen * SampleRate * 4);
        int[][] pads = {
            new[]{55, 59, 62}, new[]{55, 59, 62},   // Gmaj
            new[]{52, 55, 59}, new[]{50, 54, 57},   // Em, D
            new[]{55, 59, 62}, new[]{57, 60, 64},   // G, Am
            new[]{48, 52, 55}, new[]{50, 54, 57}    // C, D
        };
        for (int bar = 0; bar < 8; bar++)
        {
            int start = bar * barLen;
            foreach (int note in pads[bar])
                AddTone(s, start, barLen, NoteFreq(note), 0.06f, p => Sin(p));
        }

        // ─── ベース（三角波）──────
        int[] bass = {
            43, -1, 43, -1,   43, -1, 48, -1,   // G2, G2, G2, C3
            40, -1, 40, -1,   50, -1, 50, -1,   // E2, E2, D3, D3
            43, -1, 43, -1,   45, -1, 45, -1,   // G2, A2, A2
            36, -1, 36, -1,   38, -1, 43, -1    // C2, D2, G2
        };
        for (int i = 0; i < bass.Length; i++)
        {
            if (bass[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, (int)(beatLen * SampleRate * 0.7f),
                NoteFreq(bass[i]), 0.14f, p => Triangle(p));
        }

        // ─── ハープアルペジオ（サイン波 16分音符）──────
        int sixteenthDur = (int)(beatLen * SampleRate / 4f);
        int sixteenthNoteDur = (int)(sixteenthDur * 0.6f);
        int[][] arpNotes = {
            new[]{55, 59, 62, 67}, new[]{55, 59, 62, 67},
            new[]{52, 55, 59, 64}, new[]{50, 54, 57, 62},
            new[]{55, 59, 62, 67}, new[]{57, 60, 64, 69},
            new[]{48, 52, 55, 60}, new[]{50, 54, 57, 62}
        };
        for (int bar = 0; bar < 8; bar++)
        {
            for (int beat = 0; beat < 4; beat++)
            {
                for (int six = 0; six < 4; six++)
                {
                    int idx = bar * 16 + beat * 4 + six;
                    int start = idx * sixteenthDur;
                    int noteIdx = six % arpNotes[bar].Length;
                    AddTone(s, start, sixteenthNoteDur,
                        NoteFreq(arpNotes[bar][noteIdx] + 12), 0.04f, p => Sin(p));
                }
            }
        }

        // ─── ドラム（軽め）──────
        for (int i = 0; i < totalBeats; i++)
        {
            int start = (int)(i * beatLen * SampleRate);
            if (i % 4 == 0)
            {
                AddNoise(s, start, (int)(SampleRate * 0.02f), 0.08f);
                AddTone(s, start, (int)(SampleRate * 0.04f), 55f, 0.08f, p => Triangle(p));
            }
            else if (i % 4 == 2)
                AddNoise(s, start, (int)(SampleRate * 0.025f), 0.05f);
            else if (i % 2 == 0)
                AddNoise(s, start, (int)(SampleRate * 0.008f), 0.025f);
        }

        // ─── エコー（SFC風）──────
        int delaySamples = SampleRate * 200 / 1000;
        for (int i = delaySamples; i < len; i++)
            s[i] += s[i - delaySamples] * 0.20f;

        Normalize(s, 0.40f);
        return MakeClip("BgmPreparation", s);
    }

    public static AudioClip GenBgmBattle()
    {
        // 戦闘: 激烈バトル Dm 165BPM 8小節 (16bit SFC風 FF・ロマサガ系)
        float bpm = 165f;
        float beatLen = 60f / bpm;
        int totalBeats = 32;
        float duration = beatLen * totalBeats;
        int len = (int)(SampleRate * duration);
        var s = new float[len];

        int noteDur = (int)(beatLen * SampleRate * 0.7f);
        int barLen = (int)(beatLen * SampleRate * 4);
        float eighthLen = beatLen / 2f;
        float sixteenthLen = beatLen / 4f;

        // ─── メロディ（矩形波50% 激しくうねるリード）──────
        // Dm: D5=74, E5=76, F5=77, G5=79, A5=81, Bb5=82, C6=84, D6=86
        int[] melody = {
            74, 77, 81, 82,   84, 82, 81, 77,   // D5 F5 A5 Bb5 | C6 Bb5 A5 F5 (上昇→下降)
            79, 81, 84, 86,   84, -1, 81, 79,   // G5 A5 C6 D6 | C6 . A5 G5  (高音突破)
            77, 74, 77, 81,   82, 84, 86, 84,   // F5 D5 F5 A5 | Bb5 C6 D6 C6 (再上昇)
            82, 81, 79, 77,   74, 77, 74, -1    // Bb5 A5 G5 F5 | D5 F5 D5 . (叩きつけ)
        };
        for (int i = 0; i < melody.Length; i++)
        {
            if (melody[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, noteDur, NoteFreq(melody[i]), 0.22f, p => Square(p, 0.5f));
        }

        // ─── カウンターメロディ（矩形波25% オクターブ下）──────
        int[] counter = {
            62, 65, 69, 70,   72, 70, 69, 65,
            67, 69, 72, 74,   72, -1, 69, 67,
            65, 62, 65, 69,   70, 72, 74, 72,
            70, 69, 67, 65,   62, 65, 62, -1
        };
        for (int i = 0; i < counter.Length; i++)
        {
            if (counter[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, noteDur, NoteFreq(counter[i]), 0.12f, p => Square(p, 0.25f));
        }

        // ─── パワーコード（矩形波50% 強スタブ + 裏拍）──────
        // Dm, C, Bb, A, Dm, F, Gm, A
        int[][] chords = {
            new[]{50, 53, 57}, new[]{48, 52, 55},
            new[]{46, 50, 53}, new[]{45, 49, 52},
            new[]{50, 53, 57}, new[]{53, 57, 60},
            new[]{43, 46, 50}, new[]{45, 49, 52}
        };
        for (int bar = 0; bar < 8; bar++)
        {
            int bStart = bar * barLen;
            int stabDur = (int)(beatLen * SampleRate * 0.3f);
            // 1拍目：強
            foreach (int n in chords[bar])
                AddTone(s, bStart, stabDur, NoteFreq(n), 0.09f, p => Square(p, 0.5f));
            // 裏拍（8分ごと）：2拍目裏と4拍目裏
            int off2 = bStart + (int)(beatLen * SampleRate * 1.5f);
            int off4 = bStart + (int)(beatLen * SampleRate * 3.5f);
            foreach (int n in chords[bar])
            {
                AddTone(s, off2, stabDur, NoteFreq(n), 0.06f, p => Square(p, 0.5f));
                AddTone(s, off4, stabDur, NoteFreq(n), 0.06f, p => Square(p, 0.5f));
            }
        }

        // ─── ベース（三角波 16分音符で駆け回る）──────
        int[] bassNotes = {
            38, 38, 45, 50,   50, 45, 38, 38,   // D2 D2 A2 D3 | D3 A2 D2 D2
            36, 36, 43, 48,   48, 43, 36, 36,   // C2 C2 G2 C3 | C3 G2 C2 C2
            34, 34, 41, 46,   46, 41, 34, 34,   // Bb1 Bb1 F2 Bb2
            33, 33, 40, 45,   45, 40, 33, 33    // A1 A1 E2 A2
        };
        // 2ループ分（小節1-4と5-8で別パターン）
        int[] bass2 = {
            38, 50, 38, 50,   53, 50, 45, 38,   // D2 D3 D2 D3 高速オクターブ
            41, 53, 41, 53,   48, 53, 48, 41,   // F2 F3
            31, 43, 31, 43,   38, 43, 38, 31,   // G1 G2
            33, 45, 33, 45,   40, 45, 40, 33    // A1 A2
        };
        for (int i = 0; i < 32; i++)
        {
            int note = i < 32 ? (i < 16 ? bassNotes[i] : bass2[i - 16]) : bassNotes[i % 16];
            if (note < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, (int)(beatLen * SampleRate * 0.45f),
                NoteFreq(note), 0.19f, p => Triangle(p));
        }

        // ─── 16分アルペジオ（矩形波12.5% 超高速 煌めき）──────
        int[][] arpChords = {
            new[]{62, 65, 69, 74}, new[]{60, 64, 67, 72},
            new[]{58, 62, 65, 70}, new[]{57, 61, 64, 69},
            new[]{62, 65, 69, 74}, new[]{65, 69, 72, 77},
            new[]{55, 58, 62, 67}, new[]{57, 61, 64, 69}
        };
        int sixteenthDur = (int)(sixteenthLen * SampleRate * 0.6f);
        for (int bar = 0; bar < 8; bar++)
        {
            for (int s16 = 0; s16 < 16; s16++)
            {
                int idx = bar * 16 + s16;
                int start = (int)(idx * sixteenthLen * SampleRate);
                int noteIdx = s16 % arpChords[bar].Length;
                AddTone(s, start, sixteenthDur,
                    NoteFreq(arpChords[bar][noteIdx]), 0.05f, p => Square(p, 0.125f));
            }
        }

        // ─── ドラム（16ビート ハードコア）──────
        int totalSixteenths = totalBeats * 4;
        for (int i = 0; i < totalSixteenths; i++)
        {
            int start = (int)(i * sixteenthLen * SampleRate);
            int beat16 = i % 16; // 16分音符の位置（1小節=16）
            if (beat16 == 0 || beat16 == 6 || beat16 == 10) // キック (1, &3, &)
            {
                AddNoise(s, start, (int)(SampleRate * 0.03f), 0.16f);
                AddTone(s, start, (int)(SampleRate * 0.05f), 55f, 0.18f, p => Triangle(p));
            }
            else if (beat16 == 4 || beat16 == 12) // スネア (2, 4)
            {
                AddNoise(s, start, (int)(SampleRate * 0.04f), 0.14f);
                AddTone(s, start, (int)(SampleRate * 0.02f), 200f, 0.06f, p => Triangle(p));
            }
            else if (beat16 % 2 == 0) // オープンハイハット
                AddNoise(s, start, (int)(SampleRate * 0.018f), 0.05f);
            else // クローズドハイハット
                AddNoise(s, start, (int)(SampleRate * 0.008f), 0.03f);
        }
        // クラッシュ（2小節ごと）+ フィルイン
        for (int bar = 0; bar < 8; bar += 2)
        {
            int start = bar * barLen;
            AddNoise(s, start, (int)(SampleRate * 0.2f), 0.10f);
        }
        // 4小節目、8小節目にドラムフィル（16分連打）
        for (int fill = 0; fill < 2; fill++)
        {
            int fillBar = (fill == 0) ? 3 : 7;
            int fillStart = fillBar * barLen + (int)(beatLen * SampleRate * 3); // 4拍目
            for (int f = 0; f < 4; f++)
            {
                int fStart = fillStart + (int)(f * sixteenthLen * SampleRate);
                AddNoise(s, fStart, (int)(SampleRate * 0.03f), 0.12f);
                AddTone(s, fStart, (int)(SampleRate * 0.02f), 180f + f * 40f, 0.06f, p => Triangle(p));
            }
        }

        // ─── エコー（短め 緊迫感）──────
        int delaySamples = SampleRate * 100 / 1000;
        for (int i = delaySamples; i < len; i++)
            s[i] += s[i - delaySamples] * 0.15f;

        Normalize(s, 0.48f);
        return MakeClip("BgmBattle", s);
    }

    public static AudioClip GenBgmResult()
    {
        // 結果画面: 余韻・振り返り F major 78BPM 8小節 (16bit SFC風)
        float bpm = 78f;
        float beatLen = 60f / bpm;
        int totalBeats = 32;
        float duration = beatLen * totalBeats;
        int len = (int)(SampleRate * duration);
        var s = new float[len];

        int noteDur = (int)(beatLen * SampleRate * 0.9f);
        int longDur = (int)(beatLen * SampleRate * 1.8f);

        // ─── メロディ（三角波 感情的・ゆったり）──────
        // F major: F4=65, G4=67, A4=69, Bb4=70, C5=72, D5=74, E5=76, F5=77
        int[] melody = {
            65, -1, -1, 69,   72, -1, -1, 70,   // F4 . . A4 | C5 . . Bb4
            69, -1, 67, 65,   67, -1, -1, -1,   // A4 . G4 F4 | G4 (whole)
            72, -1, -1, 74,   77, -1, -1, 76,   // C5 . . D5 | F5 . . E5
            74, -1, 72, 69,   65, -1, -1, -1    // D5 . C5 A4 | F4 (whole)
        };
        for (int i = 0; i < melody.Length; i++)
        {
            if (melody[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            int dur = (melody.Length > i + 1 && melody[i + 1] < 0) ? longDur : noteDur;
            AddTone(s, start, dur, NoteFreq(melody[i]), 0.19f, p => Triangle(p));
        }

        // ─── カウンターメロディ（矩形波25%）──────
        int[] counter = {
            60, -1, -1, 64,   65, -1, -1, 62,
            64, -1, 60, 58,   60, -1, -1, -1,
            65, -1, -1, 67,   69, -1, -1, 72,
            70, -1, 65, 64,   60, -1, -1, -1
        };
        for (int i = 0; i < counter.Length; i++)
        {
            if (counter[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, noteDur, NoteFreq(counter[i]), 0.09f, p => Square(p, 0.25f));
        }

        // ─── ストリングスパッド（サイン波 温かいコード）──────
        // F, Dm, Bb, C, F, Am, Dm, F
        int barLen = (int)(beatLen * SampleRate * 4);
        int[][] pads = {
            new[]{53, 57, 60}, new[]{50, 53, 57},   // F, Dm
            new[]{58, 62, 65}, new[]{48, 52, 55},   // Bb, C
            new[]{53, 57, 60}, new[]{57, 60, 64},   // F, Am
            new[]{50, 53, 57}, new[]{53, 57, 60}    // Dm, F
        };
        for (int bar = 0; bar < 8; bar++)
        {
            int start = bar * barLen;
            foreach (int note in pads[bar])
                AddTone(s, start, barLen, NoteFreq(note), 0.07f, p => Sin(p));
        }

        // ─── ベース（三角波 ゆったり）──────
        int[] bass = {
            41, -1, 41, -1,   50, -1, 50, -1,   // F2, D3
            46, -1, 46, -1,   48, -1, 48, -1,   // Bb2, C3
            41, -1, 41, -1,   45, -1, 45, -1,   // F2, A2
            50, -1, 48, -1,   41, -1, 41, -1    // D3, C3, F2
        };
        for (int i = 0; i < bass.Length; i++)
        {
            if (bass[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, (int)(beatLen * SampleRate * 1.5f),
                NoteFreq(bass[i]), 0.13f, p => Triangle(p));
        }

        // ─── ハープアルペジオ（サイン波 ゆったり8分音符）──────
        float eighthLen = beatLen / 2f;
        int eighthNoteDur = (int)(eighthLen * SampleRate * 0.7f);
        int[][] arpNotes = {
            new[]{53, 57, 60, 65}, new[]{50, 53, 57, 62},
            new[]{58, 62, 65, 70}, new[]{48, 52, 55, 60},
            new[]{53, 57, 60, 65}, new[]{57, 60, 64, 69},
            new[]{50, 53, 57, 62}, new[]{53, 57, 60, 65}
        };
        for (int bar = 0; bar < 8; bar++)
        {
            for (int eighth = 0; eighth < 8; eighth++)
            {
                int idx = bar * 8 + eighth;
                int start = (int)(idx * eighthLen * SampleRate);
                int noteIdx = eighth % arpNotes[bar].Length;
                AddTone(s, start, eighthNoteDur,
                    NoteFreq(arpNotes[bar][noteIdx] + 12), 0.04f, p => Sin(p));
            }
        }

        // ─── ドラム（最小限）──────
        for (int i = 0; i < totalBeats; i++)
        {
            int start = (int)(i * beatLen * SampleRate);
            if (i % 4 == 0)
                AddNoise(s, start, (int)(SampleRate * 0.02f), 0.05f);
            else if (i % 4 == 2)
                AddNoise(s, start, (int)(SampleRate * 0.015f), 0.03f);
        }

        // ─── エコー（長め 残響感）──────
        int delaySamples = SampleRate * 220 / 1000;
        for (int i = delaySamples; i < len; i++)
            s[i] += s[i - delaySamples] * 0.25f;

        Normalize(s, 0.38f);
        return MakeClip("BgmResult", s);
    }

    public static AudioClip GenBgmTitle()
    {
        // 異世界冒険風タイトルBGM 8小節 テンポ108BPM (16bit SFC風)
        float bpm = 108f;
        float beatLen = 60f / bpm;
        int totalBeats = 32; // 8小節
        float duration = beatLen * totalBeats;
        int len = (int)(SampleRate * duration);
        var s = new float[len];

        int noteDur = (int)(beatLen * SampleRate * 0.85f);

        // ─── メロディ（矩形波50% 英雄的テーマ D major）──────
        // D5=74, E5=76, F#5=78, G5=79, A5=81, B5=83, D6=86
        int[] melody = {
            74, -1, -1, 78,   81, -1, 79, 78,   // Bar 1-2: D5 . . F#5 | A5 . G5 F#5
            76, -1, 79, -1,   78, -1, -1, -1,   // Bar 3-4: E5 . G5 .  | F#5 (whole)
            81, -1, 83, 81,   86, -1, -1, 83,   // Bar 5-6: A5 . B5 A5 | D6 . . B5
            81, 79, 78, 76,   74, -1, -1, -1    // Bar 7-8: A5 G5 F#5 E5 | D5 (whole)
        };
        for (int i = 0; i < melody.Length; i++)
        {
            if (melody[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            int dur = (melody.Length > i + 1 && melody[i + 1] < 0)
                ? (int)(beatLen * SampleRate * 1.7f) : noteDur;
            AddTone(s, start, dur, NoteFreq(melody[i]), 0.20f, p => Square(p, 0.5f));
        }

        // ─── カウンターメロディ（矩形波25% ハーモニー）──────
        int[] counter = {
            66, -1, -1, 69,   74, -1, 71, 69,   // F#4 . . A4 | D5 . B4 A4
            67, -1, 71, -1,   69, -1, -1, -1,   // G4 . B4 .  | A4
            73, -1, 74, 73,   78, -1, -1, 74,   // C#5 . D5 C#5 | F#5 . . D5
            73, 71, 69, 67,   66, -1, -1, -1    // C#5 B4 A4 G4 | F#4
        };
        for (int i = 0; i < counter.Length; i++)
        {
            if (counter[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, noteDur, NoteFreq(counter[i]), 0.11f, p => Square(p, 0.25f));
        }

        // ─── ストリングスパッド（三角波 持続コード）──────
        // Bar: Dmaj, Dmaj, Bm, Dmaj, G, A, G, Dmaj
        int barLen = (int)(beatLen * SampleRate * 4);
        int[][] pads = {
            new[]{62, 66, 69}, new[]{62, 66, 69},   // Dmaj
            new[]{59, 62, 66}, new[]{62, 66, 69},   // Bm → Dmaj
            new[]{55, 59, 62}, new[]{57, 61, 64},   // G → A
            new[]{55, 59, 62}, new[]{62, 66, 69}    // G → Dmaj
        };
        for (int bar = 0; bar < 8; bar++)
        {
            int start = bar * barLen;
            foreach (int note in pads[bar])
                AddTone(s, start, barLen, NoteFreq(note), 0.07f, p => Triangle(p));
        }

        // ─── ベース（三角波）──────
        int[] bass = {
            50, -1, 50, -1,   50, -1, 55, -1,   // D3, D3, D3, G3
            47, -1, 47, -1,   54, -1, 50, -1,   // B2, B2, F#3, D3
            43, -1, 43, -1,   45, -1, 45, -1,   // G2, G2, A2, A2
            43, -1, 54, -1,   50, -1, 50, -1    // G2, F#3, D3, D3
        };
        for (int i = 0; i < bass.Length; i++)
        {
            if (bass[i] < 0) continue;
            int start = (int)(i * beatLen * SampleRate);
            AddTone(s, start, (int)(beatLen * SampleRate * 0.7f),
                NoteFreq(bass[i]), 0.16f, p => Triangle(p));
        }

        // ─── ハープアルペジオ（サイン波 16分音符）──────
        int sixteenthDur = (int)(beatLen * SampleRate / 4f);
        int sixteenthNoteDur = (int)(sixteenthDur * 0.65f);
        int[][] arpNotes = {
            new[]{62, 66, 69, 74}, new[]{62, 66, 69, 74},   // Dmaj
            new[]{59, 62, 66, 71}, new[]{62, 66, 69, 74},   // Bm, Dmaj
            new[]{55, 59, 62, 67}, new[]{57, 61, 64, 69},   // G, A
            new[]{55, 59, 62, 67}, new[]{62, 66, 69, 74}    // G, Dmaj
        };
        for (int bar = 0; bar < 8; bar++)
        {
            for (int beat = 0; beat < 4; beat++)
            {
                for (int six = 0; six < 4; six++)
                {
                    int idx = bar * 16 + beat * 4 + six;
                    int start = idx * sixteenthDur;
                    int noteIdx = six % arpNotes[bar].Length;
                    // +12でオクターブ上
                    AddTone(s, start, sixteenthNoteDur,
                        NoteFreq(arpNotes[bar][noteIdx] + 12), 0.05f, p => Sin(p));
                }
            }
        }

        // ─── ドラム（ノイズ 8ビート）──────
        float eighthLen = beatLen / 2f;
        int totalEighths = totalBeats * 2;
        for (int i = 0; i < totalEighths; i++)
        {
            int start = (int)(i * eighthLen * SampleRate);
            if (i % 4 == 0) // キック
            {
                AddNoise(s, start, (int)(SampleRate * 0.025f), 0.10f);
                AddTone(s, start, (int)(SampleRate * 0.05f), 55f, 0.10f, p => Triangle(p));
            }
            else if (i % 4 == 2) // スネア
                AddNoise(s, start, (int)(SampleRate * 0.03f), 0.07f);
            else // ハイハット
                AddNoise(s, start, (int)(SampleRate * 0.01f), 0.03f);
        }

        // ─── エコー（SFC風リバーブ）──────
        int delaySamples = SampleRate * 160 / 1000; // 160ms
        for (int i = delaySamples; i < len; i++)
            s[i] += s[i - delaySamples] * 0.22f;

        Normalize(s, 0.42f);
        return MakeClip("BgmTitle", s);
    }

    // ─── ユーティリティ ──────────────────────────────

    static void Normalize(float[] samples, float targetPeak)
    {
        float maxAbs = 0f;
        for (int i = 0; i < samples.Length; i++)
            if (Mathf.Abs(samples[i]) > maxAbs) maxAbs = Mathf.Abs(samples[i]);
        if (maxAbs < 0.001f) return;
        float scale = targetPeak / maxAbs;
        for (int i = 0; i < samples.Length; i++)
            samples[i] *= scale;
    }
}
