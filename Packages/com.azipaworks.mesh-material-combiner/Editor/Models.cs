using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal enum BlendShapeCollisionMode
    {
        MergeSameName,
        PrefixByRendererName,
        KeepSeparate
    }

    internal enum ValidationSeverity { Info, Warning, Error }

    internal enum OptimizationPreset
    {
        Safe = 0,
        CompatibleMerge = 1,
        // Kept only so recipes saved by older releases retain their numeric value.
        // It is hidden from the UI and normalized to CompatibleMerge when loaded.
        AggressiveLegacy = 2,
        ForceSingleSlot = 3
    }

    internal static class OptimizationPresetUtility
    {
        public static OptimizationPreset Normalize(OptimizationPreset preset)
        {
            return preset == OptimizationPreset.AggressiveLegacy
                ? OptimizationPreset.CompatibleMerge
                : preset;
        }
    }

    internal enum ScenePreviewMode
    {
        Original,
        Optimized,
        Both
    }

    [Serializable]
    internal sealed class OptimizerSettings
    {
        public const string DefaultOutputFolder = "Assets/Azipa Works/MeshMaterialCombinerGenerated/outputs/mmc_generated";
        public const string LegacyOutputFolder = "Assets/Azipa Works/tools/MeshMaterialCombiner/outputs/AvatarOptimizerGenerated";
        public GameObject TargetRoot;
        public string OutputFolder = DefaultOutputFolder;
        public string GroupName = "Avatar";
        // Optional exact name for the generated merged GameObject. Assets keep
        // using GroupName so existing output naming remains compatible.
        public string MergedObjectName = string.Empty;
        public int AtlasSize = 2048;
        public OptimizationPreset Preset = OptimizationPreset.Safe;
        public Material ForceRepresentativeMaterial;
        public BlendShapeCollisionMode BlendShapeMode = BlendShapeCollisionMode.PrefixByRendererName;
    }

    internal sealed class RendererEntry
    {
        public Renderer Renderer;
        public Mesh Mesh;
        public bool Included = true;
        // Direct-object selection and renderer inclusion are intentionally kept
        // separate. Included controls mesh merging only.
        public int VertexCount;
        public int TriangleCount;
        public int MaterialSlotCount;
        public Material[] Materials = Array.Empty<Material>();

        public string Kind => Renderer is SkinnedMeshRenderer ? "SkinnedMeshRenderer" : "MeshRenderer";
    }

    internal sealed class ValidationIssue
    {
        public ValidationSeverity Severity;
        public string Message;
        public UnityEngine.Object Context;

        public ValidationIssue(ValidationSeverity severity, string message, UnityEngine.Object context = null)
        {
            Severity = severity;
            Message = message;
            Context = context;
        }
    }

    [Serializable]
    internal sealed class OptimizationStatistics
    {
        public int RendererCount;
        public int MaterialSlots;
        public int UniqueMaterials;
        public long Vertices;
        public long Triangles;
    }

    internal sealed class OptimizationReport
    {
        public readonly List<ValidationIssue> Issues = new List<ValidationIssue>();
        public OptimizationStatistics Before = new OptimizationStatistics();
        public OptimizationStatistics After = new OptimizationStatistics();
        public string ResultMessage = string.Empty;
        public bool HasErrors => Issues.Exists(i => i.Severity == ValidationSeverity.Error);
    }

    internal sealed class TexturePropertyInfo
    {
        public string PropertyName;
        public string DisplayName;
        public bool IsNormalMap;
        public bool IsLinear;
        public bool IsBaseTexture;
        public Color DefaultColor;

        public TexturePropertyInfo(string propertyName, string displayName, bool isNormalMap = false,
            bool isLinear = false, bool isBaseTexture = false, Color? defaultColor = null)
        {
            PropertyName = propertyName;
            DisplayName = displayName;
            IsNormalMap = isNormalMap;
            IsLinear = isLinear || isNormalMap;
            IsBaseTexture = isBaseTexture;
            DefaultColor = defaultColor ?? (isNormalMap
                ? new Color(0.5f, 0.5f, 1f, 1f)
                : Color.white);
        }
    }

    internal sealed class MaterialGroup
    {
        public int Index;
        public IMaterialMergeAdapter Adapter;
        public readonly List<Material> Materials = new List<Material>();
        public readonly Dictionary<Material, Rect> AtlasRects = new Dictionary<Material, Rect>();
        public readonly Dictionary<string, Texture2D> Atlases = new Dictionary<string, Texture2D>();
        public Material MergedMaterial;
        public Texture2D MainAtlas;
    }

    internal sealed class OptimizationAnalysis
    {
        public readonly List<RendererEntry> Renderers = new List<RendererEntry>();
        public readonly List<MaterialGroup> MaterialGroups = new List<MaterialGroup>();
        public readonly OptimizationReport Report = new OptimizationReport();
    }

    internal readonly struct AtlasMemoryEstimate
    {
        public readonly int AtlasCount;
        public readonly long PeakBytes;

        public AtlasMemoryEstimate(int atlasCount, long peakBytes)
        {
            AtlasCount = atlasCount;
            PeakBytes = peakBytes;
        }
    }

    internal sealed class MeshMergeResult
    {
        public Mesh Mesh;
        public readonly List<Material> Materials = new List<Material>();
        public readonly Dictionary<string, float> BlendShapeWeights = new Dictionary<string, float>();
        public Transform[] Bones = Array.Empty<Transform>();
        public Transform RootBone;
        public Bounds LocalBounds;
    }
}
