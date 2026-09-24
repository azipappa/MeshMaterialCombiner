using System;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal static class TextureAtlasBuilder
    {
        private const int AtlasPadding = 4;
        private static Material _copyMaterial;

        public static Texture2D BuildTextureAtlas(MaterialGroup group,
            TexturePropertyInfo property, int size, bool packNormalForRuntime = false)
        {
            if (group == null || group.Materials.Count == 0) throw new ArgumentException("Material group is empty.");
            if (property == null) throw new ArgumentNullException(nameof(property));
            var columns = Mathf.CeilToInt(Mathf.Sqrt(group.Materials.Count));
            var rows = Mathf.CeilToInt(group.Materials.Count / (float)columns);
            var tileWidth = size / columns;
            var tileHeight = size / rows;
            var atlas = new Texture2D(size, size, TextureFormat.RGBA32, false, property.IsLinear)
            {
                name = $"MaterialGroup_{group.Index}_{property.PropertyName}"
            };
            var clear = new Color32[size * size];
            atlas.SetPixels32(clear);

            for (var i = 0; i < group.Materials.Count; i++)
            {
                var material = group.Materials[i];
                var column = i % columns;
                var row = i / columns;
                var x = column * tileWidth;
                var y = row * tileHeight;
                var width = column == columns - 1 ? size - x : tileWidth;
                var height = row == rows - 1 ? size - y : tileHeight;
                var padding = Mathf.Min(AtlasPadding, Mathf.Max(0, Mathf.Min(width, height) / 8));
                var innerWidth = Mathf.Max(1, width - padding * 2);
                var innerHeight = Mathf.Max(1, height - padding * 2);
                var innerX = x + padding;
                var innerY = y + padding;
                var rect = new Rect(innerX / (float)size, innerY / (float)size,
                    innerWidth / (float)size, innerHeight / (float)size);
                group.AtlasRects[material] = rect;

                var sourceProperty = property.IsBaseTexture
                    ? MaterialTextureUtility.FindBaseTextureProperty(material)
                    : material.HasProperty(property.PropertyName) ? property.PropertyName : null;
                var texture = sourceProperty != null ? material.GetTexture(sourceProperty) : null;
                var sourceScale = sourceProperty != null
                    ? material.GetTextureScale(sourceProperty) : Vector2.one;
                var sourceOffset = sourceProperty != null
                    ? material.GetTextureOffset(sourceProperty) : Vector2.zero;
                var baseScale = MaterialTextureUtility.GetTextureScale(material);
                var baseOffset = MaterialTextureUtility.GetTextureOffset(material);
                var relativeScale = new Vector2(
                    Mathf.Abs(baseScale.x) > 0.000001f ? sourceScale.x / baseScale.x : sourceScale.x,
                    Mathf.Abs(baseScale.y) > 0.000001f ? sourceScale.y / baseScale.y : sourceScale.y);
                var relativeOffset = new Vector2(
                    sourceOffset.x - baseOffset.x * relativeScale.x,
                    sourceOffset.y - baseOffset.y * relativeScale.y);

                if (texture == null && property.IsNormalMap) texture = Texture2D.normalTexture;
                var pixels = texture != null
                    ? ReadScaled(texture, innerWidth, innerHeight, property.IsLinear,
                        property.IsNormalMap, packNormalForRuntime, relativeScale, relativeOffset)
                    : CreateSolidPixels(innerWidth, innerHeight, property.DefaultColor);
                if (property.IsBaseTexture)
                {
                    var tint = MaterialTextureUtility.GetBaseColor(material);
                    for (var p = 0; p < pixels.Length; p++) pixels[p] *= tint;
                }
                SetTileWithPadding(atlas, x, y, width, height, padding,
                    innerWidth, innerHeight, pixels);
            }
            atlas.Apply(false, false);
            if (property.IsBaseTexture) group.MainAtlas = atlas;
            return atlas;
        }

        private static Color[] ReadScaled(Texture texture, int width, int height, bool linear,
            bool normalMap, bool packNormalForRuntime, Vector2 scale, Vector2 offset)
        {
            var previous = RenderTexture.active;
            var temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
            try
            {
                var copyMaterial = GetCopyMaterial();
                if (copyMaterial != null)
                {
                    copyMaterial.SetVector("_ScaleOffset", new Vector4(scale.x, scale.y, offset.x, offset.y));
                    copyMaterial.SetFloat("_NormalMap", normalMap ? 1f : 0f);
                    copyMaterial.SetFloat("_PackNormalRuntime", packNormalForRuntime ? 1f : 0f);
                    Graphics.Blit(texture, temporary, copyMaterial);
                }
                else
                {
                    Graphics.Blit(texture, temporary, scale, offset);
                }
                RenderTexture.active = temporary;
                var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, linear);
                try
                {
                    readable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    readable.Apply(false, false);
                    return readable.GetPixels();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(readable);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Material GetCopyMaterial()
        {
            if (_copyMaterial != null) return _copyMaterial;
            var shader = Shader.Find("Hidden/AzipaWorks/MeshMaterialCombiner/AtlasCopy");
            if (shader == null) return null;
            _copyMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return _copyMaterial;
        }

        private static Color[] CreateSolidPixels(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            return pixels;
        }

        private static void SetTileWithPadding(Texture2D atlas, int x, int y, int width, int height,
            int padding, int innerWidth, int innerHeight, Color[] innerPixels)
        {
            var tilePixels = new Color[width * height];
            for (var py = 0; py < height; py++)
            {
                var sourceY = Mathf.Clamp(py - padding, 0, innerHeight - 1);
                for (var px = 0; px < width; px++)
                {
                    var sourceX = Mathf.Clamp(px - padding, 0, innerWidth - 1);
                    tilePixels[py * width + px] = innerPixels[sourceY * innerWidth + sourceX];
                }
            }
            atlas.SetPixels(x, y, width, height, tilePixels);
        }
    }
}
