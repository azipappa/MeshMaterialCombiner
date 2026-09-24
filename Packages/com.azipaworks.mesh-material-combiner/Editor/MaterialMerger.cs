using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class MaterialAtlasPlanner
    {
        public static System.Collections.Generic.List<TexturePropertyInfo> GetProperties(MaterialGroup group)
        {
            var result = new System.Collections.Generic.List<TexturePropertyInfo>();
            if (group == null || group.Adapter == null || group.Materials.Count == 0) return result;
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var property in group.Adapter.GetTextureProperties(group.Materials[0]))
            {
                if (property == null || string.IsNullOrEmpty(property.PropertyName) ||
                    !seen.Add(property.PropertyName)) continue;
                var used = property.IsBaseTexture;
                if (!used)
                    foreach (var material in group.Materials)
                        if (material != null && material.HasProperty(property.PropertyName) &&
                            material.GetTexture(property.PropertyName) != null)
                        {
                            used = true;
                            break;
                        }
                if (used) result.Add(property);
            }
            return result;
        }

        public static string GetFileSuffix(TexturePropertyInfo property)
        {
            return property.PropertyName.TrimStart('_');
        }
    }

    internal static class MaterialMerger
    {
        public static Material CreateMergedMaterial(MaterialGroup group,
            System.Collections.Generic.IEnumerable<TexturePropertyInfo> properties)
        {
            if (group == null || group.Adapter == null || group.Materials.Count == 0) return null;
            return group.Adapter.CreateMergedMaterial(group.Materials[0], group.Atlases, properties);
        }
    }
}
