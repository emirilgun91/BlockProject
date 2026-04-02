namespace RogueBlockBlast.Game
{
    /// <summary>
    /// Oyunun input alıp almayacağını kontrol eder.
    /// UI ekranları açılınca LockInput(), kapanınca UnlockInput() çağrılır.
    ///
    /// RunController.Update() başında şunu kontrol eder:
    ///   if (!GameStateController.InputAllowed) return;
    /// </summary>
    public static class GameStateController
    {
        private static int _lockCount = 0;

        /// <summary>Oyuncu input'u aktif mi?</summary>
        public static bool InputAllowed => _lockCount <= 0;

        /// <summary>Input'u kilitler. Birden fazla UI açık olabilir.</summary>
        public static void LockInput()   => _lockCount++;

        /// <summary>Input kilidini açar.</summary>
        public static void UnlockInput()
        {
            _lockCount--;
            if (_lockCount < 0) _lockCount = 0;
        }

        /// <summary>Run başında tamamen sıfırla.</summary>
        public static void Reset() => _lockCount = 0;
    }
}