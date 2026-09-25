using UnityEngine;

/// <summary>
/// 相棒Rustの「自律感情＆好奇心・喜びアクション」システム。
/// 立ち止まり時の花・蝶・水辺への自律探索、愛らしい首かしげ、
/// パーツ獲得時の喜びの宙返り＆星スパークルダンスを制御する。
/// </summary>
public partial class AdventureRustDrone
{
    public enum CuriosityKind { None, Butterfly, Flora, WaterShore, NikoEyeContact }

    [Header("Curiosity & Emotion")]
    CuriosityKind _curiosityKind = CuriosityKind.None;
    Transform _curiosityTransform;
    Vector3 _curiosityWorldPos;
    float _curiosityEndTime = 0f;
    float _nextCuriosityCheck = 0f;
    float _celebrationTotalDuration = 1.7f;
    float _idleTiltAngle = 0f;
    float _targetIdleTiltAngle = 0f;
    float _nextTiltChange = 0f;
    Vector3 _prevPlayerPos = Vector3.zero;
    float _playerRestTime = 0f;

    /// <summary>外部（スクラップ獲得や漂着ボックス開封）から呼べる喜び宙返りトリガー</summary>
    public void TriggerCelebration(string customMessage = null, float duration = 1.7f)
    {
        if (IsClimaxCrisis || IsClimaxOverdrive || _climaxHealing || _prologueDistress || _climaxFalling)
            return;

        CurrentState = RustState.Celebrating;
        _celebrationTotalDuration = duration;
        _stateTimer = duration;
        _curiosityKind = CuriosityKind.None; // 好奇心行動を中断して歓喜を最優先

        if (!string.IsNullOrEmpty(customMessage))
        {
            SpeakCustom(customMessage, 3.2f);
        }
        else
        {
            string[] cheers = {
                "やったぁ！パーツ発見だね！",
                "ピピッ♪ 大事な手がかりゲット！",
                "えへへ、順調順調！",
                "これでまた島を出る準備が進んだね！",
                "ピロリン！見つけてくれてありがとう、Niko！"
            };
            SpeakCustom(cheers[Random.Range(0, cheers.Length)], 3.0f);
        }

        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.55f;
            _audio.PlayOneShot(_happyBeepClip, 0.9f);
        }

