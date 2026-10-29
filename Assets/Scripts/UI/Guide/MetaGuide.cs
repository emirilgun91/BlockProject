using RogueBlockBlast.Core.Save;
using UnityEngine;

namespace RogueBlockBlast.UI
{
    /// <summary>
    /// Meta döngüye yönlendiren iki tek seferlik rehberin kalıcı durumu (save.json).
    ///
    /// • <see cref="ShopUpgradeGuide"/>: ilk Game Over'da hediye coin + Shape Shop'ta zorunlu
    ///   bir puan yükseltmesi. 0 = bekliyor, 1 = hediye verildi (rehber yarıda kaldıysa bir
    ///   sonraki Game Over'da hediyesiz devam eder), 2 = bitti.
    /// • <see cref="UpgradesNudge"/>: parası ilk kalıcı yükseltmeye yetince bir kereliğine
    ///   Upgrades paneline götürür. 0 = bekliyor, 1 = gösterildi.
    ///
    /// <c>SaveDataService.ResetProgress</c> SaveStore'u boşalttığı için ikisi de sıfırlanır.
    /// </summary>
    public static class MetaGuide
    {
        private const string ShopKey     = "Guide_ShopUpgrade";
        private const string UpgradesKey = "Guide_UpgradesNudge";

        public const int ShopPending = 0, ShopGiftGiven = 1, ShopDone = 2;

        public static int  ShopState      => SaveStore.GetInt(ShopKey, ShopPending);
        public static bool ShopGuideDone  => ShopState >= ShopDone;
        public static bool UpgradesNudged => SaveStore.GetInt(UpgradesKey, 0) == 1;

        /// <summary>
        /// Game Over'dan "Upgrades'e Git" ile ana menüye dönülüyor: menü açılınca Upgrades
        /// paneli açılıp rehber oradan devam eder. Kalıcı değil — yalnızca sahne geçişi için.
        /// </summary>
        public static bool OpenUpgradesOnMenu;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => OpenUpgradesOnMenu = false;

        public static void SetShopState(int state)
        {
            SaveStore.SetInt(ShopKey, state);
            SaveStore.Save();
        }

        public static void MarkUpgradesNudged()
        {
            SaveStore.SetInt(UpgradesKey, 1);
            SaveStore.Save();
        }

        /// <summary>F1 debug paneli: iki rehber de baştan.</summary>
        public static void ResetAll()
        {
            SaveStore.DeleteKey(ShopKey);
            SaveStore.DeleteKey(UpgradesKey);
            SaveStore.Save();
            OpenUpgradesOnMenu = false;
        }
    }
}
