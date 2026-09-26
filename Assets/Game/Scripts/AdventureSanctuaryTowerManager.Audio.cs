using System.Collections;
using UnityEngine;

/// <summary>
/// 聖域の塔（Sanctuary Tower）のオーディオ関連処理（効果音合成・天蓋風音・祝祭チャイム・音量フェード）
/// </summary>
public partial class AdventureSanctuaryTowerManager
{
    static AudioClip _gameClearChimeClip;
    static AudioClip _cachedColdWindClip;
    static AudioClip _cachedClankClip;
    static AudioClip _cachedHeavyLeverClip;

    void SetupAudio()
    {
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.spatialBlend = 0.5f;
        _audio.minDistance = 6f;
        _audio.maxDistance = 50f;
    }

    IEnumerator FadeAudioSource(AudioSource src, float targetVol, float duration)
    {
        if (src == null) yield break;
        float start = src.volume;
        float t = 0f;
        while (t < duration && src != null)
        {
            t += Time.unscaledDeltaTime;
            src.volume = Mathf.Lerp(start, targetVol, t / duration);
            yield return null;
        }
        if (src != null) src.volume = targetVol;
    }

    static AudioClip LoadOrSynthesizeSkybreakWindClip()
    {
        var fromRes = Resources.Load<AudioClip>("skywind_1");
        if (fromRes != null) return fromRes;

#if UNITY_EDITOR
        var fromEditor = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/AudioFiles/03_amb/skywind_1.wav");
        if (fromEditor != null) return fromEditor;
#endif

        if (_cachedColdWindClip == null)
            _cachedColdWindClip = SynthesizeColdWindClip();
        return _cachedColdWindClip;
    }

