using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal sealed class ScenePreviewController
    {
        private readonly Dictionary<Renderer, bool> _sourceStates = new Dictionary<Renderer, bool>();
        private readonly List<UnityEngine.Object> _runtimeAssets = new List<UnityEngine.Object>();
        private GameObject _container;
        private SkinnedMeshRenderer _previewRenderer;

        public bool IsActive
        {
            get
            {
                if (_container == null && _sourceStates.Count > 0) Cleanup();
                return _container != null;
            }
        }
        public ScenePreviewMode Mode { get; private set; } = ScenePreviewMode.Optimized;

        public void Build(OptimizerSettings settings, List<RendererEntry> entries, OptimizationAnalysis analysis)
        {
            Cleanup();
            if (settings.TargetRoot == null || analysis == null || analysis.Report.HasErrors)
                throw new InvalidOperationException("Analyze the selection and resolve errors before creating a preview.");

            try
            {
                foreach (var entry in entries)
                    if (entry.Included && entry.Renderer != null)
                        _sourceStates[entry.Renderer] = entry.Renderer.enabled;

                _container = new GameObject("__MeshMaterialCombiner_Preview");
                _container.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                _container.transform.SetParent(AvatarRootResolver.Resolve(settings.TargetRoot).transform, false);
                var mergedObject = new GameObject(AssetOutputManager.Sanitize(settings.GroupName) + "_Merged");
                mergedObject.hideFlags = _container.hideFlags;
                mergedObject.transform.SetParent(_container.transform, false);

                var previewAtlasSize = Mathf.Min(settings.AtlasSize, 2048);
                foreach (var group in analysis.MaterialGroups)
                {
                    if (settings.Preset == OptimizationPreset.Safe || group.Adapter is UnmodifiedMaterialAdapter)
                    {
                        group.MergedMaterial = group.Materials[0];
                        foreach (var groupMaterial in group.Materials)
                            group.AtlasRects[groupMaterial] = new Rect(0f, 0f, 1f, 1f);
                        continue;
                    }
                    var properties = MaterialAtlasPlanner.GetProperties(group);
                    foreach (var property in properties)
                    {
                        var atlas = TextureAtlasBuilder.BuildTextureAtlas(group, property, previewAtlasSize,
                            property.IsNormalMap);
                        group.Atlases[property.PropertyName] = atlas;
                        if (property.IsBaseTexture) group.MainAtlas = atlas;
                        _runtimeAssets.Add(atlas);
                    }
                    var material = MaterialMerger.CreateMergedMaterial(group, properties);
                    group.MergedMaterial = material;
                    _runtimeAssets.Add(material);
                }

                var merged = MeshMerger.Merge(entries, analysis.MaterialGroups, mergedObject.transform,
                    settings.TargetRoot,
                    TransformPlacementResolver.FindLowestCommonAncestor(entries, settings.TargetRoot.transform),
                    settings.BlendShapeMode, settings.Preset == OptimizationPreset.Safe);
                _runtimeAssets.Add(merged.Mesh);
                _previewRenderer = mergedObject.AddComponent<SkinnedMeshRenderer>();
                _previewRenderer.sharedMesh = merged.Mesh;
                _previewRenderer.sharedMaterials = merged.Materials.ToArray();
                _previewRenderer.bones = merged.Bones;
                _previewRenderer.rootBone = merged.RootBone;
                _previewRenderer.localBounds = merged.LocalBounds;
                AssetOutputManager.ApplyBlendShapeWeights(_previewRenderer, merged);
                SetMode(ScenePreviewMode.Optimized);
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        public void SetMode(ScenePreviewMode mode)
        {
            if (!IsActive) return;
            Mode = mode;
            var showSources = mode != ScenePreviewMode.Optimized;
            foreach (var pair in _sourceStates)
                if (pair.Key != null) pair.Key.enabled = showSources && pair.Value;
            if (_previewRenderer != null)
                _previewRenderer.enabled = mode != ScenePreviewMode.Original;
        }

        public void Cleanup()
        {
            foreach (var pair in _sourceStates)
                if (pair.Key != null) pair.Key.enabled = pair.Value;
            _sourceStates.Clear();
            if (_container != null) UnityEngine.Object.DestroyImmediate(_container);
            _container = null;
            _previewRenderer = null;
            for (var i = _runtimeAssets.Count - 1; i >= 0; i--)
                if (_runtimeAssets[i] != null) UnityEngine.Object.DestroyImmediate(_runtimeAssets[i]);
            _runtimeAssets.Clear();
        }
    }
}