        SpawnHappyStars();
    }

    /// <summary>喜びの星型スパークル粒子エフェクト</summary>
    void SpawnHappyStars()
    {
        var pGo = new GameObject("RustHappyStars");
        pGo.transform.position = transform.position + Vector3.up * 0.35f;
        var ps = pGo.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 0.85f;
        main.startSpeed = 2.4f;
        main.startSize = 0.18f;
        main.startColor = new Color(1f, 0.92f, 0.35f, 1f); // 鮮やかなゴールド
        main.loop = false;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.4f), 0f), new GradientColorKey(new Color(0.4f, 0.95f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        colOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    /// <summary>
    /// 自律好奇心・首かしげの毎フレーム更新
    /// </summary>
    void UpdateCuriosity()
    {
        if (_lookAt == null) return;

        // プレイヤーの移動量を計測
        if (_prevPlayerPos == Vector3.zero) _prevPlayerPos = _lookAt.position;
        float playerSpeed = Vector3.Distance(_lookAt.position, _prevPlayerPos) / Mathf.Max(0.0001f, Time.deltaTime);
        _prevPlayerPos = _lookAt.position;

        if (playerSpeed < 1.2f)
            _playerRestTime += Time.deltaTime;
        else
            _playerRestTime = 0f;

        if (CurrentState != RustState.Follow)
        {
            _curiosityKind = CuriosityKind.None;
            return;
        }

        // クライマックス中や特殊誘導中は自律好奇心を停止
        if (IsClimaxCrisis || IsClimaxOverdrive || _climaxHealing || _skybreakNestle || _prologueDistress || _isGuidingToTower || _isPointingToScrap)
        {
            _curiosityKind = CuriosityKind.None;
            return;
        }

        // プレイヤーが走っている、または離れすぎている（6.5m以上）場合は即座に解除して定位置追従へ戻る
        float distToNiko = Vector3.Distance(transform.position, _lookAt.position);
        if (playerSpeed > 2.5f || distToNiko > 6.5f)
        {
            _curiosityKind = CuriosityKind.None;
            _curiosityEndTime = 0f;
            _targetIdleTiltAngle = 0f;
        }

        // アイドル時の愛らしい首かしげ（Tilt）の更新
        if (Time.time >= _nextTiltChange)
        {
            _nextTiltChange = Time.time + Random.Range(3.2f, 6.0f);
            if (_playerRestTime > 1.2f && Random.value < 0.7f)
            {
                // 左右どちらかに16〜22度コテンと傾く
                _targetIdleTiltAngle = (Random.value < 0.5f ? -1f : 1f) * Random.Range(16f, 22f);
            }
            else
            {
                _targetIdleTiltAngle = 0f;
            }
        }
        _idleTiltAngle = Mathf.MoveTowards(_idleTiltAngle, _targetIdleTiltAngle, 65f * Time.deltaTime);

        // 好奇心アクションの実行中
        if (_curiosityKind != CuriosityKind.None)
        {
            if (Time.time >= _curiosityEndTime)
            {
                _curiosityKind = CuriosityKind.None;
                _nextCuriosityCheck = Time.time + Random.Range(8.0f, 15.0f);
            }
            return;
        }

        // プレイヤーが1.5秒以上立ち止まっている時、自律探索を開始
        if (_playerRestTime > 1.5f && Time.time >= _nextCuriosityCheck)
        {
            _nextCuriosityCheck = Time.time + Random.Range(6.0f, 11.0f);
            TryStartCuriosityInvestigation();
        }
    }

    /// <summary>周囲の興味対象（蝶、水辺、草花、Niko）を探して好奇心アクションを開始</summary>
    void TryStartCuriosityInvestigation()
    {
        Vector3 nikoPos = _lookAt.position;

        // 1. 周囲12m以内の蝶（AdventureButterflyDrift）を探す
        var butterflies = Object.FindObjectsByType<AdventureButterflyDrift>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        AdventureButterflyDrift nearestButterfly = null;
        float minBfDist = 12.0f;
        for (int i = 0; i < butterflies.Length; i++)
        {
            float d = Vector3.Distance(nikoPos, butterflies[i].transform.position);
            if (d < minBfDist)
            {
                minBfDist = d;
                nearestButterfly = butterflies[i];
            }
        }

        if (nearestButterfly != null)
        {
            _curiosityKind = CuriosityKind.Butterfly;
            _curiosityTransform = nearestButterfly.transform;
            _curiosityEndTime = Time.time + Random.Range(3.8f, 5.5f);
            PlayCuriosityBeep();
            if (Random.value < 0.45f)
            {
                string[] bfLines = { "わぁ、チョウチョだ！", "ひらひら飛んでるね…！", "ピピッ♪ きれいな羽…" };
                SpeakCustom(bfLines[Random.Range(0, bfLines.Length)], 2.8f);
            }
            return;
        }

        // 2. 水辺・波打ち際（標高5.8〜8.5m、またはオアシス池周辺）
        Vector3 oasisPos = new Vector3(480f, 48.2f, 455f);
        float distToOasis = Vector3.Distance(nikoPos, oasisPos);
        bool nearSea = nikoPos.y < 8.2f;

        if ((nearSea || distToOasis < 22f) && Random.value < 0.6f)
        {
            _curiosityKind = CuriosityKind.WaterShore;
            Vector3 shoreDir = nearSea ? Vector3.ProjectOnPlane(nikoPos - new Vector3(512f, 0f, 512f), Vector3.up).normalized : (oasisPos - nikoPos).normalized;
            _curiosityWorldPos = nikoPos + shoreDir * 2.5f;
            _curiosityWorldPos.y = nearSea ? 5.85f : 48.5f;
            _curiosityEndTime = Time.time + Random.Range(3.2f, 4.8f);
            PlayCuriosityBeep();
            if (Random.value < 0.4f)
            {
                string[] waterLines = { "水面がきらきら光ってる！", "透明ですごく綺麗だね…", "波の音が心地いいね" };
                SpeakCustom(waterLines[Random.Range(0, waterLines.Length)], 2.8f);
            }
            return;
        }

        // 3. Nikoへの愛らしいアイコンタクト（首かしげ＆見上げ）
        if (Random.value < 0.5f)
        {
            _curiosityKind = CuriosityKind.NikoEyeContact;
            _curiosityEndTime = Time.time + Random.Range(2.8f, 4.2f);
            _targetIdleTiltAngle = (Random.value < 0.5f ? -1f : 1f) * 20f;
            PlayCuriosityBeep();
            if (Random.value < 0.45f)
            {
                string[] nikoLines = { "どうしたの、Niko？", "少し休んでいく？", "ピピッ♪ 準備バッチリだよ！" };
                SpeakCustom(nikoLines[Random.Range(0, nikoLines.Length)], 2.5f);
            }
            return;
        }

        // 4. 足元の草花・低木スキャン
        _curiosityKind = CuriosityKind.Flora;
        Vector3 forwardRight = (_lookAt.forward * 1.5f + _lookAt.right * 1.2f).normalized;
        _curiosityWorldPos = nikoPos + forwardRight * 2.2f;
        var land = _land ?? Terrain.activeTerrain;
        if (land != null)
            _curiosityWorldPos.y = land.SampleHeight(_curiosityWorldPos) + land.transform.position.y + 0.42f;
        else
            _curiosityWorldPos.y = nikoPos.y + 0.35f;

        _curiosityEndTime = Time.time + Random.Range(3.0f, 4.5f);
        PlayCuriosityBeep();
        if (Random.value < 0.35f)
        {
            string[] floraLines = { "この草、いい匂いがするよ", "島中、緑がいっぱいだね！", "ピピッ、珍しい植物かな？" };
            SpeakCustom(floraLines[Random.Range(0, floraLines.Length)], 2.8f);
        }
    }

    /// <summary>好奇心発動時の愛らしいチャイム音</summary>
    void PlayCuriosityBeep()
    {
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.38f + Random.Range(-0.06f, 0.08f);
            _audio.PlayOneShot(_happyBeepClip, 0.55f);
        }
    }

    /// <summary>好奇心実行中の目標位置を取得</summary>
    Vector3 GetCuriosityTargetPosition()
    {
        if (_lookAt == null) return Vector3.zero;

        Vector3 nikoPos = _lookAt.position;
        Vector3 chestPos = GetNikoChestPosition();

        switch (_curiosityKind)
        {
            case CuriosityKind.Butterfly:
                if (_curiosityTransform != null)
                {
                    Vector3 bfPos = _curiosityTransform.position;
                    // 蝶の少し斜め上（0.55m）にホバリング追従
                    return bfPos + Vector3.up * 0.45f + Vector3.right * 0.3f;
                }
                break;

            case CuriosityKind.WaterShore:
                float waterBob = Mathf.Sin(Time.time * 2.4f) * 0.04f;
                return _curiosityWorldPos + Vector3.up * (0.2f + waterBob);

            case CuriosityKind.Flora:
                float floraBob = Mathf.Sin(Time.time * 2.8f) * 0.03f;
                return _curiosityWorldPos + Vector3.up * floraBob;

            case CuriosityKind.NikoEyeContact:
                // Nikoの斜め前（視線の先・胸〜顔の高さ）に回り込んで見つめる
                Vector3 contactPos = chestPos + _lookAt.forward * 1.35f + _lookAt.right * 0.45f + Vector3.up * 0.12f;
                float contactBob = Mathf.Sin(Time.time * 3.0f) * 0.035f;
                return contactPos + Vector3.up * contactBob;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// 歓喜の宙返り（Celebrating）用の高精度シネマティック回転とボビングオフセット
    /// </summary>
    Quaternion GetCelebrationRotation(Quaternion baseLook)
    {
        float p = 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(0.01f, _celebrationTotalDuration));

        // 3段階の流麗な歓喜アニメーション:
        // 0.0〜0.15: タメ（軽く上を向く）
        // 0.15〜0.75: 空中360度ループ（SmoothStepによる美しい宙返り）
        // 0.75〜1.0: 着地とハッピーバウンス（上下ピョコピョコ＆左右微細ロール）
        if (p < 0.15f)
        {
            float t = p / 0.15f;
            return baseLook * Quaternion.Euler(-25f * t, 0f, 0f);
        }
        else if (p < 0.75f)
        {
            float loopT = (p - 0.15f) / 0.6f;
            float smoothLoop = Mathf.SmoothStep(0f, 1f, loopT);
            float pitchAngle = -25f + smoothLoop * 360f;
            float rollWiggle = Mathf.Sin(smoothLoop * Mathf.PI * 2f) * 15f;
            return baseLook * Quaternion.Euler(pitchAngle, 0f, rollWiggle);
        }
        else
        {
            float bounceT = (p - 0.75f) / 0.25f;
            float bouncePitch = Mathf.Sin(bounceT * Mathf.PI * 4f) * 8f;
            float bounceRoll = Mathf.Sin(bounceT * Mathf.PI * 3f) * 6f;
            return baseLook * Quaternion.Euler(bouncePitch, 0f, bounceRoll);
        }
    }
}
