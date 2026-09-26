using UnityEngine;
using System.Collections;

/// <summary>
/// クライマックス位置・FX・寄り添い（AdventureRustDrone の partial）
/// </summary>
public partial class AdventureRustDrone
{
    #region Climax motion & FX

    void TickClimaxFallAway()
    {
        const float maxDistFromChest = 3.2f;
        _climaxFallVel += Vector3.down * 3.2f * Time.deltaTime;
        if (_lookAt != null)
        {
            Vector3 cling = EndingBesideNiko(0.55f, 0.9f, -0.15f);
            Vector3 pull = cling - transform.position;
            _climaxFallVel += pull * 1.2f * Time.deltaTime;
        }
        _climaxFallVel = Vector3.ClampMagnitude(_climaxFallVel, 4.5f);
        transform.position += _climaxFallVel * Time.deltaTime;
        _lagTarget = transform.position;
        _velocity = _climaxFallVel;

        if (_lookAt != null)
        {
            Vector3 chest = GetNikoChestPosition();
            Vector3 delta = transform.position - chest;
            if (delta.magnitude > maxDistFromChest)
                transform.position = chest + delta.normalized * maxDistFromChest;
        }

        if (Time.unscaledTime >= _climaxFallUntil)
            _climaxFallVel = Vector3.Lerp(_climaxFallVel, Vector3.down * 0.35f, 0.12f);

        transform.position += new Vector3(
            Mathf.Sin(Time.unscaledTime * 38f) * 0.04f,
            Mathf.Sin(Time.unscaledTime * 45f) * 0.03f,
            Mathf.Cos(Time.unscaledTime * 33f) * 0.04f);

        Vector3 faceDir = _lookAt != null
            ? (GetNikoChestPosition() - transform.position)
            : Vector3.forward;
        if (faceDir.sqrMagnitude < 0.001f) faceDir = Vector3.forward;
        Quaternion tumble = Quaternion.LookRotation(faceDir.normalized);
        tumble *= Quaternion.Euler(
            Mathf.Sin(Time.unscaledTime * 22f) * 18f,
            Mathf.Sin(Time.unscaledTime * 19f) * 22f,
            Mathf.Cos(Time.unscaledTime * 17f) * 16f);
        transform.rotation = Quaternion.Slerp(transform.rotation, tumble, 8f * Time.deltaTime);
    }

    /// <summary>全力セリフ中：肩そばだが体に食い込まない（胸から外側＋カメラ手前）</summary>
    Vector3 GetOverdriveShoulderNestle()
    {
        Vector3 nest = VisibleBesideNikoOnScreen(0.95f, 0.62f, 0.30f);
        if (_lookAt == null) return nest;

        // 水平距離が足りないと本体メッシュが体に刺さるので下限を確保
        Vector3 chest = GetNikoChestPosition();
        Vector3 flat = nest - chest;
        flat.y = 0f;
        const float minFlat = 1.05f;
        if (flat.sqrMagnitude < minFlat * minFlat)
        {
            Vector3 dir = flat.sqrMagnitude > 0.01f ? flat.normalized : _lookAt.right;
            nest = chest + dir * minFlat + Vector3.up * (nest.y - chest.y);
        }
        return nest;
    }

    /// <summary>カメラから見て必ず画面内：Nikoの右隣＋カメラ寄り（後ろに隠れない）</summary>
    Vector3 VisibleBesideNikoOnScreen(float side = 0.75f, float towardCam = 0.55f, float lift = 0.12f)
    {
        if (_lookAt == null)
            return transform.position;

        Vector3 chest = GetNikoChestPosition();
        Camera cam = Camera.main;
        Vector3 sideDir = _lookAt.right;
        Vector3 toCam = -_lookAt.forward;
        if (cam != null)
        {
            sideDir = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
            if (sideDir.sqrMagnitude < 0.01f) sideDir = _lookAt.right;
            else sideDir.Normalize();
            toCam = Vector3.ProjectOnPlane(cam.transform.position - chest, Vector3.up);
            if (toCam.sqrMagnitude < 0.01f)
                toCam = -Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (toCam.sqrMagnitude < 0.01f) toCam = -_lookAt.forward;
            else toCam.Normalize();
        }
        return chest + sideDir * side + toCam * towardCam + Vector3.up * lift;
    }

