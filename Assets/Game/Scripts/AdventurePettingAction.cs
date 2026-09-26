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

    /// <summary>Rustを愛おしく撫でるアクションを実行</summary>
    public void PetRust(string speechText = "よしよし、いつもありがとうね", float duration = 3.0f)
    {
        if (_isPetting) return;
        StartCoroutine(PettingRoutine(speechText, duration));
    }

    IEnumerator PettingRoutine(string speechText, float duration)
    {
        _isPetting = true;
        _petDuration = duration;
        _petTimer = 0f;

        var drone = AdventureRustDrone.Instance ?? FindAnyObjectByType<AdventureRustDrone>();

        // 1. Nikoのいたわり台詞＋甘える効果音、胸元ホバリング
        if (drone != null)
        {
            drone.SetPettingState(true, duration);
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
            _petTimer += Time.deltaTime;
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

            // 胸元の触れ合いから、温かなハート・光のスパークルを生成
            if (drone != null && progress > 0.15f && progress < 0.9f)
            {
                if (Random.value < 0.22f)
                {
                    Vector3 contactPos = drone.transform.position + Vector3.up * 0.15f + Random.insideUnitSphere * 0.08f;
                    SpawnHeartSparkle(contactPos);
                }
            }

            yield return null;
        }

        if (drone != null)
        {
            drone.SetPettingState(false, 0f);
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
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(1.0f, 0.62f, 0.80f, 0.95f));
                var tex = AdventureRustDrone.GetSoftSmokeTexture();
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTexture("_MainTex", tex);
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
