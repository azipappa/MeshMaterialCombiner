using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class SourceObjectSafety
    {
        public static bool CanHideAndMarkEditorOnly(RendererEntry entry,
            IEnumerable<RendererEntry> selected, HashSet<Renderer> animated,
            Transform targetRoot, out string reason)
        {
            reason = string.Empty;
            if (entry == null || entry.Renderer == null)
            { reason = "Renderer is missing."; return false; }
            var source = entry.Renderer.transform;
            if (source == targetRoot)
            { reason = "The source is the Target Root."; return false; }
            if (source.childCount != 0)
            { reason = "The source GameObject has child objects."; return false; }
            if (animated != null && animated.Contains(entry.Renderer))
            { reason = "The source Renderer or GameObject is animation-referenced."; return false; }

            foreach (var other in selected)
            {
                if (!other.Included || other.Renderer == null) continue;
                if (other.Renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.rootBone != null &&
                        (skinned.rootBone == source || skinned.rootBone.IsChildOf(source)))
                    { reason = "The source is a root bone or its ancestor."; return false; }
                    foreach (var bone in skinned.bones)
                        if (bone != null && (bone == source || bone.IsChildOf(source)))
                        { reason = "The source is a referenced bone or its ancestor."; return false; }
                }
            }

            foreach (var component in source.GetComponents<Component>())
            {
                if (component == null)
                { reason = "The source has a missing script."; return false; }
                if (component is Transform || component is Renderer || component is MeshFilter) continue;
                reason = "The source has an additional component: " + component.GetType().Name + ".";
                return false;
            }
            if (IsReferencedByAnotherComponent(source, targetRoot, out var referencingComponent))
            {
                reason = "The source is referenced by " + referencingComponent.GetType().Name +
                         " on '" + referencingComponent.gameObject.name + "'.";
                return false;
            }
            return true;
        }

        private static bool IsReferencedByAnotherComponent(Transform source, Transform targetRoot,
            out Component referencingComponent)
        {
            referencingComponent = null;
            foreach (var component in targetRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component.transform == source) continue;
                try
                {
                    var serialized = new SerializedObject(component);
                    var property = serialized.GetIterator();
                    while (property.Next(true))
                    {
                        if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = property.objectReferenceValue;
                        if (value != source && value != source.gameObject) continue;
                        referencingComponent = component;
                        return true;
                    }
                }
                catch
                {
                    // Some native/custom inspectors cannot expose all serialized state. They are ignored here;
                    // the explicit bone, animation, child and component checks still apply.
                }
            }
            return false;
        }
    }
}
