using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string _gameplaySceneName = "SampleScene";
        [Tooltip("Ekran dikeyken (telefon) açılacak oyun sahnesi. Build Settings'te olmalı.")]
        [SerializeField] private string _portraitGameplaySceneName = "GamePortrait";

        [Header("Coin Display")]
        [SerializeField] private TMP_Text _coinText;

        [Header("Buttons")]
        [SerializeField] private MenuButtonView _btnNewRun;
        [Tooltip("Classic Mode. Boşsa New Run butonu çalışma anında kopyalanıp altına konur.")]
        [SerializeField] private MenuButtonView _btnClassic;
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
            if (_btnClassic == null && _btnNewRun != null) _btnClassic = CreateClassicButton();
            if (_btnNewRun    != null) _btnNewRun.OnClicked    = OnNewRunClicked;
            if (_btnClassic   != null) _btnClassic.OnClicked   = OnClassicClicked;
            if (_btnShapeShop != null) _btnShapeShop.OnClicked = OnShapeShopClicked;
            if (_btnUpgrades  != null) _btnUpgrades.OnClicked  = OnUpgradesClicked;
            if (_btnSettings  != null) _btnSettings.OnClicked  = OnSettingsClicked;
            if (_btnQuit      != null) _btnQuit.OnClicked      = OnQuitClicked;

            // Gamepad: menünün tamamı taban kapsam, ilk seçim New Run. Paneller
            // (Shape Shop / Upgrades / Settings) açılınca kendi kapsamlarını üstüne koyar.
            if (_btnNewRun != null)
            {
                var canvas = _btnNewRun.GetComponentInParent<Canvas>();
                var root   = canvas != null ? canvas.rootCanvas.gameObject : gameObject;
                UINav.Push(root, _btnNewRun.GetComponent<UnityEngine.UI.Selectable>());
            }
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
            GameMode.SetClassic(false);
            SceneTransition.Instance?.LoadScene(ResolveGameplayScene());
        }

        private void OnClassicClicked()
        {
            GameMode.SetClassic(true);
            SceneTransition.Instance?.LoadScene(ResolveGameplayScene());
        }

        /// <summary>
        /// New Run'ın kopyası, hemen altına. Sahneye ayrı bir buton koymak yerine
        /// kopyalanıyor: görünüm (skin, ikon, hover) New Run ile hep aynı kalır ve
        /// ScreenStyleBuilder / PortraitMenuLayout'un bilmediği bir nesne oluşmaz.
        /// </summary>
        private MenuButtonView CreateClassicButton()
        {
            var src = _btnNewRun.transform;
            var go  = Instantiate(src.gameObject, src.parent, false);
            go.name = "BtnClassic";
            go.transform.SetSiblingIndex(src.GetSiblingIndex() + 1);

            var label = go.GetComponentInChildren<Assets.SimpleLocalization.Scripts.TextMeshProGUILocalized>(true);
            if (label != null) label.ChangeText("MainMenu.Classic");

            FitMenuList(src.parent as RectTransform);
            return go.GetComponent<MenuButtonView>();
        }

        /// <summary>
        /// Liste bir buton uzadı: içeriğe göre büyür. <paramref name="keepTop"/> açıkken
        /// üst kenar yerinde kalır (landscape: liste logonun altından aşağı akar);
        /// kapalıyken pivot tarafı (portrait: alttan yukarı).
        /// </summary>
        public static void FitMenuList(RectTransform list, bool keepTop = true)
        {
            if (list == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
            float extra = LayoutUtility.GetPreferredHeight(list) - list.rect.height;
            if (extra <= 0f) return;
            list.sizeDelta += new Vector2(0f, extra);
            if (keepTop) list.anchoredPosition -= new Vector2(0f, extra * (1f - list.pivot.y));
        }

        /// <summary>
        /// Ekran dikeyse (telefon) portrait oyun sahnesi, değilse normal sahne.
        /// Portrait sahne Build Settings'te yoksa normal sahneye düşülür.
        /// </summary>
        private string ResolveGameplayScene()
        {
            bool portrait = Screen.height > Screen.width;
            if (portrait && !string.IsNullOrEmpty(_portraitGameplaySceneName) &&
                Application.CanStreamedLevelBeLoaded(_portraitGameplaySceneName))
                return _portraitGameplaySceneName;
            return _gameplaySceneName;
        }

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