using UnityEngine;
using UnityEngine.Audio;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer & Groups")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Settings")]
    [SerializeField] private float musicFadeDuration = 1.0f; // Müzik geçiş süresi

    [Header("Combo / Rapid SFX Settings")]
    [Tooltip("Üst üste binen sesleri engellemek için iki ses arası minimum bekleme (örn: 0.05 saniye)")]
    [SerializeField] private float sfxCooldown = 0.05f; 
    [SerializeField] private float comboPitchStep = 0.05f; // Her skorda ses ne kadar incelecek
    [SerializeField] private float maxComboPitch = 1.5f;   // Maksimum incelme
    [SerializeField] private float comboResetTime = 0.5f;  // Komboyu sıfırlama süresi

    // Combo sesleri için zaman ve ton takibi
    private float _lastScoreTime = -1f;
    private float _currentComboPitch = 1f;

    // Ayarların kaydedileceği anahtarlar
    private const string MASTER_KEY = "MasterVolume";
    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        LoadVolumeSettings();
    }

    // --- Müzik Fonksiyonları ---
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.outputAudioMixerGroup = musicGroup;
        musicSource.loop = loop;

        StopAllCoroutines();
        StartCoroutine(CrossfadeMusic(clip));
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        float startVolume = musicSource.volume;

        if (musicSource.isPlaying)
        {
            while (musicSource.volume > 0)
            {
                musicSource.volume -= startVolume * (Time.deltaTime / musicFadeDuration);
                yield return null;
            }
            musicSource.Stop();
        }

        musicSource.clip = newClip;
        musicSource.Play();

        while (musicSource.volume < startVolume)
        {
            musicSource.volume += startVolume * (Time.deltaTime / musicFadeDuration);
            yield return null;
        }
        musicSource.volume = startVolume;
    }

    // --- Standart SFX Fonksiyonu (Tekil Sesler İçin) ---
    public void PlaySFX(AudioClip clip, float volume = 1f, bool randomizePitch = false)
    {
        if (clip == null) return;

        sfxSource.outputAudioMixerGroup = sfxGroup;

        if (randomizePitch) sfxSource.pitch = Random.Range(0.9f, 1.1f);
        else sfxSource.pitch = 1f;

        sfxSource.PlayOneShot(clip, volume);
    }

    // --- YENİ: Combo ve Hızlı SFX Fonksiyonu (Skorlar İçin) ---
    /// <summary>
    /// Çok hızlı arka arkaya gelen seslerin üst üste binip patlamasını engeller.
    /// Her çağrılışında pitch değerini hafifçe artırarak kombo hissi verir.
    /// </summary>
    public void PlayScoreSFX(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        float currentTime = Time.unscaledTime;

        // 1. Üzerinden yeterince zaman geçtiyse pitch (ton) değerini sıfırla
        if (currentTime - _lastScoreTime > comboResetTime)
        {
            _currentComboPitch = 1f;
        }

        // 2. Cooldown kontrolü: Eğer iki ses arası süre "sfxCooldown"dan kısaysa, bu sesi çalma (üst üste binmeyi engelle)
        if (currentTime - _lastScoreTime >= sfxCooldown)
        {
            sfxSource.outputAudioMixerGroup = sfxGroup;
            
            // Sesi kombo durumuna göre ayarla
            sfxSource.pitch = _currentComboPitch;
            
            // Bir sonraki ses için pitch'i artır (maksimum limite kadar)
            _currentComboPitch = Mathf.Min(_currentComboPitch + comboPitchStep, maxComboPitch);

            sfxSource.PlayOneShot(clip, volume);
            
            // Son çalınma zamanını kaydet
            _lastScoreTime = currentTime;
        }
    }

    // --- Ayarlar ---
    public void SetMasterVolume(float volume)
    {
        SetMixerVolume("MasterVol", volume);
        PlayerPrefs.SetFloat(MASTER_KEY, volume);
    }

    public void SetMusicVolume(float volume)
    {
        SetMixerVolume("MusicVol", volume);
        PlayerPrefs.SetFloat(MUSIC_KEY, volume);
    }

    public void SetSFXVolume(float volume)
    {
        SetMixerVolume("SFXVol", volume);
        PlayerPrefs.SetFloat(SFX_KEY, volume);
    }

    private void SetMixerVolume(string parameterName, float sliderValue)
    {
        float dB = Mathf.Log10(Mathf.Clamp(sliderValue, 0.0001f, 1f)) * 20f;
        mainMixer.SetFloat(parameterName, dB);
    }

    private void LoadVolumeSettings()
    {
        float masterVol = PlayerPrefs.GetFloat(MASTER_KEY, 0.75f);
        float musicVol = PlayerPrefs.GetFloat(MUSIC_KEY, 0.75f);
        float sfxVol = PlayerPrefs.GetFloat(SFX_KEY, 0.75f);

        SetMasterVolume(masterVol);
        SetMusicVolume(musicVol);
        SetSFXVolume(sfxVol);
    }
}