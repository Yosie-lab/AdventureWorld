using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 渓流・池・大カルデラ湖周辺のカエル（夜のカエル合唱・ニホンアマガエル・カジカ）および渓流水音アンビエント音響。
/// </summary>
public class AdventureTreeFrogAmbience : MonoBehaviour
{
    static AdventureTreeFrogAmbience _instance;
    public static AdventureTreeFrogAmbience Instance => _instance;

    const float NightFrogExtraGain = 1.22f;

    AudioClip _nightFrogsClip;
    AudioClip _chorusClip;
    AudioClip _kajikaClip;
    AudioClip _streamClip;
    readonly List<Bed> _beds = new List<Bed>();
    Transform _player;
    bool _muted;

    sealed class Bed
    {
        public Vector3 pos;
        public float waterY;
        public float hear;
        public float baseVol;
        public AudioSource src;
        public bool placed;
    }

    public static void Ensure()
    {
        if (_instance != null) return;
        var existing = FindAnyObjectByType<AdventureTreeFrogAmbience>();
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
        if (_nightFrogsClip == null && _chorusClip == null && _kajikaClip == null) return;
        BuildBeds();
    }

    void LoadClips()
    {
#if UNITY_EDITOR
        _nightFrogsClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/dragon-studio-frogs-croaking-at-night-329855.mp3");
        _chorusClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/カエルの大合唱.mp3");
        _kajikaClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/01カジカガエル.3.aif");
        _streamClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/虫の声/渓流.mp3");
#endif
        if (_nightFrogsClip == null)
            _nightFrogsClip = FindClipByNameHint("frogs-croaking");
        if (_nightFrogsClip == null)
            _nightFrogsClip = FindClipByNameHint("dragon-studio");
        if (_chorusClip == null)
            _chorusClip = FindClipByNameHint("カエルの大合唱");
        if (_kajikaClip == null)
            _kajikaClip = FindClipByNameHint("カジカ");
        if (_streamClip == null)
            _streamClip = FindClipByNameHint("渓流");
    }

