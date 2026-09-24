using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AvatarMeshMaterialOptimizer
{
    internal sealed class EmptyOutputContainerCandidate
    {
        public GameObject ExpectedParent;
        public GameObject Container;
        public string SceneName;
        public string HierarchyPath;
        public bool Selected;
    }

    internal static class EmptyOutputContainerCleanup
    {
        private const string ContainerName = "__MeshMaterialCombiner";

        public static IList<EmptyOutputContainerCandidate> FindAllLoadedScenes()
        {
            var result = new List<EmptyOutputContainerCandidate>();
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) ||
                    (prefabStage != null && prefabStage.scene == scene)) continue;

                var roots = scene.GetRootGameObjects();
                for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    var root = roots[rootIndex];
                    if (root == null) continue;
                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    for (var transformIndex = 0; transformIndex < transforms.Length; transformIndex++)
                    {
                        var transform = transforms[transformIndex];
                        var container = transform != null ? transform.gameObject : null;
                        var parent = transform != null && transform.parent != null
                            ? transform.parent.gameObject
                            : null;
                        if (!IsSafeEmptyContainer(container, parent)) continue;
                        result.Add(new EmptyOutputContainerCandidate
                        {
                            ExpectedParent = parent,
                            Container = container,
                            SceneName = string.IsNullOrEmpty(scene.name) ? "Untitled" : scene.name,
                            HierarchyPath = GetHierarchyPath(transform),
                            Selected = false
                        });
                    }
                }
            }
            return result;
        }

        public static bool IsSafeEmptyContainer(GameObject container, GameObject expectedParent = null)
        {
            if (container == null || container.name != ContainerName ||
                !container.scene.IsValid() || !container.scene.isLoaded ||
                container.transform.childCount != 0 || EditorUtility.IsPersistent(container)) return false;

            if (expectedParent != null && container.transform.parent != expectedParent.transform) return false;

            var components = container.GetComponents<Component>();
            if (components.Length != 1 || !(components[0] is Transform)) return false;

            if ((container.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild)) != 0)
                return false;

            return !PrefabUtility.IsPartOfPrefabInstance(container) &&
                   !PrefabUtility.IsPartOfPrefabAsset(container);
        }

        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null) return string.Empty;
            var names = new List<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Add(current.name);
            names.Reverse();
            return string.Join("/", names);
        }

        public static string GetSignature(IList<EmptyOutputContainerCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0) return string.Empty;
            var ids = new List<string>();
            for (var i = 0; i < candidates.Count; i++)
            {
                var container = candidates[i] != null ? candidates[i].Container : null;
                if (container == null) continue;
                var id = GlobalObjectId.GetGlobalObjectIdSlow(container).ToString();
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
            ids.Sort(System.StringComparer.Ordinal);
            var builder = new StringBuilder();
            for (var i = 0; i < ids.Count; i++) builder.Append(ids[i]).Append('|');
            return Hash128.Compute(builder.ToString()).ToString();
        }

        public static int DeleteSelected(IList<EmptyOutputContainerCandidate> candidates)
        {
            if (candidates == null) return 0;
            var removed = 0;
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                var container = candidate != null ? candidate.Container : null;
                var parent = candidate != null ? candidate.ExpectedParent : null;
                if (candidate == null || !candidate.Selected ||
                    !IsSafeEmptyContainer(container, parent)) continue;

                Undo.DestroyObjectImmediate(container);
                removed++;
            }
            return removed;
        }
    }

    internal sealed class EmptyOutputContainerCleanupWindow : EditorWindow
    {
        private readonly List<EmptyOutputContainerCandidate> _candidates =
            new List<EmptyOutputContainerCandidate>();
        private Vector2 _scroll;

        public static void Open(IList<EmptyOutputContainerCandidate> candidates)
        {
            var window = CreateInstance<EmptyOutputContainerCleanupWindow>();
            window.titleContent = new GUIContent(OptimizerLocalization.T("Empty Output Cleanup"));
            window.minSize = new Vector2(480f, 220f);
            if (candidates != null)
                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (candidate == null || candidate.Container == null) continue;
                    window._candidates.Add(new EmptyOutputContainerCandidate
                    {
                        ExpectedParent = candidate.ExpectedParent,
                        Container = candidate.Container,
                        SceneName = candidate.SceneName,
                        HierarchyPath = candidate.HierarchyPath,
                        Selected = true
                    });
                }
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(OptimizerLocalization.T("Empty Output Cleanup"),
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 });
            var helpStyle = new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 12,
                wordWrap = true,
                padding = new RectOffset(10, 10, 8, 8)
            };
            EditorGUILayout.LabelField(OptimizerLocalization.T("Empty Output Cleanup Help"),
                helpStyle, GUILayout.MinHeight(48f));

            RemoveInvalidCandidates();
            if (_candidates.Count == 0)
            {
                EditorGUILayout.HelpBox(OptimizerLocalization.T("No Empty Output Folders"),
                    MessageType.None);
                return;
            }

            EditorGUILayout.LabelField(
                OptimizerLocalization.T("Empty Output Folders Found") + ": " + _candidates.Count,
                EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var selectedCount = 0;
            for (var i = 0; i < _candidates.Count; i++)
            {
                var candidate = _candidates[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        candidate.Selected = EditorGUILayout.Toggle(candidate.Selected,
                            GUILayout.Width(18f));
                        EditorGUILayout.LabelField(
                            candidate.SceneName + " / " + candidate.HierarchyPath,
                            EditorStyles.boldLabel);
                        if (GUILayout.Button(OptimizerLocalization.T("Select"),
                                EditorStyles.miniButton, GUILayout.Width(64f)))
                        {
                            Selection.activeGameObject = candidate.Container;
                            EditorGUIUtility.PingObject(candidate.Container);
                        }
                    }
                    EditorGUILayout.ObjectField(candidate.Container, typeof(GameObject), true);
                }
                if (candidate.Selected) selectedCount++;
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(selectedCount == 0))
            {
                if (!GUILayout.Button(
                        OptimizerLocalization.T("Delete Selected Empty Folders") +
                        " (" + selectedCount + ")", GUILayout.Height(26f))) return;
            }

            if (!EditorUtility.DisplayDialog(
                    OptimizerLocalization.T("Delete Empty Folders Confirmation Title"),
                    OptimizerLocalization.T("Delete Empty Folders Confirmation") +
                    "\n\n" + selectedCount,
                    OptimizerLocalization.T("Delete"),
                    OptimizerLocalization.T("Cancel"))) return;

            var removed = EmptyOutputContainerCleanup.DeleteSelected(_candidates);
            RemoveInvalidCandidates();
            ShowNotification(new GUIContent(
                OptimizerLocalization.T("Empty Folders Deleted") + ": " + removed));
        }

        private void RemoveInvalidCandidates()
        {
            for (var i = _candidates.Count - 1; i >= 0; i--)
            {
                var candidate = _candidates[i];
                if (candidate != null && EmptyOutputContainerCleanup.IsSafeEmptyContainer(
                        candidate.Container, candidate.ExpectedParent)) continue;
                _candidates.RemoveAt(i);
            }
        }
    }
}
