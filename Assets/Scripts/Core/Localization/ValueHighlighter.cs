using System.Text.RegularExpressions;

namespace RogueBlockBlast.Core.Localization
{
    /// <summary>
    /// Kart metinlerindeki sayısal değerleri (<c>+6</c>, <c>20%</c>, <c>2.5x</c>,
    /// <c>-0.10</c>, <c>×1.5</c>…) TMP rich text ile kalın ve renkli yapar.
    ///
    /// Neden çalışma anında: değerler 15 dilde çevrilmiş cümlelerin içinde düz
    /// sayı olarak duruyor. CSV'leri elle etiketlemek her çeviride tekrar
    /// yapılması gereken, kolay bozulan bir iş olurdu; bu biçimlendirici her dili
    /// ve ileride eklenecek her kartı kendiliğinden kapsar. Metin ekrana
    /// basılmadan hemen önce uygulanır — CSV'deki metin değişmez.
    ///
    /// Tanınan yazımlar (tüm dillerdeki gerçek metinlerden çıkarıldı):
    ///   ondalık      0.02 · 0,02 (Almanca, Fransızca, Rusça…)
    ///   yüzde        10% · %10 (Türkçe)
    ///   çarpan       2.5x · x2 · ×1.5 (Çince) · 1.5 倍 (Japonca) · 1.5배 (Korece)
    ///   işaret       +6 · -0.10 · −3
    ///   boyut        2x2
    ///
    /// Bilinçli olarak dokunulmayanlar:
    ///   • Harfe yapışık rakamlar — şekil adları <c>I3</c>, <c>4Gen</c>, <c>6Square</c>
    ///     değer değil, isim.
    ///   • Aralık içindeki tire — <c>2-3</c>'teki <c>-3</c> negatif değer sayılmaz.
    ///   • Mevcut rich text etiketlerinin içi — <c>&lt;size=11&gt;</c> gibi.
    ///
    /// Renk: açık eksi işareti (<c>-</c> / <c>−</c>) olan değerler ceza rengi,
    /// geri kalan her şey değer rengi. İşaretsiz bir sayının iyi mi kötü mü
    /// olduğu ("hedef %20 artar") cümleden güvenilir şekilde çıkarılamaz; yanlış
    /// renk göstermektense tek renk.
    /// </summary>
    public static class ValueHighlighter
    {
        /// <summary>Değer rengi — tooltip'teki canlı değer satırıyla aynı altın aile.</summary>
        public static string ValueColor   = "#FFC94A";
        /// <summary>Açık eksi işaretli (ceza) değer rengi.</summary>
        public static string PenaltyColor = "#FF6B5E";

        // Grup 1: mevcut rich text etiketi (aynen bırakılır)
        // Grup 2: değer
        //   (?<![\p{L}\p{N}.,])  harfe / rakama / ondalığa yapışık değilse başla —
        //                        I3, 4Gen içindeki rakamlar ve 2-3'teki -3 elenir
        //                        (rakamdan sonra gelen tire işaret değildir)
        //   (?![A-Za-z\p{N}])    Latin harfe yapışık değilse bitir — 6Square elenir;
        //                        CJK / Hangul eklerine izin var (2줄, 3回)
        private static readonly Regex Pattern = new Regex(
            @"(<[^>]*>)|(?<![\p{L}\p{N}.,])(" +
                @"\d+x\d+" +                                              // 2x2
                @"|[x×]\d+(?:[.,]\d+)?" +                                 // x2, ×1.5
                @"|[+\-−]?%?\d+(?:[.,]\d+)?(?:%|x|×|\s?倍|배)?" +          // +6, -0.10, 20%, %20, 2.5x, 1.5 倍
            @")(?![A-Za-z\p{N}])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Metindeki değerleri vurgular. Boş / null metin olduğu gibi döner.
        /// Her gösterimde HAM metinden başlayın — zaten vurgulanmış metne tekrar
        /// uygulamak sayıları iki kez sarar.
        /// </summary>
        public static string Highlight(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return Pattern.Replace(text, m =>
            {
                if (m.Groups[1].Success) return m.Value;   // rich text etiketi

                string v = m.Groups[2].Value;
                bool penalty = v[0] == '-' || v[0] == '−';
                string color = penalty ? PenaltyColor : ValueColor;
                return $"<b><color={color}>{v}</color></b>";
            });
        }
    }
}
