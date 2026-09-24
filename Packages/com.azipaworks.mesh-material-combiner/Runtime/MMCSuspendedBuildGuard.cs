using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace AvatarMeshMaterialOptimizer
{
    // Legacy compatibility marker. It is no longer created or checked during builds,
    // but keeping the type prevents Missing Script components in older saved scenes.
    [ExecuteAlways]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class MMCSuspendedBuildGuard : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnEnable()
        {
            EditorApplication.delayCall -= RemoveLegacyMarker;
            EditorApplication.delayCall += RemoveLegacyMarker;
        }

        private void RemoveLegacyMarker()
        {
            if (this == null || EditorUtility.IsPersistent(this) ||
                !gameObject.scene.IsValid() || !gameObject.scene.isLoaded ||
                PrefabStageUtility.GetPrefabStage(gameObject) != null) return;
            Undo.DestroyObjectImmediate(this);
        }
#endif
    }
}
