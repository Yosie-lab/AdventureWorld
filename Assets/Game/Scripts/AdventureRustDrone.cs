using UnityEngine;
using System.Collections;
using System.Linq;

public class AdventureRustDrone : MonoBehaviour
{
    public float hoverHeight = 1.35f;
    public float bobAmount = 0.1f;
    public float bobSpeed = 1.35f;
    public float followDistance = 2.1f;
    public float stopDistance = 1.25f;
    [Range(0f, 1f)] public float soundVolume = 0.28f;

    Transform _lookAt;
    Transform _body;
    Terrain _land;
    Vector3 _velocity;
    Vector3 _lagTarget;
    AudioSource _audio;
    AudioClip[] _creaks;
    ParticleSystem _heatFx;
    Material _bodyMat;
    Material _oilMat;
    float _hitchUntil;
    float _heatUntil;
    float _nextHitch;
    float _nextCreak;
    float _heat;
    bool _wasHitching;

    // スクラップ収集・アップグレード対話
    string _speechText = "";
    float _speechTimer = 0f;
    bool _waitingForPlayerAction = false;
    float _speechShowTime = 0f;
    Vector3 _speechPlayerStartPos = Vector3.zero;
    AudioClip _happyBeepClip;
    AudioClip _sonarBeepClip;
    float _sonarTimer = 0f;
    GUIStyle _speechStyle;
    Texture2D _speechBg;

    // オイルアイテムと手当てシステム
    public int oilCount = 2;
    public float wellOiledUntil = 0f;
    bool _isPlayerNear = false;
    float _lastInteractTime = 0f;

    // 探索アシスト（近くの未発見パーツへの誘導・合図）
    AdventureScrapItem _guidedScrap;
    float _nextGuideNotice = 0f;
    bool _isPointingToScrap = false;

    // 連携アクション（Fキー指示・遠隔回収・偵察・宙返り・撫でスキンシップ）
    public enum RustState { Follow, Fetching, Returning, Scouting, Celebrating, Petting }
    public RustState CurrentState { get; private set; } = RustState.Follow;
    AdventureScrapItem _targetScrap;
    Vector3 _scoutTargetPos;
    float _stateTimer = 0f;
    AdventureScrapItem _aimedScrap;

    public void SetPettingState(bool active, float duration)
    {
        if (active)
        {
            CurrentState = RustState.Petting;
            _stateTimer = duration;
            _velocity = Vector3.zero; // 物理的な跳ね上がりや落下速度をクリアし、胸元へスムーズに引き寄せる
        }
        else
        {
            if (CurrentState == RustState.Petting)
                CurrentState = RustState.Follow;
        }
    }

    public static AdventureRustDrone Instance { get; private set; }

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<AdventureRustDrone>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var niko = GameObject.Find("Niko");
        Vector3 spawnPos = niko != null ? niko.transform.position + niko.transform.right * 1.5f + Vector3.up * 1.2f : new Vector3(266f, 49.5f, 331f);

        GameObject droneGo = null;
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RustAndFloat/Prefabs/Rust.prefab");
        if (prefab != null)
        {
            droneGo = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
            droneGo.name = "Rust";
        }
#endif
        if (droneGo == null)
        {
            droneGo = new GameObject("Rust");
            droneGo.transform.position = spawnPos;
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Body";
            body.transform.SetParent(droneGo.transform, false);
            body.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        Instance = droneGo.GetComponent<AdventureRustDrone>() ?? droneGo.AddComponent<AdventureRustDrone>();
    }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        var niko = GameObject.Find("Niko");
        if (niko != null)
            _lookAt = niko.transform;