    static AudioClip SynthesizeColdWindClip()
    {
        const int rate = 22050;
        int count = rate * 4;
        float[] data = new float[count];
        float lp = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)rate;
            float noise = (Random.value * 2f - 1f);
            lp = Mathf.Lerp(lp, noise, 0.08f);
            float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.7f) * Mathf.Sin(t * 1.3f + 0.4f);
            float low = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.12f;
            float env = 1f;
            if (i < rate / 5) env = i / (rate / 5f);
            else if (i > count - rate / 5) env = (count - i) / (rate / 5f);
            data[i] = (lp * 0.72f + low) * gust * env * 0.35f;
        }
        var clip = AudioClip.Create("SkybreakColdWind", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeClankSound()
    {
        if (_cachedClankClip != null) return _cachedClankClip;
        int rate = 22050;
        int count = rate / 4;
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 180f * t) * Mathf.Exp(-t * 18f);
        }
        var clip = AudioClip.Create("Clank", count, 1, rate, false);
        clip.SetData(d, 0);
        _cachedClankClip = clip;
        return clip;
    }

    static AudioClip MakeHeavyLeverSound()
    {
        if (_cachedHeavyLeverClip != null) return _cachedHeavyLeverClip;
        int rate = 22050;
        int count = (int)(rate * 0.65f);
        float[] d = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float snap = Mathf.Sin(2f * Mathf.PI * 90f * t) * Mathf.Exp(-t * 6f);
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 14f) * 0.4f;
            d[i] = snap + noise;
        }
        var clip = AudioClip.Create("HeavyLever", count, 1, rate, false);
        clip.SetData(d, 0);
        _cachedHeavyLeverClip = clip;
        return clip;
    }

    /// <summary>
    /// ゲームクリアボードが表示された瞬間に鳴る、感動的で壮大な祝祭のクリスタルベル・チャイムを再生
    /// </summary>
    public void PlayGameClearTriumphChime()
    {
        if (_gameClearChimeClip == null)
            _gameClearChimeClip = SynthesizeGameClearTriumphChime();

        if (_audio == null)
            _audio = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        if (_audio != null && _gameClearChimeClip != null)
        {
            _audio.spatialBlend = 0f; // 2Dステレオ空間
            _audio.PlayOneShot(_gameClearChimeClip, 0.90f);
        }
    }

    /// <summary>
    /// ゲームクリアボード表示時に鳴る、壮大で心に染み渡る祝祭のクリスタルベル・チャイム。
    /// Cメジャー7th〜9thの上行アルペジオ（大空への羽ばたき）＋天上の広大な天空リバーブ。
    /// </summary>
    static AudioClip SynthesizeGameClearTriumphChime()
    {
        const int rate = 44100;
        // C5(523Hz), E5(659Hz), G5(784Hz), B5(988Hz), C6(1046Hz), E6(1318Hz), G6(1568Hz), C7(2093Hz)
        float[] notes = { 523.25f, 659.25f, 783.99f, 987.77f, 1046.50f, 1318.51f, 1567.98f, 2093.00f };
        float[] delays = { 0.0f, 0.075f, 0.150f, 0.225f, 0.300f, 0.380f, 0.460f, 0.540f };
        float duration = 4.6f;
        int totalSamples = (int)(rate * duration);

        float[] dryL = new float[totalSamples];
        float[] dryR = new float[totalSamples];

        for (int idx = 0; idx < notes.Length; idx++)
        {
            float freq = notes[idx];
            float startT = delays[idx];
            int startIdx = (int)(startT * rate);
            float noteDur = 2.4f + idx * 0.18f;
            int noteSamples = (int)(noteDur * rate);

            // ステレオパン（低音は中央寄り、高音になるにつれて左右へ美しく広がる）
            float panVal = ((idx % 2 * 2 - 1) * 0.38f) * (0.4f + 0.6f * (float)idx / notes.Length);
            float gainL = Mathf.Cos((panVal + 1.0f) * 0.25f * Mathf.PI);
            float gainR = Mathf.Sin((panVal + 1.0f) * 0.25f * Mathf.PI);

            float detuneL = 1.0f - 0.0006f;
            float detuneR = 1.0f + 0.0006f;

            for (int i = 0; i < noteSamples; i++)
            {
                int destIdx = startIdx + i;
                if (destIdx >= totalSamples) break;

                float t = (float)i / rate;
                // 14msのS字アタックで耳に優しく、透明感のあるベルの減衰
                float envMain = (t < 0.014f)
                    ? Mathf.Sin((t / 0.014f) * Mathf.PI * 0.5f)
                    : Mathf.Exp(-(t - 0.014f) * (2.2f - idx * 0.08f));

                // 基本波＋クリスタルな第2倍音・第3倍音＋微かなチャイムの煌めき
                float sBaseL = Mathf.Sin(2.0f * Mathf.PI * (freq * detuneL) * t);
                float sBaseR = Mathf.Sin(2.0f * Mathf.PI * (freq * detuneR) * t);

                float sOct = Mathf.Sin(2.0f * Mathf.PI * (freq * 2.0f) * t) * Mathf.Exp(-t * 3.8f) * 0.35f;
                float sFifth = Mathf.Sin(2.0f * Mathf.PI * (freq * 3.0f) * t) * Mathf.Exp(-t * 5.5f) * 0.12f;
                float sSparkle = Mathf.Sin(2.0f * Mathf.PI * (freq * 4.0f) * t) * Mathf.Exp(-t * 7.5f) * 0.05f;

                float sigL = (sBaseL + sOct + sFifth + sSparkle) * envMain;
                float sigR = (sBaseR + sOct + sFifth + sSparkle) * envMain;

                // 最後の最高音(C7)を少し長めに響かせる
                float amp = (idx == notes.Length - 1) ? 0.18f : (0.13f * (1.0f - idx * 0.02f));
                dryL[destIdx] += sigL * gainL * amp;
                dryR[destIdx] += sigR * gainR * amp;
            }
        }

        // 空間ディレイ（左210ms、右310ms）と広大な天空リバーブ
        int d1 = (int)(0.210f * rate);
        int d2 = (int)(0.310f * rate);
        float feedback = 0.35f;

        float[] delBufL = new float[totalSamples + d1];
        float[] delBufR = new float[totalSamples + d2];
        float[] wetL = new float[totalSamples];
        float[] wetR = new float[totalSamples];

        int[] combDelays = { (int)(0.035f * rate), (int)(0.046f * rate), (int)(0.054f * rate), (int)(0.065f * rate) };
        float[][] combBufs = new float[4][] {
            new float[totalSamples + combDelays[0]],
            new float[totalSamples + combDelays[1]],
            new float[totalSamples + combDelays[2]],
            new float[totalSamples + combDelays[3]]
        };
        float[] combGains = { 0.74f, 0.70f, 0.67f, 0.63f };

        for (int n = 0; n < totalSamples; n++)
        {
            float inDl = dryL[n] + (n >= d2 ? delBufR[n] * feedback : 0f);
            float inDr = dryR[n] + (n >= d1 ? delBufL[n] * feedback : 0f);
            delBufL[n + d1] = inDl;
            delBufR[n + d2] = inDr;

            float delayOutL = (n >= d1 ? delBufL[n] : 0f) * 0.28f;
            float delayOutR = (n >= d2 ? delBufR[n] : 0f) * 0.28f;

            float revIn = (dryL[n] + dryR[n]) * 0.5f;
            float revOut = 0f;
            for (int c = 0; c < 4; c++)
            {
                int cd = combDelays[c];
                float delayedC = n >= cd ? combBufs[c][n] : 0f;
                combBufs[c][n + cd] = revIn + delayedC * combGains[c];
                revOut += delayedC * 0.12f;
            }

            wetL[n] = delayOutL + revOut * 0.50f;
            wetR[n] = delayOutR + revOut * 0.50f;
        }

        // ミックス & ノーマライズ
        float lpAlpha = 0.55f;
        float sL = 0f, sR = 0f;
        float[] mixedL = new float[totalSamples];
        float[] mixedR = new float[totalSamples];
        float maxPeak = 0f;

        for (int i = 0; i < totalSamples; i++)
        {
            float mL = dryL[i] + wetL[i];
            float mR = dryR[i] + wetR[i];
            sL += lpAlpha * (mL - sL);
            sR += lpAlpha * (mR - sR);
            mixedL[i] = sL;
            mixedR[i] = sR;

            float al = Mathf.Abs(sL);
            float ar = Mathf.Abs(sR);
            if (al > maxPeak) maxPeak = al;
            if (ar > maxPeak) maxPeak = ar;
        }

        float scale = maxPeak > 0.0001f ? (0.78f / maxPeak) : 1.0f;
        float[] stereoData = new float[totalSamples * 2];
        for (int i = 0; i < totalSamples; i++)
        {
            stereoData[i * 2] = mixedL[i] * scale;
            stereoData[i * 2 + 1] = mixedR[i] * scale;
        }

        var clip = AudioClip.Create("GameClearTriumphChime", totalSamples, 2, rate, false);
        clip.SetData(stereoData, 0);
        return clip;
    }
}
