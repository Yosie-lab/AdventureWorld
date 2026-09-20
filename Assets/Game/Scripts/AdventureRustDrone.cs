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
    string _speechSpeaker = "✦ 相棒 Rust";
    Color _speechSpeakerColor = new Color(0.35f, 0.92f, 0.98f, 1f);
    AudioClip _happyBeepClip;
    AudioClip _sonarBeepClip;
    AudioClip _pipiChimeClip;
    AudioClip _distressWhineClip;
    float _nextDistressSound;
    float _sonarTimer = 0f;
    GUIStyle _speechStyle;
    Texture2D _speechBg;

    // オイルアイテムと手当てシステム
    public int oilCount = 8;
    public float wellOiledUntil = 0f;
    bool _isPlayerNear = false;
    float _lastInteractTime = 0f;

    // 探索アシスト（近くの未発見パーツへの誘導・合図）
    AdventureScrapItem _guidedScrap;
    float _nextGuideNotice = 0f;
    bool _isPointingToScrap = false;

    // 20pt達成時の中央タワー先導誘導
    bool _isGuidingToTower = false;
    float _nextTowerNotice = 0f;
    bool _nearTowerNotified = false;
    static readonly Vector3 SanctuaryTowerCenter = new Vector3(512f, 63.2f, 512f);

    /// <summary>20pt達成時に中央タワーへの先導誘導を開始</summary>
    public void TriggerTowerLeadGuidance()
    {
        _isGuidingToTower = true;
        _nextTowerNotice = Time.time + 3.5f;
        _nearTowerNotified = false;
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = 1.35f;
            _audio.PlayOneShot(_happyBeepClip, 0.85f);
        }
    }

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

    static Font _jpFont;

    static Font ResolveJapaneseFont(int size = 28)
    {
        if (_jpFont != null) return _jpFont;
        try
        {
            _jpFont = Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Hiragino Sans", "HiraginoSans-W3", "Hiragino Kaku Gothic ProN",
                    "YuGothic", "Yu Gothic", "Arial Unicode MS"
                },
                size);
        }
        catch { }
        if (_jpFont == null)
            _jpFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                      ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        return _jpFont;
    }

    static GUIStyle MakeJpLabel(int fontSize, FontStyle style, TextAnchor align, bool wordWrap = false)
    {
        var font = ResolveJapaneseFont(fontSize);
        var s = new GUIStyle
        {
            font = font,
            fontSize = fontSize,
            fontStyle = style,
            alignment = align,
            wordWrap = wordWrap,
            richText = false,
            clipping = TextClipping.Overflow
        };
        s.normal.textColor = Color.white;
        return s;
    }

    public static AdventureRustDrone Instance { get; private set; }

    public static void Ensure()
    {
        // 重複Rustを一掃してから1体だけ残す
        var all = Object.FindObjectsByType<AdventureRustDrone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        AdventureRustDrone keep = Instance;
        if (keep == null)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null) { keep = all[i]; break; }
            }
        }
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i] == keep) continue;
            Object.Destroy(all[i].gameObject);
        }
        if (keep != null)
        {
            Instance = keep;
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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
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
        oilCount = Mathf.Max(oilCount, 8); // 開始時から十分ストック（Rustを甘やかす）

        AdventureRustSpeechUI.Ensure();

        // 起動時のあたたかい挨拶（新規ゲームのプロローグがある場合は後で上書き）
        SetSpeech("ピピッ…！起動したよ、Niko。一緒に行こう！", 4.5f);
        // 約30秒後から定期会話（プロローグ中は後で繰り延べ）
        _nextIdleTalk = Time.unscaledTime + 30f;
        _lastIdleLine = "";
    }

    bool _prologueDistress;

    /// <summary>冒頭ドラマ：塩水で凍りつくRust</summary>
    public void StartPrologueDistress()
    {
        _prologueDistress = true;
        IsClimaxCrisis = false;
        IsClimaxOverdrive = false;
        _climaxHealing = false;
        CurrentState = RustState.Petting;
        _stateTimer = 9999f;
        wellOiledUntil = 0f;
        _heat = 0.85f;
        _heatUntil = Time.time + 999f;
        _hitchUntil = Time.time + 999f;

        if (_bodyMat != null)
        {
            _savedEmission = _bodyMat.GetColor("_EmissionColor");
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", new Color(0.2f, 0.4f, 0.55f) * 0.35f);
        }

        if (_lookAt != null)
        {
            Vector3 nest = GetNikoChestPosition() + _lookAt.forward * 0.75f + _lookAt.right * 0.7f + Vector3.up * 0.15f;
            transform.position = nest;
            _lagTarget = nest;
            _velocity = Vector3.zero;
        }
    }

    public void EndPrologueDistress()
    {
        _prologueDistress = false;
        _heat = 0f;
        _heatUntil = 0f;
        _hitchUntil = 0f;
        if (CurrentState == RustState.Petting && !IsClimaxCrisis && !_climaxHealing && !_skybreakNestle)
        {
            CurrentState = RustState.Follow;
            _stateTimer = 0f;
        }
    }

    /// <summary>冒頭ドラマ：注油で回路復活</summary>
    public void CompletePrologueOil()
    {
        oilCount = Mathf.Max(oilCount, 8);
        wellOiledUntil = Time.time + 120f;
        _heat = 0f;
        _heatUntil = 0f;
        _hitchUntil = 0f;
        _prologueDistress = false;
        _climaxHealing = true;
        SpawnGoldSparkles(transform.position + Vector3.up * 0.3f, 28);
        SpawnClimaxHealAura();

        if (_bodyMat != null)
        {
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", new Color(1.0f, 0.82f, 0.25f) * 2.4f);
        }
        EnsureClimaxEyeLight(new Color(1f, 0.9f, 0.45f), 2.4f);

        if (_audio != null && _happyBeepClip != null)
            _audio.PlayOneShot(_happyBeepClip, 0.55f);

        StartCoroutine(EndPrologueHealSoon());
    }

    IEnumerator EndPrologueHealSoon()
    {
        yield return new WaitForSeconds(2.8f);
        _climaxHealing = false;
        if (_climaxHealFx != null)
            _climaxHealFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        // プロローグ甘え寄り添い（Petting）中は Follow へ戻さない
        if (CurrentState != RustState.Petting)
            CurrentState = RustState.Follow;
    }

    public void CelebratePrologueFirstGear()
    {
        _velocity += Vector3.up * 3.2f;
        SpawnGoldSparkles(transform.position + Vector3.up * 0.4f, 40);
        if (_audio != null && _happyBeepClip != null)
            _audio.PlayOneShot(_happyBeepClip, 0.65f);
        StartCoroutine(KeystoneFittedRoutine(1));
    }

    public void CelebratePrologueDashUnlock()
    {
        SpawnGoldSparkles(transform.position + Vector3.up * 0.5f, 48);
        if (_audio != null && _happyBeepClip != null)
            _audio.PlayOneShot(_happyBeepClip, 0.7f);
    }

    float _nextIdleTalk;
    string _lastIdleLine = "";

    void Update()
    {
        // セリフ進行は lookAt 無しでも回す（吹き出しが見えない事故防止）
        UpdateSpeech();

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
                    SpeakCustom("キャッチしたよ！……Nikoのところへ、大事に持ってくね", 3.0f);
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
                    AdventurePettingAction.Instance.PetRust("すごいよRust！えらいえらい、ありがとう。よしよし", 2.4f);
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
            if (_climaxFalling)
            {
                TickClimaxFallAway();
                return;
            }

            if (IsClimaxCrisis || IsClimaxOverdrive || _climaxHealing || _skybreakNestle || _prologueDistress)
            {
                goal = FollowPoint();
                _lagTarget = Vector3.Lerp(_lagTarget, goal, 0.85f);
            }
            else
            {
                Vector3 chestPos = GetNikoChestPosition();
                goal = chestPos + _lookAt.forward * 1.2f + _lookAt.right * 0.25f + Vector3.up * 0.1f;
                goal.y += Mathf.Sin(Time.time * 3.5f) * 0.035f;
                _lagTarget = goal;
                _stateTimer -= Time.deltaTime;
                if (_stateTimer <= 0f)
                    CurrentState = RustState.Follow;
            }
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
        if (_climaxHealing)
            smoothTime = 0.05f; // 注油直後は即座にかたわらへ
        else if (IsClimaxOverdrive)
            smoothTime = 0.10f;
        else if (IsClimaxCrisis || _skybreakNestle || _prologueDistress)
            smoothTime = 0.12f;
        float maxSpeed = _climaxHealing ? 28f : (IsClimaxOverdrive ? 22f : 8.5f);
        transform.position = Vector3.SmoothDamp(transform.position, _lagTarget, ref _velocity, smoothTime, maxSpeed);

        // エンディング寄り添い中は毎フレーム画面内チェック
        if ((_skybreakNestle || IsClimaxCrisis || _climaxHealing || IsClimaxOverdrive) && !_climaxFalling)
            KeepRustOnScreenNearNiko();

        // 危機時／冒頭ドラマ：ガタガタ震え／注油時：ふわり浮遊オフセット
        if ((IsClimaxCrisis || _prologueDistress) && !_climaxHealing)
        {
            float shake = _prologueDistress ? 0.04f : 0.055f;
            transform.position += new Vector3(
                Mathf.Sin(Time.unscaledTime * 42f) * shake,
                Mathf.Sin(Time.unscaledTime * 51f) * shake * 0.7f,
                Mathf.Cos(Time.unscaledTime * 37f) * shake);
        }
        else if (_climaxHealing)
        {
            float floatUp = Mathf.Sin(Time.unscaledTime * 3.2f) * 0.04f + 0.08f;
            transform.position += Vector3.up * floatUp;
        }

        // 回転の計算
        Vector3 to = goal - transform.position;
        if ((IsClimaxOverdrive || _climaxHealing) && _lookAt != null)
        {
            // 注油後〜オーバードライブ：Nikoの顔を見て寄り添う
            to = GetNikoHeadPosition() - transform.position;
        }
        else if (CurrentState == RustState.Follow)
        {
            to = _lookAt.position + Vector3.up * 0.7f - transform.position;
            if (_isGuidingToTower)
                to = SanctuaryTowerCenter + Vector3.up * 2.0f - transform.position;
            else if (_isPointingToScrap && _guidedScrap != null)
                to = _guidedScrap.transform.position + Vector3.up * 0.3f - transform.position;
        }
        else if (CurrentState == RustState.Returning)
        {
            to = _lookAt.position + Vector3.up * 1.35f - transform.position;
        }
        else if (CurrentState == RustState.Petting)
        {
            Vector3 lookTarget = (_skybreakNestle || IsClimaxCrisis || _climaxHealing || IsClimaxOverdrive)
                ? GetNikoChestPosition() + Vector3.up * 0.2f
                : GetNikoChestPosition() + Vector3.up * 0.32f;
            to = lookTarget - transform.position;
        }

        if (to.sqrMagnitude > 0.04f)
        {
            Quaternion look = Quaternion.LookRotation(to);
            if (CurrentState == RustState.Celebrating)
            {
                // 嬉しい宙返り回転！
                look *= Quaternion.Euler(Time.time * 720f, 0f, 0f);
            }
            else if (IsClimaxCrisis && !_climaxHealing)
            {
                // 極寒で激しく震える
                look *= Quaternion.Euler(
                    Mathf.Sin(Time.unscaledTime * 28f) * 18f,
                    Mathf.Sin(Time.unscaledTime * 33f) * 14f,
                    Mathf.Cos(Time.unscaledTime * 25f) * 16f);
            }
            else if (_climaxHealing)
            {
                look *= Quaternion.Euler(-18f + Mathf.Sin(Time.unscaledTime * 2.5f) * 6f, 0f, 8f);
            }
            else if (IsClimaxOverdrive)
            {
                look *= Quaternion.Euler(-22f, 0f, Mathf.Sin(Time.time * 6f) * 10f);
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

            float rotSpeed = IsClimaxCrisis ? 14f : (IsClimaxOverdrive ? 9f : 6.5f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, rotSpeed * Time.deltaTime);
        }

        if (!hitching && _velocity.sqrMagnitude > 6f && !wellOiled && CurrentState == RustState.Follow)
            PlayCreak(false);

        UpdateHeat(hitching);
        if (_wasHitching && !hitching)
            DripOil();
        _wasHitching = hitching;

        TickDistressAudio(wellOiled, hitching);

        UpdateGuide();
        UpdatePlayerInteraction();
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
            _isGuidingToTower = false;
            return;
        }

        // 20pt達成〜天蓋開放前：中央タワーへの先導誘導を最優先！
        bool leverUnlocked = mgr.IsLeverUnlocked;
        bool canopyBroken = AdventureSanctuaryTowerManager.IsCanopyBroken;

        if (leverUnlocked && !canopyBroken)
        {
            _isGuidingToTower = true;
            _guidedScrap = null;
            _isPointingToScrap = false;

            float distToTower = Vector3.Distance(_lookAt.position, SanctuaryTowerCenter);

            if (distToTower <= 14f)
            {
                if (!_nearTowerNotified)
                {
                    _nearTowerNotified = true;
                    SetSpeech("タワーに着いたよ！白亜のテラスに黄金のレバーがある！引いてみて、Niko！！", 5.0f);
                    if (_audio != null && _happyBeepClip != null)
                    {
                        _audio.pitch = 1.45f;
                        _audio.PlayOneShot(_happyBeepClip, 0.75f);
                    }
                }
            }
            else
            {
                _nearTowerNotified = false;
                if (Time.time >= _nextTowerNotice)
                {
                    _nextTowerNotice = Time.time + 14f;
                    SetSpeech("こっちだよ！中央タワーはこの方向だ！レバーを引きに行こう！", 4.0f);
                    if (_audio != null && _happyBeepClip != null)
                    {
                        _audio.pitch = 1.35f;
                        _audio.PlayOneShot(_happyBeepClip, 0.65f);
                    }
                }
            }
            return;
        }
        else
        {
            _isGuidingToTower = false;
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

    /// <summary>エンディング中：カメラから見てNiko右隣（画面内）の位置</summary>
    Vector3 NestleBesideNikoOnScreen(float side = 0.72f, float towardCam = 0.4f, float lift = 0.08f)
    {
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
            if (toCam.sqrMagnitude < 0.01f) toCam = -_lookAt.forward;
            else toCam.Normalize();
        }
        return chest + sideDir * side + toCam * towardCam + Vector3.up * lift;
    }

    void TickClimaxFallAway()
    {
        const float maxDistFromChest = 2.4f;
        _climaxFallVel += Vector3.down * 4.5f * Time.deltaTime;
        if (_lookAt != null)
        {
            Vector3 pull = NestleBesideNikoOnScreen(1.15f, 0.15f, -0.35f) - transform.position;
            _climaxFallVel += pull * 1.8f * Time.deltaTime;
        }
        _climaxFallVel = Vector3.ClampMagnitude(_climaxFallVel, 5.5f);
        transform.position += _climaxFallVel * Time.deltaTime;
        _lagTarget = transform.position;
        _velocity = _climaxFallVel;

        if (_lookAt != null)
        {
            Vector3 chest = GetNikoChestPosition();
            Vector3 delta = transform.position - chest;
            if (delta.magnitude > maxDistFromChest)
                transform.position = chest + delta.normalized * maxDistFromChest;
            KeepRustOnScreenNearNiko();
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

    Vector3 FollowPoint()
    {
        Vector3 niko = _lookAt.position;
        Vector3 chest = GetNikoChestPosition();

        if ((_skybreakNestle || _prologueDistress) && !_climaxFalling)
        {
            float nestleBob = Mathf.Sin(Time.time * 3.2f) * 0.025f;
            if (_prologueDistress)
                return NestleBesideNikoOnScreen(0.85f, 0.45f, 0.12f + nestleBob);
            return NestleBesideNikoOnScreen(0.75f, 0.42f, 0.06f + nestleBob);
        }

        if ((IsClimaxCrisis || _climaxHealing) && !_climaxFalling)
        {
            if (_climaxHealing)
                return NestleBesideNikoOnScreen(0.7f, 0.48f, 0.1f);
            return NestleBesideNikoOnScreen(0.78f, 0.38f, 0.05f);
        }

        if (IsClimaxOverdrive)
            return NestleBesideNikoOnScreen(0.8f, 0.4f, 0.12f);

        // 20pt達成後：RustはNikoの前方2.4m（中央タワーに向かうベクトル）へ先行飛行して先導！
        if (_isGuidingToTower)
        {
            Vector3 toTower = Vector3.ProjectOnPlane(SanctuaryTowerCenter - niko, Vector3.up).normalized;
            Vector3 guidePos = niko + toTower * 2.4f;
            float sBob = Mathf.Sin(Time.time * bobSpeed * 1.8f) * (bobAmount * 1.3f);
            return new Vector3(guidePos.x, chest.y + 0.25f + sBob, guidePos.z);
        }

        // 近くに未回収パーツがある場合、RustはNikoの少し前方（パーツ寄り）へ先行して合図
        if (_isPointingToScrap && _guidedScrap != null)
        {
            Vector3 toScrap = Vector3.ProjectOnPlane(_guidedScrap.transform.position - niko, Vector3.up).normalized;
            Vector3 guidePos = niko + toScrap * 1.8f;
            float sBob = Mathf.Sin(Time.time * bobSpeed * 1.6f) * (bobAmount * 1.2f);
            return new Vector3(guidePos.x, chest.y + 0.1f + sBob, guidePos.z);
        }

        // 通常追従の理想位置: Nikoの右肩の斜め後ろ（右1.15m、後方1.45m）
        Vector3 rightBack = _lookAt.right * 1.15f - _lookAt.forward * 1.45f;
        Vector3 targetPos = niko + rightBack;

        // Nikoの真正面／体内にRustが居座るのを防止（直径≈1.6m）
        const float bodyClearance = 1.65f;
        float flat = FlatDistance(niko);
        Vector3 toDrone = transform.position - niko;
        float forwardDot = Vector3.Dot(_lookAt.forward, toDrone);
        if (flat < bodyClearance || (flat < 1.9f && forwardDot > -0.15f))
        {
            targetPos = niko + rightBack;
        }
        else if (flat < stopDistance)
        {
            targetPos = new Vector3(transform.position.x, 0f, transform.position.z);
        }

        float followBob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        float y = chest.y + 0.05f + followBob; // 常にNikoの胸・肩の高さに追従！

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
        _pipiChimeClip = MakePipiChime();
        _distressWhineClip = MakeDistressWhine();
        _nextDistressSound = Time.time + 1.5f;
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
    public void AddOil(int amount, bool announce = true)
    {
        oilCount = Mathf.Max(0, oilCount) + amount;
        if (announce)
            SpeakCustom($"✦ 潤滑油を採取した！（+{amount}／所持: {oilCount}）", 3.2f);
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

        var towerMgr = AdventureSanctuaryTowerManager.Instance;

        // クライマックス注油中は通常の手当てを止め、注油ゲージへ回す
        if (towerMgr != null && towerMgr.IsClimaxOilPromptActive)
        {
            var kbOil = UnityEngine.InputSystem.Keyboard.current;
            bool holding = kbOil != null && (kbOil.eKey.isPressed || kbOil.spaceKey.isPressed || kbOil.enterKey.isPressed);
            try
            {
                if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.Return))
                    holding = true;
            }
            catch { }
            if (holding)
                towerMgr.NotifyOilHold(Time.unscaledDeltaTime);
            return;
        }

        // シネマティック中は通常インタラクトを出さない
        if (ShouldHideInteractionPrompt(towerMgr))
            return;

        // 押しっぱなし連打を防ぐ（wasPressed のみ）
        var kb = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool ePressed = (kb != null && kb.eKey.wasPressedThisFrame)
                     || (pad != null && pad.buttonWest.wasPressedThisFrame);
        try { if (Input.GetKeyDown(KeyCode.E)) ePressed = true; } catch { }
        // PlayerController の InteractPressed は押しっぱなしでも立つため、ここでは使わない

        // レバーの近くにいる場合はレバー操作を最優先
        if (towerMgr != null && towerMgr.IsPlayerNearLever)
            return;

        // カピタ会話レンジ内では E をカピタに譲る（Rust手当てと取り合いにしない）
        if (AdventureCapytaBlessing.IsPlayerNearTalkableCapyta(player.transform.position))
            return;

        if (_isPlayerNear && ePressed && Time.time - _lastInteractTime > 0.35f)
        {
            _lastInteractTime = Time.time;
            InteractWithNiko();
        }
    }

    static bool ShouldHideInteractionPrompt(AdventureSanctuaryTowerManager tower)
    {
        if (tower == null) return false;
        return tower.IsSkybreakModalActive
            || tower.IsClimaxOilPromptActive
            || tower.IsEpiloguePlaying
            || tower.ShowGameClearModal
            || tower.ClimaxCrisisStarted
            || tower.EpilogueTriggered;
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
        _prologueDistress = false;

        if (_happyBeepClip != null && _audio != null)
            _audio.PlayOneShot(_happyBeepClip, 0.45f);

        // NikoがRustを胸元で愛おしく撫でて手当て
        if (AdventurePettingAction.Instance != null)
        {
            string msg = needsOil
                ? "よし、油をさしたよ。調子はどう、Rust？"
                : "セーブしたよ。また何かあったら言って";
            AdventurePettingAction.Instance.PetRust(msg, 2.6f);
        }
        else
        {
            StartCoroutine(CheerSpinRoutine());
        }
        AdventureSaveManager.Instance?.SaveGame("SAVEしました");
        AdventurePrologueDrama.Instance?.NotifyOilApplied();
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
            var niko = AdventurePlayerController.Instance
                       ?? Object.FindFirstObjectByType<AdventurePlayerController>();
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
            var niko = AdventurePlayerController.Instance
                       ?? Object.FindFirstObjectByType<AdventurePlayerController>();
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
        _skybreakNestle = true;
        ClearClimaxFallState();
        IsClimaxCrisis = true;
        IsClimaxOverdrive = false;
        _climaxHealing = false;
        CurrentState = RustState.Petting;
        _stateTimer = 999f;
        wellOiledUntil = 0f;
        _heat = 0f;

        EnsureLookAtCached();
        SnapBesideNiko(healingNestle: false);

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

        Vector3 nest = NestleBesideNikoOnScreen(1.05f, 0.2f, -0.25f);
        Vector3 push = nest - transform.position;
        _climaxFallVel = push.normalized * 2.8f + Vector3.down * 1.8f;
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
        _skybreakNestle = true;
        ClearClimaxFallState();
        IsClimaxCrisis = false;
        _climaxHealing = true;
        IsClimaxOverdrive = false;
        CurrentState = RustState.Petting;
        _stateTimer = 9999f;

        EnsureLookAtCached();
        SnapBesideNiko(healingNestle: true);

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
        _skybreakNestle = true;
        ClearClimaxFallState();
        IsClimaxCrisis = false;
        _climaxHealing = false;
        IsClimaxOverdrive = true;
        CurrentState = RustState.Petting;
        _stateTimer = 9999f;
        wellOiledUntil = Time.time + 9999f;
        oilCount = 0;

        EnsureLookAtCached();
        SnapBesideNiko(healingNestle: false);

        if (_bodyMat != null)
        {
            _bodyMat.EnableKeyword("_EMISSION");
            _bodyMat.SetColor("_EmissionColor", new Color(1.45f, 1.05f, 0.35f) * 3.2f);
        }
        EnsureClimaxEyeLight(new Color(1f, 0.88f, 0.45f), 5.8f);

        if (_climaxHealFx != null)
            _climaxHealFx.Stop();

        SpawnClimaxJetFx();
        DestroyClimaxTrail();

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
            ? NestleBesideNikoOnScreen(0.7f, 0.48f, 0.1f)
            : NestleBesideNikoOnScreen(0.75f, 0.42f, 0.06f);

        transform.position = nest;
        _lagTarget = nest;
        _velocity = Vector3.zero;
        Vector3 face = GetNikoChestPosition() + Vector3.up * 0.2f - nest;
        if (face.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(face.normalized);
    }

    void KeepRustOnScreenNearNiko()
    {
        if (_lookAt == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 sp = cam.WorldToViewportPoint(transform.position);
        bool off =
            sp.z < 0.35f
            || sp.x < 0.12f || sp.x > 0.88f
            || sp.y < 0.18f || sp.y > 0.82f;
        if (!off) return;

        Vector3 safe = NestleBesideNikoOnScreen(0.72f, 0.45f, 0.08f);
        transform.position = Vector3.Lerp(transform.position, safe, 0.55f);
        _lagTarget = transform.position;
        _velocity = Vector3.zero;
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
        if (_climaxJetFx != null)
        {
            _climaxJetFx.Play();
            return;
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

        if (_bodyMat != null && !IsClimaxCrisis && !IsClimaxOverdrive && !_climaxHealing)
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

    /// <summary>快調でないとき：弱いウィーン／きしみを間欠再生（少しだけ存在感）</summary>
    void TickDistressAudio(bool wellOiled, bool hitching)
    {
        if (wellOiled)
        {
            _nextDistressSound = Time.time + 2.5f;
            return;
        }
        // オーバードライブ等は別演出に任せる
        if (IsClimaxOverdrive) return;
        if (_audio == null) return;
        if (Time.time < _nextDistressSound) return;

        bool heavy = hitching || _prologueDistress || IsClimaxCrisis || _heat > 0.35f;
        _nextDistressSound = Time.time + (heavy
            ? Random.Range(0.85f, 1.6f)
            : Random.Range(2.0f, 3.6f));

        if (Random.value < 0.55f)
            PlayCreak(true);
        else
            PlayDistressWhine(heavy);
    }

    void PlayDistressWhine(bool heavy)
    {
        if (_audio == null || _distressWhineClip == null) return;
        _audio.volume = soundVolume;
        _audio.pitch = Random.Range(0.72f, 0.92f);
        float vol = heavy ? Random.Range(0.22f, 0.34f) : Random.Range(0.14f, 0.24f);
        _audio.PlayOneShot(_distressWhineClip, vol);
    }

    /// <summary>不調時の「キュゥ…ン／ピィ…」と弱った電子ウィーン</summary>
    static AudioClip MakeDistressWhine()
    {
        const int hz = 22050;
        float dur = 0.42f;
        int n = (int)(hz * dur);
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            // 高めから下がる悲しげなグライド
            float freq = Mathf.Lerp(620f, 280f, t * t);
            phase += 2f * Mathf.PI * freq / hz;
            float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 1.8f);
            // わずかなビブラート＋粒立ち
            float vib = 1f + 0.04f * Mathf.Sin(t * 55f);
            float grit = (Mathf.PerlinNoise(t * 40f, 0.3f) * 2f - 1f) * 0.12f;
            data[i] = (Mathf.Sin(phase) * vib * 0.55f + grit) * env * 0.38f;
        }
        var clip = AudioClip.Create("RustDistressWhine", n, 1, hz, false);
        clip.SetData(data, 0);
        return clip;
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

        // 光る遺物回収ボーナス：潤滑油（その時々でランダム）
        int scrapOilBonus = RollScrapOilBonus(count);
        oilCount = Mathf.Max(0, oilCount) + scrapOilBonus;

        string scrapSpeech = "";
        switch (count)
        {
            case 1:
                scrapSpeech = "ピピピッ！綺麗なギアだ…！指先の手応え、嬉しいね";
                break;
            case 2:
                scrapSpeech = "ピロッ……また繋がったよ。胸の奥が、すこし暖かい";
                break;
            case 3:
                scrapSpeech = "ピキーン！歯車がカチリと噛み合ったよ…！僕ら、自分の足で走れる……！";
                break;
            case 4:
                scrapSpeech = "ピピッ！また見つけたよ！寄り道の先で、Nikoと一緒に宝物を拾えたね";
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
        SetSpeech($"{scrapSpeech}（潤滑油 +{scrapOilBonus}／所持: {oilCount}）", 5.2f);

        // キーストーン節目（3, 6, 9, 12個）の特別アクション演出
        if (count == 3 || count == 6 || count == 9 || count == 12)
        {
            StartCoroutine(KeystoneFittedRoutine(count));
        }
    }

    /// <summary>光る遺物回収時の油量。通常2〜6、キーストーン節目は多め。</summary>
    static int RollScrapOilBonus(int collectedCount)
    {
        bool keystone = collectedCount == 3 || collectedCount == 6
                        || collectedCount == 9 || collectedCount == 12;
        if (keystone)
        {
            float r = Random.value;
            if (r < 0.20f) return Random.Range(4, 7);   // 4〜6
            if (r < 0.70f) return Random.Range(7, 12);  // 7〜11
            return Random.Range(12, 19);                // 12〜18
        }
        else
        {
            float r = Random.value;
            if (r < 0.25f) return Random.Range(2, 4);   // 2〜3
            if (r < 0.75f) return Random.Range(4, 7);   // 4〜6
            return Random.Range(7, 11);                 // 7〜10
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
            "風を掴んだね…！いいね"
        };
        SetSpeech(glideStartLines[Random.Range(0, glideStartLines.Length)], 4.0f);
    }

    /// <summary>気流に乗った時のRustの穏やかなセリフ（光るリング通過時は油ボーナス可）</summary>
    public void OnFloatWindCaught(int oilBonus = -1)
    {
        _velocity += Vector3.up * 1.8f;
        // oilBonus < 0 → 呼び出し側任せでランダム（2〜6、たまに多め）
        if (oilBonus < 0)
            oilBonus = RollRingOilBonus();
        if (oilBonus > 0)
            oilCount = Mathf.Max(0, oilCount) + oilBonus;

        string[] windLines = oilBonus > 0
            ? new[]
            {
                $"わぁ…！風が気持ちいいね、Niko！油も +{oilBonus}（所持: {oilCount}）",
                $"ふわりと浮いたよ…！リングの光が油になったよ +{oilBonus}",
                $"風に乗って……ピピッ！潤滑油 +{oilBonus}／所持: {oilCount}",
                $"島を見下ろすと綺麗……油も増えたよ +{oilBonus}、えへへ"
            }
            : new[]
            {
                "わぁ…！風が気持ちいいね、Niko",
                "ふわりと浮いたよ…！",
                "風に乗って、どこまでも行けそう",
                "島を見下ろすと、すごく綺麗だね"
            };
        SetSpeech(windLines[Random.Range(0, windLines.Length)], 4.0f);
        if (oilBonus > 0 && _happyBeepClip != null && _audio != null)
            _audio.PlayOneShot(_happyBeepClip, 0.4f);
    }

    /// <summary>光るリング通過時の油量（その時々でランダム）</summary>
    static int RollRingOilBonus()
    {
        float r = Random.value;
        if (r < 0.20f) return Random.Range(2, 4);   // 2〜3
        if (r < 0.70f) return Random.Range(4, 7);   // 4〜6
        if (r < 0.92f) return Random.Range(7, 11);  // 7〜10
        return Random.Range(11, 16);                // 11〜15 大漁
    }

    /// <summary>指定したテキストを特大ダイアログで発話（プレイヤーが次の行動を起こすまで消えずに維持）</summary>
    public void SpeakCustom(string text, float duration = 4.5f)
    {
        SpeakAs("✦ 相棒 Rust", new Color(0.35f, 0.92f, 0.98f, 1f), text, duration);
    }

    /// <summary>Nikoのセリフを下部吹き出しで表示（ネームタグをNikoに切替）</summary>
    public void SpeakAsNiko(string text, float duration = 4.5f)
    {
        SpeakAs("✦ Niko", new Color(1f, 0.88f, 0.45f, 1f), text, duration);
    }

    public void SpeakAs(string speaker, Color speakerColor, string text, float duration = 4.5f)
    {
        _speechSpeaker = speaker;
        _speechSpeakerColor = speakerColor;
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

    /// <summary>探索中の気軽な話しかけ（自動で消え、次のセリフが続きやすい）</summary>
    public void SetIdleChat(string text, float duration = 3.8f)
    {
        _speechSpeaker = "✦ 相棒 Rust";
        _speechSpeakerColor = new Color(0.35f, 0.92f, 0.98f, 1f);
        _speechText = text;
        _speechTimer = duration;
        _waitingForPlayerAction = false;
        _speechShowTime = 0f;
    }

    public bool HasActiveSpeech => _speechTimer > 0.05f && !string.IsNullOrEmpty(_speechText);
    public string ActiveSpeechText => _speechText;
    public string ActiveSpeechSpeaker => _speechSpeaker;
    public Color ActiveSpeechSpeakerColor => _speechSpeakerColor;
    /// <summary>待ち受け中も常に不透明で見せる</summary>
    public float ActiveSpeechAlpha => HasActiveSpeech ? 1f : 0f;

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
        else if (Time.unscaledTime >= _nextIdleTalk)
        {
            if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsBlockingSpeech)
            {
                _nextIdleTalk = Time.unscaledTime + 2f; // プロローグ終了後すぐ再開
            }
            else
            {
                var tower = AdventureSanctuaryTowerManager.Instance;
                if (tower != null && (tower.IsSkybreakModalActive || tower.IsEpiloguePlaying
                    || tower.ShowGameClearModal || tower.IsClimaxOilPromptActive || tower.ClimaxCrisisStarted))
                {
                    _nextIdleTalk = Time.unscaledTime + 2f;
                }
                else
                {
                    TrySpeakIdleToNiko();
                }
            }
        }
    }

    void TrySpeakIdleToNiko()
    {
        var scraps = AdventureScrapManager.Instance;
        int collected = scraps != null ? scraps.CollectedCount : 12;
        // 本当に調子が悪いときだけ整備催促（常時 wellOiled 切れ扱いにしない）
        bool needsCare = _heat > 0.15f || Time.time < _hitchUntil || oilCount <= 2;

        // 次の発話を約30秒後に固定（壁時計）
        _nextIdleTalk = Time.unscaledTime + 30f;

        var player = AdventurePlayerController.Instance;
        string line;
        if (needsCare)
        {
            line = PickIdleLine(IdleCareLines);
        }
        else if (player != null && player.IsGliding)
        {
            line = PickIdleLine(IdleGlideLines);
        }
        else
        {
            // 話題をローテ：昔 / これから / 調子 / 嬉しい / 楽しい探しもの
            int topic = Random.Range(0, 5);
            switch (topic)
            {
                case 0: line = PickIdleLine(IdlePastLines); break;
                case 1: line = PickIdleLine(IdleFutureLines); break;
                case 2: line = PickIdleLine(IdleBodyLines); break;
                case 3: line = PickIdleLine(IdleHappyLines); break;
                default: line = PickIdleLine(collected < 6 ? IdleSeekEarlyLines : IdleSeekLaterLines); break;
            }
        }

        _lastIdleLine = line;
        SetIdleChat(line, 4.2f);
    }

    string PickIdleLine(string[] pool)
    {
        if (pool == null || pool.Length == 0) return "……Niko";
        if (pool.Length == 1) return pool[0];
        string line = pool[Random.Range(0, pool.Length)];
        // 直前と同じ台詞は避ける
        for (int i = 0; i < 6 && line == _lastIdleLine; i++)
            line = pool[Random.Range(0, pool.Length)];
        return line;
    }

    static readonly string[] IdleCareLines =
    {
        "……ギアが少し重い。油か手当てがあると助かるよ",
        "ピロッ……調子が落ちてる。【E】で整備して",
        "キキッ……動くのがきつい。少し休ませて",
        "油がほしいな。カピタのところか、地面の油でも",
        "関節がきしむ……でも、Nikoがそばなら平気",
    };

    static readonly string[] IdleGlideLines =
    {
        "風に乗って、どこまでも行けそう",
        "島を見下ろすと、すごく綺麗だね",
        "わぁ…！風が気持ちいいね、Niko",
        "この高度、ちょうどいいね",
        "ヒューッ……いいフライトだ",
        "昔はこんな風、シミュレーションでしか知らなかった",
        "自由な空……これが、これからずっと続くといいな",
    };

    static readonly string[] IdlePastLines =
    {
        "昔は倉庫の棚で眠ってた。Nikoが連れ出した日、いちばん覚えてる",
        "最適化の街では、僕の声も『不要』ってラベルだったんだ",
        "塩水に濡れた最初の夜、怖かった。でもNikoの手が温かかった",
        "スクラップ寸前の僕を、Nikoは『相棒』って呼んでくれた",
        "あの波……逃げてきた海の音、まだ耳の奥に残ってる",
        "管理されるだけの日々より、今の不確かさのほうが好き",
        "昔の記憶データ、ところどころ欠落してる。でもNikoの顔は鮮明だよ",
    };

    static readonly string[] IdleFutureLines =
    {
        "翼が直ったら、蒼い空のてっぺんまで行こうね",
        "天蓋の向こう……どんな景色が待ってるんだろう",
        "これからも、Nikoのそばで飛びたい",
        "パーツが揃ったら、もっと遠くまで案内できるよ",
        "いつか、怖がらずに笑いながら飛べるようになりたい",
        "この島のあとにも、冒険はあるのかな……ワクワクする",
        "Nikoと見つけたもの、全部覚えておくね。未来の僕の宝物だ",
    };

    static readonly string[] IdleBodyLines =
    {
        "今日の関節、なめらかだよ。油のおかげかな",
        "ピロッ……ファンの回転、気持ちいい音してる",
        "センサーは快調。潮の匂いまで拾えてるよ",
        "少し眠い……でも、そばにいると元気が出る",
        "ギアが軽やか。今ならどこまでもついていける",
        "胸のコアが温かい。Nikoのペース、ちょうどいい",
        "バランスいいね。転びそうな気配、いまはないよ",
    };

    static readonly string[] IdleHappyLines =
    {
        "えへへ……Nikoと歩くの、楽しい",
        "今、すごく安心してる。ここにいていいんだって感じ",
        "ピキーッ……嬉しい。言葉にすると恥ずかしいけど",
        "風も光も、全部が優しいね。今日はいい日だ",
        "Nikoの足音、好き。リズムが落ち着く",
        "見つかるたびに、胸がふくらむ。幸せのセンサーが鳴ってる",
        "一緒にいるだけで、充電されてるみたい",
    };

    static readonly string[] IdleSeekEarlyLines =
    {
        "ピピッ…砂の中に、光るものないかな？",
        "楽しい探しものしよう。ギアのかけら、どこだろ",
        "あそこに光柱がある気がする。行ってみる？",
        "砂浜を歩くの、冒険の入口みたいでワクワクする",
        "カピタに会うのも楽しいし、遺物探しもしよう",
        "リングをくぐると風が歌うよ。油ももらえるし",
        "草むらの奥、何か隠れてないかな？偵察するよ",
        "最初のパーツ、きっとすぐ見つかる。僕が手伝う",
    };

    static readonly string[] IdleSeekLaterLines =
    {
        "ピピッ…何か光るものがあるかな？",
        "リングをくぐると油も増えるよ。くぐってみよう",
        "タワーの方、まだ遠く見えるね。少しずつ近づこう",
        "楽しいこと探そう。未踏の岸辺、まだあるはず",
        "森の音が変わる場所、何かありそうだよ",
        "崖の上から滑空したら、新しい景色が見えるかも",
        "調子はいいよ。次の遺物、一緒に探そう",
        "光る柱の方角、覚えておくね。道しるべだ",
    };

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
        // Editor Play中の IMGUI 日本語描画は Gizmos フォント汚染を起こすため完全停止。
        // インタラクトは UpdatePlayerInteraction / Input System 側で行う。
        return;
    }

    void OnGUI_DisabledLegacy()
    {
        // 重複インスタンスは一切描画しない（プロンプト多重表示の主因）
        if (Instance != null && Instance != this) return;

        var towerHud = AdventureSanctuaryTowerManager.Instance;
        bool cinematicHide = ShouldHideInteractionPrompt(towerHud)
            || IsClimaxCrisis || IsClimaxOverdrive || _climaxHealing || _skybreakNestle || _prologueDistress;

        // 照準中のスクラップに対するRust遠隔回収プロンプト
        if (!cinematicHide && _aimedScrap != null && CurrentState == RustState.Follow && Camera.main != null)
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

        bool nearCapyta = AdventureCapytaBlessing.IsPlayerNearTalkableCapyta(
            AdventurePlayerController.Instance != null
                ? AdventurePlayerController.Instance.transform.position
                : transform.position);

        // 0. Eキー検知のフォールバック（押しっぱなし連打防止）
        // カピタ会話中は触れない（Update側と同じ優先順位）
        if (!cinematicHide && _isPlayerNear && !nearCapyta
            && Event.current != null
            && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.E
            && Time.time - _lastInteractTime > 0.35f)
        {
            _lastInteractTime = Time.time;
            InteractWithNiko();
        }

        // 1. Niko接近時の頭上インタラクションプロンプト（シネマ中／カピタ会話中は非表示）
        if (!cinematicHide && _isPlayerNear && !nearCapyta && Camera.main != null)
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
                    ? new Color(1.0f, 0.90f, 0.25f)
                    : new Color(0.40f, 0.96f, 1.0f);

                int promptFontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.026f, 20f, 30f));
                GUIStyle pStyle = MakeJpLabel(promptFontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (pStyle.font != null)
                    pStyle.font.RequestCharactersInTexture(prompt, promptFontSize, FontStyle.Bold);

                Vector2 pSize = pStyle.CalcSize(new GUIContent(prompt));
                float padX = 28f;
                float padY = 12f;
                float boxW = Mathf.Max(pSize.x + padX, 320f);
                float boxH = Mathf.Max(pSize.y + padY, promptFontSize + 18f);
                // 頭上ワールド座標は揺れやすいので、画面下部中央に固定して多重に見せない
                float boxX = (Screen.width - boxW) * 0.5f;
                float boxY = Screen.height - boxH - Mathf.Clamp(Screen.height * 0.12f, 90f, 140f);
                Rect promptBoxRect = new Rect(boxX, boxY, boxW, boxH);

                if (_speechBg == null)
                {
                    _speechBg = new Texture2D(1, 1);
                    _speechBg.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.92f));
                    _speechBg.Apply();
                }
                GUI.DrawTexture(promptBoxRect, _speechBg);

                Rect lineRect = new Rect(boxX, boxY, boxW, 3f);
                GUI.DrawTexture(lineRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, textColor, 0, 0);

                DrawOutlinedText(promptBoxRect, prompt, pStyle, textColor, new Color(0f, 0f, 0f, 0.95f));
            }
        }

        // 2. 画面右上のオイル所持数＆RustコンディションHUD
        bool isOiled = Time.time < wellOiledUntil;
        bool isDistressed = (_heat > 0.15f || Time.time < _hitchUntil || !isOiled);
        bool hideStatusHud = cinematicHide
            || (towerHud != null && (towerHud.IsEpiloguePlaying || towerHud.IsClimaxOilPromptActive || towerHud.ShowGameClearModal || towerHud.IsSkybreakModalActive));
        if (!hideStatusHud && (oilCount > 0 || isOiled || isDistressed))
        {
            int badgeSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.022f, 18f, 24f));
            GUIStyle badgeStyle = MakeJpLabel(badgeSize, FontStyle.Bold, TextAnchor.MiddleCenter);

            string status;
            Color bColor;
            if (isOiled)
            {
                status = "✦ Rust好調（整備済）";
                bColor = new Color(0.45f, 0.95f, 0.65f);
            }
            else
            {
                status = $"⚠ 要整備（【E】手当て＆セーブ / 油: {oilCount}）";
                bColor = new Color(1.0f, 0.88f, 0.35f);
            }
            if (badgeStyle.font != null)
                badgeStyle.font.RequestCharactersInTexture(status, badgeSize, FontStyle.Bold);

            GUIContent bContent = new GUIContent(status);
            Vector2 bSize = badgeStyle.CalcSize(bContent);
            float bw = Mathf.Max(200f, bSize.x + 54f);
            float bh = Mathf.Max(36f, badgeSize + 18f);

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

        // 3. セリフダイアログ表示
        if (_speechTimer <= 0f || string.IsNullOrEmpty(_speechText))
            return;

        if (towerHud != null && (towerHud.IsSkybreakModalActive || towerHud.IsEpiloguePlaying || towerHud.IsClimaxOilPromptActive || towerHud.ShowGameClearModal))
            return;

        // しっかり大きく読みやすいシネマフォント設計（1080pで約34pt）
        int bodyFontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.034f, 28f, 40f));
        int nameFontSize = Mathf.RoundToInt(bodyFontSize * 0.72f);

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
        GUIStyle nameStyle = MakeJpLabel(nameFontSize, FontStyle.Bold, TextAnchor.MiddleLeft);

        Rect nameRect = new Rect(x + 28f, y + 10f, boxWidth - 56f, nameFontSize + 4f);
        Color nameCol = _speechSpeakerColor;
        nameCol.a = alpha;
        DrawOutlinedText(nameRect, _speechSpeaker, nameStyle, nameCol, new Color(0f, 0f, 0f, 0.9f * alpha));

        // 行動待ちヒント（右上に上品に表示）
        if (_waitingForPlayerAction)
        {
            GUIStyle hintStyle = MakeJpLabel(Mathf.Max(12, Mathf.RoundToInt(nameFontSize * 0.82f)), FontStyle.Normal, TextAnchor.MiddleRight);
            Rect hintRect = new Rect(x + 28f, y + 10f, boxWidth - 56f, nameFontSize + 4f);
            DrawOutlinedText(hintRect, "（行動・移動で閉じます）", hintStyle, new Color(0.65f, 0.85f, 0.95f, alpha * 0.85f), new Color(0f, 0f, 0f, 0.85f * alpha));
        }

        // 3. セリフ本文（大きくてはっきり読める・クリッピング防止）
        GUIStyle bodyStyle = MakeJpLabel(bodyFontSize, FontStyle.Normal, TextAnchor.UpperLeft, wordWrap: true);
        if (bodyStyle.font != null)
            bodyStyle.font.RequestCharactersInTexture("「" + _speechText + "」", bodyFontSize, FontStyle.Normal);

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
            "ありがとうNiko、身体が軽くなったよ",
            "油を差してくれてありがとう。ギアが滑らかだ",
            "ピピッ…！手当てありがとう。もうギシギシしないよ",
            "整備完了。よし進もう"
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
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
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

    /// <summary>Nikoの頭付近のワールド座標</summary>
    public Vector3 GetNikoHeadPosition()
    {
        if (_lookAt == null) return transform.position;

        if (!_bonesCached || _cachedHead == null)
            CacheNikoBones();

        if (_cachedHead != null)
            return _cachedHead.position;

        // 頭ボーンが無い場合は胸より約35cm上を頭とみなす
        return GetNikoChestPosition() + Vector3.up * 0.35f;
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

