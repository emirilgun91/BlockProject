using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Oyun sahnesinde Shape Shop'u bir overlay olarak açar — ana menüye gitmeden harcama.
    ///
    /// Panel, ana menüdekiyle aynı prefab'tır (ShapeShopPanel). Ana menüde onu
    /// <see cref="PanelManager"/> açar; oyun sahnesinde PanelManager yoktur, bu bileşen
    /// onun yerini tutar: panel kapalı başlar, <see cref="Open"/> ile üstte açılır,
    /// "geri" (ya da ESC) ile kapanıp çağıran ekrana (Game Over) döner.
    ///
    /// Game Over sırasında Time.timeScale = 0'dır; fade'ler SetUpdate(true) ile çalışır.
    /// Her zaman aktif bir objeye (Gameplay Canvas) konur — panelin kendisi kapalı olduğu
    /// için bileşen onun üstünde durmamalıdır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShapeShopOverlay : MonoBehaviour
    {
        public static ShapeShopOverlay Instance { get; private set; }

        [SerializeField] private ShapeShopController _shop;
        [SerializeField] private CanvasGroup         _panel;
        [SerializeField] private float _fadeIn  = 0.22f;
        [SerializeField] private float _fadeOut = 0.16f;

        /// <summary>Overlay tamamen kapanınca — çağıran ekran kendini yeniler (para değişmiş olabilir).</summary>
        public event Action Closed;

        public bool IsOpen { get; private set; }

        /// <summary>Parası en az bir şeye yetiyor mu — "Upgrade Now!" butonunun görünürlüğü.</summary>
        public bool HasAffordableUpgrade => _shop != null && _shop.HasAffordableUpgrade();

        private void Awake()
        {
            Instance = this;
            if (_shop != null) _shop.BackRequested += Close;
            if (_panel != null)
            {
                _panel.alpha = 0f;
                _panel.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (_shop != null) _shop.BackRequested -= Close;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (IsOpen && kb != null && kb.escapeKey.wasPressedThisFrame) Close();
        }

        public void Open()
        {
            if (IsOpen || _panel == null) return;
            IsOpen = true;

            _panel.transform.SetAsLastSibling();   // her şeyin (Game Over, pause) üstünde
            _panel.DOKill();
            _panel.gameObject.SetActive(true);
            _panel.alpha          = 0f;
            _panel.interactable   = false;
            _panel.blocksRaycasts = false;
            _panel.DOFade(1f, _fadeIn).SetUpdate(true).OnComplete(() =>
            {
                _panel.interactable   = true;
                _panel.blocksRaycasts = true;
            });
        }

        public void Close()
        {
            if (!IsOpen || _panel == null) return;
            IsOpen = false;

            _panel.interactable   = false;
            _panel.blocksRaycasts = false;
            _panel.DOKill();
            _panel.DOFade(0f, _fadeOut).SetUpdate(true).OnComplete(() =>
            {
                _panel.gameObject.SetActive(false);
                Closed?.Invoke();
            });
        }
    }
}
