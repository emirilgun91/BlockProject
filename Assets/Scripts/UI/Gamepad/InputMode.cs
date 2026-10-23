using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;

namespace RogueBlockBlast.UI
{
    /// <summary>Ekranda adı yazılan gamepad tuşları.</summary>
    public enum PadButton
    {
        South, East, West, North,
        LeftShoulder, RightShoulder,
        LeftTrigger, RightTrigger,
        Start, Select
    }

    /// <summary>
    /// Son kullanılan giriş cihazı: klavye/fare mi, gamepad mi.
    ///
    /// İpuçları (döndürme çipi, öğretici metinleri) ve imleç buna göre değişir:
    /// gamepad'e dokunulduğu an tuş adları gamepad'inkine döner ve fare imleci
    /// gizlenir; fare kıpırdayınca ya da bir tuşa basılınca geri gelir.
    ///
    /// Steam Deck'te oyun gamepad modunda açılır (Steam <c>SteamDeck=1</c>
    /// ortam değişkenini koyar). Trackpad fare gibi çalıştığı için onu kullanan
    /// oyuncu klavye/fare moduna geçer — ipuçları kullandığı girişi izler.
    ///
    /// Sahne kurulumu gerekmez: ilk sahneden önce kendini kurar ve
    /// <see cref="UINav"/> ile aynı kalıcı objede yaşar.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class InputMode : MonoBehaviour
    {
        /// <summary>Son giriş gamepad'den mi geldi?</summary>
        public static bool UsingGamepad { get; private set; }

        /// <summary>Cihaz değişince — ipuçları metinlerini yeniler.</summary>
        public static event Action Changed;

        /// <summary>Steam Deck'te mi çalışıyoruz (Steam'in koyduğu ortam değişkeni).</summary>
        public static bool OnSteamDeck { get; private set; }

        // Fare, bu kadar pikselden az kıpırdarsa cihaz değişmiş sayılmaz —
        // masada duran farenin titremesi gamepad oyuncusunun ipuçlarını bozmasın.
        private const float MouseMoveThreshold = 6f;

        private static InputMode _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            UsingGamepad = false;
            Changed      = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("[Input Mode]");
            DontDestroyOnLoad(go);
            go.AddComponent<InputMode>();
            go.AddComponent<UINav>();
        }

        private void Awake()
        {
            _instance = this;
            OnSteamDeck = Environment.GetEnvironmentVariable("SteamDeck") == "1";

            // Deck'te ya da Big Picture'da (SteamTenfoot) oyuncu elinde gamepad'le
            // başlar: ilk ekranda klavye tuşu görmemeli.
            bool tenFoot = Environment.GetEnvironmentVariable("SteamTenfoot") == "1";
            if ((OnSteamDeck || tenFoot) && Gamepad.all.Count > 0) Set(true);
        }

        private void Update()
        {
            if (!UsingGamepad && AnyGamepadActivity()) Set(true);
            else if (UsingGamepad && AnyPointerOrKeyActivity()) Set(false);
        }

        private void OnApplicationFocus(bool focus)
        {
            // Odak geri gelince imleç durumu yeniden uygulansın (Alt-Tab imleci açar).
            if (focus) ApplyCursor();
        }

        private static void Set(bool gamepad)
        {
            if (UsingGamepad == gamepad) return;
            UsingGamepad = gamepad;
            ApplyCursor();
            Changed?.Invoke();
        }

        private static void ApplyCursor()
        {
            if (Application.isMobilePlatform) return;
            Cursor.visible = !UsingGamepad;
        }

        // ── Algılama ─────────────────────────────────────────────────────────

        private static bool AnyGamepadActivity()
        {
            var all = Gamepad.all;
            for (int i = 0; i < all.Count; i++)
            {
                var g = all[i];
                if (!g.wasUpdatedThisFrame) continue;

                // Çubuk yönleri ve tetikler de ButtonControl: eşiği geçen her kontrol sayılır,
                // dinlenen çubuğun küçük kayması sayılmaz.
                var controls = g.allControls;
                for (int c = 0; c < controls.Count; c++)
                    if (controls[c] is ButtonControl b && b.wasPressedThisFrame) return true;
            }
            return false;
        }

        private static bool AnyPointerOrKeyActivity()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

            var m = Mouse.current;
            if (m != null)
            {
                if (m.leftButton.wasPressedThisFrame || m.rightButton.wasPressedThisFrame ||
                    m.middleButton.wasPressedThisFrame) return true;
                if (m.scroll.ReadValue().sqrMagnitude > 0.01f) return true;
                if (m.delta.ReadValue().sqrMagnitude > MouseMoveThreshold * MouseMoveThreshold) return true;
            }

