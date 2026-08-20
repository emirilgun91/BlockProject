using RogueBlockBlast.Content;

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
            return Loc.GetOr($"Card.{Key(card.Id, card.name)}.Desc", card.Description);
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
