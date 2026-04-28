using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RogueBlockBlast.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string _gameplaySceneName = "SampleScene";

        [Header("Coin Display")]
        [SerializeField] private TMP_Text _coinText;

        [Header("Buttons")]
        [SerializeField] private MenuButtonView _btnNewRun;
        [SerializeField] private MenuButtonView _btnShapeShop;
        [SerializeField] private MenuButtonView _btnUpgrades;
        [SerializeField] private MenuButtonView _btnSettings;
        [SerializeField] private MenuButtonView _btnQuit;

        [Header("Panels — CanvasGroup")]
        [SerializeField] private CanvasGroup _shapeShopPanel;
        [SerializeField] private CanvasGroup _upgradesPanel;
        [SerializeField] private CanvasGroup _settingsPanel;
        private UpgradeLibrarySO upgradeLibrary;
        private void Start()
        {
            // Coin UI
            if (CoinWallet.Instance != null)
            {
                UpdateCoinText(CoinWallet.Instance.Balance);
                CoinWallet.Instance.OnBalanceChanged += UpdateCoinText;
            }

            // Tüm paneller kapalı başlar
            SetPanelHidden(_shapeShopPanel);
            SetPanelHidden(_upgradesPanel);
            SetPanelHidden(_settingsPanel);
            UpgradeRegistry.Instance?.Init(upgradeLibrary);
            // Buton bağlantıları
            if (_btnNewRun    != null) _btnNewRun.OnClicked    = OnNewRunClicked;
            if (_btnShapeShop != null) _btnShapeShop.OnClicked = OnShapeShopClicked;
            if (_btnUpgrades  != null) _btnUpgrades.OnClicked  = OnUpgradesClicked;
            if (_btnSettings  != null) _btnSettings.OnClicked  = OnSettingsClicked;
            if (_btnQuit      != null) _btnQuit.OnClicked      = OnQuitClicked;
        }

        private void OnDestroy()
        {
            if (CoinWallet.Instance != null)
                CoinWallet.Instance.OnBalanceChanged -= UpdateCoinText;
        }

        // ── Coin ─────────────────────────────────────────────────────────────

        private void UpdateCoinText(int balance)
        {
            if (_coinText != null)
                _coinText.text = balance.ToString("N0");
        }

        // ── Buton Handlers ───────────────────────────────────────────────────

        private void OnNewRunClicked()
        {
            Debug.LogWarning("Clicked");
            SceneTransition.Instance?.LoadScene(_gameplaySceneName);        }

        private void OnShapeShopClicked()
        {
            PanelManager.Instance?.OpenPanel(_shapeShopPanel);
        }

        private void OnUpgradesClicked()
        {
            PanelManager.Instance?.OpenPanel(_upgradesPanel);
        }

        private void OnSettingsClicked()
        {
            PanelManager.Instance?.OpenPanel(_settingsPanel);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── Panel Helper ─────────────────────────────────────────────────────

        /// <summary>Panel başlangıçta gizli ve devre dışı.</summary>
        private void SetPanelHidden(CanvasGroup panel)
        {
            if (panel == null) return;
            panel.alpha          = 0f;
            panel.interactable   = false;
            panel.blocksRaycasts = false;
            panel.gameObject.SetActive(false);
        }

        /// <summary>Geri butonu — herhangi bir panelden çağrılır.</summary>
        public void OnBackButtonClicked()
        {
            PanelManager.Instance?.CloseCurrentPanel();
        }
    }
}