    Vector3 EndingBesideNiko(float forward, float lateral, float lift)
    {
        if (_lookAt == null)
            return transform.position;

        Vector3 anchor = GetNikoChestPosition();
        Vector3 side = _lookAt.right;
        Vector3 fwd = _lookAt.forward;
        Camera cam = Camera.main;
        if (cam != null)
        {
            side = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = _lookAt.right;
            else side.Normalize();
            fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = _lookAt.forward;
            else fwd.Normalize();
        }
        return anchor + fwd * forward + side * lateral + Vector3.up * lift;
    }

    /// <summary>天蓋ボード寄り添い用（頭高さ）</summary>
    Vector3 EndingNestleAtHead(float forward, float lateral, float lift)
    {
        if (_lookAt == null)
            return transform.position;

        Vector3 head = GetNikoHeadPosition();
        Vector3 side = _lookAt.right;
        Vector3 fwd = _lookAt.forward;
        Camera cam = Camera.main;
        if (cam != null)
        {
            side = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = _lookAt.right;
            else side.Normalize();
            fwd = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = _lookAt.forward;
            else fwd.Normalize();
        }
        return head + fwd * forward + side * lateral + Vector3.up * lift;
    }

    // ── 【クライマックス専用ステート＆演出】 ──
    public bool IsClimaxCrisis { get; private set; } = false;
    public bool IsClimaxOverdrive { get; private set; } = false;
    bool _climaxHealing;
    /// <summary>天蓋ボード〜ダイブ中：Nikoのそば（胸高・クリアランス確保）に寄り添う</summary>
    bool _skybreakNestle;
    /// <summary>極寒セリフ中：しがみつきから落ちていく</summary>
    bool _climaxFalling;
    Vector3 _climaxFallVel;
    float _climaxFallUntil;
    ParticleSystem _climaxIceFx;
    ParticleSystem _climaxSparkFx;
    ParticleSystem _climaxJetFx;
    ParticleSystem _climaxHealFx;
    TrailRenderer _climaxTrail;
    Light _climaxEyeLight;
    Color _savedEmission = Color.black;

    /// <summary>天蓋開放〜エンディング：Nikoのそば（カメラから見える位置）に常時寄り添う</summary>
    public void StartSkybreakNestle()
    {
        _skybreakNestle = true;
        // 危機／注油中もフラグは維持（FollowPointで寄り添い優先）
        CurrentState = RustState.Petting;
        _stateTimer = 9999f;
        ClearSpeech();

        if (_lookAt == null)
        {
            var niko = AdventurePlayerController.Instance;
            if (niko != null) _lookAt = niko.transform;
        }

        if (_lookAt != null)
        {
            if (!_bonesCached) CacheNikoBones();
            Vector3 nest = FollowPoint();
            transform.position = nest;
            _lagTarget = nest;
            _velocity = Vector3.zero;
            Vector3 face = GetNikoChestPosition() + Vector3.up * 0.15f - nest;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(face.normalized);
        }
    }

    public void StopSkybreakNestle()
    {
        _skybreakNestle = false;
        if (CurrentState == RustState.Petting && !IsClimaxCrisis && !_climaxHealing && !_prologueDistress)
        {
            CurrentState = RustState.Follow;
            _stateTimer = 0f;
        }
        if (_lookAt != null && !IsClimaxCrisis && !_climaxHealing && !_prologueDistress)
        {
            Vector3 safe = FollowPoint();
            transform.position = safe;
            _lagTarget = safe;
            _velocity = Vector3.zero;
        }
    }

    void EnsureLookAtCached()
    {
        if (_lookAt == null)
        {
            // AdventurePlayerController.Instance を最優先（毎回Findしない）
            var niko = AdventurePlayerController.Instance;
            if (niko != null) _lookAt = niko.transform;
        }
        if (_lookAt != null && !_bonesCached)
            CacheNikoBones();
    }

    void ClearClimaxFallState()
    {
        _climaxFalling = false;
        _climaxFallVel = Vector3.zero;
    }

