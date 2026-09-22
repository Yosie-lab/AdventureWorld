using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 池・小川の岸でニホンアマガエルが「ゲコゲコ」と鳴く。
/// 大合唱・カジカガエルとは別の個体声で、水辺に近づいたときだけ聞こえる。
/// </summary>
public class AdventureTreeFrogAmbience : MonoBehaviour
{
    static AdventureTreeFrogAmbience _instance;
    public static AdventureTreeFrogAmbience Instance => _instance;

    const int Rate = 22050;

    AudioClip[] _bouts;
    readonly List<Caller> _callers = new List<Caller>();
    readonly Queue<GameObject> _pool = new Queue<GameObject>();
    Transform _player;
    bool _muted;

    sealed class Caller
    {
        public Vector3 pos;
        public float waterY;
        public float hear;
        public float pitch;
        public int clip;
        public float timer;
        public float minGap;
        public float maxGap;
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
        _bouts = new[]
        {
            MakeBout(2480f, 10, 0.128f, 3),
            MakeBout(2050f, 8, 0.155f, 9),
            MakeBout(2920f, 12, 0.108f, 21),
        };
        BuildCallers();
    }

    void Update()
    {
        if (_muted) return;
        if (_player == null)
        {
            var player = FindFirstObjectByType<AdventurePlayerController>();
            if (player == null) return;
            _player = player.transform;
        }

        Vector3 p = _player.position;
        float dt = Time.deltaTime;
        for (int i = 0; i < _callers.Count; i++)
        {
            var c = _callers[i];
            float dx = p.x - c.pos.x;
            float dz = p.z - c.pos.z;
            if (dx * dx + dz * dz > c.hear * c.hear) continue;

            c.timer -= dt;
            if (c.timer > 0f) continue;
            c.timer = Random.Range(c.minGap, c.maxGap);
            PlayCall(c);
        }
    }

    public void MuteForEndingSequence()
    {
        if (_muted) return;
        _muted = true;
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
    }

    void PlayCall(Caller c)
    {
        if (_bouts == null || c.clip < 0 || c.clip >= _bouts.Length) return;
        var clip = _bouts[c.clip];
        if (clip == null) return;

        if (!c.placed)
        {
            var terrain = Terrain.activeTerrain;
            if (terrain != null)
            {
                float y = terrain.SampleHeight(c.pos) + terrain.transform.position.y;
                c.pos.y = Mathf.Max(y, c.waterY) + 0.35f;
            }
            c.placed = true;
        }

        var go = GetPooled();
        if (go == null) 
        {
            c.timer = 0.35f;
            return;
        }

        go.transform.position = c.pos;
        var audio = go.GetComponent<AudioSource>();
        audio.clip = clip;
        audio.pitch = c.pitch * Random.Range(0.985f, 1.015f);
        audio.volume = 0.62f * Random.Range(0.88f, 1.05f);
        audio.Play();
        StartCoroutine(RecycleAfter(go, clip.length / Mathf.Max(0.5f, audio.pitch) + 0.15f));
    }

    GameObject GetPooled()
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

        if (transform.childCount >= 8) return null;

