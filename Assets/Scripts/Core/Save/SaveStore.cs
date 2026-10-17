using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RogueBlockBlast.Core.Save
{
    /// <summary>
    /// İlerleme kaydının tek sahibi: persistentDataPath/save.json.
    ///
    /// Neden dosya? Windows'ta PlayerPrefs registry'de durur; Steam Auto-Cloud
    /// registry'yi eşitleyemez, yalnızca dosyaları. Ayarlar (GameSettings) bilerek
    /// PlayerPrefs'te kalır — makineye özeldir, PC ile Steam Deck arasında taşınmamalı.
    ///
    /// API PlayerPrefs'in aynısı (GetInt / SetInt / DeleteKey / Save ...), anahtarlar
    /// da eskisiyle aynı. Dosya açılışta bir kez okunur, değişiklikler bellekte
    /// birikir ve <see cref="DebounceSeconds"/> sonra ya da <see cref="Save"/>,
    /// odak kaybı, kapanışta diske yazılır. Yazma atomiktir: önce .tmp, sonra
    /// File.Replace — yarım kalmış bir yazma eski kaydı bozmaz (.bak da tutulur).
    ///
    /// Göç: dosya hiç yoksa ve PlayerPrefs'te demo ilerlemesi varsa, eksik bir
    /// anahtar ilk okunduğunda PlayerPrefs'ten içe alınır. Anahtar listesi
    /// PlayerPrefs'ten okunamadığı için (shape / kart id'leri sahnedeki SO'lardan
    /// gelir) göç tembel yapılır. Her anahtar bir kez denenir; <see cref="DeleteKey"/>
    /// da "denendi" sayılır, yoksa silinen değer PlayerPrefs'ten geri dirilirdi.
    /// Eski PlayerPrefs anahtarları silinmez.
    /// </summary>
    public static class SaveStore
    {
        public const string FileName = "save.json";
        private const int   Version  = 1;

        /// <summary>Kirli veri en geç bu kadar saniye sonra diske yazılır.</summary>
        public const float DebounceSeconds = 5f;

        // Göçe karar vermek için bakılan anahtarlar: demoyu oynamış herkeste en az biri var.
        private static readonly string[] LegacySentinelKeys =
        {
            "CoinWallet_Balance", "MaxMilestoneReached", "BestScore", "LastRun_HasData",
            "Tutorial_Completed", "Tutorial_Intro_Completed",
        };

        private static readonly Dictionary<string, int>    _ints    = new();
        private static readonly Dictionary<string, float>  _floats  = new();
        private static readonly Dictionary<string, string> _strings = new();
        private static readonly HashSet<string>            _legacyChecked = new();

        private static bool  _loaded;
        private static bool  _legacyImport;
        private static bool  _dirty;
        private static float _dirtySince;

        public static string FilePath   => Path.Combine(Application.persistentDataPath, FileName);
        private static string BackupPath => FilePath + ".bak";
        private static string TempPath   => FilePath + ".tmp";

        /// <summary>Bu kayıt hâlâ PlayerPrefs'ten eksik anahtar alıyor mu (debug).</summary>
        public static bool IsImportingLegacy { get { EnsureLoaded(); return _legacyImport; } }

        // ── Yaşam döngüsü ────────────────────────────────────────────────────

        // Domain reload kapalıyken play mode'lar arası statik durum kalmasın.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _ints.Clear(); _floats.Clear(); _strings.Clear(); _legacyChecked.Clear();
            _loaded = _legacyImport = _dirty = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            EnsureLoaded();
            var go = new GameObject("[SaveStore]");
            go.AddComponent<SaveStoreRunner>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            foreach (var path in new[] { FilePath, BackupPath, TempPath })
            {
                if (TryRead(path, out var data))
                {
                    Apply(data);
                    if (path != FilePath)
                    {
                        Debug.LogWarning($"[SaveStore] {FileName} okunamadı, yedekten yüklendi: {path}");
                        MarkDirty();
                    }
                    return;
                }
            }

            if (File.Exists(FilePath))
            {
                // Bozuk ve yedeksiz: üstüne yazmadan önce kenara al, inceleme için kalsın.
                string corrupt = FilePath + ".corrupt";
                try { File.Copy(FilePath, corrupt, true); } catch (Exception e) { Debug.LogException(e); }
                Debug.LogError($"[SaveStore] {FileName} bozuk; {corrupt} olarak saklandı.");
            }

            // İlk açılış (ya da kurtarılamayan kayıt): demo ilerlemesi varsa içe al.
            _legacyImport = HasLegacyProgress();
            Debug.Log(_legacyImport
                ? "[SaveStore] Kayıt dosyası yok — PlayerPrefs'teki ilerleme taşınıyor."
                : "[SaveStore] Kayıt dosyası yok — yeni kayıt.");
            MarkDirty();
        }

        // ── Okuma ────────────────────────────────────────────────────────────

        public static bool HasKey(string key)
        {
            EnsureLoaded();
            TryImportLegacy(key, ValueKind.Any);
            return _ints.ContainsKey(key) || _floats.ContainsKey(key) || _strings.ContainsKey(key);
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            EnsureLoaded();
            TryImportLegacy(key, ValueKind.Int);
            return _ints.TryGetValue(key, out int v) ? v : defaultValue;
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            EnsureLoaded();
            TryImportLegacy(key, ValueKind.Float);
            return _floats.TryGetValue(key, out float v) ? v : defaultValue;
        }

        public static string GetString(string key, string defaultValue = "")
        {
            EnsureLoaded();
            TryImportLegacy(key, ValueKind.String);
            return _strings.TryGetValue(key, out string v) ? v : defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue = false) =>
            GetInt(key, defaultValue ? 1 : 0) == 1;

        // ── Yazma ────────────────────────────────────────────────────────────

        public static void SetInt(string key, int value)
        {
            EnsureLoaded();
            if (_ints.TryGetValue(key, out int old) && old == value) return;
            _ints[key] = value;
            MarkDirty();
        }

        public static void SetFloat(string key, float value)
        {
            EnsureLoaded();
            if (_floats.TryGetValue(key, out float old) && old.Equals(value)) return;
            _floats[key] = value;
            MarkDirty();
        }

        public static void SetString(string key, string value)
        {
            EnsureLoaded();
            value ??= string.Empty;
            if (_strings.TryGetValue(key, out string old) && old == value) return;
            _strings[key] = value;
            MarkDirty();
        }

        // Eski kodla uyum için bool da int olarak saklanır (1 / 0).
        public static void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);

        public static void DeleteKey(string key)
        {
            EnsureLoaded();
            bool removed = _ints.Remove(key) | _floats.Remove(key) | _strings.Remove(key);
            // Göç sürerken "denendi" olarak işaretle — yoksa bir sonraki Get
            // silinen değeri PlayerPrefs'ten geri getirirdi.
            if (_legacyImport) removed |= _legacyChecked.Add(key);
            if (removed) MarkDirty();
        }

        /// <summary>
        /// Tüm ilerlemeyi siler ve boş kaydı hemen yazar. Göç de kapanır: sıfırlanan
        /// ilerleme PlayerPrefs'ten geri gelmesin.
        /// </summary>
        public static void DeleteAll()
        {
            EnsureLoaded();
            _ints.Clear(); _floats.Clear(); _strings.Clear(); _legacyChecked.Clear();
            _legacyImport = false;
            MarkDirty();
            Save();
        }

        /// <summary>Bekleyen değişiklikleri hemen diske yazar (PlayerPrefs.Save karşılığı).</summary>
        public static void Save()
        {
            if (!_loaded || !_dirty) return;
            if (WriteAtomic(Serialize())) _dirty = false;
        }

        /// <summary>Debounce — <see cref="SaveStoreRunner"/> her frame çağırır.</summary>
        internal static void Tick()
        {
            if (_dirty && Time.unscaledTime - _dirtySince >= DebounceSeconds) Save();
        }

        private static void MarkDirty()
        {
            if (_dirty) return;
            _dirty      = true;
            _dirtySince = Time.unscaledTime;
        }

        // ── Göç (PlayerPrefs → dosya) ────────────────────────────────────────

        private enum ValueKind { Any, Int, Float, String }

        private static bool HasLegacyProgress()
        {
            foreach (var k in LegacySentinelKeys)
                if (PlayerPrefs.HasKey(k)) return true;
            return false;
        }

        private static void TryImportLegacy(string key, ValueKind kind)
        {
            if (!_legacyImport || string.IsNullOrEmpty(key)) return;
            if (_ints.ContainsKey(key) || _floats.ContainsKey(key) || _strings.ContainsKey(key)) return;
            if (!_legacyChecked.Add(key)) return;   // bu anahtar zaten denendi
            MarkDirty();

            if (!PlayerPrefs.HasKey(key)) return;

            // PlayerPrefs tipi söylemez; okuyanın istediği tip esas alınır.
            switch (kind)
            {
                case ValueKind.Float:  _floats[key]  = PlayerPrefs.GetFloat(key);  break;
                case ValueKind.String: _strings[key] = PlayerPrefs.GetString(key); break;
                default:               _ints[key]    = PlayerPrefs.GetInt(key);    break;
            }
        }

        // ── Dosya ────────────────────────────────────────────────────────────

        [Serializable] private struct IntEntry    { public string k; public int    v; }
        [Serializable] private struct FloatEntry  { public string k; public float  v; }
        [Serializable] private struct StringEntry { public string k; public string v; }

        [Serializable]
        private sealed class SaveData
        {
            public int  version;
            public bool legacyImport;
            public List<IntEntry>    ints    = new();
            public List<FloatEntry>  floats  = new();
            public List<StringEntry> strings = new();
            public List<string>      legacyChecked = new();
        }

        private static string Serialize()
        {
            var data = new SaveData { version = Version, legacyImport = _legacyImport };
            foreach (var kv in _ints)    data.ints.Add(new IntEntry    { k = kv.Key, v = kv.Value });
            foreach (var kv in _floats)  data.floats.Add(new FloatEntry  { k = kv.Key, v = kv.Value });
            foreach (var kv in _strings) data.strings.Add(new StringEntry { k = kv.Key, v = kv.Value });
            if (_legacyImport) data.legacyChecked.AddRange(_legacyChecked);

            // Sıralı yazılsın: Steam Cloud çakışmasında / diff'te okunabilir kalır.
            data.ints.Sort((a, b) => string.CompareOrdinal(a.k, b.k));
            data.floats.Sort((a, b) => string.CompareOrdinal(a.k, b.k));
            data.strings.Sort((a, b) => string.CompareOrdinal(a.k, b.k));
            data.legacyChecked.Sort(string.CompareOrdinal);
            return JsonUtility.ToJson(data, true);
        }

        private static void Apply(SaveData data)
        {
            _ints.Clear(); _floats.Clear(); _strings.Clear(); _legacyChecked.Clear();
            foreach (var e in data.ints)    if (!string.IsNullOrEmpty(e.k)) _ints[e.k]    = e.v;
            foreach (var e in data.floats)  if (!string.IsNullOrEmpty(e.k)) _floats[e.k]  = e.v;
            foreach (var e in data.strings) if (!string.IsNullOrEmpty(e.k)) _strings[e.k] = e.v ?? string.Empty;
            _legacyImport = data.legacyImport;
            if (_legacyImport && data.legacyChecked != null)
                foreach (var k in data.legacyChecked) _legacyChecked.Add(k);
        }

        private static bool TryRead(string path, out SaveData data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                string json = File.ReadAllText(path, Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json)) return false;
                data = JsonUtility.FromJson<SaveData>(json);
                if (data == null || data.version <= 0) { data = null; return false; }
                data.ints    ??= new(); data.floats ??= new();
                data.strings ??= new(); data.legacyChecked ??= new();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveStore] Okunamadı: {path}\n{e.Message}");
                data = null;
                return false;
            }
        }

        private static bool WriteAtomic(string json)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(TempPath, json, new UTF8Encoding(false));

                if (File.Exists(FilePath))
                {
                    try
                    {
                        File.Replace(TempPath, FilePath, BackupPath, true);
                    }
                    catch (Exception)
                    {
                        // File.Replace her dosya sisteminde yok — elle aynı sıra.
                        File.Copy(FilePath, BackupPath, true);
                        File.Delete(FilePath);
                        File.Move(TempPath, FilePath);
                    }
                }
                else
                {
                    File.Move(TempPath, FilePath);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveStore] Yazılamadı: {FilePath}\n{e}");
                return false;
            }
        }

        // ── Editör / debug ───────────────────────────────────────────────────

        /// <summary>
        /// Kayıt dosyasını (ve yedeklerini) diskten siler, bellekteki durumu unutur.
        /// Bir sonraki erişim "ilk açılış" gibi davranır (göç dahil).
        /// </summary>
        public static void DeleteFiles()
        {
            foreach (var p in new[] { FilePath, BackupPath, TempPath })
                try { if (File.Exists(p)) File.Delete(p); } catch (Exception e) { Debug.LogException(e); }
            ResetStatics();
        }
    }

    /// <summary>
    /// <see cref="SaveStore"/>'un frame / kapanış kancaları. Boot'ta kendini kurar,
    /// sahneye eklemek gerekmez.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class SaveStoreRunner : MonoBehaviour
    {
        private void Update() => SaveStore.Tick();

        // Mobilde uygulama arka planda öldürülebilir; Quit gelmeyebilir.
        private void OnApplicationPause(bool paused) { if (paused) SaveStore.Save(); }
        private void OnApplicationFocus(bool focus)  { if (!focus) SaveStore.Save(); }
        private void OnApplicationQuit()             => SaveStore.Save();
    }
}
