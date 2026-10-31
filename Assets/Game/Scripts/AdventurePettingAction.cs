using UnityEngine;
using System.Collections;

/// <summary>
/// NikoがRustを手当てした時や、何かを成し遂げた時（パーツ回収、キーストーン獲得など）に、
/// 愛おしくRustを撫でる（ペッティング＆スキンシップ）アクションを司るコンポーネント。
/// Niko正規のアニメーション（NikoPickItemDown / NikoIdle）を用いて、右手が脱臼・変形することなく
/// 自然で愛らしいスキンシップを実現する。
/// </summary>
public class AdventurePettingAction : MonoBehaviour
{
    public static AdventurePettingAction Instance { get; private set; }

    AdventurePlayerController _player;
    Animator _anim;

    bool _isPetting = false;
    public bool IsPetting => _isPetting;

    float _petTimer = 0f;
    float _petDuration = 2.0f;

    static ParticleSystem _heartFxInstance;
    static AudioClip _sweetCuddleSound;

    public static void Ensure(GameObject playerGo)
    {
        if (Instance != null) return;
        var comp = playerGo.GetComponent<AdventurePettingAction>() ?? playerGo.AddComponent<AdventurePettingAction>();
        Instance = comp;
    }

    void Awake()
    {
        Instance = this;
        _player = GetComponent<AdventurePlayerController>();
        _anim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // 撫でアクション中に移動入力があった場合、自然に移動へ移行できるよう解除
        if (_isPetting && _petTimer > 0.45f && _player != null && _player.HasMoveInput)
        {
            CancelPetting();
            return;
        }

        // コルーチン中断などによる万が一の永続ロックを自動解除
        if (_isPetting && _petTimer > _petDuration + 1.2f)
        {
            CancelPetting();
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_heartFxInstance != null)
        {
            Destroy(_heartFxInstance.gameObject);
            _heartFxInstance = null;
        }
    }

    /// <summary>撫でアクションの強制解除（移動再開・リセット時）</summary>
    public void CancelPetting()
    {
        _isPetting = false;
        _petTimer = 0f;
        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();
        drone?.SetPettingState(false, 0f);
    }

    private static readonly string[] SweetPettingLines = new string[]
    {
        "よしよし、いつもありがとうね、Rust",
        "今日も最高の相棒だよ！",
        "Rust、あったかいね。ずっと一緒だよ",
        "ふふ、くすぐったい？いい子だね",
        "いつもそばにいてくれて、心強いよ"
    };

    /// <summary>Rustを愛おしく撫でるアクションを実行</summary>
    public void PetRust(string speechText = null, float duration = 3.0f)
    {
        if (_isPetting) return;
        if (string.IsNullOrEmpty(speechText))
        {
            speechText = SweetPettingLines[Random.Range(0, SweetPettingLines.Length)];
        }
        string loc = AdventureLocalizationManager.Get(speechText);
        StartCoroutine(PettingRoutine(loc, duration));
    }

    IEnumerator PettingRoutine(string speechText, float duration)
    {
        _isPetting = true;
        _petDuration = duration;
        _petTimer = 0f;

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();

        // 1. Nikoのいたわり台詞＋甘える効果音、胸元ホバリング、笑顔LED
        if (drone != null)
        {
            drone.SetPettingState(true, duration);
            drone.SetExpression(AdventureRustDrone.FaceExpressionType.Smiling, duration + 0.5f);
            drone.SpeakAsNiko(speechText, duration + 1.0f);
            PlaySweetCuddleSound(drone.transform.position);
        }

        // 2. Nikoは穏やかなアイドル姿勢でRustをあたたかく迎える
        if (_anim != null)
        {
            _anim.CrossFadeInFixedTime("NikoIdle", 0.2f);
        }

        // 3. 撫でている間の演出（胸元の触れ合い・ハートエフェクト）
        while (_petTimer < _petDuration)
        {
            _petTimer += Time.unscaledDeltaTime;
            float progress = _petTimer / _petDuration;

            // 撫で始め（最初の約0.7秒間）にNikoをRustの方向へ向かせ、胸元到着後はブレないよう固定
            if (drone != null && progress < 0.25f)
            {
                Vector3 toDrone = (drone.transform.position - transform.position);
                toDrone.y = 0f;
                if (toDrone.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toDrone), Time.deltaTime * 5.0f);
                }
            }

