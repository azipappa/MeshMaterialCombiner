using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class OptimizationPipeline
    {
        internal const int StandardMaximumAtlasSize = 4096;
        private const long AtlasMemoryWarningBytes = 1L * 1024L * 1024L * 1024L;
        private const long AtlasMemoryHardLimitBytes = 4L * 1024L * 1024L * 1024L;

        public static OptimizationAnalysis Analyze(OptimizerSettings settings, List<RendererEntry> entries,
            IList<GameObject> sourceTargets = null, MMCBuildRecord ownerRecord = null)
        {
            var analysis = new OptimizationAnalysis();
            analysis.Renderers.AddRange(entries);
            analysis.Report.Issues.AddRange(OptimizationValidator.Validate(settings.TargetRoot, entries));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return analysis;
            analysis.MaterialGroups.AddRange(MaterialAnalyzer.BuildGroups(entries, analysis.Report.Issues,
                settings.Preset, settings.ForceRepresentativeMaterial));
            if (settings.Preset == OptimizationPreset.ForceSingleSlot)
                AddForceSingleSlotValidation(entries, analysis.Report.Issues);
            var isDirectObjectsMode = sourceTargets != null && sourceTargets.Count > 0;
            analysis.Report.Before = MeshAnalyzer.CalculateBefore(entries, isDirectObjectsMode);
            var mergedStats = MeshAnalyzer.CalculateBefore(entries, false);
            
            var predictedSubmeshes = CountOutputSubmeshes(entries, analysis.MaterialGroups);
            analysis.Report.After = new OptimizationStatistics
            {
                RendererCount = mergedStats.RendererCount > 0 ? 1 : 0,
                MaterialSlots = predictedSubmeshes,
                UniqueMaterials = analysis.MaterialGroups.Count,
                Vertices = MeshAnalyzer.EstimateMergedVertexCount(entries),
                Triangles = mergedStats.Triangles
            };
            if (settings.TargetRoot != null && analysis.Report.Before.RendererCount > 0)
            {
                var avatarRoot = AvatarRootResolver.Resolve(settings.TargetRoot);
                analysis.Report.Issues.Add(new ValidationIssue(ValidationSeverity.Info,
                    "Merged renderer placement: " + avatarRoot.name + "/__MeshMaterialCombiner"));
            }
            if (settings.TargetRoot != null)
                AddSourceHandlingValidation(settings.TargetRoot, entries, sourceTargets, analysis.Report.Issues);
            foreach (var conflict in MMCBuildDatabaseService.FindSourceOwnershipConflicts(
                         entries, sourceTargets, ownerRecord))
                analysis.Report.Issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    MMCBuildDatabaseService.FormatSourceOwnershipConflict(conflict),
                    conflict.SourceObject));
            AddAtlasWarnings(settings, analysis);
            return analysis;
        }

        public static GameObject Execute(OptimizerSettings settings, List<RendererEntry> entries,
            out OptimizationReport report)
        {
            return Execute(settings, entries, null, out report);
        }

        public static GameObject Execute(OptimizerSettings settings, List<RendererEntry> entries,
            IList<GameObject> sourceTargets, out OptimizationReport report)
        {
            var analysis = Analyze(settings, entries, sourceTargets);
            report = analysis.Report;
            if (analysis.Report.HasErrors) throw new InvalidOperationException("Validation failed. Resolve all errors before merging.");

            AssetOutputManager output = null;
            GameObject outputContainer = null;
            var sourceRecords = MMCBuildDatabaseService.CaptureSources(entries, sourceTargets);
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Merge Avatar Meshes and Materials");
            try
            {
                EditorUtility.DisplayProgressBar("Avatar Optimizer", "Preparing output", 0.05f);
                output = new AssetOutputManager();
                var folders = output.PrepareFolders(settings);
                var avatarRoot = AvatarRootResolver.Resolve(settings.TargetRoot);
                var baseContainerName = "__MeshMaterialCombiner";
                // Generated output always lives directly under the avatar root. Reuse the
                // existing generated directory so repeated merges do not scatter objects.
                outputContainer = avatarRoot.transform.Find(baseContainerName)?.gameObject;
                if (outputContainer == null)
                {
                    outputContainer = new GameObject(baseContainerName);
                    outputContainer.transform.SetParent(avatarRoot.transform, false);
                    Undo.RegisterCreatedObjectUndo(outputContainer, "Create optimized avatar output container");
                }
                else
                {
                    Undo.RecordObject(outputContainer, "Reuse optimized avatar output container");
                }
                outputContainer.tag = "Untagged";
                outputContainer.SetActive(true);
                var mergedBaseName = AssetOutputManager.Sanitize(
                    string.IsNullOrWhiteSpace(settings.MergedObjectName)
                        ? settings.GroupName + "_Merged"
                        : settings.MergedObjectName);
                // Never replace an existing child. Repeated builds receive a stable,
                // human-readable suffix so every generated output and its history stay
                // available for inspection and restoration.
                var mergedObjectName = GetUniqueChildName(outputContainer.transform, mergedBaseName);
                var outputObject = new GameObject(mergedObjectName);
                outputObject.tag = "Untagged";
                outputObject.SetActive(true);
                outputObject.transform.SetParent(outputContainer.transform, false);
                Undo.RegisterCreatedObjectUndo(outputObject, "Create optimized avatar renderer");

                var placement = TransformPlacementResolver.FindLowestCommonAncestor(entries,
                    settings.TargetRoot.transform);

                for (var i = 0; i < analysis.MaterialGroups.Count; i++)
                {
                    var group = analysis.MaterialGroups[i];
                    if (settings.Preset == OptimizationPreset.Safe || group.Adapter is UnmodifiedMaterialAdapter)
                    {
                        group.MergedMaterial = group.Materials[0];
                        foreach (var material in group.Materials)
                            group.AtlasRects[material] = new Rect(0f, 0f, 1f, 1f);
                        continue;
                    }
                    EditorUtility.DisplayProgressBar("Avatar Optimizer", $"Building atlas {i + 1}/{analysis.MaterialGroups.Count}",
                        0.1f + 0.3f * (i / (float)Mathf.Max(1, analysis.MaterialGroups.Count)));
                    var properties = MaterialAtlasPlanner.GetProperties(group);
                    foreach (var property in properties)
                    {
                        var runtimeAtlas = TextureAtlasBuilder.BuildTextureAtlas(group, property,
                            settings.AtlasSize);
                        var atlas = output.SaveTexture(runtimeAtlas, folders.Textures,
                            $"{GetBuildName(settings)}_Group{i}_{MaterialAtlasPlanner.GetFileSuffix(property)}",
                            property);
                        UnityEngine.Object.DestroyImmediate(runtimeAtlas);
                        group.Atlases[property.PropertyName] = atlas;
                        if (property.IsBaseTexture) group.MainAtlas = atlas;
                    }
                    group.MergedMaterial = MaterialMerger.CreateMergedMaterial(group, properties);
                    group.MergedMaterial = output.SaveMaterial(group.MergedMaterial, folders.Materials,
                        $"{GetBuildName(settings)}_Merged_{i}");
                }

                EditorUtility.DisplayProgressBar("Avatar Optimizer", "Merging meshes, bones and blend shapes", 0.5f);
                var merged = MeshMerger.Merge(entries, analysis.MaterialGroups, outputObject.transform,
                    avatarRoot, placement, settings.BlendShapeMode, settings.Preset == OptimizationPreset.Safe);
                merged.Mesh = output.SaveMesh(merged.Mesh, folders.Meshes, GetBuildName(settings));
                var renderer = Undo.AddComponent<SkinnedMeshRenderer>(outputObject);
                renderer.sharedMesh = merged.Mesh;
                renderer.sharedMaterials = merged.Materials.ToArray();
                renderer.bones = merged.Bones;
                renderer.rootBone = merged.RootBone;
                renderer.localBounds = merged.LocalBounds;
                AssetOutputManager.ApplyBlendShapeWeights(renderer, merged);

                EditorUtility.DisplayProgressBar("Avatar Optimizer", "Saving self-contained prefab", 0.85f);
                output.SavePrefab(settings, entries, merged, folders.Prefabs, placement,
                    outputContainer.transform, outputObject.name);
                var editorOnlySummary = string.Empty;
                ApplySourceObjectHandling(entries, sourceTargets, out var hidden, out var retained);
                
                report.After.Vertices = merged.Mesh.vertexCount;
                report.After.MaterialSlots = merged.Mesh.subMeshCount;
                report.After.UniqueMaterials = new HashSet<Material>(merged.Materials).Count;

                MMCBuildDatabaseService.RecordBuild(outputObject.name, avatarRoot, outputObject,
                    sourceRecords, output.CreatedAssets, settings, entries, report);
                editorOnlySummary = $" Combined+EditorOnly containers: {hidden} hidden + EditorOnly, {retained} retained.";
                AssetDatabase.SaveAssets();
                Selection.activeGameObject = outputContainer;
                report.ResultMessage = $"Created {output.CreatedAssets.Count} assets in {folders.Root}." +
                    $" Output object: {outputObject.name}." +
                    editorOnlySummary;
                Undo.CollapseUndoOperations(undoGroup);
                return outputContainer;
            }
            catch
            {
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                catch
                {
                    if (outputContainer != null) UnityEngine.Object.DestroyImmediate(outputContainer);
                }
                output?.Rollback();
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void AddAtlasWarnings(OptimizerSettings settings, OptimizationAnalysis analysis)
        {
            if (settings.AtlasSize != 1024 && settings.AtlasSize != 2048 &&
                settings.AtlasSize != StandardMaximumAtlasSize)
            {
                analysis.Report.Issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    "Atlas resolution must be 1024, 2048 or 4096px."));
                return;
            }
            if (settings.Preset != OptimizationPreset.Safe)
                AddTransformedUvWarnings(analysis.Renderers, analysis.Report.Issues);
            var atlasCount = 0;
            foreach (var group in analysis.MaterialGroups)
            {
                if (settings.Preset == OptimizationPreset.Safe || group.Adapter is UnmodifiedMaterialAdapter) continue;
                atlasCount += MaterialAtlasPlanner.GetProperties(group).Count;
                var columns = Mathf.CeilToInt(Mathf.Sqrt(group.Materials.Count));
                var rows = Mathf.CeilToInt(group.Materials.Count / (float)columns);
                var tileWidth = settings.AtlasSize / Mathf.Max(1, columns);
                var tileHeight = settings.AtlasSize / Mathf.Max(1, rows);
                foreach (var property in MaterialAtlasPlanner.GetProperties(group))
                {
                    var oversized = 0;
                    Texture largest = null;
                    foreach (var material in group.Materials)
                    {
                        var texture = property.IsBaseTexture
                            ? MaterialTextureUtility.GetBaseTexture(material)
                            : material.HasProperty(property.PropertyName)
                                ? material.GetTexture(property.PropertyName)
                                : null;
                        if (texture == null || (texture.width <= tileWidth && texture.height <= tileHeight))
                            continue;
                        oversized++;
                        if (largest == null || texture.width * texture.height > largest.width * largest.height)
                            largest = texture;
                    }
                    if (oversized > 0)
                        analysis.Report.Issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                            $"{property.DisplayName}: {oversized} source texture(s) exceed atlas tile {tileWidth}x{tileHeight}; quality may decrease. Largest: {largest.width}x{largest.height}.",
                            largest));
                }
            }
            var estimate = EstimateAtlasMemory(settings.AtlasSize, atlasCount);
            var estimatedGiB = estimate.PeakBytes / (1024d * 1024d * 1024d);
            if (estimate.PeakBytes >= AtlasMemoryHardLimitBytes)
                analysis.Report.Issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    $"Estimated peak Atlas memory is about {estimatedGiB:F1} GB " +
                    $"({atlasCount} texture(s) at {settings.AtlasSize}px). " +
                    "Processing was stopped to reduce the risk of Unity running out of memory. " +
                    "Use 4096px or reduce the number of Atlas texture properties."));
            else if (estimate.PeakBytes >= AtlasMemoryWarningBytes)
                analysis.Report.Issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                    $"High-memory Atlas processing: estimated peak memory is about {estimatedGiB:F1} GB " +
                    $"({atlasCount} texture(s) at {settings.AtlasSize}px), including full-size pixel arrays, " +
                    "temporary textures and generated Atlas textures. 4096px or lower is recommended."));
        }

        private static AtlasMemoryEstimate EstimateAtlasMemory(int atlasSize, int atlasCount)
        {
            if (atlasSize <= 0 || atlasCount <= 0) return new AtlasMemoryEstimate(atlasCount, 0L);
            try
            {
                checked
                {
                    var pixels = (long)atlasSize * atlasSize;
                    // Peak working set: Color32 atlas (4 B), two Color buffers used
                    // while scaling/copying (32 B), RenderTexture/Texture2D staging
                    // (8 B), plus approximately 8 B per retained generated atlas.
                    return new AtlasMemoryEstimate(atlasCount,
                        pixels * (44L + 8L * atlasCount));
                }
            }
            catch (OverflowException)
            {
                return new AtlasMemoryEstimate(atlasCount, long.MaxValue);
            }
        }

        private static void AddTransformedUvWarnings(IEnumerable<RendererEntry> entries,
            List<ValidationIssue> issues)
        {
            foreach (var entry in entries)
            {
                if (!entry.Included || entry.Mesh == null || entry.Renderer == null) continue;
                var uv = entry.Mesh.uv;
                if (uv == null || uv.Length != entry.Mesh.vertexCount) continue;
                var count = Mathf.Min(entry.Mesh.subMeshCount, entry.Materials.Length);
                var outside = false;
                for (var sub = 0; sub < count && !outside; sub++)
                {
                    var material = entry.Materials[sub];
                    if (material == null) continue;
                    var scale = MaterialTextureUtility.GetTextureScale(material);
                    var offset = MaterialTextureUtility.GetTextureOffset(material);
                    foreach (var vertex in entry.Mesh.GetIndices(sub))
                    {
                        if (vertex < 0 || vertex >= uv.Length) continue;
                        var transformed = Vector2.Scale(uv[vertex], scale) + offset;
                        if (transformed.x >= 0f && transformed.x <= 1f &&
                            transformed.y >= 0f && transformed.y <= 1f) continue;
                        outside = true;
                        break;
                    }
                }
                if (outside)
                    issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                        $"{entry.Renderer.name}: Base texture Scale/Offset moves UVs outside 0-1. Atlas tiles cannot preserve unrestricted Repeat sampling and may show neighboring tiles.",
                        entry.Renderer));
            }
        }

        private static void AddForceSingleSlotValidation(IEnumerable<RendererEntry> entries,
            List<ValidationIssue> issues)
        {
            foreach (var entry in entries)
            {
                if (!entry.Included || entry.Mesh == null) continue;
                var count = Mathf.Min(entry.Mesh.subMeshCount, entry.Materials.Length);
                for (var sub = 0; sub < count; sub++)
                {
                    var topology = entry.Mesh.GetTopology(sub);
                    if (topology == MeshTopology.Triangles) continue;
                    issues.Add(new ValidationIssue(ValidationSeverity.Error,
                        $"Force Single Slot requires triangle submeshes. '{entry.Mesh.name}' contains {topology} topology.",
                        entry.Mesh));
                }
            }
        }

        private static string GetBuildName(OptimizerSettings settings)
        {
            return string.IsNullOrWhiteSpace(settings.MergedObjectName)
                ? settings.GroupName + "_Merged"
                : settings.MergedObjectName;
        }

        private static string GetUniqueChildName(Transform parent, string baseName)
        {
            if (parent == null || string.IsNullOrEmpty(baseName)) return baseName;
            if (parent.Find(baseName) == null) return baseName;
            for (var index = 1; index < int.MaxValue; index++)
            {
                var candidate = baseName + " " + index;
                if (parent.Find(candidate) == null) return candidate;
            }
            throw new InvalidOperationException("Could not find a unique merged object name.");
        }

        private static void ApplySourceObjectHandling(IList<RendererEntry> entries,
            IList<GameObject> sourceTargets,
            out int hiddenCount, out int retainedCount)
        {
            hiddenCount = 0;
            retainedCount = 0;
            var processed = new HashSet<GameObject>();

            // Direct Objects are source-management targets independently of the
            // Renderer checkboxes. Only the checked entries are merged, while all
            // explicitly selected objects are hidden/EditorOnly and recorded.
            if (sourceTargets != null && sourceTargets.Count > 0)
            {
                foreach (var source in sourceTargets)
                    ApplySourceObjectHandling(source, processed, ref hiddenCount, ref retainedCount);
                return;
            }

            foreach (var entry in entries)
            {
                if (!entry.Included || entry.Renderer == null) continue;
                ApplySourceObjectHandling(entry.Renderer.gameObject, processed,
                    ref hiddenCount, ref retainedCount);
            }
        }

        internal static void ApplyRecordedSourceObjectHandling(IList<GameObject> sources,
            out int hiddenCount, out int retainedCount)
        {
            ApplySourceObjectHandling(Array.Empty<RendererEntry>(), sources,
                out hiddenCount, out retainedCount);
        }

        private static void ApplySourceObjectHandling(GameObject source,
            HashSet<GameObject> processed, ref int hiddenCount, ref int retainedCount)
        {
            if (source == null || !processed.Add(source)) return;
            if (PrefabUtility.IsPartOfPrefabInstance(source))
            {
                Undo.RecordObject(source, "Hide Prefab source and mark EditorOnly");
                source.tag = "EditorOnly";
                source.SetActive(false);
                EditorUtility.SetDirty(source);
                hiddenCount++;
                return;
            }

            // Keep every selected source together, including sources that cannot be
            // safely deactivated because of animation/constraint references.
            var container = SourceObjectOrganizer.MoveToCombinedContainer(source);
            if (container != null)
            {
                Undo.RecordObject(container.gameObject, "Hide Combined+EditorOnly container");
                container.gameObject.tag = "EditorOnly";
                container.gameObject.SetActive(false);
                EditorUtility.SetDirty(container.gameObject);
                hiddenCount++;
            }
            else
            {
                retainedCount++;
            }
        }

        private static void AddSourceHandlingValidation(GameObject targetRoot,
            IList<RendererEntry> entries, IList<GameObject> sourceTargets, List<ValidationIssue> issues)
        {
            var processed = new HashSet<GameObject>();
            var prefabSources = 0;

            // Direct Objectsモードでは、チェック ON のエントリのみならず
            // sourceTargets 全体（チェック OFF 含む）がソース処理の対象になる。
            // そのため、sourceTargetsが指定されている場合はそちらを優先して列挙する。
            if (sourceTargets != null && sourceTargets.Count > 0)
            {
                // チェック ON ソース（マージ対象）を先に列挙する
                foreach (var entry in entries)
                {
                    if (!entry.Included || entry.Renderer == null ||
                        !processed.Add(entry.Renderer.gameObject)) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(entry.Renderer.gameObject)) prefabSources++;
                    issues.Add(new ValidationIssue(ValidationSeverity.Info,
                        PrefabUtility.IsPartOfPrefabInstance(entry.Renderer.gameObject)
                            ? $"{entry.Renderer.name}: Prefab Instance hierarchy will be preserved; the source will be hidden and tagged EditorOnly in place."
                            : $"{entry.Renderer.name}: Its Combined+EditorOnly container will be hidden and tagged EditorOnly.",
                        entry.Renderer.gameObject));
                }
                // チェック OFF でもソース処理対象になる Direct Objects を追加で列挙する
                foreach (var source in sourceTargets)
                {
                    if (source == null || !processed.Add(source)) continue;
                    // このGameObjectはマージには含まれないが、非表示・EditorOnly処理は行われる。
                    // Prefab Instance かどうかを判定してカウントに含め、サマリーメッセージと一致させる。
                    var isSourcePrefab = PrefabUtility.IsPartOfPrefabInstance(source);
                    if (isSourcePrefab) prefabSources++;
                    issues.Add(new ValidationIssue(ValidationSeverity.Info,
                        isSourcePrefab
                            ? $"{source.name}: [Renderer excluded from merge] Prefab Instance will be hidden and tagged EditorOnly in place."
                            : $"{source.name}: [Renderer excluded from merge] Its Combined+EditorOnly container will be hidden and tagged EditorOnly.",
                        source));
                }
            }
            else
            {
                // Target Root モード: チェック ON のエントリのみがソース処理対象
                foreach (var entry in entries)
                {
                    if (!entry.Included || entry.Renderer == null ||
                        !processed.Add(entry.Renderer.gameObject)) continue;
                    if (PrefabUtility.IsPartOfPrefabInstance(entry.Renderer.gameObject)) prefabSources++;
                    issues.Add(new ValidationIssue(ValidationSeverity.Info,
                        PrefabUtility.IsPartOfPrefabInstance(entry.Renderer.gameObject)
                            ? $"{entry.Renderer.name}: Prefab Instance hierarchy will be preserved; the source will be hidden and tagged EditorOnly in place."
                            : $"{entry.Renderer.name}: Its Combined+EditorOnly container will be hidden and tagged EditorOnly.",
                        entry.Renderer.gameObject));
                }
            }

            issues.Add(new ValidationIssue(ValidationSeverity.Info,
                prefabSources > 0
                    ? $"Source handling preview: {prefabSources} Prefab Instance source(s) will remain in their original hierarchy. Build links are tracked by the editor database."
                    : "Source handling preview: all selected sources will be placed under hidden, EditorOnly Combined+EditorOnly containers."));
        }

        private static int CountOutputSubmeshes(IEnumerable<RendererEntry> entries, IList<MaterialGroup> groups)
        {
            var groupByMaterial = new Dictionary<Material, int>();
            foreach (var group in groups)
                foreach (var material in group.Materials) groupByMaterial[material] = group.Index;
            var keys = new HashSet<SubMeshMerger.Key>();
            foreach (var entry in entries)
            {
                if (!entry.Included || entry.Mesh == null) continue;
                var count = Mathf.Min(entry.Mesh.subMeshCount, entry.Materials.Length);
                for (var sub = 0; sub < count; sub++)
                    if (entry.Materials[sub] != null && groupByMaterial.TryGetValue(entry.Materials[sub], out var group))
                        keys.Add(new SubMeshMerger.Key(group, entry.Mesh.GetTopology(sub)));
            }
            return keys.Count;
        }
    }
}
