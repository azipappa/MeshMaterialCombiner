using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal enum MMCRebuildState
    {
        UpToDate,
        RecipeMissing,
        SourcesMissing,
        SceneNotLoaded,
        SourceConflict
    }

    internal sealed class MMCRebuildEvaluation
    {
        public MMCRebuildState State;
        public string CurrentHash = string.Empty;
        public string Message = string.Empty;
    }

    internal static class MMCRebuildService
    {
        private const int RecipeVersion = 2;

        public static MMCBuildRecipe CaptureRecipe(OptimizerSettings settings, IList<RendererEntry> entries)
        {
            var recipe = new MMCBuildRecipe
            {
                Version = RecipeVersion,
                Preset = settings != null
                    ? OptimizationPresetUtility.Normalize(settings.Preset)
                    : OptimizationPreset.Safe,
                ForceRepresentativeMaterial = settings != null ? settings.ForceRepresentativeMaterial : null,
                AtlasSize = settings != null ? settings.AtlasSize : 2048,
                BlendShapeMode = settings != null
                    ? settings.BlendShapeMode
                    : BlendShapeCollisionMode.PrefixByRendererName,
                OutputName = settings != null ? settings.MergedObjectName ?? string.Empty : string.Empty
            };
            if (entries == null) return recipe;

            var seen = new HashSet<Renderer>();
            foreach (var entry in entries)
            {
                var renderer = entry != null ? entry.Renderer : null;
                if (entry == null || !entry.Included || renderer == null || !seen.Add(renderer)) continue;
                var type = renderer is SkinnedMeshRenderer ? typeof(SkinnedMeshRenderer) : typeof(MeshRenderer);
                var components = renderer.gameObject.GetComponents(type);
                var componentIndex = 0;
                for (var i = 0; i < components.Length; i++)
                    if (components[i] == renderer)
                    {
                        componentIndex = i;
                        break;
                    }
                recipe.Renderers.Add(new MMCRendererLocator
                {
                    Object = MMCBuildDatabaseService.CaptureObject(renderer.gameObject),
                    RendererType = type.Name,
                    ComponentIndex = componentIndex,
                    DisplayName = renderer.name
                });
            }
            return recipe;
        }

        public static string ComputeInputHash(MMCBuildRecipe recipe, out List<string> dependencyGuids)
        {
            return ComputeInputHashInternal(recipe, true, out dependencyGuids);
        }

        public static string ComputeFastInputHash(MMCBuildRecipe recipe)
        {
            return ComputeInputHashInternal(recipe, false, out _);
        }

        private static string ComputeInputHashInternal(MMCBuildRecipe recipe, bool includeDependencies,
            out List<string> dependencyGuids)
        {
            dependencyGuids = new List<string>();
            if (recipe == null || !recipe.IsValid) return string.Empty;
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder(2048);
            builder.Append(recipe.Version).Append('|').Append((int)recipe.Preset).Append('|')
                .Append(recipe.AtlasSize).Append('|').Append((int)recipe.BlendShapeMode).Append('|');
            AppendAsset(builder, recipe.ForceRepresentativeMaterial, dependencies, includeDependencies);

            foreach (var locator in recipe.Renderers)
            {
                var renderer = ResolveRenderer(locator);
                builder.Append(locator.RendererType).Append('#').Append(locator.ComponentIndex).Append('|');
                if (renderer == null)
                {
                    builder.Append("<missing>|");
                    continue;
                }

                AppendTransform(builder, renderer.transform);
                var mesh = GetMesh(renderer);
                AppendAsset(builder, mesh, dependencies, includeDependencies);
                if (mesh != null)
                    builder.Append(mesh.vertexCount).Append(':').Append(mesh.subMeshCount).Append(':')
                        .Append(mesh.blendShapeCount).Append('|');
                var materials = renderer.sharedMaterials ?? Array.Empty<Material>();
                builder.Append(materials.Length).Append('|');
                foreach (var material in materials)
                    AppendAsset(builder, material, dependencies, includeDependencies);

                if (renderer is SkinnedMeshRenderer skinned)
                {
                    builder.Append(skinned.bones != null ? skinned.bones.Length : 0).Append('|');
                    if (skinned.bones != null)
                        foreach (var bone in skinned.bones)
                            builder.Append(bone != null ? GetHierarchyPath(bone) : "<null>").Append('|');
                    builder.Append(skinned.rootBone != null ? GetHierarchyPath(skinned.rootBone) : "<null>")
                        .Append('|');
                }

                var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject);
                AppendAssetPath(builder, prefabPath, dependencies, includeDependencies);
            }

            dependencyGuids.AddRange(dependencies);
            dependencyGuids.Sort(StringComparer.Ordinal);
            return Hash128.Compute(builder.ToString()).ToString();
        }

        public static MMCRebuildEvaluation Evaluate(MMCBuildRecord record)
        {
            return Evaluate(record, false, null);
        }

        internal static MMCRebuildEvaluation Evaluate(MMCBuildRecord record,
            bool ownershipConflictChecked, MMCSourceOwnershipConflict ownershipConflict)
        {
            if (record == null || record.Recipe == null || !record.Recipe.IsValid)
                return new MMCRebuildEvaluation
                {
                    State = MMCRebuildState.RecipeMissing,
                    Message = OptimizerLocalization.T("Rebuild Recipe Missing")
                };

            if (record.AvatarRoot != null && !string.IsNullOrEmpty(record.AvatarRoot.ScenePath))
            {
                var avatar = MMCBuildDatabaseService.ResolveAvatarRoot(record);
                if (avatar == null && AssetDatabase.LoadAssetAtPath<SceneAsset>(record.AvatarRoot.ScenePath) != null)
                    return new MMCRebuildEvaluation
                    {
                        State = MMCRebuildState.SceneNotLoaded,
                        Message = OptimizerLocalization.T("Rebuild Scene Not Loaded")
                    };
            }

            if (!MMCBuildDatabaseService.ValidateManagedObjectBoundaries(record,
                    out var boundaryError))
                return new MMCRebuildEvaluation
                {
                    State = MMCRebuildState.SourcesMissing,
                    Message = boundaryError
                };

            if (!TryResolveEntries(record.Recipe, out _, out var resolveError))
                return new MMCRebuildEvaluation
                {
                    State = MMCRebuildState.SourcesMissing,
                    Message = resolveError
                };

            if ((!ownershipConflictChecked &&
                 MMCBuildDatabaseService.TryFindSourceOwnershipConflict(record, out ownershipConflict)) ||
                (ownershipConflictChecked && ownershipConflict != null))
                return new MMCRebuildEvaluation
                {
                    State = MMCRebuildState.SourceConflict,
                    Message = MMCBuildDatabaseService.FormatSourceOwnershipConflict(ownershipConflict)
                };

            return new MMCRebuildEvaluation
            {
                State = MMCRebuildState.UpToDate,
                CurrentHash = record.FastInputHash ?? string.Empty,
                Message = OptimizerLocalization.T("Rebuild Up To Date")
            };
        }

        public static bool IsRelatedToRoot(MMCBuildRecord record, GameObject root)
        {
            if (record == null || root == null) return true;
            var avatar = MMCBuildDatabaseService.ResolveAvatarRoot(record);
            // Selecting the avatar (or one of its ancestors) includes every build.
            // Selecting clothing below the avatar must only match histories whose
            // sources actually belong to that clothing hierarchy.
            if (IsWithin(avatar, root)) return true;

            if (record.Sources != null)
                foreach (var sourceRecord in record.Sources)
                {
                    var source = MMCBuildDatabaseService.ResolveSource(sourceRecord);
                    if (IsWithin(source, root)) return true;
                    var originalParent = MMCBuildDatabaseService.ResolveObject(sourceRecord.OriginalParent);
                    if (IsWithin(originalParent, root)) return true;
                    if (LocatorWasBelowRoot(sourceRecord.OriginalParent, root)) return true;
                }

            if (record.Recipe != null && record.Recipe.Renderers != null)
                foreach (var rendererLocator in record.Recipe.Renderers)
                    if (IsWithin(MMCBuildDatabaseService.ResolveObject(rendererLocator.Object), root)) return true;
            return false;
        }

        public static bool Rebuild(MMCBuildRecord record, out string message)
        {
            message = string.Empty;
            var database = MMCBuildDatabaseService.Load();
            if (database == null || record == null || !database.Builds.Contains(record))
            {
                message = "The selected build record no longer exists.";
                return false;
            }
            if (record.Recipe == null || !record.Recipe.IsValid)
            {
                message = OptimizerLocalization.T("Rebuild Recipe Missing");
                return false;
            }
            if (record.Lifecycle == MMCBuildLifecycle.CleanupPending)
            {
                message = OptimizerLocalization.T("Cleanup Pending Cannot Rebuild");
                return false;
            }
            var avatarRoot = MMCBuildDatabaseService.ResolveAvatarRoot(record);
            if (avatarRoot == null)
            {
                message = OptimizerLocalization.T("Rebuild Scene Not Loaded");
                return false;
            }
            if (!MMCBuildDatabaseService.ValidateManagedObjectBoundaries(record, out message))
                return false;
            if (record.Lifecycle == MMCBuildLifecycle.SuspendedForEditing &&
                MMCBuildDatabaseService.ResolveOutput(record) == null)
            {
                message = OptimizerLocalization.T("Suspend Output Missing");
                return false;
            }
            if (MMCBuildDatabaseService.HasUnavailableManagedDependency(record,
                    out var dependencyName))
            {
                message = string.Format(OptimizerLocalization.T("Rebuild Dependency Suspended"), dependencyName);
                return false;
            }
            if (!TryResolveEntries(record.Recipe, out var entries, out message)) return false;
            if (MMCBuildDatabaseService.TryFindSourceOwnershipConflict(record,
                    out var ownershipConflict))
            {
                message = MMCBuildDatabaseService.FormatSourceOwnershipConflict(ownershipConflict);
                return false;
            }

            var settings = new OptimizerSettings
            {
                TargetRoot = avatarRoot,
                GroupName = string.IsNullOrEmpty(record.BuildName) ? avatarRoot.name : record.BuildName,
                MergedObjectName = string.IsNullOrEmpty(record.Recipe.OutputName)
                    ? record.BuildName
                    : record.Recipe.OutputName,
                AtlasSize = record.Recipe.AtlasSize,
                Preset = OptimizationPresetUtility.Normalize(record.Recipe.Preset),
                ForceRepresentativeMaterial = record.Recipe.ForceRepresentativeMaterial,
                BlendShapeMode = record.Recipe.BlendShapeMode
            };
            var analysis = OptimizationPipeline.Analyze(settings, entries, null, record);
            if (analysis.Report.HasErrors)
            {
                message = OptimizerLocalization.T("Rebuild Validation Failed");
                return false;
            }

            AssetOutputManager output = null;
            GameObject createdOutput = null;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild Mesh Material Combiner output");
            try
            {
                var wasSuspended = record.Lifecycle == MMCBuildLifecycle.SuspendedForEditing;
                var restoredSources = new List<GameObject>();
                if (wasSuspended)
                {
                    Undo.RecordObject(database, "Capture edited MMC source state");
                    if (!MMCBuildDatabaseService.CaptureCurrentSourceBaseline(record,
                            out restoredSources, out message))
                        throw new InvalidOperationException(message);
                }
                EditorUtility.DisplayProgressBar("Mesh Material Combiner", "Preparing rebuild", 0.05f);
                output = new AssetOutputManager();
                var folders = output.PrepareFolders(settings);
                var outputObject = MMCBuildDatabaseService.ResolveOutput(record);
                var container = avatarRoot.transform.Find("__MeshMaterialCombiner");
                if (container == null)
                {
                    var containerObject = new GameObject("__MeshMaterialCombiner");
                    containerObject.transform.SetParent(avatarRoot.transform, false);
                    Undo.RegisterCreatedObjectUndo(containerObject, "Create MMC output container");
                    container = containerObject.transform;
                }
                if (outputObject == null)
                {
                    outputObject = new GameObject(string.IsNullOrEmpty(record.BuildName)
                        ? "Rebuilt_Merged"
                        : record.BuildName);
                    outputObject.transform.SetParent(container, false);
                    Undo.RegisterCreatedObjectUndo(outputObject, "Recreate merged output");
                    createdOutput = outputObject;
                }

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
                    EditorUtility.DisplayProgressBar("Mesh Material Combiner", "Building material atlas", 0.2f);
                    var properties = MaterialAtlasPlanner.GetProperties(group);
                    foreach (var property in properties)
                    {
                        var runtimeAtlas = TextureAtlasBuilder.BuildTextureAtlas(group, property,
                            settings.AtlasSize);
                        var atlas = output.SaveTexture(runtimeAtlas, folders.Textures,
                            AssetOutputManager.Sanitize(record.BuildName) + "_Group" + i + "_" +
                            MaterialAtlasPlanner.GetFileSuffix(property), property);
                        UnityEngine.Object.DestroyImmediate(runtimeAtlas);
                        group.Atlases[property.PropertyName] = atlas;
                        if (property.IsBaseTexture) group.MainAtlas = atlas;
                    }
                    group.MergedMaterial = MaterialMerger.CreateMergedMaterial(group, properties);
                    group.MergedMaterial = output.SaveMaterial(group.MergedMaterial, folders.Materials,
                        AssetOutputManager.Sanitize(record.BuildName) + "_Merged_" + i);
                }

                EditorUtility.DisplayProgressBar("Mesh Material Combiner", "Merging updated sources", 0.55f);
                var placement = TransformPlacementResolver.FindLowestCommonAncestor(entries, avatarRoot.transform);
                var merged = MeshMerger.Merge(entries, analysis.MaterialGroups, outputObject.transform,
                    avatarRoot, placement, settings.BlendShapeMode,
                    settings.Preset == OptimizationPreset.Safe);
                merged.Mesh = output.SaveMesh(merged.Mesh, folders.Meshes,
                    AssetOutputManager.Sanitize(record.BuildName));

                EditorUtility.DisplayProgressBar("Mesh Material Combiner", "Saving rebuild prefab", 0.78f);
                output.SavePrefab(settings, entries, merged, folders.Prefabs, placement, container,
                    outputObject.name);

                var renderer = outputObject.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null) renderer = Undo.AddComponent<SkinnedMeshRenderer>(outputObject);
                else Undo.RecordObject(renderer, "Apply rebuilt mesh");
                renderer.sharedMesh = merged.Mesh;
                renderer.sharedMaterials = merged.Materials.ToArray();
                renderer.bones = merged.Bones;
                renderer.rootBone = merged.RootBone;
                renderer.localBounds = merged.LocalBounds;
                AssetOutputManager.ApplyBlendShapeWeights(renderer, merged);
                EditorUtility.SetDirty(renderer);

                if (wasSuspended)
                {
                    OptimizationPipeline.ApplyRecordedSourceObjectHandling(restoredSources,
                        out _, out _);
                    MMCBuildDatabaseService.RefreshSourceLocators(record, restoredSources);
                    Undo.RecordObject(outputObject, "Reactivate rebuilt MMC output");
                    record.Lifecycle = MMCBuildLifecycle.Built;
                }

                // A successfully rebuilt output is an active merge result, so it
                // must not remain excluded from avatar processing by EditorOnly.
                Undo.RecordObject(outputObject, "Activate rebuilt MMC output");
                outputObject.SetActive(true);
                outputObject.tag = "Untagged";
                Undo.RecordObject(renderer, "Enable rebuilt MMC renderer");
                renderer.enabled = true;
                EditorUtility.SetDirty(outputObject);
                EditorUtility.SetDirty(renderer);

                var rebuiltAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                // Persist the safe replacement after a successful rebuild so old
                // Aggressive recipes do not remain in the database indefinitely.
                record.Recipe.Preset = settings.Preset;
                var inputHash = ComputeInputHash(record.Recipe, out var dependencyGuids);
                Undo.RecordObject(database, "Update MMC rebuild history");
                record.OutputObject = MMCBuildDatabaseService.CaptureObject(outputObject);
                record.BuildName = outputObject.name;
                
                analysis.Report.After.Vertices = merged.Mesh.vertexCount;
                analysis.Report.After.MaterialSlots = merged.Mesh.subMeshCount;
                analysis.Report.After.UniqueMaterials = new HashSet<Material>(merged.Materials).Count;
                
                record.SavedBeforeStats = analysis.Report.Before;
                record.SavedAfterStats = analysis.Report.After;
                record.LastBuiltAt = rebuiltAt;
                record.CurrentInputHash = inputHash;
                record.FastInputHash = ComputeFastInputHash(record.Recipe);
                record.DependencyGuids = dependencyGuids;
                record.GeneratedAssetPaths.Clear();
                foreach (var path in output.CreatedAssets) record.GeneratedAssetPaths.Add(path);
                if (record.GeneratedAssets == null)
                    record.GeneratedAssets = new List<MMCGeneratedAssetReference>();
                MMCBuildDatabaseService.PopulateGeneratedAssetReferences(record.GeneratedAssetPaths,
                    record.GeneratedAssets);
                if (record.Revisions == null) record.Revisions = new List<MMCBuildRevision>();
                record.Revisions.Add(CaptureRevision(renderer, avatarRoot, inputHash,
                    ComputeFastInputHash(record.Recipe),
                    record.GeneratedAssetPaths, rebuiltAt));
                record.ActiveRevisionIndex = record.Revisions.Count - 1;
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                message = OptimizerLocalization.T("Rebuild Completed");
                return true;
            }
            catch (Exception exception)
            {
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                catch
                {
                    if (createdOutput != null) UnityEngine.Object.DestroyImmediate(createdOutput);
                }
                output?.Rollback();
                message = exception.Message;
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static MMCBuildRevision CaptureRevision(SkinnedMeshRenderer renderer, GameObject avatarRoot,
            string inputHash, string fastInputHash, IList<string> generatedAssetPaths, string createdAt)
        {
            var revision = new MMCBuildRevision
            {
                RevisionId = Guid.NewGuid().ToString("N"),
                CreatedAt = createdAt ?? string.Empty,
                InputHash = inputHash ?? string.Empty,
                FastInputHash = fastInputHash ?? string.Empty
            };
            if (generatedAssetPaths != null)
                foreach (var path in generatedAssetPaths) revision.GeneratedAssetPaths.Add(path);
            MMCBuildDatabaseService.PopulateGeneratedAssetReferences(revision.GeneratedAssetPaths,
                revision.GeneratedAssets);
            if (renderer == null) return revision;
            revision.MeshAssetPath = AssetDatabase.GetAssetPath(renderer.sharedMesh);
            foreach (var material in renderer.sharedMaterials ?? Array.Empty<Material>())
                revision.MaterialAssetPaths.Add(AssetDatabase.GetAssetPath(material));
            var avatarTransform = avatarRoot != null ? avatarRoot.transform : null;
            foreach (var bone in renderer.bones ?? Array.Empty<Transform>())
            {
                revision.BonePaths.Add(GetRelativePath(avatarTransform, bone));
                revision.BoneObjects.Add(MMCBuildDatabaseService.CaptureObject(
                    bone != null ? bone.gameObject : null));
            }
            revision.RootBonePath = GetRelativePath(avatarTransform, renderer.rootBone);
            revision.RootBoneObject = MMCBuildDatabaseService.CaptureObject(
                renderer.rootBone != null ? renderer.rootBone.gameObject : null);
            revision.LocalBounds = renderer.localBounds;
            if (renderer.sharedMesh != null)
                for (var i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                {
                    revision.BlendShapeNames.Add(renderer.sharedMesh.GetBlendShapeName(i));
                    revision.BlendShapeWeights.Add(renderer.GetBlendShapeWeight(i));
                }
            return revision;
        }

        public static bool RestorePreviousRevision(MMCBuildRecord record, out string message)
        {
            if (record == null || record.Revisions == null || record.ActiveRevisionIndex <= 0 ||
                record.ActiveRevisionIndex >= record.Revisions.Count)
            {
                message = OptimizerLocalization.T("No Previous Revision");
                return false;
            }
            return RestoreRevision(record, record.ActiveRevisionIndex - 1, out message);
        }

        public static bool RestoreNextRevision(MMCBuildRecord record, out string message)
        {
            if (record == null || record.Revisions == null || record.ActiveRevisionIndex < 0 ||
                record.ActiveRevisionIndex >= record.Revisions.Count - 1)
            {
                message = OptimizerLocalization.T("No Next Revision");
                return false;
            }
            return RestoreRevision(record, record.ActiveRevisionIndex + 1, out message);
        }

        private static bool RestoreRevision(MMCBuildRecord record, int targetIndex, out string message)
        {
            message = string.Empty;
            var database = MMCBuildDatabaseService.Load();
            if (!MMCBuildDatabaseService.ValidateManagedObjectBoundaries(record, out message))
                return false;
            var output = MMCBuildDatabaseService.ResolveOutput(record);
            var avatar = MMCBuildDatabaseService.ResolveAvatarRoot(record);
            var renderer = output != null ? output.GetComponent<SkinnedMeshRenderer>() : null;
            if (database == null || renderer == null || avatar == null)
            {
                message = OptimizerLocalization.T("Revision Restore Failed");
                return false;
            }
            var revision = record.Revisions[targetIndex];
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(revision.MeshAssetPath);
            if (mesh == null)
            {
                message = OptimizerLocalization.T("Revision Assets Missing");
                return false;
            }
            var materials = new List<Material>();
            foreach (var path in revision.MaterialAssetPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    message = OptimizerLocalization.T("Revision Assets Missing");
                    return false;
                }
                materials.Add(material);
            }

            Undo.RecordObject(renderer, "Restore previous MMC revision");
            Undo.RecordObject(database, "Restore previous MMC revision history");
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials.ToArray();
            var bones = new Transform[revision.BonePaths.Count];
            for (var i = 0; i < bones.Length; i++)
            {
                var boneObject = revision.BoneObjects != null && i < revision.BoneObjects.Count
                    ? MMCBuildDatabaseService.ResolveObject(revision.BoneObjects[i])
                    : null;
                bones[i] = boneObject != null
                    ? boneObject.transform
                    : ResolveRelativePath(avatar.transform, revision.BonePaths[i]);
            }
            renderer.bones = bones;
            var rootBoneObject = MMCBuildDatabaseService.ResolveObject(revision.RootBoneObject);
            renderer.rootBone = rootBoneObject != null
                ? rootBoneObject.transform
                : ResolveRelativePath(avatar.transform, revision.RootBonePath);
            renderer.localBounds = revision.LocalBounds;
            for (var i = 0; i < revision.BlendShapeNames.Count && i < revision.BlendShapeWeights.Count; i++)
            {
                var shapeIndex = mesh.GetBlendShapeIndex(revision.BlendShapeNames[i]);
                if (shapeIndex >= 0) renderer.SetBlendShapeWeight(shapeIndex, revision.BlendShapeWeights[i]);
            }
            record.ActiveRevisionIndex = targetIndex;
            record.CurrentInputHash = revision.InputHash;
            record.FastInputHash = revision.FastInputHash;
            record.GeneratedAssetPaths = new List<string>(revision.GeneratedAssetPaths);
            record.GeneratedAssets = revision.GeneratedAssets != null
                ? new List<MMCGeneratedAssetReference>(revision.GeneratedAssets)
                : new List<MMCGeneratedAssetReference>();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            message = OptimizerLocalization.T("Revision Restored");
            return true;
        }

        public static bool TryResolveEntries(MMCBuildRecipe recipe, out List<RendererEntry> entries,
            out string error)
        {
            entries = new List<RendererEntry>();
            error = string.Empty;
            if (recipe == null || !recipe.IsValid)
            {
                error = OptimizerLocalization.T("Rebuild Recipe Missing");
                return false;
            }
            var seen = new HashSet<Renderer>();
            foreach (var locator in recipe.Renderers)
            {
                var renderer = ResolveRenderer(locator);
                if (renderer == null)
                {
                    error = OptimizerLocalization.T("Rebuild Source Missing") + ": " + locator.DisplayName;
                    return false;
                }
                if (!seen.Add(renderer)) continue;
                var mesh = GetMesh(renderer);
                if (mesh == null)
                {
                    error = OptimizerLocalization.T("Rebuild Mesh Missing") + ": " + renderer.name;
                    return false;
                }
                entries.Add(new RendererEntry
                {
                    Renderer = renderer,
                    Mesh = mesh,
                    Included = true,
                    Materials = renderer.sharedMaterials ?? Array.Empty<Material>(),
                    MaterialSlotCount = renderer.sharedMaterials != null ? renderer.sharedMaterials.Length : 0,
                    VertexCount = mesh.vertexCount,
                    TriangleCount = MeshAnalyzer.CountTriangles(mesh)
                });
            }
            if (entries.Count > 0) return true;
            error = OptimizerLocalization.T("Rebuild Source Missing");
            return false;
        }

        public static List<MMCBuildRecord> GetRebuildOrder(IList<MMCBuildRecord> records,
            out string error)
        {
            error = string.Empty;
            var result = new List<MMCBuildRecord>();
            if (records == null) return result;
            var candidates = new List<MMCBuildRecord>();
            var outputOwners = new Dictionary<GameObject, MMCBuildRecord>();
            var database = MMCBuildDatabaseService.Load();
            var allRecords = database != null && database.Builds != null
                ? database.Builds
                : new List<MMCBuildRecord>(records);
            foreach (var record in allRecords)
            {
                if (record == null || record.Recipe == null || !record.Recipe.IsValid) continue;
                var output = MMCBuildDatabaseService.ResolveOutput(record);
                if (output != null) outputOwners[output] = record;
            }
            foreach (var record in records)
                if (record != null && record.Recipe != null && record.Recipe.IsValid &&
                    !candidates.Contains(record)) candidates.Add(record);

            // Include upstream MMC outputs even when the root filter did not display
            // them. Rebuilding a dependent output before its generated source would
            // immediately leave the dependent output stale again.
            for (var index = 0; index < candidates.Count; index++)
            {
                var record = candidates[index];
                foreach (var locator in record.Recipe.Renderers)
                {
                    var source = MMCBuildDatabaseService.ResolveObject(locator.Object);
                    if (source != null && outputOwners.TryGetValue(source, out var owner) &&
                        owner != record && !candidates.Contains(owner)) candidates.Add(owner);
                }
            }
            var dependencies = new Dictionary<MMCBuildRecord, HashSet<MMCBuildRecord>>();
            foreach (var record in candidates)
            {
                var set = new HashSet<MMCBuildRecord>();
                foreach (var locator in record.Recipe.Renderers)
                {
                    var source = MMCBuildDatabaseService.ResolveObject(locator.Object);
                    if (source != null && outputOwners.TryGetValue(source, out var owner) && owner != record)
                        set.Add(owner);
                }
                dependencies[record] = set;
            }
            while (result.Count < candidates.Count)
            {
                var added = false;
                foreach (var record in candidates)
                {
                    if (result.Contains(record)) continue;
                    var ready = true;
                    foreach (var dependency in dependencies[record])
                        if (!result.Contains(dependency))
                        {
                            ready = false;
                            break;
                        }
                    if (!ready) continue;
                    result.Add(record);
                    added = true;
                }
                if (added) continue;
                error = OptimizerLocalization.T("Rebuild Dependency Cycle");
                result.Clear();
                return result;
            }
            return result;
        }

        private static Renderer ResolveRenderer(MMCRendererLocator locator)
        {
            if (locator == null) return null;
            var gameObject = MMCBuildDatabaseService.ResolveObject(locator.Object);
            if (gameObject == null) return null;
            var type = locator.RendererType == nameof(SkinnedMeshRenderer)
                ? typeof(SkinnedMeshRenderer)
                : typeof(MeshRenderer);
            var components = gameObject.GetComponents(type);
            return locator.ComponentIndex >= 0 && locator.ComponentIndex < components.Length
                ? components[locator.ComponentIndex] as Renderer
                : null;
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            return filter != null ? filter.sharedMesh : null;
        }

        private static void AppendAsset(StringBuilder builder, UnityEngine.Object asset,
            HashSet<string> dependencies, bool includeDependencies)
        {
            if (asset == null)
            {
                builder.Append("<null>|");
                return;
            }
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId))
                builder.Append(guid).Append(':').Append(localId).Append('|');
            else
                builder.Append(asset.name).Append('|');
            AppendAssetPath(builder, AssetDatabase.GetAssetPath(asset), dependencies, includeDependencies);
        }

        private static void AppendAssetPath(StringBuilder builder, string path,
            HashSet<string> dependencies, bool includeDependencies)
        {
            if (string.IsNullOrEmpty(path)) return;
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid)) dependencies.Add(guid);
            builder.Append(path).Append('|');
            if (!includeDependencies) return;
            builder.Append(AssetDatabase.GetAssetDependencyHash(path)).Append('|');
            foreach (var dependencyPath in AssetDatabase.GetDependencies(path, true))
            {
                var dependencyGuid = AssetDatabase.AssetPathToGUID(dependencyPath);
                if (!string.IsNullOrEmpty(dependencyGuid)) dependencies.Add(dependencyGuid);
            }
        }

        private static void AppendTransform(StringBuilder builder, Transform transform)
        {
            if (transform == null) return;
            AppendVector(builder, transform.localPosition);
            AppendVector(builder, transform.localEulerAngles);
            AppendVector(builder, transform.localScale);
        }

        private static void AppendVector(StringBuilder builder, Vector3 value)
        {
            builder.Append(value.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                .Append(value.z.ToString("R", CultureInfo.InvariantCulture)).Append('|');
        }

        private static bool IsWithin(GameObject candidate, GameObject root)
        {
            return candidate != null && root != null &&
                   (candidate == root || candidate.transform.IsChildOf(root.transform));
        }

        private static bool LocatorWasBelowRoot(MMCObjectLocator locator, GameObject root)
        {
            if (locator == null || root == null || !root.scene.IsValid()) return false;
            if (!string.Equals(locator.ScenePath, root.scene.path, StringComparison.Ordinal)) return false;
            var rootPath = GetHierarchyPath(root.transform);
            return string.Equals(locator.HierarchyPath, rootPath, StringComparison.Ordinal) ||
                   (!string.IsNullOrEmpty(rootPath) && locator.HierarchyPath != null &&
                    locator.HierarchyPath.StartsWith(rootPath + "/", StringComparison.Ordinal));
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            var names = new List<string>();
            for (var current = transform; current != null; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == null || target == null) return string.Empty;
            if (target == root) return string.Empty;
            if (!target.IsChildOf(root)) return string.Empty;
            return AnimationUtility.CalculateTransformPath(target, root);
        }

        private static Transform ResolveRelativePath(Transform root, string path)
        {
            if (root == null) return null;
            return string.IsNullOrEmpty(path) ? root : root.Find(path);
        }
    }

}
