using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// プレイヤーの周囲どこでも（スタート地点・大草原・海岸・木立）
/// リアルな夏の蝉時雨や虫の声が心地よく響き渡るアンビエント音響システム
/// </summary>
public class AdventureCicadaAmbienceManager : MonoBehaviour
{
    static AdventureCicadaAmbienceManager _instance;
    public static AdventureCicadaAmbienceManager Instance => _instance;

    AudioSource _bgAmbienceSourceA;
    AudioSource _bgAmbienceSourceB;
    Transform _playerTransform;

    AudioClip _forestAmbienceClip;
    AudioClip _countryRoadClip;
    AudioClip[] _soloCicadaClips;

    float _soloTimer = 2.0f;
    readonly Queue<GameObject> _pool = new Queue<GameObject>();
    bool _mutedForEndingSequence;
    float _targetBgA;
    float _targetBgB;
    float _worldCicadaDampTimer;

    [Header("Volume & Frequency")]
    [Range(0f, 1f)] public float bgVolume = 0.24f;
    [Range(0f, 1f)] public float soloVolume = 0.50f;
    public float minSoloInterval = 4.0f;
    public float maxSoloInterval = 9.0f;

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindObjectOfType<AdventureCicadaAmbienceManager>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("ParadiseCicadaAmbienceManager");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureCicadaAmbienceManager>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        LoadAudioClips();
        SetupBgSources();
    }

    void LoadAudioClips()
    {
#if UNITY_EDITOR
        _forestAmbienceClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/ミンミンゼミが鳴く雑木林.mp3");
        _countryRoadClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/夏の田舎道.mp3");

        var list = new List<AudioClip>();
        string[] soloPaths = {
            "Assets/虫の声/ヒグラシの鳴き声.mp3",
            "Assets/虫の声/ミンミンゼミの鳴き声.mp3",
            "Assets/虫の声/アブラゼミの鳴き声1.mp3",
            "Assets/虫の声/アブラゼミの鳴き声2.mp3",
            "Assets/虫の声/ツクツクボウシの鳴き声1.mp3",
            "Assets/虫の声/ツクツクボウシの鳴き声2.mp3",
            "Assets/虫の声/ニイニイゼミの鳴き声.mp3",
            "Assets/虫の声/エンマコオロギの鳴き声.mp3"
        };
        foreach (var p in soloPaths)
        {
            var c = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (c != null) list.Add(c);
        }
        _soloCicadaClips = list.ToArray();
#endif
    }

    void SetupBgSources()
    {
        // プレイヤーの耳を自然に包み込む広域蝉時雨アンビエンス（2系統）
        _bgAmbienceSourceA = CreateBgSource(_forestAmbienceClip, bgVolume);
        _bgAmbienceSourceB = CreateBgSource(_countryRoadClip ?? _forestAmbienceClip, bgVolume * 0.75f);

        if (_forestAmbienceClip != null)
            _bgAmbienceSourceA.Play();

        if (_countryRoadClip != null)
        {
            _bgAmbienceSourceB.time = 8.0f;
            _bgAmbienceSourceB.Play();
        }
    }

    AudioSource CreateBgSource(AudioClip clip, float vol)
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0.0f;
        src.volume = vol;
        src.playOnAwake = false;
        return src;
    }

    void Update()
    {
        if (_mutedForEndingSequence) return;

        if (_playerTransform == null)
        {
            var player = FindObjectOfType<AdventurePlayerController>();
            if (player != null)
                _playerTransform = player.transform;
            else
                return;
        }

        var pc = AdventurePlayerController.Instance;
        Vector3 p = _playerTransform.position;
        bool onBeach = pc != null && pc.IsInBeachOrCoastZone(p);
        bool inForest = !onBeach && IsForestZone(p);
        bool inMeadow = !onBeach && !inForest;

        // 砂浜は波・ウミネコのみ。草原は田舎道／虫、森は蝉時雨。
        if (onBeach)
        {
            _targetBgA = 0f;
            _targetBgB = 0f;
        }
        else if (inForest)
        {
            _targetBgA = bgVolume;
            _targetBgB = bgVolume * 0.22f;
        }
        else if (inMeadow)
        {
            _targetBgA = bgVolume * 0.08f;
            _targetBgB = bgVolume * 0.7f;
        }
        else
        {
            _targetBgA = bgVolume * 0.35f;
            _targetBgB = bgVolume * 0.4f;
        }

        float dt = Time.deltaTime;
        if (_bgAmbienceSourceA != null)
            _bgAmbienceSourceA.volume = Mathf.MoveTowards(_bgAmbienceSourceA.volume, _targetBgA, dt * 0.35f);
        if (_bgAmbienceSourceB != null)
            _bgAmbienceSourceB.volume = Mathf.MoveTowards(_bgAmbienceSourceB.volume, _targetBgB, dt * 0.35f);

        if (onBeach)
        {
            _worldCicadaDampTimer -= dt;
            if (_worldCicadaDampTimer <= 0f)
            {
                _worldCicadaDampTimer = 0.6f;
                SoftenWorldCicadasNearPlayer(p, 0.04f);
            }
            return;
        }

        _soloTimer -= dt;
        if (_soloTimer <= 0f)
        {
            _soloTimer = Random.Range(minSoloInterval, maxSoloInterval);
            SpawnRandomCicadaSound(preferCricket: inMeadow);
        }
    }

    static void SoftenWorldCicadasNearPlayer(Vector3 playerPos, float volume)
    {
        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            var src = sources[i];
            if (src == null || !src.isPlaying) continue;
            if (!IsCicadaOrInsectSource(src)) continue;
            if ((src.transform.position - playerPos).sqrMagnitude > 90f * 90f) continue;
            src.volume = Mathf.Min(src.volume, volume);
        }
    }

    static bool IsForestZone(Vector3 pos)
    {
        // 東部〜北部の大樹海帯（Dress配置と概ね一致）
        if (pos.x >= 460f && pos.z >= 180f && pos.z <= 780f) return true;
        if (pos.x >= 420f && pos.z >= 500f) return true;
        return false;
    }

    /// <summary>天蓋開放〜エンディング中は蝉・虫の声を完全に止める</summary>
    public void MuteForEndingSequence()
    {
        if (_mutedForEndingSequence) return;
        _mutedForEndingSequence = true;

        if (_bgAmbienceSourceA != null)
        {
            _bgAmbienceSourceA.Stop();
            _bgAmbienceSourceA.volume = 0f;
            _bgAmbienceSourceA.mute = true;
        }
        if (_bgAmbienceSourceB != null)
        {
            _bgAmbienceSourceB.Stop();
            _bgAmbienceSourceB.volume = 0f;
            _bgAmbienceSourceB.mute = true;
        }

        // 再生中の3Dワンショット蝉を停止
        _pool.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (child == null) continue;
            var src = child.GetComponent<AudioSource>();
            if (src != null) src.Stop();
            child.gameObject.SetActive(false);
            _pool.Enqueue(child.gameObject);
        }

        MuteWorldCicadaSources();
    }

    static void MuteWorldCicadaSources()
    {
        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            var src = sources[i];
            if (src == null) continue;
            if (!IsCicadaOrInsectSource(src)) continue;
            src.Stop();
            src.mute = true;
            src.volume = 0f;
        }
    }

    static bool IsCicadaOrInsectSource(AudioSource src)
    {
        string n = src.gameObject.name;
        if (n.IndexOf("Cicada", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (n.IndexOf("Meadow", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (n.Contains("虫") || n.Contains("蝉") || n.Contains("セミ")) return true;

        // 親階層（森配置の Cicadas / MeadowInsects）
        Transform t = src.transform.parent;
        while (t != null)
        {
            string pn = t.name;
            if (pn.IndexOf("Cicada", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (pn.IndexOf("MeadowInsect", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            t = t.parent;
        }

        if (src.clip == null) return false;
        string c = src.clip.name;
        return c.Contains("セミ") || c.Contains("蝉") || c.Contains("コオロギ")
            || c.Contains("ヒグラシ") || c.Contains("雑木林") || c.Contains("田舎道")
            || c.IndexOf("Cicada", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void SpawnRandomCicadaSound(bool preferCricket)
    {
        if (_soloCicadaClips == null || _soloCicadaClips.Length == 0) return;
        AudioClip clip = null;
        if (preferCricket)
        {
            for (int i = 0; i < _soloCicadaClips.Length; i++)
            {
                var c = _soloCicadaClips[i];
                if (c != null && c.name.Contains("コオロギ"))
                {
                    clip = c;
                    break;
                }
            }
        }
        if (clip == null)
            clip = _soloCicadaClips[Random.Range(0, _soloCicadaClips.Length)];
        if (clip == null) return;

        Vector3 p = _playerTransform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(14f, 32f);
        float height = preferCricket ? Random.Range(0.4f, 2.2f) : Random.Range(3f, 10f);
        Vector3 spawnPos = p + new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);

        GameObject sndGo = GetPooledObject();
        sndGo.transform.position = spawnPos;

        var audio = sndGo.GetComponent<AudioSource>();
        audio.clip = clip;
        audio.volume = soloVolume * Random.Range(0.85f, 1.15f) * (preferCricket ? 0.75f : 1f);
        audio.pitch = Random.Range(0.95f, 1.05f);
        audio.Play();

        StartCoroutine(RecycleAfter(sndGo, clip.length + 0.5f));
    }

    GameObject GetPooledObject()
    {
        while (_pool.Count > 0)
        {
            var candidate = _pool.Dequeue();
            if (candidate != null)
            {
                candidate.SetActive(true);
                return candidate;
            }
        }

        var sndGo = new GameObject("CicadaOneShot3D");
        sndGo.transform.SetParent(transform);
        var src = sndGo.AddComponent<AudioSource>();
        src.spatialBlend = 1.0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 6f;
        src.maxDistance = 50f;
        src.dopplerLevel = 0f;
        return sndGo;
    }

    System.Collections.IEnumerator RecycleAfter(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go != null)
        {
            go.SetActive(false);
            _pool.Enqueue(go);
        }
    }
}
