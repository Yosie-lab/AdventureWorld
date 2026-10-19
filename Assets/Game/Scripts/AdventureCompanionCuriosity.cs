using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 相棒Rust＆Floatの好奇心リアクション（Companion Connection）
/// ピアノを弾くカピバラや咲き誇る花畑に近づいた際、
/// 相棒が近寄って首をかしげたり、小さなハートや音符を浮かべて喜ぶ世界の温もり演出。
/// </summary>
public class AdventureCompanionCuriosity : MonoBehaviour
{
    private static AdventureCompanionCuriosity _instance;
    public static AdventureCompanionCuriosity Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void AutoInitialize()
    {
        if (_instance != null) return;
        var existing = Object.FindAnyObjectByType<AdventureCompanionCuriosity>();
        if (existing != null)
        {
            _instance = existing;
            return;
        }

        var go = new GameObject("AdventureCompanionCuriosity");
        _instance = go.AddComponent<AdventureCompanionCuriosity>();
        DontDestroyOnLoad(go);
    }

    private Transform _playerTransform;
    private AdventureRustDrone _rustDrone;
    private AudioSource _audioSource;
    private AudioClip _curiosityChimeClip;
    private AudioClip _flowerChimeClip;

    private Texture2D _heartTexture;
    private Texture2D _musicNoteTexture;
    private Material _heartMaterial;
    private Material _musicNoteMaterial;

    // スポット定義
    private enum SpotType { None, CapytaPiano, FlowerMeadow }

    private float _nextCheckTime = 0f;
    private float _lastReactionTime = -60f;
    private SpotType _currentReactedSpot = SpotType.None;
    private Vector3 _currentReactedSpotPos;

    // 好奇心ステート
    private bool _isCuriousActive = false;
    private float _curiousTimer = 0f;
    private const float CuriousDuration = 4.5f;
    private Vector3 _curiousTargetPos;
    private float _tiltAngle = 0f;

    // 花畑の代表的アンカー座標リスト（西斜面・高原・湧水池周辺）
    private static readonly Vector3[] FlowerMeadowAnchors = new Vector3[]
    {
        // 西斜面陽だまり花畑
        new Vector3(190f, 0f, 240f),
        new Vector3(205f, 0f, 288f),
        new Vector3(220f, 0f, 285f),
        new Vector3(230f, 0f, 238f),
        new Vector3(245f, 0f, 305f),
        // 中央台地・高原花畑絨毯
        new Vector3(268f, 0f, 280f),
        new Vector3(285f, 0f, 295f),
        new Vector3(302f, 0f, 275f),
        new Vector3(315f, 0f, 325f),
        // せせらぎ池・オアシス水辺花畑
        new Vector3(270f, 0f, 345f),
        new Vector3(290f, 0f, 355f),
        new Vector3(474f, 0f, 460f),
        // 北部高台花畑
        new Vector3(288f, 0f, 410f),
        new Vector3(310f, 0f, 405f),
        // 南部陽だまり
        new Vector3(285f, 0f, 228f),
        new Vector3(308f, 0f, 215f)
    };

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0.5f;