    /// <summary>クライマックス：警告時点ではまだそば。氷FXとしがみつき開始</summary>
    public void StartClimaxCrisis()
    {
        _skybreakNestle = false; // 危機演出へ移行（胸元しがみつき）
        ClearClimaxFallState();
        IsClimaxCrisis = true;
        IsClimaxOverdrive = false;
        _climaxHealing = false;
        CurrentState = RustState.Petting;
        _stateTimer = 999f;
        wellOiledUntil = 0f;
        _heat = 0f;

        EnsureLookAtCached();
        if (_lookAt != null)
        {
            if (!_bonesCached) CacheNikoBones();
            Vector3 nest = VisibleBesideNikoOnScreen(0.78f, 0.58f, 0.14f);
            transform.position = nest;
            _lagTarget = nest;
            _velocity = Vector3.zero;
            Vector3 face = GetNikoChestPosition() + Vector3.up * 0.2f - nest;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(face.normalized);
        }

        if (_bodyMat != null)
        {
            _savedEmission = _bodyMat.GetColor("_EmissionColor");
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", new Color(0.15f, 0.35f, 0.55f) * 0.4f);
        }

        SpawnClimaxIceFx();
        SpawnClimaxSparkFx();
        PlayCreak(true);
    }

    /// <summary>外の気流が冷たすぎる…！しがみつきから力なく落ちていく</summary>
    public void BeginClimaxColdFallAway()
    {
        if (_climaxHealing || IsClimaxOverdrive) return;
        IsClimaxCrisis = true;
        _climaxFalling = true;
        _climaxFallUntil = Time.unscaledTime + 2.8f;
        CurrentState = RustState.Petting;
        _stateTimer = 9999f;

        EnsureLookAtCached();
        if (_lookAt == null) return;

        Vector3 nest = VisibleBesideNikoOnScreen(0.95f, 0.35f, -0.15f);
        Vector3 push = nest - transform.position;
        _climaxFallVel = push.normalized * 2.2f + Vector3.down * 2.0f;
        if (_climaxFallVel.sqrMagnitude < 0.01f)
            _climaxFallVel = Vector3.down * 2f + _lookAt.right * 1.5f;
        _velocity = _climaxFallVel;

        SpawnClimaxIceFx();
        SpawnClimaxSparkFx();
        PlayCreak(true);
        if (_audio != null && _sonarBeepClip != null)
        {
            _audio.pitch = 0.7f;
            _audio.PlayOneShot(_sonarBeepClip, 0.45f);
        }
    }

