using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AdventureAudioPatternPlayer : MonoBehaviour
{
    [Header("交互再生する録音WAVパターン (A ➔ B ➔ C)")]
    public AudioClip[] patternClips;

    [Header("設定")]
    public float volume = 0.9f;
    public bool playOnStart = false;
    public float repeatInterval = 0f; // 0なら自動繰り返し無し

    AudioSource _audioSource;
    int _currentIndex = 0;
    float _timer = 0f;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (playOnStart)
        {
            PlayNextPattern();
        }
    }

    void Update()
    {
        if (repeatInterval > 0f)
        {
            _timer += Time.deltaTime;
            if (_timer >= repeatInterval)
            {
                _timer = 0f;
                PlayNextPattern();
            }
        }
    }

    /// <summary>
    /// 次のパターン (A ➔ B ➔ C ➔ A ...) に切り替えて再生します
    /// </summary>
    public void PlayNextPattern()
    {
        if (patternClips == null || patternClips.Length == 0)
        {
            return;
        }

        int index = _currentIndex;
        AudioClip clipToPlay = patternClips[index];

        // 次回のためにインデックスを更新 (0 ➔ 1 ➔ 2 ➔ 0 ...)
        _currentIndex = (_currentIndex + 1) % patternClips.Length;

        if (clipToPlay != null && _audioSource != null)
        {
            string patternLabel = ((char)('A' + (index % 26))).ToString();
            Debug.Log($"🔊 [{gameObject.name}] パターン {patternLabel} を交互再生 (Clip: {clipToPlay.name}, Index: {index})");

            _audioSource.PlayOneShot(clipToPlay, volume);
        }
    }
}