            // 胸元の触れ合いから、温かなハート・光のスパークルを生成（高頻度で舞い上がる）
            if (drone != null && progress > 0.12f && progress < 0.95f)
            {
                if (Random.value < 0.35f)
                {
                    Vector3 contactPos = drone.transform.position + Vector3.up * 0.12f + Random.insideUnitSphere * 0.10f;
                    SpawnHeartSparkle(contactPos);
                }
            }

            yield return null;
        }

        if (drone != null)
        {
            drone.SetPettingState(false, 0f);
            drone.SetExpression(AdventureRustDrone.FaceExpressionType.Joyful, 2.5f);
        }

        _isPetting = false;

        // 撫で終わった後、Rustが満足そうに小さくピピッ！と一回転
        if (drone != null)
        {
            drone.StartCoroutine(drone.CheerSpinRoutine());
        }
    }

    void SpawnHeartSparkle(Vector3 pos)
    {
        if (_heartFxInstance == null)
        {
            var fxGo = new GameObject("HeartSparkleFX");
            fxGo.transform.SetParent(transform, false);
            _heartFxInstance = fxGo.AddComponent<ParticleSystem>();
            var main = _heartFxInstance.main;
            main.startSpeed = 0.5f;
            main.startLifetime = 1.4f;
            main.startSize = 0.26f;
            main.startColor = new Color(1.0f, 0.62f, 0.80f, 0.95f); // 愛らしい温かなピンクゴールド
            main.loop = false;
            main.maxParticles = 50;

            var shape = _heartFxInstance.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            main.startSpeed = 0.75f; // ふわふわと上へ昇る
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var rend = fxGo.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader) { name = "HeartSparkle_Mat" };
                Color pinkGold = new Color(1.0f, 0.62f, 0.80f, 0.95f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", pinkGold);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", pinkGold);

                var tex = AdventureRustDrone.GetSoftSmokeTexture();
                if (tex != null)
                {
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }
                rend.sharedMaterial = mat;
            }
        }

        _heartFxInstance.transform.position = pos;
        _heartFxInstance.Emit(1);
    }

    void PlaySweetCuddleSound(Vector3 pos)
    {
        if (_sweetCuddleSound == null)
            _sweetCuddleSound = SynthesizeCuddleSound();

        var drone = AdventureRustDrone.Instance;
        var audio = drone != null ? drone.GetComponent<AudioSource>() : null;
        if (audio != null && _sweetCuddleSound != null)
        {
            audio.pitch = Random.Range(1.1f, 1.25f);
            audio.PlayOneShot(_sweetCuddleSound, 0.75f);
        }
    }

    /// <summary>「キュ〜…ン♪ ピピッ」と甘えたような温かいドローンの電子音</summary>
    static AudioClip SynthesizeCuddleSound()
    {
        const int rate = 44100;
        float dur = 0.85f;
        int count = (int)(rate * dur);
        float[] data = new float[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float freq;
            float amp;

            if (t < 0.55f)
            {
                // キュ〜ン（ピッチがゆっくり上昇して温かく落ち着く）
                freq = Mathf.Lerp(480f, 720f, Mathf.Sin((t / 0.55f) * Mathf.PI * 0.5f));
                amp = Mathf.Sin((t / 0.55f) * Mathf.PI) * 0.28f;
            }
            else
            {
                // ピピッ♪（嬉しそうに小さく跳ねる）
                float t2 = (t - 0.55f) / 0.30f;
                freq = (t2 < 0.15f) ? 880f : 1174f;
                amp = Mathf.Exp(-t2 * 8.0f) * 0.24f;
            }

            // サイン波＋柔らかな倍音
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) + 0.2f * Mathf.Sin(4f * Mathf.PI * freq * t);
            data[i] = wave * amp;
        }

        var clip = AudioClip.Create("RustSweetCuddle", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
