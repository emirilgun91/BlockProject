#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using UnityEngine;
using RogueBlockBlast.Content;
using RogueBlockBlast.Core;
using RogueBlockBlast.Core.Localization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RogueBlockBlast.Game
{
    /// <summary>
    /// Geliştirici paneli — F1 ile açılır/kapanır.
    ///
    /// İstediğin kartı anında oyuna ekleyip test edersin; rastgele kart beklemek yok.
    /// Kart uygulaması gerçek seçim yolunu (RunController.OnCardPicked) kullanır,
    /// yani panelden eklenen kart ile milestone'da seçilen kart aynı kodu çalıştırır.
    ///
    /// Sahneye elle eklemek gerekmez: oyun sahnesi açıldığında kendini oluşturur.
    /// Sadece editör ve development build'de derlenir — release'e sızmaz.
    /// </summary>
    public sealed class DebugPanel : MonoBehaviour
    {
        // Panel KAPALI. Açmak için DEBUG_PANEL define'ini ekle — ya da test
        // ederken aşağıdaki varsayılanı geçici olarak true yap.
        //
        // static readonly, const değil: const olsaydı derleyici aşağıdaki
        // "if (!Enabled) return;" satırını ölü kod sayıp CS0162 uyarısı verirdi.
#if DEBUG_PANEL
        private static readonly bool Enabled = true;
#else
        private static readonly bool Enabled = false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Enabled) return;
            if (FindFirstObjectByType<DebugPanel>() != null) return;
            var go = new GameObject("[DebugPanel]");
            go.AddComponent<DebugPanel>();
            DontDestroyOnLoad(go);
        }

        private bool   _open;
        private string _search = "";
        private Vector2 _scroll;
        private RunController _run;
        private GUIStyle _header, _small, _cardBtn;
        private float _lastRefresh;

        private const float PanelW = 470f;

        private void Update()
        {
            if (EscapeToggle()) _open = !_open;

            // RunController sahne değişince kaybolur — periyodik olarak tazele
            if (_run == null && Time.unscaledTime - _lastRefresh > 0.5f)
            {
                _lastRefresh = Time.unscaledTime;
                _run = FindFirstObjectByType<RunController>();
            }
        }

        private static bool EscapeToggle()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.F1);
#endif
        }

        private void EnsureStyles()
        {
            if (_header != null) return;

            _header = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.18f, 0.90f, 1f) }
            };
            _small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11, wordWrap = true,
                normal = { textColor = new Color(0.85f, 0.88f, 0.95f) }
            };
            _cardBtn = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11, alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4)
            };
        }

        private void OnGUI()
        {
            if (!_open)
            {
                EnsureStyles();
                GUI.Label(new Rect(10, 10, 220, 20), "F1 — debug panel", _small);
                return;
            }

            EnsureStyles();

            float h = Mathf.Min(Screen.height - 20f, 760f);
            GUILayout.BeginArea(new Rect(10, 10, PanelW, h), GUI.skin.box);

            GUILayout.Label("DEBUG PANEL   (F1 kapat)", _header);

            if (_run == null)
            {
                GUILayout.Label("RunController bulunamadı — oyun sahnesinde değilsin.", _small);
                GUILayout.EndArea();
                return;
            }

            // ── Durum ────────────────────────────────────────────────────────
            GUILayout.Label(_run.DebugStateSummary(), _small);
            GUILayout.Space(4);

            // ── Hızlı aksiyonlar ─────────────────────────────────────────────
            GUILayout.Label("Aksiyonlar", _header);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10 şekil"))  _run.DebugAddPieces(10);
            if (GUILayout.Button("-10 şekil"))  _run.DebugAddPieces(-10);
            if (GUILayout.Button("+5000 skor")) _run.DebugAddScore(5000);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Havuzu yenile")) _run.DebugRerollPool();
            if (GUILayout.Button("Tahtayı temizle")) _run.DebugClearBoard();
            if (GUILayout.Button("Tahtayı doldur")) _run.DebugFillBoardExceptOne();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1000 coin")) CoinWallet.Instance?.Earn(1000);
            if (GUILayout.Button("Game Over"))  _run.DebugForceGameOver();
            if (GUILayout.Button("Dili değiştir")) Loc.NextLanguage();
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            // ── Kart ekleme ──────────────────────────────────────────────────
            GUILayout.Label("Kart ekle", _header);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ara:", _small, GUILayout.Width(30));
            _search = GUILayout.TextField(_search ?? "");
            if (GUILayout.Button("X", GUILayout.Width(24))) _search = "";
            GUILayout.EndHorizontal();

            var pool = _run.DebugCardPool;
            if (pool == null || pool.Count == 0)
            {
                GUILayout.Label("RunController.CardPool boş.", _small);
                GUILayout.EndArea();
                return;
            }

            var filtered = pool.Where(c => c != null).Where(c =>
                string.IsNullOrEmpty(_search) ||
                (c.CardName ?? "").ToLowerInvariant().Contains(_search.ToLowerInvariant()) ||
                c.name.ToLowerInvariant().Contains(_search.ToLowerInvariant())).ToList();

            GUILayout.Label($"{filtered.Count} / {pool.Count} kart", _small);

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var card in filtered)
            {
                GUILayout.BeginHorizontal();

                var prev = GUI.color;
                GUI.color = RarityColor(card.Rarity);
                GUILayout.Label("■", GUILayout.Width(14));
                GUI.color = prev;

                string label = ContentLocalization.Name(card);
                if (card.IsShapeCard) label += "  [shape]";

                if (GUILayout.Button(label, _cardBtn))
                {
                    _run.DebugApplyCard(card);
                    Debug.Log($"[DebugPanel] kart eklendi: {card.name}");
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private static Color RarityColor(CardRarity r) => r switch
        {
            CardRarity.Common   => new Color(0.35f, 0.62f, 0.85f),
            CardRarity.Uncommon => new Color(0.20f, 0.80f, 0.62f),
            CardRarity.Rare     => new Color(0.55f, 0.50f, 0.95f),
            CardRarity.Epic     => new Color(0.75f, 0.40f, 0.90f),
            _                   => Color.gray,
        };
    }
}
#endif
