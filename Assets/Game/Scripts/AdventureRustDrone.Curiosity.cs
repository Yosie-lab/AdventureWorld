using UnityEngine;

/// <summary>
/// 相棒Rustの「自律感情＆好奇心・無駄の愛おしさ・生命感」システム。
/// 立ち止まり時の足元うたた寝（寝息・飛び起き）、水辺嫌がり（肩退避・プルプル水滴払い）、
/// 足元の花をじっと見つめる（小首かしげ）、蝶との螺旋ダンス、
/// Nikoへの愛らしい見つめ合い、パーツ獲得時の喜び宙返り＆星スパークルを制御する。
/// </summary>
public partial class AdventureRustDrone
{
    public enum CuriosityKind
    {
        None,
        Butterfly,
        Flora,
        WaterShore,
        NikoEyeContact,
        SleepNap,
        WaterPanic,
        BeckonGesture,
        Crab,
        IdleOrbit
    }

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

    // 手招き＆呼びかけ（BeckonGesture）用
    Vector3 _beckonTargetPos = Vector3.zero;
    float _beckonStartTime = 0f;

    // カニ好奇心（Crab）用
    float _crabInvestigateStartTime = 0f;
    bool _crabScaredHop = false;

    // 待機旋回（IdleOrbit）用
    float _idleOrbitStartTime = 0f;

    // うたた寝（SleepNap）用
    bool _isSleeping = false;
    float _sleepBreathPhase = 0f;
    Light _sleepGlowLight;

    // 水辺嫌がり（WaterPanic）用
    bool _wasInWaterPanic = false;
    float _panicSpeechCooldown = 0f;

