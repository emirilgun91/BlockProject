namespace RogueBlockBlast.Core
{
    /// <summary>
    /// Hangi modda oynandığı. Ana menü oyun sahnesini yüklemeden önce ayarlar.
    /// Statik olduğu için sahne yeniden yüklense de (Game Over ▸ Yeni Run) mod korunur.
    ///
    /// Classic: milestone, kart, upgrade ve coin yok; döndürme kapalı, şekiller
    /// rastgele bir yönde gelir. Run yalnızca hiçbir parça sığmayınca biter.
    /// </summary>
    public static class GameMode
    {
        public static bool IsClassic { get; private set; }

        public static void SetClassic(bool classic) => IsClassic = classic;
    }
}
