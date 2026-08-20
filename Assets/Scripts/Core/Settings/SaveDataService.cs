using UnityEngine;
using RogueBlockBlast.Content;

namespace RogueBlockBlast.Core.Settings
{
    /// <summary>
    /// Kayıt verisi yönetimi — ilerleme sıfırlama.
    ///
    /// PlayerPrefs anahtar listesini dışarıdan okuyamadığımız için sıfırlama
    /// "her şeyi sil, ayarları geri yaz" şeklinde yapılır. Böylece hangi sistem
    /// hangi anahtarı yazmış olursa olsun hiçbiri arkada kalmaz.
    /// </summary>
    public static class SaveDataService
    {
        /// <summary>Coin, upgrade, shape upgrade ve unlock ilerlemesini siler. Ayarlar ve dil korunur.</summary>
        public static void ResetProgress()
        {
            GameSettings.EnsureLoaded();     // ayarlar bellekte olsun — birazdan diski silecek

            PlayerPrefs.DeleteAll();
            PersistSettings();               // ayarları geri yaz
            PlayerPrefs.Save();

            // Bellekteki singleton'ları da temizle — yoksa eski değerleri göstermeye devam ederler
            CoinWallet.Instance?.Reset();
            UpgradeRegistry.Instance?.ClearRuntimeCache();
            UnlockRegistry.Instance?.ClearRuntimeCache();
            ShapeUpgradeRegistry.Instance?.ResetAll();

            Debug.Log("[SaveData] İlerleme sıfırlandı (ayarlar korundu).");
        }

        /// <summary>
        /// Bellekteki ayar değerlerini PlayerPrefs'e geri yazar.
        /// DeleteAll sonrası çağrılır.
        /// </summary>
        private static void PersistSettings()
        {
            PlayerPrefs.SetFloat(GameSettings.K_Master, GameSettings.MasterVolume);
            PlayerPrefs.SetFloat(GameSettings.K_Music,  GameSettings.MusicVolume);
            PlayerPrefs.SetFloat(GameSettings.K_Sfx,    GameSettings.SfxVolume);
            PlayerPrefs.SetInt(GameSettings.K_Muted,         GameSettings.Muted ? 1 : 0);
            PlayerPrefs.SetInt(GameSettings.K_MuteUnfocused, GameSettings.MuteWhenUnfocused ? 1 : 0);

            PlayerPrefs.SetInt(GameSettings.K_Fullscreen,    GameSettings.Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(GameSettings.K_ResolutionIdx, GameSettings.ResolutionIndex);
            PlayerPrefs.SetInt(GameSettings.K_VSync,         GameSettings.VSync);
            PlayerPrefs.SetInt(GameSettings.K_TargetFps,     GameSettings.TargetFrameRate);

            PlayerPrefs.SetFloat(GameSettings.K_ScreenShake,  GameSettings.ScreenShakeRaw);
            PlayerPrefs.SetFloat(GameSettings.K_VfxIntensity, GameSettings.VfxIntensityRaw);
            PlayerPrefs.SetInt(GameSettings.K_ReduceMotion,   GameSettings.ReduceMotion ? 1 : 0);
            PlayerPrefs.SetInt(GameSettings.K_ReduceFlashing, GameSettings.ReduceFlashing ? 1 : 0);

            PlayerPrefs.SetInt(GameSettings.K_GhostPreview,   GameSettings.GhostPreview ? 1 : 0);
            PlayerPrefs.SetInt(GameSettings.K_HighlightLines, GameSettings.HighlightClearingLines ? 1 : 0);
            PlayerPrefs.SetInt(GameSettings.K_ConfirmQuit,    GameSettings.ConfirmQuit ? 1 : 0);
            PlayerPrefs.SetInt(GameSettings.K_ShowFps,        GameSettings.ShowFps ? 1 : 0);
            PlayerPrefs.SetInt(GameSettings.K_GridLines,      GameSettings.GridLines ? 1 : 0);

            PlayerPrefs.SetInt(GameSettings.K_Haptics,       GameSettings.Haptics ? 1 : 0);
            PlayerPrefs.SetFloat(GameSettings.K_DragOffsetY, GameSettings.DragOffsetY);

            PlayerPrefs.SetInt(GameSettings.K_Colorblind, (int)GameSettings.Colorblind);
            PlayerPrefs.SetFloat(GameSettings.K_UiScale,  GameSettings.UiScale);
            PlayerPrefs.SetInt(GameSettings.K_LargeText,  GameSettings.LargeText ? 1 : 0);

            if (!string.IsNullOrEmpty(GameSettings.Language))
                PlayerPrefs.SetString(GameSettings.K_Language, GameSettings.Language);
        }
    }
}
