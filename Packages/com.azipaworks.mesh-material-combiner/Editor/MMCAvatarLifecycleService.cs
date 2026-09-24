using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class MMCAvatarLifecycleService
    {
        public static bool SuspendAvatar(IList<MMCBuildRecord> records, out string message)
        {
            message = string.Empty;
            if (!TryGetExactDependencyOrder(records, out var order, out message)) return false;
            foreach (var record in order)
            {
                if (record.Lifecycle == MMCBuildLifecycle.SuspendedForEditing) continue;
                if (MMCBuildDatabaseService.TryFindSourceOwnershipConflict(record,
                        out var ownershipConflict))
                {
                    message = BuildFailure(record,
                        MMCBuildDatabaseService.FormatSourceOwnershipConflict(ownershipConflict));
                    return false;
                }
                if (record.Lifecycle == MMCBuildLifecycle.CleanupPending)
                {
                    message = OptimizerLocalization.T("Cleanup Pending Cannot Suspend");
                    return false;
                }
                var state = MMCBuildDatabaseService.ResolveState(record);
                if (state.Output == null || state.Status == MMCBuildStatus.SourceMissing ||
                    state.Status == MMCBuildStatus.SourceParentMissing ||
                    state.Status == MMCBuildStatus.SceneNotLoaded)
                {
                    message = BuildFailure(record, OptimizerLocalization.T("Avatar Suspend Preflight Failed"));
                    return false;
                }
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Suspend all MMC builds for avatar");
            var suspended = 0;
            try
            {
                for (var i = order.Count - 1; i >= 0; i--)
                {
                    var record = order[i];
                    if (record.Lifecycle == MMCBuildLifecycle.SuspendedForEditing) continue;
                    if (!MMCBuildDatabaseService.SuspendForEditing(record, out var failure))
                        throw new InvalidOperationException(BuildFailure(record, failure));
                    suspended++;
                }
                Undo.CollapseUndoOperations(undoGroup);
                message = string.Format(OptimizerLocalization.T("Avatar Suspended"), suspended);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                message = exception.Message;
                return false;
            }
        }

        public static bool RebuildAvatar(IList<MMCBuildRecord> records, out string message)
        {
            message = string.Empty;
            if (!TryGetExactDependencyOrder(records, out var order, out message)) return false;
            foreach (var record in order)
            {
                if (!ValidateRebuild(record, out var failure))
                {
                    message = BuildFailure(record, failure);
                    return false;
                }
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Prepare MMC avatar for upload");
            var createdAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rebuilt = 0;
            try
            {
                foreach (var record in order)
                {
                    var previousRevisionCount = record.Revisions != null ? record.Revisions.Count : 0;
                    if (!MMCRebuildService.Rebuild(record, out var failure))
                        throw new InvalidOperationException(BuildFailure(record, failure));
                    rebuilt++;
                    if (record.Revisions == null || record.Revisions.Count <= previousRevisionCount) continue;
                    var revision = record.Revisions[record.Revisions.Count - 1];
                    if (revision == null || revision.GeneratedAssetPaths == null) continue;
                    foreach (var path in revision.GeneratedAssetPaths)
                        if (!string.IsNullOrEmpty(path)) createdAssets.Add(path);
                }
                Undo.CollapseUndoOperations(undoGroup);
                message = string.Format(OptimizerLocalization.T("Avatar Prepared"), rebuilt);
                return true;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                foreach (var path in createdAssets)
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                        AssetDatabase.DeleteAsset(path);
                MMCBuildDatabaseService.CleanupEmptyBuildOutputFolders();
                AssetDatabase.SaveAssets();
                message = exception.Message;
                return false;
            }
        }

        private static bool ValidateRebuild(MMCBuildRecord record, out string message)
        {
            message = string.Empty;
            if (record == null || record.Recipe == null || !record.Recipe.IsValid)
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
            if (!MMCRebuildService.TryResolveEntries(record.Recipe, out var entries, out message))
                return false;
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
            if (!analysis.Report.HasErrors) return true;
            message = OptimizerLocalization.T("Rebuild Validation Failed");
            return false;
        }

        private static bool TryGetExactDependencyOrder(IList<MMCBuildRecord> records,
            out List<MMCBuildRecord> order, out string message)
        {
            message = string.Empty;
            order = new List<MMCBuildRecord>();
            if (records == null || records.Count == 0)
            {
                message = OptimizerLocalization.T("No Managed Builds");
                return false;
            }
            var requested = new HashSet<MMCBuildRecord>();
            foreach (var record in records)
                if (record != null) requested.Add(record);
            order = MMCRebuildService.GetRebuildOrder(new List<MMCBuildRecord>(requested), out message);
            if (order.Count == 0) return false;
            if (order.Count != requested.Count)
            {
                order.Clear();
                message = OptimizerLocalization.T("Rebuild Recipe Missing");
                return false;
            }
            foreach (var record in order)
                if (!requested.Contains(record))
                {
                    order.Clear();
                    message = OptimizerLocalization.T("External Build Dependency");
                    return false;
                }
            return true;
        }

        private static string BuildFailure(MMCBuildRecord record, string failure)
        {
            var name = record != null && !string.IsNullOrEmpty(record.BuildName)
                ? record.BuildName
                : "(Unnamed Build)";
            return name + ": " + failure;
        }
    }
}
