using System;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Oyuncunun parası ilk kez bir kalıcı yükseltmeye (Upgrades paneli) yetince, bir
    /// kereliğine oraya yönlendirir. Oyuncuların çoğu ana menüdeki Upgrades bölümünü hiç
    /// fark etmiyordu.
    ///
    /// Koşul (<see cref="FindTarget"/>): hiç yükseltme alınmamış ve kilidi açık (aşaması
    /// geçilmiş) bir yükseltmenin ilk seviyesine para yetiyor. Daha önce bir yükseltme
    /// almış oyuncu bölümü zaten biliyor: rehber sessizce kapanır.
    ///
    /// İki giriş, ikisi de aynı tek seferlik bayrağı (<see cref="MetaGuide.UpgradesNudged"/>)
    /// kullanır, hangisi önce gelirse:
    /// • Game Over (<see cref="TryOfferAtGameOver"/>): "Upgrades'e Git" ana menüye döner,
    ///   menü Upgrades panelini açar. Hep "Yeni Run"a basan oyuncu menüyü hiç görmeyebilir.
    /// • Ana menü (<see cref="RunInMenu"/>): menü açılınca kart, "Upgrades'i Aç" paneli açar.
    ///
    /// Panele gitmek zorunludur: kartta tek buton var ("Sonra" yok, B bir şey yapmaz).
    /// Satın almak zorunlu değil: panel açılınca ışık deliği önce hedef yükseltmenin
    /// slotunda, seçilince Satın Al butonundadır ve "Atla" (ya da B) rehberi kapatır.
    /// </summary>
    public sealed class UpgradesNudge : MonoBehaviour
    {
        private const float GameOverDelay = 0.7f;
        private const float MenuDelay     = 0.9f;
        private const float PanelTimeout  = 4f;

        private enum Phase { Waiting, Offer, OpeningPanel, PickSlot, Buy }

        private static UpgradesNudge _active;
        public static bool IsActive => _active != null;

        private Phase  _phase;
        private float  _phaseAt;
        private float  _delay;
        private GuideOverlay _ui;
        private ShopConfirmDialog.Skin _skin;
        private Transform _canvasChild;
        private UpgradeLibrarySO _library;
        private UpgradeSO _target;

        // Game Over
        private Action _goToMenu;
        // Ana menü
        private CanvasGroup        _panel;
        private UpgradesController _controller;
        private Action             _openPanel;
        private Selectable         _pushed;

        // ── Koşul ────────────────────────────────────────────────────────────

        /// <summary>
        /// Rehberin göstereceği yükseltme: açık, hiç alınmamış ve parası yeten en ucuzu.
        /// Oyuncu daha önce herhangi bir yükseltme aldıysa <paramref name="ownsAny"/> true.
        /// </summary>
        public static UpgradeSO FindTarget(UpgradeLibrarySO library, out bool ownsAny)
        {
            ownsAny = false;
            var reg = UpgradeRegistry.Instance;
            if (library == null || reg == null || CoinWallet.Instance == null) return null;
            reg.Init(library);

            UpgradeSO best = null;
            int bestCost = int.MaxValue;
            foreach (var u in library.Upgrades)
            {
                if (u == null) continue;
                if (reg.GetLevel(u.Id) > 0) { ownsAny = true; return null; }
                if (!reg.CanUpgradeIgnoringCoins(u) || !reg.CanAffordUpgrade(u)) continue;
                int cost = reg.GetNextCost(u);
                if (cost < bestCost) { best = u; bestCost = cost; }
            }
            return best;
        }

        private static bool Blocked()
        {
            if (_active != null || MetaGuide.UpgradesNudged) return true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Game.Trailer.TrailerDirector.IsRunning) return true;
#endif
            return false;
        }

        /// <summary>Koşul tutuyor mu; yükseltme almış oyuncu için rehberi kalıcı kapatır.</summary>
        private static UpgradeSO Eligible(UpgradeLibrarySO library)
        {
            var target = FindTarget(library, out bool ownsAny);
            if (ownsAny) MetaGuide.MarkUpgradesNudged();
            return target;
        }

        // ── Game Over ────────────────────────────────────────────────────────

        /// <summary>
        /// Game Over açılırken çağrılır. Koşul tutuyorsa kısa bir gecikmeyle kart çıkar ve true döner.
        /// "Upgrades'e Git" <paramref name="goToMenu"/>'yü çağırır; menü paneli kendisi açar.
        /// </summary>
        public static bool TryOfferAtGameOver(Transform canvasChild, ShopConfirmDialog.Skin skin,
                                              UpgradeLibrarySO library, Action goToMenu)
        {
            if (GameMode.IsClassic || Blocked() || canvasChild == null || goToMenu == null) return false;
            var target = Eligible(library);
            if (target == null) return false;

            var nudge = Create(canvasChild, skin, library, GameOverDelay);
            nudge._target   = target;
            nudge._goToMenu = goToMenu;
            return true;
        }

        // ── Ana menü ─────────────────────────────────────────────────────────

        /// <summary>
        /// Ana menü açılınca çağrılır. Game Over'dan "Upgrades'e Git" ile gelindiyse paneli
        /// doğrudan açar; değilse koşul tutuyorsa önce kartla sorar.
        /// </summary>
        public static void RunInMenu(CanvasGroup upgradesPanel, Action openPanel)
        {
            bool fromGameOver = MetaGuide.OpenUpgradesOnMenu;
            MetaGuide.OpenUpgradesOnMenu = false;
            if (_active != null || upgradesPanel == null || openPanel == null) return;

            var controller = upgradesPanel.GetComponentInChildren<UpgradesController>(true);
            var shop = FindFirstObjectByType<ShapeShopController>(FindObjectsInactive.Include);
            if (controller == null) return;
            var skin = shop != null ? shop.DialogSkin : default;

            if (fromGameOver)
            {
                // Kart Game Over'da görüldü: burada yalnızca panel + ışık deliği
                var nudge = Create(upgradesPanel.transform, skin, controller.Library, 0.5f);
                nudge.BindMenu(upgradesPanel, controller, openPanel);
                nudge._skipOffer = true;
                return;
            }

            if (Blocked()) return;
            var target = Eligible(controller.Library);
            if (target == null) return;

            var n = Create(upgradesPanel.transform, skin, controller.Library, MenuDelay);
            n._target = target;
            n.BindMenu(upgradesPanel, controller, openPanel);
        }

        private bool _skipOffer;

        private void BindMenu(CanvasGroup panel, UpgradesController controller, Action openPanel)
        {
            _panel      = panel;
            _controller = controller;
            _openPanel  = openPanel;
            _controller.SlotSelected += OnSlotSelected;
            _controller.Bought       += OnBought;
        }

        // ── Yaşam döngüsü ────────────────────────────────────────────────────

        private static UpgradesNudge Create(Transform canvasChild, ShopConfirmDialog.Skin skin,
                                            UpgradeLibrarySO library, float delay)
        {
            var go = new GameObject("UpgradesNudge");
            _active = go.AddComponent<UpgradesNudge>();
            _active._canvasChild = canvasChild;
            _active._skin    = skin;
            _active._library = library;
            _active._delay   = delay;
            _active.Enter(Phase.Waiting);
            return _active;
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.SlotSelected -= OnSlotSelected;
                _controller.Bought       -= OnBought;
            }
            if (_pushed != null) UINav.Pop(_pushed.gameObject);
            if (_ui != null) Destroy(_ui.gameObject);
            if (_active == this) _active = null;
        }

        private void Update()
        {
            float t = Time.unscaledTime - _phaseAt;
            switch (_phase)
            {
                case Phase.Waiting:
                    if (t < _delay) break;
                    if (_controller == null) ShowGameOverOffer();
                    else if (_skipOffer || _panel.gameObject.activeInHierarchy) OpenPanel();
                    else ShowMenuOffer();
                    break;

                case Phase.OpeningPanel:
                    if (_panel.interactable && t >= 0.1f) BeginPickSlot();
                    else if (t >= PanelTimeout) Finish("panel açılmadı");
                    break;

                case Phase.PickSlot:
                case Phase.Buy:
                    // Panel kapandıysa (geri, başka panel) rehber de biter
                    if (!_panel.gameObject.activeInHierarchy || !_panel.interactable) { Finish(null); break; }
                    if (InputMode.UsingGamepad && _pushed != null && !UINav.IsTop(_pushed.gameObject))
                        UINav.Push(_pushed.gameObject, _pushed, () => Finish(null));
                    break;
            }
        }

        private void Enter(Phase phase)
        {
            _phase   = phase;
            _phaseAt = Time.unscaledTime;
        }

        private bool EnsureUi()
        {
            if (_ui != null) return true;
            _ui = GuideOverlay.Create(_canvasChild, _skin, "UpgradesNudge_UI");
            if (_ui == null) { Finish("canvas yok"); return false; }
            return true;
        }

        // ── Adımlar ──────────────────────────────────────────────────────────

        private void ShowGameOverOffer()
        {
            if (!EnsureUi()) return;
            _ui.SetTargets();
            _ui.ShowCard(
                Loc.Get("Guide.Upgrades.Title"),
                OfferBody(),
                0,
                "Guide.Upgrades.Go", () =>
                {
                    MetaGuide.MarkUpgradesNudged();
                    MetaGuide.OpenUpgradesOnMenu = true;
                    Debug.Log($"[UpgradesNudge] Game Over → Upgrades ({_target?.Id})");
                    var go = _goToMenu;
                    Finish(null);
                    go?.Invoke();
                });
            Enter(Phase.Offer);
        }

        private void ShowMenuOffer()
        {
            if (!EnsureUi()) return;
            _ui.SetTargets();
            _ui.ShowCard(
                Loc.Get("Guide.Upgrades.Title"),
                OfferBody(),
                0,
                "Guide.Upgrades.Open", OpenPanel);
            Enter(Phase.Offer);
        }

        private string OfferBody()
        {
            int coins = CoinWallet.Instance?.Balance ?? 0;
            string name = _target != null ? ContentLocalization.Name(_target) : string.Empty;
            return Loc.Get("Guide.Upgrades.Body", coins.ToString("N0"), name);
        }

        private void OpenPanel()
        {
            if (!EnsureUi()) return;
            // Panel açıldığı an yönlendirme yapılmış sayılır (butona basınca kaydedilir:
            // kart açıkken oyun kapanırsa bir dahaki sefer yine gelir)
            MetaGuide.MarkUpgradesNudged();
            _ui.HideCard();
            if (!_panel.gameObject.activeInHierarchy) _openPanel();
            Debug.Log("[UpgradesNudge] Upgrades paneli açılıyor");
            Enter(Phase.OpeningPanel);
        }

        private void BeginPickSlot()
        {
            // Game Over'dan gelindiyse hedef burada seçilir (bakiye aynı)
            if (_target == null) _target = FindTarget(_library, out _);
            var slot = _target != null ? _controller.FindSlot(_target) : null;
            if (slot == null || slot.Button == null) { Finish("hedef slot yok"); return; }

            _ui.SetTargets((RectTransform)slot.transform);
            _ui.ShowCard(
                Loc.Get("Guide.Upgrades.Pick.Title"),
                Loc.Get("Guide.Upgrades.Pick.Body", ContentLocalization.Name(_target)),
                0, null, null,
                "Guide.Skip", () => Finish(null));
            Focus(slot.Button);
            Enter(Phase.PickSlot);
        }

        private void OnSlotSelected(UpgradeSO upgrade)
        {
            if (_phase != Phase.PickSlot || upgrade != _target) return;
            var buy = _controller.BuyButton;
            if (buy == null) { Finish("satın al butonu yok"); return; }

            // Açıklama da ışıkta kalır: oyuncu neyi aldığını okuyabilsin (kart üste yerleşir)
            _ui.SetTargets((RectTransform)buy.transform,
                           _controller.BuyPriceText != null ? _controller.BuyPriceText.rectTransform : null,
                           _controller.DetailName   != null ? _controller.DetailName.rectTransform   : null,
                           _controller.DetailDesc   != null ? _controller.DetailDesc.rectTransform   : null);
            _ui.ShowCard(
                Loc.Get("Guide.Upgrades.Buy.Title"),
                Loc.Get("Guide.Upgrades.Buy.Body"),
                0, null, null,
                "Guide.Skip", () => Finish(null));
            Focus(buy);
            Enter(Phase.Buy);
        }

        private void OnBought(UpgradeSO upgrade)
        {
            if (_phase != Phase.Buy && _phase != Phase.PickSlot) return;
            Debug.Log($"[UpgradesNudge] Satın alındı: {upgrade?.Id}");
            Finish(null);
        }

        /// <summary>Gamepad: odak ışık deliğindeki kontrolde, B = Atla.</summary>
        private void Focus(Selectable target)
        {
            if (_pushed != null) UINav.Pop(_pushed.gameObject);
            _pushed = target;
            UINav.Push(target.gameObject, target, () => Finish(null));
        }

        private void Finish(string error)
        {
            if (error != null) Debug.LogWarning($"[UpgradesNudge] Yarıda bırakıldı: {error}");
            if (_pushed != null) { UINav.Pop(_pushed.gameObject); _pushed = null; }
            if (_ui != null) { var ui = _ui; _ui = null; ui.Close(); }
            Destroy(gameObject);
        }
    }
}
