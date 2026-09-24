using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class OptimizationValidator
    {
        public static List<ValidationIssue> Validate(GameObject root, List<RendererEntry> entries)
        {
            var issues = new List<ValidationIssue>();
            if (root == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "Target Root is not assigned."));
                return issues;
            }
            if (!root.scene.IsValid())
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    "Target Root must be a GameObject in an open Scene, not a Prefab asset.", root));
                return issues;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    "Mesh merging is only supported in Edit Mode. Exit Play Mode before analyzing or merging.", root));
                return issues;
            }

            // Validate the complete avatar hierarchy before any assets are generated.
            // A missing script on an unrelated component can still make Unity reject
            // the temporary avatar clone when it is saved as a Prefab.
            var missingScriptObjects = FindMissingScriptObjects(root);
            if (missingScriptObjects.Count > 0)
            {
                var preview = string.Join(", ", missingScriptObjects.Take(5).Select(GetHierarchyPath));
                if (missingScriptObjects.Count > 5) preview += $" (+{missingScriptObjects.Count - 5})";
                issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    $"The avatar hierarchy contains {missingScriptObjects.Count} GameObject(s) with a missing script: {preview}",
                    missingScriptObjects[0]));
            }

            var selected = entries.FindAll(e => e.Included);
            if (selected.Count == 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Error, "No renderer is selected."));

            var animated = AnimationReferenceScanner.Scan(root, selected);
            var blendNames = new HashSet<string>();
            foreach (var entry in selected)
            {
                if (entry.Renderer == null) continue;
                if (entry.Mesh == null)
                {
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{entry.Renderer.name}: Missing Mesh.", entry.Renderer));
                    continue;
                }
                if (!entry.Mesh.isReadable)
                    issues.Add(new ValidationIssue(ValidationSeverity.Info,
                        $"{entry.Renderer.name}: Mesh Read/Write is disabled; Unity Editor access will be used without changing the source import settings.", entry.Mesh));
                if (entry.Materials.Length < entry.Mesh.subMeshCount)
                    issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{entry.Renderer.name}: Material slots are fewer than submeshes.", entry.Renderer));
                for (var i = 0; i < entry.Mesh.subMeshCount && i < entry.Materials.Length; i++)
                    if (entry.Materials[i] == null)
                        issues.Add(new ValidationIssue(ValidationSeverity.Error, $"{entry.Renderer.name}: Material slot {i} is missing.", entry.Renderer));

                if (entry.Renderer.transform.localToWorldMatrix.determinant < 0f)
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{entry.Renderer.name}: Negative scale detected; tangent handedness will be corrected where possible.", entry.Renderer));

                var uv = entry.Mesh.uv;
                for (var i = 0; i < uv.Length; i++)
                {
                    if (uv[i].x < 0f || uv[i].x > 1f || uv[i].y < 0f || uv[i].y > 1f)
                    {
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"{entry.Renderer.name}: UV0 is outside 0-1; atlas sampling may differ from texture wrapping.", entry.Mesh));
                        break;
                    }
                }

                for (var i = 0; i < entry.Mesh.blendShapeCount; i++)
                {
                    var shape = entry.Mesh.GetBlendShapeName(i);
                    if (!blendNames.Add(shape))
                        issues.Add(new ValidationIssue(ValidationSeverity.Info, $"BlendShape name collision: {shape}", entry.Mesh));
                }
                if (animated.Contains(entry.Renderer))
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                        $"{entry.Renderer.name}: This Renderer, GameObject, or Transform is referenced by an AnimationClip. Curves are not retargeted, so the source GameObject will be retained.", entry.Renderer));
                if (entry.Renderer is MeshRenderer)
                {
                    var anchor = StaticMeshBoneResolver.Resolve(entry, selected, root.transform);
                    issues.Add(new ValidationIssue(ValidationSeverity.Info,
                        $"{entry.Renderer.name}: Static vertices will follow retained bone '{anchor.name}'; the source MeshRenderer Transform will not be referenced by the merged mesh.", anchor));
                }
            }

            if (selected.Count > 0)
            {
                var bones = new HashSet<Transform>();
                var bindposes = new Dictionary<Transform, Matrix4x4>();
                // Bone diagnostics are intentionally aggregated per renderer.  A single
                // clothing mesh can reference hundreds of bones, and reporting every
                // bone individually makes Analyze both noisy and expensive to render.
                var avatarRoot = AvatarRootResolver.Resolve(root);
                foreach (var entry in selected)
                    if (entry.Renderer is SkinnedMeshRenderer smr)
                    {
                        var sourceBones = smr.bones;
                        var sourceBindposes = entry.Mesh != null
                            ? entry.Mesh.bindposes : new Matrix4x4[0];
                        var outsideSelected = new HashSet<Transform>();
                        var outsideAvatar = new HashSet<Transform>();
                        var incompatibleBindposes = new HashSet<Transform>();
                        for (var i = 0; i < sourceBones.Length; i++)
                        {
                            var bone = sourceBones[i];
                            if (bone == null) continue;
                            bones.Add(bone);
                            if (bone != root.transform && !bone.IsChildOf(root.transform))
                            {
                                outsideSelected.Add(bone);
                                if (avatarRoot == null ||
                                    (bone != avatarRoot.transform && !bone.IsChildOf(avatarRoot.transform)))
                                    outsideAvatar.Add(bone);
                            }
                            if (i >= sourceBindposes.Length) continue;
                            var normalized = sourceBindposes[i] * smr.transform.worldToLocalMatrix;
                            if (bindposes.TryGetValue(bone, out var previous) && !Approximately(previous, normalized))
                                incompatibleBindposes.Add(bone);
                            else bindposes[bone] = normalized;
                        }
                        if (incompatibleBindposes.Count > 0)
                            issues.Add(new ValidationIssue(ValidationSeverity.Error,
                                $"{smr.name}: {incompatibleBindposes.Count} bone(s) have incompatible bindposes across selected meshes.", smr));
                        if (outsideSelected.Count > 0)
                        {
                            var severity = outsideAvatar.Count > 0
                                ? ValidationSeverity.Warning
                                : ValidationSeverity.Info;
                            var scopeText = outsideAvatar.Count > 0
                                ? "outside the avatar root and will be mapped to the avatar root"
                                : "outside the selected scope but inside the avatar root; it will be retained";
                            issues.Add(new ValidationIssue(severity,
                                $"{smr.name}: {outsideSelected.Count} referenced bone(s) are {scopeText}.", smr));
                        }
                    }
                if (bones.Count > 256)
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning, $"Merged bone count is {bones.Count}; this may be expensive or unsupported on some targets."));
            }
            return issues;
        }

        internal static List<GameObject> FindMissingScriptObjects(GameObject root)
        {
            var result = new List<GameObject>();
            if (root == null) return result;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform == null || GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) <= 0)
                    continue;
                result.Add(transform.gameObject);
            }
            return result;
        }

        private static string GetHierarchyPath(GameObject gameObject)
        {
            if (gameObject == null) return "<unknown>";
            var parts = new Stack<string>();
            var current = gameObject.transform;
            while (current != null)
            {
                parts.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }

        private static bool Approximately(Matrix4x4 a, Matrix4x4 b)
        {
            for (var i = 0; i < 16; i++)
                if (Mathf.Abs(a[i] - b[i]) > 0.0001f) return false;
            return true;
        }
    }
}
