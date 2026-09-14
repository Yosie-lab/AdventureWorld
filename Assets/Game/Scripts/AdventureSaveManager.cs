using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

/// <summary>
/// 『Rust & Float』セーブ＆ロード管理システム
/// パーツ収集状況、プレイヤー位置、能力アンロック、Rustの状態を自動/手動で安全に保存・復元
/// </summary>
public class AdventureSaveManager : MonoBehaviour
{
    public static AdventureSaveManager Instance { get; private set; }

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

    float _saveNotificationTimer = 0f;
    string _saveNotificationText = "";
    static AudioClip _saveSoundClip;
    AudioSource _audioSource;
    Texture2D _bgTex;

    public static void Ensure()
    {
        if (Instance != null) return;
        var existing = FindAnyObjectByType<AdventureSaveManager>();
        if (existing != null)
        {
            Instance = existing;
            return;
        }

        var go = new GameObject("AdventureSaveManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<AdventureSaveManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        SetupAudio();
    }

    void SetupAudio()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2Dステレオ
        if (_saveSoundClip == null)
        {
            _saveSoundClip = CreateSaveSound();
        }
    }

    static AudioClip CreateSaveSound()
    {
        int sampleRate = 44100;
        float duration = 0.32f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            // 2音の心地よいクリスタルチャイム（前半: 587Hz=D5、後半: 880Hz=A5）
            float freq = t < 0.12f ? 587.33f : 880.0f;
            float env = Mathf.Exp(-t * 9f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.4f;
        }
        var clip = AudioClip.Create("SaveChime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void Start()
    {
        // 起動時に既存セーブデータがあれば自動ロード
        TryLoadGame();
    }

    float _periodicSaveTimer = 180f; // 3分間隔の定期オートセーブ

    void Update()
    {
        // F5キーで手動クイックセーブ
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && kb.f5Key.wasPressedThisFrame)
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

        if (_saveNotificationTimer > 0f)
        {
            _saveNotificationTimer -= Time.deltaTime;
        }
    }

    /// <summary>ゲームの現状をJSONファイルに保存</summary>
    public void SaveGame(string customMessage = "セーブしました")
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
            var scrapMgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
            if (scrapMgr != null)
            {
                data.collectedScrapIds = new List<int>(scrapMgr.GetCollectedIds());
                data.collectedCount = scrapMgr.CollectedCount;
            }

            // 3. Rustの油所持数
            var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
            if (drone != null)
            {
                data.oilCount = drone.oilCount;
            }

            // 4. 天蓋レバーの進行状況
            data.isCanopyBroken = AdventureSanctuaryTowerManager.IsCanopyBroken;

            // JSONシリアライズしてファイル書き出し
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SaveFilePath, json);

            ShowSaveNotification(customMessage);
            if (_audioSource != null && _saveSoundClip != null)
            {
                _audioSource.PlayOneShot(_saveSoundClip, 0.7f);
            }
            Debug.Log($"[AdventureSaveManager] セーブ完了: {SaveFilePath} (パーツ: {data.collectedCount}個, 油: {data.oilCount})");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AdventureSaveManager] セーブ失敗: {ex.Message}");
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
        // シーン初期化やマネージャーの起動を1フレーム待機
        yield return null;

        // 1. スクラップ収集状態の復元
        var scrapMgr = AdventureScrapManager.Instance ?? FindAnyObjectByType<AdventureScrapManager>();
        if (scrapMgr != null)
        {
            scrapMgr.ApplyLoadedScraps(data.collectedScrapIds);
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

        ShowSaveNotification("セーブデータを復元しました");
        Debug.Log($"[AdventureSaveManager] ロード完了: パーツ{data.collectedCount}個, 位置{data.GetPosition()}");
    }

    public void ShowSaveNotification(string text)
    {
        _saveNotificationText = text;
        _saveNotificationTimer = 3.0f;
    }

    void OnGUI()
    {
        if (_saveNotificationTimer <= 0f) return;

        float alpha = 1f;
        if (_saveNotificationTimer > 2.6f)
        {
            alpha = Mathf.Clamp01((3.0f - _saveNotificationTimer) / 0.4f);
        }
        else if (_saveNotificationTimer < 0.6f)
        {
            alpha = Mathf.Clamp01(_saveNotificationTimer / 0.6f);
        }

        // 画面上部中央（左上のクエスト目標や右上のRustバッジと被らず視界に飛び込む特等席）
        int fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.024f, 18f, 26f));
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = fontSize;
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.alignment = TextAnchor.MiddleCenter;

        string displayMsg = $"✦ {_saveNotificationText} ✦";
        Vector2 textSz = labelStyle.CalcSize(new GUIContent(displayMsg));

        float width = Mathf.Max(340f, textSz.x + 64f);
        float height = Mathf.Max(50f, fontSize + 24f);
        float x = (Screen.width - width) * 0.5f;
        float y = Mathf.Clamp(Screen.height * 0.11f, 65f, 100f);

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        // 半透明ダーク背景
        if (_bgTex == null)
        {
            _bgTex = new Texture2D(1, 1);
            _bgTex.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.12f, 0.94f));
            _bgTex.Apply();
        }
        Rect boxRect = new Rect(x, y, width, height);
        GUI.DrawTexture(boxRect, _bgTex);

        // エメラルドグリーンの光彩アクセントライン（上部と下部）
        Color accentCol = new Color(0.35f, 0.98f, 0.65f, alpha);
        Rect topLine = new Rect(x, y, width, 3f);
        GUI.DrawTexture(topLine, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, accentCol, 0, 0);

        // 黒アウトライン付きテキスト
        DrawOutlinedText(boxRect, displayMsg, labelStyle, accentCol, new Color(0f, 0f, 0f, 0.95f * alpha));

        GUI.color = prevColor;
    }

    void DrawOutlinedText(Rect r, string text, GUIStyle style, Color frontColor, Color outlineColor)
    {
        Color prev = style.normal.textColor;
        style.normal.textColor = outlineColor;
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oy = -2; oy <= 2; oy++)
            {
                if (ox == 0 && oy == 0) continue;
                Rect offsetRect = new Rect(r.x + ox, r.y + oy, r.width, r.height);
                GUI.Label(offsetRect, text, style);
            }
        }
        style.normal.textColor = frontColor;
        GUI.Label(r, text, style);
        style.normal.textColor = prev;
    }
}
