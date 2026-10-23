using System;
using System.IO;
using RogueBlockBlast.Game.Trailer;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// TrailerDirector'ın TakeStarted / TakeEnded olaylarını Unity Recorder'a bağlar.
    /// Her take <c>Trailer/Takes/&lt;isim&gt;.mp4</c> olarak 1920×1080, 60 fps sabit kare
    /// hızıyla (oyun zamanı kayda kilitli) sesli kaydedilir.
    ///
    /// Oturum: <b>Tools ▸ RogueBlockBlast ▸ Trailer ▸ Begin Session</b> save.json'u yedekler
    /// ve steam_appid.txt'yi kenara alır (çekimler coin / best score / achievement
    /// yazmasın); <b>End Session</b> ikisini geri koyar.
    /// </summary>
    [InitializeOnLoad]
    public static class TrailerRecorderBridge
    {
        static RecorderController _ctrl;
        public static string LastFile = "";

        static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Trailer"));
        public static string TakesDir => Path.Combine(Root, "Takes");

        static TrailerRecorderBridge()
        {
            TrailerDirector.TakeStarted += OnStart;
            TrailerDirector.TakeEnded   += OnEnd;
            EditorApplication.playModeStateChanged += s =>
            {
                if (s == PlayModeStateChange.ExitingPlayMode) OnEnd();
            };
        }

        static void OnStart(string take)
        {
            OnEnd();
            Directory.CreateDirectory(TakesDir);

            var settings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            var movie    = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movie.name    = "TrailerTake";
            movie.Enabled = true;
            movie.EncoderSettings = new CoreEncoderSettings
            {
                Codec           = CoreEncoderSettings.OutputCodec.MP4,
                // Ara kayıt: kurgu sırasında yeniden kodlanacak, kayıp az olsun
                EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.Custom,
                TargetBitRate   = 32f,
                GopSize         = 30,
                EncodingProfile = CoreEncoderSettings.H264EncodingProfile.High,
            };
            movie.ImageInputSettings = new GameViewInputSettings { OutputWidth = 1920, OutputHeight = 1080 };
            movie.AudioInputSettings.PreserveAudio = true;
            LastFile = Path.Combine(TakesDir, take);
            movie.OutputFile = LastFile;   // uzantıyı Recorder ekler

            settings.AddRecorderSettings(movie);
            settings.SetRecordModeToManual();
            settings.FrameRate         = 60f;
            settings.FrameRatePlayback = FrameRatePlayback.Constant;
            settings.CapFrameRate      = true;

            _ctrl = new RecorderController(settings);
            _ctrl.PrepareRecording();
            _ctrl.StartRecording();
            Debug.Log($"[TrailerRecorder] kayıt: {LastFile}.mp4");
        }

        static void OnEnd()
        {
            if (_ctrl == null) return;
            if (_ctrl.IsRecording()) _ctrl.StopRecording();
            _ctrl = null;
            Debug.Log($"[TrailerRecorder] durdu: {LastFile}.mp4");
        }

        // ── Elle kayıt: oyuncu oynar, kısayol kaydı açıp kapatır ───────────────
        // Bot yok: ekrandaki her değer oyuncunun kendi run'ından gelir. F9 (play mode,
        // Game view odaktayken) kaydı başlatır / durdurur; dosya Takes/Manual_<saat>.mp4.

        static bool _prevMute;

        [MenuItem("Tools/RogueBlockBlast/Trailer/Toggle Manual Recording _F9")]
        public static void ToggleManual()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("[TrailerRecorder] Önce play mode'a gir."); return; }
            if (_ctrl != null && _ctrl.IsRecording())
            {
                OnEnd();
                EditorUtility.audioMasterMute = _prevMute;
                return;
            }
            // Game view "Mute Audio" açıkken Recorder sessizlik yazıyor
            _prevMute = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = false;
            OnStart("Manual_" + DateTime.Now.ToString("HHmmss"));
        }

        // ── Oturum: kayıtları koru ───────────────────────────────────────────

        static string SavePath   => Path.Combine(Application.persistentDataPath, "save.json");
        static string BackupPath => Path.Combine(Root, "save_backup.json");
        static string AppIdPath  => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "steam_appid.txt"));
        static string AppIdAside => Path.Combine(Root, "steam_appid.txt.aside");

        [MenuItem("Tools/RogueBlockBlast/Trailer/Begin Session (backup save, Steam off)")]
        public static string BeginSession()
        {
            Directory.CreateDirectory(Root);
            string r = "";
            if (File.Exists(SavePath) && !File.Exists(BackupPath)) { File.Copy(SavePath, BackupPath); r += "save yedeklendi; "; }
            if (File.Exists(AppIdPath)) { File.Move(AppIdPath, AppIdAside); r += "steam_appid kenarda; "; }
            Debug.Log("[Trailer] " + r);
            return r;
        }

        [MenuItem("Tools/RogueBlockBlast/Trailer/End Session (restore save, Steam on)")]
        public static string EndSession()
        {
            if (EditorApplication.isPlaying) return "önce play mode'dan çık";
            string r = "";
            if (File.Exists(BackupPath))
            {
                File.Copy(BackupPath, SavePath, true);
                File.Delete(BackupPath);
                foreach (var ext in new[] { ".bak", ".tmp" })
                    if (File.Exists(SavePath + ext)) File.Delete(SavePath + ext);
                r += "save geri yüklendi; ";
            }
            // Steamworks.NET, dosya yokken editörde 480 (Spacewar test uygulaması) yazıyor —
            // çekimler oraya bağlanır, gerçek uygulamaya achievement gitmez. Orijinali üzerine yaz.
            if (File.Exists(AppIdAside)) { File.Copy(AppIdAside, AppIdPath, true); File.Delete(AppIdAside); r += "steam_appid geri; "; }
            Debug.Log("[Trailer] " + r);
            return r;
        }

        /// <summary>Play mode'da (Begin Session sonrası) bir take'i JSON spec ile başlatır.</summary>
        public static string StartTake(string specJson) => TrailerDirector.Begin(specJson);
    }
}
