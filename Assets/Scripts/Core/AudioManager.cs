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

    // Ayarların kaydedileceği anahtarlar
    private const string MASTER_KEY = "MasterVolume";
    private const string MUSIC_KEY = "MusicVolume";
    private const string SFX_KEY = "SFXVolume";

    private void Awake()
    {
        // Gelişmiş Singleton yapısı
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
        // Oyun başladığında kaydedilmiş ses ayarlarını yükle (Yoksa varsayılan 1f yani %100 olsun)
        LoadVolumeSettings();
    }

    // --- Müzik Fonksiyonları ---
    
    /// <summary>
    /// Müzik çalar. Eğer zaten bir müzik çalıyorsa yumuşak geçiş (Crossfade) yapar.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying) return; // Aynı müzik çalıyorsa tekrar başlatma

        musicSource.outputAudioMixerGroup = musicGroup;
        musicSource.loop = loop;

        StopAllCoroutines(); // Eğer yarım kalmış bir geçiş varsa durdur
        StartCoroutine(CrossfadeMusic(clip));
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        float startVolume = musicSource.volume;

        // 1. Mevcut müziği yavaşça kıs
        if (musicSource.isPlaying)
        {
            while (musicSource.volume > 0)
            {
                musicSource.volume -= startVolume * (Time.deltaTime / musicFadeDuration);
                yield return null;
            }
            musicSource.Stop();
        }

        // 2. Yeni müziği koy ve başlat
        musicSource.clip = newClip;
        musicSource.Play();

        // 3. Yeni müziğin sesini yavaşça aç
        while (musicSource.volume < startVolume)
        {
            musicSource.volume += startVolume * (Time.deltaTime / musicFadeDuration);
            yield return null;
        }
        musicSource.volume = startVolume;
    }

    // --- SFX Fonksiyonları ---

    /// <summary>
    /// Ses efekti çalar. İsteğe bağlı olarak yapaylığı bozmak için pitch (ton) dalgalanması yapabilir.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, bool randomizePitch = false)
    {
        if (clip == null) return;

        sfxSource.outputAudioMixerGroup = sfxGroup;

        if (randomizePitch)
        {
            // Sesi ufak bir oranda inceleştirip kalınlaştırarak doğallık katar
            sfxSource.pitch = Random.Range(0.9f, 1.1f);
        }
        else
        {
            sfxSource.pitch = 1f; // Varsayılana döndür
        }

        sfxSource.PlayOneShot(clip, volume);
    }

    // --- Ayarlar (UI Slider'lar için) ---
    
    public void SetMasterVolume(float volume)
    {
        SetMixerVolume("MasterVol", volume);
        PlayerPrefs.SetFloat(MASTER_KEY, volume); // Ayarı diske kaydet
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
        // 0.0001f ile 1f arasını -80dB ile 0dB arasına çeker (Logaritmik ses düzeyi)
        float dB = Mathf.Log10(Mathf.Clamp(sliderValue, 0.0001f, 1f)) * 20f;
        mainMixer.SetFloat(parameterName, dB);
    }

    private void LoadVolumeSettings()
    {
        // Kayıtlı bir değer yoksa 0.75f (%75) ses seviyesi ile başla
        float masterVol = PlayerPrefs.GetFloat(MASTER_KEY, 0.75f);
        float musicVol = PlayerPrefs.GetFloat(MUSIC_KEY, 0.75f);
        float sfxVol = PlayerPrefs.GetFloat(SFX_KEY, 0.75f);

        SetMasterVolume(masterVol);
        SetMusicVolume(musicVol);
        SetSFXVolume(sfxVol);

        // Not: Eğer UI'da Slider'larınız varsa, oyun başladığında bu değerleri okuyup
        // Slider'ların value'sunu (örneğin masterSlider.value = PlayerPrefs.GetFloat(...))
        // güncellemeyi unutmayın, aksi takdirde Slider yanlış yeri gösterebilir.
    }
}