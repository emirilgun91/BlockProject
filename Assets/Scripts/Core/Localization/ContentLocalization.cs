using RogueBlockBlast.Content;
using UnityEngine;

namespace RogueBlockBlast.Core.Localization
{
    /// <summary>
    /// ScriptableObject içeriklerinin (kart / upgrade / shape) çevirisi.
    ///
    /// Asset'lere dokunmadan çalışır: anahtar CSV'de varsa çeviri, yoksa asset'teki
    /// mevcut metin kullanılır. Yani çeviriyi sırayla ekleyebilirsin, hiçbir aşamada
    /// oyun boş metin göstermez.
    ///
    /// Anahtar şeması:
    ///   Card.&lt;id&gt;.Name       Card.&lt;id&gt;.Desc
    ///   upgrade.&lt;id&gt;.name    upgrade.&lt;id&gt;.desc
    ///   shape.&lt;id&gt;.name
    ///
    /// UpgradeSO'da NameKey / DescriptionKey doluysa şema yerine onlar kullanılır.
    /// </summary>
    public static class ContentLocalization
    {
        // ── Card ─────────────────────────────────────────────────────────────
        public static string Name(CardSO card)
        {
            if (card == null) return string.Empty;
            return Loc.GetOr($"Card.{Key(card.Id, card.name)}.Name", card.CardName);
        }

        public static string Description(CardSO card)
        {
            if (card == null) return string.Empty;
            string text = Loc.GetOr($"Card.{Key(card.Id, card.name)}.Desc", card.Description);
            return card.IsShapeCard ? FormatShapeEffects(card, text) : text;
        }

        /// <summary>
        /// Şekil kartı metnindeki sayılar veriden gelir: {0} = puan bonusu, {1} = ağırlık
        /// değişiminin büyüklüğü (CardSO.ShapeEffects). Eskiden CSV'de sabit yazılıydı ve
        /// asset'teki değer değişince kart eski sayıyı göstermeye devam ediyordu.
        /// </summary>
        private static string FormatShapeEffects(CardSO card, string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0 || card.ShapeEffects == null) return text;

            float score = 0f, weight = 0f;
            foreach (var e in card.ShapeEffects)
            {
                if (e == null) continue;
                if (e.EffectType == ShapeEffectType.ScoreBoost)   score  += e.Value;
                if (e.EffectType == ShapeEffectType.WeightChange) weight += e.Value;
            }
            try
            {
                return string.Format(text, score.ToString("0.##"), Mathf.Abs(weight).ToString("0.##"));
            }
            catch (System.FormatException)
            {
                Debug.LogWarning($"[ContentLocalization] {card.Id} açıklama formatı hatalı: \"{text}\"");
                return text;
            }
        }

        /// <summary>
        /// Kartın alt kısmındaki efekt satırı.
        ///
        /// Anahtar şeması <c>Card.&lt;id&gt;.Effect&lt;sıra&gt;</c> — sıra 1'den başlar.
        /// Sıra tabanlı olmasının bedeli, bir kartın efektleri yeniden
        /// sıralanırsa metinlerin kayması; karşılığında asset'lere alan
        /// eklemeden çalışıyor. Kartların çoğunda tek efekt var.
        ///
        /// Anahtar yoksa asset'teki metin döner — çeviri sırayla eklenebilir,
        /// hiçbir aşamada boş satır görünmez.
        /// </summary>
        public static string EffectDescription(CardSO card, int index, string fallback)
        {
            if (card == null) return fallback;
            return Loc.GetOr($"Card.{Key(card.Id, card.name)}.Effect{index}", fallback);
        }

        // ── Upgrade ──────────────────────────────────────────────────────────
        public static string Name(UpgradeSO upgrade)
        {
            if (upgrade == null) return string.Empty;
            string key = !string.IsNullOrEmpty(upgrade.NameKey)
                ? upgrade.NameKey
                : $"Upgrade.{Key(upgrade.Id, upgrade.name)}.Name";
            return Loc.GetOr(key, upgrade.name);
        }

        public static string Description(UpgradeSO upgrade)
        {
            if (upgrade == null) return string.Empty;
            string key = !string.IsNullOrEmpty(upgrade.DescriptionKey)
                ? upgrade.DescriptionKey
                : $"Upgrade.{Key(upgrade.Id, upgrade.name)}.Desc";
            return Loc.GetOr(key, string.Empty);
        }

        // ── Shape ────────────────────────────────────────────────────────────
        public static string Name(ShapeSO shape)
        {
            if (shape == null) return string.Empty;
            return Loc.GetOr($"Shape.{Key(shape.Id, shape.name)}.Name", shape.name);
        }

        private static string Key(string id, string assetName)
            => string.IsNullOrWhiteSpace(id) ? assetName : id;
    }
}
