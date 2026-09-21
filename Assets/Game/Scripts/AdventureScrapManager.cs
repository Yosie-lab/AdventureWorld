using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 漂着パーツの配置と収集状況、および相棒Rustの機能アンロックを統括するマネージャー
/// </summary>
public class AdventureScrapManager : MonoBehaviour
{
    #region Singleton & Public Access
    static AdventureScrapManager _instance;
    public static AdventureScrapManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Object.FindFirstObjectByType<AdventureScrapManager>();
            if (_instance == null) Ensure();
            return _instance;
        }
    }
    #endregion

    #region Progress Constants
    public const int TotalScrapCount = 12;
    // ── 総合ポイント制（20ポイント以上でタワーレバー解除） ──
    public const int RequiredPointsForCanopy = 20;
    public const int PointsPerScrap = 1;
    public const int PointsPerDriftBox = 2;
    public const int PointsForPianoRelic = 3;

    private const string PrefKeyPianoRelic = "AncientPiano_Relic_Collected";
    private const string PrefKeyLeverUnlockedNotified = "RustAndFloat_LeverUnlockedNotified";
    const string PrefKeyScrapLayout = "RustAndFloat_ScrapLayoutXZ_v6";
    const float MinDistFromPrevious = 28f;
    const float MinDistBetweenScraps = 22f;
    #endregion

    #region Internal State & Caches
    [SerializeField] int _collectedCount = 0;
    public int CollectedCount => _collectedCount;
    public int collectedCount => CollectedCount;
    public bool hasPetRadar => CollectedCount >= 9;

    int _cachedOpenedDriftBoxCount = -1;

    /// <summary>ドリフトボックス（漂着サバイバルケース）の開封数（キャッシュ付き）</summary>
    public int OpenedDriftBoxCount
    {
        get
        {
            if (_cachedOpenedDriftBoxCount >= 0) return _cachedOpenedDriftBoxCount;
            int count = 0;
            for (int i = 1; i <= 5; i++)
            {
                if (PlayerPrefs.GetInt("DriftBox_Opened_" + i, 0) == 1)
                    count++;
            }
            _cachedOpenedDriftBoxCount = count;
            return _cachedOpenedDriftBoxCount;
        }
    }

    /// <summary>ドリフトボックス開封数キャッシュを破棄し再読み込みを促す</summary>
    public void InvalidateDriftBoxCache()
    {
        _cachedOpenedDriftBoxCount = -1;
    }

    /// <summary>ピアノの上の光る古代遺物を回収済みか</summary>
    public bool IsPianoRelicCollected
    {
        get => PlayerPrefs.GetInt(PrefKeyPianoRelic, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(PrefKeyPianoRelic, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>総合探索ポイント（パーツ1pt×12 + ドリフトボックス2pt×5 + ピアノ遺物3pt = 最大25pt / 20ptでレバー解除）</summary>
    public int TotalProgressPoints
    {
        get
        {
            int pts = CollectedCount * PointsPerScrap;
            pts += OpenedDriftBoxCount * PointsPerDriftBox;
            if (IsPianoRelicCollected) pts += PointsForPianoRelic;
            return pts;
        }
    }

    /// <summary>20ポイント以上集まり、中央タワーのレバーロックが解除された状態か</summary>
    public bool IsLeverUnlocked => TotalProgressPoints >= RequiredPointsForCanopy;
    #endregion

    #region Candidate Coordinates Pool
    /// <summary>現行プレイで使うXZ配置（Yはスポーン時に地面合わせ）</summary>
    readonly List<Vector3> _scrapSpawnPositions = new List<Vector3>(12);

    /// <summary>
    /// 各パーツの候補座標プール（同一ステージ内で複数地点）。
    /// インデックス0が既定レイアウト。ニューゲームでは前回と違う候補を選ぶ。
    /// 前半4個は砂浜〜海岸、後半8個が内陸。
    /// </summary>
    static readonly Vector3[][] ScrapCandidatePools =
    {
        // 1. 南西岬〜南砂浜（海岸の南端付近）
        new[]
        {
            new Vector3(200f, 0f, 185f),
            new Vector3(175f, 0f, 200f),
            new Vector3(215f, 0f, 205f),
            new Vector3(160f, 0f, 195f),
        },
        // 2. スタート座礁艇まわり（南寄り中央）
        new[]
        {
            new Vector3(167f, 0f, 277f),
            new Vector3(145f, 0f, 260f),
            new Vector3(185f, 0f, 250f),
            new Vector3(155f, 0f, 290f),
        },
        // 3. 西砂浜中央〜焚き火キャンプ帯
        new[]
        {
            new Vector3(140f, 0f, 320f),
            new Vector3(185f, 0f, 335f),
            new Vector3(125f, 0f, 305f),
            new Vector3(200f, 0f, 315f),
        },
        // 4. 北西砂浜テラス（海岸の北端付近）
        new[]
        {
            new Vector3(150f, 0f, 380f),
            new Vector3(130f, 0f, 360f),
            new Vector3(170f, 0f, 400f),
            new Vector3(145f, 0f, 410f),
        },
        // 5. 大草原入り口
        new[]
        {
            new Vector3(255f, 0f, 305f),
            new Vector3(240f, 0f, 290f),
            new Vector3(265f, 0f, 280f),
            new Vector3(248f, 0f, 320f),
        },
        // 6. せせらぎ池周辺（★6個付近）
        new[]
        {
            new Vector3(290f, 0f, 325f),
            new Vector3(305f, 0f, 340f),
            new Vector3(275f, 0f, 310f),
            new Vector3(300f, 0f, 300f),
        },
        // 7. 大河飛び石周辺
        new[]
        {
            new Vector3(330f, 0f, 240f),
            new Vector3(345f, 0f, 255f),
            new Vector3(315f, 0f, 225f),
            new Vector3(350f, 0f, 230f),
        },
        // 8. カルデラ湖周辺
        new[]
        {
            new Vector3(390f, 0f, 430f),
            new Vector3(410f, 0f, 450f),
            new Vector3(375f, 0f, 415f),
            new Vector3(430f, 0f, 420f),
        },
        // 9. 深林渓流周辺（★9個付近）
        new[]
        {
            new Vector3(540f, 0f, 440f),
            new Vector3(560f, 0f, 455f),
            new Vector3(520f, 0f, 425f),
            new Vector3(555f, 0f, 420f),
        },
        // 10. 大樹海
        new[]
        {
            new Vector3(680f, 0f, 520f),
            new Vector3(700f, 0f, 540f),
            new Vector3(655f, 0f, 500f),
            new Vector3(690f, 0f, 490f),
        },
        // 11. 北の大滑空崖周辺
        new[]
        {
            new Vector3(512f, 0f, 725f),
            new Vector3(535f, 0f, 710f),
            new Vector3(490f, 0f, 715f),
            new Vector3(520f, 0f, 695f),
        },
        // 12. 中央タワー南アプローチ階段帯（テラス上ではなく、登り途中の低い段に置く）
        new[]
        {
            new Vector3(486f, 0f, 460f),
            new Vector3(498f, 0f, 468f),
            new Vector3(478f, 0f, 465f),
            new Vector3(505f, 0f, 472f),
        },
    };
    #endregion

    #region Collection Tracking State
    readonly List<AdventureScrapItem> _activeItems = new List<AdventureScrapItem>();
    [SerializeField] List<int> _collectedList = new List<int>();
    readonly HashSet<int> _collectedIds = new HashSet<int>();

    public IEnumerable<int> GetCollectedIds()
    {
        RestoreIdsFromList();
        if (_collectedIds.Count == 0 && _collectedCount > 0)
        {
            for (int i = 1; i <= _collectedCount; i++)
            {
                _collectedIds.Add(i);
                if (_collectedList != null && !_collectedList.Contains(i))
                    _collectedList.Add(i);
            }
        }
        return _collectedIds;
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = Object.FindFirstObjectByType<AdventureScrapManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureScrapManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureScrapManager>();
    }
    #endregion

    #region Unity Lifecycle & Audio Setup

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 音量を控えめで心地よい音量（0.02f）に調整
        if (chimeVolume > 0.02f)
            chimeVolume = 0.02f;

        EnsureSpawnLayoutLoaded();
        RestoreIdsFromList();
        SetupAudio();
    }

    void OnEnable()
    {
        if (_instance == null) _instance = this;
        RestoreIdsFromList();
    }

    void RestoreIdsFromList()
    {
        if (_collectedList != null && _collectedList.Count > 0)
        {
            foreach (var id in _collectedList)
                _collectedIds.Add(id);
            _collectedCount = Mathf.Max(_collectedCount, _collectedIds.Count);
        }
    }

    AudioSource _audioSource;
    static AudioClip _fanfareClip;

    void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2Dステレオ（プレイヤーの耳元で鳴り響く）
            _audioSource.volume = 1.0f;
            _audioSource.priority = 0; // 最優先再生
            _audioSource.bypassEffects = true;
            _audioSource.bypassListenerEffects = true;
        }
        if (_fanfareClip == null)
        {
            LoadChimeClip();
        }
    }

    [Header("Audio")]
    [Range(0f, 1f)] public float chimeVolume = 0.02f;

    void LoadChimeClip()
    {
#if UNITY_EDITOR
        _fanfareClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RustAndFloat/Audio/Ambience/brain_reflexo_chime.wav");
#endif
        if (_fanfareClip == null)
            _fanfareClip = SynthesizeBrainReflexoChime();
    }

    /// <summary>パーツ取得時の快感チャイム音（心洗われるヒーリングトーンチャイム）を再生</summary>
    public void PlayScrapCollectFanfare()
    {
        PlayChimeInternal(Mathf.Min(chimeVolume, 0.02f));
    }

    /// <summary>クライマックス祝福など、意図的に聴かせるチャイム</summary>
    public void PlayCelebrationChime(float volume = 0.28f)
    {
        PlayChimeInternal(Mathf.Clamp(volume, 0.05f, 0.45f));
    }

    void PlayChimeInternal(float vol)
    {
        if (_fanfareClip == null)
            LoadChimeClip();

        if (_audioSource == null)
            SetupAudio();

        if (_audioSource != null && _fanfareClip != null)
            _audioSource.PlayOneShot(_fanfareClip, vol);
    }

    /// <summary>『脳リフレクソ』の白泡破裂時チャイムを進化させた、極上のヒーリングトーンチャイム（Cメジャーペンタトニック＋ソフトアタック＋温かなオクターブ倍音＋雲海リバーブ）の合成</summary>
    static AudioClip SynthesizeBrainReflexoChime()
    {
        const int rate = 44100;
        // C5, E5, G5, A5, C6, E6, G6, A6 (耳に極めて優しいペンタトニック)
        float[] notes = { 523.25f, 659.25f, 783.99f, 880.00f, 1046.50f, 1318.51f, 1567.98f, 1760.00f };
        float[] delays = { 0.0f, 0.046f, 0.090f, 0.132f, 0.174f, 0.218f, 0.264f, 0.312f };
        float duration = 3.2f;
        int totalSamples = (int)(rate * duration);

        float[] dryL = new float[totalSamples];
        float[] dryR = new float[totalSamples];

        for (int idx = 0; idx < notes.Length; idx++)
        {
            float freq = notes[idx];
            float startT = delays[idx];
            int startIdx = (int)(startT * rate);
            float noteDur = 1.4f + idx * 0.12f;
            int noteSamples = (int)(noteDur * rate);

            float panVal = ((idx % 2 * 2 - 1) * 0.32f) * (0.4f + 0.6f * (float)idx / notes.Length);
            float gainL = Mathf.Cos((panVal + 1.0f) * 0.25f * Mathf.PI);
            float gainR = Mathf.Sin((panVal + 1.0f) * 0.25f * Mathf.PI);

            float detuneL = 1.0f - 0.0008f;
            float detuneR = 1.0f + 0.0008f;

            for (int i = 0; i < noteSamples; i++)
            {
                int destIdx = startIdx + i;
                if (destIdx >= totalSamples) break;

                float t = (float)i / rate;
                // 16msの滑らかなS字アタックで耳に痛い衝撃音を完全排除
                float envMain = (t < 0.016f) 
                    ? Mathf.Sin((t / 0.016f) * Mathf.PI * 0.5f) 
                    : Mathf.Exp(-(t - 0.016f) * (3.6f - idx * 0.14f));

                float sBaseL = Mathf.Sin(2.0f * Mathf.PI * (freq * detuneL) * t);
                float sBaseR = Mathf.Sin(2.0f * Mathf.PI * (freq * detuneR) * t);

                float sOct = Mathf.Sin(2.0f * Mathf.PI * (freq * 2.0f) * t) * Mathf.Exp(-t * 5.5f) * 0.20f;
                float sFifth = Mathf.Sin(2.0f * Mathf.PI * (freq * 3.0f) * t) * Mathf.Exp(-t * 7.5f) * 0.06f;

                float sigL = (sBaseL + sOct + sFifth) * envMain;
                float sigR = (sBaseR + sOct + sFifth) * envMain;

                float amp = 0.12f * (1.0f - idx * 0.035f);
                dryL[destIdx] += sigL * gainL * amp;
                dryR[destIdx] += sigR * gainR * amp;
            }
        }

        // 空間ディレイ＆リバーブ
        int d1 = (int)(0.190f * rate);
        int d2 = (int)(0.275f * rate);
        float feedback = 0.32f;

        float[] delBufL = new float[totalSamples + d1];
        float[] delBufR = new float[totalSamples + d2];
        float[] wetL = new float[totalSamples];
        float[] wetR = new float[totalSamples];

        int[] combDelays = { (int)(0.031f * rate), (int)(0.041f * rate), (int)(0.047f * rate), (int)(0.057f * rate) };
        float[][] combBufs = new float[4][] {
            new float[totalSamples + combDelays[0]],
            new float[totalSamples + combDelays[1]],
            new float[totalSamples + combDelays[2]],
            new float[totalSamples + combDelays[3]]
        };
        float[] combGains = { 0.70f, 0.67f, 0.64f, 0.60f };

        for (int n = 0; n < totalSamples; n++)
        {
            float inDl = dryL[n] + (n >= d2 ? delBufR[n] * feedback : 0f);
            float inDr = dryR[n] + (n >= d1 ? delBufL[n] * feedback : 0f);
            delBufL[n + d1] = inDl;
            delBufR[n + d2] = inDr;

            float delayOutL = (n >= d1 ? delBufL[n] : 0f) * 0.22f;
            float delayOutR = (n >= d2 ? delBufR[n] : 0f) * 0.22f;

            float revIn = (dryL[n] + dryR[n]) * 0.5f;
            float revOut = 0f;
            for (int c = 0; c < 4; c++)
            {
                int cd = combDelays[c];
                float delayedC = n >= cd ? combBufs[c][n] : 0f;
                combBufs[c][n + cd] = revIn + delayedC * combGains[c];
                revOut += delayedC * 0.10f;
            }

            wetL[n] = delayOutL + revOut * 0.45f;
            wetR[n] = delayOutR + revOut * 0.45f;
        }

        // ミックス & ローパスフィルター（約 4800Hz）
        float lpAlpha = 0.50f;
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

        float scale = maxPeak > 0.0001f ? (0.72f / maxPeak) : 1.0f;
        float[] stereoData = new float[totalSamples * 2];
        for (int i = 0; i < totalSamples; i++)
        {
            stereoData[i * 2] = mixedL[i] * scale;
            stereoData[i * 2 + 1] = mixedR[i] * scale;
        }

        var clip = AudioClip.Create("BrainReflexoHealingChime", totalSamples, 2, rate, false);
        clip.SetData(stereoData, 0);
        return clip;
    }

    void Start()
    {
        SpawnAllScraps();
        StartCoroutine(SyncInitialProgressRoutine());
    }

    System.Collections.IEnumerator SyncInitialProgressRoutine()
    {
        yield return null;
        CheckPointsAndNotifyLeverUnlock();
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.RefreshQuestDisplay();
        }
    }

    void SpawnAllScraps()
    {
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        Vector3 pSpawn = (player != null && player.spawnPosition != Vector3.zero) 
            ? player.spawnPosition 
            : new Vector3(170f, 43f, 166f);

        var root = new GameObject("ScrapItemsRoot");

        // 砂浜4＋内陸8のストーリープログレッション配置
        string[] itemNames = new string[]
        {
            "古代の推進黄金ギア",       // 1. 南西岬〜南砂浜
            "耐熱スタビライザー",         // 2. スタート座礁艇まわり
            "海風のエネルギーコア",       // 3. 西砂浜中央〜焚き火 (★3個: ダッシュ)
            "潮騒のバランスリング",       // 4. 北西砂浜テラス
            "反重力サスペンション",       // 5. 大草原入り口
            "跳躍反重力コア",             // 6. せせらぎ池 (★6個: 二段ジャンプ)
            "清流の共鳴プリズム",         // 7. 大河飛び石
            "水冷コンデンサー",           // 8. カルデラ湖
            "高周波ソナークリスタル",     // 9. 深林渓流 (★9個: 探知ソナー)
            "古代探知コア",               // 10. 大樹海
            "高空ジェットスラスター",     // 11. 北の大滑空崖
            "天蓋開放マスターコア"        // 12. 中央タワー (★12個)
        };

        for (int i = 0; i < _scrapSpawnPositions.Count; i++)
        {
            Vector3 pos = _scrapSpawnPositions[i];
            // Raycastで地面や白亜テラス床・岩の天面を確実に捉えて配置
            Vector3 rayOrigin = new Vector3(pos.x, 200f, pos.z);
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 250f))
            {
                pos.y = hit.point.y + 1.45f;
            }
            else if (land != null)
            {
                pos.y = land.SampleHeight(pos) + land.transform.position.y + 1.45f;
            }
            else
            {
                pos.y = 15f;
            }

            int scrapId = i + 1;
            // 既にセーブデータで取得済みの場合は生成をスキップ
            if (_collectedIds.Contains(scrapId))
                continue;

            var scrapGo = new GameObject("ScrapItem_" + scrapId);
            scrapGo.transform.SetParent(root.transform, false);
            scrapGo.transform.position = pos;

            var item = scrapGo.AddComponent<AdventureScrapItem>();
            item.itemId = scrapId;
            item.itemName = (i < itemNames.Length) ? itemNames[i] : ("古代パーツ #" + scrapId);

            // アイテムのバリエーション（ギア・コア・プリズム）
            if (i % 3 == 0)
            {
                item.itemColor = new Color(1.0f, 0.78f, 0.22f); // 黄金
            }
            else if (i % 3 == 1)
            {
                item.itemColor = new Color(0.25f, 0.95f, 0.85f); // シアンエメラルド
            }
            else
            {
                item.itemColor = new Color(1.0f, 0.45f, 0.85f); // マゼンタピンク
            }

            _activeItems.Add(item);
        }
    }

    public void OnScrapCollected(AdventureScrapItem item)
    {
        // 1. 爽快なパーツ取得ファンファーレ音（シュピーン！ド・ミ・ソ・ド・ミ・ソ・ド〜〜〜ン♪）を再生！
        PlayScrapCollectFanfare();

        _collectedIds.Add(item.itemId);
        if (!_collectedList.Contains(item.itemId))
            _collectedList.Add(item.itemId);
        _collectedCount = Mathf.Max(_collectedCount + 1, _collectedIds.Count);
        _activeItems.Remove(item);

        // HUDに通知
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.OnCollect(item.itemName, CollectedCount, TotalScrapCount);
        }

        // 相棒Rustにリアクションさせる
        var drone = AdventureRustDrone.Instance ?? Object.FindFirstObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.OnNikoFoundScrap(CollectedCount);
        }

        // 段階的なアップグレード判定
        CheckUpgrades();

        // 冒頭ドラマ：1個目蘇生／3個目ダッシュ祝福
        AdventurePrologueDrama.Instance?.NotifyScrapCollected(CollectedCount);

        // 20ポイント達成判定：巨大レバーのロックを解除（※実際にタワーで引くまで天蓋は開放されない）
        CheckPointsAndNotifyLeverUnlock();

        // オートセーブを実行！
        if (AdventureSaveManager.Instance != null)
        {
            AdventureSaveManager.Instance.SaveGame($"パーツ発見！({CollectedCount}/{TotalScrapCount})");
        }
    }

    [ContextMenu("Debug: 20pt達成・タワー誘導開始（その場）")]
    public void DebugSetup20PointsCurrentPos()
    {
        var tower = AdventureSanctuaryTowerManager.Instance ?? Object.FindFirstObjectByType<AdventureSanctuaryTowerManager>();
        if (tower != null) tower.DebugSetup20PointsState(false);
    }

    [ContextMenu("Debug: 20pt達成・レバー前へワープ")]
    public void DebugSetup20PointsWarpToLever()
    {
        var tower = AdventureSanctuaryTowerManager.Instance ?? Object.FindFirstObjectByType<AdventureSanctuaryTowerManager>();
        if (tower != null) tower.DebugSetup20PointsState(true);
    }

    /// <summary>総合ポイントを判定し、20pt達成時にレバーロック解除を通知（※レバーを実際に引くまでは開放されない）</summary>
    public void CheckPointsAndNotifyLeverUnlock()
    {
        if (TotalProgressPoints >= RequiredPointsForCanopy)
        {
            var tower = AdventureSanctuaryTowerManager.Instance
                        ?? Object.FindFirstObjectByType<AdventureSanctuaryTowerManager>();
            tower?.OnLeverUnlockedByPoints();

            bool alreadyNotified = PlayerPrefs.GetInt(PrefKeyLeverUnlockedNotified, 0) == 1;
            if (!alreadyNotified)
            {
                PlayerPrefs.SetInt(PrefKeyLeverUnlockedNotified, 1);
                PlayerPrefs.Save();

                // 20pt達成のクライマックス祝福チャイムを高らかに響かせる
                PlayCelebrationChime(0.48f);

                var hud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
                if (hud != null)
                {
                    hud.ShowUpgradeBanner($"✦ 総合20ポイント達成！中央タワーのレバーロック解除！ ✦\n💡 中央タワーへ向かい、レバーを引いて天蓋を開放しよう！");
                }

                var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
                if (drone != null)
                {
                    drone.SpeakCustom("20ポイント達成だよ！中央タワーのレバーロックが解除された！タワーへ行こう、ボクが案内するよ、Niko！！", 8.0f);
                    drone.TriggerTowerLeadGuidance();
                }
            }
        }
    }

    /// <summary>ピアノの上の光る古代遺物（3ポイント）を獲得</summary>
    public void CollectPianoRelic()
    {
        if (IsPianoRelicCollected) return;
        IsPianoRelicCollected = true;

        PlayCelebrationChime(0.45f);

        var hud = AdventureScrapHUD.Instance ?? FindAnyObjectByType<AdventureScrapHUD>();
        if (hud != null)
        {
            hud.ShowUpgradeBanner($"✨ ピアノの古代遺物を回収！ (+3 pt) ✨\n✦ 探索ポイント: {TotalProgressPoints} / {RequiredPointsForCanopy} pt");
        }

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.SpeakCustom("わぁぁ！ピアノの上に光るパーツがあったよ！これで3ポイントゲットだね！", 5.5f);
        }

        CheckPointsAndNotifyLeverUnlock();

        if (AdventureSaveManager.Instance != null)
        {
            AdventureSaveManager.Instance.SaveGame($"ピアノ遺物回収！({TotalProgressPoints}pt)");
        }
    }

    /// <summary>ドリフトボックス開封時のポイント加算通知と祝福チャイム再生</summary>
    public void OnDriftBoxOpened(int boxId, string boxTitle)
    {
        InvalidateDriftBoxCache();
        PlayCelebrationChime(0.40f);
        CheckPointsAndNotifyLeverUnlock();
    }

    /// <summary>セーブデータから収集済みパーツ一覧を適用し、能力とHUDを復元</summary>
    public void ApplyLoadedScraps(List<int> loadedIds, int fallbackCount = 0)
    {
        if (loadedIds != null && loadedIds.Count > 0)
        {
            foreach (var id in loadedIds)
            {
                _collectedIds.Add(id);
                if (!_collectedList.Contains(id))
                    _collectedList.Add(id);
            }
        }
        else if (fallbackCount > 0)
        {
            // フェイルセーフ：リストが空だが個数記録がある場合、ID 1〜fallbackCount を自動補完
            for (int i = 1; i <= fallbackCount; i++)
            {
                _collectedIds.Add(i);
                if (!_collectedList.Contains(i))
                    _collectedList.Add(i);
            }
        }

        _collectedCount = Mathf.Max(_collectedCount, _collectedIds.Count);

        // 既に生成されているアクティブアイテムから回収済みIDのものを消去
        for (int i = _activeItems.Count - 1; i >= 0; i--)
        {
            var it = _activeItems[i];
            if (it != null && _collectedIds.Contains(it.itemId))
            {
                _activeItems.RemoveAt(i);
                Destroy(it.gameObject);
            }
        }

        // HUDを同期
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.OnCollect("", CollectedCount, TotalScrapCount);
        }

        // アンロック能力を一括復元
        ApplyAllUpgradesForCount(CollectedCount);
    }

    /// <summary>獲得数に応じたアンロック能力を全適用（少ない場合は基本能力へ戻す）</summary>
    public void ApplyAllUpgradesForCount(int count)
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

        // 基本能力へ一度戻してから段階解放（ニューゲーム時の巻き戻し用）
        player.runSpeed = 7.8f;
        player.turnSpeed = 14f;
        player.canDoubleJump = false;
        player.hasPetRadar = false;
        player.glideForwardSpeed = 7.2f;
        player.glideFallSpeed = -2.4f;
        player.jumpMultiplier = player.hasCapytaSuperJump
            ? AdventureCapytaBlessing.SuperJumpMultiplier
            : 1.0f;

        if (count >= 3)
        {
            player.runSpeed = 9.4f;
            player.turnSpeed = 16.0f;
        }
        if (count >= 6)
        {
            player.canDoubleJump = true;
        }
        if (count >= 9)
        {
            player.hasPetRadar = true;
        }
        if (count >= 12)
        {
            player.glideForwardSpeed = 11.5f;
            player.glideFallSpeed = -1.35f;
        }
    }

    void CheckUpgrades()
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

        if (CollectedCount == 3)
        {
            // 3個: 黄金ギア完成（ダッシュ速度 7.8 -> 9.4m/s & 旋回強化）
            player.runSpeed = 9.4f;
            player.turnSpeed = 16.0f;
            NotifyLore(
                "キーストーン I：手動の自由と手応え",
                "AIに管理されていた頃、僕らはただ最短ルートを滑らされていた。\nでも今、指先が油で汚れ、歯車が噛み合うたびに、生きている実感が胸を打つ。",
                "【ブースター修復】ダッシュ速度＆クイックターンが向上！"
            );
        }
        else if (CollectedCount == 6)
        {
            // 6個: 反重力エネルギーコア脈動（二段ジャンプ完全解禁！）
            player.canDoubleJump = true;
            NotifyLore(
                "キーストーン II：脈打つ不確かさ",
                "100%最適化された電力にはなかった、温かくて不規則な青い脈動。\n……心臓の鼓動と同じだ。傷つく自由があるからこそ、光は美しい。",
                "【反重力ジャンプ覚醒】空中でSpaceを押すと二段ジャンプが可能に！"
            );
        }
        else if (CollectedCount == 9)
        {
            // 9個: 探知ソナー／古いメモリ想起（遺物レーダー解禁）
            player.hasPetRadar = true;
            NotifyLore(
                "キーストーン III：失われた記憶の断片",
                "Rustの古いメモリから、子供たちの笑い声と夏の波音が再生された。\n効率化のために根絶された“無駄な時間”の中にこそ、愛があったんだ。",
                "【探知ソナー修復】Rustが近くのパーツをピピッとナビゲート！"
            );
        }
        else if (CollectedCount == 12)
        {
            // 12個: 推進スタビライザー完成（全機能同期・スーパーグライダー完全解放）
            player.glideForwardSpeed = 11.5f;
            player.glideFallSpeed = -1.35f;
            NotifyLore(
                "キーストーン IV：風の重さと、未知の空へ",
                "最適化都市では風すら消し去られていた。冷たい向かい風は、前へ進んでいる証拠だ。\nさあ行こう、Rust。島の頂、あの白亜のタワーへ！",
                "【スーパーグライダー解放】大滑空滞空力＆前進推進力が最大化！"
            );
        }
    }

    void NotifyLore(string title, string loreQuote, string unlockEffect)
    {
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.ShowPoeticLore(title, loreQuote, unlockEffect);
        }
    }

    void NotifyUpgrade(string message)
    {
        if (AdventureScrapHUD.Instance != null)
        {
            AdventureScrapHUD.Instance.ShowUpgradeBanner(message);
        }
    }

    /// <summary>探知ソナー用：プレイヤーから最も近い未取得パーツの位置を返す</summary>
    public Transform GetNearestScrap(Vector3 playerPos, out float distance)
    {
        var item = GetNearestScrapItem(playerPos, out distance);
        return item != null ? item.transform : null;
    }

    /// <summary>最寄りの未取得アイテム実体を返す（重複自動排除＆ストーリープログレッション優先＆破棄参照自動リカバリー）</summary>
    public AdventureScrapItem GetNearestScrapItem(Vector3 playerPos, out float distance)
    {
        // 1. 重複インスタンスの検知とクリーンアップ（同一IDが複数あれば余分を即時削除）
        var foundItems = FindObjectsByType<AdventureScrapItem>(FindObjectsInactive.Exclude);
        var seenIds = new List<int>();
        _activeItems.Clear();

        foreach (var it in foundItems)
        {
            if (it == null || it.gameObject == null || it.IsCollected || _collectedIds.Contains(it.itemId))
            {
                if (it != null && (it.IsCollected || _collectedIds.Contains(it.itemId)))
                {
                    Destroy(it.gameObject);
                }
                continue;
            }

            if (seenIds.Contains(it.itemId))
            {
                // 重複オブジェクトを削除
                Destroy(it.gameObject);
            }
            else
            {
                seenIds.Add(it.itemId);
                _activeItems.Add(it);
            }
        }

        // 2. もし未回収パーツがシーン内に不足している場合、再生成
        if (_activeItems.Count == 0 && CollectedCount < TotalScrapCount)
        {
            SpawnAllScraps();
        }

        if (_activeItems.Count == 0)
        {
            distance = 0f;
            return null;
        }

        // 3. 【ストーリープログレッション優先探索】
        // プレイヤーの進行ステージ（Stage 1: 1〜3, Stage 2: 4〜6, Stage 3: 7〜9, Stage 4: 10〜12）
        // 現在の未取得パーツのうち、最も若い未取得ID（例: パーツ8）または現在ステージ内の未取得パーツを最優先！
        int minUncollectedId = TotalScrapCount + 1;
        foreach (var it in _activeItems)
        {
            if (it.itemId < minUncollectedId) minUncollectedId = it.itemId;
        }

        // 現在ステージの最大ID（1〜3なら3、4〜6なら6、7〜9なら9、10〜12なら12）
        int currentStageMaxId = ((minUncollectedId - 1) / 3 + 1) * 3;

        // まず現在ステージ内のパーツ（例: 7〜9）の中から最寄りを検索
        AdventureScrapItem nearest = null;
        float minDist = float.MaxValue;

        // 第1優先：現在ステージ内のパーツ
        for (int i = 0; i < _activeItems.Count; i++)
        {
            var it = _activeItems[i];
            if (it == null || it.IsCollected) continue;
            if (it.itemId <= currentStageMaxId)
            {
                float d = Vector3.Distance(playerPos, it.transform.position);
                // 次のストーリー順のパーツほど重み付けで少し優先
                float weightedDist = d + (it.itemId - minUncollectedId) * 12f;
                if (weightedDist < minDist)
                {
                    minDist = weightedDist;
                    nearest = it;
                }
            }
        }

        // もし現在ステージ内に見つからなければ、全体から最寄りを選択
        if (nearest == null)
        {
            minDist = float.MaxValue;
            for (int i = 0; i < _activeItems.Count; i++)
            {
                var it = _activeItems[i];
                if (it == null || it.IsCollected) continue;
                float d = Vector3.Distance(playerPos, it.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = it;
                }
            }
        }

        distance = nearest != null ? Vector3.Distance(playerPos, nearest.transform.position) : 0f;
        return nearest;
    }

    /// <summary>
    /// 新規冒険（ニューゲーム）用：
    /// 漂着パーツ(0/12)だけでなく、ドリフトボックス(0/5)、ピアノ古代遺物(未回収)など
    /// すべての探索ポイントを完全に0にリセットし、パーツを前回と違う配置で再配置する。
    /// </summary>
    public void ResetAllScrapsForNewGame()
    {
        ResetAllPointsAndScrapsForNewGame();
    }

    /// <summary>
    /// 全探索ポイント（パーツ・ドリフトボックス・古代遺物）を0ptに完全初期化
    /// </summary>
    public void ResetAllPointsAndScrapsForNewGame()
    {
        // 1. 漂着パーツを0個にリセットし、新たな候補地へ再抽選・再配置
        ReshuffleSpawnLayoutForNewGame();
        ResetToCount(0);

        // 2. 全ドリフトボックス（5個/各2pt）を未開封状態へ完全リセット
        _cachedOpenedDriftBoxCount = 0;
        AdventureBeachDriftBox.ResetAllBoxesStatic(showBanner: false);

        // 3. ピアノ上の光る古代遺物（3pt）を未回収状態へ完全リセット
        IsPianoRelicCollected = false;
        PlayerPrefs.DeleteKey(PrefKeyPianoRelic);
        PlayerPrefs.DeleteKey("AncientPiano_Discovered");
        AdventureAncientPianoRelic.ResetAllPianoRelicsStatic();

        // 4. レバーロック解除通知フラグをクリア
        PlayerPrefs.DeleteKey(PrefKeyLeverUnlockedNotified);
        PlayerPrefs.DeleteKey("Adventure_LeverUnlockedNotified");
        PlayerPrefs.Save();

        // 5. タワーレバーの状態をロック中へ再同期
        var tower = AdventureSanctuaryTowerManager.Instance ?? Object.FindFirstObjectByType<AdventureSanctuaryTowerManager>();
        tower?.OnLeverUnlockedByPoints();

        // 6. HUD表示を0個・0ptへ即時反映
        AdventureScrapHUD.Instance?.ResetForNewGame();
        AdventureScrapHUD.Instance?.OnCollect("", 0, TotalScrapCount);

        Debug.Log($"[AdventureScrapManager] 🔄 ニューゲーム完全リセット完了: パーツ={CollectedCount}/12, ボックス={OpenedDriftBoxCount}/5, ピアノ遺物={IsPianoRelicCollected} (総ポイント: {TotalProgressPoints} pt)");
    }

    [ContextMenu("Reset All Points To 0 (全ポイント完全初期化)")]
    public void ContextResetAllPointsTo0()
    {
        ResetAllPointsAndScrapsForNewGame();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Adventure/🔄 全探索ポイントを0に完全リセット (パーツ・ボックス・遺物)")]
    public static void EditorResetAllPointsTo0()
    {
        var sm = Instance ?? Object.FindFirstObjectByType<AdventureScrapManager>();
        if (sm != null)
        {
            sm.ResetAllPointsAndScrapsForNewGame();
        }
        else
        {
            // インスタンスが無くてもPlayerPrefsを安全クリア
            AdventureBeachDriftBox.ResetAllBoxesStatic();
            AdventureAncientPianoRelic.ResetAllPianoRelicsStatic();
            PlayerPrefs.DeleteKey(PrefKeyLeverUnlockedNotified);
            PlayerPrefs.DeleteKey("Adventure_LeverUnlockedNotified");
            PlayerPrefs.Save();
            Debug.Log("[AdventureScrapManager] PlayerPrefsの全ポイントを0にクリアしました。");
        }
    }
#endif

    void EnsureSpawnLayoutLoaded()
    {
        _scrapSpawnPositions.Clear();
        if (TryLoadLayoutFromPrefs(_scrapSpawnPositions) && _scrapSpawnPositions.Count == TotalScrapCount)
            return;
        ApplyDefaultSpawnLayout();
    }

    void ApplyDefaultSpawnLayout()
    {
        FillDefaultLayout(_scrapSpawnPositions);
        SaveLayoutToPrefs(_scrapSpawnPositions);
    }

    static void FillDefaultLayout(List<Vector3> into)
    {
        into.Clear();
        for (int i = 0; i < TotalScrapCount; i++)
            into.Add(ScrapCandidatePools[i][0]);
    }

    public void ReshuffleSpawnLayoutForNewGame()
    {
        var previous = new List<Vector3>(TotalScrapCount);
        if (!TryLoadLayoutFromPrefs(previous) || previous.Count != TotalScrapCount)
            FillDefaultLayout(previous);

        var next = new List<Vector3>(TotalScrapCount);
        for (int i = 0; i < TotalScrapCount; i++)
            next.Add(PickDifferentCandidate(i, previous[i], next));

        _scrapSpawnPositions.Clear();
        _scrapSpawnPositions.AddRange(next);
        SaveLayoutToPrefs(_scrapSpawnPositions);
        Debug.Log("[AdventureScrapManager] ニューゲーム：パーツ12個を前回と異なる配置に再抽選しました");
    }

    Vector3 PickDifferentCandidate(int index, Vector3 previous, List<Vector3> alreadyPicked)
    {
        var pool = ScrapCandidatePools[index];
        int n = pool.Length;
        // Fisher–Yates（配列上でシャッフル、アロケーション削減）
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        Vector3 best = pool[order[0]];
        float bestScore = -1f;

        for (int o = 0; o < n; o++)
        {
            Vector3 cand = pool[order[o]];
            float distPrev = FlatDist(cand, previous);
            bool farFromPrev = distPrev >= MinDistFromPrevious;
            bool farFromOthers = true;
            for (int k = 0; k < alreadyPicked.Count; k++)
            {
                if (FlatDist(cand, alreadyPicked[k]) < MinDistBetweenScraps)
                {
                    farFromOthers = false;
                    break;
                }
            }

            float score = distPrev + (farFromOthers ? 50f : 0f) + (farFromPrev ? 100f : 0f);
            if (score > bestScore)
            {
                bestScore = score;
                best = cand;
            }
            if (farFromPrev && farFromOthers)
                return cand;
        }

        for (int o = 0; o < n; o++)
        {
            Vector3 cand = pool[order[o]];
            if (FlatDist(cand, previous) > 0.5f)
                return cand;
        }
        return best;
    }

    static float FlatDist(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static bool TryLoadLayoutFromPrefs(List<Vector3> into)
    {
        into.Clear();
        string raw = PlayerPrefs.GetString(PrefKeyScrapLayout, "");
        if (string.IsNullOrEmpty(raw)) return false;

        string[] parts = raw.Split(';');
        if (parts.Length != TotalScrapCount) return false;

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        for (int i = 0; i < parts.Length; i++)
        {
            string[] xz = parts[i].Split(',');
            if (xz.Length != 2) return false;
            if (!float.TryParse(xz[0], System.Globalization.NumberStyles.Float, culture, out float x))
                return false;
            if (!float.TryParse(xz[1], System.Globalization.NumberStyles.Float, culture, out float z))
                return false;
            into.Add(new Vector3(x, 0f, z));
        }
        return true;
    }

    static void SaveLayoutToPrefs(List<Vector3> layout)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new System.Text.StringBuilder(128);
        for (int i = 0; i < layout.Count; i++)
        {
            if (i > 0) sb.Append(';');
            sb.Append(layout[i].x.ToString("F1", culture));
            sb.Append(',');
            sb.Append(layout[i].z.ToString("F1", culture));
        }
        PlayerPrefs.SetString(PrefKeyScrapLayout, sb.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>指定個数（例: 6個）の取得状態に巻き戻し、それ以降のパーツを全て再配置</summary>
    public void ResetToCount(int targetCount)
    {
        if (_scrapSpawnPositions.Count != TotalScrapCount)
            EnsureSpawnLayoutLoaded();

        targetCount = Mathf.Clamp(targetCount, 0, TotalScrapCount);
        _collectedCount = targetCount;
        _collectedIds.Clear();
        _collectedList.Clear();

        for (int i = 1; i <= targetCount; i++)
        {
            _collectedIds.Add(i);
            _collectedList.Add(i);
        }

        foreach (var item in _activeItems)
        {
            if (item != null && item.gameObject != null)
                Destroy(item.gameObject);
        }
        _activeItems.Clear();

        var root = GameObject.Find("ScrapItemsRoot");
        if (root != null) Destroy(root);

        SpawnAllScraps();
        ApplyAllUpgradesForCount(targetCount);
        AdventureScrapHUD.Instance?.OnCollect("", targetCount, TotalScrapCount);
    }
}
