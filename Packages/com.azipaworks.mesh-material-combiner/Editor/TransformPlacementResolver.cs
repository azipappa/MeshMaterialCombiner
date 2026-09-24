using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class TransformPlacementResolver
    {
        public static Transform FindLowestCommonAncestor(IEnumerable<RendererEntry> entries, Transform targetRoot)
        {
            var targets = new List<Transform>();
            foreach (var entry in entries)
            {
                if (!entry.Included || entry.Renderer == null) continue;
                if (entry.Renderer is SkinnedMeshRenderer skinned)
                {
                    foreach (var bone in skinned.bones)
                        if (bone != null && !targets.Contains(bone)) targets.Add(bone);
                    if (skinned.rootBone != null && !targets.Contains(skinned.rootBone)) targets.Add(skinned.rootBone);
                }
                else
                {
                    var anchor = StaticMeshBoneResolver.Resolve(entry, entries, targetRoot);
                    if (anchor != null && !targets.Contains(anchor)) targets.Add(anchor);
                }
            }

            if (targets.Count == 0) return targetRoot;
            var candidate = targets[0];
            while (candidate != null)
            {
                var containsAll = true;
                for (var i = 1; i < targets.Count; i++)
                {
                    if (targets[i] != candidate && !targets[i].IsChildOf(candidate))
                    {
                        containsAll = false;
                        break;
                    }
                }
                if (containsAll) break;
                candidate = candidate.parent;
            }

            if (candidate == null || (candidate != targetRoot && !candidate.IsChildOf(targetRoot)))
                return targetRoot;
            return candidate;
        }
    }
}
