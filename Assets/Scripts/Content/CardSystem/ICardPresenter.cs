using System;
using System.Collections.Generic;
using RogueBlockBlast.Content;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Kart seçiminin <b>sunum</b> tarafı.
    ///
    /// <see cref="CardSelectionUI"/> iki iş yapıyor: hangi kartların
    /// çıkabileceğine karar vermek (kilit filtresi, unique kontrolü, ağırlıklı
    /// seçim, reroll) ve onları ekranda göstermek. Bu arayüz ikincisini ayırır.
    ///
    /// Kart kuralları tek yerde kalsın diye havuz mantığı ayrılmadı —
    /// kopyalanırsa iki kaynak birbirinden kaçar. Sunucu yalnızca hazır listeyi
    /// alır ve seçimi geri bildirir.
    ///
    /// <see cref="CardSelectionUI.ExternalPresenter"/> atanmadığında sistem
    /// bugünkü UGUI paneliyle çalışmaya devam eder.
    /// </summary>
    public interface ICardPresenter
    {
        /// <summary>
        /// Seçilecek kartları gösterir.
        /// </summary>
        /// <param name="cards">Gösterilecek kartlar — havuz mantığı çoktan uygulanmış.</param>
        /// <param name="showNewBadge">İlk kart yeni açılmış bir kartsa true.</param>
        /// <param name="onPicked">Oyuncu bir kart seçince çağrılır.</param>
        /// <param name="rerollsRemaining">Kalan reroll hakkı; 0 ise buton gizlenir.</param>
        /// <param name="onReroll">Reroll istendiğinde çağrılır — yeni bir Present tetikler.</param>
        void Present(
            IReadOnlyList<CardSO> cards,
            bool                  showNewBadge,
            Action<CardSO>        onPicked,
            int                   rerollsRemaining,
            Action                onReroll);

        /// <summary>
        /// Seçim kapanırken çağrılır. <paramref name="picked"/> null olabilir
        /// (skip). Kapanış animasyonu <b>oyunu bloklamaz</b> — oyun mantığı
        /// bu çağrıdan hemen sonra devam eder, animasyon kozmetiktir.
        /// </summary>
        void Dismiss(CardSO picked);
    }
}
