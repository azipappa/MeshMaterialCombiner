using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvatarMeshMaterialOptimizer
{
    [Serializable]
    internal sealed class MMCObjectLocator
    {
        public string GlobalObjectId = string.Empty;
        public string ScenePath = string.Empty;
        public string HierarchyPath = string.Empty;
        public string DisplayName = string.Empty;
    }

    [Serializable]
    internal sealed class MMCBuildSourceRecord
    {
        public MMCObjectLocator Object = new MMCObjectLocator();
        public MMCObjectLocator OriginalParent = new MMCObjectLocator();
        public bool OriginalActiveSelf;
        public string OriginalTag = "Untagged";
        public int OriginalSiblingIndex;
        public int OriginalDepth;
        public MMCSourceOwnershipScope OwnershipScope = MMCSourceOwnershipScope.Legacy;

        [NonSerialized] public GameObject RuntimeObject;
    }

    internal enum MMCSourceOwnershipScope
    {
        Legacy = 0,
        RendererOnly = 1,
        WholeHierarchy = 2,
        RetainedOnly = 3
    }

    internal sealed class MMCSourceOwnershipConflict
    {
        public MMCBuildRecord Record;
        public MMCBuildRecord OtherRecord;
        public GameObject SourceObject;
    }

    [Serializable]
    internal sealed class MMCRendererLocator
    {
        public MMCObjectLocator Object = new MMCObjectLocator();
        public string RendererType = string.Empty;
        public int ComponentIndex;
        public string DisplayName = string.Empty;
    }

    [Serializable]
    internal sealed class MMCBuildRecipe
    {
        public int Version;
        public OptimizationPreset Preset = OptimizationPreset.Safe;
        public Material ForceRepresentativeMaterial;
        public int AtlasSize = 2048;
        public BlendShapeCollisionMode BlendShapeMode = BlendShapeCollisionMode.PrefixByRendererName;
        public string OutputName = string.Empty;
        public List<MMCRendererLocator> Renderers = new List<MMCRendererLocator>();

        public bool IsValid => Version > 0 && Renderers != null && Renderers.Count > 0;
    }

    [Serializable]
    internal sealed class MMCGeneratedAssetReference
    {
        public string Path = string.Empty;
        public string Guid = string.Empty;
    }

    [Serializable]
    internal sealed class MMCBuildRevision
    {
        public string RevisionId = string.Empty;
        public string CreatedAt = string.Empty;
        public string InputHash = string.Empty;
        public string FastInputHash = string.Empty;
        public string MeshAssetPath = string.Empty;
        public List<string> MaterialAssetPaths = new List<string>();
        public List<string> GeneratedAssetPaths = new List<string>();
        public List<MMCGeneratedAssetReference> GeneratedAssets = new List<MMCGeneratedAssetReference>();
        public List<string> BonePaths = new List<string>();
        public string RootBonePath = string.Empty;
        public List<MMCObjectLocator> BoneObjects = new List<MMCObjectLocator>();
        public MMCObjectLocator RootBoneObject = new MMCObjectLocator();
        public Bounds LocalBounds;
        public List<string> BlendShapeNames = new List<string>();
        public List<float> BlendShapeWeights = new List<float>();
    }

    [Serializable]
    internal sealed class MMCBuildRecord
    {
        public string BuildId = string.Empty;
        public string BuildName = string.Empty;
        public string CreatedAt = string.Empty;
        public MMCObjectLocator AvatarRoot = new MMCObjectLocator();
        public MMCObjectLocator OutputObject = new MMCObjectLocator();
        public List<MMCBuildSourceRecord> Sources = new List<MMCBuildSourceRecord>();
        public List<string> GeneratedAssetPaths = new List<string>();
        public List<MMCGeneratedAssetReference> GeneratedAssets = new List<MMCGeneratedAssetReference>();
        public MMCBuildRecipe Recipe = new MMCBuildRecipe();
        public string CurrentInputHash = string.Empty;
        public string FastInputHash = string.Empty;
        public List<string> DependencyGuids = new List<string>();
        public string LastBuiltAt = string.Empty;
        public List<MMCBuildRevision> Revisions = new List<MMCBuildRevision>();
        public int ActiveRevisionIndex = -1;
        public OptimizationStatistics SavedBeforeStats;
        public OptimizationStatistics SavedAfterStats;
        public MMCBuildLifecycle Lifecycle = MMCBuildLifecycle.Built;
    }

    internal enum MMCBuildLifecycle
    {
        Built,
        SuspendedForEditing,
        CleanupPending
    }

    internal sealed class MMCBuildDatabase : ScriptableObject
    {
        public List<MMCBuildRecord> Builds = new List<MMCBuildRecord>();
    }

    internal enum MMCBuildStatus
    {
        Valid,
        OutputDisabled,
        OutputRendererDisabled,
        OutputMissing,
        SourceMissing,
        SourceParentMissing,
        SceneNotLoaded,
        Modified
    }

    internal sealed class MMCBuildResolvedState
    {
        public GameObject Output;
        public readonly List<GameObject> Sources = new List<GameObject>();
        public readonly List<GameObject> AvailableSources = new List<GameObject>();
        public MMCBuildStatus Status;
    }

    internal static class MMCBuildDatabaseService
    {
        public const string DatabasePath = OptimizerSettings.DefaultOutputFolder + "/MMCBuildDatabase.asset";
        private const string LegacyDatabasePath = OptimizerSettings.LegacyOutputFolder + "/MMCBuildDatabase.asset";

        public static MMCBuildDatabase Load()
        {
            var database = AssetDatabase.LoadAssetAtPath<MMCBuildDatabase>(DatabasePath);
            if (database != null) return NormalizeSuspendedOutputTags(database);

            var legacyDatabase = AssetDatabase.LoadAssetAtPath<MMCBuildDatabase>(LegacyDatabasePath);
            if (legacyDatabase == null) return null;

            // Preserve existing history when upgrading the output directory name.
            // Generated assets themselves stay where they are and remain restorable.
            EnsureFolder(OptimizerSettings.DefaultOutputFolder);
            var moveError = AssetDatabase.MoveAsset(LegacyDatabasePath, DatabasePath);
            if (string.IsNullOrEmpty(moveError))
                return NormalizeSuspendedOutputTags(
                    AssetDatabase.LoadAssetAtPath<MMCBuildDatabase>(DatabasePath));
            return NormalizeSuspendedOutputTags(legacyDatabase);
        }

        private static MMCBuildDatabase NormalizeSuspendedOutputTags(MMCBuildDatabase database)
        {
            if (database == null || database.Builds == null) return database;
            var changed = false;
            foreach (var record in database.Builds)
            {
                if (record == null || record.Lifecycle != MMCBuildLifecycle.SuspendedForEditing) continue;
                var output = Resolve(record.OutputObject);
                if (output == null || output.CompareTag("EditorOnly")) continue;
                Undo.RecordObject(output, "Mark suspended MMC output as EditorOnly");
                output.tag = "EditorOnly";
                EditorUtility.SetDirty(output);
                changed = true;
            }
            if (!changed) return database;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return database;
        }

        public static List<MMCBuildSourceRecord> CaptureSources(IList<RendererEntry> entries)
        {
            return CaptureSources(entries, null);
        }

        public static List<MMCBuildSourceRecord> CaptureSources(IList<RendererEntry> entries,
            IList<GameObject> sourceTargets)
        {
            var records = new List<MMCBuildSourceRecord>();
            var seen = new HashSet<GameObject>();
            if (sourceTargets != null && sourceTargets.Count > 0)
            {
                foreach (var source in sourceTargets)
                {
                    if (source == null || !seen.Add(source)) continue;
                    records.Add(CaptureSourceRecord(source, DetermineOwnershipScope(source)));
                }
                return records;
            }
            if (entries == null) return records;
            foreach (var entry in entries)
            {
                if (entry == null || !entry.Included || entry.Renderer == null) continue;
                var source = entry.Renderer.gameObject;
                if (!seen.Add(source)) continue;
                records.Add(CaptureSourceRecord(source, DetermineOwnershipScope(source)));
            }
            return records;
        }

        private static MMCBuildSourceRecord CaptureSourceRecord(GameObject source,
            MMCSourceOwnershipScope ownershipScope)
        {
            return new MMCBuildSourceRecord
            {
                Object = Capture(source),
                OriginalParent = Capture(source.transform.parent != null
                    ? source.transform.parent.gameObject
                    : null),
                OriginalActiveSelf = source.activeSelf,
                OriginalTag = source.tag,
                OriginalSiblingIndex = source.transform.GetSiblingIndex(),
                OriginalDepth = GetDepth(source.transform),
                OwnershipScope = ownershipScope,
                RuntimeObject = source
            };
        }

        private static MMCSourceOwnershipScope DetermineOwnershipScope(GameObject source)
        {
            if (source == null) return MMCSourceOwnershipScope.RendererOnly;
            return PrefabUtility.IsPartOfPrefabInstance(source) || source.transform.parent != null
                ? MMCSourceOwnershipScope.WholeHierarchy
                : MMCSourceOwnershipScope.RetainedOnly;
        }

        public static void RecordBuild(string buildName, GameObject avatarRoot, GameObject outputObject,
            List<MMCBuildSourceRecord> sources, IReadOnlyList<string> generatedAssetPaths,
            OptimizerSettings settings, IList<RendererEntry> entries, OptimizationReport report = null)
        {
            var database = LoadOrCreate();
            var recipe = MMCRebuildService.CaptureRecipe(settings, entries);
            recipe.OutputName = buildName ?? recipe.OutputName;
            var inputHash = MMCRebuildService.ComputeInputHash(recipe, out var dependencyGuids);
            var fastInputHash = MMCRebuildService.ComputeFastInputHash(recipe);
            var createdAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var record = new MMCBuildRecord
            {
                BuildId = Guid.NewGuid().ToString("N"),
                BuildName = buildName ?? string.Empty,
                CreatedAt = createdAt,
                LastBuiltAt = createdAt,
                AvatarRoot = Capture(avatarRoot),
                OutputObject = Capture(outputObject),
                Sources = sources ?? new List<MMCBuildSourceRecord>(),
                Recipe = recipe,
                CurrentInputHash = inputHash,
                FastInputHash = fastInputHash,
                DependencyGuids = dependencyGuids,
                SavedBeforeStats = report != null ? report.Before : null,
                SavedAfterStats = report != null ? report.After : null
            };
            foreach (var source in record.Sources)
            {
                // Refresh the path after source organization. GlobalObjectId is stable,
                // while this fallback path reflects its current location.
                if (source.RuntimeObject != null) source.Object = Capture(source.RuntimeObject);
            }
            if (generatedAssetPaths != null)
                for (var i = 0; i < generatedAssetPaths.Count; i++)
                    record.GeneratedAssetPaths.Add(generatedAssetPaths[i]);
            PopulateGeneratedAssetReferences(record.GeneratedAssetPaths, record.GeneratedAssets);
            var renderer = outputObject != null ? outputObject.GetComponent<SkinnedMeshRenderer>() : null;
            var revision = MMCRebuildService.CaptureRevision(renderer, avatarRoot, inputHash, fastInputHash,
                record.GeneratedAssetPaths, createdAt);
            record.Revisions.Add(revision);
            record.ActiveRevisionIndex = 0;

            Undo.RecordObject(database, "Record Mesh Material Combiner build");
            database.Builds.Add(record);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        public static MMCBuildStatus GetStatus(MMCBuildRecord record)
        {
            return ResolveState(record).Status;
        }

        public static MMCBuildResolvedState ResolveState(MMCBuildRecord record)
        {
            var state = new MMCBuildResolvedState { Status = MMCBuildStatus.Modified };
            if (record == null) return state;
            state.Output = Resolve(record.OutputObject);
            var missingSource = false;
            var missingParent = false;
            var unloadedScene = state.Output == null && IsReferencedSceneUnloaded(record.OutputObject);
            var modifiedSource = false;
            var resolvedParentCache = new Dictionary<string, GameObject>();
            foreach (var sourceRecord in record.Sources)
            {
                var source = Resolve(sourceRecord.Object);
                state.Sources.Add(source);
                if (source == null)
                {
                    if (IsReferencedSceneUnloaded(sourceRecord.Object)) unloadedScene = true;
                    else missingSource = true;
                }
                else
                {
                    if (!state.AvailableSources.Contains(source)) state.AvailableSources.Add(source);
                    if (!IsExcludedSource(source)) modifiedSource = true;
                }
                if (HasLocator(sourceRecord.OriginalParent))
                {
                    var parentKey = GetLocatorKey(sourceRecord.OriginalParent);
                    if (!resolvedParentCache.TryGetValue(parentKey, out var originalParent))
                    {
                        originalParent = Resolve(sourceRecord.OriginalParent);
                        resolvedParentCache[parentKey] = originalParent;
                    }
                    if (originalParent == null)
                    {
                        if (IsReferencedSceneUnloaded(sourceRecord.OriginalParent)) unloadedScene = true;
                        else missingParent = true;
                    }
                }
            }
            if (unloadedScene) state.Status = MMCBuildStatus.SceneNotLoaded;
            else if (missingSource) state.Status = MMCBuildStatus.SourceMissing;
            else if (missingParent) state.Status = MMCBuildStatus.SourceParentMissing;
            else if (state.Output == null) state.Status = MMCBuildStatus.OutputMissing;
            else if (!state.Output.activeSelf) state.Status = MMCBuildStatus.OutputDisabled;
            else
            {
                var outputRenderer = state.Output.GetComponent<Renderer>();
                if (outputRenderer == null) state.Status = MMCBuildStatus.Modified;
                else if (!outputRenderer.enabled) state.Status = MMCBuildStatus.OutputRendererDisabled;
                else state.Status = modifiedSource ? MMCBuildStatus.Modified : MMCBuildStatus.Valid;
            }
            return state;
        }

        public static GameObject ResolveOutput(MMCBuildRecord record)
        {
            return record == null ? null : Resolve(record.OutputObject);
        }

        public static GameObject ResolveAvatarRoot(MMCBuildRecord record)
        {
            return record == null ? null : Resolve(record.AvatarRoot);
        }

        internal static bool ValidateManagedObjectBoundaries(MMCBuildRecord record,
            out string message)
        {
            message = string.Empty;
            if (record == null)
            {
                message = "Managed-object safety validation failed: the build record is missing.";
                return false;
            }

            var avatarRoot = Resolve(record.AvatarRoot);
            if (avatarRoot == null)
            {
                message = BuildBoundaryError("Avatar Root", record.AvatarRoot,
                    "the recorded avatar root could not be resolved");
                return false;
            }
            if (!ValidateLocatorScene(record.AvatarRoot, avatarRoot, "Avatar Root", out message))
                return false;

            var output = Resolve(record.OutputObject);
            if (!ValidateLocatorScene(record.OutputObject, output, "Output", out message))
                return false;
            if (output != null)
            {
                if (!IsInAvatarHierarchy(output, avatarRoot))
                {
                    message = BuildBoundaryError("Output", record.OutputObject,
                        "the object is outside the recorded avatar hierarchy");
                    return false;
                }
                var container = output.transform.parent;
                if (container == null || container.name != "__MeshMaterialCombiner" ||
                    container.parent != avatarRoot.transform)
                {
                    message = BuildBoundaryError("Output", record.OutputObject,
                        "the object is not directly under AvatarRoot/__MeshMaterialCombiner");
                    return false;
                }
            }

            if (record.Sources != null)
                foreach (var sourceRecord in record.Sources)
                {
                    if (sourceRecord == null) continue;
                    var source = Resolve(sourceRecord.Object);
                    if (!ValidateLocatorScene(sourceRecord.Object, source, "Source", out message))
                        return false;
                    if (source != null && !IsInAvatarHierarchy(source, avatarRoot))
                    {
                        message = BuildBoundaryError("Source", sourceRecord.Object,
                            "the object is outside the recorded avatar hierarchy");
                        return false;
                    }

                    if (!HasLocator(sourceRecord.OriginalParent)) continue;
                    var originalParent = Resolve(sourceRecord.OriginalParent);
                    if (!ValidateLocatorScene(sourceRecord.OriginalParent, originalParent,
                            "Original Parent", out message)) return false;
                    if (originalParent != null && !IsInAvatarHierarchy(originalParent, avatarRoot))
                    {
                        message = BuildBoundaryError("Original Parent", sourceRecord.OriginalParent,
                            "the object is outside the recorded avatar hierarchy");
                        return false;
                    }
                }

            if (record.Recipe != null && record.Recipe.Renderers != null)
                foreach (var renderer in record.Recipe.Renderers)
                {
                    if (renderer == null) continue;
                    var rendererObject = Resolve(renderer.Object);
                    if (!ValidateLocatorScene(renderer.Object, rendererObject,
                            "Recipe Source", out message)) return false;
                    if (rendererObject != null && !IsInAvatarHierarchy(rendererObject, avatarRoot))
                    {
                        message = BuildBoundaryError("Recipe Source", renderer.Object,
                            "the object is outside the recorded avatar hierarchy");
                        return false;
                    }
                }
            return true;
        }

        private static bool ValidateLocatorScene(MMCObjectLocator locator, GameObject resolved,
            string role, out string message)
        {
            message = string.Empty;
            if (locator == null) return true;
            if (!string.IsNullOrEmpty(locator.GlobalObjectId))
            {
                if (!GlobalObjectId.TryParse(locator.GlobalObjectId, out var globalId))
                {
                    message = BuildBoundaryError(role, locator,
                        "the recorded GlobalObjectId is invalid");
                    return false;
                }
                if (!string.IsNullOrEmpty(locator.ScenePath))
                {
                    var sceneGuid = AssetDatabase.AssetPathToGUID(locator.ScenePath);
                    var globalSceneGuid = globalId.assetGUID.ToString();
                    if (string.IsNullOrEmpty(sceneGuid) ||
                        !string.Equals(sceneGuid, globalSceneGuid,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        message = BuildBoundaryError(role, locator,
                            "the Scene path does not match the GlobalObjectId Scene");
                        return false;
                    }
                }
            }
            if (resolved == null || string.IsNullOrEmpty(locator.ScenePath)) return true;
            if (resolved.scene.IsValid() && string.Equals(resolved.scene.path,
                    locator.ScenePath, StringComparison.OrdinalIgnoreCase)) return true;
            message = BuildBoundaryError(role, locator,
                "the resolved object belongs to a different Scene");
            return false;
        }

        private static bool IsInAvatarHierarchy(GameObject gameObject, GameObject avatarRoot)
        {
            return gameObject != null && avatarRoot != null &&
                   gameObject.scene == avatarRoot.scene &&
                   (gameObject == avatarRoot || gameObject.transform.IsChildOf(avatarRoot.transform));
        }

        private static string BuildBoundaryError(string role, MMCObjectLocator locator,
            string reason)
        {
            var scenePath = locator != null && !string.IsNullOrEmpty(locator.ScenePath)
                ? locator.ScenePath
                : "<Unknown Scene>";
            var hierarchyPath = locator != null && !string.IsNullOrEmpty(locator.HierarchyPath)
                ? locator.HierarchyPath
                : locator != null && !string.IsNullOrEmpty(locator.DisplayName)
                    ? locator.DisplayName
                    : "<Unknown Object>";
            return "Managed-object safety validation failed.\nTarget: " + role + "\nPath: " +
                   scenePath + " :: " + hierarchyPath + "\nReason: " + reason +
                   "\nThe operation was stopped.";
        }

        public static List<GameObject> ResolveSources(MMCBuildRecord record)
        {
            var result = new List<GameObject>();
            if (record == null) return result;
            foreach (var source in record.Sources)
            {
                var resolved = Resolve(source.Object);
                if (resolved != null && !result.Contains(resolved)) result.Add(resolved);
            }
            return result;
        }

        public static GameObject ResolveSource(MMCBuildSourceRecord record)
        {
            return record == null ? null : Resolve(record.Object);
        }

        internal static bool CaptureCurrentSourceBaseline(MMCBuildRecord record,
            out List<GameObject> sources, out string error)
        {
            sources = new List<GameObject>();
            error = string.Empty;
            if (record == null || record.Sources == null)
            {
                error = OptimizerLocalization.T("Rebuild Source Missing");
                return false;
            }
            foreach (var sourceRecord in record.Sources)
            {
                var source = Resolve(sourceRecord.Object);
                if (source == null)
                {
                    error = OptimizerLocalization.T("Rebuild Source Missing") + ": " +
                            GetDisplayName(sourceRecord.Object);
                    return false;
                }
                sourceRecord.Object = Capture(source);
                sourceRecord.OriginalParent = Capture(source.transform.parent != null
                    ? source.transform.parent.gameObject
                    : null);
                sourceRecord.OriginalActiveSelf = source.activeSelf;
                sourceRecord.OriginalTag = source.tag;
                sourceRecord.OriginalSiblingIndex = source.transform.GetSiblingIndex();
                sourceRecord.OriginalDepth = GetDepth(source.transform);
                sourceRecord.RuntimeObject = source;
                sources.Add(source);
            }
            return true;
        }

        internal static void RefreshSourceLocators(MMCBuildRecord record,
            IList<GameObject> sources)
        {
            if (record == null || record.Sources == null || sources == null) return;
            for (var i = 0; i < record.Sources.Count && i < sources.Count; i++)
                if (sources[i] != null) record.Sources[i].Object = Capture(sources[i]);
        }

        internal static bool HasUnavailableManagedDependency(MMCBuildRecord record,
            out string dependencyName)
        {
            dependencyName = string.Empty;
            var database = Load();
            if (database == null || database.Builds == null || record == null ||
                record.Recipe == null || record.Recipe.Renderers == null) return false;
            foreach (var locator in record.Recipe.Renderers)
            {
                var source = Resolve(locator.Object);
                if (source == null) continue;
                foreach (var owner in database.Builds)
                {
                    if (owner == null || ReferenceEquals(owner, record) ||
                        owner.Lifecycle == MMCBuildLifecycle.Built) continue;
                    if (Resolve(owner.OutputObject) != source) continue;
                    dependencyName = string.IsNullOrEmpty(owner.BuildName)
                        ? "(Unnamed Build)"
                        : owner.BuildName;
                    return true;
                }
            }
            return false;
        }

        internal static List<MMCSourceOwnershipConflict> FindSourceOwnershipConflicts(
            IList<RendererEntry> entries, IList<GameObject> sourceTargets,
            MMCBuildRecord excludedRecord = null)
        {
            var result = new List<MMCSourceOwnershipConflict>();
            var database = Load();
            if (database == null || database.Builds == null) return result;
            var requestedScopes = BuildRequestedOwnershipScopes(entries, sourceTargets);
            var seenRecords = new HashSet<MMCBuildRecord>();
            foreach (var record in database.Builds)
            {
                if (record == null || ReferenceEquals(record, excludedRecord)) continue;
                if (!TryFindOverlap(requestedScopes, BuildRecordOwnershipScopes(record),
                        out var sourceObject) || !seenRecords.Add(record)) continue;
                result.Add(new MMCSourceOwnershipConflict
                {
                    Record = excludedRecord,
                    OtherRecord = record,
                    SourceObject = sourceObject
                });
            }
            return result;
        }

        internal static bool TryFindSourceOwnershipConflict(MMCBuildRecord record,
            out MMCSourceOwnershipConflict conflict)
        {
            conflict = null;
            var database = Load();
            if (database == null || record == null) return false;
            return BuildSourceOwnershipConflictMap(database).TryGetValue(record, out conflict);
        }

        internal static Dictionary<MMCBuildRecord, MMCSourceOwnershipConflict>
            BuildSourceOwnershipConflictMap(MMCBuildDatabase database)
        {
            var result = new Dictionary<MMCBuildRecord, MMCSourceOwnershipConflict>();
            if (database == null || database.Builds == null) return result;
            var records = new List<MMCBuildRecord>();
            var scopes = new Dictionary<MMCBuildRecord, List<OwnershipScopeEntry>>();
            foreach (var record in database.Builds)
            {
                if (record == null) continue;
                records.Add(record);
                scopes[record] = BuildRecordOwnershipScopes(record);
            }
            for (var i = 0; i < records.Count; i++)
                for (var j = i + 1; j < records.Count; j++)
                {
                    var first = records[i];
                    var second = records[j];
                    if (!TryFindOverlap(scopes[first], scopes[second], out var sourceObject)) continue;
                    if (!result.ContainsKey(first))
                        result[first] = new MMCSourceOwnershipConflict
                        {
                            Record = first,
                            OtherRecord = second,
                            SourceObject = sourceObject
                        };
                    if (!result.ContainsKey(second))
                        result[second] = new MMCSourceOwnershipConflict
                        {
                            Record = second,
                            OtherRecord = first,
                            SourceObject = sourceObject
                        };
                }
            return result;
        }

        internal static string FormatSourceOwnershipConflict(MMCSourceOwnershipConflict conflict)
        {
            if (conflict == null) return "Managed source ownership conflict.";
            var sourceName = conflict.SourceObject != null ? conflict.SourceObject.name : "(Unknown Source)";
            var buildName = conflict.OtherRecord != null &&
                            !string.IsNullOrEmpty(conflict.OtherRecord.BuildName)
                ? conflict.OtherRecord.BuildName
                : "(Unnamed Build)";
            var lifecycle = conflict.OtherRecord != null
                ? conflict.OtherRecord.Lifecycle.ToString()
                : "Unknown";
            return string.Format(OptimizerLocalization.T("Source Ownership Conflict"),
                sourceName, buildName, lifecycle);
        }

        private sealed class OwnershipScopeEntry
        {
            public GameObject Object;
            public bool WholeHierarchy;
        }

        private static List<OwnershipScopeEntry> BuildRequestedOwnershipScopes(
            IList<RendererEntry> entries, IList<GameObject> sourceTargets)
        {
            var result = new List<OwnershipScopeEntry>();
            if (entries != null)
                foreach (var entry in entries)
                    if (entry != null && entry.Included && entry.Renderer != null)
                        AddOwnershipScope(result, entry.Renderer.gameObject, false);

            if (sourceTargets != null && sourceTargets.Count > 0)
            {
                foreach (var source in sourceTargets)
                    AddOwnershipScope(result, source,
                        DetermineOwnershipScope(source) == MMCSourceOwnershipScope.WholeHierarchy);
            }
            else if (entries != null)
            {
                foreach (var entry in entries)
                    if (entry != null && entry.Included && entry.Renderer != null)
                        AddOwnershipScope(result, entry.Renderer.gameObject,
                            DetermineOwnershipScope(entry.Renderer.gameObject) ==
                            MMCSourceOwnershipScope.WholeHierarchy);
            }
            return result;
        }

        private static List<OwnershipScopeEntry> BuildRecordOwnershipScopes(MMCBuildRecord record)
        {
            var result = new List<OwnershipScopeEntry>();
            if (record == null) return result;
            if (record.Recipe != null && record.Recipe.Renderers != null)
                foreach (var renderer in record.Recipe.Renderers)
                    if (renderer != null) AddOwnershipScope(result, Resolve(renderer.Object), false);
            if (record.Sources == null) return result;
            foreach (var sourceRecord in record.Sources)
            {
                if (sourceRecord == null) continue;
                var source = Resolve(sourceRecord.Object);
                var wholeHierarchy = sourceRecord.OwnershipScope ==
                                     MMCSourceOwnershipScope.WholeHierarchy;
                if (sourceRecord.OwnershipScope == MMCSourceOwnershipScope.Legacy)
                    wholeHierarchy = HasLocator(sourceRecord.OriginalParent);
                AddOwnershipScope(result, source, wholeHierarchy);
            }
            return result;
        }

        private static void AddOwnershipScope(List<OwnershipScopeEntry> scopes,
            GameObject gameObject, bool wholeHierarchy)
        {
            if (gameObject == null) return;
            foreach (var scope in scopes)
            {
                if (scope.Object != gameObject) continue;
                scope.WholeHierarchy |= wholeHierarchy;
                return;
            }
            scopes.Add(new OwnershipScopeEntry
            {
                Object = gameObject,
                WholeHierarchy = wholeHierarchy
            });
        }

        private static bool TryFindOverlap(IList<OwnershipScopeEntry> first,
            IList<OwnershipScopeEntry> second, out GameObject sourceObject)
        {
            sourceObject = null;
            if (first == null || second == null) return false;
            foreach (var left in first)
                foreach (var right in second)
                {
                    if (left == null || right == null || left.Object == null || right.Object == null)
                        continue;
                    if (left.Object == right.Object ||
                        (left.WholeHierarchy && right.Object.transform.IsChildOf(left.Object.transform)) ||
                        (right.WholeHierarchy && left.Object.transform.IsChildOf(right.Object.transform)))
                    {
                        sourceObject = left.Object == right.Object ? left.Object : right.Object;
                        return true;
                    }
                }
            return false;
        }

        internal static MMCObjectLocator CaptureObject(GameObject gameObject)
        {
            return Capture(gameObject);
        }

        internal static GameObject ResolveObject(MMCObjectLocator locator)
        {
            return Resolve(locator);
        }

        internal static void SaveDatabase(MMCBuildDatabase database, string undoName)
        {
            if (database == null) return;
            Undo.RecordObject(database, undoName);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
        }

        public static bool Restore(MMCBuildRecord record, out string message)
        {
            return FullyDetach(record, out message);
        }

        public static int CountExistingGeneratedAssets(MMCBuildRecord record)
        {
            var paths = CollectGeneratedAssetPaths(record);
            var count = 0;
            foreach (var path in paths)
                if (TryGetSafeGeneratedAssetPath(path, out var safePath) &&
                    TryGetRecordedGeneratedAssetGuid(record, safePath, out var expectedGuid) &&
                    string.Equals(AssetDatabase.AssetPathToGUID(safePath), expectedGuid,
                        StringComparison.OrdinalIgnoreCase) &&
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(safePath) != null) count++;
            return count;
        }

        public static bool Restore(MMCBuildRecord record, bool deleteGeneratedAssets,
            out string message)
        {
            return deleteGeneratedAssets
                ? FullyDetach(record, out message)
                : SuspendForEditing(record, out message);
        }

        public static bool SuspendForEditing(MMCBuildRecord record, out string message)
        {
            message = string.Empty;
            var database = Load();
            if (database == null || record == null || !database.Builds.Contains(record))
            {
                message = "The selected build record no longer exists.";
                return false;
            }
            if (record.Lifecycle == MMCBuildLifecycle.CleanupPending)
            {
                message = OptimizerLocalization.T("Cleanup Pending Cannot Suspend");
                return false;
            }
            if (record.Lifecycle == MMCBuildLifecycle.SuspendedForEditing)
            {
                message = OptimizerLocalization.T("Already Suspended");
                return true;
            }
            if (TryFindSourceOwnershipConflict(record, out var ownershipConflict))
            {
                message = FormatSourceOwnershipConflict(ownershipConflict);
                return false;
            }
            if (!TryResolveRestoreObjects(record, out var sources, out var resolvedSources,
                    out var resolvedParents, out message)) return false;

            var output = Resolve(record.OutputObject);
            if (output == null)
            {
                message = OptimizerLocalization.T("Suspend Output Missing");
                return false;
            }
            if (HasDependentBuild(database, record, output, false, out var dependentName))
            {
                message = string.Format(OptimizerLocalization.T("Suspend Has Active Dependent Build"),
                    dependentName);
                return false;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Suspend Mesh Material Combiner build for editing");
            try
            {
                var restored = RestoreSources(sources, resolvedSources, resolvedParents);
                Undo.RecordObject(output, "Temporarily disable MMC output");
                output.SetActive(false);
                output.tag = "EditorOnly";
                EditorUtility.SetDirty(output);
                Undo.RecordObject(database, "Suspend MMC build history");
                record.Lifecycle = MMCBuildLifecycle.SuspendedForEditing;
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                message = string.Format(OptimizerLocalization.T("Build Suspended"), restored);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                message = exception.Message;
                return false;
            }
        }

        public static bool FullyDetach(MMCBuildRecord record, out string message)
        {
            message = string.Empty;
            var database = Load();
            if (database == null || record == null || !database.Builds.Contains(record))
            {
                message = "The selected build record no longer exists.";
                return false;
            }

            var managedOutput = Resolve(record.OutputObject);
            if (managedOutput != null && HasDependentBuild(database, record, managedOutput, true,
                    out var dependentName))
            {
                message = string.Format(OptimizerLocalization.T("Detach Has Dependent Build"), dependentName);
                return false;
            }

            var cleanupCandidates = CollectGeneratedAssetPaths(record);

            if (!TryResolveRestoreObjects(record, out var sources, out var resolvedSources,
                    out var resolvedParents, out message)) return false;
            var restored = 0;
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Restore Mesh Material Combiner build");
            try
            {
                restored = RestoreSources(sources, resolvedSources, resolvedParents);

                var output = Resolve(record.OutputObject);
                if (output != null) Undo.DestroyObjectImmediate(output);

                var deletedAssets = 0;
                var protectedAssets = 0;
                var failedAssets = 0;
                Undo.RecordObject(database, "Update detached MMC build history");
                DeleteGeneratedAssets(database, record, cleanupCandidates,
                    out deletedAssets, out protectedAssets, out failedAssets);

                // Asset deletion is not reliably Undoable. Keep the record when an
                // actual deletion failed so the remaining files can be inspected and
                // cleanup can be retried. Referenced or unverifiable assets are
                // intentionally retained and do not make the restore fail.
                if (failedAssets == 0)
                {
                    Undo.RecordObject(database, "Remove restored build record");
                    database.Builds.Remove(record);
                }
                else
                {
                    record.Lifecycle = MMCBuildLifecycle.CleanupPending;
                }
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                if (failedAssets > 0)
                {
                    message = $"Restored {restored} source object(s) and deleted {deletedAssets} generated asset(s). " +
                              $"{failedAssets} asset(s) could not be deleted, so the build record was kept. " +
                              $"{protectedAssets} protected or unverifiable asset(s) were retained.";
                }
                else
                {
                    message = $"Restored {restored} source object(s) and deleted {deletedAssets} generated asset(s).";
                    if (protectedAssets > 0)
                        message += $" {protectedAssets} protected or unverifiable asset(s) were retained.";
                }
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                message = exception.Message;
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static bool TryResolveRestoreObjects(MMCBuildRecord record,
            out List<MMCBuildSourceRecord> sources,
            out Dictionary<MMCBuildSourceRecord, GameObject> resolvedSources,
            out Dictionary<MMCBuildSourceRecord, GameObject> resolvedParents,
            out string message)
        {
            sources = new List<MMCBuildSourceRecord>();
            resolvedSources = new Dictionary<MMCBuildSourceRecord, GameObject>();
            resolvedParents = new Dictionary<MMCBuildSourceRecord, GameObject>();
            message = string.Empty;
            if (!ValidateManagedObjectBoundaries(record, out message)) return false;
            if (HasUnloadedSceneReference(record))
            {
                message = OptimizerLocalization.T("Restore Scene Not Loaded");
                return false;
            }
            sources.AddRange(record.Sources);
            sources.Sort((a, b) => a.OriginalDepth.CompareTo(b.OriginalDepth));
            foreach (var sourceRecord in sources)
            {
                resolvedSources[sourceRecord] = Resolve(sourceRecord.Object);
                resolvedParents[sourceRecord] = Resolve(sourceRecord.OriginalParent);
            }
            var missingSources = new List<string>();
            var missingParents = new List<string>();
            foreach (var sourceRecord in sources)
            {
                if (resolvedSources[sourceRecord] == null)
                    missingSources.Add(GetDisplayName(sourceRecord.Object));
                if (HasLocator(sourceRecord.OriginalParent) && resolvedParents[sourceRecord] == null)
                    missingParents.Add(GetDisplayName(sourceRecord.OriginalParent) +
                        " (" + GetDisplayName(sourceRecord.Object) + ")");
            }
            if (missingSources.Count == 0 && missingParents.Count == 0) return true;
            message = OptimizerLocalization.T("Restore Missing Dependencies");
            if (missingSources.Count > 0)
                message += "\n\n" + OptimizerLocalization.T("Missing Sources") + ": " +
                           FormatNames(missingSources);
            if (missingParents.Count > 0)
                message += "\n\n" + OptimizerLocalization.T("Missing Original Parents") + ": " +
                           FormatNames(missingParents);
            return false;
        }

        private static int RestoreSources(IList<MMCBuildSourceRecord> sources,
            IDictionary<MMCBuildSourceRecord, GameObject> resolvedSources,
            IDictionary<MMCBuildSourceRecord, GameObject> resolvedParents)
        {
            var restored = 0;
            foreach (var sourceRecord in sources)
            {
                var source = resolvedSources[sourceRecord];
                var oldContainer = source.transform.parent;
                var originalParent = resolvedParents[sourceRecord];
                if (originalParent != null && source.transform.parent != originalParent.transform)
                    Undo.SetTransformParent(source.transform, originalParent.transform,
                        "Restore source parent");
                Undo.RecordObject(source, "Restore source state");
                source.transform.SetSiblingIndex(Mathf.Clamp(sourceRecord.OriginalSiblingIndex, 0,
                    source.transform.parent != null ? source.transform.parent.childCount - 1 : 0));
                TrySetTag(source, sourceRecord.OriginalTag);
                source.SetActive(sourceRecord.OriginalActiveSelf);
                EditorUtility.SetDirty(source);
                DeleteEmptySourceContainer(oldContainer);
                restored++;
            }
            return restored;
        }

        private static HashSet<string> CollectGeneratedAssetPaths(MMCBuildRecord record)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (record == null) return result;
            AddGeneratedPaths(result, record.GeneratedAssetPaths);
            if (record.Revisions != null)
                foreach (var revision in record.Revisions)
                    if (revision != null) AddGeneratedPaths(result, revision.GeneratedAssetPaths);
            return result;
        }

        private static bool HasDependentBuild(MMCBuildDatabase database, MMCBuildRecord owner,
            GameObject output, bool includeSuspended, out string dependentName)
        {
            dependentName = string.Empty;
            if (database == null || database.Builds == null || output == null) return false;
            foreach (var candidate in database.Builds)
            {
                if (candidate == null || ReferenceEquals(candidate, owner) ||
                    (!includeSuspended && candidate.Lifecycle != MMCBuildLifecycle.Built) ||
                    candidate.Recipe == null ||
                    candidate.Recipe.Renderers == null) continue;
                foreach (var locator in candidate.Recipe.Renderers)
                {
                    if (Resolve(locator.Object) != output) continue;
                    dependentName = string.IsNullOrEmpty(candidate.BuildName)
                        ? "(Unnamed Build)"
                        : candidate.BuildName;
                    return true;
                }
            }
            return false;
        }

        private static void AddGeneratedPaths(HashSet<string> destination,
            IEnumerable<string> paths)
        {
            if (paths == null) return;
            foreach (var path in paths)
            {
                var normalized = NormalizeAssetPath(path);
                if (!string.IsNullOrEmpty(normalized)) destination.Add(normalized);
            }
        }

        internal static void PopulateGeneratedAssetReferences(IEnumerable<string> paths,
            List<MMCGeneratedAssetReference> destination)
        {
            if (paths == null || destination == null) return;
            destination.Clear();
            foreach (var path in paths)
            {
                if (!TryGetSafeGeneratedAssetPath(path, out var safePath)) continue;
                var guid = AssetDatabase.AssetPathToGUID(safePath);
                if (string.IsNullOrEmpty(guid)) continue;
                destination.Add(new MMCGeneratedAssetReference { Path = safePath, Guid = guid });
            }
        }

        private static void DeleteGeneratedAssets(MMCBuildDatabase database,
            MMCBuildRecord restoringRecord, HashSet<string> recordedPaths,
            out int deleted, out int protectedCount, out int failed)
        {
            deleted = 0;
            protectedCount = 0;
            failed = 0;
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in recordedPaths)
            {
                if (!TryGetSafeGeneratedAssetPath(path, out var safePath))
                {
                    protectedCount++;
                    continue;
                }
                var currentGuid = AssetDatabase.AssetPathToGUID(safePath);
                if (string.IsNullOrEmpty(currentGuid) &&
                    AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(safePath) == null) continue;
                if (!TryGetRecordedGeneratedAssetGuid(restoringRecord, safePath,
                        out var expectedGuid) ||
                    !string.Equals(currentGuid, expectedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    // Never delete an asset that has been replaced since the history
                    // was written, or a legacy asset without a recorded GUID.
                    protectedCount++;
                    continue;
                }
                var folder = NormalizeAssetPath(Path.GetDirectoryName(safePath));
                if (!string.IsNullOrEmpty(folder)) folders.Add(folder);
                var buildFolder = GetBuildOutputFolder(safePath);
                if (!string.IsNullOrEmpty(buildFolder)) folders.Add(buildFolder);
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(safePath) != null)
                    candidates.Add(safePath);
            }
            if (candidates.Count == 0)
            {
                DeleteEmptyGeneratedFolders(folders);
                CleanupEmptyBuildOutputFolders();
                return;
            }

            var protectedPaths = FindOtherHistoryReferences(database, restoringRecord, candidates);
            // Do not scan every asset in the Project here. Even the batched
            // GetDependencies overload becomes very expensive in large VRChat
            // projects. Generated files live in a unique build directory, so the
            // practical guards are other MMC histories and currently loaded Scenes.
            foreach (var path in FindLoadedSceneReferences(candidates))
                protectedPaths.Add(path);
            ProtectCandidateDependencies(candidates, protectedPaths);

            var index = 0;
            var editingStarted = false;
            try
            {
                AssetDatabase.StartAssetEditing();
                editingStarted = true;
                foreach (var path in candidates)
                {
                    index++;
                    EditorUtility.DisplayProgressBar("Mesh Material Combiner",
                        "Cleaning generated assets", index / (float)candidates.Count);
                    if (protectedPaths.Contains(path))
                    {
                        protectedCount++;
                        continue;
                    }
                    try
                    {
                        if (AssetDatabase.DeleteAsset(path)) deleted++;
                        else failed++;
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }
            finally
            {
                if (editingStarted)
                {
                    try
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                    catch
                    {
                        failed++;
                    }
                }
            }
            DeleteEmptyGeneratedFolders(folders);
            CleanupEmptyBuildOutputFolders();
        }

        private static HashSet<string> FindOtherHistoryReferences(MMCBuildDatabase database,
            MMCBuildRecord restoringRecord, HashSet<string> candidates)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (database == null || database.Builds == null) return result;
            foreach (var other in database.Builds)
            {
                if (other == null || ReferenceEquals(other, restoringRecord)) continue;
                foreach (var path in CollectGeneratedAssetPaths(other))
                    if (candidates.Contains(path)) result.Add(path);
            }
            return result;
        }

        private static HashSet<string> FindLoadedSceneReferences(HashSet<string> candidates)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var roots = new List<UnityEngine.Object>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects()) roots.Add(root);
            }
            if (roots.Count == 0) return result;
            foreach (var dependency in EditorUtility.CollectDependencies(roots.ToArray()))
            {
                var path = NormalizeAssetPath(AssetDatabase.GetAssetPath(dependency));
                if (candidates.Contains(path)) result.Add(path);
            }
            return result;
        }

        private static void ProtectCandidateDependencies(HashSet<string> candidates,
            HashSet<string> protectedPaths)
        {
            var queue = new Queue<string>(protectedPaths);
            while (queue.Count > 0)
            {
                var protectedPath = queue.Dequeue();
                try
                {
                    foreach (var dependency in AssetDatabase.GetDependencies(protectedPath, true))
                    {
                        var normalized = NormalizeAssetPath(dependency);
                        if (candidates.Contains(normalized) && protectedPaths.Add(normalized))
                            queue.Enqueue(normalized);
                    }
                }
                catch
                {
                    // Keep the directly protected asset even if its dependency list
                    // cannot be inspected.
                }
            }
        }

        private static void DeleteEmptyGeneratedFolders(HashSet<string> folders)
        {
            var ordered = new List<string>(folders);
            ordered.Sort((a, b) => b.Length.CompareTo(a.Length));
            foreach (var startingFolder in ordered)
            {
                try
                {
                    var folder = startingFolder;
                    var stopFolder = GetAvatarOutputFolder(folder);
                    if (string.IsNullOrEmpty(stopFolder)) continue;
                    while (!string.IsNullOrEmpty(folder) &&
                           folder.StartsWith(stopFolder + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!AssetDatabase.IsValidFolder(folder) || FolderContainsAssets(folder)) break;
                        var parent = NormalizeAssetPath(Path.GetDirectoryName(folder));
                        if (!AssetDatabase.DeleteAsset(folder)) break;
                        folder = parent;
                    }
                }
                catch
                {
                    // Folder cleanup is optional. Never turn a successfully restored
                    // hierarchy into a failed restore after asset deletion has begun.
                }
            }
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static bool TryGetSafeGeneratedAssetPath(string path, out string safePath)
        {
            safePath = string.Empty;
            var normalized = NormalizeAssetPath(path).Trim();
            if (string.IsNullOrEmpty(normalized) || Path.IsPathRooted(normalized) ||
                IsDatabasePath(normalized)) return false;
            var segments = normalized.Split('/');
            foreach (var segment in segments)
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..") return false;

            var extension = Path.GetExtension(normalized);
            if (!string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".mat", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase)) return false;
            if (AssetDatabase.IsValidFolder(normalized)) return false;

            try
            {
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var absolutePath = Path.GetFullPath(Path.Combine(projectRoot,
                    normalized.Replace('/', Path.DirectorySeparatorChar)));
                var relativePath = absolutePath.Substring(projectRoot.TrimEnd(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar).Replace('\\', '/');
                var canonicalAssetPath = relativePath;
                if (!string.Equals(canonicalAssetPath, normalized,
                        StringComparison.OrdinalIgnoreCase)) return false;

                var currentRoot = GetCanonicalManagedRoot(projectRoot,
                    OptimizerSettings.DefaultOutputFolder);
                var legacyRoot = GetCanonicalManagedRoot(projectRoot,
                    OptimizerSettings.LegacyOutputFolder);
                if (!IsStrictChildOf(absolutePath, currentRoot) &&
                    !IsStrictChildOf(absolutePath, legacyRoot)) return false;
                safePath = canonicalAssetPath;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string GetCanonicalManagedRoot(string projectRoot, string assetRoot)
        {
            return Path.GetFullPath(Path.Combine(projectRoot,
                NormalizeAssetPath(assetRoot).Replace('/', Path.DirectorySeparatorChar)))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsStrictChildOf(string path, string root)
        {
            return path.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryGetRecordedGeneratedAssetGuid(MMCBuildRecord record, string path,
            out string guid)
        {
            guid = string.Empty;
            if (record == null) return false;
            if (TryFindGeneratedAssetGuid(record.GeneratedAssets, path, out guid)) return true;
            if (record.Revisions == null) return false;
            foreach (var revision in record.Revisions)
                if (revision != null && TryFindGeneratedAssetGuid(revision.GeneratedAssets, path, out guid))
                    return true;
            return false;
        }

        private static bool TryFindGeneratedAssetGuid(IEnumerable<MMCGeneratedAssetReference> references,
            string path, out string guid)
        {
            guid = string.Empty;
            if (references == null) return false;
            foreach (var reference in references)
            {
                if (reference == null || string.IsNullOrEmpty(reference.Guid) ||
                    !string.Equals(NormalizeAssetPath(reference.Path), path,
                        StringComparison.OrdinalIgnoreCase)) continue;
                guid = reference.Guid;
                return true;
            }
            return false;
        }

        private static string GetManagedOutputRoot(string path)
        {
            var normalized = NormalizeAssetPath(path);
            var currentRoot = NormalizeAssetPath(OptimizerSettings.DefaultOutputFolder).TrimEnd('/');
            if (normalized.StartsWith(currentRoot + "/", StringComparison.OrdinalIgnoreCase))
                return currentRoot;
            var legacyRoot = NormalizeAssetPath(OptimizerSettings.LegacyOutputFolder).TrimEnd('/');
            return normalized.StartsWith(legacyRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? legacyRoot
                : string.Empty;
        }

        private static string GetAvatarOutputFolder(string path)
        {
            var normalized = NormalizeAssetPath(path);
            var root = GetManagedOutputRoot(normalized);
            if (string.IsNullOrEmpty(root)) return string.Empty;
            var relative = normalized.Substring(root.Length).TrimStart('/');
            var separator = relative.IndexOf('/');
            if (separator <= 0) return string.Empty;
            return root + "/" + relative.Substring(0, separator);
        }

        private static string GetBuildOutputFolder(string path)
        {
            var normalized = NormalizeAssetPath(path);
            var avatarFolder = GetAvatarOutputFolder(normalized);
            if (string.IsNullOrEmpty(avatarFolder)) return string.Empty;
            var relative = normalized.Substring(avatarFolder.Length).TrimStart('/');
            var separator = relative.IndexOf('/');
            if (separator <= 0) return string.Empty;
            return avatarFolder + "/" + relative.Substring(0, separator);
        }

        private static bool FolderContainsAssets(string folder)
        {
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path)) return true;
            }
            return false;
        }

        internal static void CleanupEmptyBuildOutputFolders()
        {
            CleanupEmptyBuildOutputFolders(OptimizerSettings.DefaultOutputFolder);
            CleanupEmptyBuildOutputFolders(OptimizerSettings.LegacyOutputFolder);
        }

        private static void CleanupEmptyBuildOutputFolders(string outputRoot)
        {
            var normalizedRoot = NormalizeAssetPath(outputRoot).TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(normalizedRoot)) return;
            foreach (var avatarFolder in AssetDatabase.GetSubFolders(normalizedRoot))
            {
                foreach (var buildFolder in AssetDatabase.GetSubFolders(avatarFolder))
                {
                    try
                    {
                        if (!FolderContainsAssets(buildFolder)) AssetDatabase.DeleteAsset(buildFolder);
                    }
                    catch
                    {
                        // Empty-folder cleanup must never fail a restore or rollback.
                    }
                }
            }
        }

        private static bool IsDatabasePath(string path)
        {
            var normalized = NormalizeAssetPath(path);
            return string.Equals(normalized, NormalizeAssetPath(DatabasePath),
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, NormalizeAssetPath(LegacyDatabasePath),
                       StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanRemoveUnrecoverableRecord(MMCBuildStatus status)
        {
            return status == MMCBuildStatus.SourceMissing ||
                status == MMCBuildStatus.SourceParentMissing;
        }

        public static bool RemoveUnrecoverableRecord(MMCBuildRecord record, out string message)
        {
            message = string.Empty;
            var database = Load();
            if (database == null || record == null || !database.Builds.Contains(record))
            {
                message = "The selected build record no longer exists.";
                return false;
            }
            var state = ResolveState(record);
            if (!CanRemoveUnrecoverableRecord(state.Status))
            {
                message = "This build is still recoverable. Open the referenced scene or restore it before removing the record.";
                return false;
            }
            Undo.RecordObject(database, "Remove Mesh Material Combiner build record");
            database.Builds.Remove(record);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            return true;
        }

        public static int RemoveUnrecoverableRecords(out string message)
        {
            message = string.Empty;
            var database = Load();
            if (database == null || database.Builds == null) return 0;
            var removable = new List<MMCBuildRecord>();
            foreach (var record in database.Builds)
            {
                if (record != null && CanRemoveUnrecoverableRecord(ResolveState(record).Status))
                    removable.Add(record);
            }
            if (removable.Count == 0)
            {
                message = "No unrecoverable build records were found.";
                return 0;
            }
            Undo.RecordObject(database, "Remove unrecoverable Mesh Material Combiner records");
            foreach (var record in removable) database.Builds.Remove(record);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            message = $"Removed {removable.Count} unrecoverable build record(s).";
            return removable.Count;
        }

        private static MMCBuildDatabase LoadOrCreate()
        {
            var database = Load();
            if (database != null) return database;
            EnsureFolder(OptimizerSettings.DefaultOutputFolder);
            database = ScriptableObject.CreateInstance<MMCBuildDatabase>();
            AssetDatabase.CreateAsset(database, DatabasePath);
            AssetDatabase.SaveAssets();
            return database;
        }

        private static MMCObjectLocator Capture(GameObject gameObject)
        {
            var locator = new MMCObjectLocator();
            if (gameObject == null) return locator;
            locator.DisplayName = gameObject.name;
            locator.ScenePath = gameObject.scene.IsValid() ? gameObject.scene.path : string.Empty;
            locator.HierarchyPath = GetHierarchyPath(gameObject.transform);
            try
            {
                locator.GlobalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(gameObject).ToString();
            }
            catch
            {
                locator.GlobalObjectId = string.Empty;
            }
            return locator;
        }

        private static GameObject Resolve(MMCObjectLocator locator)
        {
            if (locator == null) return null;
            if (!string.IsNullOrEmpty(locator.GlobalObjectId) &&
                GlobalObjectId.TryParse(locator.GlobalObjectId, out var globalId))
            {
                var resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId) as GameObject;
                // A parsed persistent ID is authoritative. Falling back to a reused
                // hierarchy path could connect an old build to an unrelated replacement.
                return resolved;
            }

            Scene scene = default;
            if (!string.IsNullOrEmpty(locator.ScenePath)) scene = SceneManager.GetSceneByPath(locator.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) return null;
            var parts = (locator.HierarchyPath ?? string.Empty).Split('/');
            if (parts.Length == 0) return null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;
                var current = root.transform;
                for (var i = 1; i < parts.Length && current != null; i++) current = current.Find(parts[i]);
                if (current != null) return current.gameObject;
            }
            return null;
        }

        private static bool IsExcludedSource(GameObject source)
        {
            if (source == null || source.activeInHierarchy) return false;
            for (var current = source.transform; current != null; current = current.parent)
                if (current.gameObject.CompareTag("EditorOnly")) return true;
            return false;
        }

        private static bool HasLocator(MMCObjectLocator locator)
        {
            return locator != null && (!string.IsNullOrEmpty(locator.GlobalObjectId) ||
                !string.IsNullOrEmpty(locator.ScenePath) || !string.IsNullOrEmpty(locator.HierarchyPath));
        }

        private static bool HasUnloadedSceneReference(MMCBuildRecord record)
        {
            if (record == null) return false;
            if (IsReferencedSceneUnloaded(record.AvatarRoot) ||
                IsReferencedSceneUnloaded(record.OutputObject)) return true;
            foreach (var source in record.Sources)
                if (IsReferencedSceneUnloaded(source.Object) ||
                    IsReferencedSceneUnloaded(source.OriginalParent)) return true;
            return false;
        }

        private static bool IsReferencedSceneUnloaded(MMCObjectLocator locator)
        {
            if (locator == null || string.IsNullOrEmpty(locator.ScenePath)) return false;
            var scene = SceneManager.GetSceneByPath(locator.ScenePath);
            if (scene.IsValid() && scene.isLoaded) return false;
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(locator.ScenePath) != null;
        }

        private static string GetLocatorKey(MMCObjectLocator locator)
        {
            if (locator == null) return string.Empty;
            return !string.IsNullOrEmpty(locator.GlobalObjectId)
                ? locator.GlobalObjectId
                : locator.ScenePath + "|" + locator.HierarchyPath;
        }

        private static string GetDisplayName(MMCObjectLocator locator)
        {
            if (locator == null || string.IsNullOrEmpty(locator.DisplayName)) return "<Unknown>";
            return locator.DisplayName;
        }

        private static string FormatNames(IList<string> names)
        {
            const int maxVisible = 8;
            var visible = new List<string>();
            for (var i = 0; i < Mathf.Min(maxVisible, names.Count); i++) visible.Add(names[i]);
            var result = string.Join(", ", visible);
            if (names.Count > maxVisible) result += $" (+{names.Count - maxVisible})";
            return result;
        }

        private static void DeleteEmptySourceContainer(Transform container)
        {
            if (container == null || container.name != SourceObjectOrganizer.ContainerName ||
                container.childCount != 0) return;
            Undo.DestroyObjectImmediate(container.gameObject);
        }

        private static void TrySetTag(GameObject gameObject, string tag)
        {
            try
            {
                gameObject.tag = string.IsNullOrEmpty(tag) ? "Untagged" : tag;
            }
            catch (UnityException)
            {
                gameObject.tag = "Untagged";
            }
        }

        private static int GetDepth(Transform transform)
        {
            var depth = 0;
            for (var current = transform; current != null; current = current.parent) depth++;
            return depth;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            var names = new List<string>();
            for (var current = transform; current != null; current = current.parent) names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Replace('\\', '/').TrimEnd('/').Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
