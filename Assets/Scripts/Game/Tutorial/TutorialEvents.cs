using System;

namespace RogueBlockBlast.Game.Tutorial
{
    /// <summary>
    /// Öğreticinin dinlediği oyun olayları. Statik bir hub olmasının sebebi
    /// RunController'ın öğreticiyi hiç tanımaması: RunController yalnızca
    /// "şu oldu" diye haber veriyor, dinleyen olmasa da hiçbir maliyeti yok.
    ///
    /// Öğretici kapalıyken (oyuncu daha önce bitirdiyse) hiçbir abone olmadığı
    /// için bu çağrılar boş event tetiklemesinden ibaret kalır.
    /// </summary>
    public static class TutorialEvents
    {
        public static event Action           OnRunStarted;
        public static event Action           OnRotated;
        public static event Action<int>      OnPiecePlaced;      // aynı hamlede temizlenen satır/sütun sayısı
        public static event Action<float>    OnComboChanged;     // temizlik sonrası çarpan
        public static event Action           OnMilestoneReached;
        public static event Action           OnCardPicked;
        public static event Action           OnDeadPool;

        public static void RunStarted()             => OnRunStarted?.Invoke();
        public static void Rotated()                => OnRotated?.Invoke();
        public static void PiecePlaced(int cleared) => OnPiecePlaced?.Invoke(cleared);
        public static void ComboChanged(float m)    => OnComboChanged?.Invoke(m);
        public static void MilestoneReached()       => OnMilestoneReached?.Invoke();
        public static void CardPicked()             => OnCardPicked?.Invoke();
        public static void DeadPool()               => OnDeadPool?.Invoke();
    }
}