    /// <summary>外部（スクラップ獲得や漂着ボックス開封）から呼べる喜び宙返りトリガー</summary>
    public void TriggerCelebration(string customMessage = null, float duration = 1.7f)
    {
        if (IsClimaxCrisis || IsClimaxOverdrive || _climaxHealing || _prologueDistress || _climaxFalling)
            return;

        CurrentState = RustState.Celebrating;
        _celebrationTotalDuration = duration;
        _stateTimer = duration;
        EndSleepState(wakeHop: false);
        _curiosityKind = CuriosityKind.None;

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
                "ピロリン！見つけてくれてありがとう、Float！"
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

    /// <summary>貝殻・素材ポーチ取得時のRustの愛らしいリアクション（宙返り＋セリフ＋鳴き声＋星屑）</summary>
    public void TriggerSeashellReaction(
        AdventureBeachSeashellItem.ShellKind kind, 
        string itemName, 
        string itemReaction, 
        AdventureRustCosmetics.CosmeticDef newlyCraftable)
    {
        if (IsClimaxCrisis || IsClimaxOverdrive || _climaxHealing || _prologueDistress || _climaxFalling)
            return;

        string speech;
        if (newlyCraftable != null)
        {
            speech = AdventureLocalizationManager.GetFormat("shell_craft_ready_speech", newlyCraftable.displayName);
        }
        else if (!string.IsNullOrEmpty(itemReaction))
        {
            speech = AdventureLocalizationManager.Get(itemReaction);
        }
        else
        {
            string locItem = AdventureLocalizationManager.Get(itemName);
            speech = AdventureLocalizationManager.GetFormat("shell_picked_generic_speech", locItem);
        }

        // 喜びの宙返りアニメーション（フリップ回転）＆セリフ発火！
        TriggerCelebration(speech, 3.2f);
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
        main.startColor = new Color(1f, 0.92f, 0.35f, 1f);
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
    /// 自律好奇心・感情・生命感の毎フレーム更新
    /// </summary>
    void UpdateCuriosity()
    {
        if (_lookAt == null) return;

        Vector3 nikoPos = _lookAt.position;

        // プレイヤーの移動速度を計測
        if (_prevPlayerPos == Vector3.zero) _prevPlayerPos = nikoPos;
        float playerSpeed = Vector3.Distance(nikoPos, _prevPlayerPos) / Mathf.Max(0.0001f, Time.deltaTime);
        _prevPlayerPos = nikoPos;

        if (playerSpeed < 0.85f)
            _playerRestTime += Time.deltaTime;
        else
            _playerRestTime = 0f;

        // クライマックス中や特殊誘導中は自律行動を完全停止
        if (IsClimaxCrisis || IsClimaxOverdrive || _climaxHealing || _skybreakNestle || _prologueDistress || _isGuidingToTower || _isPointingToScrap)
        {
            EndSleepState(wakeHop: false);
            _curiosityKind = CuriosityKind.None;
            return;
        }

        // ── 1. 水没パニック判定（最優先：機械のRustは水が大の苦手！） ──
        bool isNikoInWater = CheckIsNikoInWater(nikoPos);
        if (isNikoInWater)
        {
            EndSleepState(wakeHop: false);
            if (_curiosityKind != CuriosityKind.WaterPanic)
            {
                _curiosityKind = CuriosityKind.WaterPanic;
                _curiosityEndTime = float.MaxValue; // 水から出るまで継続
                PlayPanicBeep();
                if (Time.time >= _panicSpeechCooldown)
                {
                    _panicSpeechCooldown = Time.time + 4.5f;
                    string[] panicLines = {
                        "ヒャッ！水だ！濡れちゃう濡れちゃう！",
                        "Float、水はダメだよ〜！ギアが錆びちゃう…！",
                        "た、高いところ！抱っこして〜！",
                        "ピピッ！ギギッ！水滴が…！"
                    };
                    SpeakCustom(panicLines[Random.Range(0, panicLines.Length)], 2.8f);
                }
            }
            _wasInWaterPanic = true;
            return;
        }
        else if (_wasInWaterPanic)
        {
            // 水から脱出した瞬間のホッとしたリアクション
            _wasInWaterPanic = false;
            _curiosityKind = CuriosityKind.None;
            _nextCuriosityCheck = Time.time + 4.0f;
            PlayReliefBeep();
            string[] reliefLines = {
                "ふぅ……助かったぁ……！",
                "ギアに水が入らなくてよかった…！",
                "乾いた地面って最高だね、Float！"
            };
            SpeakCustom(reliefLines[Random.Range(0, reliefLines.Length)], 2.8f);
            return;
        }

        // プレイヤーが大きく動いた、または遠く離れた場合はうたた寝・好奇心を即解除
        float distToNiko = Vector3.Distance(transform.position, nikoPos);
        if (playerSpeed > 1.25f || distToNiko > 6.0f)
        {
            if (_isSleeping)
            {
                // 寝ていたのにNikoが歩き出して慌てて飛び起きる！
                EndSleepState(wakeHop: true);
            }
            _curiosityKind = CuriosityKind.None;
            _curiosityEndTime = 0f;
            _targetIdleTiltAngle = 0f;
        }

        // アイドル時の愛らしい首かしげ（Tilt）の更新
        if (Time.time >= _nextTiltChange)
        {
            _nextTiltChange = Time.time + Random.Range(3.2f, 5.8f);
            if (_playerRestTime > 1.0f && !_isSleeping && Random.value < 0.72f)
            {
                _targetIdleTiltAngle = (Random.value < 0.5f ? -1f : 1f) * Random.Range(16f, 24f);
            }
            else
            {
                _targetIdleTiltAngle = 0f;
            }
        }
        _idleTiltAngle = Mathf.MoveTowards(_idleTiltAngle, _targetIdleTiltAngle, 65f * Time.deltaTime);

        // うたた寝実行中の寝息ライト演出
        if (_isSleeping)
        {
            TickSleepingState();
            return;
        }

        // 好奇心アクションの実行中カウントダウン
        if (_curiosityKind != CuriosityKind.None)
        {
            if (_curiosityKind == CuriosityKind.Crab && !_crabScaredHop && Time.time >= _crabInvestigateStartTime + 2.4f)
            {
                _crabScaredHop = true;
                _velocity += Vector3.up * 2.8f;
                PlaySurpriseBeep();
                string scaredKey = Random.value < 0.5f ? "rust_curiosity_crab_scared_1" : "rust_curiosity_crab_scared_2";
                SpeakCustom(AdventureLocalizationManager.Get(scaredKey), 3.0f);
            }

            if (Time.time >= _curiosityEndTime)
            {
                _curiosityKind = CuriosityKind.None;
                _nextCuriosityCheck = Time.time + Random.Range(7.0f, 13.0f);
            }
            return;
        }

        // プレイヤーが一定時間立ち止まっている時の自律アクション抽選
        var curiosityBlock = AdventurePrologueDrama.Instance;
        bool prologueTalking = curiosityBlock != null && curiosityBlock.IsBlockingSpeech;
        if (_playerRestTime > 1.4f && Time.time >= _nextCuriosityCheck && CurrentState == RustState.Follow && !prologueTalking)
        {
            _nextCuriosityCheck = Time.time + Random.Range(6.0f, 11.0f);
            TryStartCuriosityInvestigation(nikoPos);
        }
    }

    /// <summary>Nikoが波打ち際や池などの水中にいるかを判定</summary>
    bool CheckIsNikoInWater(Vector3 nikoPos)
    {
        // 1. 海面（波打ち際深部: 標高6.0m以下）
        if (nikoPos.y < 6.05f) return true;

        // 2. 段々池やオアシス池の水面（標高48.2m付近、水深のあるエリア）
        Vector3 oasisCenter = new Vector3(480f, 48.0f, 455f);
        if (Vector2.Distance(new Vector2(nikoPos.x, nikoPos.z), new Vector2(oasisCenter.x, oasisCenter.z)) < 16f)
        {
            if (nikoPos.y < 48.7f) return true;
        }

        return false;
    }

    /// <summary>周囲の興味対象（手招き呼びかけ、うたた寝、蝶、花、見つめ合い、水辺）を探して自律アクションを開始</summary>
    void TryStartCuriosityInvestigation(Vector3 nikoPos)
    {
        // 0. 手招き＆呼びかけジェスチャー（プレイヤーが迷って立ち止まっている時、または周囲に未回収物がある時）
        if (TryStartBeckonGesture(nikoPos))
            return;

        // A. Nikoが4.5秒以上じっと立ち止まっている場合：足元でうたた寝（寝息・Zzz）
        if (_playerRestTime >= 4.2f && Random.value < 0.65f)
        {
            StartSleepState(nikoPos);
            return;
        }

        // B0. 周囲12m以内のカニ（CrabWander）を探して観察＆驚きホップ
        var crabs = CrabWander.AllCrabs;
        CrabWander nearestCrab = null;
        float minCrabDist = 12.0f;
        for (int i = 0; i < crabs.Count; i++)
        {
            var c = crabs[i];
            if (c == null) continue;
            float d = Vector3.Distance(nikoPos, c.transform.position);
            if (d < minCrabDist)
            {
                minCrabDist = d;
                nearestCrab = c;
            }
        }

        if (nearestCrab != null && Random.value < 0.85f)
        {
            _curiosityKind = CuriosityKind.Crab;
            _curiosityTransform = nearestCrab.transform;
            _crabInvestigateStartTime = Time.time;
            _crabScaredHop = false;
            _curiosityEndTime = Time.time + 5.0f;
            _targetIdleTiltAngle = (Random.value < 0.5f ? -1f : 1f) * 22f;
            PlayCuriosityBeep();
            string crabKey = Random.value < 0.5f ? "rust_curiosity_crab_1" : "rust_curiosity_crab_2";
            SpeakCustom(AdventureLocalizationManager.Get(crabKey), 2.8f);
            return;
        }

        // B. 周囲14m以内の蝶（AdventureButterflyDrift）を探して螺旋ダンス
        var butterflies = AdventureButterflyDrift.ActiveButterflies;
        AdventureButterflyDrift nearestButterfly = null;
        float minBfDist = 14.0f;
        for (int i = 0; i < butterflies.Count; i++)
        {
            var b = butterflies[i];
            if (b == null) continue;
            float d = Vector3.Distance(nikoPos, b.transform.position);
            if (d < minBfDist)
            {
                minBfDist = d;
                nearestButterfly = b;
            }
        }

        if (nearestButterfly != null)
        {
            _curiosityKind = CuriosityKind.Butterfly;
            _curiosityTransform = nearestButterfly.transform;
            _curiosityEndTime = Time.time + Random.Range(4.5f, 6.5f);
            PlayCuriosityBeep();
            if (Random.value < 0.5f)
            {
                string[] bfLines = {
                    "わぁ、チョウチョさん！一緒に飛ぼう！",
                    "ひらひら飛んでるね…！綺麗だなぁ",
                    "ピピッ♪ どこまで飛んでいくのかな？"
                };
                SpeakCustom(bfLines[Random.Range(0, bfLines.Length)], 2.8f);
            }
            return;
        }

        // C. 足元の花・珍しい草をじっくり見下ろして首かしげ
        if (Random.value < 0.55f)
        {
            _curiosityKind = CuriosityKind.Flora;
            Vector3 forwardRight = (_lookAt.forward * 1.4f + _lookAt.right * 1.1f).normalized;
            _curiosityWorldPos = nikoPos + forwardRight * 1.8f;
            var land = _land ?? Terrain.activeTerrain;
            if (land != null)
                _curiosityWorldPos.y = land.SampleHeight(_curiosityWorldPos) + land.transform.position.y + 0.32f;
            else
                _curiosityWorldPos.y = nikoPos.y + 0.32f;

            _curiosityEndTime = Time.time + Random.Range(3.8f, 5.2f);
            _targetIdleTiltAngle = (Random.value < 0.5f ? -1f : 1f) * 22f;
            PlayCuriosityBeep();
            if (Random.value < 0.45f)
            {
                string[] floraLines = {
                    "わぁ、小さな花が咲いてる…！",
                    "ピピッ♪ すごくいい匂いがするよ",
                    "誰が植えたのかな…？綺麗だね、Float",
                    "ふふっ、風に揺れてご挨拶してるみたい！"
                };
                SpeakCustom(floraLines[Random.Range(0, floraLines.Length)], 2.8f);
            }
            return;
        }

        // D. Nikoへの愛らしい見つめ合い（上目遣い＆首かしげ）
        _curiosityKind = CuriosityKind.NikoEyeContact;
        _curiosityEndTime = Time.time + Random.Range(3.2f, 4.8f);
        _targetIdleTiltAngle = (Random.value < 0.5f ? -1f : 1f) * 20f;
        PlayCuriosityBeep();

        // 探索ポイントが10pt以上でまだ焚き火に火を点けていない場合、高確率で焚き火＆工房をリマインド
        var campfire = AdventureBeachCampfire.Instance ?? Object.FindAnyObjectByType<AdventureBeachCampfire>();
        var scrapMgr = AdventureScrapManager.Instance;
        int pts = scrapMgr != null ? scrapMgr.TotalProgressPoints : 0;
        bool campfireUnlit = campfire != null && !campfire.IsLit;

        if (pts >= 10 && campfireUnlit && Random.value < 0.80f)
        {
            string[] campfireLines = {
                "ねぇFloat、探索ポイントが10pt貯まってるよ！砂浜の焚き火に火を点けに行ってみない？",
                "探索ポイントが10ptあるよ！【Bキー】で工房を開いて、ボクのアクセサリーを作ってみない？",
                "砂浜の焚き火、10ポイントあるから火を点けられるよ！あったまりに行こうよ、Float！",
                "【Bキー】を押すとボクの着せ替え工房が開くよ！新しいアクセサリーを作ってみようよ！"
            };
            SpeakCustom(campfireLines[Random.Range(0, campfireLines.Length)], 4.8f);
        }
        else if (pts >= 20 && Random.value < 0.65f)
        {
            SpeakCustom("20ポイント達成してるよ！中央タワーへ行って天蓋レバーを引こう、Float！", 4.8f);
        }
        else if (Random.value < 0.45f)
        {
            // E. Floatの周囲をくるりと1周旋回（退屈しぐさ・次どこ行く？促し）
            _curiosityKind = CuriosityKind.IdleOrbit;
            _idleOrbitStartTime = Time.time;
            _curiosityEndTime = Time.time + 3.8f;
            _targetIdleTiltAngle = 10f;
            PlayCuriosityBeep();
            string[] orbitKeys = { "rust_idle_orbit_1", "rust_idle_orbit_2", "rust_idle_orbit_3" };
            SpeakCustom(AdventureLocalizationManager.Get(orbitKeys[Random.Range(0, orbitKeys.Length)]), 3.0f);
        }
        else if (Random.value < 0.5f)
        {
            string[] nikoLines = {
                "どうしたの、Float？疲れてない？",
                "ふふっ、Floatの顔を見てると安心するな",
                "ずっと一緒だよ、Float！",
                "ピピッ♪ 準備ができたら、いつでも合図してね"
            };
            SpeakCustom(nikoLines[Random.Range(0, nikoLines.Length)], 2.8f);
        }
    }

    /// <summary>うたた寝を開始</summary>
    void StartSleepState(Vector3 nikoPos)
    {
        _isSleeping = true;
        _curiosityKind = CuriosityKind.SleepNap;
        _curiosityEndTime = float.MaxValue;
        _sleepBreathPhase = 0f;

        Vector3 groundTarget = nikoPos + _lookAt.forward * 0.95f + _lookAt.right * 0.35f;
        var land = _land ?? Terrain.activeTerrain;
        if (land != null)
            groundTarget.y = land.SampleHeight(groundTarget) + land.transform.position.y + 0.28f;
        else
            groundTarget.y = nikoPos.y + 0.28f;

        _curiosityWorldPos = groundTarget;
        _targetIdleTiltAngle = 12f; // ちょこんと首を傾けて丸まる

        if (_sleepGlowLight == null)
        {
            var lGo = new GameObject("RustSleepLight");
            lGo.transform.SetParent(transform, false);
            lGo.transform.localPosition = new Vector3(0f, 0.05f, 0.25f);
            _sleepGlowLight = lGo.AddComponent<Light>();
            _sleepGlowLight.type = LightType.Point;
            _sleepGlowLight.range = 3.0f;
            _sleepGlowLight.color = new Color(0.45f, 0.85f, 1f); // 穏やかなシアンブルー
        }
        _sleepGlowLight.enabled = true;

        PlaySoftChirp();
        string[] sleepLines = {
            "すぅ……すぅ……",
            "ぽかぽかして……きもちいい……Zzz",
            "Floatのそば……安心するな……すぅ……",
            "……すやぁ……"
        };
        SpeakCustom(sleepLines[Random.Range(0, sleepLines.Length)], 3.5f);
    }

    /// <summary>うたた寝中の寝息パルス</summary>
    void TickSleepingState()
    {
        _sleepBreathPhase += Time.deltaTime * 1.8f;
        float pulse = Mathf.Sin(_sleepBreathPhase) * 0.5f + 0.5f;

        if (_sleepGlowLight != null)
            _sleepGlowLight.intensity = Mathf.Lerp(0.25f, 0.85f, pulse);

        if (_bodyMat != null)
        {
            _bodyMat.EnableKeyword("_EMISSION");
            Color breathCol = Color.Lerp(new Color(0.1f, 0.25f, 0.4f), new Color(0.3f, 0.65f, 0.9f), pulse);
            _bodyMat.SetColor("_EmissionColor", breathCol);
        }
    }

    /// <summary>うたた寝を終了</summary>
    void EndSleepState(bool wakeHop)
    {
        if (!_isSleeping) return;
        _isSleeping = false;

        if (_sleepGlowLight != null)
            _sleepGlowLight.enabled = false;

        if (wakeHop)
        {
            // パッと飛び起きる勢いのあるホップ！
            _velocity += Vector3.up * 3.4f;
            PlaySurpriseBeep();
            string[] wakeLines = {
                "ハッ！寝てないよ！起きてるよ！",
                "ピピッ！ふぁ…びっくりしたぁ！",
                "いつでも行けるよ、Float！えへへ…",
                "シャキーン！準備かんりょう！"
            };
            SpeakCustom(wakeLines[Random.Range(0, wakeLines.Length)], 2.5f);
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
            case CuriosityKind.WaterPanic:
                // 水を怖がってNikoの右肩斜め上（食い込まない安全距離）へピタッと退避
                Vector3 shoulderPos = nikoPos + Vector3.up * 1.82f - _lookAt.forward * 0.35f + _lookAt.right * 0.85f;
                float panicBob = Mathf.Sin(Time.time * 24f) * 0.04f;
                return shoulderPos + Vector3.up * panicBob;

            case CuriosityKind.SleepNap:
                // Nikoの足元でちょこんと丸まる（微弱な呼吸ボビング）
                float breathBob = Mathf.Sin(_sleepBreathPhase) * 0.02f;
                return _curiosityWorldPos + Vector3.up * breathBob;

            case CuriosityKind.Butterfly:
                if (_curiosityTransform != null)
                {
                    Vector3 bfPos = _curiosityTransform.position;
                    // 蝶の周りを半径0.6mで優雅に螺旋周回
                    float orbitAngle = Time.time * 2.8f;
                    Vector3 orbitOffset = new Vector3(Mathf.Cos(orbitAngle) * 0.6f, Mathf.Sin(Time.time * 3.2f) * 0.25f + 0.35f, Mathf.Sin(orbitAngle) * 0.6f);
                    return bfPos + orbitOffset;
                }
                break;

            case CuriosityKind.Flora:
                // 花の真上0.35mでじっとホバリング
                float floraBob = Mathf.Sin(Time.time * 2.4f) * 0.025f;
                return _curiosityWorldPos + Vector3.up * (0.05f + floraBob);

            case CuriosityKind.NikoEyeContact:
                // Nikoの斜め前（視線の先・胸〜顔の高さ）に回り込んで見つめる（食い込まない安全距離）
                Vector3 contactPos = chestPos + _lookAt.forward * 1.60f + _lookAt.right * 0.65f + Vector3.up * 0.22f;
                float contactBob = Mathf.Sin(Time.time * 2.8f) * 0.03f;
                return contactPos + Vector3.up * contactBob;

            case CuriosityKind.BeckonGesture:
                // 前半（0〜1.5秒）：Floatの正面1.4mでアイコンタクト＆お辞儀バウンス
                // 後半（1.5〜4.5秒）：目標方角へ2.2m進み出て「こっちだよ！」と手招きホバリング
                float elapsed = Time.time - _beckonStartTime;
                float leadBlend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((elapsed - 1.2f) / 1.5f));
                Vector3 frontPos = chestPos + _lookAt.forward * 1.45f + Vector3.up * 0.28f;
                Vector3 toTargetFlat = _beckonTargetPos - nikoPos;
                toTargetFlat.y = 0f;
                Vector3 leadDir = toTargetFlat.sqrMagnitude > 0.01f ? toTargetFlat.normalized : _lookAt.forward;
                Vector3 leadPos = chestPos + leadDir * 2.35f + Vector3.up * 0.35f;

                Vector3 targetBeckonPos = Vector3.Lerp(frontPos, leadPos, leadBlend);
                float beckonBounce = Mathf.Sin(Time.time * 5.5f) * (leadBlend > 0.5f ? 0.06f : 0.04f);
                return targetBeckonPos + Vector3.up * beckonBounce;

            case CuriosityKind.Crab:
                if (_curiosityTransform != null)
                {
                    float crabElapsed = Time.time - _crabInvestigateStartTime;
                    if (crabElapsed < 2.4f)
                    {
                        // カニの斜め前0.7m、地面から0.42mの低空でじっくり観察
                        Vector3 crabPos = _curiosityTransform.position;
                        Vector3 approachOffset = _curiosityTransform.forward * 0.72f + Vector3.up * 0.42f;
                        float observeBob = Mathf.Sin(Time.time * 3.2f) * 0.025f;
                        return crabPos + approachOffset + Vector3.up * observeBob;
                    }
                    else
                    {
                        // 驚いてFloatの後ろ斜め上にサッと隠れる
                        Vector3 behindPos = nikoPos - _lookAt.forward * 0.85f + _lookAt.right * 0.65f + Vector3.up * 1.55f;
                        float tremble = Mathf.Sin(Time.time * 26f) * 0.03f;
                        return behindPos + Vector3.up * tremble;
                    }
                }
                break;

            case CuriosityKind.IdleOrbit:
                // Floatの胸〜頭の周りを半径1.35mでふわりと1周旋回
                float orbitTime = (Time.time - _idleOrbitStartTime) * 2.2f;
                Vector3 orbitPos = chestPos + new Vector3(Mathf.Sin(orbitTime) * 1.35f, 0.35f + Mathf.Sin(orbitTime * 2f) * 0.15f, Mathf.Cos(orbitTime) * 1.35f);
                return orbitPos;

            case CuriosityKind.WaterShore:
                float waterBob = Mathf.Sin(Time.time * 2.4f) * 0.04f;
                return _curiosityWorldPos + Vector3.up * (0.2f + waterBob);
        }

        return Vector3.zero;
    }

    /// <summary>好奇心・感情に応じた回転姿勢の適用</summary>
    Quaternion ApplyCuriosityRotation(Quaternion baseLook)
    {
        switch (_curiosityKind)
        {
            case CuriosityKind.WaterPanic:
                // 水滴を激しく払うプルプル振動！
                float panicPitch = Mathf.Sin(Time.time * 38f) * 12f;
                float panicRoll = Mathf.Sin(Time.time * 46f) * 18f;
                return baseLook * Quaternion.Euler(panicPitch, 0f, panicRoll);

            case CuriosityKind.SleepNap:
                // うつむいて丸まる（ピッチ+16度）＋微弱な寝息揺らぎ
                float breathTilt = Mathf.Sin(_sleepBreathPhase) * 2.5f;
                return baseLook * Quaternion.Euler(16f + breathTilt, 0f, _idleTiltAngle);

            case CuriosityKind.Flora:
                // 足元の花をじっくり見下ろす（下向きピッチ-42度）＋小首かしげ
                return baseLook * Quaternion.Euler(-42f, 0f, _idleTiltAngle);

            case CuriosityKind.NikoEyeContact:
                // Nikoの顔を見上げる姿勢＋小首かしげ
                return baseLook * Quaternion.Euler(-15f, 0f, _idleTiltAngle);

            case CuriosityKind.BeckonGesture:
                // 前半はFloatを見つめ、後半は目標方向へ顔を向けて「こっち！」と手招きチルト
                float beckonElapsed = Time.time - _beckonStartTime;
                if (beckonElapsed < 1.4f)
                {
                    // Floatの目をじっと見つめて呼びかけ（微小な縦うなずき）
                    float nod = Mathf.Sin(Time.time * 6.5f) * 8f;
                    return baseLook * Quaternion.Euler(-12f + nod, 0f, _idleTiltAngle * 0.5f);
                }
                else
                {
                    // 目標方向を向く
                    Vector3 toTargetDir = _beckonTargetPos - transform.position;
                    toTargetDir.y = 0f;
                    if (toTargetDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion lookTarget = Quaternion.LookRotation(toTargetDir.normalized, Vector3.up);
                        // 時折Floatを振り返る愛らしい振り返りチルト
                        float glanceFloat = Mathf.Sin(Time.time * 3.5f) * 16f;
                        return lookTarget * Quaternion.Euler(6f, glanceFloat, Mathf.Sin(Time.time * 4f) * 8f);
                    }
                    return baseLook;
                }

            case CuriosityKind.Crab:
                if (_curiosityTransform != null)
                {
                    float crabElapsed = Time.time - _crabInvestigateStartTime;
                    if (crabElapsed < 2.4f)
                    {
                        // カニを見下ろす（下向きピッチ-32度）＋首かしげ
                        Vector3 toCrab = _curiosityTransform.position - transform.position;
                        toCrab.y = 0f;
                        Quaternion faceCrab = toCrab.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCrab.normalized, Vector3.up) : baseLook;
                        return faceCrab * Quaternion.Euler(-32f, 0f, _idleTiltAngle);
                    }
                    else
                    {
                        // Floatの背後から前方のカニを警戒してチラ見する姿勢
                        Vector3 forwardDir = _lookAt != null ? _lookAt.forward : transform.forward;
                        Quaternion lookFront = Quaternion.LookRotation(forwardDir, Vector3.up);
                        float shiver = Mathf.Sin(Time.time * 28f) * 6f;
                        return lookFront * Quaternion.Euler(12f, shiver, _idleTiltAngle);
                    }
                }
                return baseLook;

            case CuriosityKind.IdleOrbit:
                // 旋回中は常にFloatの胸元を向く
                Vector3 toFloatChest = GetNikoChestPosition() - transform.position;
                toFloatChest.y = 0f;
                if (toFloatChest.sqrMagnitude > 0.01f)
                {
                    Quaternion faceFloat = Quaternion.LookRotation(toFloatChest.normalized, Vector3.up);
                    float rollBank = Mathf.Sin((Time.time - _idleOrbitStartTime) * 2.2f) * 14f;
                    return faceFloat * Quaternion.Euler(-10f, 0f, rollBank);
                }
                return baseLook;
        }

        if (Mathf.Abs(_idleTiltAngle) > 0.05f)
            return baseLook * Quaternion.Euler(0f, 0f, _idleTiltAngle);

        return baseLook;
    }

