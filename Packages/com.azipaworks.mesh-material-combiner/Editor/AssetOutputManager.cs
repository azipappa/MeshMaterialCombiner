using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal sealed class AssetOutputManager
    {
        private readonly List<string> _createdAssets = new List<string>();
        public IReadOnlyList<string> CreatedAssets => _createdAssets;

        public OutputPaths PrepareFolders(OptimizerSettings settings)
        {
            var avatarRoot = AvatarRootResolver.Resolve(settings.TargetRoot);
            var avatar = Sanitize(avatarRoot != null
                ? avatarRoot.name
                : (settings.TargetRoot != null ? settings.TargetRoot.name : "Avatar"));
            var buildName = string.IsNullOrWhiteSpace(settings.MergedObjectName)
                ? settings.GroupName
                : settings.MergedObjectName;
            var baseBuildName = Sanitize(string.IsNullOrWhiteSpace(buildName) ? avatar + "_Merged" : buildName);
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var group = baseBuildName + "_" + timestamp;
            var root = NormalizeAssetPath(OptimizerSettings.DefaultOutputFolder).TrimEnd('/') + "/" + avatar + "/" + group;
            return new OutputPaths
            {
                Root = EnsureFolder(root),
                Meshes = EnsureFolder(root + "/Meshes"),
                Materials = EnsureFolder(root + "/Materials"),
                Textures = EnsureFolder(root + "/Textures"),
                Prefabs = EnsureFolder(root + "/Prefabs")
            };
        }

        public Texture2D SaveTexture(Texture2D texture, string folder, string name,
            TexturePropertyInfo property = null)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Sanitize(name) + ".png");
            var relative = path.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            var absolute = Path.GetFullPath(Path.Combine(Application.dataPath, relative));
            var assetsRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
            if (!absolute.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Texture output path is outside Assets.");
            File.WriteAllBytes(absolute, texture.EncodeToPNG());
            _createdAssets.Add(path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = Mathf.Max(texture.width, texture.height);
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = property == null || !property.IsLinear;
                importer.textureType = property != null && property.IsNormalMap
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        public Mesh SaveMesh(Mesh mesh, string folder, string name)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Sanitize(name) + ".asset");
            AssetDatabase.CreateAsset(mesh, path);
            _createdAssets.Add(path);
            Undo.RegisterCreatedObjectUndo(mesh, "Create merged mesh asset");
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        public Material SaveMaterial(Material material, string folder, string name)
        {
            var path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Sanitize(name) + ".mat");
            AssetDatabase.CreateAsset(material, path);
            _createdAssets.Add(path);
            Undo.RegisterCreatedObjectUndo(material, "Create merged material asset");
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }

        public string SavePrefab(OptimizerSettings settings, IList<RendererEntry> selected,
            MeshMergeResult result, string folder, Transform placementSource,
            Transform sceneOutputContainer, string mergedObjectName = null)
        {
            var targetRoot = AvatarRootResolver.Resolve(settings.TargetRoot);
            var groupName = string.IsNullOrWhiteSpace(settings.MergedObjectName)
                ? settings.GroupName + "_Merged"
                : settings.MergedObjectName;
            // Do not serialize a full avatar clone.  The generated Prefab is a
            // portable optimized-mesh artifact, so it only needs the bone paths
            // referenced by the merged renderer and the merged renderer itself.
            // This deliberately excludes PhysBones, MA components, descriptors,
            // Animators and unrelated source renderers from Prefab serialization.
            var clone = BuildMinimalPrefabRoot(targetRoot, result, groupName);
            try
            {
                var containerTransform = clone.transform.Find(sceneOutputContainer.name);
                GameObject container;
                if (containerTransform != null)
                {
                    container = containerTransform.gameObject;
                }
                else
                {
                    container = new GameObject(sceneOutputContainer.name);
                    container.transform.SetParent(clone.transform, false);
                }
                container.tag = "Untagged";
                container.SetActive(true);
                var mergedName = Sanitize(string.IsNullOrWhiteSpace(mergedObjectName)
                    ? groupName
                    : mergedObjectName);
                var previousMerged = container.transform.Find(mergedName);
                if (previousMerged != null)
                    UnityEngine.Object.DestroyImmediate(previousMerged.gameObject);
                var mergedObject = new GameObject(mergedName);
                mergedObject.tag = "Untagged";
                mergedObject.SetActive(true);
                mergedObject.transform.SetParent(container.transform, false);
                var rendererOut = mergedObject.AddComponent<SkinnedMeshRenderer>();
                rendererOut.sharedMesh = result.Mesh;
                rendererOut.sharedMaterials = result.Materials.ToArray();
                rendererOut.bones = MapBones(targetRoot.transform, clone.transform, result.Bones);
                rendererOut.rootBone = MapBone(targetRoot.transform, clone.transform, result.RootBone);
                rendererOut.localBounds = result.LocalBounds;
                ApplyBlendShapeWeights(rendererOut, result);

                var pathOut = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + Sanitize(groupName) + "_Optimized.prefab");
                var prefab = PrefabUtility.SaveAsPrefabAsset(clone, pathOut, out var saveSucceeded);
                var savedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(pathOut);
                var savedMissing = savedRoot == null
                    ? null
                    : OptimizationValidator.FindMissingScriptObjects(savedRoot);
                if (!saveSucceeded || prefab == null || savedRoot == null ||
                    (savedMissing != null && savedMissing.Count > 0))
                {
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(pathOut) != null)
                        AssetDatabase.DeleteAsset(pathOut);
                    var missingText = savedMissing != null && savedMissing.Count > 0
                        ? $" ({savedMissing.Count} missing script object(s) were serialized)"
                        : string.Empty;
                    throw new InvalidOperationException(
                        $"Unity failed to save a valid optimized Prefab: {pathOut}{missingText}");
                }
                _createdAssets.Add(pathOut);
                return pathOut;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }
        }

        private static GameObject BuildMinimalPrefabRoot(GameObject targetRoot,
            MeshMergeResult result, string groupName)
        {
            if (targetRoot == null)
                throw new InvalidOperationException("Avatar root could not be resolved for Prefab output.");

            var root = new GameObject(Sanitize(groupName) + "_Optimized");
            CopyTransform(targetRoot.transform, root.transform);

            // EnsureTransformPath creates only the ancestry needed by the merged
            // renderer. Transforms are copied without any Components or scripts.
            if (result != null && result.Bones != null)
            {
                foreach (var bone in result.Bones)
                    EnsureTransformPath(targetRoot.transform, root.transform, bone);
            }
            EnsureTransformPath(targetRoot.transform, root.transform, result != null ? result.RootBone : null);
            return root;
        }

        private static Transform EnsureTransformPath(Transform sourceRoot, Transform cloneRoot,
            Transform source)
        {
            if (source == null || source == sourceRoot || !source.IsChildOf(sourceRoot))
                return cloneRoot;

            var chain = new List<Transform>();
            var current = source;
            while (current != null && current != sourceRoot)
            {
                chain.Add(current);
                current = current.parent;
            }
            var parent = cloneRoot;
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                var sourceTransform = chain[i];
                var child = parent.Find(sourceTransform.name);
                if (child == null)
                {
                    var childObject = new GameObject(sourceTransform.name);
                    child = childObject.transform;
                    child.SetParent(parent, false);
                    CopyTransform(sourceTransform, child);
                }
                parent = child;
            }
            return parent;
        }

        private static void CopyTransform(Transform source, Transform destination)
        {
            if (source == null || destination == null) return;
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }

        public void Rollback()
        {
            for (var i = _createdAssets.Count - 1; i >= 0; i--)
                if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(_createdAssets[i])))
                    AssetDatabase.DeleteAsset(_createdAssets[i]);
            MMCBuildDatabaseService.CleanupEmptyBuildOutputFolders();
            _createdAssets.Clear();
        }

        public static void ApplyBlendShapeWeights(SkinnedMeshRenderer renderer, MeshMergeResult result)
        {
            if (renderer == null || renderer.sharedMesh == null) return;
            foreach (var pair in result.BlendShapeWeights)
            {
                var index = renderer.sharedMesh.GetBlendShapeIndex(pair.Key);
                if (index >= 0) renderer.SetBlendShapeWeight(index, pair.Value);
            }
        }

        private static Transform[] MapBones(Transform sourceRoot, Transform cloneRoot, Transform[] bones)
        {
            var result = new Transform[bones.Length];
            for (var i = 0; i < bones.Length; i++) result[i] = MapBone(sourceRoot, cloneRoot, bones[i]);
            return result;
        }

        private static Transform MapBone(Transform sourceRoot, Transform cloneRoot, Transform bone)
        {
            if (bone == null || (bone != sourceRoot && !bone.IsChildOf(sourceRoot))) return cloneRoot;
            var path = AnimationUtility.CalculateTransformPath(bone, sourceRoot);
            return string.IsNullOrEmpty(path) ? cloneRoot : cloneRoot.Find(path) ?? cloneRoot;
        }

        private static Transform MoveCloneToCombinedContainer(Transform source)
        {
            if (source == null || source.parent == null) return null;
            if (source.parent.name == SourceObjectOrganizer.ContainerName) return source.parent;
            var container = source.parent.Find(SourceObjectOrganizer.ContainerName);
            if (container == null)
            {
                var objectContainer = new GameObject(SourceObjectOrganizer.ContainerName);
                objectContainer.tag = "Untagged";
                objectContainer.SetActive(true);
                objectContainer.transform.SetParent(source.parent, false);
                container = objectContainer.transform;
            }
            source.SetParent(container, true);
            return container;
        }

        private static string EnsureFolder(string path)
        {
            path = NormalizeAssetPath(path).TrimEnd('/');
            if (!path.StartsWith("Assets", StringComparison.Ordinal))
                throw new InvalidOperationException("Output folder must be under Assets.");
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
            return current;
        }

        private static string NormalizeAssetPath(string path) => (path ?? string.Empty).Replace('\\', '/');

        internal static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Avatar";
            foreach (var invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Trim();
        }
    }

    internal sealed class OutputPaths
    {
        public string Root;
        public string Meshes;
        public string Materials;
        public string Textures;
        public string Prefabs;
    }
}
