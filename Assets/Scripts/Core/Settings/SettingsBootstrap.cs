using UnityEngine;
using RogueBlockBlast.Core.Localization;

namespace RogueBlockBlast.Core.Settings
{
    /// <summary>
    /// Oyun açılışında ayarları ve dili yükler; hiçbir sahneye elle eklemek gerekmez.
    ///
    /// - Ekran / kalite ayarları hemen uygulanır.
    /// - Ses ayarları AudioManager ortaya çıkar çıkmaz uygulanır (o bir sahne objesi).
    /// - Uygulama arka plana düşünce ses kısılır (MuteWhenUnfocused açıksa).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsBootstrap : MonoBehaviour
    {
        private bool _audioApplied;
        private bool _duckedByFocus;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            // Odak kaybında Unity oyunu tamamen durdurur; bazı GPU / çoklu ekran
            // kombinasyonlarında ekran o sırada camera background rengine döner.
            Application.runInBackground = true;

            GameSettings.EnsureLoaded();
            GameSettings.ApplyDisplay();
            Loc.Init();

            var go = new GameObject("[SettingsBootstrap]");
            go.AddComponent<SettingsBootstrap>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            // AudioManager sahne ile geldiği için ilk frame'de hazır olmayabilir.
            if (_audioApplied) { enabled = false; return; }
            if (AudioManager.Instance == null) return;

            GameSettings.ApplyAudio();
            _audioApplied = true;
            enabled = false;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!GameSettings.MuteWhenUnfocused) return;
            if (AudioManager.Instance == null) return;

            if (!hasFocus)
            {
                AudioManager.Instance.SetMasterVolume(0f);
                _duckedByFocus = true;
            }
            else if (_duckedByFocus)
            {
                AudioManager.Instance.SetMasterVolume(GameSettings.EffectiveMaster);
                _duckedByFocus = false;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Mobilde OnApplicationFocus her platformda tetiklenmiyor.
            OnApplicationFocus(!paused);
        }
    }
}
