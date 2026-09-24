using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class MaterialTextureUtility
    {
        private static readonly string[] BaseTextureProperties =
        {
            "_MainTex", "_BaseMap", "_BaseColorMap", "_BaseColorTexture"
        };

        public static string FindBaseTextureProperty(Material material)
        {
            if (material == null) return null;
            foreach (var property in BaseTextureProperties)
                if (material.HasProperty(property)) return property;
            return null;
        }

        public static bool IsBaseTextureProperty(string property)
        {
            foreach (var candidate in BaseTextureProperties)
                if (candidate == property) return true;
            return false;
        }

        public static Texture GetBaseTexture(Material material)
        {
            var property = FindBaseTextureProperty(material);
            return property != null ? material.GetTexture(property) : null;
        }

        public static Vector2 GetTextureScale(Material material)
        {
            var property = FindBaseTextureProperty(material);
            return property != null ? material.GetTextureScale(property) : Vector2.one;
        }

        public static Vector2 GetTextureOffset(Material material)
        {
            var property = FindBaseTextureProperty(material);
            return property != null ? material.GetTextureOffset(property) : Vector2.zero;
        }

        public static Color GetBaseColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            return Color.white;
        }
    }

    internal interface IMaterialMergeAdapter
    {
        string Name { get; }
        bool CanHandle(Material material);
        IEnumerable<TexturePropertyInfo> GetTextureProperties(Material material);
        bool CanMerge(Material a, Material b, out string reason);
        Material CreateMergedMaterial(Material template, IDictionary<string, Texture2D> atlases,
            IEnumerable<TexturePropertyInfo> properties);
    }

    internal abstract class MaterialMergeAdapterBase : IMaterialMergeAdapter
    {
        public abstract string Name { get; }
        public abstract bool CanHandle(Material material);

        public virtual IEnumerable<TexturePropertyInfo> GetTextureProperties(Material material)
        {
            var property = MaterialTextureUtility.FindBaseTextureProperty(material);
            if (property != null)
                yield return new TexturePropertyInfo(property, "Main Texture",
                    isBaseTexture: true, defaultColor: Color.white);
        }

        public virtual bool CanMerge(Material a, Material b, out string reason)
        {
            return MaterialCompatibilityChecker.AreCompatibleForAtlasing(a, b, this, out reason);
        }

        public virtual Material CreateMergedMaterial(Material template,
            IDictionary<string, Texture2D> atlases, IEnumerable<TexturePropertyInfo> properties)
        {
            var merged = new Material(template) { name = template.name + "_Merged" };
            foreach (var property in properties)
            {
                if (!atlases.TryGetValue(property.PropertyName, out var atlas) || atlas == null) continue;
                var targetProperty = property.IsBaseTexture
                    ? MaterialTextureUtility.FindBaseTextureProperty(merged)
                    : property.PropertyName;
                if (string.IsNullOrEmpty(targetProperty) || !merged.HasProperty(targetProperty)) continue;
                merged.SetTexture(targetProperty, atlas);
                merged.SetTextureScale(targetProperty, Vector2.one);
                merged.SetTextureOffset(targetProperty, Vector2.zero);
            }
            if (merged.HasProperty("_Color")) merged.SetColor("_Color", Color.white);
            if (merged.HasProperty("_BaseColor")) merged.SetColor("_BaseColor", Color.white);
            return merged;
        }
    }

    internal sealed class LilToonMaterialMergeAdapter : MaterialMergeAdapterBase
    {
        public override string Name => "lilToon";
        public override bool CanHandle(Material material)
        {
            return material != null && material.shader != null &&
                   material.shader.name.IndexOf("lilToon", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public override IEnumerable<TexturePropertyInfo> GetTextureProperties(Material material)
        {
            foreach (var property in base.GetTextureProperties(material)) yield return property;
            if (material != null && material.HasProperty("_BumpMap"))
                yield return new TexturePropertyInfo("_BumpMap", "Normal Map", true);
            if (material != null && material.HasProperty("_Bump2ndMap") &&
                UsesUv0(material, "_Bump2ndMap_UVMode"))
                yield return new TexturePropertyInfo("_Bump2ndMap", "2nd Normal Map", true);
            if (material != null && material.HasProperty("_AlphaMask"))
                yield return new TexturePropertyInfo("_AlphaMask", "Alpha Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_Main2ndBlendMask"))
                yield return new TexturePropertyInfo("_Main2ndBlendMask", "2nd Main Blend Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_Main3rdBlendMask"))
                yield return new TexturePropertyInfo("_Main3rdBlendMask", "3rd Main Blend Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_EmissionMap") &&
                UsesUv0(material, "_EmissionMap_UVMode") &&
                HasNoUvAnimation(material, "_EmissionMap_ScrollRotate"))
                yield return new TexturePropertyInfo("_EmissionMap", "Emission");
            if (material != null && material.HasProperty("_EmissionBlendMask") &&
                UsesUv0(material, "_EmissionMap_UVMode") &&
                HasNoUvAnimation(material, "_EmissionBlendMask_ScrollRotate"))
                yield return new TexturePropertyInfo("_EmissionBlendMask", "Emission Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_Emission2ndMap") &&
                UsesUv0(material, "_Emission2ndMap_UVMode") &&
                HasNoUvAnimation(material, "_Emission2ndMap_ScrollRotate"))
                yield return new TexturePropertyInfo("_Emission2ndMap", "2nd Emission");
            if (material != null && material.HasProperty("_Emission2ndBlendMask") &&
                UsesUv0(material, "_Emission2ndMap_UVMode") &&
                HasNoUvAnimation(material, "_Emission2ndBlendMask_ScrollRotate"))
                yield return new TexturePropertyInfo("_Emission2ndBlendMask", "2nd Emission Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_ShadowColorTex"))
                yield return new TexturePropertyInfo("_ShadowColorTex", "Shadow Color",
                    defaultColor: Color.black);
            if (material != null && material.HasProperty("_Shadow2ndColorTex"))
                yield return new TexturePropertyInfo("_Shadow2ndColorTex", "2nd Shadow Color",
                    defaultColor: Color.black);
            if (material != null && material.HasProperty("_Shadow3rdColorTex"))
                yield return new TexturePropertyInfo("_Shadow3rdColorTex", "3rd Shadow Color",
                    defaultColor: Color.black);
            if (material != null && material.HasProperty("_ShadowStrengthMask"))
                yield return new TexturePropertyInfo("_ShadowStrengthMask", "Shadow Strength Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_MatCapBlendMask"))
                yield return new TexturePropertyInfo("_MatCapBlendMask", "MatCap Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_MatCap2ndBlendMask"))
                yield return new TexturePropertyInfo("_MatCap2ndBlendMask", "2nd MatCap Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_RimColorTex"))
                yield return new TexturePropertyInfo("_RimColorTex", "Rim Mask");
            if (material != null && material.HasProperty("_MetallicGlossMap"))
                yield return new TexturePropertyInfo("_MetallicGlossMap", "Metallic Map",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_SmoothnessTex"))
                yield return new TexturePropertyInfo("_SmoothnessTex", "Smoothness Map",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_OutlineTex") &&
                HasNoUvAnimation(material, "_OutlineTex_ScrollRotate"))
                yield return new TexturePropertyInfo("_OutlineTex", "Outline Texture");
            if (material != null && material.HasProperty("_OutlineWidthMask"))
                yield return new TexturePropertyInfo("_OutlineWidthMask", "Outline Width Mask",
                    isLinear: true, defaultColor: Color.white);
        }

        private static bool UsesUv0(Material material, string uvModeProperty)
        {
            return material == null || !material.HasProperty(uvModeProperty) ||
                   Mathf.Approximately(material.GetFloat(uvModeProperty), 0f);
        }

        private static bool HasNoUvAnimation(Material material, string property)
        {
            if (material == null || !material.HasProperty(property)) return true;
            return material.GetVector(property).sqrMagnitude < 0.0000001f;
        }
    }

    internal sealed class GenericMaterialMergeAdapter : MaterialMergeAdapterBase
    {
        public override string Name => "Generic MainTex";
        public override bool CanHandle(Material material)
        {
            return material != null && material.shader != null &&
                   MaterialTextureUtility.FindBaseTextureProperty(material) != null;
        }

        public override IEnumerable<TexturePropertyInfo> GetTextureProperties(Material material)
        {
            foreach (var property in base.GetTextureProperties(material)) yield return property;
            if (material != null && material.HasProperty("_BumpMap"))
                yield return new TexturePropertyInfo("_BumpMap", "Normal Map", true);
            if (material != null && material.HasProperty("_EmissionMap"))
                yield return new TexturePropertyInfo("_EmissionMap", "Emission");
            if (material != null && material.HasProperty("_AlphaMask"))
                yield return new TexturePropertyInfo("_AlphaMask", "Alpha Mask",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_MetallicGlossMap"))
                yield return new TexturePropertyInfo("_MetallicGlossMap", "Metallic Map",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_SmoothnessTex"))
                yield return new TexturePropertyInfo("_SmoothnessTex", "Smoothness Map",
                    isLinear: true, defaultColor: Color.white);
            if (material != null && material.HasProperty("_OcclusionMap"))
                yield return new TexturePropertyInfo("_OcclusionMap", "Occlusion Map",
                    isLinear: true, defaultColor: Color.white);
        }
    }

    // Safe mode does not build an atlas and therefore can preserve materials that
    // do not expose a supported texture property. This adapter is only a metadata
    // placeholder for the MaterialGroup; it is never used to create a merged asset.
    internal sealed class UnmodifiedMaterialAdapter : MaterialMergeAdapterBase
    {
        public override string Name => "Unmodified";
        public override bool CanHandle(Material material) => material != null;
    }

    internal static class MaterialAdapterRegistry
    {
        private static readonly IMaterialMergeAdapter[] Adapters =
        {
            new LilToonMaterialMergeAdapter(),
            new GenericMaterialMergeAdapter()
        };

        public static IMaterialMergeAdapter Find(Material material)
        {
            foreach (var adapter in Adapters)
                if (adapter.CanHandle(material)) return adapter;
            return null;
        }
    }

    internal static class MaterialCompatibilityChecker
    {
        public static bool AreCompatibleForAtlasing(Material a, Material b,
            IMaterialMergeAdapter adapter, out string reason)
        {
            if (!AreCriticalRenderStatesCompatible(a, b, out reason)) return false;
            if (a.enableInstancing != b.enableInstancing || a.doubleSidedGI != b.doubleSidedGI ||
                a.globalIlluminationFlags != b.globalIlluminationFlags)
            { reason = "Material rendering flags differ."; return false; }
            if (!SetEquals(a.shaderKeywords, b.shaderKeywords))
            { reason = "Shader keywords differ."; return false; }

            var atlasedProperties = new HashSet<string>();
            if (adapter != null)
                foreach (var property in adapter.GetTextureProperties(a))
                    if (property != null && !string.IsNullOrEmpty(property.PropertyName))
                        atlasedProperties.Add(property.PropertyName);

            var shader = a.shader;
            var count = ShaderUtil.GetPropertyCount(shader);
            for (var i = 0; i < count; i++)
            {
                var name = ShaderUtil.GetPropertyName(shader, i);
                if (MaterialTextureUtility.IsBaseTextureProperty(name) ||
                    name == "_Color" || name == "_BaseColor" || atlasedProperties.Contains(name))
                    continue;
                var type = ShaderUtil.GetPropertyType(shader, i);
                switch (type)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        if (!Approximately(a.GetColor(name), b.GetColor(name)))
                        { reason = name + " differs."; return false; }
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        if (!Approximately(a.GetVector(name), b.GetVector(name)))
                        { reason = name + " differs."; return false; }
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        if (!Mathf.Approximately(a.GetFloat(name), b.GetFloat(name)))
                        { reason = name + " differs."; return false; }
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        var textureA = a.GetTexture(name);
                        var textureB = b.GetTexture(name);
                        if (!IsStableNonMeshTexture(name) &&
                            (IsAssignedAsset(textureA) || IsAssignedAsset(textureB)))
                        {
                            reason = name + " is populated but cannot be atlased safely.";
                            return false;
                        }
                        if (textureA != textureB ||
                            a.GetTextureScale(name) != b.GetTextureScale(name) ||
                            a.GetTextureOffset(name) != b.GetTextureOffset(name))
                        { reason = name + " differs."; return false; }
                        break;
                }
            }
            reason = string.Empty;
            return true;
        }

        private static bool IsAssignedAsset(Texture texture)
        {
            return texture != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture));
        }

        private static bool IsStableNonMeshTexture(string property)
        {
            return property.IndexOf("MatCap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   property.IndexOf("Cube", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   property.IndexOf("Reflection", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool AreCompatible(Material a, Material b, out string reason)
        {
            if (!AreCriticalRenderStatesCompatible(a, b, out reason)) return false;
            if (a.enableInstancing != b.enableInstancing || a.doubleSidedGI != b.doubleSidedGI ||
                a.globalIlluminationFlags != b.globalIlluminationFlags)
            { reason = "Material rendering flags differ."; return false; }
            if (!SetEquals(a.shaderKeywords, b.shaderKeywords)) { reason = "Shader keywords differ."; return false; }

            var shader = a.shader;
            var count = ShaderUtil.GetPropertyCount(shader);
            for (var i = 0; i < count; i++)
            {
                var name = ShaderUtil.GetPropertyName(shader, i);
                if (MaterialTextureUtility.IsBaseTextureProperty(name) ||
                    name == "_Color" || name == "_BaseColor") continue;
                var type = ShaderUtil.GetPropertyType(shader, i);
                switch (type)
                {
                    case ShaderUtil.ShaderPropertyType.Color:
                        if (!Approximately(a.GetColor(name), b.GetColor(name))) { reason = name + " differs."; return false; }
                        break;
                    case ShaderUtil.ShaderPropertyType.Vector:
                        if (!Approximately(a.GetVector(name), b.GetVector(name))) { reason = name + " differs."; return false; }
                        break;
                    case ShaderUtil.ShaderPropertyType.Float:
                    case ShaderUtil.ShaderPropertyType.Range:
                        if (!Mathf.Approximately(a.GetFloat(name), b.GetFloat(name))) { reason = name + " differs."; return false; }
                        break;
                    case ShaderUtil.ShaderPropertyType.TexEnv:
                        if (a.GetTexture(name) != b.GetTexture(name) || a.GetTextureScale(name) != b.GetTextureScale(name) ||
                            a.GetTextureOffset(name) != b.GetTextureOffset(name))
                        { reason = name + " differs."; return false; }
                        break;
                }
            }
            return true;
        }

        public static bool AreCriticalRenderStatesCompatible(Material a, Material b, out string reason)
        {
            reason = string.Empty;
            if (a == null || b == null) { reason = "Material is missing."; return false; }
            if (a.shader != b.shader) { reason = "Shader differs."; return false; }
            if (a.renderQueue != b.renderQueue) { reason = "Render Queue differs."; return false; }
            if (a.GetTag("RenderType", false, string.Empty) != b.GetTag("RenderType", false, string.Empty))
            { reason = "RenderType differs."; return false; }

            var criticalProperties = new[]
            {
                "_SrcBlend", "_DstBlend", "_SrcBlendAlpha", "_DstBlendAlpha",
                "_BlendOp", "_BlendOpAlpha", "_Cull", "_ZWrite", "_ZTest", "_ColorMask",
                "_BlendMode", "_TransparentMode"
            };
            foreach (var property in criticalProperties)
            {
                if (a.HasProperty(property) != b.HasProperty(property))
                { reason = property + " availability differs."; return false; }
                if (a.HasProperty(property) && !Mathf.Approximately(a.GetFloat(property), b.GetFloat(property)))
                { reason = property + " differs."; return false; }
            }
            return true;
        }

        private static bool SetEquals(string[] a, string[] b)
        {
            var aa = new HashSet<string>(a ?? Array.Empty<string>());
            return aa.SetEquals(b ?? Array.Empty<string>());
        }

        private static bool Approximately(Vector4 a, Vector4 b) => (a - b).sqrMagnitude < 0.0000001f;
    }

    internal static class MaterialAnalyzer
    {
        public static List<MaterialGroup> BuildGroups(IEnumerable<RendererEntry> entries,
            List<ValidationIssue> issues, OptimizationPreset preset, Material forceRepresentativeMaterial = null)
        {
            preset = OptimizationPresetUtility.Normalize(preset);
            if (preset == OptimizationPreset.ForceSingleSlot)
                return BuildForceSingleGroup(entries, issues, forceRepresentativeMaterial);

            var groups = new List<MaterialGroup>();
            var seen = new HashSet<Material>();
            foreach (var entry in entries)
            {
                if (!entry.Included) continue;
                var usedSlots = entry.Mesh != null ? Mathf.Min(entry.Mesh.subMeshCount, entry.Materials.Length) : entry.Materials.Length;
                for (var slot = 0; slot < usedSlots; slot++)
                {
                    var material = entry.Materials[slot];
                    if (material == null || !seen.Add(material)) continue;
                    var adapter = MaterialAdapterRegistry.Find(material);
                    if (adapter == null)
                    {
                        // Unsupported shaders remain as an isolated material group.
                        // They cannot participate in an atlas, but they should not
                        // prevent otherwise valid meshes from being merged.
                        issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                            $"Material '{material.name}' has no supported main texture property and will be preserved without atlas merging.", material));
                        var unmodified = new MaterialGroup
                        {
                            Index = groups.Count,
                            Adapter = new UnmodifiedMaterialAdapter()
                        };
                        unmodified.Materials.Add(material);
                        groups.Add(unmodified);
                        continue;
                    }

                    MaterialGroup destination = null;
                    // Safe mode deliberately keeps every distinct Material in its own
                    // group. Duplicate references are still collapsed later by the
                    // SubMeshMerger, but no cross-material Atlas merge is performed.
                    IEnumerable<MaterialGroup> candidateGroups = preset == OptimizationPreset.Safe
                        ? Array.Empty<MaterialGroup>()
                        : groups;
                    foreach (var group in candidateGroups)
                    {
                        if (group.Adapter.GetType() != adapter.GetType()) continue;
                        var template = group.Materials[0];
                        bool compatible;
                        if (preset == OptimizationPreset.CompatibleMerge)
                            compatible = adapter.CanMerge(template, material, out _);
                        else
                            compatible = false;
                        if (!compatible) continue;
                        destination = group;
                        if (preset != OptimizationPreset.Safe &&
                            !adapter.CanMerge(template, material, out var strictReason))
                            issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                                $"{preset} preset merges '{material.name}' into the group using '{template.name}'. " +
                                $"Non-atlased properties from '{template.name}' will be used ({strictReason})", material));
                        break;
                    }
                    if (destination == null)
                    {
                        destination = new MaterialGroup { Index = groups.Count, Adapter = adapter };
                        groups.Add(destination);
                    }
                    destination.Materials.Add(material);
                }
            }
            return groups;
        }

        private static List<MaterialGroup> BuildForceSingleGroup(IEnumerable<RendererEntry> entries,
            List<ValidationIssue> issues, Material requestedRepresentative)
        {
            var materials = new List<Material>();
            var seen = new HashSet<Material>();
            foreach (var entry in entries)
            {
                if (!entry.Included) continue;
                var usedSlots = entry.Mesh != null
                    ? Mathf.Min(entry.Mesh.subMeshCount, entry.Materials.Length)
                    : entry.Materials.Length;
                for (var slot = 0; slot < usedSlots; slot++)
                {
                    var material = entry.Materials[slot];
                    if (material != null && seen.Add(material)) materials.Add(material);
                }
            }

            if (materials.Count == 0) return new List<MaterialGroup>();

            Material representative = null;
            if (requestedRepresentative != null)
            {
                if (!seen.Contains(requestedRepresentative))
                    issues.Add(new ValidationIssue(ValidationSeverity.Error,
                        $"Representative Material '{requestedRepresentative.name}' is not used by the selected Renderers.",
                        requestedRepresentative));
                else
                    representative = requestedRepresentative;
            }

            if (representative == null)
            {
                representative = materials.Find(material =>
                    MaterialAdapterRegistry.Find(material) != null &&
                    MaterialTextureUtility.FindBaseTextureProperty(material) != null);
            }

            if (representative == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    "Force Single Slot requires at least one selected Material with a supported base texture property."));
                representative = materials[0];
            }

            var adapter = MaterialAdapterRegistry.Find(representative);
            if (adapter == null || MaterialTextureUtility.FindBaseTextureProperty(representative) == null)
            {
                issues.Add(new ValidationIssue(ValidationSeverity.Error,
                    $"Representative Material '{representative.name}' has no supported base texture property.",
                    representative));
                adapter = new UnmodifiedMaterialAdapter();
            }

            var group = new MaterialGroup { Index = 0, Adapter = adapter };
            group.Materials.Add(representative);
            foreach (var material in materials)
                if (material != representative) group.Materials.Add(material);

            AddForceSingleSlotDiagnostics(group, issues);
            return new List<MaterialGroup> { group };
        }

        private static void AddForceSingleSlotDiagnostics(MaterialGroup group, List<ValidationIssue> issues)
        {
            var representative = group.Materials[0];
            issues.Add(new ValidationIssue(ValidationSeverity.Info,
                $"Force Single Slot representative: '{representative.name}' / Shader: '{representative.shader?.name ?? "Missing"}'.",
                representative));

            var shaders = new HashSet<Shader>();
            var noBaseTexture = new List<string>();
            var incompatibleReasons = new HashSet<string>();
            var nonAtlasedProperties = new HashSet<string>();
            var supportedAtlasProperties = new HashSet<string>();
            foreach (var property in group.Adapter.GetTextureProperties(representative))
                if (property != null && !string.IsNullOrEmpty(property.PropertyName))
                    supportedAtlasProperties.Add(property.PropertyName);
            foreach (var material in group.Materials)
            {
                if (material.shader != null) shaders.Add(material.shader);
                if (MaterialTextureUtility.FindBaseTextureProperty(material) == null)
                    noBaseTexture.Add(material.name);

                if (material != representative &&
                    !MaterialCompatibilityChecker.AreCompatible(representative, material, out var reason) &&
                    !string.IsNullOrEmpty(reason))
                    incompatibleReasons.Add(reason);

                if (material.shader == null) continue;
                var baseProperty = MaterialTextureUtility.FindBaseTextureProperty(material);
                var propertyCount = ShaderUtil.GetPropertyCount(material.shader);
                for (var i = 0; i < propertyCount; i++)
                {
                    if (ShaderUtil.GetPropertyType(material.shader, i) != ShaderUtil.ShaderPropertyType.TexEnv)
                        continue;
                    var property = ShaderUtil.GetPropertyName(material.shader, i);
                    if (property == baseProperty || supportedAtlasProperties.Contains(property) ||
                        material.GetTexture(property) == null) continue;
                    nonAtlasedProperties.Add(property);
                }
            }

            if (shaders.Count > 1)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                    $"Force Single Slot will replace {shaders.Count} source Shaders with representative Shader '{representative.shader?.name ?? "Missing"}'.",
                    representative));
            if (noBaseTexture.Count > 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                    $"{noBaseTexture.Count} Material(s) have no supported base texture property and will use a white atlas tile: {FormatExamples(noBaseTexture)}",
                    representative));
            if (incompatibleReasons.Count > 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                    $"Representative Material settings will replace incompatible source settings. Examples: {FormatExamples(incompatibleReasons)}",
                    representative));
            if (nonAtlasedProperties.Count > 0)
                issues.Add(new ValidationIssue(ValidationSeverity.Warning,
                    $"These populated texture properties are not supported by the selected adapter and use the representative Material value: {FormatExamples(nonAtlasedProperties)}",
                    representative));
        }

        private static string FormatExamples(IEnumerable<string> values)
        {
            var examples = new List<string>();
            foreach (var value in values)
            {
                examples.Add(value);
                if (examples.Count == 5) break;
            }
            return string.Join(", ", examples);
        }
    }
}
