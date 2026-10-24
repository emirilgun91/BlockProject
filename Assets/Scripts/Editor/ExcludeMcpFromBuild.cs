// Yalnızca MCP paketi kuruluyken anlamlı (ve System.Reflection.Metadata da onun NuGet
// bağımlılıklarıyla geliyor); paket kaldırılınca bu dosya kendiliğinden devre dışı kalır.
#if UNITY_MCP_READY
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace RogueBlockBlast.EditorTools
{
    /// <summary>
    /// Unity MCP (com.ivanmurzak.unity.mcp) yalnızca editörde, Claude'un Unity'yi sürmesi için
    /// var; oyun kodu ona hiç dokunmuyor. Ama Runtime assembly'leri <c>UNITY_MCP_READY</c> ile
    /// derleniyor ve <c>Assets/Plugins/NuGet</c> DLL'leri (Roslyn, SignalR…, ~18 MB) "Any
    /// Platform" işaretli — ikisi de player build'ine giriyordu. Tanımı kaldırmak editördeki
    /// MCP'yi de kapatır, DLL'leri Editor-only yapmak Runtime assembly'lerinin build'ini kırar;
    /// bu yüzden yalnızca player build'inin assembly listesinden çıkarılıyorlar.
    ///
    /// Bir NuGet DLL'ini build'de kalan bir assembly gerçekten referans veriyorsa (metadata'daki
    /// AssemblyRef tablosu) o DLL kalır — örn. System.Memory, Unity'nin System.IO.Hashing'i
    /// için. Metin taraması yetmiyor: derleyici her assembly'ye "Microsoft.CodeAnalysis" adlı
    /// bir isim alanı gömüyor.
    /// </summary>
    public sealed class ExcludeMcpFromBuild : IFilterBuildAssemblies
    {
        public int callbackOrder => 0;

        private const string NuGetFolder = "Assets/Plugins/NuGet/";
        private const string McpPrefix   = "com.IvanMurzak.";

        public string[] OnFilterAssemblies(BuildOptions buildOptions, string[] assemblies)
        {
            static string Name(string p) => Path.GetFileNameWithoutExtension(p);
            bool IsCandidate(string p) =>
                Path.GetFileName(p).StartsWith(McpPrefix) || p.Replace('\\', '/').Contains(NuGetFolder);

            var keep       = assemblies.Where(p => !IsCandidate(p)).ToList();
            var candidates = assemblies.Where(IsCandidate).ToDictionary(Name, p => p);

            // Kalan assembly'lerin referans verdiği aday da kalır, onun referansları da.
            var queue = new Queue<string>(keep);
            while (queue.Count > 0)
                foreach (var reference in References(queue.Dequeue()))
                    if (candidates.Remove(reference, out var path))
                    {
                        keep.Add(path);
                        queue.Enqueue(path);
                    }

            if (candidates.Count > 0)
                Debug.Log($"[ExcludeMcpFromBuild] {candidates.Count} assembly build dışında: " +
                          string.Join(", ", candidates.Keys.OrderBy(n => n)));
            return keep.ToArray();
        }

        private static IEnumerable<string> References(string path)
        {
            if (!File.Exists(path)) return System.Array.Empty<string>();
            try
            {
                using var pe = new PEReader(File.OpenRead(path));
                if (!pe.HasMetadata) return System.Array.Empty<string>();
                var md = pe.GetMetadataReader();
                return md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).ToList();
            }
            catch (System.BadImageFormatException)
            {
                return System.Array.Empty<string>();   // yerel (native) DLL
            }
        }
    }
}
#endif