        var go = new GameObject("TreeFrogCall3D");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 4.5f;
        src.maxDistance = 36f;
        src.dopplerLevel = 0f;
        src.playOnAwake = false;
        src.loop = false;
        return go;
    }

    System.Collections.IEnumerator RecycleAfter(GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (go == null) yield break;
        go.SetActive(false);
        if (!_muted) _pool.Enqueue(go);
    }

    void BuildCallers()
    {
        // オアシス湧水池、カルデラ湖、草原せせらぎ池、スタート地点の池
        AddRing(new Vector3(480f, 48.2f, 455f), 48.2f, 14.5f, 44f, 4, 0.4f);
        AddRing(new Vector3(420f, 25.5f, 440f), 25.5f, 36f, 48f, 7, 0.2f);
        AddRing(new Vector3(290f, 14.5f, 320f), 14.5f, 15f, 44f, 4, 1.1f);
        AddRing(new Vector3(135f, 18.15f, 166f), 18.15f, 18f, 42f, 4, 0.6f);

        Vector3[] stream =
        {
            new Vector3(465f, 40f, 450f),
            new Vector3(400f, 28f, 420f),
            new Vector3(340f, 19.5f, 365f),
            new Vector3(250f, 11.5f, 270f),
            new Vector3(180f, 6.5f, 200f),
        };
        for (int i = 0; i < stream.Length; i++)
        {
            Vector3 p = stream[i];
            float side = (i % 2 == 0) ? 6.5f : -6.5f;
            Vector3 tangent = Vector3.forward;
            if (i + 1 < stream.Length)
                tangent = (stream[i + 1] - stream[i]);
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.01f) tangent = Vector3.right;
            tangent.Normalize();
            Vector3 lateral = new Vector3(-tangent.z, 0f, tangent.x) * side;
            AddCaller(p + lateral, p.y, 34f);
        }
    }

    void AddRing(Vector3 center, float waterY, float radius, float hear, int count, float angle0)
    {
        for (int i = 0; i < count; i++)
        {
            float ang = angle0 + i * Mathf.PI * 2f / count;
            var p = center + new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius);
            p.y = waterY + 0.4f;
            AddCaller(p, waterY, hear);
        }
    }

    void AddCaller(Vector3 pos, float waterY, float hear)
    {
        int n = _callers.Count;
        float[] pitches = { 0.94f, 1.0f, 1.07f, 0.97f, 1.03f, 0.91f, 1.1f };
        var c = new Caller
        {
            pos = pos,
            waterY = waterY,
            hear = hear,
            pitch = pitches[n % pitches.Length],
            clip = n % 3,
            minGap = 2.4f + (n % 3) * 0.45f,
            maxGap = 5.6f + (n % 4) * 0.4f,
            timer = 0.35f + (n % 6) * 0.42f,
        };
        _callers.Add(c);
    }

    static AudioClip MakeBout(float f0, int notes, float gap, int seed)
    {
        float dur = 0.05f + notes * gap + 0.25f;
        int count = (int)(Rate * dur);
        var data = new float[count];
        var rng = new System.Random(seed);
        for (int k = 0; k < notes; k++)
        {
            float jitter = ((float)rng.NextDouble() - 0.5f) * 0.012f;
            int start = (int)(Rate * (0.04f + k * gap + jitter));
            float nf = f0 * (1f - 0.03f * k / Mathf.Max(1, notes - 1)) * (0.985f + (float)rng.NextDouble() * 0.03f);
            WriteNote(data, start, nf);
        }

        float peak = 0.0001f;
        for (int i = 0; i < count; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = 0.8f / peak;
        for (int i = 0; i < count; i++)
            data[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);

        var clip = AudioClip.Create("TreeFrogBout", count, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static void WriteNote(float[] data, int start, float freq)
    {
        int n = (int)(Rate * 0.078f);
        float phase = 0f;
        float x1a = 0f, x2a = 0f, y1a = 0f, y2a = 0f;
        float x1b = 0f, x2b = 0f, y1b = 0f, y2b = 0f;
        int pulseEvery = Mathf.Max(1, Rate / 145);
        for (int i = 0; i < n; i++)
        {
            int idx = start + i;
            if (idx < 0 || idx >= data.Length) continue;
            float t = (float)i / Rate;
            float u = (float)i / Mathf.Max(1, n - 1);
            float f = freq * (1f - 0.16f * u);
            phase += 2f * Mathf.PI * f / Rate;
            float gp = (i % pulseEvery) / (float)pulseEvery;
            float glottal = Mathf.Sin(Mathf.PI * gp);
            glottal *= glottal;
            float excite = (Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.18f) * glottal;
            float y = Biquad(ref x1a, ref x2a, ref y1a, ref y2a, excite, f, 6.5f);
            float f2 = Mathf.Min(f * 1.7f, Rate * 0.45f);
            y += 0.22f * Biquad(ref x1b, ref x2b, ref y1b, ref y2b, excite, f2, 9f);
            float att = Mathf.Clamp01(t / 0.004f);
            float env = att * Mathf.Exp(-t * 16.5f);
            data[idx] += y * env * 1.6f;
        }
    }

    static float Biquad(ref float x1, ref float x2, ref float y1, ref float y2, float x, float freq, float q)
    {
        float w0 = 2f * Mathf.PI * freq / Rate;
        float alpha = Mathf.Sin(w0) / (2f * q);
        float a0 = 1f + alpha;
        float b0 = alpha / a0;
        float b2 = -alpha / a0;
        float a1 = -2f * Mathf.Cos(w0) / a0;
        float a2 = (1f - alpha) / a0;
        float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
        x2 = x1;
        x1 = x;
        y2 = y1;
        y1 = y;
        return y;
    }
}
