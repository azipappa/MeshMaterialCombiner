using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class RendererCollector
    {
        public static List<RendererEntry> Collect(GameObject root)
        {
            var result = new List<RendererEntry>();
            if (root == null) return result;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned)
                    mesh = skinned.sharedMesh;
                else if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null) mesh = filter.sharedMesh;
                }
                else
                    continue;

                var entry = new RendererEntry
                {
                    Renderer = renderer,
                    Mesh = mesh,
                    Materials = renderer.sharedMaterials ?? System.Array.Empty<Material>(),
                    MaterialSlotCount = renderer.sharedMaterials?.Length ?? 0,
                    VertexCount = mesh != null ? mesh.vertexCount : 0,
                    TriangleCount = MeshAnalyzer.CountTriangles(mesh)
                };
                result.Add(entry);
            }
            return result;
        }
    }
}