        _land = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude)
            .FirstOrDefault(t => t.name == "LandTerrain" || t.name == "IslandTerrain");
        _lagTarget = transform.position;
        _nextHitch = Time.time + 2.2f;
        SetupAudio();
        SetupHeat();
        SetupOil();
        oilCount = Mathf.Max(oilCount, 2); // ゲーム開始時に確実に2個以上油を所持

        // 起動時のあたたかい挨拶
        SetSpeech("ピピッ…！起動したよ、Niko。一緒に行こう！", 4.5f);
        _nextIdleTalk = Time.time + 20f;
    }

    float _nextIdleTalk;

    void Update()
    {
        if (_lookAt == null)
            return;

        UpdateCommandInput();

        Vector3 goal;
        bool wellOiled = Time.time < wellOiledUntil;
        bool hitching = !wellOiled && Time.time < _hitchUntil;

        if (CurrentState == RustState.Fetching)
        {
            if (_targetScrap == null || _targetScrap.IsCollected)
            {
                CurrentState = RustState.Follow;
                goal = FollowPoint();
            }
            else
            {
                goal = _targetScrap.transform.position + Vector3.up * 0.45f;
                float distToTarget = Vector3.Distance(transform.position, goal);
                if (distToTarget < 2.0f)
                {
                    _targetScrap.AttachToDrone(transform);
                    CurrentState = RustState.Returning;
                    SpeakCustom("キャッチしたよ！Nikoのところへ持ってくね！", 3.0f);
                    if (_audio != null && _happyBeepClip != null)
                    {
                        _audio.pitch = 1.4f;
                        _audio.PlayOneShot(_happyBeepClip, 0.8f);
                    }
                }
            }
        }
        else if (CurrentState == RustState.Returning)
        {
            goal = _lookAt.position + Vector3.up * 1.1f + _lookAt.forward * 1.0f;
            float distToNiko = Vector3.Distance(transform.position, _lookAt.position + Vector3.up * 1.0f);
            if (distToNiko < 1.85f)
            {
                if (_targetScrap != null)
                {
                    _targetScrap.Collect();
                    _targetScrap = null;
                }
                CurrentState = RustState.Celebrating;
                _stateTimer = 1.6f;

                if (AdventurePettingAction.Instance != null)
                {
                    AdventurePettingAction.Instance.PetRust("すごいよRust！取ってきてくれてありがとう！", 2.2f);
                }
                else
                {
                    SpeakCustom("えへへ、お届け完了！", 3.0f);
                }

                if (_audio != null && _happyBeepClip != null)
                {
                    _audio.pitch = 1.55f;
                    _audio.PlayOneShot(_happyBeepClip, 0.85f);
                }
            }
        }
        else if (CurrentState == RustState.Scouting)
        {
            goal = _scoutTargetPos + Vector3.up * 1.2f;
            float dist = Vector3.Distance(transform.position, goal);
            if (dist < 1.8f)
            {
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                {
                    CurrentState = RustState.Follow;
                    SpeakCustom("偵察完了！周囲に危険はないよ！", 3.0f);
                }
            }
        }
        else if (CurrentState == RustState.Celebrating)
        {
            goal = _lookAt.position + Vector3.up * 1.35f + _lookAt.right * 1.2f;
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
                CurrentState = RustState.Follow;
        }
        else if (CurrentState == RustState.Petting)
        {
            // Rustの全幅（直径約1.2m、半径約0.6m）とNikoの腕の位置を考慮し、
            // 腕に一切干渉しない胸の真正面0.95m・胸骨〜鎖骨の高さに配置
            Vector3 chestPos = GetNikoChestPosition();
            goal = chestPos + _lookAt.forward * 0.95f + Vector3.up * 0.08f;
            goal.y += Mathf.Sin(Time.time * 3.5f) * 0.035f; // 胸元でのふんわりホバー

            _lagTarget = goal; // 遅延によるオーバーシュート（めり込み）を防止

            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0f)
                CurrentState = RustState.Follow;
        }
        else // Follow
        {
            goal = FollowPoint();
            if (!wellOiled && !hitching && Time.time >= _nextHitch && FlatDistance(goal) > 2.4f)
            {
                _hitchUntil = Time.time + Random.Range(0.22f, 0.5f);
                _nextHitch = Time.time + Random.Range(3.5f, 6.5f);
                hitching = true;
                PlayCreak(true);
                BeginHeatBurst();
            }
        }

        if (hitching && CurrentState == RustState.Follow)
        {
            goal.x = transform.position.x;
            goal.z = transform.position.z;
        }

        _lagTarget = Vector3.Lerp(_lagTarget, goal, 1f - Mathf.Exp(-2.2f * Time.deltaTime));
        float smoothTime = (CurrentState == RustState.Fetching || CurrentState == RustState.Returning || CurrentState == RustState.Petting) ? 0.24f : (wellOiled ? 0.38f : 0.52f);
        transform.position = Vector3.SmoothDamp(transform.position, _lagTarget, ref _velocity, smoothTime, 8.5f);

        // 回転の計算
        Vector3 to = goal - transform.position;
        if (CurrentState == RustState.Follow)
        {
            to = _lookAt.position + Vector3.up * 0.7f - transform.position;
            if (_isPointingToScrap && _guidedScrap != null)
                to = _guidedScrap.transform.position + Vector3.up * 0.3f - transform.position;
        }
        else if (CurrentState == RustState.Returning)
        {
            to = _lookAt.position + Vector3.up * 1.35f - transform.position;
        }
        else if (CurrentState == RustState.Petting)
        {
            to = (GetNikoChestPosition() + Vector3.up * 0.32f) - transform.position; // Nikoの顔を見上げる
        }

        if (to.sqrMagnitude > 0.04f)
        {
            Quaternion look = Quaternion.LookRotation(to);
            if (CurrentState == RustState.Celebrating)
            {
                // 嬉しい宙返り回転！
                look *= Quaternion.Euler(Time.time * 720f, 0f, 0f);
            }
            else if (CurrentState == RustState.Petting)
            {
                // 胸元ですり寄る甘えチルト
                look *= Quaternion.Euler(-10f + Mathf.Sin(Time.time * 4f) * 4f, 0f, 16f);
            }
            else if (hitching)
                look *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 18f) * 8f, 0f);
            else if (_isPointingToScrap && CurrentState == RustState.Follow)
                look *= Quaternion.Euler(Mathf.Sin(Time.time * 10f) * 6f, 0f, Mathf.Cos(Time.time * 8f) * 4f);

            transform.rotation = Quaternion.Slerp(transform.rotation, look, 6.5f * Time.deltaTime);
        }

        if (!hitching && _velocity.sqrMagnitude > 6f && !wellOiled && CurrentState == RustState.Follow)
            PlayCreak(false);

        UpdateHeat(hitching);
        if (_wasHitching && !hitching)
            DripOil();
        _wasHitching = hitching;

        UpdateGuide();
        UpdatePlayerInteraction();
        UpdateSpeech();
        UpdateSonar();
    }

    void UpdateCommandInput()
    {
        // 照準先のスクラップを探す
        _aimedScrap = null;
        var cam = Camera.main;
        if (cam != null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var scraps = FindObjectsByType<AdventureScrapItem>(FindObjectsInactive.Exclude);
            float bestDot = 0.88f; // 視野角約30度以内
            float maxDist = 38f;

            foreach (var s in scraps)
            {
                if (s == null || s.IsCollected) continue;
                Vector3 toScrap = s.transform.position - cam.transform.position;
                float d = toScrap.magnitude;
                if (d < maxDist)
                {
                    float dot = Vector3.Dot(ray.direction, toScrap.normalized);
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        _aimedScrap = s;
                    }
                }
            }
        }

        // Fキー（New Input Systemによる安全な検知）
        bool fPressed = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            fPressed = kb.fKey.wasPressedThisFrame;
        }

        if (fPressed && CurrentState == RustState.Follow)
        {
            if (_aimedScrap != null)
            {
                // スクラップ回収を指示！
                _targetScrap = _aimedScrap;
                CurrentState = RustState.Fetching;
                SpeakCustom("了解！あのパーツを取ってくるね、Niko！", 3.2f);
                if (_audio != null && _happyBeepClip != null)
                {
                    _audio.pitch = 1.3f;
                    _audio.PlayOneShot(_happyBeepClip, 0.75f);
                }
            }
            else if (cam != null)
            {
                // 何もない場所を指差した場合は偵察指示
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                if (Physics.Raycast(ray, out RaycastHit hit, 35f))
                {
                    _scoutTargetPos = hit.point;
                    CurrentState = RustState.Scouting;
                    _stateTimer = 1.8f;
                    SpeakCustom("あそこを見に行ってみるよ！", 2.8f);
                    if (_audio != null && _happyBeepClip != null)
                    {
                        _audio.pitch = 1.2f;
                        _audio.PlayOneShot(_happyBeepClip, 0.65f);
                    }
                }
            }
        }
    }

    void UpdateGuide()
    {
        var mgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
        if (mgr == null || _lookAt == null)
        {
            _guidedScrap = null;
            _isPointingToScrap = false;
            return;
        }

        var nearest = mgr.GetNearestScrapItem(_lookAt.position, out float dist);
        // 35m以内のパーツを鋭敏に探知してプレイヤーに案内
        if (nearest != null && dist <= 35f && !nearest.IsCollected)
        {
            _guidedScrap = nearest;
            _isPointingToScrap = true;

            if (Time.time >= _nextGuideNotice)
            {
                _nextGuideNotice = Time.time + 12f;
                SetSpeech("ピピピッ！あそこにパーツの反応があるよ！", 3.8f);
                if (_audio != null && _happyBeepClip != null)
                {
                    _audio.pitch = 1.35f;
                    _audio.PlayOneShot(_happyBeepClip, 0.65f);
                }
            }
        }
        else
        {
            _guidedScrap = null;
            _isPointingToScrap = false;
        }
    }

    Vector3 FollowPoint()
    {
        Vector3 niko = _lookAt.position;
        Vector3 chest = GetNikoChestPosition();

        // クライマックス危機時：Nikoの両腕に抱きとめられる位置（胸の正面）
        if (IsClimaxCrisis)
        {
            return chest + _lookAt.forward * 0.42f - Vector3.up * 0.08f;
        }
        // クライマックス・オーバードライブ時：Nikoの右肩上に力強くドッキングして蒼炎噴射
        if (IsClimaxOverdrive)
        {
            return chest + _lookAt.right * 0.55f + Vector3.up * 0.35f - _lookAt.forward * 0.15f;
        }

        // 近くに未回収パーツがある場合、RustはNikoの少し前方（パーツ寄り）へ先行して合図
        if (_isPointingToScrap && _guidedScrap != null)
        {
            Vector3 toScrap = Vector3.ProjectOnPlane(_guidedScrap.transform.position - niko, Vector3.up).normalized;
            Vector3 guidePos = niko + toScrap * 1.8f;
            float sBob = Mathf.Sin(Time.time * bobSpeed * 1.6f) * (bobAmount * 1.2f);
            return new Vector3(guidePos.x, chest.y + 0.1f + sBob, guidePos.z);
        }

        // 通常追従の理想位置: Nikoの右肩の斜め後ろ（右0.95m、後方1.25m、腕に触れない快適クリアランス）
        Vector3 rightBack = _lookAt.right * 0.95f - _lookAt.forward * 1.25f;
        Vector3 targetPos = niko + rightBack;

        // Nikoの真正面（股間の前）にRustが入り込んで居座るのを防止
        Vector3 toDrone = transform.position - niko;
        float forwardDot = Vector3.Dot(_lookAt.forward, toDrone);
        if (FlatDistance(niko) < 1.4f && forwardDot > -0.2f)
        {
            // 目の前（股間・お腹の正面）にいる時は、積極的に右肩後ろのポジションへ誘導
            targetPos = niko + rightBack;
        }
        else if (FlatDistance(niko) < stopDistance)
        {
            // Nikoから適切な距離にいる時はその水平位置を維持
            targetPos = new Vector3(transform.position.x, 0f, transform.position.z);
        }

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        float y = chest.y + 0.05f + bob; // 常にNikoの胸・肩の高さに追従！

        return new Vector3(targetPos.x, y, targetPos.z);
    }

    float SurfaceY(Vector3 pos)
    {
        float landY = pos.y;
        if (_land != null)
            landY = _land.SampleHeight(pos) + _land.transform.position.y;

        var bounds = AdventureIslandBoundary.Instance;
        float water = bounds != null ? bounds.waterLevel : float.NegativeInfinity;
        return landY < water ? water : landY;
    }

    float FlatDistance(Vector3 target)
    {
        Vector3 a = transform.position;
        a.y = 0f;
        target.y = 0f;
        return Vector3.Distance(a, target);
    }

    void SetupAudio()
    {
        _audio = GetComponent<AudioSource>();
        if (_audio == null)
            _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 1f;
        _audio.minDistance = 1.5f;
        _audio.maxDistance = 22f;
        _audio.volume = soundVolume;
        _creaks = new[] { MakeCreak(11), MakeCreak(29), MakeCreak(47) };
        _happyBeepClip = MakeSynthBeep(880f, 1320f, 0.18f);
        _sonarBeepClip = MakeSynthBeep(1480f, 1100f, 0.22f);
    }

    void SetupHeat()
    {
        _body = transform.Find("Body");
        if (_body != null)
        {
            var rend = _body.GetComponent<Renderer>();
            if (rend != null)
            {
                _bodyMat = rend.material;
                _bodyMat.EnableKeyword("_EMISSION");
            }
        }

        var go = new GameObject("Heat");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.38f, 0f);
        _heatFx = go.AddComponent<ParticleSystem>();

        var main = _heatFx.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.32f, 0.65f);
        main.startColor = new Color(1f, 1f, 1f, 0.65f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = -0.16f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 16;
        main.scalingMode = ParticleSystemScalingMode.Local;

        var emission = _heatFx.emission;
        emission.rateOverTime = 0f;

        var shape = _heatFx.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 24f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var color = _heatFx.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.92f, 0.94f, 0.98f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),      // 発生時はスッとフェードイン
                new GradientAlphaKey(0.6f, 0.25f), // ピーク透明度
                new GradientAlphaKey(0f, 1f)       // 自然に消滅
            });
        color.color = grad;

        var size = _heatFx.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.8f));

        var rot = _heatFx.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);

        var noise = _heatFx.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.15f;
        noise.damping = true;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 1.5f;
        renderer.material = LoadHeatMaterial();
        _heatFx.Play();
    }

    void SetupOil()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard");
        _oilMat = new Material(shader);
        _oilMat.SetColor("_BaseColor", new Color(0.04f, 0.02f, 0.01f, 1f));
        _oilMat.SetColor("_Color", new Color(0.04f, 0.02f, 0.01f, 1f));
        _oilMat.SetFloat("_Metallic", 0.9f);
        _oilMat.SetFloat("_Smoothness", 0.92f);
        _oilMat.SetFloat("_Glossiness", 0.92f);
    }

    void DripOil()
    {
        int n = Random.Range(1, 3);
        for (int i = 0; i < n; i++)
            StartCoroutine(FallingOilDrop(i * 0.16f));
    }

    IEnumerator FallingOilDrop(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        drop.name = "OilDrop";
        var col = drop.GetComponent<Collider>();
        if (col != null)
            Destroy(col);
        drop.transform.position = transform.position + Vector3.down * 0.7f
            + new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f));
        drop.transform.localScale = Vector3.one * Random.Range(0.07f, 0.1f);
        var rend = drop.GetComponent<Renderer>();
        if (rend != null && _oilMat != null)
            rend.sharedMaterial = _oilMat;

        Vector3 vel = Vector3.down * 0.35f;
        float t = 0f;
        float groundY = SurfaceY(drop.transform.position);

        while (t < 2.4f && drop != null)
        {
            vel += Vector3.down * 9.8f * 0.55f * Time.deltaTime;
            drop.transform.position += vel * Time.deltaTime;
            if (drop.transform.position.y <= groundY + 0.08f)
            {
                // 地面に到達！
                drop.transform.position = new Vector3(drop.transform.position.x, groundY + 0.05f, drop.transform.position.z);
                break;
            }
            t += Time.deltaTime;
            yield return null;
        }

        if (drop != null)
        {
            // フィールド上のオイルが4個未満なら採取可能なアイテムとして残す
            int existingOils = Object.FindObjectsByType<AdventureRustOilDrop>(FindObjectsInactive.Exclude).Length;
            if (existingOils < 4)
            {
                var oilItem = new GameObject("RustOilDrop");
                oilItem.transform.position = drop.transform.position;
                oilItem.AddComponent<AdventureRustOilDrop>();
            }
            Destroy(drop);
        }
    }

    /// <summary>オイル採取時の通知</summary>
    public void AddOil(int amount)
    {
        oilCount += amount;
        SpeakCustom("✦ 潤滑油を採取した！（所持数: " + oilCount + "）", 3.2f);
        if (_happyBeepClip != null && _audio != null)
            _audio.PlayOneShot(_happyBeepClip, 0.45f);
    }

    void UpdatePlayerInteraction()
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

        // Rustの浮遊高さを考慮し、水平5.5m・高低差5.0mまで広角に接近検知
        Vector3 diff = transform.position - player.transform.position;
        float horizontalDist = new Vector2(diff.x, diff.z).magnitude;
        float verticalDist = Mathf.Abs(diff.y);
        _isPlayerNear = (horizontalDist < 5.5f && verticalDist < 5.0f);

        // キーボードEキー・パッド決定・PlayerController経由のいずれでも100%直接検知
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool ePressed = (kb != null && (kb.eKey.wasPressedThisFrame || kb.eKey.wasReleasedThisFrame))
                     || (pad != null && pad.buttonWest.wasPressedThisFrame)
                     || (player != null && player.InteractPressed);

        // レバーの近くにいる場合はレバー操作（天蓋開放）を最優先し、Rustの手当て割り込みを抑止
        var towerMgr = AdventureSanctuaryTowerManager.Instance;
        if (towerMgr != null && towerMgr.IsPlayerNearLever)
        {
            return;
        }

        if (_isPlayerNear && ePressed && Time.time - _lastInteractTime > 0.35f)
        {
            _lastInteractTime = Time.time;
            InteractWithNiko();
        }
    }

    /// <summary>Nikoとの直接対話または手当て（常備油により絶対に0にならず、いつでも手当て・全回復可能）</summary>
    void InteractWithNiko()
    {
        bool needsOil = (_heat > 0.15f || Time.time < _hitchUntil || Time.time > wellOiledUntil);

        // 油所持は常に最低1個を保証（常備オイル）
        oilCount = Mathf.Max(oilCount, 1);

        // 予備オイル（2個以上）があれば1個消費し、最後の1個（常備油）は消費せず大切に保持
        if (oilCount > 1)
        {
            oilCount--;
        }

        // いつでも手当て＆全快調化（90秒快調を付与、熱・きしみ・引っかかりを即時完全解消）
        wellOiledUntil = Mathf.Max(wellOiledUntil, Time.time) + 90f;
        _heatUntil = 0f;
        _heat = 0f;
        _hitchUntil = 0f;

        if (_happyBeepClip != null && _audio != null)
            _audio.PlayOneShot(_happyBeepClip, 0.45f);

        // NikoがRustを胸元で愛おしく撫でて手当て
        if (AdventurePettingAction.Instance != null)
        {
            string msg = needsOil 
                ? "よしよし、油をさしてピカピカに整備したよ！いつもありがとう、Rust" 
                : "いい子だね、Rust。いつでも一緒だよ！";
            AdventurePettingAction.Instance.PetRust(msg, 3.2f);
        }
        else
        {
            StartCoroutine(CheerSpinRoutine());
        }
        AdventureSaveManager.Instance?.SaveGame("SAVEしました");
    }

    // ── 【クライマックス専用ステート＆演出】 ──
    public bool IsClimaxCrisis { get; private set; } = false;
    public bool IsClimaxOverdrive { get; private set; } = false;
    ParticleSystem _climaxIceFx;
    ParticleSystem _climaxJetFx;

    /// <summary>クライマックス：天蓋目前でのRust機能停止・凍結危機を開始</summary>
    public void StartClimaxCrisis()
    {
        IsClimaxCrisis = true;
        CurrentState = RustState.Petting; // 通常追従から離脱
        wellOiledUntil = 0f;
        _heat = 0f;

        // 冷気・火花エフェクト噴射
        SpawnClimaxIceFx();

        // 悲痛なアラートセリフ
        SpeakCustom("キキキッ……！ Niko……外の気流が冷たすぎる……僕の古いギアが……凍りついて……", 4.5f);
        PlayCreak(true);
    }

    /// <summary>Nikoに抱きとめられ、最後の油を注がれる瞬間の演出</summary>
    public void StartClimaxPetAndOil()
    {
        // 黄金の治癒の光
        SpawnGoldSparkles(transform.position, 35);
        if (_climaxIceFx != null)
            _climaxIceFx.Stop();

        SpeakCustom("……あ……温かい油が……心臓に……！", 3.0f);
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.0f;
            _audio.PlayOneShot(_happyBeepClip, 0.6f);
        }
    }

    /// <summary>魂の再点火！超高出力オーバードライブに突入</summary>
    public void TriggerClimaxOverdrive()
    {
        IsClimaxCrisis = false;
        IsClimaxOverdrive = true;
        wellOiledUntil = Time.time + 9999f; // 永久快調
        oilCount = 0; // 最後の1個を注ぎ切った証

        // 眩しいエメラルドシアンの発光
        if (_bodyMat != null)
        {
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", new Color(0.2f, 1.8f, 2.0f) * 3.5f);
        }

        // 背後から蒼いプラズマジェット噴射
        SpawnClimaxJetFx();

        // 魂の叫び
        SpeakCustom("ピピッ！……ありがとうNiko！僕たちの翼は絶対に折れない！全出力で行くよ！！", 7.0f);

        if (_audio != null)
        {
            _audio.pitch = 1.45f;
            if (_happyBeepClip != null)
                _audio.PlayOneShot(_happyBeepClip, 1.0f);
        }
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
        main.startLifetime = 0.8f;
        main.startSpeed = 1.5f;
        main.startSize = 0.18f;
        main.startColor = new Color(0.6f, 0.9f, 1.0f, 0.8f);
        var emission = _climaxIceFx.emission;
        emission.rateOverTime = 25f;
    }

    void SpawnClimaxJetFx()
    {
        if (_climaxJetFx != null)
        {
            _climaxJetFx.Play();
            return;
        }
        var go = new GameObject("Rust_ClimaxJetFx");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.1f, -0.25f);
        _climaxJetFx = go.AddComponent<ParticleSystem>();
        var main = _climaxJetFx.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = 0.45f;
        main.startSpeed = 8.5f;
        main.startSize = 0.35f;
        main.startColor = new Color(0.2f, 0.85f, 1.0f, 0.95f);
        var emission = _climaxJetFx.emission;
        emission.rateOverTime = 60f;
    }

    void BeginHeatBurst()
    {
        _heatUntil = Time.time + 4f;
        if (_heatFx != null)
            _heatFx.Emit(2);
    }

    void UpdateHeat(bool hitching)
    {
        if (hitching || _velocity.sqrMagnitude > 3f)
            _heatUntil = Mathf.Max(_heatUntil, Time.time + 1.4f);

        float want = Time.time < _heatUntil ? 1f : 0f;
        _heat = Mathf.MoveTowards(_heat, want, Time.deltaTime * (want > _heat ? 4f : 0.32f));

        if (_heatFx != null)
        {
            var emission = _heatFx.emission;
            emission.rateOverTime = _heat * 6f;
        }

        if (_bodyMat != null)
            _bodyMat.SetColor("_EmissionColor", new Color(1.6f, 0.35f, 0.05f) * (_heat * 2.1f));

        if (_body != null)
        {
            float h = _heat * _heat;
            _body.localPosition = new Vector3(
                Mathf.Sin(Time.time * 19f) * 0.01f * h,
                Mathf.Sin(Time.time * 27f) * 0.014f * h,
                Mathf.Cos(Time.time * 15f) * 0.008f * h);
        }
    }

    static Texture2D _softSmokeTex;

    public static Texture2D GetSoftSmokeTexture()
    {
        if (_softSmokeTex != null)
            return _softSmokeTex;

        const int res = 64;
        _softSmokeTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        _softSmokeTex.wrapMode = TextureWrapMode.Clamp;
        _softSmokeTex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float radius = res * 0.48f;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float t = Mathf.Clamp01(dist / radius);
                // コサイン曲線による非常に滑らかな減衰（端は完全な透明、境界の四角感をゼロに）
                float alpha = t >= 1f ? 0f : (Mathf.Cos(t * Mathf.PI) * 0.5f + 0.5f);
                alpha = Mathf.Pow(alpha, 1.5f);
                _softSmokeTex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        _softSmokeTex.Apply();
        return _softSmokeTex;
    }

    static Material LoadHeatMaterial()
    {
        var shader = Shader.Find("RustAndFloat/WhiteSmoke");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default");
        var mat = new Material(shader);
        mat.SetTexture("_BaseMap", GetSoftSmokeTexture());
        mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.85f));
        mat.renderQueue = 3100;
        return mat;
    }

    void PlayCreak(bool force)
    {
        if (_audio == null || _creaks == null || _creaks.Length == 0)
            return;
        if (!force && Time.time < _nextCreak)
            return;
        var clip = _creaks[Random.Range(0, _creaks.Length)];
        if (clip == null)
            return;
        _audio.volume = soundVolume;
        _audio.pitch = Random.Range(0.86f, 1.08f);
        _audio.PlayOneShot(clip, Random.Range(0.22f, 0.38f));
        _nextCreak = Time.time + Random.Range(1.2f, 2.2f);
    }

    static AudioClip MakeCreak(int seed)
    {
        const int hz = 22050;
        int n = (int)(hz * 0.24f);
        float[] data = new float[n];
        var rng = new System.Random(seed);
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float env = Mathf.Exp(-t * 7.5f) * (1f - t);
            phase += (320f + (float)rng.NextDouble() * 280f) * (2f * Mathf.PI / hz);
            float grit = (float)rng.NextDouble() * 2f - 1f;
            data[i] = (Mathf.Sin(phase) * 0.32f + grit * 0.62f) * env * 0.5f;
        }

        var clip = AudioClip.Create("RustCreak", n, 1, hz, false);
        clip.SetData(data, 0);
        return clip;
    }

    public void OnNikoFoundScrap(int count)
    {
        // 嬉しそうにピョンと跳ねる
        _velocity += Vector3.up * 2.8f;
        if (_audio != null && _happyBeepClip != null)
            _audio.PlayOneShot(_happyBeepClip, 0.5f);

        string scrapSpeech = "";
        switch (count)
        {
            case 1:
                scrapSpeech = "ピピピッ！綺麗なギアだ…！指先が油で汚れても、この生身の手応えが嬉しいね、Niko！";
                break;
            case 2:
                scrapSpeech = "微かに温かい光が残ってる…！最短ルートを走るだけじゃ出会えなかった宝物だね。";
                break;
            case 3:
                scrapSpeech = "ピキーン！歯車がカチリと噛み合ったよ…！僕らは今、自分の足で走ってるんだ！";
                break;
            case 4:
                scrapSpeech = "ピピッ！また見つけたよ！少し寄り道した先に、こんな綺麗なパーツが眠ってたなんて！";
                break;
            case 5:
                scrapSpeech = "煤けてるけど大丈夫。優しく拭いてあげたら、青く澄んだ光が戻ってきたよ…！";
                break;
            case 6:
                scrapSpeech = "ピロロ…！温かい光が胸に灯ったよ……心臓の鼓動みたいだ。空中でSpaceを押してみて！";
                break;
            case 7:
                scrapSpeech = "歯車のひとつひとつに、昔の人の手の温もりが残っているみたいだね。";
                break;
            case 8:
                scrapSpeech = "ピロッ…！冷たい海風が心地いいね。僕たちの翼が少しずつ呼吸を取り戻してるよ。";
                break;
            case 9:
                scrapSpeech = "ピピ…！古いメモリから子供たちの声が聞こえたよ。無駄な時間の中にこそ愛があったんだね！";
                break;
            case 10:
                scrapSpeech = "森の木漏れ日、海の青さ…寄り道して迷った道こそが、本当の景色だったんだね。";
                break;
            case 11:
                scrapSpeech = "あとひとつで全てが繋がるよ…！あの白亜のタワーの頂が、僕たちを呼んでいる！";
                break;
            case 12:
                scrapSpeech = "ピキーッ！風の重さを取り戻したよ！冷たい向かい風は前へ進む証拠だ…行こう、中央タワーへ！";
                break;
            default:
                scrapSpeech = "ピピッ！ギアの波長が合ってきたよ！";
                break;
        }
        SetSpeech(scrapSpeech, 5.2f);

        // キーストーン節目（3, 6, 9, 12個）の特別アクション演出
        if (count == 3 || count == 6 || count == 9 || count == 12)
        {
            StartCoroutine(KeystoneFittedRoutine(count));
        }
    }

    /// <summary>キーストーン取り付け時の祝祭・感情豊かなリアクション演出</summary>
    IEnumerator KeystoneFittedRoutine(int count)
    {
        yield return new WaitForSeconds(0.2f);

        // 黄金スパークル放出
        Color sparkColor = (count == 6) ? new Color(0.25f, 0.95f, 1.0f) : new Color(1.0f, 0.85f, 0.35f);
        SpawnGoldSparkles(transform.position + Vector3.up * 0.4f, 32);

        // 嬉しそうに宙返り・旋回
        float elapsed = 0f;
        float duration = 0.9f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float step = (Time.deltaTime / duration) * 360f;
            transform.Rotate(Vector3.right, step, Space.Self);
            transform.position += Vector3.up * (Mathf.Sin((elapsed / duration) * Mathf.PI) * 0.045f);
            yield return null;
        }

        if (_audio != null && _sonarBeepClip != null)
            _audio.PlayOneShot(_sonarBeepClip, 0.75f);
    }

    /// <summary>滑空を開始した瞬間のRustの穏やかなセリフ</summary>
    public void OnGlideStarted()
    {
        _velocity += Vector3.up * 1.5f;
        string[] glideStartLines = {
            "わぁ…！風が気持ちいいね、Niko",
            "ふわりと浮いたよ…！",
            "風を掴んだね…！すごいよ！"
        };
        SetSpeech(glideStartLines[Random.Range(0, glideStartLines.Length)], 4.0f);
    }

    /// <summary>気流に乗った時のRustの穏やかなセリフ</summary>
    public void OnFloatWindCaught()
    {
        _velocity += Vector3.up * 1.8f;
        string[] windLines = {
            "わぁ…！風が気持ちいいね、Niko",
            "ふわりと浮いたよ…！",
            "風に乗って、どこまでも行けそう",
            "島を見下ろすと、すごく綺麗だね"
        };
        SetSpeech(windLines[Random.Range(0, windLines.Length)], 4.0f);
    }

    /// <summary>指定したテキストを特大ダイアログで発話（プレイヤーが次の行動を起こすまで消えずに維持）</summary>
    public void SpeakCustom(string text, float duration = 4.5f)
    {
        _velocity += Vector3.up * 0.8f;
        SetSpeech(text, duration);
    }

    /// <summary>セリフを表示し、プレイヤーが次の行動（WASD移動やジャンプなど）を起こすまで画面に維持</summary>
    public void SetSpeech(string text, float duration = 4.5f)
    {
        _speechText = text;
        _speechTimer = duration;
        _waitingForPlayerAction = true;
        _speechShowTime = 0f;
        var player = AdventurePlayerController.Instance;
        _speechPlayerStartPos = player != null ? player.transform.position : transform.position;
    }

    /// <summary>プレイヤーが次の行動を起こしたか判定（キー入力・コントローラー・移動検知）</summary>
    bool CheckPlayerActionInput()
    {
        // 1. 移動・ジャンプ等のキー入力検知 (新旧Input両対応)
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb.wKey.isPressed || kb.aKey.isPressed || kb.sKey.isPressed || kb.dKey.isPressed ||
                kb.upArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.downArrowKey.isPressed || kb.rightArrowKey.isPressed ||
                kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)
            {
                return true;
            }
        }
        if (UnityEngine.InputSystem.Gamepad.current != null)
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad.leftStick.ReadValue().sqrMagnitude > 0.04f || pad.buttonSouth.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif

        // 2. 旧Inputの安全なフォールバック（New Input System環境での例外を抑止）
        try
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D) ||
                Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.RightArrow) ||
                Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
            {
                return true;
            }
        }
        catch { }

        // 2. プレイヤーの移動距離検知 (キー入力以外でも歩行移動していれば確実に行動検知)
        var player = AdventurePlayerController.Instance;
        if (player != null && Vector3.Distance(player.transform.position, _speechPlayerStartPos) > 0.8f)
        {
            return true;
        }

        return false;
    }

    /// <summary>シネマティックストーリーボード表示時などにセリフ吹き出しを即時消去</summary>
    public void ClearSpeech()
    {
        _speechTimer = 0f;
        _speechText = "";
        _waitingForPlayerAction = false;
    }

    void UpdateSpeech()
    {
        if (_speechTimer > 0f)
        {
            if (_waitingForPlayerAction)
            {
                _speechShowTime += Time.deltaTime;
                // 発話直後の誤消去防止（最低1.2秒は確実に表示キープ）
                if (_speechShowTime >= 1.2f && CheckPlayerActionInput())
                {
                    _waitingForPlayerAction = false;
                    _speechTimer = Mathf.Min(_speechTimer, 2.5f); // 次の行動を起こした後は2.5秒の余韻でフェードアウト
                }
            }
            else
            {
                _speechTimer -= Time.deltaTime;
            }
        }
        else if (Time.time >= _nextIdleTalk)
        {
            _nextIdleTalk = Time.time + Random.Range(30f, 50f);
            var player = AdventurePlayerController.Instance;
            if (player != null && player.IsGliding)
            {
                string[] glideLines = {
                    "風に乗って、どこまでも行けそう",
                    "島を見下ろすと、すごく綺麗だね",
                    "わぁ…！風が気持ちいいね、Niko"
                };
                SetSpeech(glideLines[Random.Range(0, glideLines.Length)], 4.0f);
            }
            else
            {
                string[] exploreLines = {
                    "風の音が心地いいね、Niko",
                    "どこへ行こうか？のんびり行こう",
                    "この島の空気、すこし温かいね",
                    "ピピッ…何か光るものがあるかな？"
                };
                SetSpeech(exploreLines[Random.Range(0, exploreLines.Length)], 3.8f);
            }
        }
    }

    void UpdateSonar()
    {
        var mgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
        if (mgr == null) return;

        // レーダー未解放（パーツ9個未満）でも近距離（25m）で探知反応し、解放後は60mの超広域に強化
        bool hasRadar = mgr.hasPetRadar;
        float maxDist = hasRadar ? 60f : 25f;

        var nearest = mgr.GetNearestScrap(transform.position, out float dist);
        if (nearest == null || dist > maxDist)
            return;

        _sonarTimer -= Time.deltaTime;
        if (_sonarTimer <= 0f)
        {
            float rate = Mathf.Lerp(1.0f, hasRadar ? 3.0f : 2.0f, 1f - Mathf.Clamp01(dist / maxDist));
            _sonarTimer = (hasRadar ? 2.5f : 3.5f) / rate;

            if (_audio != null && _sonarBeepClip != null)
            {
                _audio.pitch = Mathf.Lerp(hasRadar ? 1.0f : 0.85f, 1.45f, 1f - Mathf.Clamp01(dist / maxDist));
                _audio.PlayOneShot(_sonarBeepClip, hasRadar ? 0.38f : 0.28f);
            }
        }
    }

    void OnGUI()
    {
        // 照準中のスクラップに対するRust遠隔回収プロンプト
        if (_aimedScrap != null && CurrentState == RustState.Follow && Camera.main != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(_aimedScrap.transform.position + Vector3.up * 0.4f);
            if (screenPos.z > 0.5f)
            {
                float aimW = 260f;
                float aimH = 34f;
                float aimX = screenPos.x - aimW * 0.5f;
                float aimY = Screen.height - screenPos.y - 45f;

                var promptStyle = new GUIStyle(GUI.skin.box);
                promptStyle.fontSize = 15;
                promptStyle.fontStyle = FontStyle.Bold;
                promptStyle.alignment = TextAnchor.MiddleCenter;
                promptStyle.normal.textColor = new Color(0.35f, 0.95f, 1.0f);

                GUI.Box(new Rect(aimX, aimY, aimW, aimH), "【F】Rustに回収を指示", promptStyle);
            }
        }

        // 0. Eキー検知の確実なフォールバック（InputSystemのフレーム遅延やEventSystem遮断を完全救済）
        if (_isPlayerNear && Event.current != null && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.E && Time.time - _lastInteractTime > 0.35f)
        {
            _lastInteractTime = Time.time;
            InteractWithNiko();
        }

        // 1. Niko接近時の頭上インタラクションプロンプト（特大フォント・高コントラスト）
        if (_isPlayerNear && Camera.main != null)
        {
            Vector3 headPos = transform.position + Vector3.up * 0.85f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(headPos);
            if (screenPos.z > 0.2f)
            {
                bool needsOil = (_heat > 0.15f || Time.time < _hitchUntil || Time.time > wellOiledUntil);
                oilCount = Mathf.Max(oilCount, 1);

                string prompt = needsOil 
                    ? $"【E】油をさして手当て＆セーブ（常備油: {oilCount}）" 
                    : $"【E】Rustを撫でてセーブ（常備油: {oilCount}）";
                Color textColor = needsOil 
                    ? new Color(1.0f, 0.90f, 0.25f) // 鮮やかなゴールド
                    : new Color(0.40f, 0.96f, 1.0f); // 爽やかなシアン

                // セリフ本文と調和する上品で読みやすいフォント（20〜30pt）
                int promptFontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.026f, 20f, 30f));
                GUIStyle pStyle = new GUIStyle(GUI.skin.label);
                pStyle.fontSize = promptFontSize;
                pStyle.fontStyle = FontStyle.Bold;
                pStyle.alignment = TextAnchor.MiddleCenter;

                Vector2 pSize = pStyle.CalcSize(new GUIContent(prompt));
                float padX = 28f;
                float padY = 12f;
                float boxW = pSize.x + padX;
                float boxH = pSize.y + padY;
                float boxX = screenPos.x - boxW * 0.5f;
                float boxY = Screen.height - screenPos.y - boxH - 16f;
                Rect promptBoxRect = new Rect(boxX, boxY, boxW, boxH);

                // 半透明ダーク背景
                if (_speechBg == null)
                {
                    _speechBg = new Texture2D(1, 1);
                    _speechBg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.92f));
                    _speechBg.Apply();
                }
                GUI.DrawTexture(promptBoxRect, _speechBg);

                // 上部アクセントライン
                Rect lineRect = new Rect(boxX, boxY, boxW, 3f);
                GUI.DrawTexture(lineRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, textColor, 0, 0);

                // 黒アウトライン付き特大テキスト描画
                DrawOutlinedText(promptBoxRect, prompt, pStyle, textColor, new Color(0f, 0f, 0f, 0.95f));
            }
        }

        // 2. 画面右上のオイル所持数＆RustコンディションHUD（文字欠け防止＆余裕のセーフマージン）
        bool isOiled = Time.time < wellOiledUntil;
        bool isDistressed = (_heat > 0.15f || Time.time < _hitchUntil || !isOiled);
        if (oilCount > 0 || isOiled || isDistressed)
        {
            // 上品で視認性の高いHUDフォント（15〜22pt）
            int badgeSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.018f, 15f, 22f));
            GUIStyle badgeStyle = new GUIStyle(GUI.skin.label);
            badgeStyle.fontSize = badgeSize;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleCenter;
            badgeStyle.clipping = TextClipping.Overflow; // 文字クリッピングを完全防止

            string status;
            Color bColor;
            if (isOiled)
            {
                status = "✦ Rust好調（整備済）";
                bColor = new Color(0.45f, 0.95f, 0.65f); // エメラルドグリーン
            }
            else
            {
                status = $"⚠ 要整備（【E】手当て＆セーブ / 油: {oilCount}）";
                bColor = new Color(1.0f, 0.88f, 0.35f); // イエロー
            }

            GUIContent bContent = new GUIContent(status);
            Vector2 bSize = badgeStyle.CalcSize(bContent);
            // 幅と高さを大幅に広げて余裕を確保（絶対に文字欠けしない）
            float bw = Mathf.Max(200f, bSize.x + 54f);
            float bh = Mathf.Max(36f, badgeSize + 18f);

            // 画面右端からしっかり離す（セーフエリアマージン 65〜110px）
            float rightMargin = Mathf.Clamp(Screen.width * 0.055f, 65f, 110f);
            float topMargin = Mathf.Clamp(Screen.height * 0.045f, 45f, 75f);
            Rect bRect = new Rect(Screen.width - bw - rightMargin, topMargin, bw, bh);

            if (_speechBg == null)
            {
                _speechBg = new Texture2D(1, 1);
                _speechBg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.92f));
                _speechBg.Apply();
            }
            GUI.DrawTexture(bRect, _speechBg);
            Rect bLine = new Rect(bRect.x, bRect.y, bw, 2.5f);
            GUI.DrawTexture(bLine, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, bColor, 0, 0);

            DrawOutlinedText(bRect, status, badgeStyle, bColor, new Color(0f, 0f, 0f, 0.95f));
        }

        // 3. セリフダイアログ表示（視認性抜群＆文字欠け防止設計）
        if (_speechTimer <= 0f || string.IsNullOrEmpty(_speechText))
            return;

        // 天蓋破壊シネマティックストーリーボード表示中はボードと重ならないよう抑制
        if (AdventureSanctuaryTowerManager.Instance != null && AdventureSanctuaryTowerManager.Instance.IsSkybreakModalActive)
            return;

        // しっかり大きく読みやすいシネマフォント設計（1080pで約30〜31pt）
        int bodyFontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.029f, 23f, 34f));
        int nameFontSize = Mathf.RoundToInt(bodyFontSize * 0.70f);

        // スタイル生成・キャッシュ
        if (_speechStyle == null)
        {
            _speechStyle = new GUIStyle();
            _speechBg = new Texture2D(1, 1);
            _speechBg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.90f));
            _speechBg.Apply();
        }

        // ウィンドウサイズの計算（拡大した文字が欠けずゆったり収まるサイズ）
        float boxWidth = Mathf.Clamp(Screen.width * 0.68f, 520f, 960f);
        float boxHeight = bodyFontSize * 2.8f + nameFontSize + 32f;
        float x = (Screen.width - boxWidth) * 0.5f;
        float y = Screen.height - boxHeight - Mathf.Clamp(Screen.height * 0.05f, 35f, 65f);

        float alpha = Mathf.Clamp01(_speechTimer);
        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        Rect boxRect = new Rect(x, y, boxWidth, boxHeight);

        // 1. 半透明ダーク背景（映画字幕風ウィンドウ）
        GUI.DrawTexture(boxRect, _speechBg);

        // 上部アクセントバー（エメラルドシアンの風の光彩ライン）
        Rect barRect = new Rect(x, y, boxWidth, 2.5f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, new Color(0.2f, 0.95f, 0.85f, 0.9f * alpha), 0, 0);

        // 2. ネームタグ [ 相棒 Rust ]
        GUIStyle nameStyle = new GUIStyle(GUI.skin.label);
        nameStyle.fontSize = nameFontSize;
        nameStyle.fontStyle = FontStyle.Bold;
        nameStyle.alignment = TextAnchor.MiddleLeft;
        nameStyle.clipping = TextClipping.Overflow;

        Rect nameRect = new Rect(x + 28f, y + 10f, boxWidth - 56f, nameFontSize + 4f);
        DrawOutlinedText(nameRect, "✦ 相棒 Rust", nameStyle, new Color(0.35f, 0.92f, 0.98f, alpha), new Color(0f, 0f, 0f, 0.9f * alpha));

        // 行動待ちヒント（右上に上品に表示）
        if (_waitingForPlayerAction)
        {
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(nameFontSize * 0.82f));
            hintStyle.alignment = TextAnchor.MiddleRight;
            hintStyle.clipping = TextClipping.Overflow;
            Rect hintRect = new Rect(x + 28f, y + 10f, boxWidth - 56f, nameFontSize + 4f);
            DrawOutlinedText(hintRect, "（行動・移動で閉じます）", hintStyle, new Color(0.65f, 0.85f, 0.95f, alpha * 0.85f), new Color(0f, 0f, 0f, 0.85f * alpha));
        }

        // 3. セリフ本文（大きくてはっきり読める・クリッピング防止）
        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label);
        bodyStyle.fontSize = bodyFontSize;
        bodyStyle.fontStyle = FontStyle.Bold;
        bodyStyle.alignment = TextAnchor.UpperLeft;
        bodyStyle.wordWrap = true;
        bodyStyle.clipping = TextClipping.Overflow; // 上下左右の文字クリップを完全排除

        Rect bodyRect = new Rect(x + 28f, y + nameFontSize + 14f, boxWidth - 56f, bodyFontSize * 2.6f);
        DrawOutlinedText(bodyRect, "「" + _speechText + "」", bodyStyle, new Color(1.0f, 1.0f, 1.0f, alpha), new Color(0f, 0f, 0f, 0.95f * alpha));

        GUI.color = prevColor;
    }

    /// <summary>4方向の黒フチ取り（アウトライン）で背景色問わず100%くっきり描画</summary>
    static void DrawOutlinedText(Rect rect, string text, GUIStyle style, Color textColor, Color outlineColor)
    {
        int spread = Mathf.Max(1, style.fontSize / 16);
        Color origColor = style.normal.textColor;

        style.normal.textColor = outlineColor;
        GUI.Label(new Rect(rect.x - spread, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + spread, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - spread, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + spread, rect.width, rect.height), text, style);

        style.normal.textColor = textColor;
        GUI.Label(rect, text, style);
        style.normal.textColor = origColor;
    }

    static AudioClip MakeSynthBeep(float startFreq, float endFreq, float duration)
    {
        const int hz = 44100;
        int samples = (int)(hz * duration);
        float[] data = new float[samples];
        float phase = 0f;
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float freq = Mathf.Lerp(startFreq, endFreq, t);
            phase += 2f * Mathf.PI * freq / hz;
            float env = Mathf.Sin(t * Mathf.PI);
            data[i] = Mathf.Sin(phase) * env * 0.45f;
        }
        var clip = AudioClip.Create("SynthBeep", samples, 1, hz, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>油を差してもらって快調になったRustの祝祭・宙返り＆パーツレーダー演出</summary>
    public IEnumerator CheerSpinRoutine()
    {
        // 1. 黄金の光粒子スパークルをバースト放出
        SpawnGoldSparkles(transform.position + Vector3.up * 0.4f, 26);

        string[] treatLines = {
            "わぁ…！ありがとうNiko、身体がすごく軽くなったよ…！",
            "油を差してくれてありがとう！ギアが滑らかに回ってるよ！",
            "ピピッ…！温かい手当てをありがとう。もうギシギシしないよ！"
        };
        SpeakCustom(treatLines[Random.Range(0, treatLines.Length)], 4.2f);

        // 2. 宙返りアニメーション（嬉しそうに一回転）
        float elapsed = 0f;
        float duration = 0.75f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float step = (Time.deltaTime / duration) * 360f;
            transform.Rotate(Vector3.right, step, Space.Self);
            transform.position += Vector3.up * (Mathf.Sin((elapsed / duration) * Mathf.PI) * 0.035f);
            yield return null;
        }

        // 3. 最寄りの未回収パーツ（スクラップ）を探知して方角を案内
        yield return new WaitForSeconds(0.4f);
        var allScraps = FindObjectsByType<AdventureScrapItem>(FindObjectsInactive.Exclude);
        AdventureScrapItem nearest = null;
        float minDist = float.MaxValue;
        foreach (var s in allScraps)
        {
            if (s == null || !s.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(transform.position, s.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = s;
            }
        }

        if (nearest != null && minDist < 95f)
        {
            Vector3 diff = nearest.transform.position - transform.position;
            string dirName;
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
                dirName = diff.x > 0 ? "東（右奥）" : "西（海側）";
            else
                dirName = diff.z > 0 ? "北（奥の高台）" : "南（浜辺側）";

            if (_audio != null && _sonarBeepClip != null)
                _audio.PlayOneShot(_sonarBeepClip, 0.7f);

            SpeakCustom($"ピピッ！{dirName}の方角から、古代パーツの共鳴を感じるよ！", 5.0f);
        }
    }

    void SpawnGoldSparkles(Vector3 pos, int count)
    {
        var go = new GameObject("Rust_GoldSparkles");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.8f;
        main.loop = false;
        main.startLifetime = 1.4f;
        main.startSpeed = 3.6f;
        main.startSize = 0.35f;
        main.startColor = new Color(1f, 0.90f, 0.35f, 0.95f); // 鮮やかなゴールド
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, count) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.45f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(1f, 0.92f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.6f, 0.1f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        if (rend != null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.SetTexture("_BaseMap", GetSoftSmokeTexture());
            rend.material = mat;
        }

        ps.Play();
        Destroy(go, 2.0f);
    }

    Transform _cachedBreastL, _cachedBreastR;
    Transform _cachedShoulderL, _cachedShoulderR;
    Transform _cachedSpine003, _cachedSpine004, _cachedHead;
    bool _bonesCached = false;

    void CacheNikoBones()
    {
        if (_lookAt == null) return;

        foreach (var t in _lookAt.GetComponentsInChildren<Transform>())
        {
            string n = t.name.ToLower();
            if (n.Contains("breast") && (n.Contains(".l") || n.EndsWith("_l") || n.Contains("left")))
                _cachedBreastL = t;
            else if (n.Contains("breast") && (n.Contains(".r") || n.EndsWith("_r") || n.Contains("right")))
                _cachedBreastR = t;
            else if (n.Contains("shoulder") && (n.Contains(".l") || n.EndsWith("_l") || n.Contains("left")))
                _cachedShoulderL = t;
            else if (n.Contains("shoulder") && (n.Contains(".r") || n.EndsWith("_r") || n.Contains("right")))
                _cachedShoulderR = t;
            else if (n == "spine.003" || n.Contains("spine3") || n.Contains("spine_03") || n.Contains("chest"))
                _cachedSpine003 = t;
            else if (n == "spine.004" || n.Contains("neck"))
                _cachedSpine004 = t;
            else if (n.Contains("head"))
                _cachedHead = t;
        }
        _bonesCached = true;
    }

    /// <summary>Nikoの胸のワールド座標を高精度かつゼロアロケーションで取得</summary>
    public Vector3 GetNikoChestPosition()
    {
        if (_lookAt == null) return transform.position;

        if (!_bonesCached || (_cachedBreastL == null && _cachedShoulderL == null))
        {
            CacheNikoBones();
        }

        // 最優先: 両胸（breast.L と breast.R）の中点（100%正真正銘のバスト・胸の中央！）
        if (_cachedBreastL != null && _cachedBreastR != null)
        {
            return (_cachedBreastL.position + _cachedBreastR.position) * 0.5f;
        }
        if (_cachedBreastL != null) return _cachedBreastL.position;
        if (_cachedBreastR != null) return _cachedBreastR.position;

        // 両肩の中点（鎖骨・胸骨の真上！）
        if (_cachedShoulderL != null && _cachedShoulderR != null)
        {
            Vector3 midShoulder = (_cachedShoulderL.position + _cachedShoulderR.position) * 0.5f;
            return midShoulder - _lookAt.up * 0.08f; // 肩ラインから胸の中央へ約8cm下げる
        }

        // spine.003（胸骨ボーン）
        if (_cachedSpine003 != null)
            return _cachedSpine003.position;

        // 首（spine.004）から少し下
        if (_cachedSpine004 != null)
            return _cachedSpine004.position - _lookAt.up * 0.15f;

        // 頭（Head）から胸の高さへオフセット（約35cm下）
        if (_cachedHead != null)
            return _cachedHead.position - _lookAt.up * 0.35f;

        // フォールバック
        return _lookAt.position + Vector3.up * 1.45f;
    }
}

