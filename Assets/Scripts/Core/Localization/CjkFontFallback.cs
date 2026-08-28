using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RogueBlockBlast.Core.Localization
{
    /// <summary>
    /// Seçilen dile göre CJK fallback fontlarının sırasını değiştirir.
    ///
    /// <b>Neden gerekli:</b> Japonca ve Çince'nin paylaştığı Han karakterlerinin
    /// bölgesel glyph varyantları farklıdır (直, 骨, 令 gibi karakterler iki
    /// dilde farklı çizilir). TMP'nin fallback zinciri ise <b>global ve
    /// ilk-eşleşen-kazanır</b>; dil başına ayrı zincir diye bir şey yok.
    /// Sabit bir sıra bırakılırsa dillerden biri her zaman yanlış varyantla
    /// render edilir — ana dili konuşanların hemen fark ettiği bir kusur.
    ///
    /// Çözüm: dil değiştiğinde zinciri yeniden dizmek. Seçilen dilin fontu
    /// CJK'lar arasında öne alınır; Inter her zaman en başta kalır çünkü
    /// Latin/Kiril boşluklarını o kapatıyor ve CJK fontlarından önce
    /// denenmeli.
    ///
    /// Font <b>yüklemez</b> — yalnızca <see cref="TMP_Settings"/>'in mevcut
    /// listesini yeniden sıralar. Assetler zaten o listede olduğu için build'e
    /// dahil edilirler ve Resources'a ihtiyaç kalmaz.
    /// </summary>
    public static class CjkFontFallback
    {
        /// <summary>Dil adı → o dilin font asset adında aranacak damga.</summary>
        private static readonly Dictionary<string, string> PreferredTag = new()
        {
            { "Japanese",            "JP" },
            { "Korean",              "KR" },
            { "Chinese (Simplified)", "SC" },
        };

        // Bir asset adının CJK olup olmadığını anlamak için.
        private static readonly string[] CjkTags = { "JP", "KR", "SC" };

        private static string _appliedFor;

        /// <summary>
        /// Zinciri verilen dile göre diz. Aynı dil için tekrar çağrılırsa
        /// hiçbir şey yapmaz — dil değişimi dışında maliyet sıfır.
        /// </summary>
        public static void Apply(string language)
        {
            if (string.IsNullOrEmpty(language) || language == _appliedFor) return;

            var list = TMP_Settings.fallbackFontAssets;
            if (list == null || list.Count == 0) return;

            _appliedFor = language;

            // CJK olmayan diller için sıralama zaten önemsiz: Latin ve Kiril
            // karakterleri Inter'de bulunuyor, CJK fontlarına hiç düşülmüyor.
            if (!PreferredTag.TryGetValue(language, out string tag)) return;

            var reordered = new List<TMP_FontAsset>(list.Count);
            TMP_FontAsset preferred = null;

            // 1. Inter ve diğer CJK olmayanlar sırasını korur.
            foreach (var f in list)
            {
                if (f == null) continue;
                if (!IsCjk(f)) reordered.Add(f);
            }

            // 2. Seçilen dilin fontu CJK'ların başına.
            foreach (var f in list)
            {
                if (f == null || !IsCjk(f)) continue;
                if (f.name.Contains(tag)) { preferred = f; break; }
            }
            if (preferred != null) reordered.Add(preferred);

            // 3. Kalan CJK fontları — bulunamayan karakterler için yedek kalsın.
            foreach (var f in list)
            {
                if (f == null || !IsCjk(f) || f == preferred) continue;
                reordered.Add(f);
            }

            list.Clear();
            list.AddRange(reordered);

            // Fallback değişince mevcut yazılar kendiliğinden yeniden
            // çizilmiyor; zaten dil değişiminde metinler yenileniyor ama
            // değişmeyenler eski glyph'lerde kalmasın diye zorluyoruz.
            ForceRefreshAllText();
        }

        /// <summary>Kaydedilmiş dil için ilk kurulum — <c>Loc.Init</c> çağırır.</summary>
        public static void Reset() => _appliedFor = null;

        // ── Private ──────────────────────────────────────────────────────────

        private static bool IsCjk(TMP_FontAsset font)
        {
            if (font == null || string.IsNullOrEmpty(font.name)) return false;
            if (!font.name.Contains("Noto")) return false;

            foreach (var t in CjkTags)
                if (font.name.Contains(t)) return true;

            return false;
        }

        private static void ForceRefreshAllText()
        {
            var texts = Object.FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var t in texts)
                if (t != null) t.SetAllDirty();
        }
    }
}