    static AudioClip FindClipByNameHint(string hint)
    {
        var sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include);
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
            var player = FindAnyObjectByType<AdventurePlayerController>();
            if (player == null) return;
            _player = player.transform;
        }

        Vector3 p = _player.position;
        float night = AdventureDayNightDirector.NightFactor;
        float seScale = PlayerPrefs.GetFloat("Adventure_SeVolume", 1f);

        for (int i = 0; i < _beds.Count; i++)
        {
            var bed = _beds[i];
            if (bed.src == null) continue;
            float dx = p.x - bed.pos.x;
            float dz = p.z - bed.pos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            bool near = dist < bed.hear;

            bool nightFrog = bed.src.clip == _nightFrogsClip;
            float nightMul = nightFrog
                ? Mathf.Lerp(0.08f, NightFrogExtraGain, night)
                : Mathf.Lerp(0.35f, 1.05f, night);
            float targetVol = 0f;
            if (near)
                targetVol = Mathf.Min(1f, bed.baseVol * nightMul * seScale);

            bed.src.volume = Mathf.MoveTowards(bed.src.volume, targetVol, 1.05f * Time.deltaTime);

            if (bed.src.volume <= 0.001f && !near)
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
        // ─────────────────────────────────────────────────────────────
        // 1. カルデラ湖周辺 (420, 25.5, 440) 半径38mの広大な湖
        // ─────────────────────────────────────────────────────────────
        // 夜のカエルたちの鳴き声（dragon-studio-frogs-croaking-at-night-329855）
        if (_nightFrogsClip != null)
        {
            AddBed(new Vector3(420f, 25.5f, 440f), 25.5f, 82f, 1.0f, _nightFrogsClip, 1.0f, "NightFrogs_CalderaCenter");
            AddBed(new Vector3(395f, 25.5f, 435f), 25.5f, 62f, 0.98f, _nightFrogsClip, 0.98f, "NightFrogs_CalderaWest");
            AddBed(new Vector3(445f, 25.5f, 445f), 25.5f, 62f, 0.98f, _nightFrogsClip, 1.02f, "NightFrogs_CalderaEast");
            AddBed(new Vector3(420f, 25.5f, 465f), 25.5f, 62f, 0.96f, _nightFrogsClip, 0.96f, "NightFrogs_CalderaNorth");
            AddBed(new Vector3(420f, 25.5f, 415f), 25.5f, 62f, 0.96f, _nightFrogsClip, 1.01f, "NightFrogs_CalderaSouth");
        }
        // カエルの大合唱との立体ブレンド
        if (_chorusClip != null)
        {
            AddBed(new Vector3(415f, 25.5f, 445f), 25.5f, 62f, 0.50f, _chorusClip, 1.0f, "FrogChorus_Caldera");
        }

        // ─────────────────────────────────────────────────────────────
        // 2. 池（オアシス湧水池・草原池・せせらぎ池）
        // ─────────────────────────────────────────────────────────────
        // オアシス湧水池 (480, 48.2, 455)
        if (_nightFrogsClip != null)
            AddBed(new Vector3(480f, 48.2f, 455f), 48.2f, 60f, 0.98f, _nightFrogsClip, 1.02f, "NightFrogs_OasisPond");
        if (_kajikaClip != null)
            AddBed(new Vector3(480f, 48.2f, 455f), 48.2f, 45f, 0.45f, _kajikaClip, 1.0f, "Kajika_OasisPond");

        // 大草原池 (290, 14.5, 320)
        if (_nightFrogsClip != null)
            AddBed(new Vector3(290f, 14.5f, 320f), 14.5f, 66f, 1.0f, _nightFrogsClip, 0.97f, "NightFrogs_MeadowPond");
        if (_chorusClip != null)
            AddBed(new Vector3(290f, 14.5f, 320f), 14.5f, 48f, 0.42f, _chorusClip, 0.96f, "FrogChorus_MeadowPond");

        // 西側せせらぎ池 (135, 18.15, 166)
        if (_nightFrogsClip != null)
            AddBed(new Vector3(135f, 18.15f, 166f), 18.15f, 56f, 0.96f, _nightFrogsClip, 1.03f, "NightFrogs_WestPond");
        if (_kajikaClip != null)
            AddBed(new Vector3(135f, 18.15f, 166f), 18.15f, 38f, 0.38f, _kajikaClip, 0.98f, "Kajika_WestPond");

        // ─────────────────────────────────────────────────────────────
        // 3. 渓流（上流急流・東の深林大渓流・本流大河）
        // ─────────────────────────────────────────────────────────────
        // オアシス〜カルデラ湖の上流急流岩場
        if (_nightFrogsClip != null)
            AddBed(new Vector3(460f, 38.0f, 448f), 38.0f, 58f, 0.96f, _nightFrogsClip, 1.0f, "NightFrogs_UpperRapids");
        if (_kajikaClip != null)
            AddBed(new Vector3(460f, 38.0f, 448f), 38.0f, 40f, 0.42f, _kajikaClip, 1.04f, "Kajika_UpperRapids");

        // カルデラ湖流入部
        if (_nightFrogsClip != null)
            AddBed(new Vector3(440f, 28.0f, 445f), 28.0f, 56f, 0.95f, _nightFrogsClip, 0.99f, "NightFrogs_LakeInlet");

        // 東の深林大渓流（高地森林〜カルデラ湖）
        if (_nightFrogsClip != null)
        {
            AddBed(new Vector3(470f, 36.0f, 490f), 36.0f, 60f, 0.96f, _nightFrogsClip, 0.96f, "NightFrogs_EastForestStream1");
            AddBed(new Vector3(450f, 30.0f, 465f), 30.0f, 60f, 0.95f, _nightFrogsClip, 1.01f, "NightFrogs_EastForestStream2");
        }

        // 本流大河（カルデラ湖〜草原池〜海）
        if (_nightFrogsClip != null)
        {
            AddBed(new Vector3(350f, 19.5f, 365f), 19.5f, 58f, 0.96f, _nightFrogsClip, 1.0f, "NightFrogs_MainStreamStepping");
            AddBed(new Vector3(250f, 11.5f, 270f), 11.5f, 56f, 0.94f, _nightFrogsClip, 0.98f, "NightFrogs_MainStreamMid");
            AddBed(new Vector3(190f, 8.5f, 215f), 8.5f, 54f, 0.92f, _nightFrogsClip, 1.02f, "NightFrogs_MainStreamLower");
        }
        if (_kajikaClip != null)
        {
            AddBed(new Vector3(340f, 19.5f, 365f), 19.5f, 36f, 0.38f, _kajikaClip, 1.02f, "Kajika_MainStreamStepping");
            AddBed(new Vector3(230f, 10.0f, 245f), 10.0f, 34f, 0.34f, _kajikaClip, 0.94f, "Kajika_MainStreamMid");
        }

        // 渓流の水音（せせらぎ）
        if (_streamClip != null)
        {
            AddBed(new Vector3(460f, 38.0f, 448f), 38.0f, 40f, 0.40f, _streamClip, 1.0f, "StreamAudio_UpperRapids");
            AddBed(new Vector3(460f, 33.0f, 480f), 33.0f, 40f, 0.38f, _streamClip, 0.98f, "StreamAudio_EastStream");
            AddBed(new Vector3(350f, 19.5f, 365f), 19.5f, 38f, 0.36f, _streamClip, 1.02f, "StreamAudio_MainStream");
        }
    }

    void AddBed(Vector3 pos, float waterY, float hear, float volume, AudioClip clip, float pitch, string bedName = null)
    {
        if (clip == null) return;
        pos.y = waterY + 0.4f;
        var go = new GameObject(string.IsNullOrEmpty(bedName) ? "FrogBed3D_" + clip.name : bedName);
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.spatialBlend = 0.80f; // 包み込まれるようにしっかり聴こえる3Dブレンド
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 18f;    // 水辺付近で聴こえやすい減衰
        src.maxDistance = hear;
        src.dopplerLevel = 0f;
        src.playOnAwake = false;
        src.volume = 0f; // スムーズフェードイン用初期値
        src.pitch = pitch;
        src.spread = 90f;
        _beds.Add(new Bed { pos = pos, waterY = waterY, hear = hear, baseVol = volume, src = src });
    }
}
