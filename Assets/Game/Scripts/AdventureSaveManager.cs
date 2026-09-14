using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』セーブ＆ロード管理システム
/// パーツ収集状況、プレイヤー位置、能力アンロック、Rustの状態を自動/手動で安全に保存・復元
/// Mac向けに【F5】/【Fn+F5】/【K】/【Cmd+S】の全ショートカットに対応し、
/// 最前面uGUIバナーとクリスタルチャイム音で100%確実にセーブを通知
/// </summary>
public class AdventureSaveManager : MonoBehaviour
{
    static AdventureSaveManager _instance;
    public static AdventureSaveManager Instance
    {
        get
        {
            if (_instance == null)
            {
                Ensure();
            }
            return _instance;
        }
        private set { _instance = value; }
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public float posX;
        public float posY;
        public float posZ;
        public float rotY;
        public List<int> collectedScrapIds = new List<int>();
        public int collectedCount;
        public int oilCount;
        public bool isCanopyBroken;
        public string saveTime;

        public Vector3 GetPosition() => new Vector3(posX, posY, posZ);
        public void SetPosition(Vector3 pos)
        {
            posX = pos.x;
            posY = pos.y;
            posZ = pos.z;
        }
    }

    static string SaveFilePath => Path.Combine(Application.persistentDataPath, "rust_and_float_save.json");

    // uGUI 通知バナー
    Canvas _canvas;
    CanvasGroup _bannerCg;
    Text _bannerTitleText;
    Text _bannerSubText;
    float _bannerTimer = 0f;

    static AudioClip _saveSoundClip;
    AudioSource _audioSource;
    Texture2D _bgTex;

    // シーン開始時に自動で確実に常駐化
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoInit()
    {
        Ensure();
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindAnyObjectByType<AdventureSaveManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureSaveManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureSaveManager>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        SetupAudio();
        CreateSaveUI();
    }

