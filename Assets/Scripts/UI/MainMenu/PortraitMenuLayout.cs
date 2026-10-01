using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Ana menüyü ekran dikeyken (telefon) portrait düzene çevirir; yataya dönünce
    /// orijinal düzeni birebir geri yükler.
    ///
    /// Ana menü build'de ilk sahne olduğu için ayrı bir portrait menü sahnesi işe
    /// yaramazdı — telefon önce landscape menüyle açılırdı. Bu yüzden aynı sahne,
    /// çalışma anında yeniden dizilir. MainMenuCanvas'a eklemek kurulumun tamamı;
    /// objeler isimleriyle bulunur, bulunamayan atlanır.
    ///
    /// Portrait düzen (1080×1920 referans, genişliğe eşlenir):
    ///   üst     logo
    ///   orta    "Last Run" önizlemesi
    ///   alt     menü butonları (büyütülmüş dokunma hedefleri)
    ///   en alt  sosyal şerit (genişliğe sığdırılır)
    /// Modal pencereler (Yükseltmeler, Ayarlar) ekran genişliğine sığdırılır.
    ///
    /// Mobilde "Quit" butonu gizlenir — iOS uygulamanın kendini kapatmasına izin vermez.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PortraitMenuLayout : MonoBehaviour
    {
        [SerializeField] private Vector2 _portraitReference = new Vector2(1080f, 1920f);
        [Tooltip("Menü butonlarının portrait'teki ölçeği — parmakla rahat dokunulsun.")]
        [SerializeField] private float _menuScale = 1.35f;
        [SerializeField] private float _sideMargin = 40f;
        [Tooltip("Logo grubunun görsel merkezini ekran ortasına getiren yatay kaydırma (logo ölçeğinden önce).")]
        [SerializeField] private float _logoVisualCenterOffset = 194f;
        [Tooltip("'Last Run' önizlemesinin içeriğini ortalayan yatay kaydırma.")]
        [SerializeField] private float _previewCenterOffset = 162f;

        private CanvasScaler _scaler;
        private Vector2 _landscapeReference;
        private float   _landscapeMatch;
        private bool?   _portraitApplied;

        private struct RectState
        {
            public RectTransform Rt;
            public Vector2 AMin, AMax, Pivot, Pos, Size;
            public Vector3 Scale;
        }
        private readonly List<RectState> _saved = new List<RectState>();
        private GameObject _quitButton;
        private bool _quitWasActive;

        private void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            if (_scaler != null)
            {
                _landscapeReference = _scaler.referenceResolution;
                _landscapeMatch     = _scaler.matchWidthOrHeight;
            }
            var quit = transform.Find("LeftPanel/MenuList/BtnQuit");
            if (quit != null) { _quitButton = quit.gameObject; _quitWasActive = _quitButton.activeSelf; }
        }

        private void Update()
        {
            bool portrait = Screen.height > Screen.width;
            if (_portraitApplied == portrait) return;
            _portraitApplied = portrait;
            if (portrait) ApplyPortrait();
            else          RestoreLandscape();
        }

        // ── Portrait ─────────────────────────────────────────────────────────

        private void ApplyPortrait()
        {
            if (_saved.Count == 0) SaveAll();

            if (_scaler != null)
            {
                _scaler.referenceResolution = _portraitReference;
                // Genişliğe eşle: telefonlar 9:16 … 9:21 — fazla yükseklik boşluk olur, taşma olmaz.
                _scaler.matchWidthOrHeight = 0f;
            }

            float usable = _portraitReference.x - _sideMargin * 2f;

            // Paneller tam ekran; içerikleri kendi anchor'larıyla yerleşir
            Set("LeftPanel",  Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 1f);
            Set("RightPanel", new Vector2(0f, 0.5f), new Vector2(1f, 0.86f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-_sideMargin * 2f, 0f), 1f);

            // Logo: üstte ortalı. Logonun çocukları kökün merkezine göre sola tasarlanmış
            // (yazı −87, "DEMO" etiketleri −390…−462): görsel merkez ≈ −194 → kök o kadar
            // sağa kaydırılır (ölçekle çarpılarak).
            const float logoScale = 1.25f;
            Set("LeftPanel/LogoRoot", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f),
                new Vector2(_logoVisualCenterOffset * logoScale, -180f), new Vector2(usable, 110f), logoScale);

            // "Last Run" önizlemesi: içeriği landscape'te sol panelle dengelensin diye
            // −162 kaydırılmış; portrait'te tek sütunda ortalanır.
            var stage = Find("RightPanel/PreviewStage");
            if (stage != null) stage.anchoredPosition = new Vector2(_previewCenterOffset, 0f);

            // Menü: alt yarıda ortalı, büyütülmüş
            Set("LeftPanel/MenuList", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 250f), new Vector2(450f, 500f), _menuScale);

            // Sosyal şerit: en altta, genişliğe sığdırılır
            var social = Find("LeftPanel/SocialStrip");
            if (social != null)
            {
                float w = PreferredWidth(social);
                float s = w > usable ? usable / w : 1f;
                Set("LeftPanel/SocialStrip", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(0f, 60f), new Vector2(w, social.sizeDelta.y), s);
            }

            // Modal pencereler genişliğe sığsın
            FitWidth("UpgradePanel/Window", usable);
            FitWidth("SettingsPanel/Window", usable);

            if (_quitButton != null && Application.isMobilePlatform) _quitButton.SetActive(false);
        }

        private void RestoreLandscape()
        {
            if (_scaler != null)
            {
                _scaler.referenceResolution = _landscapeReference;
                _scaler.matchWidthOrHeight  = _landscapeMatch;
            }
            foreach (var s in _saved)
            {
                if (s.Rt == null) continue;
                s.Rt.anchorMin = s.AMin; s.Rt.anchorMax = s.AMax; s.Rt.pivot = s.Pivot;
                s.Rt.anchoredPosition = s.Pos; s.Rt.sizeDelta = s.Size; s.Rt.localScale = s.Scale;
            }
            if (_quitButton != null) _quitButton.SetActive(_quitWasActive);
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        private static readonly string[] Managed =
        {
            "LeftPanel", "RightPanel", "RightPanel/PreviewStage", "LeftPanel/LogoRoot", "LeftPanel/MenuList",
            "LeftPanel/SocialStrip", "UpgradePanel/Window", "SettingsPanel/Window",
        };

        private void SaveAll()
        {
            foreach (var path in Managed)
            {
                var rt = Find(path);
                if (rt == null) continue;
                _saved.Add(new RectState
                {
                    Rt = rt, AMin = rt.anchorMin, AMax = rt.anchorMax, Pivot = rt.pivot,
                    Pos = rt.anchoredPosition, Size = rt.sizeDelta, Scale = rt.localScale,
                });
            }
        }

        private RectTransform Find(string path) => transform.Find(path) as RectTransform;

        private void Set(string path, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size, float scale)
        {
            var rt = Find(path);
            if (rt == null) return;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;
        }

        private void FitWidth(string path, float maxWidth)
        {
            var rt = Find(path);
            if (rt == null) return;
            float w = rt.sizeDelta.x;
            rt.localScale = Vector3.one * (w > maxWidth ? maxWidth / w : 1f);
        }

        /// <summary>Yatay layout'un çocuklarının toplam genişliği (+ aralık + dolgu).</summary>
        private static float PreferredWidth(RectTransform strip)
        {
            var lg = strip.GetComponent<HorizontalLayoutGroup>();
            float sum = 0f; int n = 0;
            foreach (RectTransform c in strip)
            {
                if (!c.gameObject.activeSelf) continue;
                sum += c.sizeDelta.x; n++;
            }
            if (lg != null) sum += lg.spacing * Mathf.Max(0, n - 1) + lg.padding.left + lg.padding.right;
            return Mathf.Max(sum, 1f);
        }
    }
}
