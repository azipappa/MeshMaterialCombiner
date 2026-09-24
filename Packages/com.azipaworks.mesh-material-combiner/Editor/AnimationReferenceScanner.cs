using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class AnimationReferenceScanner
    {
        public static HashSet<Renderer> Scan(GameObject root, IEnumerable<RendererEntry> entries)
        {
            var result = new HashSet<Renderer>();
            if (root == null) return result;
            var byPath = new Dictionary<string, Renderer>();
            foreach (var entry in entries)
            {
                if (entry.Renderer == null) continue;
                byPath[AnimationUtility.CalculateTransformPath(entry.Renderer.transform, root.transform)] = entry.Renderer;
            }

            var clips = new HashSet<AnimationClip>();
            foreach (var animator in root.GetComponentsInChildren<Animator>(true))
                if (animator.runtimeAnimatorController != null)
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                        if (clip != null) clips.Add(clip);
            foreach (var animation in root.GetComponentsInChildren<Animation>(true))
                foreach (AnimationState state in animation)
                    if (state.clip != null) clips.Add(state.clip);

            foreach (var clip in clips)
            {
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    CheckBinding(binding.path, binding.propertyName, byPath, result);
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                    CheckBinding(binding.path, binding.propertyName, byPath, result);
            }
            return result;
        }

        private static void CheckBinding(string path, string property, Dictionary<string, Renderer> byPath, HashSet<Renderer> result)
        {
            if (!byPath.TryGetValue(path, out var renderer)) return;
            if (property == "m_Enabled" || property == "m_IsActive" ||
                property.StartsWith("m_LocalPosition") || property.StartsWith("m_LocalRotation") ||
                property.StartsWith("m_LocalScale") || property.StartsWith("localEulerAngles") ||
                property.StartsWith("blendShape.") || property.StartsWith("material.") ||
                property.Contains("m_Materials"))
                result.Add(renderer);
        }
    }
}
