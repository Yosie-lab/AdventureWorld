using UnityEngine;
using System.Collections;

/// <summary>
/// NikoがRustを手当てした時や、何かを成し遂げた時（パーツ回収、キーストーン獲得など）に、
/// 愛おしくRustを撫でる（ペッティング＆スキンシップ）アクションを司るコンポーネント。
/// </summary>
public class AdventurePettingAction : MonoBehaviour
{
    public static AdventurePettingAction Instance { get; private set; }

    AdventurePlayerController _player;
    Animator _anim;
    Transform _rightHandBone;
    Transform _rightArmBone;

    bool _isPetting = false;
    public bool IsPetting => _isPetting;

    float _petTimer = 0f;
    float _petDuration = 2.0f;
    float _petWeight = 0f; // 0.0〜1.0の手のブレンドウェイト

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
        FindBones();
    }

    void FindBones()
    {
        if (_anim != null && _anim.isHuman)
        {
            _rightHandBone = _anim.GetBoneTransform(HumanBodyBones.RightHand);
            _rightArmBone = _anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        if (_rightHandBone == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLower();
                if ((n.Contains("hand") || n.Contains("wrist")) && (n.Contains("r") || n.Contains("right")))
                {
                    _rightHandBone = t;
                    break;
                }
            }
        }

        if (_rightArmBone == null)
        {
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLower();
                if (n.Contains("arm") && (n.Contains("r") || n.Contains("right")) && !n.Contains("fore"))
                {
                    _rightArmBone = t;
                    break;
                }
            }
        }
    }

    /// <summary>Rustを愛おしく撫でるアクションを実行</summary>
    public void PetRust(string speechText = "よしよし、いつもありがとうね", float duration = 2.2f)
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

        // 1. Rustへの挨拶・台詞と甘える効果音
        if (drone != null)
        {
            drone.SpeakCustom(speechText, duration + 1.0f);
            PlaySweetCuddleSound(drone.transform.position);
        }

        // 2. 撫でるアニメーション（開始時は手を持ち上げ、ストロークし、戻す）
        while (_petTimer < _petDuration)
        {
            _petTimer += Time.deltaTime;
            float progress = _petTimer / _petDuration;

            // イーズイン・アウトのウェイト曲線
            if (progress < 0.25f)
                _petWeight = Mathf.SmoothStep(0f, 1f, progress / 0.25f);
            else if (progress > 0.75f)
                _petWeight = Mathf.SmoothStep(1f, 0f, (progress - 0.75f) / 0.25f);
            else
                _petWeight = 1f;

            // RustをNikoの手の届く位置（Nikoの少し前方・胸の高さ）へ優しく寄り添わせる
            if (drone != null)
            {
                Vector3 petAnchor = transform.position + transform.forward * 0.75f + transform.right * 0.25f + Vector3.up * 1.05f;
                drone.transform.position = Vector3.Lerp(drone.transform.position, petAnchor, Time.deltaTime * 6.0f);

                // RustがNikoに甘えるように少し首を傾げる
                Quaternion cuddleRot = Quaternion.LookRotation(transform.position - drone.transform.position)
                                     * Quaternion.Euler(Mathf.Sin(Time.time * 6f) * 8f, 0f, 14f);
                drone.transform.rotation = Quaternion.Slerp(drone.transform.rotation, cuddleRot, Time.deltaTime * 8.0f);
            }

            // NikoをRustの方へ向かせる
            if (drone != null)
            {
                Vector3 toDrone = (drone.transform.position - transform.position);
                toDrone.y = 0f;
                if (toDrone.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toDrone), Time.deltaTime * 6.0f);
                }
            }

            // ハート・温かい光のパーティクルを生成（撫でている最中にポワポワと浮かぶ）
            if (drone != null && progress > 0.2f && progress < 0.8f)
            {
                if (Random.value < 0.18f)
                {
                    Vector3 contactPos = drone.transform.position + Vector3.up * 0.25f + Random.insideUnitSphere * 0.12f;
                    SpawnHeartSparkle(contactPos);
                }
            }

            yield return null;
        }

        _petWeight = 0f;
        _isPetting = false;

        // 撫で終わった後、Rustが嬉しそうに小さくピピッ！と一回転
        if (drone != null)
        {
            drone.StartCoroutine(drone.CheerSpinRoutine());
        }
    }

    void LateUpdate()
    {
        if (_petWeight <= 0.001f) return;

        var drone = AdventureRustDrone.Instance;
        if (drone == null) return;

        Vector3 targetHeadPos = drone.transform.position + Vector3.up * 0.22f;

        // 優しくなでなでするストローク運動（前後・上下の穏やかな往復）
        float strokePhase = Mathf.Sin(_petTimer * 7.5f);
        Vector3 strokeOffset = transform.forward * (strokePhase * 0.06f) + Vector3.up * (Mathf.Abs(strokePhase) * 0.035f);
        targetHeadPos += strokeOffset;

        // ボーンがある場合、右腕・右手をRustの頭の上へ向けて滑らかに曲げる
        if (_rightHandBone != null)
        {
            Vector3 handTarget = targetHeadPos;
            _rightHandBone.position = Vector3.Lerp(_rightHandBone.position, handTarget, _petWeight * 0.85f);
            _rightHandBone.rotation = Quaternion.Slerp(_rightHandBone.rotation, Quaternion.Euler(0f, transform.eulerAngles.y - 45f, -75f), _petWeight * 0.85f);
        }

        if (_rightArmBone != null)
        {
            Quaternion reachRot = Quaternion.LookRotation(targetHeadPos - _rightArmBone.position, Vector3.up);
            _rightArmBone.rotation = Quaternion.Slerp(_rightArmBone.rotation, reachRot, _petWeight * 0.45f);
        }
    }

    void SpawnHeartSparkle(Vector3 pos)
    {
        if (_heartFxInstance == null)
        {
            var fxGo = new GameObject("HeartSparkleFX");
            _heartFxInstance = fxGo.AddComponent<ParticleSystem>();
            var main = _heartFxInstance.main;
            main.startSpeed = 0.65f;
            main.startLifetime = 1.3f;
            main.startSize = 0.28f;
            main.startColor = new Color(1.0f, 0.65f, 0.82f, 0.95f); // 愛らしい温かなピンクゴールド
            main.loop = false;
            main.maxParticles = 50;

            var shape = _heartFxInstance.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var vel = _heartFxInstance.velocityOverLifetime;
            vel.enabled = true;
            vel.y = 0.8f; // ふわふわと上へ昇る

            var rend = fxGo.GetComponent<ParticleSystemRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            rend.material = new Material(shader);
            rend.material.SetColor("_BaseColor", new Color(1.0f, 0.65f, 0.82f, 0.95f));
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
