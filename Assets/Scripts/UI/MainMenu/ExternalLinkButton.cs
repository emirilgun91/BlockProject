using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Bir Button'a dış bağlantı açtırır (Discord, YouTube, Steam wishlist vb).
    ///
    /// Kullanım: butonun kendi objesine ekle, Url alanına adresi yaz.
    /// Button referansı boşsa aynı objedeki Button otomatik bulunur.
    ///
    /// Not: Butona ayrıca OnClick üzerinden başka bir şey bağlamana gerek yok —
    /// bu bileşen kendi listener'ını ekler.
    /// </summary>
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    public sealed class ExternalLinkButton : MonoBehaviour
    {
        [Tooltip("Açılacak adres. http:// veya https:// ile başlamalı.")]
        [SerializeField] private string _url;

        [Tooltip("Tıklama sesi (opsiyonel).")]
        [SerializeField] private AudioClip _clickSfx;

        [Tooltip("Aynı linke arka arkaya basmayı engelleyen bekleme süresi.")]
        [SerializeField] private float _cooldown = 1f;

        private Button _button;
        private float  _lastClickTime = -99f;

        public string Url
        {
            get => _url;
            set { _url = value; RefreshInteractable(); }
        }

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Open);
            RefreshInteractable();
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(Open);
        }

        /// <summary>Adres boş veya geçersizse buton tıklanamaz olur — ölü buton kalmasın.</summary>
        private void RefreshInteractable()
        {
            if (_button == null) return;
            _button.interactable = IsValid(_url);
        }

        public void Open()
        {
            if (!IsValid(_url))
            {
                Debug.LogWarning($"[ExternalLink] '{name}' için geçerli bir adres yok: \"{_url}\"", this);
                return;
            }

            if (Time.unscaledTime - _lastClickTime < _cooldown) return;
            _lastClickTime = Time.unscaledTime;

            if (_clickSfx != null) AudioManager.Instance?.PlaySFX(_clickSfx);

            Application.OpenURL(_url);
        }

        /// <summary>
        /// Sadece http/https kabul edilir. Bu, yanlışlıkla girilen dosya yolu veya
        /// özel şema (steam://, javascript: gibi) ile beklenmedik bir şey açılmasını engeller.
        /// </summary>
        private static bool IsValid(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return System.Uri.TryCreate(url, System.UriKind.Absolute, out var uri) &&
                   (uri.Scheme == System.Uri.UriSchemeHttp || uri.Scheme == System.Uri.UriSchemeHttps);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrWhiteSpace(_url) && !IsValid(_url))
                Debug.LogWarning($"[ExternalLink] '{name}': adres http:// veya https:// ile başlamalı → \"{_url}\"", this);
        }
#endif
    }
}
