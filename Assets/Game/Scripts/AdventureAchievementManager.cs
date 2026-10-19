using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商用販売向け実績（Achievements）統括マネージャー
/// Steam実績 / プラットフォーム実績と100%互換のゲーム内実績トラッカー＆プレミアム通知トースト。
/// 各種アチーブメント解除時に画面上部に洗練された通知バナーを滑らかに表示し、PlayerPrefsに安全に永続化する。
/// </summary>
public class AdventureAchievementManager : MonoBehaviour
{
    private static AdventureAchievementManager _instance;
    public static AdventureAchievementManager Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInitialize()
    {
        if (_instance != null) return;
        var existing = UnityEngine.Object.FindAnyObjectByType<AdventureAchievementManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureAchievementManager");
        _instance = go.AddComponent<AdventureAchievementManager>();
        DontDestroyOnLoad(go);
    }

    [System.Serializable]
    public class Achievement
    {
        public string id;
        public string title;
        public string description;
        public string iconGlyph;
        public bool isUnlocked;
    }

    public static readonly Dictionary<string, Achievement> Achievements = new Dictionary<string, Achievement>
    {
        { "first_glide", new Achievement { id = "first_glide", title = "大空の旅立ち", description = "初めて翼を広げて空へ飛び立った", iconGlyph = "🪽" } },
        { "find_scrap", new Achievement { id = "find_scrap", title = "古代の記憶", description = "初めてスクラップパーツを回収した", iconGlyph = "⚙️" } },
        { "meet_capybara", new Achievement { id = "meet_capybara", title = "オアシスの温もり", description = "温泉カピバラと出会い、祝福を得た", iconGlyph = "🐾" } },
        { "thermal_ride", new Achievement { id = "thermal_ride", title = "風の翼", description = "サーマル上昇気流に乗って高空へ舞い上がった", iconGlyph = "🌪️" } },
        { "wind_highway", new Achievement { id = "wind_highway", title = "気流ハイウェイ", description = "大空のウィンドストリームラインに乗って高速巡航した", iconGlyph = "💨" } },
        { "scrap_10", new Achievement { id = "scrap_10", title = "熟練の収集家", description = "スクラップパーツを10個以上集めた", iconGlyph = "📦" } },
        { "tower_lever_unlock", new Achievement { id = "tower_lever_unlock", title = "サンクチュアリの覚醒", description = "総合20ポイントを達成し、白亜タワーの台座レバーを解放した", iconGlyph = "🏛️" } },
        { "sky_opened", new Achievement { id = "sky_opened", title = "大空の解放者", description = "オベリスク頂上のレバーを引き、天蓋を打ち破った！", iconGlyph = "✨" } },
        { "ledge_climb", new Achievement { id = "ledge_climb", title = "軽快なステップ", description = "崖縁のレッジグラブ（よじ登り）を成功させた", iconGlyph = "🧗" } },
        { "scrap_all", new Achievement { id = "scrap_all", title = "島の全知者", description = "全20個のスクラップパーツを完全コンプリートした！", iconGlyph = "👑" } },
    };

    // UI コンポーネント
    private Canvas _canvas;
    private CanvasGroup _toastGroup;
    private RectTransform _toastRect;
    private Text _toastTitleText;
    private Text _toastDescText;
    private Text _toastIconText;
    private AudioSource _audioSource;
    private AudioClip _achieveChimeClip;

    private readonly Queue<Achievement> _toastQueue = new Queue<Achievement>();
    private bool _isShowingToast = false;

