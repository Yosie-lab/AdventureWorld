using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 『Rust & Float』相棒Rustの協力ギミック（古代クリスタル送電チャージ等）拡張
/// </summary>
public partial class AdventureRustDrone
{
    private bool _isPerformingGimmick = false;
    public bool IsPerformingGimmick => _isPerformingGimmick;

    private LineRenderer _chargeBeamLine;
    private AudioSource _chargeBeamAudio;

    /// <summary>指定されたクリスタルの受光部へ飛行し、エネルギービームを照射チャージする</summary>
    public void CommandChargeCrystal(Vector3 crystalTargetPos, Action onProgress, Action onComplete)
    {
        if (_isPerformingGimmick) return;
        StartCoroutine(ChargeCrystalRoutine(crystalTargetPos, onProgress, onComplete));
    }

    private IEnumerator ChargeCrystalRoutine(Vector3 crystalTargetPos, Action onProgress, Action onComplete)
    {
        _isPerformingGimmick = true;
        CurrentState = RustState.Follow;

        // 1. Rustに意気揚々と返事をさせる
        string[] startLines = {
            "ピピッ！任せて、Float！ボクのエネルギーを注ぐよ！",
            "古代の機械だね！チャージしてみるよ！",
            "ピピッ♪ フルチャージ開始するよ！"
        };
        SpeakCustom(startLines[UnityEngine.Random.Range(0, startLines.Length)], 2.5f);
        PlayCuriosityBeep();

        // 目標位置：クリスタルの手前約1.25m、高さ+0.4m
        Vector3 playerPos = _lookAt != null ? _lookAt.position : transform.position;
        Vector3 toPlayer = (playerPos - crystalTargetPos);
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.1f) toPlayer = Vector3.forward;
        toPlayer.Normalize();

        Vector3 hoverPos = crystalTargetPos + toPlayer * 1.35f + Vector3.up * 0.45f;
        Vector3 startPos = transform.position;

        // 2. 照射位置へのスムーズな飛行アプローチ（約1.1秒）
        float approachDuration = 1.1f;
        float elapsed = 0f;
        while (elapsed < approachDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / approachDuration);
            transform.position = Vector3.Lerp(startPos, hoverPos, t);

            // クリスタルの方を真っ直ぐ注視
            Vector3 lookDir = (crystalTargetPos - transform.position).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir, Vector3.up), 12f * Time.deltaTime);
            }
            yield return null;
        }

        transform.position = hoverPos;

        // 3. 送電ビームのセットアップと照射（約2.4秒間）
        EnsureChargeBeamComponents();
        if (_chargeBeamLine != null)
        {
            _chargeBeamLine.enabled = true;
            _chargeBeamLine.SetPosition(0, transform.position + Vector3.up * 0.1f);
            _chargeBeamLine.SetPosition(1, crystalTargetPos);
        }

        if (_chargeBeamAudio != null)
        {
            _chargeBeamAudio.Play();
        }

        // 星スパークル演出を放出
        SpawnHappyStars();

        float chargeDuration = 2.4f;
        float chargeElapsed = 0f;
        while (chargeElapsed < chargeDuration)
        {
            chargeElapsed += Time.deltaTime;
            float prog = Mathf.Clamp01(chargeElapsed / chargeDuration);

            // ビームの揺らぎと位置更新
            if (_chargeBeamLine != null)
            {
                Vector3 beamStart = transform.position + Vector3.up * 0.1f;
                // パルス微振動
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * 0.04f;
                _chargeBeamLine.SetPosition(0, beamStart);
                _chargeBeamLine.SetPosition(1, crystalTargetPos + jitter);

                float width = Mathf.Lerp(0.08f, 0.22f, Mathf.Sin(chargeElapsed * 24f) * 0.5f + 0.5f);
                _chargeBeamLine.startWidth = width;
                _chargeBeamLine.endWidth = width * 1.4f;
            }

            // ホバリングの微小な浮遊
            transform.position = hoverPos + Vector3.up * Mathf.Sin(Time.time * 6f) * 0.05f;

            onProgress?.Invoke();
            yield return null;
        }

        // 4. ビーム終了
        if (_chargeBeamLine != null)
        {
            _chargeBeamLine.enabled = false;
        }
        if (_chargeBeamAudio != null)
        {
            _chargeBeamAudio.Stop();
        }

        // 5. 達成の宙返りと祝福
        PlayReliefBeep();
        SpawnHappyStars();
        string[] successLines = {
            "ピピッ！起動したよ！風が吹き上がったよ、Float！",
            "大成功！これで気流に乗って上に行けるよ！",
            "やったぁ！回路がつながったよ！飛んでみて、Float！"
        };
        onComplete?.Invoke();

        _isPerformingGimmick = false;
        TriggerCelebration(successLines[UnityEngine.Random.Range(0, successLines.Length)], 3.5f);
    }

    private void EnsureChargeBeamComponents()
    {
        if (_chargeBeamLine == null)
        {
            var lineGo = new GameObject("RustChargeBeamLine");
            lineGo.transform.SetParent(transform, false);
            _chargeBeamLine = lineGo.AddComponent<LineRenderer>();
            _chargeBeamLine.positionCount = 2;
            _chargeBeamLine.startWidth = 0.14f;
            _chargeBeamLine.endWidth = 0.22f;

            // 加算発光マテリアル
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Particles/Standard Unlit")
                         ?? Shader.Find("Mobile/Particles/Additive");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", new Color(0.25f, 0.95f, 1.0f, 1.0f));
            mat.SetColor("_EmissionColor", new Color(0.5f, 1.8f, 2.5f) * 2.2f);
            mat.EnableKeyword("_EMISSION");
            _chargeBeamLine.material = mat;

            // 美しいシアン〜エレクトリックゴールドのグラデーション
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.3f, 0.9f, 1f), 0f), new GradientColorKey(new Color(1f, 0.95f, 0.4f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 1f) }
            );
            _chargeBeamLine.colorGradient = grad;
            _chargeBeamLine.enabled = false;
        }

        if (_chargeBeamAudio == null)
        {
            _chargeBeamAudio = gameObject.AddComponent<AudioSource>();
            _chargeBeamAudio.playOnAwake = false;
            _chargeBeamAudio.spatialBlend = 0.7f;
            _chargeBeamAudio.minDistance = 3f;
            _chargeBeamAudio.maxDistance = 25f;
            _chargeBeamAudio.volume = 0.55f;
            _chargeBeamAudio.loop = true;
            _chargeBeamAudio.clip = CreateChargeSynthClip();
        }
    }

    private static AudioClip CreateChargeSynthClip()
    {
        int rate = 44100;
        float duration = 1.2f;
        int numSamples = (int)(rate * duration);
        float[] samples = new float[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            float t = (float)i / rate;
            // 澄んだ高周波パルスシンセ（520Hz + 1040Hz倍音 + ピッチ揺らぎ）
            float f = 520f + Mathf.Sin(t * 32f) * 45f;
            float wave1 = Mathf.Sin(2f * Mathf.PI * f * t);
            float wave2 = Mathf.Sin(2f * Mathf.PI * f * 2.0f * t) * 0.35f;
            float spark = (UnityEngine.Random.value - 0.5f) * 0.08f; // 放電チリチリ音
            samples[i] = (wave1 + wave2 + spark) * 0.28f;
        }

        var clip = AudioClip.Create("RustChargeBeamSynth", numSamples, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
