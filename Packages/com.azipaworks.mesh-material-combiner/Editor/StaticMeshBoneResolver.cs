using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class StaticMeshBoneResolver
    {
        public static Transform Resolve(RendererEntry entry, IEnumerable<RendererEntry> selected,
            Transform targetRoot)
        {
            if (targetRoot == null) return null;
            if (entry == null || entry.Renderer == null) return targetRoot;

            var selectedTransforms = new HashSet<Transform>();
            foreach (var candidate in selected)
                if (candidate != null && candidate.Included && candidate.Renderer != null)
                    selectedTransforms.Add(candidate.Renderer.transform);

            var current = entry.Renderer.transform.parent;
            while (current != null && (current == targetRoot || current.IsChildOf(targetRoot)))
            {
                // Selected source objects may be hidden and stripped after the merge. Never use one as
                // a generated bone. EditorOnly ancestors are likewise unavailable in an avatar build.
                if (!selectedTransforms.Contains(current) && !current.CompareTag("EditorOnly"))
                    return current;
                if (current == targetRoot) break;
                current = current.parent;
            }
            return targetRoot;
        }
    }
}
