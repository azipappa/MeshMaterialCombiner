using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    /// <summary>Places hidden source objects in a predictable, per-parent editor-only container.</summary>
    internal static class SourceObjectOrganizer
    {
        public const string ContainerName = "Combined+EditorOnly";

        public static Transform MoveToCombinedContainer(GameObject source)
        {
            if (source == null) return null;
            var sourceTransform = source.transform;
            var originalParent = sourceTransform.parent;
            if (originalParent == null) return null;
            if (originalParent.name == ContainerName) return originalParent;

            var container = originalParent.Find(ContainerName);
            if (container == null)
            {
                var containerObject = new GameObject(ContainerName);
                containerObject.tag = "Untagged";
                containerObject.SetActive(true);
                containerObject.transform.SetParent(originalParent, false);
                Undo.RegisterCreatedObjectUndo(containerObject, "Create Combined+EditorOnly container");
                container = containerObject.transform;
            }

            if (sourceTransform.parent != container)
                Undo.SetTransformParent(sourceTransform, container, "Move source under Combined+EditorOnly");
            return container;
        }
    }
}
