using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class MeshAnalyzer
    {
        public static int CountTriangles(Mesh mesh)
        {
            if (mesh == null) return 0;
            var count = 0;
            for (var i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetTopology(i) == MeshTopology.Triangles)
                    count += (int)mesh.GetIndexCount(i) / 3;
            }
            return count;
        }

        public static OptimizationStatistics CalculateBefore(IEnumerable<RendererEntry> entries, bool countAll = false)
        {
            var stats = new OptimizationStatistics();
            var materials = new HashSet<Material>();
            foreach (var entry in entries)
            {
                if (!countAll && !entry.Included) continue;
                stats.RendererCount++;
                stats.MaterialSlots += entry.MaterialSlotCount;
                stats.Vertices += entry.VertexCount;
                stats.Triangles += entry.TriangleCount;
                foreach (var material in entry.Materials)
                    if (material != null) materials.Add(material);
            }
            stats.UniqueMaterials = materials.Count;
            return stats;
        }

        public static long EstimateMergedVertexCount(IEnumerable<RendererEntry> entries)
        {
            long total = 0;
            foreach (var entry in entries)
            {
                if (!entry.Included || entry.Mesh == null) continue;
                var unique = new HashSet<long>();
                var slots = Mathf.Min(entry.Mesh.subMeshCount, entry.Materials.Length);
                for (var sub = 0; sub < slots; sub++)
                {
                    var materialId = entry.Materials[sub] != null ? entry.Materials[sub].GetInstanceID() : 0;
                    foreach (var index in entry.Mesh.GetIndices(sub))
                        unique.Add(((long)(uint)materialId << 32) | (uint)index);
                }
                total += unique.Count;
            }
            return total;
        }
    }
}
