using RogueBlockBlast.UI;
using RogueBlockBlast.UI.Juice;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// HUD "juice" bileşenlerini açık sahneye bağlar.
    ///
    /// Hedefler elle sürüklenmez, mevcut view'ların <b>serileştirilmiş
    /// alanlarından</b> çözülür (<c>MilestoneView._piecesText</c>,
    /// <c>ComboView._multiplierText</c> gibi). Böylece hiyerarşi adlarına
    /// bağımlı olmayız; biri nesneyi yeniden adlandırsa da çalışır.
    ///
    /// İdempotent: her bileşen eklemeden önce varlığı kontrol edilir, ikinci
    /// çalıştırma bir şeyi ikiye katlamaz.
    ///
    /// Hangi sahne açıksa ona uygulanır. Geri almak için
    /// <c>Remove From Open Scene</c>.
    /// </summary>
    public static class JuiceBuilder
    {
        [MenuItem("Tools/RogueBlockBlast/Juice/Apply To Open Scene", priority = 100)]
        public static void Apply()
        {
            int applied = 0;

            applied += ApplyShapeCounter() ? 1 : 0;
            applied += ApplyScoreShine()   ? 1 : 0;
            applied += ApplyFlowingBar()   ? 1 : 0;
            applied += ApplyComboBeat()    ? 1 : 0;

            // Pool nabzı prefab üstünde bir alan — bileşen eklemeye gerek yok.
            Debug.Log(
                $"[Juice] {applied}/4 HUD efekti bağlandı. " +
                "Pool seçim nabzı PoolSlotView'da alan olarak duruyor (varsayılan açık).");

            MarkDirty();
        }

        /// <summary>
        /// Juice bileşenlerinin oturduğu nesneleri Hierarchy'de seçer ve
        /// Console'a tıklanabilir bir liste basar.
        ///
        /// Bileşenler yeni GameObject'ler yaratmaz — mevcut yazı, panel ve bar
        /// nesnelerinin üstüne binerler. Bu yüzden hiyerarşide gözle bulmak zor;
        /// bu komut onları doğrudan seçili hâle getirir.
        /// </summary>
        [MenuItem("Tools/RogueBlockBlast/Juice/Select Juice Objects", priority = 102)]
        public static void SelectAll()
        {
            var found = new System.Collections.Generic.List<Object>();

            Collect<ShapeCounterPulse>(found, "1 — Şekil sayacı nabzı");
            Collect<ShineSweep>(found,        "2 — Skorboard parlaması");
            Collect<FlowingFill>(found,       "3 — Akan milestone barı");
            Collect<MilestoneGain>(found,     "3b — Milestone kazanç efekti");
            Collect<PoolSlotView>(found,      "4 — Pool seçim nabzı (PoolSlotView alanları)");
            Collect<ComboHeartbeat>(found,    "5 — Combo kalp atışı");

            if (found.Count == 0)
            {
                Debug.LogWarning(
                    "[Juice] Hiçbir juice bileşeni bulunamadı. " +
                    "Önce 'Apply To Open Scene' çalıştırın.");
                return;
            }

            Selection.objects = found.ToArray();
            EditorGUIUtility.PingObject(found[0]);

            Debug.Log(
                $"[Juice] {found.Count} nesne seçildi. Inspector'da hepsi birden görünür; " +
                "tek tek ayarlamak için Hierarchy'den birini seçin.");
        }

        private static void Collect<T>(System.Collections.Generic.List<Object> into, string label)
            where T : Component
        {
            var items = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (items.Length == 0)
            {
                Debug.LogWarning($"[Juice] {label}: bulunamadı.");
                return;
            }

            foreach (var item in items)
            {
                into.Add(item.gameObject);
                // Nesne adına tıklayınca Hierarchy'de seçilir.
                Debug.Log($"[Juice] {label} → {Path(item.transform)}", item.gameObject);
            }
        }

        /// <summary>Hierarchy yolu — nesneyi elle bulmak isteyenler için.</summary>
        private static string Path(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + " / " + path;
            }
            return path;
        }

        [MenuItem("Tools/RogueBlockBlast/Juice/Remove From Open Scene", priority = 101)]
        public static void Remove()
        {
            int removed = 0;

            removed += DestroyAll<ShapeCounterPulse>();
            removed += DestroyAll<ShineSweep>();
            removed += DestroyAll<FlowingFill>();
            removed += DestroyAll<MilestoneGain>();
            removed += DestroyAll<ComboHeartbeat>();

            // Runtime'da üretilen çocuklar (glow, şerit, akış katmanları)
            // yalnızca Play sırasında var; sahnede kalıntı bırakmazlar.
            Debug.Log($"[Juice] {removed} bileşen kaldırıldı.");
            MarkDirty();
        }

        // ── 1. Şekil sayacı ──────────────────────────────────────────────────

        private static bool ApplyShapeCounter()
        {
            var view = Object.FindFirstObjectByType<MilestoneView>(FindObjectsInactive.Include);
            if (view == null) return Warn("MilestoneView bulunamadı — şekil sayacı atlandı.");

            var piecesText = ReadObject<TMP_Text>(view, "_piecesText");
            if (piecesText == null)
                return Warn("MilestoneView._piecesText boş — şekil sayacı atlandı.");

            // Nabız YALNIZCA yazıya uygulanır. Taşıyıcısına bağlanırsa tüm
            // milestone paneli nefes alır ve panelin pivotu merkezde olmadığı
            // için ölçeklenme ekranda kayma gibi görünür.
            var host = piecesText.rectTransform;

            var pulse = host.GetComponent<ShapeCounterPulse>();
            if (pulse == null) pulse = host.gameObject.AddComponent<ShapeCounterPulse>();

            var so = new SerializedObject(pulse);
            so.FindProperty("_target").objectReferenceValue = host;
            so.FindProperty("_text").objectReferenceValue   = piecesText;
            // Çerçeve bilinçli olarak boş: kullanıcı yalnızca yazının hareket
            // etmesini istiyor, çerçeve sabit kalmalı.
            so.FindProperty("_frame").objectReferenceValue  = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(pulse);
            Debug.Log($"[Juice] Şekil sayacı nabzı → {host.name} (yalnızca yazı)");
            return true;
        }

        // ── 2. Skorboard parlaması ───────────────────────────────────────────

        private static bool ApplyScoreShine()
        {
            var score = Object.FindFirstObjectByType<ScoreView>(FindObjectsInactive.Include);
            if (score == null) return Warn("ScoreView bulunamadı — parlama atlandı.");

            // ScoreView yazının üstünde olabilir; parlama panelde anlamlı.
            // En fazla iki seviye yukarı çıkıp arka planı olan rect aranır.
            var host = score.transform as RectTransform;
            if (host == null) return Warn("ScoreView'ın RectTransform'u yok — parlama atlandı.");

            if (host.GetComponent<Image>() == null)
            {
                var probe = host;
                for (int i = 0; i < 2 && probe.parent is RectTransform parent; i++)
                {
                    probe = parent;
                    if (probe.GetComponent<Image>() != null) { host = probe; break; }
                }
            }

            var sweep = host.GetComponent<ShineSweep>();
            if (sweep == null) sweep = host.gameObject.AddComponent<ShineSweep>();

            var so = new SerializedObject(sweep);
            so.FindProperty("_area").objectReferenceValue = host;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(sweep);
            Debug.Log($"[Juice] Skorboard parlaması → {host.name}");
            return true;
        }

        // ── 3. Akan milestone barı ───────────────────────────────────────────

        private static bool ApplyFlowingBar()
        {
            var view = Object.FindFirstObjectByType<MilestoneView>(FindObjectsInactive.Include);
            if (view == null) return Warn("MilestoneView bulunamadı — akan bar atlandı.");

            var fill = ReadObject<Image>(view, "_fill");
            if (fill == null) return Warn("MilestoneView._fill boş — akan bar atlandı.");

            var flow = fill.GetComponent<FlowingFill>();
            if (flow == null) flow = fill.gameObject.AddComponent<FlowingFill>();

            var so = new SerializedObject(flow);
            so.FindProperty("_fill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);

            // Kazanç efekti aynı dolguya biner.
            var gain = fill.GetComponent<MilestoneGain>();
            if (gain == null) gain = fill.gameObject.AddComponent<MilestoneGain>();

            var gainSo = new SerializedObject(gain);
            gainSo.FindProperty("_fill").objectReferenceValue = fill;
            gainSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gain);

            Debug.Log($"[Juice] Akan bar + kazanç efekti → {fill.name}");
            return true;
        }

        // ── 5. Combo kalp atışı ──────────────────────────────────────────────

        private static bool ApplyComboBeat()
        {
            var view = Object.FindFirstObjectByType<ComboView>(FindObjectsInactive.Include);
            if (view == null) return Warn("ComboView bulunamadı — kalp atışı atlandı.");

            var text = ReadObject<TMP_Text>(view, "_multiplierText");
            if (text == null) return Warn("ComboView._multiplierText boş — kalp atışı atlandı.");

            var beat = text.GetComponent<ComboHeartbeat>();
            if (beat == null) beat = text.gameObject.AddComponent<ComboHeartbeat>();

            var so = new SerializedObject(beat);
            so.FindProperty("_text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(beat);
            Debug.Log($"[Juice] Combo kalp atışı → {text.name}");
            return true;
        }

        // ── Yardımcılar ──────────────────────────────────────────────────────

        /// <summary>
        /// Bir bileşenin private serileştirilmiş alanından referans okur.
        /// Hiyerarşi adı aramaktan daha sağlam: nesneler yeniden adlandırılsa
        /// da view'ın kendi bağlantısı doğru hedefi gösterir.
        /// </summary>
        private static T ReadObject<T>(Object host, string field) where T : Object
        {
            var so   = new SerializedObject(host);
            var prop = so.FindProperty(field);

            if (prop == null)
            {
                Debug.LogWarning($"[Juice] '{host.GetType().Name}.{field}' alanı bulunamadı.");
                return null;
            }

            return prop.objectReferenceValue as T;
        }

        private static int DestroyAll<T>() where T : Component
        {
            var found = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var c in found) Object.DestroyImmediate(c);
            return found.Length;
        }

        private static bool Warn(string message)
        {
            Debug.LogWarning($"[Juice] {message}");
            return false;
        }

        private static void MarkDirty()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid()) EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
