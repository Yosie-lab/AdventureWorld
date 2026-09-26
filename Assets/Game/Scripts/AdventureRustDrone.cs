using UnityEngine;
using System.Collections;
using System.Linq;

public partial class AdventureRustDrone : MonoBehaviour
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

    // 3大お宝レーダー探知（スクラップ・漂流木箱・貝殻）
    public enum GuidedTreasureType
    {
        None,
        Scrap,
        DriftBox,
        Seashell
    }

    GuidedTreasureType _guidedTreasureType = GuidedTreasureType.None;
    GuidedTreasureType _prevGuidedTreasureType = GuidedTreasureType.None;
    Vector3 _guidedTreasurePos;
    Color _radarLightColor = Color.yellow;
    Light _radarFlashLight;
    float _nextRadarBeepTime = 0f;

    AdventureScrapItem _guidedScrap;
    float _nextGuideNotice = 0f;
    bool _isPointingToScrap = false;
    public bool IsGuidingTreasure => _guidedTreasureType != GuidedTreasureType.None;

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


    public static AdventureRustDrone Instance { get; private set; }

    public static void Ensure()
    {
        if (Instance != null && Instance.gameObject != null)
            return;

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

        // ドローン自身や子オブジェクトのコライダーがNikoの歩行やレイキャストを邪魔しないよう完全除去
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            Destroy(col);
        }
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
        SetupRadarLight();
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

    /// <summary>プレイヤーの隣へドローンを即時テレポート・静止させる（ニューゲーム／リセット用）</summary>
    public void TeleportNearPlayer()
    {
        EnsureLookAtCached();
        if (_lookAt != null)
        {
            Vector3 nest = GetNikoChestPosition() + _lookAt.forward * 0.75f + _lookAt.right * 0.7f + Vector3.up * 0.15f;
            transform.position = nest;
            _lagTarget = nest;
            _velocity = Vector3.zero;
        }
        else
        {
            var player = AdventurePlayerController.Instance;
            if (player != null)
            {
                Vector3 p = player.transform.position + player.transform.right * 1.0f + Vector3.up * 1.2f;
                transform.position = p;
                _lagTarget = p;
                _velocity = Vector3.zero;
            }
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

    /// <summary>着せ替えやクラフト成功時の大はしゃぎ（ピョンと跳ねて宙返り＆キラキラ音）</summary>
    public void TriggerCelebrate()
    {
        _velocity += Vector3.up * 2.8f;
        SpawnGoldSparkles(transform.position + Vector3.up * 0.4f, 40);
        if (_audio != null && _happyBeepClip != null)
        {
            _audio.pitch = Random.Range(1.15f, 1.35f);
            _audio.PlayOneShot(_happyBeepClip, 0.85f);
        }
        CurrentState = RustState.Celebrating;
        _stateTimer = 1.4f;
    }

    float _nextIdleTalk;
    string _lastIdleLine = "";
    float _nextDriftBoxCheckTime = 5.0f;
    int _lastNotifiedBoxId = -1;

    void TickDriftBoxDetection()
    {
        if (Time.time < _nextDriftBoxCheckTime) return;
        _nextDriftBoxCheckTime = Time.time + 22f; // 22秒間隔でチェック

        if (IsClimaxCrisis || _skybreakNestle || _prologueDistress || CurrentState != RustState.Follow)
            return;

        var box = AdventureBeachDriftBox.GetNearestUnopenedBox(transform.position, out float dist);
        if (box != null && dist <= 75f && box.boxId != _lastNotifiedBoxId)
        {
            _lastNotifiedBoxId = box.boxId;
            Vector3 diff = box.transform.position - transform.position;
            string dirName;
            if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
                dirName = diff.x > 0 ? "東側" : "西側";
            else
                dirName = diff.z > 0 ? "北側" : "南側";

            if (_audio != null && _sonarBeepClip != null)
                _audio.PlayOneShot(_sonarBeepClip, 0.65f);

            string shortName = box.boxTitle.Replace("漂着", "");
            SpeakCustom($"ピピッ！{dirName}の白砂ビーチに「{shortName}」の電波反応だよ！", 5.2f);
        }
    }

    void Update()
    {
        // セリフ進行は lookAt 無しでも回す（吹き出しが見えない事故防止）
        UpdateSpeech();

        if (_lookAt == null)
            return;

        // プロローグの遭難目覚め演出中はAdventurePrologueDramaが位置・姿勢・覗き込みを直接演出
        if (AdventurePrologueDrama.Instance != null && AdventurePrologueDrama.Instance.IsAwakening)
            return;

        TickDriftBoxDetection();
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
            goal = _lookAt.position + Vector3.up * 1.42f + _lookAt.right * 1.15f;
            float bounce = Mathf.Sin(Time.time * 8.5f) * 0.08f;
            goal.y += bounce;
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
                goal = chestPos + _lookAt.forward * 1.35f + _lookAt.right * 0.45f + Vector3.up * 0.18f;
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
            smoothTime = 0.08f;
        else if (IsClimaxOverdrive)
            smoothTime = 0.05f; // 肩そばへ素早く密着
        else if (IsClimaxCrisis || _skybreakNestle || _prologueDistress)
            smoothTime = 0.12f;
        float maxSpeed = _climaxHealing ? 22f : (IsClimaxOverdrive ? 28f : 8.5f);
        transform.position = Vector3.SmoothDamp(transform.position, _lagTarget, ref _velocity, smoothTime, maxSpeed);

        // 危機・寄り添い・全力中は毎フレーム画面内へ吸着（限界警告／ありがとうで見切れ・消失を防ぐ）
        if ((_skybreakNestle || IsClimaxCrisis || _climaxHealing || IsClimaxOverdrive || _prologueDistress) && !_climaxFalling)
            KeepRustOnScreenNearNiko(force: IsClimaxCrisis || _climaxHealing || IsClimaxOverdrive);

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

        // Nikoの体躯への食い込みを物理的に100%遮断・押し出す安全ガード
        EnforceNikoBodyClearance();

        // 回転の計算
        Vector3 to = goal - transform.position;
        if (IsClimaxOverdrive && _lookAt != null)
        {
            // 頭直視だと機首が胸に刺さるので、頭の少し外側を見る
            Vector3 head = GetNikoHeadPosition();
            Vector3 away = transform.position - GetNikoChestPosition();
            away.y = 0f;
            if (away.sqrMagnitude > 0.01f) away.Normalize();
            else away = _lookAt.right;
            to = (head + away * 0.35f) - transform.position;
        }
        else if (_climaxHealing && _lookAt != null)
        {
            to = GetNikoHeadPosition() - transform.position;
        }
        else if (CurrentState == RustState.Follow)
        {
            to = _lookAt.position + Vector3.up * 0.7f - transform.position;
            if (_isGuidingToTower)
                to = SanctuaryTowerCenter + Vector3.up * 2.0f - transform.position;
            else if (_guidedTreasureType != GuidedTreasureType.None)
                to = _guidedTreasurePos + Vector3.up * 0.25f - transform.position;
            else if (_isPointingToScrap && _guidedScrap != null)
                to = _guidedScrap.transform.position + Vector3.up * 0.3f - transform.position;
        }
        else if (CurrentState == RustState.Returning)
        {
            to = _lookAt.position + Vector3.up * 1.35f - transform.position;
        }
        else if (CurrentState == RustState.Petting)
        {
            Vector3 lookTarget = (_skybreakNestle || IsClimaxCrisis || _prologueDistress)
                ? GetNikoChestPosition() + Vector3.up * 0.2f
                : GetNikoChestPosition() + Vector3.up * 0.32f;
            to = lookTarget - transform.position;
        }

        if (to.sqrMagnitude > 0.04f)
        {
            Quaternion look = Quaternion.LookRotation(to);
            if (CurrentState == RustState.Celebrating)
            {
                // 流麗な360度宙返り＆ハッピーバウンス！
                look = GetCelebrationRotation(look);
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
            else if ((_guidedTreasureType != GuidedTreasureType.None || _isPointingToScrap) && CurrentState == RustState.Follow)
                look *= Quaternion.Euler(Mathf.Sin(Time.time * 10f) * 6f, 0f, Mathf.Cos(Time.time * 8f) * 4f);
            else if (CurrentState == RustState.Follow)
            {
                // 自律感情・好奇心（水濡れプルプル・花見下ろし・うたた寝・首かしげ）の姿勢補正
                look = ApplyCuriosityRotation(look);
            }

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
        UpdateCuriosity();
    }

    void LateUpdate()
    {
        // Nikoの移動処理後にも毎フレーム体躯クリアランスを強制し、めり込みを完全排除
        EnforceNikoBodyClearance();
    }

    /// <summary>
    /// Nikoの体躯（足元〜頭上、半径0.82m）への食い込みを物理的に100%遮断・押し出す安全ガード。
    /// 急停止・急旋回・寄り添い・スキンシップ・水パニック等、いかなる状況でもNikoの体にRustがめり込むのを完全根絶する。
    /// </summary>
    void EnforceNikoBodyClearance()
    {
        if (_lookAt == null) return;

        Vector3 nikoPos = _lookAt.position;
        Vector3 curPos = transform.position;

        // Nikoの体躯の高さ範囲（足元 -0.1m 〜 頭上 +0.25m）
        float minY = nikoPos.y - 0.1f;
        float maxY = nikoPos.y + 1.85f;

        if (curPos.y < minY || curPos.y > maxY)
            return; // Nikoの頭上高く、または足元より下なら干渉なし

        Vector2 nikoXZ = new Vector2(nikoPos.x, nikoPos.z);
        Vector2 droneXZ = new Vector2(curPos.x, curPos.z);
        float flatDist = Vector2.Distance(droneXZ, nikoXZ);

        // Niko体幹半径 (0.40m) + Rust球体半径・アクセサリー余裕 (0.42m) = 安全距離 0.82m
        const float minSafeRadius = 0.82f;

        if (flatDist < minSafeRadius)
        {
            Vector2 pushDir = droneXZ - nikoXZ;
            if (pushDir.sqrMagnitude < 0.0001f)
            {
                // 完全に同軸の場合はNikoの右斜め後方へ押し出し
                Vector3 defaultDir = (_lookAt.right * 0.85f - _lookAt.forward * 0.52f).normalized;
                pushDir = new Vector2(defaultDir.x, defaultDir.z);
            }
            else
            {
                pushDir.Normalize();
            }

            Vector2 safeXZ = nikoXZ + pushDir * minSafeRadius;
            transform.position = new Vector3(safeXZ.x, curPos.y, safeXZ.y);
            _lagTarget = new Vector3(safeXZ.x, _lagTarget.y, safeXZ.y);

            // Nikoの中心に向かう速度成分をカットし、めり込み慣性を消去
            Vector3 pushDir3D = new Vector3(pushDir.x, 0f, pushDir.y);
            float inwardVel = Vector3.Dot(_velocity, -pushDir3D);
            if (inwardVel > 0f)
            {
                _velocity = Vector3.ProjectOnPlane(_velocity, pushDir3D);
            }
        }
    }

    static Camera ResolveCommandCamera()
    {
        var follow = FindAnyObjectByType<AdventureCameraFollow>();
        if (follow != null)
        {
            var c = follow.GetComponent<Camera>() ?? follow.GetComponentInChildren<Camera>();
            if (c != null && c.isActiveAndEnabled) return c;
        }
        if (Camera.main != null && Camera.main.isActiveAndEnabled)
            return Camera.main;
        return null;
    }

    /// <summary>視線レイと光の柱（垂直な線分）の最接近。</summary>
    static void ClosestOnRayToSegment(Ray ray, Vector3 a, Vector3 b, out float along, out float separation)
    {
        Vector3 u = ray.direction.normalized;
        Vector3 v = b - a;
        Vector3 w = ray.origin - a;
        float uu = Vector3.Dot(u, u);
        float uv = Vector3.Dot(u, v);
        float vv = Vector3.Dot(v, v);
        float wu = Vector3.Dot(w, u);
        float wv = Vector3.Dot(w, v);
        float denom = uu * vv - uv * uv;
        float s = 0f;
        float t = 0f;
        if (denom > 1e-6f)
        {
            s = (uv * wv - vv * wu) / denom;
            t = (uu * wv - uv * wu) / denom;
        }
        s = Mathf.Max(0f, s);
        t = Mathf.Clamp01(t);
        Vector3 q = a + v * t;
        s = Mathf.Max(0f, Vector3.Dot(q - ray.origin, u));
        Vector3 p = ray.origin + u * s;
        along = s;
        separation = Vector3.Distance(p, q);
    }

    void UpdateCommandInput()
    {
        if (ShouldHideInteractionPrompt(AdventureSanctuaryTowerManager.Instance))
        {
            _aimedScrap = null;
            return;
        }

        // 照準先のスクラップを探す（光の柱を見ていても根元のパーツを狙う）
        _aimedScrap = null;
        var cam = ResolveCommandCamera();
        if (cam != null)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var scraps = FindObjectsByType<AdventureScrapItem>(FindObjectsInactive.Exclude);
            float bestSep = 14f;
            float bestAlong = 180f;
            AdventureScrapItem aimed = null;

            foreach (var s in scraps)
            {
                if (s == null || s.IsCollected) continue;
                Vector3 foot = s.transform.position;
                ClosestOnRayToSegment(ray, foot, foot + Vector3.up * 60f, out float along, out float sep);
                if (along < 2f || along > 180f || sep > 14f) continue;
                if (sep < bestSep - 0.35f || (sep <= bestSep + 0.35f && along < bestAlong))
                {
                    bestSep = sep;
                    bestAlong = along;
                    aimed = s;
                }
            }

            _aimedScrap = aimed;
        }

        // Fキー（New Input Systemによる安全な検知）
        bool fPressed = false;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
            fPressed = kb.fKey.wasPressedThisFrame;
        try { if (Input.GetKeyDown(KeyCode.F)) fPressed = true; } catch { }

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
        if (_lookAt == null)
        {
            _guidedTreasureType = GuidedTreasureType.None;
            _guidedScrap = null;
            _isPointingToScrap = false;
            _isGuidingToTower = false;
            UpdateRadarVisuals();
            return;
        }

        Vector3 nikoPos = _lookAt.position;

        // 1. 中央タワーへの先導誘導（20pt達成〜天蓋開放前：最優先！）
        var scrapMgr = AdventureScrapManager.Instance;
        bool leverUnlocked = scrapMgr != null && scrapMgr.IsLeverUnlocked;
        bool canopyBroken = AdventureSanctuaryTowerManager.IsCanopyBroken;

        if (leverUnlocked && !canopyBroken)
        {
            _isGuidingToTower = true;
            _guidedTreasureType = GuidedTreasureType.None;
            _guidedScrap = null;
            _isPointingToScrap = false;

            float distToTower = Vector3.Distance(nikoPos, SanctuaryTowerCenter);
            if (distToTower <= 14f)
            {
                _isGuidingToTower = false; // タワー到着後はNikoの肩へ寄り添う（オベリスクへ突っ込まない）
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
            UpdateRadarVisuals();
            return;
        }
        else
        {
            _isGuidingToTower = false;
        }

        // 2. スクラップパーツの探知（35m以内、脱出・修復に最重要なお宝）
        AdventureScrapItem nearestScrap = null;
        float scrapDist = 999f;
        if (scrapMgr != null)
        {
            nearestScrap = scrapMgr.GetNearestScrapItem(nikoPos, out scrapDist);
        }

        if (nearestScrap != null && scrapDist <= 35f && !nearestScrap.IsCollected)
        {
            _guidedTreasureType = GuidedTreasureType.Scrap;
            _guidedScrap = nearestScrap;
            _guidedTreasurePos = nearestScrap.transform.position;
            _radarLightColor = new Color(1f, 0.85f, 0.2f); // 黄金の輝き
            _isPointingToScrap = true;

            if (_prevGuidedTreasureType != GuidedTreasureType.Scrap || Time.time >= _nextGuideNotice)
            {
                _nextGuideNotice = Time.time + 12f;
                SetSpeech("ピピピッ！あそこにパーツの反応があるよ！", 3.8f);
                PlayRadarSonarSound();
            }

            _prevGuidedTreasureType = _guidedTreasureType;
            UpdateRadarVisuals();
            return;
        }
        else
        {
            _guidedScrap = null;
        }

        // 3. 漂流木箱の探知（26m以内）
        AdventureBeachDriftBox nearestBox = AdventureBeachDriftBox.GetNearestUnopenedBox(nikoPos, out float boxDist);
        if (nearestBox != null && boxDist <= 26f)
        {
            _guidedTreasureType = GuidedTreasureType.DriftBox;
            _guidedTreasurePos = nearestBox.transform.position;
            _radarLightColor = new Color(0.2f, 0.85f, 1f); // シアンブルー
            _isPointingToScrap = true;

            if (_prevGuidedTreasureType != GuidedTreasureType.DriftBox || Time.time >= _nextGuideNotice)
            {
                _nextGuideNotice = Time.time + 12f;
                SetSpeech("見て見て！あっちに漂着した木箱が落ちてるよ！", 3.8f);
                PlayRadarSonarSound();
            }

            _prevGuidedTreasureType = _guidedTreasureType;
            UpdateRadarVisuals();
            return;
        }

        // 4. 貝殻・シーグラスの探知（22m以内）
        var shellMgr = AdventureBeachSeashellManager.Instance;
        AdventureBeachSeashellItem nearestShell = null;
        float shellDist = 999f;
        if (shellMgr != null)
        {
            nearestShell = shellMgr.GetNearestUncollectedShell(nikoPos, out shellDist);
        }

        if (nearestShell != null && shellDist <= 22f)
        {
            _guidedTreasureType = GuidedTreasureType.Seashell;
            _guidedTreasurePos = nearestShell.transform.position;
            // 貝殻固有のテーマカラー（マリンピンク／エメラルド等）
            _radarLightColor = (nearestShell.themeColor.maxColorComponent > 0.1f) ? nearestShell.themeColor : new Color(1f, 0.45f, 0.75f);
            _isPointingToScrap = true;

            if (_prevGuidedTreasureType != GuidedTreasureType.Seashell || Time.time >= _nextGuideNotice)
            {
                _nextGuideNotice = Time.time + 12f;
                string shellName = string.IsNullOrEmpty(nearestShell.itemName) ? "貝殻" : nearestShell.itemName;
                SetSpeech($"ピピッ！あっちに綺麗な『{shellName}』があるよ！", 3.8f);
                PlayRadarSonarSound();
            }

            _prevGuidedTreasureType = _guidedTreasureType;
            UpdateRadarVisuals();
            return;
        }

        // 周囲にお宝なし
        _guidedTreasureType = GuidedTreasureType.None;
        _prevGuidedTreasureType = GuidedTreasureType.None;
        _isPointingToScrap = false;
        UpdateRadarVisuals();
    }

    void SetupRadarLight()
    {
        if (_radarFlashLight != null) return;

        // ドローン上部のアンテナ位置にレーダーPointLightを生成
        GameObject lightObj = new GameObject("Rust_RadarLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 0.45f, 0f);

        _radarFlashLight = lightObj.AddComponent<Light>();
        _radarFlashLight.type = LightType.Point;
        _radarFlashLight.range = 4.2f;
        _radarFlashLight.intensity = 2.4f;
        _radarFlashLight.color = Color.yellow;
        _radarFlashLight.enabled = false;
    }

    void UpdateRadarVisuals()
    {
        if (_radarFlashLight != null)
        {
            if (_guidedTreasureType != GuidedTreasureType.None)
            {
                _radarFlashLight.color = _radarLightColor;
                // 9Hz でピカピカ点滅（お宝探知ソナー点滅）
                float flash = Mathf.PingPong(Time.time * 9f, 1f);
                _radarFlashLight.enabled = flash > 0.35f;
                _radarFlashLight.intensity = Mathf.Lerp(1.2f, 3.2f, flash);
            }
            else
            {
                _radarFlashLight.enabled = false;
            }
        }

        // 探知中の定期ソナーチャイム音（4.0秒おき）
        if (_guidedTreasureType != GuidedTreasureType.None)
        {
            if (Time.time >= _nextRadarBeepTime)
            {
                _nextRadarBeepTime = Time.time + 4.0f;
                if (_audio != null && _happyBeepClip != null && !AdventurePauseMenu.IsOpen)
                {
                    _audio.pitch = 1.5f;
                    _audio.PlayOneShot(_happyBeepClip, 0.4f);
                }
            }
        }
    }

    void PlayRadarSonarSound()
    {
        if (_audio != null && _happyBeepClip != null && !AdventurePauseMenu.IsOpen)
        {
            _audio.pitch = 1.4f;
            _audio.PlayOneShot(_happyBeepClip, 0.65f);
        }
    }


    Vector3 FollowPoint()
    {
        Vector3 niko = _lookAt.position;
        Vector3 chest = GetNikoChestPosition();

        // 天蓋崩壊〜エンディング同伴：カメラ右斜め前（以前の見え方）
        if ((_skybreakNestle || _prologueDistress) && !_climaxFalling && !IsClimaxCrisis && !_climaxHealing && !IsClimaxOverdrive)
        {
            float nestleBob = Mathf.Sin(Time.time * 3.2f) * 0.03f;
            if (_prologueDistress)
                return EndingBesideNiko(0.72f, 0.78f, 0.18f + nestleBob);
            // 右0.95m・前0.45m、頭〜胸の高さ
            return EndingNestleAtHead(0.45f, 0.95f, 0.08f + nestleBob);
        }

        // クライマックス危機／注油：カメラ側・画面右に寄せて必ず見える
        // 「上空でRustが限界」時点で画面外に出ないこと
        if ((IsClimaxCrisis || _climaxHealing) && !_climaxFalling)
        {
            if (_climaxHealing)
                return VisibleBesideNikoOnScreen(0.95f, 0.62f, 0.22f);
            return VisibleBesideNikoOnScreen(0.78f, 0.58f, 0.14f);
        }

        // オーバードライブ「ありがとうNiko！翼は…」：右肩のすぐ外側
        if (IsClimaxOverdrive)
            return GetOverdriveShoulderNestle();

        // 20pt達成後：RustはNikoの前方2.4m（中央タワーに向かうベクトル）へ先行飛行して先導！
        if (_isGuidingToTower)
        {
            Vector3 toTower = Vector3.ProjectOnPlane(SanctuaryTowerCenter - niko, Vector3.up).normalized;
            Vector3 guidePos = niko + toTower * 2.4f;
            float sBob = Mathf.Sin(Time.time * bobSpeed * 1.8f) * (bobAmount * 1.3f);
            return new Vector3(guidePos.x, chest.y + 0.25f + sBob, guidePos.z);
        }

        // 近くに未発見お宝（スクラップ・漂流木箱・貝殻）がある場合、RustはNikoとお宝を結ぶ線上の前方へ先行飛行して指差し案内！
        if (_guidedTreasureType != GuidedTreasureType.None)
        {
            Vector3 toTreasure = Vector3.ProjectOnPlane(_guidedTreasurePos - niko, Vector3.up);
            if (toTreasure.sqrMagnitude > 0.01f)
                toTreasure.Normalize();
            else
                toTreasure = _lookAt.forward;

            Vector3 guidePos = niko + toTreasure * 2.1f;
            float sBob = Mathf.Sin(Time.time * bobSpeed * 1.8f) * (bobAmount * 1.3f);
            return new Vector3(guidePos.x, chest.y + 0.18f + sBob, guidePos.z);
        }
        else if (_isPointingToScrap && _guidedScrap != null)
        {
            Vector3 toScrap = Vector3.ProjectOnPlane(_guidedScrap.transform.position - niko, Vector3.up).normalized;
            Vector3 guidePos = niko + toScrap * 1.8f;
            float sBob = Mathf.Sin(Time.time * bobSpeed * 1.6f) * (bobAmount * 1.2f);
            return new Vector3(guidePos.x, chest.y + 0.1f + sBob, guidePos.z);
        }

        // 自律好奇心アクション実行中（花・蝶・水辺・アイコンタクト）：対象位置へフワリと移動
        if (_curiosityKind != CuriosityKind.None)
        {
            Vector3 curiousPos = GetCuriosityTargetPosition();
            if (curiousPos != Vector3.zero)
                return curiousPos;
        }

        // 通常追従の理想位置: Nikoの右肩の斜め後ろ（右1.25m、後方1.55m）
        Vector3 rightBack = _lookAt.right * 1.25f - _lookAt.forward * 1.55f;
        Vector3 targetPos = niko + rightBack;

        // Nikoの真正面／体内にRustが居座るのを防止（直径≈1.75m）
        const float bodyClearance = 1.75f;
        float flat = FlatDistance(niko);
        Vector3 toDrone = transform.position - niko;
        float forwardDot = Vector3.Dot(_lookAt.forward, toDrone);
        if (flat < bodyClearance || (flat < 2.0f && forwardDot > -0.15f))
        {
            targetPos = niko + rightBack;
        }
        else if (flat < stopDistance)
        {
            targetPos = new Vector3(transform.position.x, 0f, transform.position.z);
        }

        float followBob = Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        float y = chest.y + 0.05f + followBob; // 常にNikoの胸・肩の高さに追従！

        Vector3 finalGoal = new Vector3(targetPos.x, y, targetPos.z);

        // タワー中央（オベリスク）へのRust突入・埋まり込み防止ガード
        // 天蓋開放前の通常探索中、タワー中心（512, 512）の半径5.2m以内に入り込まないよう外周へクランプ
        if (!AdventureSanctuaryTowerManager.IsCanopyBroken)
        {
            Vector2 goalXZ = new Vector2(finalGoal.x, finalGoal.z);
            Vector2 towerXZ = new Vector2(SanctuaryTowerCenter.x, SanctuaryTowerCenter.z);
            float distToTowerCenter = Vector2.Distance(goalXZ, towerXZ);
            const float towerObeliskRadius = 5.2f;
            if (distToTowerCenter < towerObeliskRadius)
            {
                Vector2 pushDir = (goalXZ - towerXZ).normalized;
                if (pushDir == Vector2.zero) pushDir = new Vector2(0f, -1f); // 南側（テラス側）へ退避
                Vector2 pushedXZ = towerXZ + pushDir * towerObeliskRadius;
                finalGoal.x = pushedXZ.x;
                finalGoal.z = pushedXZ.y;
            }
        }

        return finalGoal;
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
        // キャッシュ済みのInstanceを使用（毎フレームFind廃止）
        var player = AdventurePlayerController.Instance;
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
        if (AdventureCapytaBlessing.IsTalkPromptActive ||
            AdventureCapytaBlessing.IsPlayerNearTalkableCapyta(player.transform.position))
            return;

        if (_isPlayerNear && ePressed && Time.time - _lastInteractTime > 0.35f)
        {
            _lastInteractTime = Time.time;
            InteractWithNiko();
        }
    }

    static bool ShouldHideInteractionPrompt(AdventureSanctuaryTowerManager tower)
    {
        return tower != null && AdventureStoryFlow.HidesRustInteraction;
    }

    /// <summary>Nikoとの直接対話または手当て（常備油により絶対に0にならず、いつでも手当て・全回復可能）</summary>
    public void InteractWithNiko()
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
                if (AdventureStoryFlow.IsPerformance)
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
        // エピローグ／クリア後は整備催促ボードを出さない
        bool postEnding = AdventureSanctuaryTowerManager.IsGameCleared
                          || (AdventureSanctuaryTowerManager.Instance != null
                              && AdventureSanctuaryTowerManager.Instance.EpilogueTriggered);
        bool needsCare = !postEnding
                         && (_heat > 0.15f || Time.time < _hitchUntil || oilCount <= 2);

        // 次の発話を約55〜70秒後に（冒頭の独り言を半分にする）
        _nextIdleTalk = Time.unscaledTime + Random.Range(55f, 70f);

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
        // ScrapManager.Instanceはシングルトン。見つからない場合はソナー無効
        var mgr = AdventureScrapManager.Instance;
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
        if (ShouldHideInteractionPrompt(AdventureSanctuaryTowerManager.Instance)) return;

        if (_aimedScrap != null && CurrentState == RustState.Follow)
        {
            DrawCenterPrompt(0.62f, "【F】光るパーツを取ってきて");
            return;
        }

        if (!_isPlayerNear || CurrentState != RustState.Follow) return;
        var towerMgr = AdventureSanctuaryTowerManager.Instance;
        if (towerMgr != null && towerMgr.IsPlayerNearLever) return;
        var player = AdventurePlayerController.Instance;
        if (player == null) return;
        if (AdventureCapytaBlessing.IsTalkPromptActive ||
            AdventureCapytaBlessing.IsPlayerNearTalkableCapyta(player.transform.position))
            return;

        DrawCenterPrompt(0.68f, "【E】Rustと話す");
    }

    static void DrawCenterPrompt(float yFrac, string label)
    {
        float w = 460f;
        float h = 36f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * yFrac;
        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        style.normal.textColor = Color.black;
        GUI.Label(new Rect(x + 1.5f, y + 1.5f, w, h), label, style);
        style.normal.textColor = new Color(1f, 0.92f, 0.55f);
        GUI.Label(new Rect(x, y, w, h), label, style);
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
        else
        {
            // 最寄りの未開封漂着ボックスを探知
            var box = AdventureBeachDriftBox.GetNearestUnopenedBox(transform.position, out float boxDist);
            if (box != null && boxDist < 120f)
            {
                Vector3 diff = box.transform.position - transform.position;
                string dirName;
                if (Mathf.Abs(diff.x) > Mathf.Abs(diff.z))
                    dirName = diff.x > 0 ? "東（右奥）" : "西（海側）";
                else
                    dirName = diff.z > 0 ? "北（奥の高台）" : "南（浜辺側）";

                if (_audio != null && _sonarBeepClip != null)
                    _audio.PlayOneShot(_sonarBeepClip, 0.7f);

                string boxLabel = string.IsNullOrEmpty(box.BoxDisplayName) ? "漂着ボックス" : box.BoxDisplayName;
                SpeakCustom($"ピピッ！{dirName}の砂浜に『{boxLabel}』が漂着しているよ！", 5.0f);
            }
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