        CreateProceduralTextures();
        CreateProceduralChimes();
    }

    void Start()
    {
        FindReferences();
    }

    void FindReferences()
    {
        if (_playerTransform == null)
        {
            var p = GameObject.Find("Niko") ?? GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }

        if (_rustDrone == null)
        {
            _rustDrone = AdventureRustDrone.Instance ?? Object.FindAnyObjectByType<AdventureRustDrone>();
        }
    }

    void Update()
    {
        if (_playerTransform == null || _rustDrone == null)
        {
            FindReferences();
            if (_playerTransform == null || _rustDrone == null) return;
        }

        // 好奇心リアクション進行中のアニメーション
        if (_isCuriousActive)
        {
            UpdateCuriosityBehavior();
            return;
        }

        // 0.25秒間隔の近接スポット検知
        if (Time.time >= _nextCheckTime)
        {
            _nextCheckTime = Time.time + 0.25f;
            CheckNearbySpots();
        }
    }

    private void CheckNearbySpots()
    {
        // 連続発火を防ぐクールダウン（同じスポットは30秒、全体で10秒）
        if (Time.time < _lastReactionTime + 10f) return;

        Vector3 playerPos = _playerTransform.position;

        // 1. カピバラピアノの検知（12m以内）
        Vector3 pianoPos = Vector3.zero;
        bool foundPiano = false;

        var pianoRelic = AdventureAncientPianoRelic.Instance;
        if (pianoRelic != null)
        {
            pianoPos = pianoRelic.transform.position;
            if (Vector3.Distance(playerPos, pianoPos) <= 13.5f)
            {
                foundPiano = true;
            }
        }
        else
        {
            // インスタンス未生成時は候補地座標でチェック
            foreach (var cand in AdventureAncientPianoRelic.Candidates)
            {
                if (Vector3.Distance(new Vector3(playerPos.x, 0f, playerPos.z), new Vector3(cand.position.x, 0f, cand.position.z)) <= 13.5f)
                {
                    pianoPos = cand.position;
                    foundPiano = true;
                    break;
                }
            }
        }

        if (foundPiano)
        {
            if (_currentReactedSpot != SpotType.CapytaPiano || Time.time > _lastReactionTime + 30f)
            {
                TriggerReaction(SpotType.CapytaPiano, pianoPos);
                return;
            }
        }

        // 2. 花畑パッチの検知（8.5m以内）
        foreach (var anchor in FlowerMeadowAnchors)
        {
            float dist = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(anchor.x, anchor.z));
            if (dist <= 8.5f)
            {
                if (_currentReactedSpot != SpotType.FlowerMeadow || Vector3.Distance(anchor, _currentReactedSpotPos) > 15f || Time.time > _lastReactionTime + 35f)
                {
                    TriggerReaction(SpotType.FlowerMeadow, anchor);
                    return;
                }
            }
        }
    }

    private void TriggerReaction(SpotType type, Vector3 spotPos)
    {
        _currentReactedSpot = type;
        _currentReactedSpotPos = spotPos;
        _lastReactionTime = Time.time;
        _isCuriousActive = true;
        _curiousTimer = 0f;
        _curiousTargetPos = spotPos;

        Vector3 dronePos = _rustDrone.transform.position;
        Vector3 floatPos = _playerTransform.position;

        if (type == SpotType.CapytaPiano)
        {
            // ピアノ・カピタへの好奇心リアクション
            if (_audioSource != null && _curiosityChimeClip != null)
            {
                _audioSource.transform.position = dronePos;
                _audioSource.PlayOneShot(_curiosityChimeClip, 0.75f);
            }

            // Rustのセリフ発話
            string[] pianoLines = new string[]
            {
                "わあ…カピタがピアノを弾いてるよ！優しいメロディだね♪",
                "ピロリン♪ 聴いてFloat！ピアノの音が風に乗って聴こえるよ…！",
                "プヒヒ…カピタさん上手だね！Rustもこの曲大好きだよ♪"
            };
            string line = pianoLines[Random.Range(0, pianoLines.Length)];
            _rustDrone.SpeakCustom(line, 4.2f);

            // 音符（♪）と小さなハート（♥）をポップ
            StartCoroutine(SpawnMelodyShowerRoutine(dronePos + Vector3.up * 0.4f, 6, isNote: true));
            StartCoroutine(SpawnMelodyShowerRoutine(floatPos + Vector3.up * 1.8f, 4, isNote: false)); // Floatの頭上にもハート
        }
        else if (type == SpotType.FlowerMeadow)
        {
            // 花畑への好奇心リアクション
            if (_audioSource != null && _flowerChimeClip != null)
            {
                _audioSource.transform.position = dronePos;
                _audioSource.PlayOneShot(_flowerChimeClip, 0.7f);
            }

            string[] flowerLines = new string[]
            {
                "わあ！きれいなお花がたくさん咲いてる！いい香りだね、Float♪",
                "ピピッ♪ お花畑だ！風に揺れてキラキラ光ってるよ！",
                "あたたかいね…Float、お花がとっても綺麗だよ♥"
            };
            string line = flowerLines[Random.Range(0, flowerLines.Length)];
            _rustDrone.SpeakCustom(line, 4.0f);

            // ハート（♥）と音符（♪）をポップ
            StartCoroutine(SpawnMelodyShowerRoutine(dronePos + Vector3.up * 0.4f, 5, isNote: false));
            StartCoroutine(SpawnMelodyShowerRoutine(floatPos + Vector3.up * 1.8f, 3, isNote: true));
        }
    }

    private void UpdateCuriosityBehavior()
    {
        _curiousTimer += Time.deltaTime;
        if (_curiousTimer >= CuriousDuration)
        {
            _isCuriousActive = false;
            return;
        }

        // 首をかしげるチルトモーション（ゆったり左右に傾ける愛らしい仕草）
        // 周期 1.8秒で -18度 〜 +18度
        _tiltAngle = Mathf.Sin(_curiousTimer * 3.8f) * 16.5f;

        // Rustの機体を少し傾けて興味深そうに見つめる
        if (_rustDrone != null)
        {
            Transform droneTr = _rustDrone.transform;
            Vector3 lookDir = (_curiousTargetPos - droneTr.position);
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
            {
                Quaternion baseRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                // Roll軸（首かしげ）を合成
                Quaternion tiltRot = Quaternion.AngleAxis(_tiltAngle, droneTr.forward);
                droneTr.rotation = Quaternion.Slerp(droneTr.rotation, tiltRot * baseRot, Time.deltaTime * 6f);
            }
        }
    }

    /// <summary>
    /// 音符やハートをプロシージャルにポップさせてゆらゆら舞い上がらせるコルーチン
    /// </summary>
    private IEnumerator SpawnMelodyShowerRoutine(Vector3 center, int count, bool isNote)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-0.45f, 0.45f),
                Random.Range(0f, 0.35f),
                Random.Range(-0.45f, 0.45f)
            );
            SpawnSingleNoteOrHeart(center + offset, isNote);
            yield return new WaitForSeconds(Random.Range(0.12f, 0.24f));
        }
    }

    private void SpawnSingleNoteOrHeart(Vector3 pos, bool isNote)
    {
        var go = new GameObject(isNote ? "NoteParticle" : "HeartParticle");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * Random.Range(0.22f, 0.32f);

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.mesh = CreateBillboardQuadMesh();
        mr.material = isNote ? _musicNoteMaterial : _heartMaterial;

        // 浮遊アニメーション
        StartCoroutine(FloatAndFadeRoutine(go, isNote));
    }

    private IEnumerator FloatAndFadeRoutine(GameObject particleGo, bool isNote)
    {
        float duration = Random.Range(1.8f, 2.4f);
        float elapsed = 0f;
        Vector3 startPos = particleGo.transform.position;
        Vector3 driftVelocity = new Vector3(
            Random.Range(-0.25f, 0.25f),
            Random.Range(0.65f, 1.1f),
            Random.Range(-0.25f, 0.25f)
        );

        float wobbleFreq = Random.Range(3f, 5f);
        float wobbleAmp = Random.Range(0.15f, 0.25f);
        Vector3 wobbleAxis = Vector3.Cross(Vector3.up, Camera.main != null ? Camera.main.transform.forward : Vector3.forward);

        Transform tr = particleGo.transform;
        Material mat = particleGo.GetComponent<MeshRenderer>().material;
        Color initialColor = mat.color;

        while (elapsed < duration)
        {
            if (particleGo == null) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 上昇と横揺れ
            Vector3 currentPos = startPos + driftVelocity * elapsed;
            currentPos += wobbleAxis * (Mathf.Sin(elapsed * wobbleFreq) * wobbleAmp);
            tr.position = currentPos;

            // ビルボード（常にカメラに向ける）
            if (Camera.main != null)
            {
                tr.rotation = Quaternion.LookRotation(tr.position - Camera.main.transform.position);
            }

            // スケール（ポンと出てゆっくり小さくなる）
            float scaleMul = Mathf.Sin(t * Mathf.PI);
            tr.localScale = Vector3.one * (0.28f * (0.6f + 0.4f * scaleMul));

            // フェードアウト
            float alpha = (1f - t);
            mat.color = new Color(initialColor.r, initialColor.g, initialColor.b, alpha);

            yield return null;
        }

        if (particleGo != null)
        {
            Destroy(particleGo);
        }
    }

    private Mesh CreateBillboardQuadMesh()
    {
        var mesh = new Mesh { name = "BillboardQuad" };
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateNormals();
        return mesh;
    }

    private void CreateProceduralTextures()
    {
        int size = 64;

        // 1. ハート（♥）テクスチャ
        _heartTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _heartTexture.filterMode = FilterMode.Bilinear;
        Color transparent = new Color(0, 0, 0, 0);
        Color pink = new Color(1f, 0.42f, 0.68f, 1f); // 鮮やかで優しいコーラルピンク

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * 0.5f) / (size * 0.44f);
                float ny = (y - size * 0.42f) / (size * 0.44f);
                // ハート曲線方程式
                float f = nx * nx + ny * ny - 0.72f;
                float inside = f * f * f - nx * nx * ny * ny * ny;
                if (inside <= 0f)
                {
                    float edge = Mathf.Clamp01(-inside * 9.0f);
                    _heartTexture.SetPixel(x, y, new Color(pink.r, pink.g, pink.b, edge));
                }
                else
                {
                    _heartTexture.SetPixel(x, y, transparent);
                }
            }
        }
        _heartTexture.Apply();

        // 2. 音符（♪）テクスチャ
        _musicNoteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        _musicNoteTexture.filterMode = FilterMode.Bilinear;
        Color noteColor = new Color(1f, 0.88f, 0.35f, 1f); // 温かみのあるゴールドイエロー

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size;
                float v = (float)y / size;

                // 符頭: 左下 (0.35, 0.3) の斜め楕円
                float dx = (u - 0.36f);
                float dy = (v - 0.28f);
                float rotX = dx * 0.86f + dy * 0.5f;
                float rotY = -dx * 0.5f + dy * 0.86f;
                float headDist = (rotX * rotX) / (0.13f * 0.13f) + (rotY * rotY) / (0.09f * 0.09f);

                // 符幹: 垂直な棒 (u: 0.46〜0.51, v: 0.28〜0.78)
                bool isStem = (u >= 0.44f && u <= 0.51f && v >= 0.28f && v <= 0.78f);

                // 符尾: 右上の旗 (u: 0.46〜0.75, v: 0.62〜0.78)
                bool isFlag = false;
                if (u >= 0.48f && u <= 0.74f && v >= 0.56f && v <= 0.78f)
                {
                    float flagCurve = 0.78f - Mathf.Pow((u - 0.48f) / 0.26f, 1.4f) * 0.22f;
                    if (Mathf.Abs(v - flagCurve) < 0.07f) isFlag = true;
                }

                if (headDist <= 1.0f || isStem || isFlag)
                {
                    _musicNoteTexture.SetPixel(x, y, noteColor);
                }
                else
                {
                    _musicNoteTexture.SetPixel(x, y, transparent);
                }
            }
        }
        _musicNoteTexture.Apply();

        // マテリアル生成
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default");

        _heartMaterial = new Material(shader) { name = "Curiosity_HeartMat" };
        _heartMaterial.mainTexture = _heartTexture;
        SetupTransparentMaterial(_heartMaterial, pink);

        _musicNoteMaterial = new Material(shader) { name = "Curiosity_NoteMat" };
        _musicNoteMaterial.mainTexture = _musicNoteTexture;
        SetupTransparentMaterial(_musicNoteMaterial, noteColor);
    }

    private void SetupTransparentMaterial(Material mat, Color col)
    {
        mat.color = col;
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void CreateProceduralChimes()
    {
        // 1. ピアノ好奇心チャイム（ド・ミ・ソ・ドの高音アルペジオ bell / music box）
        int sampleRate = 44100;
        float duration = 1.2f;
        int sampleCount = (int)(sampleRate * duration);
        float[] pSamples = new float[sampleCount];
        float[] freqs = new float[] { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
        float noteDelay = 0.14f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;
            for (int n = 0; n < freqs.Length; n++)
            {
                float noteT = t - n * noteDelay;
                if (noteT >= 0f)
                {
                    float env = Mathf.Exp(-noteT * 5.5f);
                    float sine = Mathf.Sin(2f * Mathf.PI * freqs[n] * noteT);
                    float harm = Mathf.Sin(4f * Mathf.PI * freqs[n] * noteT) * 0.28f;
                    sample += (sine + harm) * env * 0.24f;
                }
            }
            pSamples[i] = Mathf.Clamp(sample, -1f, 1f);
        }
        _curiosityChimeClip = AudioClip.Create("Curiosity_PianoChime", sampleCount, 1, sampleRate, false);
        _curiosityChimeClip.SetData(pSamples, 0);

        // 2. 花畑チャイム（そよ風とベルの軽やかな 3音チャイム）
        float[] fSamples = new float[sampleCount];
        float[] fFreqs = new float[] { 659.25f, 880.00f, 1174.66f }; // E5, A5, D6
        float fDelay = 0.16f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float sample = 0f;
            for (int n = 0; n < fFreqs.Length; n++)
            {
                float noteT = t - n * fDelay;
                if (noteT >= 0f)
                {
                    float env = Mathf.Exp(-noteT * 4.8f);
                    float sine = Mathf.Sin(2f * Mathf.PI * fFreqs[n] * noteT);
                    float bell = Mathf.Sin(3f * Mathf.PI * fFreqs[n] * noteT) * 0.22f;
                    sample += (sine + bell) * env * 0.28f;
                }
            }
            fSamples[i] = Mathf.Clamp(sample, -1f, 1f);
        }
        _flowerChimeClip = AudioClip.Create("Curiosity_FlowerChime", sampleCount, 1, sampleRate, false);
        _flowerChimeClip.SetData(fSamples, 0);
    }
}
