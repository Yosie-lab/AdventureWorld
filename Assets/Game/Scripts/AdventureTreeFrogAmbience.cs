using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 池と小川でニホンアマガエル／カエルの合唱。
/// 録音（大合唱・カジカ）のみ使い、合成音は使わない。
/// </summary>
public class AdventureTreeFrogAmbience : MonoBehaviour
{
    static AdventureTreeFrogAmbience _instance;
    public static AdventureTreeFrogAmbience Instance => _instance;

    AudioClip _chorusClip;
    AudioClip _kajikaClip;
    readonly List<Bed> _beds = new List<Bed>();
    Transform _player;
    bool _muted;

    sealed class Bed
    {
        public Vector3 pos;
        public float waterY;
        public float hear;
        public AudioSource src;
        public bool placed;
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindFirstObjectByType<AdventureTreeFrogAmbience>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("ParadiseTreeFrogAmbience");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AdventureTreeFrogAmbience>();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        LoadClips();
        if (_chorusClip == null && _kajikaClip == null) return;
        BuildBeds();
    }

    void LoadClips()
    {
#if UNITY_EDITOR
        _chorusClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/カエルの大合唱.mp3");
        _kajikaClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/01カジカガエル.3.aif");
#endif
        if (_chorusClip == null)
            _chorusClip = FindClipByNameHint("カエルの大合唱");
        if (_kajikaClip == null)
            _kajikaClip = FindClipByNameHint("カジカ");
    }

    static AudioClip FindClipByNameHint(string hint)
    {
        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sources.Length; i++)
        {
            var c = sources[i] != null ? sources[i].clip : null;
            if (c != null && c.name.Contains(hint))
                return c;
        }
        return null;
    }

    void Update()
    {
        if (_muted || _beds.Count == 0) return;
        if (_player == null)
        {
            var player = FindFirstObjectByType<AdventurePlayerController>();
            if (player == null) return;
            _player = player.transform;
        }

        Vector3 p = _player.position;
        for (int i = 0; i < _beds.Count; i++)
        {
            var bed = _beds[i];
            if (bed.src == null) continue;
            float dx = p.x - bed.pos.x;
            float dz = p.z - bed.pos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            bool near = dist < bed.hear;
            if (!near)
            {
                if (bed.src.isPlaying) bed.src.Pause();
                continue;
            }

            if (!bed.placed)
            {
                var terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    float y = terrain.SampleHeight(bed.pos) + terrain.transform.position.y;
                    bed.pos.y = Mathf.Max(y, bed.waterY) + 0.4f;
                    bed.src.transform.position = bed.pos;
                }
                bed.placed = true;
            }

            if (!bed.src.isPlaying) bed.src.Play();
        }
    }

    public void MuteForEndingSequence()
    {
        if (_muted) return;
        _muted = true;
        for (int i = 0; i < _beds.Count; i++)
        {
            var src = _beds[i].src;
            if (src == null) continue;
            src.Stop();
            src.mute = true;
        }
    }

    void BuildBeds()
    {
        // 大合唱：広い池。カジカ：湧水・小川
        if (_chorusClip != null)
        {
            AddBed(new Vector3(420f, 25.5f, 440f), 25.5f, 58f, 0.55f, _chorusClip, 1.0f);
            AddBed(new Vector3(290f, 14.5f, 320f), 14.5f, 44f, 0.42f, _chorusClip, 0.96f);
        }
        if (_kajikaClip != null)
        {
            AddBed(new Vector3(480f, 48.2f, 455f), 48.2f, 40f, 0.48f, _kajikaClip, 1.0f);
            AddBed(new Vector3(400f, 28f, 420f), 28f, 34f, 0.38f, _kajikaClip, 1.04f);
            AddBed(new Vector3(250f, 11.5f, 270f), 11.5f, 32f, 0.34f, _kajikaClip, 0.92f);
            AddBed(new Vector3(135f, 18.15f, 166f), 18.15f, 36f, 0.36f, _kajikaClip, 0.98f);
        }
    }

    void AddBed(Vector3 pos, float waterY, float hear, float volume, AudioClip clip, float pitch)
    {
        if (clip == null) return;
        pos.y = waterY + 0.4f;
        var go = new GameObject(clip == _chorusClip ? "FrogChorusBed3D" : "KajikaFrogBed3D");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 10f;
        src.maxDistance = hear;
        src.dopplerLevel = 0f;
        src.playOnAwake = false;
        src.volume = volume;
        src.pitch = pitch;
        src.spread = 80f;
        _beds.Add(new Bed { pos = pos, waterY = waterY, hear = hear, src = src });
    }
}
