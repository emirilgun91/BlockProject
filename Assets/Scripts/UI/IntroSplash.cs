using System;
using System.Collections;
using RogueBlockBlast.Core.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Açılıştaki stüdyo videosu (Intro sahnesi, build index 0). Video oynarken ana menü
    /// arka planda yüklenir; video bitince ya da herhangi bir tuş / tık / kumanda tuşuyla
    /// atlanınca görüntü siyaha söner ve menü açılır (menünün kendi SceneTransition'ı
    /// siyahtan açılır).
    ///
    /// Video bir RenderTexture'a, oradan tam ekran bir RawImage'a çizilir. Kamera düzlemi
    /// modu (CameraNearPlane) URP 2D Renderer'da hiçbir şey çizmiyor — hata da vermiyor.
    /// Video VP8/WebM: Windows'ta H.264 sistemin Media Foundation çözücüsüyle oynuyor,
    /// Steam Deck'te (Proton) bu güvenilir değil; VP8'i Unity kendisi çözer.
    /// Video hazırlanamazsa ya da hata verirse beklemeden menüye geçilir — açılış asla
    /// bu ekranda takılmamalı.
    /// </summary>
    public sealed class IntroSplash : MonoBehaviour
    {
        [SerializeField] private VideoClip _clip;
        [SerializeField] private string    _nextScene = "MainMenu";

        [Tooltip("Açılıştaki ilk dokunuş (pencereye tıklama vb.) videoyu hemen atlamasın.")]
        [SerializeField] private float _skipDelay = 0.4f;
        [SerializeField] private float _fadeOut   = 0.35f;

        [Tooltip("Video bu sürede hazırlanamazsa doğrudan menüye geçilir.")]
        [SerializeField] private float _prepareTimeout = 3f;

        private VideoPlayer    _player;
        private AudioSource    _audio;
        private RawImage       _image;
        private RenderTexture  _texture;
        private AsyncOperation _load;
        private IDisposable    _anyButton;
        private float          _startTime;
        private bool           _finishing;

        private void Start()
        {
            _startTime = Time.unscaledTime;

            // Menü video oynarken yüklenir; geçişte bekleme olmaz.
            _load = SceneManager.LoadSceneAsync(_nextScene);
            if (_load != null) _load.allowSceneActivation = false;

            if (_clip == null) { Finish(); return; }

            BuildScreen();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            // AudioManager (mixer) menü sahnesinde; burada ayarlar doğrudan uygulanır.
            _audio.volume = GameSettings.EffectiveMaster * GameSettings.SfxVolume;

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake       = false;
            _player.isLooping         = false;
            _player.skipOnDrop        = true;
            _player.waitForFirstFrame = true;
            _player.clip              = _clip;
            _player.renderMode        = VideoRenderMode.RenderTexture;
            _player.targetTexture     = _texture;
            _player.audioOutputMode   = VideoAudioOutputMode.AudioSource;
            _player.SetTargetAudioSource(0, _audio);
            _player.prepareCompleted += p => { _image.enabled = true; p.Play(); };
            _player.loopPointReached += _ => Finish();
            _player.errorReceived    += (_, msg) => { Debug.LogWarning($"[Intro] video error: {msg}"); Finish(); };
            _player.Prepare();

            _anyButton = InputSystem.onAnyButtonPress.Call(_ =>
            {
                if (Time.unscaledTime - _startTime >= _skipDelay) Finish();
            });

            StartCoroutine(Watchdog());
        }

        /// <summary>Siyah tam ekran + videonun oranını koruyan (letterbox) görüntü.</summary>
        private void BuildScreen()
        {
            _texture = new RenderTexture((int)_clip.width, (int)_clip.height, 0) { name = "IntroVideo" };

            var canvasGo = new GameObject("IntroCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = Color.black;

            var video = new GameObject("Video", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            video.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)video.transform);
            var fitter = video.GetComponent<AspectRatioFitter>();
            fitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = (float)_clip.width / _clip.height;
            _image = video.GetComponent<RawImage>();
            _image.texture = _texture;
            _image.raycastTarget = false;
            _image.enabled = false;   // ilk kare gelene kadar boş dokuyu gösterme
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private IEnumerator Watchdog()
        {
            while (!_player.isPrepared)
            {
                if (Time.unscaledTime - _startTime > _prepareTimeout) { Finish(); yield break; }
                yield return null;
            }
            // Bitiş olayı gelmezse de takılma: klip süresi + pay.
            yield return new WaitForSecondsRealtime((float)_clip.length + 1.5f);
            Finish();
        }

        private void Finish()
        {
            if (_finishing) return;
            _finishing = true;
            _anyButton?.Dispose();
            _anyButton = null;
            StopAllCoroutines();
            StartCoroutine(FadeAndLeave());
        }

        private IEnumerator FadeAndLeave()
        {
            float startVolume = _audio != null ? _audio.volume : 0f;
            for (float t = 0f; _image != null && _image.enabled && t < _fadeOut; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / _fadeOut;
                _image.color = new Color(1f, 1f, 1f, k);
                if (_audio != null) _audio.volume = startVolume * k;
                yield return null;
            }
            if (_image != null) _image.enabled = false;
            if (_player != null) _player.Stop();

            if (_load != null) _load.allowSceneActivation = true;
            else SceneManager.LoadScene(_nextScene);
        }

        private void OnDestroy()
        {
            _anyButton?.Dispose();
            if (_texture != null) { _texture.Release(); Destroy(_texture); }
        }
    }
}
