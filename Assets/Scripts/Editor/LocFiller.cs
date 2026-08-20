using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// CSV çeviri tablolarına toplu hücre yazan yardımcı.
    ///
    /// Mevcut satırları KORUR, sadece belirtilen dil sütunlarını doldurur.
    /// Varsayılan olarak dolu hücrelerin üstüne yazmaz (overwrite=false) —
    /// böylece elle düzeltilmiş çeviriler kazara ezilmez.
    ///
    /// Kullanım (batch scriptlerinden):
    ///   LocFiller.Fill(path, "Toast.NotEnoughCoins", new[]{ "German", "..." }, new[]{ "NICHT GENUG", "..." });
    /// </summary>
    public static class LocFiller
    {
        /// <summary>Bu projede desteklenen 12 çekirdek dil — CSV sütun adlarıyla birebir.</summary>
        public static readonly string[] CoreLanguages =
        {
            "English", "Turkish", "German", "French", "Castilian Spanish", "Italian",
            "Portuguese", "Russian", "Polish", "Japanese", "Korean", "Chinese (Simplified)"
        };

        // ── CSV ayrıştırma ───────────────────────────────────────────────────
        public static List<string> SplitLine(string line)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (q)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else q = false;
                    }
                    else sb.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == ',') { res.Add(sb.ToString()); sb.Length = 0; }
                else sb.Append(c);
            }
            res.Add(sb.ToString());
            return res;
        }

        public static string Quote(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Contains(",") || s.Contains("\"") || s.Contains("\n")
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
        }

        /// <summary>
        /// rows: key → (dil adı → metin). Var olmayan anahtar eklenir, var olan güncellenir.
        /// Dönen değer: (yazılan hücre, atlanan hücre, eklenen yeni satır).
        /// </summary>
        public static (int written, int skipped, int newRows) Fill(
            string csvPath,
            Dictionary<string, Dictionary<string, string>> rows,
            bool overwrite = false)
        {
            string full = Path.GetFullPath(csvPath);
            var raw = File.ReadAllLines(full).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (raw.Count == 0) { Debug.LogError("[LocFiller] boş dosya: " + csvPath); return (0, 0, 0); }

            var header = SplitLine(raw[0]);
            int langCount = header.Count - 1;

            var colOf = new Dictionary<string, int>();
            for (int i = 1; i < header.Count; i++) colOf[header[i].Trim()] = i;

            // Mevcut satırları oku
            var table = new List<List<string>>();
            var indexOfKey = new Dictionary<string, int>();
            for (int r = 1; r < raw.Count; r++)
            {
                var cells = SplitLine(raw[r]);
                while (cells.Count < header.Count) cells.Add("");
                table.Add(cells);
                indexOfKey[cells[0].Trim()] = table.Count - 1;
            }

            int written = 0, skipped = 0, newRows = 0;

            foreach (var kv in rows)
            {
                string key = kv.Key;
                if (!indexOfKey.TryGetValue(key, out int rowIdx))
                {
                    var fresh = new List<string> { key };
                    for (int i = 0; i < langCount; i++) fresh.Add("");
                    table.Add(fresh);
                    rowIdx = table.Count - 1;
                    indexOfKey[key] = rowIdx;
                    newRows++;
                }

                foreach (var lang in kv.Value)
                {
                    if (!colOf.TryGetValue(lang.Key, out int col))
                    {
                        Debug.LogWarning($"[LocFiller] sütun yok: {lang.Key}");
                        continue;
                    }
                    bool empty = string.IsNullOrWhiteSpace(table[rowIdx][col]);
                    if (!empty && !overwrite) { skipped++; continue; }
                    table[rowIdx][col] = lang.Value;
                    written++;
                }
            }

            var sb = new StringBuilder();
            sb.Append(raw[0]).Append('\n');
            foreach (var row in table)
                sb.Append(string.Join(",", row.Select(Quote))).Append('\n');

            File.WriteAllText(full, sb.ToString(), new UTF8Encoding(true));
            AssetDatabase.Refresh();
            return (written, skipped, newRows);
        }

        /// <summary>
        /// Sıralı dizi formundan sözlük üretir — batch scriptlerini kısaltmak için.
        /// row[0] = key, sonrası langs dizisiyle aynı sırada.
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> Build(
            string[] langs, IEnumerable<string[]> rows)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            foreach (var r in rows)
            {
                var map = new Dictionary<string, string>();
                for (int i = 0; i < langs.Length && i + 1 < r.Length; i++)
                    if (!string.IsNullOrEmpty(r[i + 1])) map[langs[i]] = r[i + 1];
                result[r[0]] = map;
            }
            return result;
        }
    }
}