            var touch = Touchscreen.current;
            return touch != null && touch.primaryTouch.press.wasPressedThisFrame;
        }

        // ── Tuş adları ───────────────────────────────────────────────────────

        private enum Family { Xbox, PlayStation, SteamDeck }

        private static Family CurrentFamily()
        {
            if (Gamepad.current is DualShockGamepad) return Family.PlayStation;
            return OnSteamDeck ? Family.SteamDeck : Family.Xbox;
        }

        /// <summary>
        /// Tuşun bağlı gamepad'deki adı: Xbox / Steam Deck / PlayStation.
        /// Steam Input açıkken Deck ve çoğu gamepad Xbox gibi görünür; Deck'in
        /// omuz tuşları ise üstünde yazan adla (L1/R1) gösterilir.
        /// </summary>
        public static string Glyph(PadButton button)
        {
            var family = CurrentFamily();
            switch (button)
            {
                case PadButton.South: return family == Family.PlayStation ? "×" : "A";
                case PadButton.East:  return family == Family.PlayStation ? "○" : "B";
                case PadButton.West:  return family == Family.PlayStation ? "□" : "X";
                case PadButton.North: return family == Family.PlayStation ? "△" : "Y";

                case PadButton.LeftShoulder:  return family == Family.Xbox ? "LB" : "L1";
                case PadButton.RightShoulder: return family == Family.Xbox ? "RB" : "R1";
                case PadButton.LeftTrigger:   return family == Family.Xbox ? "LT" : "L2";
                case PadButton.RightTrigger:  return family == Family.Xbox ? "RT" : "R2";

                case PadButton.Start:  return family == Family.PlayStation ? "Options" : "Menu";
                case PadButton.Select: return family == Family.PlayStation ? "Share"   : "View";
            }
            return "?";
        }

        /// <summary>
        /// Metin içinde kullanılacak, kalın ve yüz tuşlarında gamepad'in kendi
        /// rengiyle boyanmış tuş adı (TMP rich text).
        /// </summary>
        public static string GlyphRich(PadButton button)
        {
            string glyph = Glyph(button);
            string color = null;
            if (CurrentFamily() == Family.PlayStation)
            {
                switch (button)
                {
                    case PadButton.South: color = "#8FB4FF"; break;
                    case PadButton.East:  color = "#FF7A7A"; break;
                    case PadButton.West:  color = "#F49BD8"; break;
                    case PadButton.North: color = "#5FE0C0"; break;
                }
            }
            else
            {
                switch (button)
                {
                    case PadButton.South: color = "#6BD66B"; break;
                    case PadButton.East:  color = "#FF6B6B"; break;
                    case PadButton.West:  color = "#5EA8FF"; break;
                    case PadButton.North: color = "#FFD34D"; break;
                }
            }
            return color != null ? $"<b><color={color}>{glyph}</color></b>" : $"<b>{glyph}</b>";
        }

        /// <summary>Tuşun bu karede basılıp basılmadığı — gamepad yoksa false.</summary>
        public static bool Pressed(PadButton button)
        {
            var g = Gamepad.current;
            if (g == null) return false;
            return Control(g, button).wasPressedThisFrame;
        }

        private static ButtonControl Control(Gamepad g, PadButton button)
        {
            switch (button)
            {
                case PadButton.South:         return g.buttonSouth;
                case PadButton.East:          return g.buttonEast;
                case PadButton.West:          return g.buttonWest;
                case PadButton.North:         return g.buttonNorth;
                case PadButton.LeftShoulder:  return g.leftShoulder;
                case PadButton.RightShoulder: return g.rightShoulder;
                case PadButton.LeftTrigger:   return g.leftTrigger;
                case PadButton.RightTrigger:  return g.rightTrigger;
                case PadButton.Start:         return g.startButton;
                default:                      return g.selectButton;
            }
        }

        // ── Yön (tekrarlı) ───────────────────────────────────────────────────

        /// <summary>
        /// Sol çubuk + d-pad'den basılı tutunca tekrarlayan adım yönü. Tutan
        /// taraf kendi <see cref="RepeatState"/>'ini taşır — tahta imleci ve
        /// kart yelpazesi aynı anda farklı tekrar durumunda olabilir.
        /// </summary>
        public sealed class RepeatState
        {
            private Vector2Int _held;
            private float      _nextAt;

            public float FirstDelay = 0.30f;
            public float Interval   = 0.085f;

            /// <summary>Bu karede atılacak adım (yoksa sıfır). Zamanı ölçeksiz — timeScale 0'da da çalışır.</summary>
            public Vector2Int Step()
            {
                var dir = ReadDirection();
                if (dir == Vector2Int.zero) { _held = Vector2Int.zero; return Vector2Int.zero; }

                float now = Time.unscaledTime;
                if (dir != _held)
                {
                    _held   = dir;
                    _nextAt = now + FirstDelay;
                    return dir;
                }
                if (now < _nextAt) return Vector2Int.zero;
                _nextAt = now + Interval;
                return dir;
            }

            public void Reset() => _held = Vector2Int.zero;
        }

        private static Vector2Int ReadDirection()
        {
            var g = Gamepad.current;
            if (g == null) return Vector2Int.zero;

            Vector2 v = g.dpad.ReadValue();
            if (v == Vector2.zero) v = g.leftStick.ReadValue();
            if (v.sqrMagnitude < 0.5f * 0.5f) return Vector2Int.zero;

            // Dört yön: baskın eksen. Çaprazlar tahtada hedefi kaçırtıyordu.
            return Mathf.Abs(v.x) >= Mathf.Abs(v.y)
                ? new Vector2Int((int)Mathf.Sign(v.x), 0)
                : new Vector2Int(0, (int)Mathf.Sign(v.y));
        }
    }
}
