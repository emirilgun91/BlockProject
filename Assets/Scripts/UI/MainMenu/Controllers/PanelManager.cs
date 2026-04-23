using DG.Tweening;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// MainMenu'deki overlay panelleri yönetir.
    /// ShapeShop, Upgrades, Settings panelleri burada açılıp kapanır.
    ///
    /// Kullanım:
    ///   PanelManager.Instance.OpenPanel(shapeShopPanel);
    ///   PanelManager.Instance.CloseCurrentPanel();
    ///
    /// Hierarchy:
    ///   Her panel bir CanvasGroup içermeli — fade için.
    /// </summary>
    public sealed class PanelManager : MonoBehaviour
    {
        public static PanelManager Instance { get; private set; }

        [Header("Fade Settings")]
        [SerializeField] private float _fadeInDuration  = 0.25f;
        [SerializeField] private float _fadeOutDuration = 0.2f;

        // Şu an açık olan panel
        private CanvasGroup _currentPanel;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Paneli açar. Önce mevcut paneli kapatır.</summary>
        public void OpenPanel(CanvasGroup panel)
        {
            if (panel == null) return;

            // Önce açık olanı kapat
            if (_currentPanel != null && _currentPanel != panel)
                ClosePanel(_currentPanel);

            _currentPanel = panel;
            panel.gameObject.SetActive(true);
            panel.alpha          = 0f;
            panel.interactable   = false;
            panel.blocksRaycasts = false;

            panel.DOFade(1f, _fadeInDuration)
                 .SetUpdate(true)
                 .OnComplete(() =>
                 {
                     panel.interactable   = true;
                     panel.blocksRaycasts = true;
                 });
        }

        /// <summary>Aktif paneli kapatır.</summary>
        public void CloseCurrentPanel()
        {
            if (_currentPanel == null) return;
            ClosePanel(_currentPanel);
            _currentPanel = null;
        }

        /// <summary>Belirli bir paneli kapatır.</summary>
        public void ClosePanel(CanvasGroup panel)
        {
            if (panel == null) return;

            panel.interactable   = false;
            panel.blocksRaycasts = false;

            panel.DOFade(0f, _fadeOutDuration)
                 .SetUpdate(true)
                 .OnComplete(() => panel.gameObject.SetActive(false));
        }
    }
}