    // Steamworks等の外部プラットフォーム連携用イベント
    public static event Action<string> OnAchievementUnlockedEvent;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        LoadAchievementStates();
        BuildToastUI();
        CreateAudioChime();
    }

    void LoadAchievementStates()
    {
        foreach (var kvp in Achievements)
        {
            kvp.Value.isUnlocked = PlayerPrefs.GetInt("RustAndFloat_Ach_" + kvp.Key, 0) == 1;
        }
    }

    /// <summary>
    /// 実績を解除する（未解除の場合のみトースト通知と保存を実行）
    /// </summary>
    public static void Unlock(string id)
    {
        if (Instance == null) AutoInitialize();
        Instance?.UnlockInternal(id);
    }

    private void UnlockInternal(string id)
    {
        if (!Achievements.TryGetValue(id, out var ach)) return;
        if (ach.isUnlocked) return;

        ach.isUnlocked = true;
        PlayerPrefs.SetInt("RustAndFloat_Ach_" + id, 1);
        PlayerPrefs.Save();

        // 外部連携コールバック（Steamworks.SteamUserStats.SetAchievement等と即連携可能）
        try
        {
            OnAchievementUnlockedEvent?.Invoke(id);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Achievement] Callback error: {ex.Message}");
        }

        // トースト通知キューに追加
        _toastQueue.Enqueue(ach);
        if (!_isShowingToast)
        {
            StartCoroutine(ProcessToastQueue());
        }
    }

    private IEnumerator ProcessToastQueue()
    {
        _isShowingToast = true;

        while (_toastQueue.Count > 0)
        {
            var ach = _toastQueue.Dequeue();
            yield return StartCoroutine(DisplayToastRoutine(ach));
        }

        _isShowingToast = false;
    }

    private IEnumerator DisplayToastRoutine(Achievement ach)
    {
        if (_toastTitleText != null) _toastTitleText.text = $"✦ 実績解除: {ach.title}";
        if (_toastDescText != null) _toastDescText.text = ach.description;
        if (_toastIconText != null) _toastIconText.text = ach.iconGlyph;

        if (_audioSource != null && _achieveChimeClip != null)
        {
            _audioSource.PlayOneShot(_achieveChimeClip, 0.75f);
        }

        // スライドイン＆フェードイン (0.35秒)
        float elapsed = 0f;
        const float inDuration = 0.35f;
        Vector2 startPos = new Vector2(0f, 60f);
        Vector2 targetPos = new Vector2(0f, -25f);

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / inDuration);
            if (_toastGroup != null) _toastGroup.alpha = t;
            if (_toastRect != null) _toastRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        if (_toastGroup != null) _toastGroup.alpha = 1f;
        if (_toastRect != null) _toastRect.anchoredPosition = targetPos;

        // 表示維持 (3.2秒)
        yield return new WaitForSecondsRealtime(3.2f);

        // フェードアウト (0.45秒)
        elapsed = 0f;
        const float outDuration = 0.45f;
        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);
            if (_toastGroup != null) _toastGroup.alpha = 1f - t;
            yield return null;
        }

        if (_toastGroup != null) _toastGroup.alpha = 0f;
        yield return new WaitForSecondsRealtime(0.2f);
    }

    private void BuildToastUI()
    {
        var canvasGo = new GameObject("AchievementToastCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999; // 最前面

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var toastGo = new GameObject("ToastPanel");
        toastGo.transform.SetParent(canvasGo.transform, false);
        _toastRect = toastGo.AddComponent<RectTransform>();
        _toastRect.anchorMin = new Vector2(0.5f, 1f);
        _toastRect.anchorMax = new Vector2(0.5f, 1f);
        _toastRect.pivot = new Vector2(0.5f, 1f);
        _toastRect.sizeDelta = new Vector2(480f, 74f);
        _toastRect.anchoredPosition = new Vector2(0f, 60f);

        _toastGroup = toastGo.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable = false;

        // 背景（上質な濃紺半透明＋ゴールド枠）
        var bgImage = toastGo.AddComponent<Image>();
        bgImage.color = new Color(0.06f, 0.12f, 0.18f, 0.94f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // アイコン枠
        var iconGo = new GameObject("Icon");
        iconGo.transform.SetParent(toastGo.transform, false);
        var iconRect = iconGo.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(36f, 0f);
        iconRect.sizeDelta = new Vector2(44f, 44f);

        _toastIconText = iconGo.AddComponent<Text>();
        _toastIconText.font = font;
        _toastIconText.fontSize = 26;
        _toastIconText.alignment = TextAnchor.MiddleCenter;
        _toastIconText.color = Color.white;

        // タイトル
        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(toastGo.transform, false);
        var titleRect = titleGo.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(68f, -12f);
        titleRect.sizeDelta = new Vector2(-78f, 24f);

        _toastTitleText = titleGo.AddComponent<Text>();
        _toastTitleText.font = font;
        _toastTitleText.fontSize = 17;
        _toastTitleText.fontStyle = FontStyle.Bold;
        _toastTitleText.color = new Color(1f, 0.85f, 0.35f, 1f);

        // 説明文
        var descGo = new GameObject("Desc");
        descGo.transform.SetParent(toastGo.transform, false);
        var descRect = descGo.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 0f);
        descRect.pivot = new Vector2(0f, 0f);
        descRect.anchoredPosition = new Vector2(68f, 12f);
        descRect.sizeDelta = new Vector2(-78f, 22f);

        _toastDescText = descGo.AddComponent<Text>();
        _toastDescText.font = font;
        _toastDescText.fontSize = 13;
        _toastDescText.color = new Color(0.85f, 0.92f, 0.98f, 0.90f);
    }

    private void CreateAudioChime()
    {
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f; // 2D音響
        _audioSource.ignoreListenerPause = true;

        // 澄んだFmaj7のプレミアム実績チャイム音（DSPプロシージャル合成）
        const int rate = 44100;
        const float duration = 1.2f;
        int count = (int)(rate * duration);
        float[] samples = new float[count];

        float[] freqs = { 698.46f, 880.0f, 1046.5f, 1318.5f }; // F5, A5, C6, E6
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float sum = 0f;
            for (int k = 0; k < freqs.Length; k++)
            {
                float noteDelay = k * 0.08f;
                if (t >= noteDelay)
                {
                    float noteT = t - noteDelay;
                    float env = Mathf.Exp(-noteT * 3.8f);
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[k] * noteT) * env * 0.22f;
                    sum += Mathf.Sin(2f * Mathf.PI * freqs[k] * 2f * noteT) * env * 0.05f;
                }
            }
            samples[i] = Mathf.Clamp(sum, -1f, 1f);
        }

        _achieveChimeClip = AudioClip.Create("AchievementChime", count, 1, rate, false);
        _achieveChimeClip.SetData(samples, 0);
    }
}
