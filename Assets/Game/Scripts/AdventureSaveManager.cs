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
    GUIStyle _saveNotificationStyle;
    Texture2D _gearIconTex;

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
            SaveGame("クイックセーブ完了");
        }

        // 定期オートセーブ
        _periodicSaveTimer -= Time.deltaTime;
        if (_periodicSaveTimer <= 0f)
        {
            _periodicSaveTimer = 180f;
            SaveGame("定期オートセーブ");
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
        _saveNotificationTimer = 2.8f;
    }

    void OnGUI()
    {
        if (_saveNotificationTimer <= 0f) return;

        float alpha = Mathf.Clamp01(_saveNotificationTimer / 0.5f);
        if (_saveNotificationTimer > 2.3f)
        {
            alpha = Mathf.Clamp01((2.8f - _saveNotificationTimer) / 0.5f);
        }

        if (_saveNotificationStyle == null)
        {
            _saveNotificationStyle = new GUIStyle(GUI.skin.box);
            _saveNotificationStyle.fontSize = 13;
            _saveNotificationStyle.fontStyle = FontStyle.Bold;
            _saveNotificationStyle.alignment = TextAnchor.MiddleCenter;
            _saveNotificationStyle.normal.textColor = new Color(1.0f, 0.92f, 0.75f, 1f); // 温かなゴールドホワイト

            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.10f, 0.12f, 0.16f, 0.88f)); // 半透明ダークスレート
            bgTex.Apply();
            _saveNotificationStyle.normal.background = bgTex;
        }

        Color prevColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);

        // 画面右上に通知ボックス
        float width = 240f;
        float height = 36f;
        float x = Screen.width - width - 24f;
        float y = 24f;

        GUI.Box(new Rect(x, y, width, height), $"✦ {_saveNotificationText}", _saveNotificationStyle);

        GUI.color = prevColor;
    }
}