    #region Sounds for Curiosity & Emotions
    void PlayCuriosityBeep()
    {
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.38f + Random.Range(-0.05f, 0.08f);
            _audio.PlayOneShot(_happyBeepClip, 0.55f);
        }
    }

    void PlayBeckonChime()
    {
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.65f;
            _audio.PlayOneShot(_happyBeepClip, 0.70f);
        }
    }

    /// <summary>周囲の興味対象や進行ルートに向けて、Rustが手招き＆呼びかけを行うジェスチャー</summary>
    bool TryStartBeckonGesture(Vector3 nikoPos)
    {
        if (Random.value > 0.65f) return false;

        // 1. 周囲35m以内の未回収スクラップを探す
        Vector3 targetPos = Vector3.zero;
        string beckonLine = null;

        if (_distantScrap != null)
        {
            targetPos = _distantScrap.transform.position;
            beckonLine = "ピピッ！Float、あっちの方に何か光るものがあるよ！行ってみよう！";
        }
        else if (SanctuaryTowerCenter != Vector3.zero && Vector3.Distance(nikoPos, SanctuaryTowerCenter) > 40f)
        {
            targetPos = SanctuaryTowerCenter;
            beckonLine = "見て、Float！中央の高いタワーがあんなにそびえ立ってるよ！あそこを目指してみない？";
        }
        else
        {
            // 前方の未知のエリア
            targetPos = nikoPos + _lookAt.forward * 18f;
            string[] generalLines = {
                "ピロッ♪ Float、次はあっちの高い丘まで登ってみない？",
                "あっちの空、すごく綺麗だよ！一緒に飛んでみようよ♪",
                "準備はバッチリ？ Floatと一緒ならどこまでも行けるよ！",
                "こっちこっち！風の通り道があるみたいだよ！"
            };
            beckonLine = generalLines[Random.Range(0, generalLines.Length)];
        }

        _curiosityKind = CuriosityKind.BeckonGesture;
        _beckonTargetPos = targetPos;
        _beckonStartTime = Time.time;
        _curiosityEndTime = Time.time + Random.Range(4.2f, 5.5f);
        _targetIdleTiltAngle = (Random.value < 0.5f ? -1f : 1f) * 18f;

        SetExpression(FaceExpressionType.Smiling, 4.0f);
        PlayBeckonChime();
        SpeakCustom(beckonLine, 3.2f);

        Debug.Log("[RustAndFloat] Rustの手招き＆呼びかけジェスチャー発動！");
        return true;
    }

    void PlayPanicBeep()
    {
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.85f;
            _audio.PlayOneShot(_happyBeepClip, 0.8f);
        }
    }

    void PlayReliefBeep()
    {
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.25f;
            _audio.PlayOneShot(_happyBeepClip, 0.65f);
        }
    }

    void PlaySoftChirp()
    {
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.15f;
            _audio.PlayOneShot(_happyBeepClip, 0.4f);
        }
    }

    void PlaySurpriseBeep()
    {
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.65f;
            _audio.PlayOneShot(_happyBeepClip, 0.75f);
        }
    }
    #endregion

    /// <summary>
    /// 歓喜の宙返り（Celebrating）用の高精度シネマティック回転とボビングオフセット
    /// </summary>
    Quaternion GetCelebrationRotation(Quaternion baseLook)
    {
        float p = 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(0.01f, _celebrationTotalDuration));

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
