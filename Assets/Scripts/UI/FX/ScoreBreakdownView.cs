using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.UI.FX
{
    /// <summary>Skor zincirinde yer alan tek bir çarpan.</summary>
    public readonly struct ScoreFactor
    {
        public readonly string Label;
        public readonly float  Value;

        public ScoreFactor(string label, float value)
        {
            Label = label;
            Value = value;
        }

        /// <summary>1'in altındaki çarpanlar ceza — UI bunları kırmızı gösterir.</summary>
        public bool IsPenalty => Value < 0.999f;
    }

    /// <summary>
    /// Skorun altında beliren çarpan zinciri: "x2.4 COMBO · x1.1 BONUS · x3 SLOW BURN".
    ///
    /// Bu, tek tek kartlara popup eklemenin alternatifi. Score Multiplier, Card
    /// Collector, Heavy Load, Diet Plan, Slow Burn, Hyperfocus gibi kartların
    /// ortak sorunu şu: hepsi her hamlede sessizce çarpıyor. Her biri için ayrı
    /// popup koysaydık ekran her yerleştirmede patlardı ve hiçbiri okunmazdı.
    /// Zincir ise tek bir yerde, skorun tam yanında, yalnızca o hamlede GERÇEKTEN
    /// devreye giren çarpanları gösteriyor — nötr olanlar hiç yazılmıyor.
    ///
    /// UI çalışma zamanında kuruluyor; ScoreView'a bileşen eklemek yeterli.
    /// </summary>
    public sealed class ScoreBreakdownView : MonoBehaviour
    {
        private const int   MaxChips   = 6;
        private const float ChipStagger = 0.055f;

        private RectTransform     _row;
        private readonly List<TMP_Text> _chips = new();
        private TMP_FontAsset     _font;
        private Sequence          _seq;

        private static readonly Color BonusColor   = new Color(1f,    0.84f, 0.35f);
        private static readonly Color PenaltyColor = new Color(1f,    0.45f, 0.42f);
        private static readonly Color SepColor     = new Color(0.60f, 0.66f, 0.78f);

        /// <summary>ScoreView tarafından çağrılır — zincirin nereye çizileceğini söyler.</summary>
        public void Setup(RectTransform anchorTo, TMP_FontAsset font, float yOffset)
        {
            _font = font;
            if (anchorTo == null || anchorTo.parent == null) return;

            var go = new GameObject("ScoreBreakdown", typeof(RectTransform));
            _row = (RectTransform)go.transform;
            _row.SetParent(anchorTo.parent, false);
            _row.anchorMin = anchorTo.anchorMin;
            _row.anchorMax = anchorTo.anchorMax;
            _row.pivot     = anchorTo.pivot;
            _row.sizeDelta = new Vector2(anchorTo.sizeDelta.x, 34f);
            _row.anchoredPosition = anchorTo.anchoredPosition + new Vector2(0f, yOffset);

            // Yatay akış: chip sayısı hamleden hamleye değişiyor, elle konumlamak
            // yerine layout'a bırakmak zincirin her zaman ortalı kalmasını sağlıyor.
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment       = TextAnchor.MiddleCenter;
            layout.spacing              = 10f;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth    = true;
            layout.childControlHeight   = true;

            var cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable   = false;

            for (int i = 0; i < MaxChips; i++)
            {
                var chipGo = new GameObject("Chip" + i, typeof(RectTransform));
                chipGo.transform.SetParent(_row, false);

                var t = chipGo.AddComponent<TextMeshProUGUI>();
                if (_font != null) t.font = _font;
                t.alignment        = TextAlignmentOptions.Center;
                t.fontSize         = 22f;
                t.fontStyle        = FontStyles.Bold;
                t.raycastTarget    = false;
                t.enableAutoSizing = false;
                t.enableWordWrapping = false;

                // ContentSizeFitter kasıtlı olarak yok: childControlWidth zaten
                // chip'in preferred width'ini kullanıyor, ikisi birlikte layout'u
                // her frame yeniden hesaplatıp uyarı üretiyordu.
                chipGo.SetActive(false);
                _chips.Add(t);
            }
        }

        /// <summary>
        /// Zinciri gösterir. Chip'ler soldan sağa sırayla düşer — hepsi aynı anda
        /// belirseydi göz nereye bakacağını bilemezdi; sıralı düşüş okuma yönünü
        /// dayatıyor ve zincirin bir HESAP olduğunu anlatıyor.
        /// </summary>
        public void Show(IReadOnlyList<ScoreFactor> factors)
        {
            if (_row == null || factors == null) return;

            _seq?.Kill();
            foreach (var c in _chips)
            {
                c.rectTransform.DOKill();
                c.DOKill();
                c.gameObject.SetActive(false);
            }

            int shown = 0;
            for (int i = 0; i < factors.Count && shown < _chips.Count; i++)
            {
                var f = factors[i];
                if (string.IsNullOrEmpty(f.Label)) continue;

                var chip = _chips[shown];
                chip.gameObject.SetActive(true);
                chip.text = $"<size=90%>x</size>{f.Value.ToString("0.##")} " +
                            $"<color=#{ColorUtility.ToHtmlStringRGB(SepColor)}>{f.Label}</color>";
                chip.color = f.IsPenalty ? PenaltyColor : BonusColor;
                shown++;
            }

            if (shown == 0) return;

            _seq = DOTween.Sequence();
            for (int i = 0; i < shown; i++)
            {
                var chip = _chips[i];
                var rt   = chip.rectTransform;

                chip.alpha        = 0f;
                rt.localScale     = Vector3.one * 0.55f;
                rt.localRotation  = Quaternion.identity;

                _seq.Insert(i * ChipStagger,
                    chip.DOFade(1f, 0.10f));
                _seq.Insert(i * ChipStagger,
                    rt.DOScale(1f, 0.26f).SetEase(Ease.OutBack));
            }

            float holdStart = shown * ChipStagger + 0.30f;
            for (int i = 0; i < shown; i++)
                _seq.Insert(holdStart + 0.75f, _chips[i].DOFade(0f, 0.30f));

            _seq.OnComplete(() =>
            {
                foreach (var c in _chips) c.gameObject.SetActive(false);
            });
        }

        private void OnDestroy()
        {
            _seq?.Kill();
            foreach (var c in _chips)
                if (c != null) { c.DOKill(); c.rectTransform.DOKill(); }
        }
    }
}
