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
    Queue<GameObject> _pool = new Queue<GameObject>();

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
        // プレイヤーの耳を自然に包み込む広域蝉時雨アンビエンス
        _bgAmbienceSourceA = gameObject.AddComponent<AudioSource>();
        _bgAmbienceSourceA.clip = _forestAmbienceClip;
        _bgAmbienceSourceA.loop = true;
        _bgAmbienceSourceA.spatialBlend = 0.0f;
        _bgAmbienceSourceA.volume = 0.26f;
        _bgAmbienceSourceA.playOnAwake = false;

        _bgAmbienceSourceB = gameObject.AddComponent<AudioSource>();
        _bgAmbienceSourceB.clip = _countryRoadClip ?? _forestAmbienceClip;
        _bgAmbienceSourceB.loop = true;
        _bgAmbienceSourceB.spatialBlend = 0.0f;
        _bgAmbienceSourceB.volume = 0.20f;
        _bgAmbienceSourceB.playOnAwake = false;

        if (_forestAmbienceClip != null)
        {
            _bgAmbienceSourceA.Play();
        }
        if (_countryRoadClip != null)
        {
            _bgAmbienceSourceB.time = 8.0f;
            _bgAmbienceSourceB.Play();
        }
    }

    void Update()
    {
        if (_playerTransform == null)
        {
            var player = FindObjectOfType<AdventurePlayerController>();
            if (player != null)
                _playerTransform = player.transform;
            else
                return;
        }

        // プレイヤー周囲の木立や梢から時折鳴り響く3D単独蝉（ヒグラシ、ミンミンゼミ、ツクツクボウシなど）
        _soloTimer -= Time.deltaTime;
        if (_soloTimer <= 0f)
        {
            _soloTimer = Random.Range(4.0f, 9.0f);
            SpawnRandomCicadaSound();
        }
    }

    void SpawnRandomCicadaSound()
    {
        if (_soloCicadaClips == null || _soloCicadaClips.Length == 0) return;
        var clip = _soloCicadaClips[Random.Range(0, _soloCicadaClips.Length)];
        if (clip == null) return;

        // プレイヤーの周囲12m〜32m、高さ3m〜12mのランダムな梢・草むら
        Vector3 p = _playerTransform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(14f, 32f);
        Vector3 spawnPos = p + new Vector3(Mathf.Cos(angle) * dist, Random.Range(3f, 10f), Mathf.Sin(angle) * dist);

        GameObject sndGo;
        if (_pool.Count > 0)
        {
            sndGo = _pool.Dequeue();
            sndGo.SetActive(true);
        }
        else
        {
            sndGo = new GameObject("CicadaOneShot3D");
            sndGo.transform.SetParent(transform);
            var src = sndGo.AddComponent<AudioSource>();
            src.spatialBlend = 1.0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 6f;
            src.maxDistance = 50f;
            src.dopplerLevel = 0f;
        }

        sndGo.transform.position = spawnPos;
        var audio = sndGo.GetComponent<AudioSource>();
        audio.clip = clip;
        audio.volume = Random.Range(0.40f, 0.65f);
        audio.pitch = Random.Range(0.95f, 1.05f);
        audio.Play();

        StartCoroutine(RecycleAfter(sndGo, clip.length + 0.5f));
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
