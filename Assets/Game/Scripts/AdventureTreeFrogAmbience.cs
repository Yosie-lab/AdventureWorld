using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 池と小川でニホンアマガエルの合唱。
/// 1声は短い「ゲッ」で、何匹かがずれて重なる。大合唱・カジカガエルとは別。
/// </summary>
public class AdventureTreeFrogAmbience : MonoBehaviour
{
    static AdventureTreeFrogAmbience _instance;
    public static AdventureTreeFrogAmbience Instance => _instance;

    const int Rate = 22050;
    const float ChorusSeconds = 8f;

    AudioClip _chorus;
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
        _chorus = MakeChorus(11);
        BuildBeds();
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
        for (int i = 0; i < _beds.Count; i++)
        {
            var bed = _beds[i];
            float dx = p.x - bed.pos.x;
            float dz = p.z - bed.pos.z;
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            bool near = dist < bed.hear;
            if (bed.src == null) continue;
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
        AddBed(new Vector3(480f, 48.2f, 455f), 48.2f, 46f, 1.0f);
        AddBed(new Vector3(420f, 25.5f, 440f), 25.5f, 58f, 0.94f);
        AddBed(new Vector3(290f, 14.5f, 320f), 14.5f, 46f, 1.06f);
        AddBed(new Vector3(135f, 18.15f, 166f), 18.15f, 44f, 0.97f);

        Vector3[] stream =
        {
            new Vector3(400f, 28f, 420f),
            new Vector3(250f, 11.5f, 270f),
        };
        float[] streamPitch = { 1.03f, 0.91f };
        for (int i = 0; i < stream.Length; i++)
            AddBed(stream[i], stream[i].y, 36f, streamPitch[i]);
    }

    void AddBed(Vector3 pos, float waterY, float hear, float pitch)
    {
        pos.y = waterY + 0.4f;
        var go = new GameObject("TreeFrogChorus3D");
        go.transform.SetParent(transform, false);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = _chorus;
        src.loop = true;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 12f;
        src.maxDistance = hear;
        src.dopplerLevel = 0f;
        src.playOnAwake = false;
        src.volume = 0.72f;
        src.pitch = pitch;
        src.spread = 70f;
        _beds.Add(new Bed { pos = pos, waterY = waterY, hear = hear, src = src });
    }

    static AudioClip MakeChorus(int seed)
    {
        int extra = (int)(Rate * 0.06f);
        int count = (int)(Rate * ChorusSeconds);
        var data = new float[count + extra];
        float[] voices = { 2380f, 2620f, 2140f, 2860f, 2480f, 3080f, 2260f };
        for (int v = 0; v < voices.Length; v++)
            AddVoice(data, voices[v], seed + v * 17);

        int fade = extra;
        for (int i = 0; i < fade; i++)
        {
            float w = (float)i / fade;
            data[i] = data[i] * w + data[count + i] * (1f - w);
        }

        float peak = 0.0001f;
        for (int i = 0; i < count; i++)
            peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        float gain = 0.72f / peak;
        var clipData = new float[count];
        for (int i = 0; i < count; i++)
            clipData[i] = Mathf.Clamp(data[i] * gain, -1f, 1f);

        var clip = AudioClip.Create("TreeFrogChorus", count, 1, Rate, false);
        clip.SetData(clipData, 0);
        return clip;
    }

    static void AddVoice(float[] data, float freq, int seed)
    {
        var rng = new System.Random(seed);
        float t = (float)rng.NextDouble() * 0.8f;
        while (t < ChorusSeconds)
        {
            int notes = 7 + rng.Next(6);
            for (int k = 0; k < notes && t < ChorusSeconds; k++)
            {
                float f = freq * (0.97f + (float)rng.NextDouble() * 0.06f);
                float amp = 0.62f + (float)rng.NextDouble() * 0.38f;
                AddGek(data, (int)(Rate * t), f, amp, rng);
                t += 0.125f + (float)rng.NextDouble() * 0.045f;
            }
            t += 0.22f + (float)rng.NextDouble() * 0.38f;
        }
    }

    static void AddGek(float[] data, int start, float freq, float amp, System.Random rng)
    {
        int pulses = 4 + rng.Next(3);
        var times = new int[pulses];
        float cursor = 0.003f;
        float gap = 0.0072f + (float)rng.NextDouble() * 0.0028f;
        for (int p = 0; p < pulses; p++)
        {
            times[p] = (int)(Rate * cursor);
            cursor += gap * (0.84f + (float)rng.NextDouble() * 0.32f);
        }
        int len = (int)(Rate * (cursor + 0.028f));
        Ring(data, start, len, times, freq, 16f, amp);
        Ring(data, start, len, times, freq * 1.28f, 10f, amp * 0.18f);
    }

    static void Ring(float[] data, int start, int len, int[] times, float freq, float q, float amp)
    {
        float w = 2f * Mathf.PI * freq / Rate;
        float decay = Mathf.Exp(-Mathf.PI * (freq / q) / Rate);
        float a1 = 2f * decay * Mathf.Cos(w);
        float a2 = -(decay * decay);
        float y1 = 0f;
        float y2 = 0f;
        int pulse = 0;
        for (int i = 0; i < len; i++)
        {
            int idx = start + i;
            float x = 0f;
            if (pulse < times.Length && i >= times[pulse])
            {
                float env = 1f - 0.45f * pulse / times.Length;
                x = amp * env;
                pulse++;
            }
            float y = x + a1 * y1 + a2 * y2;
            y2 = y1;
            y1 = y;
            if ((uint)idx < (uint)data.Length)
                data[idx] += y;
        }
    }
}
