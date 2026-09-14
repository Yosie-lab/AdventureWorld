using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 漂着パーツの配置と収集状況、および相棒Rustの機能アンロックを統括するマネージャー
/// </summary>
public class AdventureScrapManager : MonoBehaviour
{
    static AdventureScrapManager _instance;
    public static AdventureScrapManager Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindAnyObjectByType<AdventureScrapManager>();
            if (_instance == null) Ensure();
            return _instance;
        }
    }

    public const int TotalScrapCount = 12;
    [SerializeField] int _collectedCount = 0;
    public int CollectedCount => Mathf.Max(_collectedCount, Mathf.Max(_collectedIds != null ? _collectedIds.Count : 0, _collectedList != null ? _collectedList.Count : 0));
    public int collectedCount => CollectedCount;
    public bool hasPetRadar => CollectedCount >= 9;

    readonly List<Vector3> _scrapSpawnPositions = new List<Vector3>
    {
        // ── Stage 1: 白砂ビーチ・海辺（パーツ 0〜3個 → ダッシュ速度UP解禁！） ──
        // 1. 【砂浜】二人の座礁漂着艇の先、波打ち際の白砂（開始時に視界の正面9m先で光り輝く）
        new Vector3(167f, 0f, 277f),
        // 2. 【砂浜】初日の焚き火キャンプ跡の木陰
        new Vector3(182f, 0f, 332f),
        // 3. 【砂浜】南西の岬・砂浜から内陸大草原への登り口
        new Vector3(195f, 0f, 230f),

        // ── Stage 2: 西側大草原・せせらぎ池（パーツ 4〜6個 → 二段ジャンプ解禁！） ──
        // 4. 【大草原】草原の入り口・小道沿い
        new Vector3(240f, 0f, 290f),
        // 5. 【大草原】憩いのせせらぎ池の畔
        new Vector3(290f, 0f, 325f),
        // 6. 【大草原】南西大河の飛び石の岩の上
        new Vector3(330f, 0f, 240f),

        // ── Stage 3: カルデラ湖・深林渓流・大樹海（パーツ 7〜9個 → 探知ソナー解禁！） ──
        // 7. 【カルデラ湖】湖東岸・睡蓮の木陰
        new Vector3(390f, 0f, 430f),
        // 8. 【深林渓流】東の山岳渓谷激流の巨大苔岩
        new Vector3(540f, 0f, 440f),
        // 9. 【大樹海】東部巨木原生林の古樹の根元
        new Vector3(680f, 0f, 520f),

        // ── Stage 4: 北の高地・大滑空崖・中央タワー（パーツ 10〜12個 → 大滑空完成！） ──
        // 10. 【北東高地】絶景の見晴らし岩
        new Vector3(640f, 0f, 650f),
        // 11. 【北の大滑空崖】標高92mジャンプ台先端
        new Vector3(512f, 0f, 725f),
        // 12. 【中央タワー】サンクチュアリ中央広場・白亜テラス南側正面
        new Vector3(512f, 0f, 496f)
    };

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
        var existing = FindAnyObjectByType<AdventureScrapManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureScrapManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureScrapManager>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // 音量を少し控えめで心地よい音量（0.32f）に調整
        if (chimeVolume > 0.32f)
            chimeVolume = 0.32f;

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
    [Range(0f, 1f)] public float chimeVolume = 0.32f;

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
        if (_fanfareClip == null)
            LoadChimeClip();

        if (_audioSource == null)
            SetupAudio();

        if (_audioSource != null && _fanfareClip != null)
        {
            _audioSource.PlayOneShot(_fanfareClip, chimeVolume);
        }
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
    }

    void SpawnAllScraps()
    {
        var land = Terrain.activeTerrain ?? FindAnyObjectByType<Terrain>();
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        Vector3 pSpawn = (player != null && player.spawnPosition != Vector3.zero) 
            ? player.spawnPosition 
            : new Vector3(170f, 43f, 166f);

        var root = new GameObject("ScrapItemsRoot");

        // 海岸から内陸へと続く12個のストーリープログレッション配置
        string[] itemNames = new string[]
        {
            "古代の推進黄金ギア",       // 1. 座礁艇
            "耐熱スタビライザー",         // 2. 焚き火キャンプ跡
            "海風のエネルギーコア",       // 3. 砂浜岬 (★3個: ダッシュ速度UP)
            "反重力サスペンション",       // 4. 草原小道
            "清流の共鳴プリズム",         // 5. せせらぎ池
            "跳躍反重力コア",             // 6. 大河飛び石 (★6個: 二段ジャンプ)
            "水冷コンデンサー",           // 7. カルデラ湖
            "高周波ソナークリスタル",     // 8. 深林渓流
            "古代探知コア",               // 9. 大樹海 (★9個: 探知ソナー)
            "超伝導エアフォイル",         // 10. 北東高地
            "高空ジェットスラスター",     // 11. 北の大滑空崖
            "天蓋開放マスターコア"        // 12. 中央タワー (★12個: 大滑空完成)
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
        var drone = FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.OnNikoFoundScrap(CollectedCount);
        }

        // 段階的なアップグレード判定
        CheckUpgrades();

        // オートセーブを実行！
        if (AdventureSaveManager.Instance != null)
        {
            AdventureSaveManager.Instance.SaveGame($"パーツ発見！({CollectedCount}/{TotalScrapCount})");
        }
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

    /// <summary>獲得数に応じたアンロック能力を全適用</summary>
    public void ApplyAllUpgradesForCount(int count)
    {
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player == null) return;

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

    /// <summary>新規冒険（ニューゲーム）用に収集状態を0個に完全リセットして全パーツを砂浜から再配置</summary>
    public void ResetAllScrapsForNewGame()
    {
        ResetToCount(0);
    }

    /// <summary>指定個数（例: 6個）の取得状態に巻き戻し、それ以降のパーツを全て再配置</summary>
    public void ResetToCount(int targetCount)
    {
        targetCount = Mathf.Clamp(targetCount, 0, TotalScrapCount);
        _collectedCount = targetCount;
        _collectedIds.Clear();
        _collectedList.Clear();

        for (int i = 1; i <= targetCount; i++)
        {
            _collectedIds.Add(i);
            _collectedList.Add(i);
        }

        // 既存のアイテムを全破棄
        foreach (var item in _activeItems)
        {
            if (item != null && item.gameObject != null)
                Destroy(item.gameObject);
        }
        _activeItems.Clear();

        var root = GameObject.Find("ScrapItemsRoot");
        if (root != null) Destroy(root);

        // 未取得パーツ（targetCount + 1 〜 TotalScrapCount）を全再生成
        SpawnAllScraps();

        // 獲得数に応じた能力アンロックを一括同期
        ApplyAllUpgradesForCount(targetCount);

        // HUDを更新
        AdventureScrapHUD.Instance?.OnCollect("", targetCount, TotalScrapCount);
    }
}