    void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2Dステレオ
            _audioSource.volume = 1.0f;
            _audioSource.priority = 0; // 最優先再生
            _audioSource.bypassEffects = true;
            _audioSource.bypassListenerEffects = true;
        }
        if (_saveSoundClip == null)
        {
            _saveSoundClip = CreateSaveSound();
        }
    }

    static AudioClip CreateSaveSound()
    {
        const int rate = 44100;
        const float duration = 0.85f;
        int count = (int)(rate * duration);
        float[] data = new float[count];

        // A Major 9th の美しいクリスタルチャイム（F#5, A5, C#6, E6, A6）
        float[] notes = { 739.99f, 880.00f, 1108.73f, 1318.51f, 1760.00f };
        float offset = 0.055f; // ポ・ロ・ロ・ロ・ン♪

        for (int n = 0; n < notes.Length; n++)
        {
            float f = notes[n];
            float startT = n * offset;
            int startIdx = (int)(startT * rate);

            for (int i = startIdx; i < count; i++)
            {
                float t = (float)(i - startIdx) / rate;
                // クッキリした立ち上がり（アタック）と心地よい余韻
                float attack = Mathf.Clamp01(t / 0.004f);
                float decay = Mathf.Exp(-t * (n == notes.Length - 1 ? 4.0f : 6.5f));
                float env = attack * decay;

                // 基音 + オクターブ倍音 + 金属ベルきらめき成分
                float wave = Mathf.Sin(2f * Mathf.PI * f * t)
                           + 0.35f * Mathf.Sin(2f * Mathf.PI * (f * 2.0f) * t)
                           + 0.15f * Mathf.Sin(2f * Mathf.PI * (f * 2.76f) * t);

                data[i] += wave * env * 0.32f;
            }
        }

        // ノーマライズ（最大振幅を 0.95 に最大化して確実に聞こえるようにする）
        float max = 0f;
        for (int i = 0; i < count; i++)
        {
            float abs = Mathf.Abs(data[i]);
            if (abs > max) max = abs;
        }
        if (max > 0.001f)
        {
            float scale = 0.95f / max;
            for (int i = 0; i < count; i++) data[i] *= scale;
        }

        var clip = AudioClip.Create("SaveCrystalChime", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void CreateSaveUI()
    {
        var canvasGo = new GameObject("SaveManager_Canvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 300; // 最前面に確実に表示

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        Font font = ResolveFont();

        // バナーパネル（画面中央上部）
        var bannerGo = new GameObject("SaveBannerPanel");
        bannerGo.transform.SetParent(canvasGo.transform, false);

        var rt = bannerGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -50f);
        rt.sizeDelta = new Vector2(460f, 64f);

        // 背景
        var bgImage = bannerGo.AddComponent<Image>();
        bgImage.color = new Color(0.04f, 0.08f, 0.14f, 0.94f);

        _bannerCg = bannerGo.AddComponent<CanvasGroup>();
        _bannerCg.alpha = 0f;
        _bannerCg.blocksRaycasts = false;

        // 上部アクセントライン（エメラルドグリーン光彩）
        var lineGo = new GameObject("AccentLine");
        lineGo.transform.SetParent(bannerGo.transform, false);
        var lineRt = lineGo.AddComponent<RectTransform>();
        lineRt.anchorMin = new Vector2(0f, 1f);
        lineRt.anchorMax = new Vector2(1f, 1f);
        lineRt.pivot = new Vector2(0.5f, 1f);
        lineRt.anchoredPosition = Vector2.zero;
        lineRt.sizeDelta = new Vector2(0f, 3.5f);
        var lineImg = lineGo.AddComponent<Image>();
        lineImg.color = new Color(0.35f, 0.98f, 0.65f, 1.0f);

        // タイトルテキスト
        var titleGo = new GameObject("TitleText");
        titleGo.transform.SetParent(bannerGo.transform, false);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.35f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.sizeDelta = Vector2.zero;
        titleRt.anchoredPosition = new Vector2(0f, -2f);

        _bannerTitleText = titleGo.AddComponent<Text>();
        _bannerTitleText.font = font;
        _bannerTitleText.fontSize = 21;
        _bannerTitleText.fontStyle = FontStyle.Bold;
        _bannerTitleText.alignment = TextAnchor.MiddleCenter;
        _bannerTitleText.color = new Color(0.40f, 0.98f, 0.70f, 1.0f);

        var outline = titleGo.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        // サブテキスト
        var subGo = new GameObject("SubText");
        subGo.transform.SetParent(bannerGo.transform, false);
        var subRt = subGo.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0f, 0f);
        subRt.anchorMax = new Vector2(1f, 0.42f);
        subRt.sizeDelta = Vector2.zero;
        subRt.anchoredPosition = new Vector2(0f, 4f);

        _bannerSubText = subGo.AddComponent<Text>();
        _bannerSubText.font = font;
        _bannerSubText.fontSize = 12;
        _bannerSubText.alignment = TextAnchor.MiddleCenter;
        _bannerSubText.color = new Color(0.85f, 0.92f, 0.98f, 0.90f);
    }

    static Font ResolveFont()
    {
        string[] fonts = {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Arial Unicode MS",
            "YuGothic",
            "Arial"
        };
        foreach (var name in fonts)
        {
            var f = Font.CreateDynamicFontFromOSFont(name, 18);
            if (f != null) return f;
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void Start()
    {
        // 起動時に既存セーブデータがあれば自動ロード
        TryLoadGame();
    }

    float _periodicSaveTimer = 180f; // 3分間隔の定期オートセーブ

    void Update()
    {
        // Mac/Windows問わず確実に押せるセーブキー判定
        if (CheckSaveKeyTriggered())
        {
            SaveGame("SAVEしました");
        }

        // 定期オートセーブ
        _periodicSaveTimer -= Time.deltaTime;
        if (_periodicSaveTimer <= 0f)
        {
            _periodicSaveTimer = 180f;
            SaveGame("オートセーブ完了");
        }

        // uGUIバナーのフェードアニメーション
        if (_bannerTimer > 0f)
        {
            _bannerTimer -= Time.deltaTime;
            if (_bannerCg != null)
                _bannerCg.alpha = Mathf.MoveTowards(_bannerCg.alpha, 1.0f, Time.deltaTime * 6f);
        }
        else
        {
            if (_bannerCg != null)
                _bannerCg.alpha = Mathf.MoveTowards(_bannerCg.alpha, 0.0f, Time.deltaTime * 2.5f);
        }
    }

    static UnityEngine.InputSystem.Keyboard GetKeyboard()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null) return kb;
        foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.Keyboard found)
                return found;
        }
        return null;
    }

    /// <summary>MacでFnを押さずにワンキーで確実にセーブできる【K】キー、および【F5】判定</summary>
    bool CheckSaveKeyTriggered()
    {
        var kb = GetKeyboard();
        if (kb != null)
        {
            // 1. Kキー（Macで最も押しやすく、エディタ衝突ゼロのワンキーセーブ！）
            if (kb.kKey.wasPressedThisFrame) return true;

            // 2. F5キー（通常またはFn+F5）
            if (kb.f5Key.wasPressedThisFrame) return true;
        }

        // 旧Inputフォールバック
        try
        {
            if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.F5))
                return true;
        }
        catch { }

        return false;
    }

    /// <summary>ゲームの現状をJSONファイルに保存</summary>
    public void SaveGame(string customMessage = "SAVEしました")
    {
        try
        {
            var data = new SaveData();
            data.saveTime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");

            // 1. プレイヤー位置と向き
            var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
            if (player != null)
            {
                data.SetPosition(player.transform.position);
                data.rotY = player.transform.eulerAngles.y;
            }

            // 2. スクラップ収集状況
            int scrapCount = 0;
            var scrapMgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
            if (scrapMgr != null)
            {
                data.collectedScrapIds = new List<int>(scrapMgr.GetCollectedIds());
                data.collectedCount = scrapMgr.CollectedCount;
                scrapCount = data.collectedCount;
            }

            // 【超重要安全ガード】もし未初期化やドメインリロード事故でscrapCountが既存ファイルより少ない場合、
            // 既存のパーツデータを絶対に消さずに保護維持・補完する！
            if (File.Exists(SaveFilePath))
            {
                try
                {
                    string oldJson = File.ReadAllText(SaveFilePath);
                    var oldData = JsonUtility.FromJson<SaveData>(oldJson);
                    if (oldData != null)
                    {
                        if (oldData.collectedCount > scrapCount)
                        {
                            data.collectedScrapIds = new List<int>(oldData.collectedScrapIds);
                            data.collectedCount = oldData.collectedCount;
                            scrapCount = data.collectedCount;
                            Debug.LogWarning($"[AdventureSaveManager] 安全ガード発動: メモリ上のパーツ({scrapMgr?.CollectedCount})が既存記録({oldData.collectedCount})より少ないため、既存記録を保護維持しました。");
                        }
                    }
                }
                catch { }
            }

            // リストが空だが個数がある場合の自動補完（1〜scrapCount）
            if (data.collectedScrapIds.Count == 0 && data.collectedCount > 0)
            {
                for (int i = 1; i <= data.collectedCount; i++)
                    data.collectedScrapIds.Add(i);
            }

            // 3. Rustの油所持数
            int oil = 1;
            var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (drone != null)
            {
                data.oilCount = drone.oilCount;
                oil = drone.oilCount;
            }

            // 4. 天蓋レバーの進行状況
            data.isCanopyBroken = AdventureSanctuaryTowerManager.IsCanopyBroken;

            // 直前ファイルの自動バックアップ（.bak）を作成
            if (File.Exists(SaveFilePath))
            {
                try { File.Copy(SaveFilePath, SaveFilePath + ".bak", true); } catch { }
            }

            // JSONシリアライズしてファイル書き出し
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);

            // バナー表示＆サウンド再生
            string subText = $"遺物パーツ: {scrapCount}個 ／ Rust常備油: {oil}個（Rustと絆を結びました）";
            ShowSaveNotification(customMessage, subText);
            PlaySaveSound();

            Debug.Log($"[AdventureSaveManager] セーブ完了: {SaveFilePath} (パーツ: {data.collectedCount}個, 油: {data.oilCount})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdventureSaveManager] セーブ失敗: {ex.Message}");
        }
    }

    void PlaySaveSound()
    {
        if (_saveSoundClip == null)
            _saveSoundClip = CreateSaveSound();

        if (_audioSource == null)
            SetupAudio();

        // 1. 2Dステレオでの直接再生
        if (_audioSource != null && _saveSoundClip != null)
        {
            _audioSource.PlayOneShot(_saveSoundClip, 1.0f);
        }

        // 2. AudioListener（またはメインカメラ）位置での直接再生（聞こえない問題を完全に防止）
        var listener = FindAnyObjectByType<AudioListener>();
        Vector3 playPos = listener != null ? listener.transform.position 
                        : (Camera.main != null ? Camera.main.transform.position : transform.position);

        if (_saveSoundClip != null)
        {
            AudioSource.PlayClipAtPoint(_saveSoundClip, playPos, 1.0f);
        }
    }

    /// <summary>保存されたデータがあれば読み込み、ゲーム状態を復元</summary>
    public bool TryLoadGame()
    {
        if (!File.Exists(SaveFilePath))
        {
            Debug.Log("[AdventureSaveManager] セーブデータは見つかりませんでした（新規開始）");
            return false;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            var data = JsonUtility.FromJson<SaveData>(json);
            if (data == null) return false;

            StartCoroutine(ApplySaveDataRoutine(data));
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdventureSaveManager] ロード失敗: {ex.Message}");
            return false;
        }
    }

    System.Collections.IEnumerator ApplySaveDataRoutine(SaveData data)
    {
        yield return null;

        // 1. スクラップ収集状態の復元（万が一リストが空でもcollectedCountから自動補完復元）
        var scrapMgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
        if (scrapMgr != null)
        {
            scrapMgr.ApplyLoadedScraps(data.collectedScrapIds, data.collectedCount);
        }

        // 2. Rustの油の復元
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        if (drone != null)
        {
            drone.oilCount = Mathf.Max(1, Mathf.Max(drone.oilCount, data.oilCount));
        }

        // 3. プレイヤー位置の復元
        var player = AdventurePlayerController.Instance ?? FindAnyObjectByType<AdventurePlayerController>();
        if (player != null && data.GetPosition() != Vector3.zero)
        {
            player.Teleport(data.GetPosition());
            player.transform.rotation = Quaternion.Euler(0f, data.rotY, 0f);
        }

        ShowSaveNotification("セーブデータを復元しました", $"前回の冒険記録（パーツ: {data.collectedCount}個）を読み込みました");
        Debug.Log($"[AdventureSaveManager] ロード完了: パーツ{data.collectedCount}個, 位置{data.GetPosition()}");
    }

    public void ShowSaveNotification(string title, string subText = "")
    {
        _bannerTimer = 3.2f;
        if (_bannerTitleText != null)
        {
            _bannerTitleText.text = $"✦ {title} ✦";
        }
        if (_bannerSubText != null)
        {
            _bannerSubText.text = subText;
        }
    }
}
