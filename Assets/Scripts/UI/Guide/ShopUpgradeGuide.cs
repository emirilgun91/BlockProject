using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// İlk Game Over'da bir kereliğine: oyuncuya hediye coin verir, Shape Shop'u açar ve
    /// bir şeklin puanını yükseltmesini ister. Yükseltme yapılana kadar mağaza kapanmaz
    /// (<see cref="ShapeShopOverlay.Close"/>), yalnızca ışık deliğindeki puan butonu
    /// tıklanır ve mağaza yalnızca o satın almayı kabul eder
    /// (<see cref="ShapeShopController.GuidedScoreShapeId"/>) — başka bir harcama hediyeyi
    /// bitirip oyuncuyu kilitleyemez.
    ///
    /// Akış: Hediye kartı → mağaza açılır → ışık deliği hedef kartın puan butonunda →
    /// satın alma → kısa kutlama → "Harika" kartı → serbest.
    ///
    /// Durum <see cref="MetaGuide"/>'da: hediye verildiği an kaydedilir; oyun yarıda
    /// kapanırsa bir sonraki Game Over'da hediyesiz devam eder (para azsa yalnızca eksiği
    /// tamamlanır). Oyuncu daha önce bir puan yükseltmesi yaptıysa rehber hiç çıkmaz.
    /// Bir şey ters giderse (hedef bulunamadı, mağaza açılmadı) oyuncu kilitli kalmaz:
    /// rehber kendini bitirir.
    ///
    /// Game Over sırasında <c>timeScale = 0</c>; her şey unscaled zamanla çalışır.
    /// </summary>
    public sealed class ShopUpgradeGuide : MonoBehaviour
    {
        public const int GiftAmount = 100;

        private const float StartDelay   = 0.7f;   // Game Over paneli önce görünsün
        private const float ShopTimeout  = 4f;     // mağaza açılıp hedef bulunamazsa vazgeç
        private const float CelebrateFor = 1.1f;   // satın alma efekti ışık deliğinde izlensin

        private static ShopUpgradeGuide _active;

        /// <summary>Rehber sürüyor (pause menüsü açılmaz).</summary>
        public static bool IsActive => _active != null;

        /// <summary>Yükseltme yapılana kadar mağaza kapanmaz.</summary>
        public static bool BlocksShopClose => _active != null && _active._phase < Phase.Celebrate;

        private enum Phase { Waiting, Gift, OpeningShop, Spotlight, Celebrate, Done }

        private ShapeShopOverlay   _overlay;
        private ShapeShopController _shop;
        private GuideOverlay       _ui;
        private Phase _phase;
        private float _phaseAt;
        private int   _gift;
        private ShapeShopCardView _target;
        private Button _targetButton;
        private string _boughtShapeId;

        // ── Başlatma ─────────────────────────────────────────────────────────

        /// <summary>
        /// Game Over açılırken çağrılır. Rehber bekliyorsa başlar ve true döner
        /// (aynı Game Over'da başka rehber gösterilmez).
        /// </summary>
        public static bool TryStart(ShapeShopOverlay overlay)
        {
            if (_active != null) return true;
            if (overlay == null || overlay.Shop == null || overlay.Panel == null) return false;
            if (GameMode.IsClassic || MetaGuide.ShopGuideDone || CoinWallet.Instance == null) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Game.Trailer.TrailerDirector.IsRunning) return false;
#endif
            // Mağazayı zaten bulmuş ve kullanmış oyuncuya öğretecek bir şey yok
            if (overlay.Shop.HasAnyScoreUpgrade())
            {
                MetaGuide.SetShopState(MetaGuide.ShopDone);
                return false;
            }
            // Yükseltilebilir şekil yoksa (beklenmez) zorunlu adım oyuncuyu kilitlerdi
            if (overlay.Shop.CheapestScoreUpgradeCost() < 0) return false;

            var go = new GameObject("ShopUpgradeGuide");
            go.transform.SetParent(overlay.transform, false);
            _active = go.AddComponent<ShopUpgradeGuide>();
            _active._overlay = overlay;
            _active._shop    = overlay.Shop;
            _active.Enter(Phase.Waiting);
            return true;
        }

        // ── Unity ────────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            if (_shop != null)
            {
                _shop.ScoreUpgradeBought -= OnScoreUpgradeBought;
                if (_target != null && _shop.GuidedScoreShapeId == _target.Shape.Id) _shop.GuidedScoreShapeId = null;
            }
            if (_targetButton != null) UINav.Pop(_targetButton.gameObject);
            if (_ui != null) Destroy(_ui.gameObject);
            if (_active == this) _active = null;
        }

        private void Update()
        {
            float t = Time.unscaledTime - _phaseAt;
            switch (_phase)
            {
                case Phase.Waiting:
                    if (t >= StartDelay) ShowGift();
                    break;

                case Phase.OpeningShop:
                    if (_overlay.IsReady && t >= 0.1f) BeginSpotlight();
                    else if (t >= ShopTimeout) Abort("mağaza açılmadı");
                    break;

                case Phase.Spotlight:
                    // Mağaza bir şekilde kapandıysa (sahne değişimi vb.) rehber yarıda kalır;
                    // durum "hediye verildi"de kalır, bir sonraki Game Over'da devam eder.
                    if (!_overlay.IsOpen || _target == null || _targetButton == null) { Abort("mağaza kapandı"); break; }
                    // Fade sırasında seçilemeyen buton, panel tıklanabilir olunca odağa girer
                    if (InputMode.UsingGamepad && !UINav.IsTop(_targetButton.gameObject))
                        UINav.Push(_targetButton.gameObject, _targetButton, NoOp);
                    break;

                case Phase.Celebrate:
                    if (t >= CelebrateFor) ShowDone();
                    break;
            }
        }

        private static void NoOp() { }

        private void Enter(Phase phase)
        {
            _phase   = phase;
            _phaseAt = Time.unscaledTime;
        }

        // ── Adımlar ──────────────────────────────────────────────────────────

        private void ShowGift()
        {
            _ui = GuideOverlay.Create(_overlay.Panel.transform, _shop.DialogSkin, "ShopUpgradeGuide_UI");
            if (_ui == null) { Abort("canvas yok"); return; }

            var wallet = CoinWallet.Instance;
            int cheapest = _shop.CheapestScoreUpgradeCost();
            if (MetaGuide.ShopState == MetaGuide.ShopPending)
            {
                // Hediye her zaman en az bir puan yükseltmesine yeter
                _gift = Mathf.Max(GiftAmount, cheapest - wallet.Balance);
            }
            else
            {
                // Yarıda kalmış rehber: hediye zaten verildi, yalnızca eksik tamamlanır
                _gift = Mathf.Max(0, cheapest - wallet.Balance);
            }
            if (_gift > 0) wallet.Grant(_gift);
            MetaGuide.SetShopState(MetaGuide.ShopGiftGiven);
            Debug.Log($"[ShopGuide] Başladı: hediye {_gift}, bakiye {wallet.Balance}, en ucuz yükseltme {cheapest}");

            _ui.SetTargets();
            _ui.ShowCard(
                Loc.Get("Guide.Shop.Gift.Title"),
                Loc.Get("Guide.Shop.Gift.Body"),
                _gift,
                "Guide.Shop.Gift.Button", OpenShop);
            Enter(Phase.Gift);
        }

        private void OpenShop()
        {
            if (_phase != Phase.Gift) return;
            // Karartma kalır (tıklamalar yutulur), mağaza altında açılır; ışık deliği hazır olunca gelir
            _ui.HideCard();
            _overlay.Open();
            Enter(Phase.OpeningShop);
        }

        private void BeginSpotlight()
        {
            _target = PickTarget();
            _targetButton = _target != null ? _target.ScoreUpgradeButton : null;
            if (_target == null || _targetButton == null) { Abort("hedef kart bulunamadı"); return; }

            _shop.GuidedScoreShapeId = _target.Shape.Id;
            _shop.ScoreUpgradeBought += OnScoreUpgradeBought;

            ScrollIntoView((RectTransform)_target.transform);

            var reg  = ShapeUpgradeRegistry.Instance;
            float now  = reg.GetTileValue(_target.Shape.Id, _target.Shape.BaseTileValue);
            float next = now + reg.GetNextScoreGain(_target.Shape.Id);
            string name = Loc.ToUpper(ContentLocalization.Name(_target.Shape));

            _ui.SetTargets(Rt(_targetButton), Rt(_target.ScoreValueText), Rt(_target.ScoreCostText));
            _ui.ShowCard(
                Loc.Get("Guide.Shop.Upgrade.Title"),
                Loc.Get("Guide.Shop.Upgrade.Body", name, $"{now:0}", $"{next:0}"),
                0, null, null);
            if (InputMode.UsingGamepad) UINav.Push(_targetButton.gameObject, _targetButton, NoOp);
            Debug.Log($"[ShopGuide] Hedef: {_target.Shape.Id} ({now:0} → {next:0})");
            Enter(Phase.Spotlight);
        }

        private void OnScoreUpgradeBought(string shapeId)
        {
            if (_phase != Phase.Spotlight) return;
            _boughtShapeId = shapeId;

            // Asıl amaç gerçekleşti: kayıt hemen, kutlama sonra
            MetaGuide.SetShopState(MetaGuide.ShopDone);
            _shop.GuidedScoreShapeId = null;
            _shop.ScoreUpgradeBought -= OnScoreUpgradeBought;
            UINav.Pop(_targetButton.gameObject);
            _ui.BlockHole = true;   // ikinci tık ikinci seviyeyi almasın
            Debug.Log($"[ShopGuide] Tamamlandı: {shapeId}");
            Enter(Phase.Celebrate);
        }

        private void ShowDone()
        {
            var shape = _target != null ? _target.Shape : null;
            float value = shape != null ? ShapeUpgradeRegistry.Instance.GetTileValue(shape.Id, shape.BaseTileValue) : 0f;
            string name = shape != null ? Loc.ToUpper(ContentLocalization.Name(shape)) : _boughtShapeId;

            _ui.SetTargets();
            _ui.ShowCard(
                Loc.Get("Guide.Shop.Done.Title"),
                Loc.Get("Guide.Shop.Done.Body", name, $"{value:0}"),
                0, "Guide.Shop.Done.Button", Finish);
            Enter(Phase.Done);
        }

        private void Finish()
        {
            if (_ui != null) { var ui = _ui; _ui = null; ui.Close(); }
            Destroy(gameObject);
        }

        /// <summary>Oyuncuyu kilitli bırakmamak için: rehber sessizce çekilir.</summary>
        private void Abort(string reason)
        {
            Debug.LogWarning($"[ShopGuide] Yarıda bırakıldı: {reason}");
            // Hedef hiç bulunamadıysa tekrar denemek aynı yere çıkar: bitti say.
            if (_phase == Phase.OpeningShop) MetaGuide.SetShopState(MetaGuide.ShopDone);
            Finish();
        }

        // ── Hedef ────────────────────────────────────────────────────────────

        /// <summary>
        /// Mağazadaki sırayla ilk kart: açık, puanı yükseltilebilir ve fiyatı bakiyeye yetiyor.
        /// Sıra ekrandaki sıra olduğu için hedef çoğunlukla zaten görünür alandadır.
        /// </summary>
        private ShapeShopCardView PickTarget()
        {
            var reg = ShapeUpgradeRegistry.Instance;
            int coins = CoinWallet.Instance?.Balance ?? 0;
            foreach (var card in _shop.Cards)
            {
                if (card == null || card.Shape == null || card.ScoreUpgradeButton == null) continue;
                var s = card.Shape;
                if (!s.IsUnlocked || !reg.CanUpgradeScore(s.Id)) continue;
                if (reg.GetScoreUpgradeCost(s.Id) > coins) continue;
                if (!card.ScoreUpgradeButton.gameObject.activeInHierarchy) continue;
                return card;
            }
            return null;
        }

        /// <summary>Kart kaydırma alanının dışındaysa içeriği kaydırıp görünür yapar.</summary>
        private static void ScrollIntoView(RectTransform item)
        {
            var scroll = item.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.content == null) return;
            var viewport = scroll.viewport != null ? scroll.viewport : (RectTransform)scroll.transform;

            Canvas.ForceUpdateCanvases();
            Rect view = viewport.rect;
            var corners = new Vector3[4];
            item.GetWorldCorners(corners);
            Vector2 min = viewport.InverseTransformPoint(corners[0]);
            Vector2 max = viewport.InverseTransformPoint(corners[2]);

            Vector2 shift = Vector2.zero;
            const float pad = 24f;
            if (scroll.vertical)
            {
                if (max.y > view.yMax - pad)      shift.y = -(max.y - (view.yMax - pad));
                else if (min.y < view.yMin + pad) shift.y = (view.yMin + pad) - min.y;
            }
            if (scroll.horizontal)
            {
                if (max.x > view.xMax - pad)      shift.x = -(max.x - (view.xMax - pad));
                else if (min.x < view.xMin + pad) shift.x = (view.xMin + pad) - min.x;
            }
            if (shift == Vector2.zero) return;

            // Viewport uzayındaki kayma içeriğin ebeveyn uzayına çevrilir
            Vector3 worldShift = viewport.TransformVector(shift);
            var parent = scroll.content.parent;
            Vector2 local = parent != null ? (Vector2)parent.InverseTransformVector(worldShift) : shift;
            scroll.StopMovement();
            scroll.content.anchoredPosition += local;
        }

        private static RectTransform Rt(Component c) => c != null ? (RectTransform)c.transform : null;
    }
}
