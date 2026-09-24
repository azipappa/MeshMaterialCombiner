using System.Collections.Generic;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal sealed class BoneRemapper
    {
        private readonly List<Transform> _bones = new List<Transform>();
        private readonly List<Matrix4x4> _bindposes = new List<Matrix4x4>();
        private readonly Dictionary<Transform, int> _indices = new Dictionary<Transform, int>();

        public IReadOnlyList<Transform> Bones => _bones;

        public int GetOrAdd(Transform bone, Matrix4x4 bindpose)
        {
            if (bone == null) return -1;
            if (_indices.TryGetValue(bone, out var index)) return index;
            index = _bones.Count;
            _bones.Add(bone);
            _bindposes.Add(bindpose);
            _indices.Add(bone, index);
            return index;
        }

        public int[] BuildMap(SkinnedMeshRenderer renderer, Mesh mesh, Transform outputTransform)
        {
            var sourceBones = renderer.bones;
            var count = Mathf.Max(sourceBones.Length, mesh.bindposes.Length);
            if (count == 0)
            {
                var fallback = renderer.rootBone != null ? renderer.rootBone : renderer.transform;
                return new[] { GetOrAdd(fallback, fallback.worldToLocalMatrix * outputTransform.localToWorldMatrix) };
            }
            var result = new int[count];
            for (var i = 0; i < count; i++)
            {
                var bone = i < sourceBones.Length ? sourceBones[i] : null;
                if (bone == null) bone = renderer.rootBone != null ? renderer.rootBone : renderer.transform;
                var bindpose = i < mesh.bindposes.Length
                    ? mesh.bindposes[i] * renderer.transform.worldToLocalMatrix * outputTransform.localToWorldMatrix
                    : bone.worldToLocalMatrix * outputTransform.localToWorldMatrix;
                result[i] = GetOrAdd(bone, bindpose);
            }
            return result;
        }

        public Matrix4x4[] CreateBindposes(Transform outputTransform)
        {
            return _bindposes.ToArray();
        }
    }
}
