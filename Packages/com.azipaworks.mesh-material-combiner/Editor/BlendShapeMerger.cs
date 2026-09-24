using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal sealed class BlendShapeSource
    {
        public RendererEntry Entry;
        public int ShapeIndex;
        public string OutputName;
        public Dictionary<int, List<int>> OutputVerticesBySourceVertex;
        public Matrix4x4 PositionMatrix;
        public Matrix4x4 NormalMatrix;
        public float CurrentWeight;
    }

    internal static class BlendShapeMerger
    {
        public static void AddBlendShapes(Mesh output, IList<BlendShapeSource> sources,
            Dictionary<string, float> outputWeights)
        {
            var groups = new Dictionary<string, List<BlendShapeSource>>();
            foreach (var source in sources)
            {
                if (!groups.TryGetValue(source.OutputName, out var list))
                {
                    list = new List<BlendShapeSource>();
                    groups.Add(source.OutputName, list);
                }
                list.Add(source);
                if (!outputWeights.TryGetValue(source.OutputName, out var old) || source.CurrentWeight > old)
                    outputWeights[source.OutputName] = source.CurrentWeight;
            }

            foreach (var pair in groups)
            {
                var weights = new SortedSet<float>();
                foreach (var source in pair.Value)
                {
                    var mesh = source.Entry.Mesh;
                    for (var f = 0; f < mesh.GetBlendShapeFrameCount(source.ShapeIndex); f++)
                        weights.Add(mesh.GetBlendShapeFrameWeight(source.ShapeIndex, f));
                }
                foreach (var weight in weights)
                {
                    var dv = new Vector3[output.vertexCount];
                    var dn = new Vector3[output.vertexCount];
                    var dt = new Vector3[output.vertexCount];
                    foreach (var source in pair.Value) AddSample(source, weight, dv, dn, dt);
                    output.AddBlendShapeFrame(pair.Key, weight, dv, dn, dt);
                }
            }
        }

        private static void AddSample(BlendShapeSource source, float targetWeight, Vector3[] outDv,
            Vector3[] outDn, Vector3[] outDt)
        {
            var mesh = source.Entry.Mesh;
            var frameCount = mesh.GetBlendShapeFrameCount(source.ShapeIndex);
            if (frameCount == 0) return;
            var lower = 0;
            var upper = frameCount - 1;
            for (var i = 0; i < frameCount; i++)
            {
                var w = mesh.GetBlendShapeFrameWeight(source.ShapeIndex, i);
                if (w <= targetWeight) lower = i;
                if (w >= targetWeight) { upper = i; break; }
            }
            var lowWeight = mesh.GetBlendShapeFrameWeight(source.ShapeIndex, lower);
            var highWeight = mesh.GetBlendShapeFrameWeight(source.ShapeIndex, upper);
            var t = Mathf.Approximately(lowWeight, highWeight) ? 0f : Mathf.InverseLerp(lowWeight, highWeight, targetWeight);
            var count = mesh.vertexCount;
            var ldv = new Vector3[count]; var ldn = new Vector3[count]; var ldt = new Vector3[count];
            var hdv = new Vector3[count]; var hdn = new Vector3[count]; var hdt = new Vector3[count];
            mesh.GetBlendShapeFrameVertices(source.ShapeIndex, lower, ldv, ldn, ldt);
            if (upper != lower) mesh.GetBlendShapeFrameVertices(source.ShapeIndex, upper, hdv, hdn, hdt);
            else { hdv = ldv; hdn = ldn; hdt = ldt; }

            foreach (var mapping in source.OutputVerticesBySourceVertex)
            {
                var sourceIndex = mapping.Key;
                var dv = source.PositionMatrix.MultiplyVector(Vector3.LerpUnclamped(ldv[sourceIndex], hdv[sourceIndex], t));
                var dn = source.NormalMatrix.MultiplyVector(Vector3.LerpUnclamped(ldn[sourceIndex], hdn[sourceIndex], t));
                var dt = source.NormalMatrix.MultiplyVector(Vector3.LerpUnclamped(ldt[sourceIndex], hdt[sourceIndex], t));
                foreach (var outputIndex in mapping.Value)
                {
                    outDv[outputIndex] += dv;
                    outDn[outputIndex] += dn;
                    outDt[outputIndex] += dt;
                }
            }
        }
    }
}
