using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace AvatarMeshMaterialOptimizer
{
    internal static class UVRemapper
    {
        public static Vector2 Remap(Vector2 uv, Material material, Rect atlasRect)
        {
            var scale = MaterialTextureUtility.GetTextureScale(material);
            var offset = MaterialTextureUtility.GetTextureOffset(material);
            var transformed = Vector2.Scale(uv, scale) + offset;
            return new Vector2(atlasRect.xMin + transformed.x * atlasRect.width,
                atlasRect.yMin + transformed.y * atlasRect.height);
        }
    }

    internal static class SubMeshMerger
    {
        internal readonly struct Key : IEquatable<Key>
        {
            public readonly int MaterialGroup;
            public readonly MeshTopology Topology;
            public Key(int materialGroup, MeshTopology topology) { MaterialGroup = materialGroup; Topology = topology; }
            public bool Equals(Key other) => MaterialGroup == other.MaterialGroup && Topology == other.Topology;
            public override bool Equals(object obj) => obj is Key other && Equals(other);
            public override int GetHashCode() => (MaterialGroup * 397) ^ (int)Topology;
        }
    }

    internal static class MeshMerger
    {
        private readonly struct VertexKey : IEquatable<VertexKey>
        {
            private readonly int _vertex;
            private readonly int _materialId;
            public VertexKey(int vertex, int materialId) { _vertex = vertex; _materialId = materialId; }
            public bool Equals(VertexKey other) => _vertex == other._vertex && _materialId == other._materialId;
            public override bool Equals(object obj) => obj is VertexKey other && Equals(other);
            public override int GetHashCode() => (_vertex * 397) ^ _materialId;
        }

        private sealed class SourceData : IDisposable
        {
            public RendererEntry Entry;
            public Vector3[] Vertices;
            public Vector3[] Normals;
            public Vector4[] Tangents;
            public Color[] Colors;
            public Vector2[] Uv0;
            public Vector2[] Uv1;
            public Vector2[] Uv2;
            public Vector2[] Uv3;
            public Matrix4x4 PositionMatrix;
            public Matrix4x4 NormalMatrix;
            public int[] BoneMap;
            public NativeArray<byte> BonesPerVertex;
            public NativeArray<BoneWeight1> BoneWeights;
            public int[] WeightOffsets;
            public int StaticBone = -1;
            public readonly Dictionary<VertexKey, int> VertexMap = new Dictionary<VertexKey, int>();
            public readonly Dictionary<int, List<int>> OutputBySource = new Dictionary<int, List<int>>();

            public void Dispose()
            {
                if (BonesPerVertex.IsCreated) BonesPerVertex.Dispose();
                if (BoneWeights.IsCreated) BoneWeights.Dispose();
            }
        }

        public static MeshMergeResult Merge(IList<RendererEntry> entries, IList<MaterialGroup> groups,
            Transform outputTransform, GameObject targetRoot, Transform rootBone,
            BlendShapeCollisionMode collisionMode, bool preserveMaterialUvs = false)
        {
            var result = new MeshMergeResult();
            var materialGroups = new Dictionary<Material, MaterialGroup>();
            foreach (var group in groups)
                foreach (var material in group.Materials) materialGroups[material] = group;

            var vertices = new List<Vector3>(); var normals = new List<Vector3>();
            var tangents = new List<Vector4>(); var colors = new List<Color>();
            var uv0 = new List<Vector2>(); var uv1 = new List<Vector2>();
            var uv2 = new List<Vector2>(); var uv3 = new List<Vector2>();
            var bonesPerVertex = new List<byte>(); var weights = new List<BoneWeight1>();
            var submeshOrder = new List<SubMeshMerger.Key>();
            var indices = new Dictionary<SubMeshMerger.Key, List<int>>();
            var boneRemapper = new BoneRemapper();
            var blendSources = new List<BlendShapeSource>();
            var outputNameCounts = new Dictionary<string, int>();
            var sources = new List<SourceData>();

            try
            {
                foreach (var entry in entries)
                {
                    if (!entry.Included || entry.Mesh == null || entry.Renderer == null) continue;
                    var data = CreateSourceData(entry, entries, targetRoot.transform, outputTransform,
                        boneRemapper);
                    sources.Add(data);
                    var mesh = entry.Mesh;
                    for (var sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var material = entry.Materials[sub];
                        var group = materialGroups[material];
                        var topology = mesh.GetTopology(sub);
                        var submeshKey = new SubMeshMerger.Key(group.Index, topology);
                        if (!indices.TryGetValue(submeshKey, out var outputIndices))
                        {
                            outputIndices = new List<int>();
                            indices.Add(submeshKey, outputIndices);
                            submeshOrder.Add(submeshKey);
                        }
                        foreach (var sourceIndex in mesh.GetIndices(sub))
                        {
                            var key = new VertexKey(sourceIndex, material.GetInstanceID());
                            if (!data.VertexMap.TryGetValue(key, out var outputIndex))
                            {
                                outputIndex = CopyVertex(data, sourceIndex, material, group.AtlasRects[material],
                                    vertices, normals, tangents, colors, uv0, uv1, uv2, uv3,
                                    bonesPerVertex, weights, preserveMaterialUvs);
                                data.VertexMap.Add(key, outputIndex);
                                if (!data.OutputBySource.TryGetValue(sourceIndex, out var copies))
                                {
                                    copies = new List<int>();
                                    data.OutputBySource.Add(sourceIndex, copies);
                                }
                                copies.Add(outputIndex);
                            }
                            outputIndices.Add(outputIndex);
                        }
                    }
                    AddBlendShapeSources(data, collisionMode, blendSources, outputNameCounts);
                }

                var meshOut = new Mesh
                {
                    name = targetRoot.name + "_Merged",
                    indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
                };
                meshOut.SetVertices(vertices); meshOut.SetNormals(normals); meshOut.SetTangents(tangents);
                meshOut.SetColors(colors); meshOut.SetUVs(0, uv0); meshOut.SetUVs(1, uv1);
                meshOut.SetUVs(2, uv2); meshOut.SetUVs(3, uv3);
                meshOut.bindposes = boneRemapper.CreateBindposes(outputTransform);
                using (var bpv = new NativeArray<byte>(bonesPerVertex.ToArray(), Allocator.Temp))
                using (var bw = new NativeArray<BoneWeight1>(weights.ToArray(), Allocator.Temp))
                    meshOut.SetBoneWeights(bpv, bw);
                meshOut.subMeshCount = submeshOrder.Count;
                for (var i = 0; i < submeshOrder.Count; i++)
                {
                    var key = submeshOrder[i];
                    meshOut.SetIndices(indices[key], key.Topology, i, false);
                    result.Materials.Add(groups[key.MaterialGroup].MergedMaterial);
                }
                BlendShapeMerger.AddBlendShapes(meshOut, blendSources, result.BlendShapeWeights);
                meshOut.RecalculateBounds();
                result.Mesh = meshOut;
                result.Bones = new List<Transform>(boneRemapper.Bones).ToArray();
                result.RootBone = rootBone != null ? rootBone : targetRoot.transform;
                result.LocalBounds = CalculateBounds(entries, outputTransform, result.RootBone, meshOut.bounds);
                return result;
            }
            finally
            {
                foreach (var source in sources) source.Dispose();
            }
        }

        private static SourceData CreateSourceData(RendererEntry entry, IEnumerable<RendererEntry> entries,
            Transform targetRoot, Transform output, BoneRemapper remapper)
        {
            var mesh = entry.Mesh;
            var positionMatrix = output.worldToLocalMatrix * entry.Renderer.transform.localToWorldMatrix;
            var data = new SourceData
            {
                Entry = entry, Vertices = mesh.vertices, Normals = mesh.normals, Tangents = mesh.tangents,
                Colors = mesh.colors, Uv0 = mesh.uv, Uv1 = mesh.uv2, Uv2 = mesh.uv3, Uv3 = mesh.uv4,
                PositionMatrix = positionMatrix, NormalMatrix = positionMatrix.inverse.transpose
            };
            if (entry.Renderer is SkinnedMeshRenderer skinned)
            {
                data.BoneMap = remapper.BuildMap(skinned, mesh, output);
                data.BonesPerVertex = mesh.GetBonesPerVertex();
                data.BoneWeights = mesh.GetAllBoneWeights();
                data.WeightOffsets = new int[mesh.vertexCount];
                var offset = 0;
                for (var i = 0; i < mesh.vertexCount; i++)
                {
                    data.WeightOffsets[i] = offset;
                    offset += data.BonesPerVertex[i];
                }
            }
            else
            {
                var anchor = StaticMeshBoneResolver.Resolve(entry, entries, targetRoot);
                data.StaticBone = remapper.GetOrAdd(anchor,
                    anchor.worldToLocalMatrix * output.localToWorldMatrix);
            }
            return data;
        }

        private static int CopyVertex(SourceData d, int source, Material material, Rect rect,
            List<Vector3> vertices, List<Vector3> normals, List<Vector4> tangents, List<Color> colors,
            List<Vector2> uv0, List<Vector2> uv1, List<Vector2> uv2, List<Vector2> uv3,
            List<byte> bonesPerVertex, List<BoneWeight1> outputWeights, bool preserveMaterialUvs)
        {
            var index = vertices.Count;
            vertices.Add(d.PositionMatrix.MultiplyPoint3x4(d.Vertices[source]));
            normals.Add(d.Normals.Length == d.Vertices.Length ? d.NormalMatrix.MultiplyVector(d.Normals[source]).normalized : Vector3.up);
            if (d.Tangents.Length == d.Vertices.Length)
            {
                var t = d.NormalMatrix.MultiplyVector(new Vector3(d.Tangents[source].x, d.Tangents[source].y, d.Tangents[source].z)).normalized;
                var sign = d.PositionMatrix.determinant < 0f ? -d.Tangents[source].w : d.Tangents[source].w;
                tangents.Add(new Vector4(t.x, t.y, t.z, sign));
            }
            else tangents.Add(new Vector4(1f, 0f, 0f, 1f));
            colors.Add(d.Colors.Length == d.Vertices.Length ? d.Colors[source] : Color.white);
            var sourceUv = d.Uv0.Length == d.Vertices.Length ? d.Uv0[source] : Vector2.zero;
            uv0.Add(preserveMaterialUvs ? sourceUv : UVRemapper.Remap(sourceUv, material, rect));
            uv1.Add(d.Uv1.Length == d.Vertices.Length ? d.Uv1[source] : Vector2.zero);
            uv2.Add(d.Uv2.Length == d.Vertices.Length ? d.Uv2[source] : Vector2.zero);
            uv3.Add(d.Uv3.Length == d.Vertices.Length ? d.Uv3[source] : Vector2.zero);

            if (d.StaticBone >= 0)
            {
                bonesPerVertex.Add(1);
                outputWeights.Add(new BoneWeight1 { boneIndex = d.StaticBone, weight = 1f });
            }
            else
            {
                var count = d.BonesPerVertex[source];
                bonesPerVertex.Add(count == 0 ? (byte)1 : count);
                if (count == 0)
                    outputWeights.Add(new BoneWeight1 { boneIndex = d.BoneMap.Length > 0 ? d.BoneMap[0] : 0, weight = 1f });
                else
                {
                    var offset = d.WeightOffsets[source];
                    for (var i = 0; i < count; i++)
                    {
                        var weight = d.BoneWeights[offset + i];
                        weight.boneIndex = weight.boneIndex >= 0 && weight.boneIndex < d.BoneMap.Length
                            ? d.BoneMap[weight.boneIndex] : d.BoneMap[0];
                        outputWeights.Add(weight);
                    }
                }
            }
            return index;
        }

        private static void AddBlendShapeSources(SourceData data, BlendShapeCollisionMode mode,
            List<BlendShapeSource> output, Dictionary<string, int> outputNameCounts)
        {
            var mesh = data.Entry.Mesh;
            var skinned = data.Entry.Renderer as SkinnedMeshRenderer;
            for (var i = 0; i < mesh.blendShapeCount; i++)
            {
                var original = mesh.GetBlendShapeName(i);
                string name;
                if (mode == BlendShapeCollisionMode.MergeSameName)
                    name = original;
                else
                {
                    var desired = mode == BlendShapeCollisionMode.PrefixByRendererName
                        ? data.Entry.Renderer.name + "_" + original : original;
                    outputNameCounts.TryGetValue(desired, out var count);
                    count++;
                    outputNameCounts[desired] = count;
                    name = count == 1 ? desired : desired + "_" + count;
                }
                output.Add(new BlendShapeSource
                {
                    Entry = data.Entry, ShapeIndex = i, OutputName = name,
                    OutputVerticesBySourceVertex = data.OutputBySource,
                    PositionMatrix = data.PositionMatrix, NormalMatrix = data.NormalMatrix,
                    CurrentWeight = skinned != null ? skinned.GetBlendShapeWeight(i) : 0f
                });
            }
        }

        private static Bounds CalculateBounds(IList<RendererEntry> entries, Transform output,
            Transform mergedRootBone, Bounds meshBounds)
        {
            var initialized = false;
            var bounds = new Bounds();
            var boundsSpace = mergedRootBone != null ? mergedRootBone : output;

            // Mesh.bounds is stored in the generated mesh/output Transform space,
            // while SkinnedMeshRenderer.localBounds is interpreted in the actual
            // root-bone space. Always include the generated geometry after converting
            // it into the merged renderer's root-bone space.
            var meshToBounds = boundsSpace.worldToLocalMatrix * output.localToWorldMatrix;
            EncapsulateTransformedBounds(meshBounds, meshToBounds, ref bounds, ref initialized);

            foreach (var entry in entries)
            {
                if (!entry.Included || entry.Renderer == null || entry.Mesh == null) continue;
                Bounds sourceBounds;
                Transform sourceBoundsSpace;
                if (entry.Renderer is SkinnedMeshRenderer skinned)
                {
                    sourceBounds = skinned.localBounds;
                    sourceBoundsSpace = skinned.rootBone != null ? skinned.rootBone : skinned.transform;
                }
                else
                {
                    sourceBounds = entry.Mesh.bounds;
                    sourceBoundsSpace = entry.Renderer.transform;
                }

                var sourceToMergedBounds = boundsSpace.worldToLocalMatrix *
                                           sourceBoundsSpace.localToWorldMatrix;
                EncapsulateTransformedBounds(sourceBounds, sourceToMergedBounds,
                    ref bounds, ref initialized);
            }
            return initialized ? bounds : meshBounds;
        }

        private static void EncapsulateTransformedBounds(Bounds source, Matrix4x4 matrix,
            ref Bounds destination, ref bool initialized)
        {
            var min = source.min;
            var max = source.max;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var point = matrix.MultiplyPoint3x4(new Vector3(
                    x == 0 ? min.x : max.x,
                    y == 0 ? min.y : max.y,
                    z == 0 ? min.z : max.z));
                if (!initialized)
                {
                    destination = new Bounds(point, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    destination.Encapsulate(point);
                }
            }
        }
    }
}
