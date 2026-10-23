using System.Text;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Çeviri metinlerindeki tuş yer tutucularını bağlı gamepad'in tuş adlarıyla
    /// değiştirir: <c>{A} {B} {X} {Y} {LB} {RB} {LT} {RT}</c>.
    ///
    /// Yer tutucular Xbox adlarıyla yazılır ama konumu anlatır (A = alt yüz tuşu,
    /// LB = sol omuz). PlayStation'da {A} "×", Steam Deck'te {LB} "L1" olur —
    /// çevirmen tek bir metin yazar, her gamepad doğru adı görür.
    /// </summary>
    public static class PadText
    {
        private static readonly (string token, PadButton button)[] Tokens =
        {
            ("{A}",  PadButton.South),
            ("{B}",  PadButton.East),
            ("{X}",  PadButton.West),
            ("{Y}",  PadButton.North),
            ("{LB}", PadButton.LeftShoulder),
            ("{RB}", PadButton.RightShoulder),
            ("{LT}", PadButton.LeftTrigger),
            ("{RT}", PadButton.RightTrigger),
        };

        public static string Format(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder(text);
            foreach (var (token, button) in Tokens)
                sb.Replace(token, InputMode.GlyphRich(button));
            return sb.ToString();
        }
    }
}