    /// <summary>F9再演用：危機／注油／オーバードライブ状態を通常へ戻す</summary>
    public void ResetClimaxState()
    {
        IsClimaxCrisis = false;
        IsClimaxOverdrive = false;
        _climaxHealing = false;
        _skybreakNestle = false;
        ClearClimaxFallState();
        _prologueDistress = false;
        ClearSpeech();

        if (_climaxIceFx != null) _climaxIceFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_climaxSparkFx != null) _climaxSparkFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_climaxHealFx != null) _climaxHealFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_climaxJetFx != null) _climaxJetFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        DestroyClimaxTrail();
        if (_climaxEyeLight != null) _climaxEyeLight.enabled = false;

        if (_bodyMat != null)
        {
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", _savedEmission);
        }

        if (CurrentState == RustState.Petting)
        {
            CurrentState = RustState.Follow;
            _stateTimer = 0f;
        }
        if (_lookAt != null)
        {
            Vector3 safe = FollowPoint();
            transform.position = safe;
            _lagTarget = safe;
            _velocity = Vector3.zero;
        }
    }

    /// <summary>Nikoに抱きとめられ、最後の油を注がれる瞬間の演出</summary>
    public void StartClimaxPetAndOil()
    {
        _skybreakNestle = false;
        ClearClimaxFallState();
        IsClimaxCrisis = false;
        _climaxHealing = true;
        IsClimaxOverdrive = false;
        CurrentState = RustState.Petting;
        _stateTimer = 9999f;

        EnsureLookAtCached();
        if (_lookAt != null)
        {
            if (!_bonesCached) CacheNikoBones();
            Vector3 nest = FollowPoint();
            transform.position = nest;
            _lagTarget = nest;
            _velocity = Vector3.zero;
        }

        SpawnGoldSparkles(transform.position, 48);
        SpawnClimaxHealAura();

        if (_climaxIceFx != null) _climaxIceFx.Stop();
        if (_climaxSparkFx != null) _climaxSparkFx.Stop();

        if (_bodyMat != null)
        {
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", new Color(1.0f, 0.82f, 0.25f) * 2.8f);
        }
        EnsureClimaxEyeLight(new Color(1f, 0.9f, 0.45f), 2.8f);

        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.0f;
            _audio.PlayOneShot(_happyBeepClip, 0.6f);
        }
    }

    /// <summary>魂の再点火！超高出力オーバードライブに突入</summary>
    public void TriggerClimaxOverdrive()
    {
        _skybreakNestle = false;
        ClearClimaxFallState();
        IsClimaxCrisis = false;
        _climaxHealing = false;
        IsClimaxOverdrive = true;
        CurrentState = RustState.Petting; // Followだと追従ラグで肩に届かない
        _stateTimer = 9999f;
        wellOiledUntil = Time.time + 9999f;
        oilCount = Mathf.Max(oilCount, 5);
        _heat = 0f;
        _heatUntil = 0f;
        _hitchUntil = 0f;

        EnsureLookAtCached();
        if (_lookAt != null)
        {
            if (!_bonesCached) CacheNikoBones();
            Vector3 nest = GetOverdriveShoulderNestle();
            transform.position = nest;
            _lagTarget = nest;
            _velocity = Vector3.zero;
            Vector3 away = nest - GetNikoChestPosition();
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f) away.Normalize();
            else away = _lookAt.right;
            Vector3 face = (GetNikoHeadPosition() + away * 0.35f) - nest;
            if (face.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(face.normalized);
        }

        // 関門調整前：暖色の全力発光＋シンプルな金ジェット
        if (_bodyMat != null)
        {
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", new Color(1.45f, 1.05f, 0.35f) * 3.2f);
        }
        EnsureClimaxEyeLight(new Color(1f, 0.88f, 0.45f), 5.8f);

        if (_climaxHealFx != null)
            _climaxHealFx.Stop();
        if (_climaxIceFx != null)
            _climaxIceFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (_climaxSparkFx != null)
            _climaxSparkFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        DestroyClimaxTrail();
        SpawnClimaxJetFx();

        // 「ピピッ！」に合わせて復活チャイム
        PlayPipiRevivalChime();
    }

    /// <summary>全出力セリフ冒頭の「ピピッ！」用チャイム（二連ビープ＋明るい和音）</summary>
    public void PlayPipiRevivalChime()
    {
        if (_audio == null)
            SetupAudio();
        if (_audio == null) return;

        if (_pipiChimeClip == null)
            _pipiChimeClip = MakePipiChime();

        float savedPitch = _audio.pitch;
        _audio.pitch = 1f;
        if (_pipiChimeClip != null)
            _audio.PlayOneShot(_pipiChimeClip, 0.9f);
        if (_happyBeepClip != null)
            _audio.PlayOneShot(_happyBeepClip, 0.55f);
        _audio.pitch = savedPitch;

        // パーツ回収と同系のヒーリングチャイムも重ねて祝福感を出す
        AdventureScrapManager.Instance?.PlayCelebrationChime(0.32f);
    }

    /// <summary>ピ・ピッ の二連電子音＋短い高音チャイム</summary>
    static AudioClip MakePipiChime()
    {
        const int hz = 44100;
        float duration = 0.55f;
        int samples = (int)(hz * duration);
        float[] data = new float[samples];

        void AddBeep(float startSec, float dur, float f0, float f1, float amp)
        {
            int start = Mathf.FloorToInt(startSec * hz);
            int len = Mathf.FloorToInt(dur * hz);
            float phase = 0f;
            for (int i = 0; i < len; i++)
            {
                int idx = start + i;
                if (idx < 0 || idx >= samples) continue;
                float t = i / (float)Mathf.Max(1, len - 1);
                float freq = Mathf.Lerp(f0, f1, t);
                phase += 2f * Mathf.PI * freq / hz;
                float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                // ソフトアタック
                if (t < 0.08f) env *= t / 0.08f;
                data[idx] += Mathf.Sin(phase) * env * amp;
            }
        }

        // ピ（短）・ピッ（少し長め上昇）
        AddBeep(0.00f, 0.09f, 980f, 1180f, 0.42f);
        AddBeep(0.12f, 0.16f, 1200f, 1560f, 0.48f);
        // 明るい和音の余韻
        AddBeep(0.22f, 0.30f, 784f, 784f, 0.18f);   // G5
        AddBeep(0.24f, 0.28f, 988f, 988f, 0.16f);   // B5
        AddBeep(0.26f, 0.26f, 1319f, 1319f, 0.14f); // E6

        for (int i = 0; i < samples; i++)
            data[i] = Mathf.Clamp(data[i], -1f, 1f);

        var clip = AudioClip.Create("RustPipiChime", samples, 1, hz, false);
        clip.SetData(data, 0);
        return clip;
    }

    void SnapBesideNiko(bool healingNestle)
    {
        if (_lookAt == null) return;
        Vector3 nest = healingNestle
            ? VisibleBesideNikoOnScreen(0.95f, 0.62f, 0.22f)
            : VisibleBesideNikoOnScreen(0.78f, 0.58f, 0.14f);

        transform.position = nest;
        _lagTarget = nest;
        _velocity = Vector3.zero;
        Vector3 face = GetNikoChestPosition() + Vector3.up * 0.2f - nest;
        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(face.normalized);
    }

    void KeepRustOnScreenNearNiko(bool force = false)
    {
        if (_lookAt == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 safe = IsClimaxOverdrive
            ? GetOverdriveShoulderNestle()
            : VisibleBesideNikoOnScreen(
                _climaxHealing ? 0.95f : 0.78f,
                _climaxHealing ? 0.62f : 0.58f,
                _climaxHealing ? 0.22f : 0.14f);

        if (!force)
        {
            Vector3 sp = cam.WorldToViewportPoint(transform.position);
            bool off =
                sp.z < 0.35f
                || sp.x < 0.12f || sp.x > 0.88f
                || sp.y < 0.15f || sp.y > 0.85f;
            if (!off) return;
            transform.position = Vector3.Lerp(transform.position, safe, 0.55f);
        }
        else
        {
            // 危機／全力中は毎フレーム画面内スロットへ強く吸着
            transform.position = Vector3.Lerp(transform.position, safe, 0.72f);
        }
        _lagTarget = transform.position;
        _velocity *= 0.35f;
    }

    void SpawnClimaxIceFx()
    {
        if (_climaxIceFx != null)
        {
            _climaxIceFx.Play();
            return;
        }
        var go = new GameObject("Rust_ClimaxIceFx");
        go.transform.SetParent(transform, false);
        _climaxIceFx = go.AddComponent<ParticleSystem>();
        var main = _climaxIceFx.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 1.1f;
        main.startSpeed = 0.55f;
        main.startSize = 0.28f;
        main.startColor = new Color(0.72f, 0.92f, 1.0f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = _climaxIceFx.emission;
        emission.rateOverTime = 38f;
        var shape = _climaxIceFx.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.35f;
        ApplySoftParticleMaterial(go, new Color(0.7f, 0.9f, 1f, 0.45f));
    }

    void SpawnClimaxSparkFx()
    {
        if (_climaxSparkFx != null)
        {
            _climaxSparkFx.Play();
            return;
        }
        var go = new GameObject("Rust_ClimaxSparkFx");
        go.transform.SetParent(transform, false);
        _climaxSparkFx = go.AddComponent<ParticleSystem>();
        var main = _climaxSparkFx.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 0.25f;
        main.startSpeed = 3.8f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.55f, 0.85f, 1f, 1f);
        main.gravityModifier = 0.4f;
        var emission = _climaxSparkFx.emission;
        emission.rateOverTime = 22f;
        var shape = _climaxSparkFx.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;
        ApplySoftParticleMaterial(go, new Color(0.55f, 0.85f, 1f, 1f));
    }

    void SpawnClimaxHealAura()
    {
        if (_climaxHealFx != null)
        {
            _climaxHealFx.Play();
            return;
        }
        var go = new GameObject("Rust_ClimaxHealAura");
        go.transform.SetParent(transform, false);
        _climaxHealFx = go.AddComponent<ParticleSystem>();
        var main = _climaxHealFx.main;
        main.duration = 2f;
        main.loop = true;
        main.startLifetime = 1.4f;
        main.startSpeed = 0.35f;
        main.startSize = 0.12f;
        main.startColor = new Color(1f, 0.85f, 0.35f, 0.9f);
        var emission = _climaxHealFx.emission;
        emission.rateOverTime = 55f;
        var shape = _climaxHealFx.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;
        ApplySoftParticleMaterial(go, new Color(1f, 0.85f, 0.35f, 0.9f));
    }

    void SpawnClimaxJetFx()
    {
        // 実験用の変な設定が残らないよう毎回作り直す
        if (_climaxJetFx != null)
        {
            Destroy(_climaxJetFx.gameObject);
            _climaxJetFx = null;
        }
        var go = new GameObject("Rust_ClimaxJetFx");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.05f, -0.32f);
        go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
        _climaxJetFx = go.AddComponent<ParticleSystem>();
        var main = _climaxJetFx.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = 0.35f;
        main.startSpeed = 12f;
        main.startSize = 0.22f;
        main.startColor = new Color(1f, 0.92f, 0.55f, 0.92f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        var emission = _climaxJetFx.emission;
        emission.rateOverTime = 90f;
        var shape = _climaxJetFx.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f;
        shape.radius = 0.05f;
        ApplySoftParticleMaterial(go, new Color(1f, 0.88f, 0.42f, 0.9f));
        _climaxJetFx.Play();
    }

    void EnsureClimaxTrail()
    {
        if (_climaxTrail != null)
        {
            _climaxTrail.emitting = true;
            return;
        }
        _climaxTrail = gameObject.AddComponent<TrailRenderer>();
        _climaxTrail.time = 0.55f;
        _climaxTrail.startWidth = 0.28f;
        _climaxTrail.endWidth = 0.02f;
        _climaxTrail.minVertexDistance = 0.08f;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                 ?? Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Sprites/Default");
        if (sh != null)
        {
            var mat = new Material(sh);
            mat.SetColor("_BaseColor", new Color(0.3f, 0.95f, 1f, 0.85f));
            mat.color = new Color(0.3f, 0.95f, 1f, 0.85f);
            _climaxTrail.material = mat;
        }
        _climaxTrail.startColor = new Color(0.4f, 1f, 1f, 0.9f);
        _climaxTrail.endColor = new Color(0.2f, 0.6f, 1f, 0f);
    }

    static void ApplySoftParticleMaterial(GameObject go, Color color)
    {
        var renderer = go != null ? go.GetComponent<ParticleSystemRenderer>() : null;
        if (renderer == null) return;
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                 ?? Shader.Find("Universal Render Pipeline/Unlit")
                 ?? Shader.Find("Sprites/Default")
                 ?? Shader.Find("Particles/Standard Unlit");
        if (sh == null) return;
        var mat = new Material(sh);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        var tex = GetSoftSmokeTexture();
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_MainTex", tex);
        }
        renderer.sharedMaterial = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    void DestroyClimaxTrail()
    {
        if (_climaxTrail == null)
            _climaxTrail = GetComponent<TrailRenderer>();
        if (_climaxTrail != null)
        {
            _climaxTrail.emitting = false;
            Destroy(_climaxTrail);
            _climaxTrail = null;
        }
    }

    void EnsureClimaxEyeLight(Color color, float intensity)
    {
        if (_climaxEyeLight == null)
        {
            var go = new GameObject("Rust_ClimaxEyeLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, 0.35f);
            _climaxEyeLight = go.AddComponent<Light>();
            _climaxEyeLight.type = LightType.Point;
            _climaxEyeLight.range = 4.5f;
        }
        _climaxEyeLight.color = color;
        _climaxEyeLight.intensity = intensity;
        _climaxEyeLight.enabled = true;
    }

    #endregion
}
