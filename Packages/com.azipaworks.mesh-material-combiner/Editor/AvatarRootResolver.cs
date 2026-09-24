using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace AvatarMeshMaterialOptimizer
{
    internal static class AvatarRootResolver
    {
        public static GameObject Resolve(GameObject selected)
        {
            if (selected == null) return null;
            var descriptor = selected.GetComponentInParent<VRCAvatarDescriptor>(true);
            if (descriptor != null) return descriptor.gameObject;

            // Keep the tool usable for non-VRChat test hierarchies as well.
            var current = selected.transform;
            while (current.parent != null) current = current.parent;
            return current.gameObject;
        }
    }
}
