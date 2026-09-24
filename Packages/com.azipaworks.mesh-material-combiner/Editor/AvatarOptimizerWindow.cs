using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AvatarMeshMaterialOptimizer
{
    internal sealed class AvatarOptimizerWindow : EditorWindow
    {
        private enum MainView { Merge, History }
        private enum HistoryFilter { All, Issues, Valid }

        private readonly OptimizerSettings _settings = new OptimizerSettings();
        private readonly List<RendererEntry> _entries = new List<RendererEntry>();
        private readonly List<GameObject> _directTargets = new List<GameObject>();
        private readonly Dictionary<Renderer, bool> _expanded = new Dictionary<Renderer, bool>();
        private readonly Dictionary<Transform, bool> _hierarchyExpanded = new Dictionary<Transform, bool>();
        private readonly Dictionary<string, bool> _managedBuildExpanded = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _managedAvatarGroupExpanded = new Dictionary<string, bool>();
        private readonly Dictionary<string, bool> _managedAvatarStatsExpanded = new Dictionary<string, bool>();
        private readonly Dictionary<string, MMCBuildResolvedState> _managedBuildCache =
            new Dictionary<string, MMCBuildResolvedState>();
        private readonly Dictionary<string, MMCRebuildEvaluation> _rebuildEvaluationCache =
            new Dictionary<string, MMCRebuildEvaluation>();
        private readonly Dictionary<MMCBuildRecord, MMCSourceOwnershipConflict>
            _sourceOwnershipConflictCache =
                new Dictionary<MMCBuildRecord, MMCSourceOwnershipConflict>();
        private readonly Dictionary<string, AvatarReductionPreviewData> _avatarReductionPreviewCache =
            new Dictionary<string, AvatarReductionPreviewData>();
        private readonly Dictionary<MMCBuildRecord, bool> _historyRelatedCache =
            new Dictionary<MMCBuildRecord, bool>();
        private readonly ScenePreviewController _scenePreview = new ScenePreviewController();
        private MMCBuildDatabase _managedBuildDatabase;
        private OptimizationAnalysis _analysis;
        private Vector2 _mergeScroll;
        private Vector2 _historyScroll;
        private Vector2 _rendererScroll;
        private GameObject _pendingDirectTarget;
        private bool _directTargetMode = true;
        private bool _showAllValidationIssues;
        private MainView _mainView = MainView.Merge;
        private HistoryFilter _historyFilter = HistoryFilter.All;
        private string _highlightedBuildId = string.Empty;
        private bool _historyGroupInternal;
        private string _historyGroupFilterKey = string.Empty;
        private GameObject _historySearchRoot;
        private readonly List<EmptyOutputContainerCandidate> _emptyOutputContainers =
            new List<EmptyOutputContainerCandidate>();
        private string _emptyContainerIgnoredSignature = string.Empty;
        private bool _managedBuildCacheDirty = true;
        private bool _sourceOwnershipConflictCacheReady;
        private bool _managedBuildInvalidationQueued;
        private double _managedBuildInvalidationDueTime;

        private const double ManagedBuildInvalidationDelay = 0.25d;

        private sealed class ManagedBuildItem
        {
            public string Key;
            public MMCBuildRecord Record;
            public MMCBuildResolvedState State;
        }

        private sealed class ManagedAvatarGroup
        {
            public string Key;
            public string Name;
            public readonly List<ManagedBuildItem> Items = new List<ManagedBuildItem>();
        }

        private sealed class AvatarReductionPreviewData
        {
            public bool Available;
            public OptimizationStatistics Before;
            public OptimizationStatistics After;
            public int RecordCount;
        }
        private static Texture2D _headerIcon;
        private const float HeaderHeight = 94f;
        private const float HeaderPadding = 16f;
        private const float CharacterSize = 56f;
        private const float TitleFontSize = 18f;
        private const float SubtitleFontSize = 12f;
        private const float ElementSpacing = 5f;

        private void OnEnable()
        {
            _emptyContainerIgnoredSignature = string.Empty;
            AssemblyReloadEvents.beforeAssemblyReload += ExitScenePreview;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.hierarchyChanged += QueueManagedBuildCacheInvalidation;
            Undo.undoRedoPerformed += QueueManagedBuildCacheInvalidation;
            ObjectChangeEvents.changesPublished += OnObjectChangesPublished;
            EditorApplication.delayCall -= ScanTrackedEmptyOutputContainers;
            EditorApplication.delayCall += ScanTrackedEmptyOutputContainers;
        }

        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ExitScenePreview;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.hierarchyChanged -= QueueManagedBuildCacheInvalidation;
            Undo.undoRedoPerformed -= QueueManagedBuildCacheInvalidation;
            ObjectChangeEvents.changesPublished -= OnObjectChangesPublished;
            EditorApplication.update -= FlushQueuedManagedBuildCacheInvalidation;
            _managedBuildInvalidationQueued = false;
            EditorApplication.delayCall -= ScanTrackedEmptyOutputContainers;
            ExitScenePreview();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode) ExitScenePreview();
        }

        [MenuItem("Tools/Azipa Tools/Mesh Material Combiner", false, 912)]
        private static void Open()
        {
            var window = GetWindow<AvatarOptimizerWindow>();
            window.titleContent = new GUIContent("Mesh Material Combiner");
            window.minSize = new Vector2(650f, 520f);
            window.Show();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawEmptyOutputContainerNotice();
            EditorGUILayout.Space(6f);
            DrawPrimaryNavigation();
            EditorGUILayout.Space(4f);
            if (_mainView == MainView.Merge)
            {
                _mergeScroll = EditorGUILayout.BeginScrollView(_mergeScroll);
                DrawTarget();
                EditorGUILayout.Space(6f);
                DrawSettings();
                EditorGUILayout.Space(8f);
                DrawRenderers();
                EditorGUILayout.Space(8f);
                DrawPreview();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                _historyScroll = EditorGUILayout.BeginScrollView(_historyScroll);
                DrawManagedBuilds();
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawPrimaryNavigation()
        {
            var historyLabel = OptimizerLocalization.T("History and Restore");
            using (new EditorGUILayout.HorizontalScope())
            {
                var navigationLabels = new[]
                {
                    OptimizerLocalization.T("New Merge"),
                    historyLabel
                };
                var nextView = (MainView)GUILayout.Toolbar((int)_mainView, navigationLabels,
                    GUILayout.Height(28f), GUILayout.MinWidth(340f));
                if (nextView != _mainView)
                {
                    _mainView = nextView;
                    if (_mainView == MainView.History && _managedBuildCacheDirty)
                        InvalidateManagedBuildCache();
                }

                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                var language = EditorGUILayout.Popup((int)OptimizerLocalization.Language,
                    OptimizerLocalization.LanguageLabels, GUILayout.Width(112f));
                if (EditorGUI.EndChangeCheck())
                {
                    OptimizerLocalization.Language = (OptimizerLanguage)language;
                    Repaint();
                }
            }
        }

        private void ScanTrackedEmptyOutputContainers()
        {
            if (this == null) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall -= ScanTrackedEmptyOutputContainers;
                EditorApplication.delayCall += ScanTrackedEmptyOutputContainers;
                return;
            }
            _emptyOutputContainers.Clear();
            _emptyOutputContainers.AddRange(EmptyOutputContainerCleanup.FindAllLoadedScenes());
            Repaint();
        }

        private void DrawEmptyOutputContainerNotice()
        {
            RemoveInvalidEmptyOutputContainers();
            if (_emptyOutputContainers.Count == 0) return;

            var signature = EmptyOutputContainerCleanup.GetSignature(_emptyOutputContainers);
            if (!string.IsNullOrEmpty(signature) && string.Equals(
                    _emptyContainerIgnoredSignature, signature, StringComparison.Ordinal)) return;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
                if (warningIcon == null || warningIcon.image == null)
                    warningIcon = EditorGUIUtility.IconContent("console.warnicon");
                if (warningIcon != null && warningIcon.image != null)
                    GUILayout.Label(warningIcon, GUILayout.Width(22f), GUILayout.Height(22f));

                var noticeStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    wordWrap = true,
                    alignment = TextAnchor.MiddleLeft
                };
                EditorGUILayout.LabelField(
                    OptimizerLocalization.T("Empty Output Folders Notice") + " " +
                    _emptyOutputContainers.Count,
                    noticeStyle, GUILayout.MinHeight(24f));
                using (new EditorGUILayout.VerticalScope(GUILayout.Height(28f)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(OptimizerLocalization.T("Review Empty Output Folders"),
                            EditorStyles.miniButton, GUILayout.Width(120f), GUILayout.Height(24f)))
                        EmptyOutputContainerCleanupWindow.Open(_emptyOutputContainers);
                    GUILayout.FlexibleSpace();
                }
                using (new EditorGUILayout.VerticalScope(GUILayout.Height(28f)))
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(OptimizerLocalization.T("Ignore This Time"),
                            EditorStyles.miniButton, GUILayout.Width(100f), GUILayout.Height(24f)))
                    {
                        _emptyContainerIgnoredSignature = signature;
                        Repaint();
                    }
                    GUILayout.FlexibleSpace();
                }
            }
            EditorGUILayout.Space(2f);
        }

        private void RemoveInvalidEmptyOutputContainers()
        {
            for (var i = _emptyOutputContainers.Count - 1; i >= 0; i--)
            {
                var candidate = _emptyOutputContainers[i];
                if (candidate != null && EmptyOutputContainerCleanup.IsSafeEmptyContainer(
                        candidate.Container, candidate.ExpectedParent)) continue;
                _emptyOutputContainers.RemoveAt(i);
            }
        }

        private void DrawHeader()
        {
            var headerBackground = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset((int)HeaderPadding, (int)HeaderPadding, 10, 10),
                margin = new RectOffset(0, 0, 0, 0)
            };
            using (new EditorGUILayout.VerticalScope(headerBackground, GUILayout.Height(HeaderHeight)))
            {
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = (int)TitleFontSize,
                    fixedHeight = 24f
                };
                var subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    fontSize = (int)SubtitleFontSize,
                    fixedHeight = 18f
                };
                // Use a stronger secondary-text color than centeredGreyMiniLabel's
                // default so the description remains readable in both Editor themes.
                subtitleStyle.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.72f, 0.72f, 0.72f, 1f)
                    : new Color(0.28f, 0.28f, 0.28f, 1f);
                if (_headerIcon == null)
                    _headerIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        "Packages/com.azipaworks.mesh-material-combiner/Editor/mesh_material_combiner_icon.png");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (_headerIcon != null)
                        GUILayout.Label(_headerIcon, GUIStyle.none, GUILayout.Width(CharacterSize), GUILayout.Height(CharacterSize));
                    GUILayout.Space(ElementSpacing + 5f);
                    using (new EditorGUILayout.VerticalScope(GUILayout.Height(CharacterSize), GUILayout.ExpandWidth(true)))
                    {
                        EditorGUILayout.LabelField(OptimizerLocalization.T("Tool Title"), titleStyle);
                        EditorGUILayout.Space(ElementSpacing);
                        EditorGUILayout.LabelField(OptimizerLocalization.T("Tool Subtitle"), subtitleStyle);
                    }
                    GUILayout.Space(10f);
                    if (GUILayout.Button(OptimizerLocalization.T("Readme"), GUILayout.Width(88f), GUILayout.Height(26f)))
                        OpenReadme();
                }
                GUILayout.FlexibleSpace();
                var footerStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    alignment = TextAnchor.MiddleRight
                };
                EditorGUILayout.LabelField("Mesh Material Combiner  v1.2.2", footerStyle,
                    GUILayout.ExpandWidth(true), GUILayout.Height(14f));
            }
        }

        private static void OpenReadme()
        {
            const string path = "Packages/com.azipaworks.mesh-material-combiner/README.md";
            var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (readme == null)
            {
                EditorUtility.DisplayDialog("Mesh Material Combiner",
                    "README.md was not found at:\n" + path, "OK");
                return;
            }
            Selection.activeObject = readme;
            EditorGUIUtility.PingObject(readme);
        }

        private void DrawTarget()
        {
            EditorGUILayout.LabelField(OptimizerLocalization.T("Target Selection Method"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var directMode = GUILayout.Toggle(_directTargetMode, OptimizerLocalization.T("Direct Objects"), EditorStyles.miniButtonLeft);
                var rootMode = GUILayout.Toggle(!_directTargetMode, OptimizerLocalization.T("Target Root"), EditorStyles.miniButtonRight);
                if (directMode && !_directTargetMode)
                {
                    _directTargetMode = true;
                    RefreshRenderers();
                }
                else if (rootMode && _directTargetMode)
                {
                    _directTargetMode = false;
                    // Direct Objectsモードで自動設定されたTargetRootをリセットする。
                    // リセットしないと旧Direct ObjectsのGameObjectが
                    // アバタールートとして誤用され、マージ先が意図しない場所になる。
                    _settings.TargetRoot = null;
                    RefreshRenderers();
                    SetAllRenderersIncluded(false);
                }
            }
            if (!_directTargetMode)
            {
                EditorGUILayout.LabelField(OptimizerLocalization.T("Target Root"), EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var target = (GameObject)EditorGUILayout.ObjectField(OptimizerLocalization.T("Target Root"), _settings.TargetRoot,
                    typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    _settings.TargetRoot = target;
                    ApplyAvatarDefaultNames(target);
                    RefreshRenderers();
                    SetAllRenderersIncluded(false);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(OptimizerLocalization.T("Refresh Renderers"), GUILayout.Width(150f))) RefreshRenderers();
                }
            }

            if (_directTargetMode)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(OptimizerLocalization.T("Direct Object Targets"), EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    OptimizerLocalization.T("Direct Object Help"),
                    MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(OptimizerLocalization.T("Add Selected"), GUILayout.Width(190f)))
                    {
                        foreach (var selected in Selection.gameObjects) AddDirectTarget(selected);
                        _pendingDirectTarget = null;
                    }
                    if (GUILayout.Button(OptimizerLocalization.T("Selected Reset"), GUILayout.Width(105f)))
                        ResetDirectTargets();
                }
                EditorGUI.BeginChangeCheck();
                _pendingDirectTarget = (GameObject)EditorGUILayout.ObjectField(
                    "Object (auto-add)", _pendingDirectTarget, typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck() && _pendingDirectTarget != null)
                {
                    AddDirectTarget(_pendingDirectTarget);
                    _pendingDirectTarget = null;
                }
                // 追加後にEditorOnlyになったオブジェクトを検出して除去する。
                // 表示中に_directTargetsを変更するため逆順ループを使う。
                var removedEditorOnly = false;
                for (var i = _directTargets.Count - 1; i >= 0; i--)
                {
                    if (_directTargets[i] == null) { _directTargets.RemoveAt(i); continue; }
                    // 追加後に別のマージ等でEditorOnlyになった場合は自動除去する
                    if (IsEditorOnlyHierarchy(_directTargets[i]))
                    {
                        _directTargets.RemoveAt(i);
                        removedEditorOnly = true;
                        continue;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.ObjectField(_directTargets[i], typeof(GameObject), true);
                        if (GUILayout.Button(OptimizerLocalization.T("Remove"), GUILayout.Width(65f)))
                        {
                            _directTargets.RemoveAt(i);
                            RefreshRenderers();
                        }
                    }
                }
                // EditorOnlyになったオブジェクトを除去した場合は一括でRefreshする
                if (removedEditorOnly) RefreshRenderers();

                // Rendererを持たないGameObjectだけが追加されている場合に理由を表示する
                if (_directTargets.Count > 0 && _entries.Count == 0)
                    EditorGUILayout.HelpBox(
                        "追加されたオブジェクトに有効な Renderer が見つかりません。" +
                        " SkinnedMeshRenderer または MeshRenderer を持つオブジェクトを追加してください。",
                        MessageType.Warning);

                var selectedObjectsStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 12,
                    normal = { textColor = EditorStyles.label.normal.textColor }
                };
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.LabelField($"Selected Objects: {_directTargets.Count}", selectedObjectsStyle,
                        GUILayout.MinWidth(160f));
                }
            }
        }

        private void DrawSettings()
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(OptimizerLocalization.T("Merge Settings"), EditorStyles.boldLabel);
            var atlasSizeAdjusted = false;
            if (_settings.AtlasSize != 1024 && _settings.AtlasSize != 2048 &&
                _settings.AtlasSize != OptimizationPipeline.StandardMaximumAtlasSize)
            {
                _settings.AtlasSize = OptimizationPipeline.StandardMaximumAtlasSize;
                atlasSizeAdjusted = true;
            }
            _settings.AtlasSize = EditorGUILayout.IntPopup("Atlas Resolution", _settings.AtlasSize,
                new[] { "1024", "2048", "4096" }, new[] { 1024, 2048, 4096 });
            var blendShapeLabels = new[]
            {
                "Merge Same Name",
                "Prefix By Renderer Name (Recommended)",
                "Keep Separate"
            };
            _settings.BlendShapeMode = (BlendShapeCollisionMode)EditorGUILayout.Popup(
                OptimizerLocalization.T("BlendShape Collision"), (int)_settings.BlendShapeMode, blendShapeLabels);
            _settings.Preset = OptimizationPresetUtility.Normalize(_settings.Preset);
            var presetLabels = new[]
            {
                OptimizerLocalization.T("Preset Safe Label"),
                OptimizerLocalization.T("Preset Compatible Merge Label"),
                OptimizerLocalization.T("Preset Force Single Slot Label")
            };
            var presetValues = new[]
            {
                (int)OptimizationPreset.Safe,
                (int)OptimizationPreset.CompatibleMerge,
                (int)OptimizationPreset.ForceSingleSlot
            };
            _settings.Preset = (OptimizationPreset)EditorGUILayout.IntPopup(
                OptimizerLocalization.T("Optimization Preset"), (int)_settings.Preset,
                presetLabels, presetValues);
            if (_settings.Preset == OptimizationPreset.ForceSingleSlot)
            {
                _settings.ForceRepresentativeMaterial = (Material)EditorGUILayout.ObjectField(
                    new GUIContent(OptimizerLocalization.T("Representative Material"),
                        OptimizerLocalization.T("Representative Material Tooltip")),
                    _settings.ForceRepresentativeMaterial, typeof(Material), false);
            }
            EditorGUILayout.HelpBox(OptimizerLocalization.PresetDescription(_settings.Preset), MessageType.Info);
            if (EditorGUI.EndChangeCheck() || atlasSizeAdjusted) InvalidateAnalysis();

            EditorGUILayout.HelpBox(OptimizerLocalization.T("Merged Output Description"), MessageType.Info);
        }

        private void DrawRenderers()
        {
            var selectedCount = 0;
            foreach (var entry in _entries)
                if (entry.Renderer != null && entry.Included) selectedCount++;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Renderers ({selectedCount} / {_entries.Count} selected)", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(OptimizerLocalization.T("All ON"), GUILayout.Width(78f))) SetAllRenderersIncluded(true);
                if (GUILayout.Button(OptimizerLocalization.T("All OFF"), GUILayout.Width(78f))) SetAllRenderersIncluded(false);
            }
            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox(OptimizerLocalization.T(_directTargetMode
                    ? "Assign Direct Objects Help" : "Assign Target Root Help"), MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(OptimizerLocalization.T("Renderer Selection Help"), MessageType.None);

            _rendererScroll = EditorGUILayout.BeginScrollView(_rendererScroll, GUILayout.MinHeight(180f), GUILayout.MaxHeight(380f));
            if (!_directTargetMode && _settings.TargetRoot != null)
            {
                var byTransform = new Dictionary<Transform, List<RendererEntry>>();
                var relevant = new HashSet<Transform>();
                foreach (var entry in _entries)
                {
                    if (entry.Renderer == null) continue;
                    var transform = entry.Renderer.transform;
                    if (!byTransform.TryGetValue(transform, out var list))
                    { list = new List<RendererEntry>(); byTransform.Add(transform, list); }
                    list.Add(entry);
                    while (transform != null)
                    {
                        relevant.Add(transform);
                        if (transform == _settings.TargetRoot.transform) break;
                        transform = transform.parent;
                    }
                }
                DrawHierarchyNode(_settings.TargetRoot.transform, byTransform, relevant);

                var outsideRoot = new List<RendererEntry>();
                foreach (var entry in _entries)
                    if (entry.Renderer != null && !entry.Renderer.transform.IsChildOf(_settings.TargetRoot.transform))
                        outsideRoot.Add(entry);
                if (outsideRoot.Count > 0)
                {
                    EditorGUILayout.Space(4f);
                    EditorGUILayout.LabelField(OptimizerLocalization.T("Direct Object Renderers"), EditorStyles.boldLabel);
                    foreach (var entry in outsideRoot) DrawRendererEntry(entry);
                }
            }
            else
            {
                foreach (var entry in _entries)
                    if (entry.Renderer != null) DrawRendererEntry(entry);
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawHierarchyNode(Transform transform,
            Dictionary<Transform, List<RendererEntry>> byTransform, HashSet<Transform> relevant)
        {
            if (transform == null || !relevant.Contains(transform)) return;
            var hasRelevantChildren = false;
            foreach (Transform child in transform)
                if (relevant.Contains(child)) { hasRelevantChildren = true; break; }
            var hasRenderers = byTransform.ContainsKey(transform);
            var expandable = hasRelevantChildren || hasRenderers;
            if (!_hierarchyExpanded.TryGetValue(transform, out var expanded)) expanded = true;

            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = expandable
                    ? EditorGUILayout.Foldout(expanded, transform.name, true)
                    : false;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50f)))
                {
                    Selection.activeGameObject = transform.gameObject;
                    EditorGUIUtility.PingObject(transform.gameObject);
                }
            }
            _hierarchyExpanded[transform] = expanded;
            if (!expanded) return;

            EditorGUI.indentLevel++;
            if (byTransform.TryGetValue(transform, out var renderers))
                foreach (var entry in renderers) DrawRendererEntry(entry);
            foreach (Transform child in transform)
                if (relevant.Contains(child)) DrawHierarchyNode(child, byTransform, relevant);
            EditorGUI.indentLevel--;
        }

        private void DrawRendererEntry(RendererEntry entry)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                var hierarchyIndent = EditorGUI.indentLevel;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.indentLevel = 0;
                    GUILayout.Space(hierarchyIndent * 15f);
                    EditorGUI.BeginChangeCheck();
                    entry.Included = EditorGUILayout.Toggle(entry.Included, GUILayout.Width(18f));
                    if (EditorGUI.EndChangeCheck()) InvalidateAnalysis();
                    _expanded.TryGetValue(entry.Renderer, out var expanded);
                    var meshName = entry.Mesh != null ? entry.Mesh.name : "<Missing Mesh>";
                    expanded = EditorGUILayout.Foldout(expanded, $"{entry.Kind}  |  {meshName}", true);
                    _expanded[entry.Renderer] = expanded;
                }
                EditorGUI.indentLevel = hierarchyIndent;
                if (_expanded[entry.Renderer]) DrawRendererDetails(entry);
            }
        }

        private void SetAllRenderersIncluded(bool included)
        {
            foreach (var entry in _entries)
                if (entry.Renderer != null) entry.Included = included;
            InvalidateAnalysis();
            Repaint();
        }

        private void ResetDirectTargets()
        {
            _directTargets.Clear();
            _pendingDirectTarget = null;
            // TargetRootはAddDirectTargetが「nullのとき」だけ自動設定するため、
            // リセットしないと別アバターを追加しても旧アバターのままになる。
            _settings.TargetRoot = null;
            _settings.GroupName = "Avatar";
            _settings.MergedObjectName = string.Empty;
            RefreshRenderers();
        }

        private static void DrawRendererDetails(RendererEntry entry)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Vertices", entry.VertexCount.ToString("N0"));
            EditorGUILayout.LabelField("Triangles", entry.TriangleCount.ToString("N0"));
            EditorGUILayout.LabelField("Material Slots", entry.MaterialSlotCount.ToString());
            for (var i = 0; i < entry.Materials.Length; i++)
            {
                var material = entry.Materials[i];
                var shader = material != null && material.shader != null ? material.shader.name : "<Missing>";
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField($"Material {i}", material, typeof(Material), false);
                    GUILayout.Label(shader, GUILayout.MinWidth(180f));
                }
            }
            EditorGUI.indentLevel--;
        }

        private void DrawPreview()
        {
            _settings.MergedObjectName = EditorGUILayout.TextField(
                "Merged Object Name",
                string.IsNullOrWhiteSpace(_settings.MergedObjectName)
                    ? _settings.GroupName + "_Merged"
                    : _settings.MergedObjectName);
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(OptimizerLocalization.T("Analyze"), GUILayout.Height(28f))) Analyze();
                using (new EditorGUI.DisabledScope(_analysis == null || _analysis.Report.HasErrors))
                {
                    if (GUILayout.Button(OptimizerLocalization.T("Merge Meshes"), GUILayout.Height(28f))) ExecuteMerge();
                }
            }
            if (_analysis == null) return;

            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(OptimizerLocalization.T("Preview"), EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawStatistics("Before", _analysis.Report.Before, null);
                DrawStatistics("After", _analysis.Report.After, _analysis.Report.Before);
            }

            DrawScenePreviewControls();

            EditorGUILayout.LabelField($"Material Compatibility Groups: {_analysis.MaterialGroups.Count}", EditorStyles.boldLabel);
            foreach (var group in _analysis.MaterialGroups)
                EditorGUILayout.LabelField($"Group {group.Index}: {group.Adapter.Name} / {group.Materials.Count} material(s)");

            if (_analysis.Report.Issues.Count > 0)
            {
                EditorGUILayout.Space(5f);
                DrawValidationIssues();
            }
            if (!string.IsNullOrEmpty(_analysis.Report.ResultMessage))
                EditorGUILayout.HelpBox(_analysis.Report.ResultMessage, MessageType.Info);
        }

        private void DrawValidationIssues()
        {
            var issues = _analysis.Report.Issues;
            var orderedIssues = new List<ValidationIssue>(issues.Count);
            AppendValidationIssues(issues, orderedIssues, ValidationSeverity.Error);
            AppendValidationIssues(issues, orderedIssues, ValidationSeverity.Warning);
            AppendValidationIssues(issues, orderedIssues, ValidationSeverity.Info);
            var errors = 0;
            var warnings = 0;
            var infos = 0;
            for (var i = 0; i < issues.Count; i++)
            {
                switch (issues[i].Severity)
                {
                    case ValidationSeverity.Error: errors++; break;
                    case ValidationSeverity.Warning: warnings++; break;
                    default: infos++; break;
                }
            }

            var validationLabel = OptimizerLocalization.T("Validation");
            EditorGUILayout.LabelField(
                $"{validationLabel}  (Error {errors}, Warning {warnings}, Info {infos})",
                EditorStyles.boldLabel);

            // Keep the window responsive even when a mesh contains a very large
            // number of diagnostics. Details can still be expanded on demand.
            const int maxVisible = 20;
            var visibleCount = _showAllValidationIssues
                ? orderedIssues.Count
                : Mathf.Min(maxVisible, orderedIssues.Count);
            for (var i = 0; i < visibleCount; i++)
            {
                var issue = orderedIssues[i];
                var type = issue.Severity == ValidationSeverity.Error ? MessageType.Error :
                    issue.Severity == ValidationSeverity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox($"[{issue.Severity}] {issue.Message}", type);
            }
            if (!_showAllValidationIssues && issues.Count > maxVisible)
            {
                EditorGUILayout.HelpBox(
                    $"{issues.Count - maxVisible} additional diagnostics are hidden to keep the editor responsive.",
                    MessageType.None);
                if (GUILayout.Button($"Show all diagnostics ({issues.Count})", EditorStyles.miniButton))
                    _showAllValidationIssues = true;
            }
            else if (_showAllValidationIssues && issues.Count > maxVisible)
            {
                if (GUILayout.Button("Show fewer diagnostics", EditorStyles.miniButton))
                    _showAllValidationIssues = false;
            }
        }

        private static void AppendValidationIssues(IList<ValidationIssue> source,
            ICollection<ValidationIssue> destination, ValidationSeverity severity)
        {
            for (var i = 0; i < source.Count; i++)
                if (source[i].Severity == severity)
                    destination.Add(source[i]);
        }

        private void DrawManagedBuilds()
        {
            if (_managedBuildDatabase == null) _managedBuildDatabase = MMCBuildDatabaseService.Load();
            var database = _managedBuildDatabase;
            EnsureSourceOwnershipConflictCache(database);
            var unrecoverableCount = GetUnrecoverableRecordCount(database);
            if (DrawHistorySearchPanel(database)) return;
            // Keep the history toolbar usable even when the window is temporarily
            // narrower than its normal minimum size. The bulk-action button may
            // shrink, but must never push the other controls outside the window.
            var bulkActionWidth = Mathf.Clamp(position.width - 520f, 110f, 160f);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(OptimizerLocalization.T("Managed Builds"), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(unrecoverableCount == 0))
                {
                    if (GUILayout.Button(OptimizerLocalization.T("Remove All Unrecoverable Records") +
                                         " (" + unrecoverableCount + ")",
                            EditorStyles.miniButton, GUILayout.Width(bulkActionWidth)))
                    {
                        if (EditorUtility.DisplayDialog(
                                OptimizerLocalization.T("Remove All Unrecoverable Records"),
                                OptimizerLocalization.T("Remove All Unrecoverable Records Confirmation") +
                                "\n\n" + OptimizerLocalization.T("Unrecoverable Record Count") + ": " + unrecoverableCount,
                                OptimizerLocalization.T("Remove All Unrecoverable Records"),
                                OptimizerLocalization.T("Cancel")))
                        {
                            if (MMCBuildDatabaseService.RemoveUnrecoverableRecords(out var bulkMessage) > 0)
                                ShowNotification(new GUIContent(bulkMessage));
                            InvalidateManagedBuildCache();
                            return;
                        }
                    }
                }
                var filterLabels = new[]
                {
                    OptimizerLocalization.T("History Filter All"),
                    OptimizerLocalization.T("History Filter Issues"),
                    OptimizerLocalization.T("History Filter Valid")
                };
                _historyFilter = (HistoryFilter)EditorGUILayout.Popup((int)_historyFilter, filterLabels,
                    GUILayout.Width(120f));
                if (GUILayout.Button(OptimizerLocalization.T("Refresh"), EditorStyles.miniButton,
                        GUILayout.Width(72f))) InvalidateManagedBuildCache();
            }

            if (database == null || database.Builds.Count == 0)
            {
                EditorGUILayout.HelpBox(OptimizerLocalization.T("No Managed Builds"), MessageType.None);
                return;
            }

            var groupOrder = new List<string>();
            var groupNames = new Dictionary<string, string>();
            var groupNameCounts = new Dictionary<string, int>();
            var groupCounts = new Dictionary<string, int>();
            // Keep the avatar-level status based on every matching history,
            // regardless of the currently selected history filter.
            var groupAllIssues = new Dictionary<string, int>();
            var databaseNameChanged = false;
            var databaseUndoRecorded = false;
            for (var i = database.Builds.Count - 1; i >= 0; i--)
            {
                var record = database.Builds[i];
                if (record == null) continue;
                if (!MatchesHistorySearch(record)) continue;
                var buildKey = GetRecordKey(record, i);
                var state = GetManagedBuildState(buildKey, record);
                var rebuild = GetRebuildEvaluation(buildKey, record);
                if (state.Output != null && !string.Equals(record.BuildName, state.Output.name,
                        StringComparison.Ordinal))
                {
                    if (!databaseUndoRecorded)
                    {
                        Undo.RecordObject(database, "Synchronize merged object name in history");
                        databaseUndoRecorded = true;
                    }
                    record.BuildName = state.Output.name;
                    if (record.Recipe != null && record.Recipe.IsValid)
                        record.Recipe.OutputName = state.Output.name;
                    databaseNameChanged = true;
                }
                var hasIssue = state.Status != MMCBuildStatus.Valid ||
                               rebuild.State != MMCRebuildState.UpToDate;
                var groupKey = GetAvatarGroupKey(record, i);
                if (!groupAllIssues.ContainsKey(groupKey)) groupAllIssues[groupKey] = 0;
                if (hasIssue) groupAllIssues[groupKey]++;
                if (_historyFilter == HistoryFilter.Issues && !hasIssue) continue;
                if (_historyFilter == HistoryFilter.Valid && hasIssue) continue;
                if (!groupCounts.ContainsKey(groupKey))
                {
                    groupOrder.Add(groupKey);
                    var avatarName = GetAvatarGroupName(record);
                    if (!groupNameCounts.TryGetValue(avatarName, out var nameCount)) nameCount = 0;
                    nameCount++;
                    groupNameCounts[avatarName] = nameCount;
                    groupNames[groupKey] = nameCount > 1 ? avatarName + " (" + nameCount + ")" : avatarName;
                    groupCounts[groupKey] = 0;
                }
                groupCounts[groupKey]++;
            }

            if (databaseNameChanged)
            {
                EditorUtility.SetDirty(database);
                AssetDatabase.SaveAssets();
            }

            if (groupOrder.Count == 0)
            {
                EditorGUILayout.HelpBox(OptimizerLocalization.T("No Matching Builds"), MessageType.None);
                return;
            }

            for (var groupIndex = 0; groupIndex < groupOrder.Count; groupIndex++)
            {
                var groupKey = groupOrder[groupIndex];
                if (!_managedAvatarGroupExpanded.TryGetValue(groupKey, out var expanded)) expanded = true;
                var groupStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(7, 7, 4, 5),
                    margin = new RectOffset(0, 0, 3, 5)
                };
                using (new EditorGUILayout.VerticalScope(groupStyle))
                {
                    var groupLabelStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = EditorStyles.boldLabel.fontSize + 1
                    };
                    var groupHasIssue = groupAllIssues[groupKey] > 0;
                    var groupStatusText = groupHasIssue
                        ? groupAllIssues[groupKey] + OptimizerLocalization.T("History Group Unmerged Count")
                        : OptimizerLocalization.T("History Group Merge Complete");
                    var groupStatusStyle = GetStatusStyle(groupHasIssue ?
                        MMCBuildStatus.Modified : MMCBuildStatus.Valid);
                    groupStatusStyle.fontSize = EditorStyles.miniBoldLabel.fontSize;
                    groupStatusStyle.alignment = TextAnchor.MiddleRight;
                    var headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                    var statusWidth = Mathf.Max(80f,
                        groupStatusStyle.CalcSize(new GUIContent(groupStatusText)).x);
                    var statusRect = new Rect(
                        Mathf.Max(headerRect.x, headerRect.xMax - statusWidth),
                        headerRect.y, statusWidth, headerRect.height);
                    var foldoutRect = new Rect(
                        headerRect.x, headerRect.y,
                        Mathf.Max(0f, statusRect.x - headerRect.x - 6f), headerRect.height);
                    expanded = EditorGUI.Foldout(foldoutRect,
                        expanded, groupNames[groupKey] + "  (" + groupCounts[groupKey] + ")", true,
                        groupLabelStyle);
                    EditorGUI.LabelField(statusRect, groupStatusText, groupStatusStyle);
                    _managedAvatarGroupExpanded[groupKey] = expanded;
                    if (!expanded) continue;
                    EditorGUILayout.Space(2f);
                    var avatarRecords = GetAvatarGroupRecords(database, groupKey);
                    if (DrawAvatarLifecycleActions(avatarRecords)) return;
                    DrawAvatarReductionPreview(groupKey, avatarRecords);
                    EditorGUILayout.Space(2f);
                    _historyGroupInternal = true;
                    _historyGroupFilterKey = groupKey;
                    DrawManagedBuildsInternal();
                    var historyChanged = _managedBuildDatabase == null;
                    _historyGroupFilterKey = string.Empty;
                    _historyGroupInternal = false;
                    if (historyChanged) return;
                }
            }
        }

        private bool DrawHistorySearchPanel(MMCBuildDatabase database)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(OptimizerLocalization.T("Find Related Builds"),
                    EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var root = (GameObject)EditorGUILayout.ObjectField(
                    OptimizerLocalization.T("Related Root"), _historySearchRoot,
                    typeof(GameObject), true);
                if (EditorGUI.EndChangeCheck())
                {
                    _historySearchRoot = root;
                    _historyRelatedCache.Clear();
                    GUI.FocusControl(null);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(OptimizerLocalization.T("Use Selected Object"),
                            EditorStyles.miniButton))
                    {
                        _historySearchRoot = Selection.activeGameObject;
                        _historyRelatedCache.Clear();
                        GUI.FocusControl(null);
                    }
                    using (new EditorGUI.DisabledScope(_historySearchRoot == null))
                        if (GUILayout.Button(OptimizerLocalization.T("Clear Filter"),
                                EditorStyles.miniButton))
                        {
                            _historySearchRoot = null;
                            _historyRelatedCache.Clear();
                            GUI.FocusControl(null);
                        }
                }

                var related = new List<MMCBuildRecord>();
                var relatedCount = 0;
                // Related-history operations require an explicit root. Without a
                // search target, do not interpret the entire database as related.
                if (_historySearchRoot != null && database != null && database.Builds != null)
                    for (var i = 0; i < database.Builds.Count; i++)
                    {
                        var record = database.Builds[i];
                        if (record == null || !MatchesHistorySearch(record)) continue;
                        related.Add(record);
                        relatedCount++;
                    }
                MMCSourceOwnershipConflict relatedConflict = null;
                foreach (var record in related)
                    if (record != null &&
                        _sourceOwnershipConflictCache.TryGetValue(record, out relatedConflict))
                        break;
                if (relatedConflict != null)
                    EditorGUILayout.HelpBox(
                        MMCBuildDatabaseService.FormatSourceOwnershipConflict(relatedConflict),
                        MessageType.Error);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(
                        OptimizerLocalization.T("Related Build Count") + ": " + related.Count,
                        EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    using (new EditorGUI.DisabledScope(relatedCount == 0 || relatedConflict != null))
                    {
                        if (GUILayout.Button(
                                OptimizerLocalization.T("Rebuild Related Builds") + " (" + relatedCount + ")",
                                EditorStyles.miniButton, GUILayout.MaxWidth(230f)))
                        {
                            if (!EditorUtility.DisplayDialog(
                                    OptimizerLocalization.T("Rebuild Related Builds"),
                                    OptimizerLocalization.T("Rebuild Related Builds Confirmation") +
                                    "\n\n" + relatedCount,
                                    OptimizerLocalization.T("Rebuild"),
                                    OptimizerLocalization.T("Cancel"))) return false;
                            var order = MMCRebuildService.GetRebuildOrder(related, out var orderError);
                            if (order.Count == 0 && !string.IsNullOrEmpty(orderError))
                            {
                                EditorUtility.DisplayDialog("Mesh Material Combiner", orderError, "OK");
                                return false;
                            }
                            var rebuilt = 0;
                            var failed = new List<string>();
                            foreach (var record in order)
                            {
                                if (!MMCRebuildService.Rebuild(record, out var rebuildMessage))
                                {
                                    failed.Add(rebuildMessage);
                                    continue;
                                }
                                rebuilt++;
                            }
                            if (failed.Count > 0)
                                EditorUtility.DisplayDialog("Mesh Material Combiner",
                                    string.Join("\n\n", failed), "OK");
                            ShowNotification(new GUIContent(
                                OptimizerLocalization.T("Bulk Rebuild Completed") + ": " + rebuilt +
                                (failed.Count > 0 ? " / " + relatedCount : string.Empty)));
                            InvalidateManagedBuildCache();
                            return true;
                        }
                    }
                }
            }
            EditorGUILayout.Space(4f);
            return false;
        }

        private bool MatchesHistorySearch(MMCBuildRecord record)
        {
            if (_historySearchRoot == null) return true;
            if (_historyRelatedCache.TryGetValue(record, out var related)) return related;
            related = MMCRebuildService.IsRelatedToRoot(record, _historySearchRoot);
            _historyRelatedCache[record] = related;
            return related;
        }

        private void DrawManagedBuildsInternal()
        {
            if (!_historyGroupInternal)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(OptimizerLocalization.T("Managed Builds"), EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    var filterLabels = new[]
                    {
                        OptimizerLocalization.T("History Filter All"),
                        OptimizerLocalization.T("History Filter Issues"),
                        OptimizerLocalization.T("History Filter Valid")
                    };
                    _historyFilter = (HistoryFilter)EditorGUILayout.Popup((int)_historyFilter, filterLabels,
                        GUILayout.Width(120f));
                    if (GUILayout.Button(OptimizerLocalization.T("Refresh"), EditorStyles.miniButton,
                            GUILayout.Width(72f))) InvalidateManagedBuildCache();
                }
            }

            if (_managedBuildDatabase == null) _managedBuildDatabase = MMCBuildDatabaseService.Load();
            var database = _managedBuildDatabase;
            if (database == null || database.Builds.Count == 0)
            {
                EditorGUILayout.HelpBox(OptimizerLocalization.T("No Managed Builds"), MessageType.None);
                return;
            }

            var visibleBuildCount = 0;
            for (var i = database.Builds.Count - 1; i >= 0; i--)
            {
                var record = database.Builds[i];
                if (record == null) continue;
                if (!MatchesHistorySearch(record)) continue;
                var key = GetRecordKey(record, i);
                if (!string.IsNullOrEmpty(_historyGroupFilterKey) &&
                    GetAvatarGroupKey(record, i) != _historyGroupFilterKey) continue;
                var resolvedState = GetManagedBuildState(key, record);
                var rebuildEvaluation = GetRebuildEvaluation(key, record);
                var hasIssue = resolvedState.Status != MMCBuildStatus.Valid ||
                               rebuildEvaluation.State != MMCRebuildState.UpToDate;
                if (_historyFilter == HistoryFilter.Issues && !hasIssue) continue;
                if (_historyFilter == HistoryFilter.Valid && hasIssue) continue;
                visibleBuildCount++;
                if (!_managedBuildExpanded.TryGetValue(key, out var expanded)) expanded = false;
                var cardStyle = new GUIStyle(EditorStyles.helpBox)
                {
                    padding = new RectOffset(8, 8, 5, 6),
                    margin = new RectOffset(0, 0, 1, 4)
                };
                using (new EditorGUILayout.VerticalScope(cardStyle))
                {
                    var status = resolvedState.Status;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var buildLabel = string.IsNullOrEmpty(record.BuildName) ? "(Unnamed Build)" : record.BuildName;
                        if (key == _highlightedBuildId) buildLabel = "★ " + buildLabel;
                        var buildLabelStyle = new GUIStyle(EditorStyles.foldout)
                        {
                            fontStyle = FontStyle.Bold,
                            fontSize = EditorStyles.boldLabel.fontSize
                        };
                        expanded = EditorGUILayout.Foldout(expanded,
                            buildLabel, true, buildLabelStyle);
                        GUILayout.FlexibleSpace();
                        DrawLifecycleBadge(record.Lifecycle);
                        GUILayout.Space(4f);
                        DrawRebuildStatusBadge(rebuildEvaluation.State);
                        GUILayout.Space(4f);
                        DrawStatusBadge(status);
                    }
                    _managedBuildExpanded[key] = expanded;
                    var output = resolvedState.Output;

                    var summary = OptimizerLocalization.T("Source Count") + ": " + record.Sources.Count;
                    var lastBuiltAt = string.IsNullOrEmpty(record.LastBuiltAt)
                        ? record.CreatedAt
                        : record.LastBuiltAt;
                    if (!string.IsNullOrEmpty(lastBuiltAt))
                        summary += "    " + OptimizerLocalization.T("Last Built At") + ": " + lastBuiltAt;
                    EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

                    if (expanded)
                    {
                        EditorGUILayout.Space(2f);
                        if (record.SavedBeforeStats != null && record.SavedAfterStats != null)
                        {
                            DrawHistoryStatistics(record.SavedBeforeStats, record.SavedAfterStats);
                            EditorGUILayout.Space(2f);
                        }

                        EditorGUI.indentLevel++;
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(OptimizerLocalization.T("Merged Output"), output,
                                typeof(GameObject), true);
                        EditorGUILayout.LabelField(OptimizerLocalization.T("Generated Assets"),
                            record.GeneratedAssetPaths.Count.ToString());
                        EditorGUILayout.LabelField(OptimizerLocalization.T("Revisions"),
                            record.Revisions != null && record.Revisions.Count > 0
                                ? (record.ActiveRevisionIndex + 1) + " / " + record.Revisions.Count
                                : "0");
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            using (new EditorGUI.DisabledScope(record.Lifecycle != MMCBuildLifecycle.Built ||
                                                               record.Revisions == null ||
                                                               record.ActiveRevisionIndex <= 0))
                            {
                                if (GUILayout.Button(OptimizerLocalization.T("Restore Previous Revision"),
                                        EditorStyles.miniButton, GUILayout.Height(19f)) &&
                                    EditorUtility.DisplayDialog(
                                        OptimizerLocalization.T("Revision Management"),
                                        OptimizerLocalization.T("Apply Revision Confirmation"),
                                        OptimizerLocalization.T("Apply Revision"),
                                        OptimizerLocalization.T("Cancel")))
                                {
                                    if (!MMCRebuildService.RestorePreviousRevision(record,
                                            out var revisionMessage))
                                        EditorUtility.DisplayDialog("Mesh Material Combiner",
                                            revisionMessage, "OK");
                                    else
                                        ShowNotification(new GUIContent(revisionMessage));
                                    InvalidateManagedBuildCache();
                                    return;
                                }
                            }
                            using (new EditorGUI.DisabledScope(record.Lifecycle != MMCBuildLifecycle.Built ||
                                                               record.Revisions == null ||
                                                               record.ActiveRevisionIndex < 0 ||
                                                               record.ActiveRevisionIndex >=
                                                               record.Revisions.Count - 1))
                            {
                                if (GUILayout.Button(OptimizerLocalization.T("Restore Next Revision"),
                                        EditorStyles.miniButton, GUILayout.Height(19f)) &&
                                    EditorUtility.DisplayDialog(
                                        OptimizerLocalization.T("Revision Management"),
                                        OptimizerLocalization.T("Apply Revision Confirmation"),
                                        OptimizerLocalization.T("Apply Revision"),
                                        OptimizerLocalization.T("Cancel")))
                                {
                                    if (!MMCRebuildService.RestoreNextRevision(record,
                                            out var revisionMessage))
                                        EditorUtility.DisplayDialog("Mesh Material Combiner",
                                            revisionMessage, "OK");
                                    else
                                        ShowNotification(new GUIContent(revisionMessage));
                                    InvalidateManagedBuildCache();
                                    return;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(rebuildEvaluation.Message))
                            EditorGUILayout.HelpBox(rebuildEvaluation.Message,
                                rebuildEvaluation.State == MMCRebuildState.UpToDate
                                    ? MessageType.Info
                                    : MessageType.Warning);
                        var sourcesKey = key + ":sources";
                        if (!_managedBuildExpanded.TryGetValue(sourcesKey, out var sourcesExpanded))
                            sourcesExpanded = false;
                        sourcesExpanded = EditorGUILayout.Foldout(sourcesExpanded,
                            OptimizerLocalization.T("Source List") + " (" + record.Sources.Count + ")", true);
                        _managedBuildExpanded[sourcesKey] = sourcesExpanded;
                        if (sourcesExpanded)
                        {
                            EditorGUI.indentLevel++;
                            for (var sourceIndex = 0; sourceIndex < record.Sources.Count; sourceIndex++)
                            {
                                var sourceRecord = record.Sources[sourceIndex];
                                var sourceObject = sourceIndex < resolvedState.Sources.Count
                                    ? resolvedState.Sources[sourceIndex]
                                    : null;
                                using (new EditorGUI.DisabledScope(true))
                                    EditorGUILayout.ObjectField(sourceRecord.Object.DisplayName, sourceObject,
                                        typeof(GameObject), true);
                            }
                            EditorGUI.indentLevel--;
                        }
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.Space(2f);
                    if (DrawManagedBuildActions(record, key, resolvedState, rebuildEvaluation)) return;
                }
            }
            if (!_historyGroupInternal && visibleBuildCount == 0)
                EditorGUILayout.HelpBox(OptimizerLocalization.T("No Matching Builds"), MessageType.None);
        }

        private bool DrawManagedBuildActions(MMCBuildRecord record, string key,
            MMCBuildResolvedState state, MMCRebuildEvaluation rebuildEvaluation)
        {
            var compact = position.width < 600f;
            if (compact)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSelectOutputAction(state.Output);
                    DrawSelectSourcesAction(state.AvailableSources);
                    if (DrawSuspendAction(record, state.Status)) return true;
                }
                EditorGUILayout.Space(1f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (DrawRebuildAction(record, rebuildEvaluation)) return true;
                    if (DrawFullyDetachAction(record, state.Status)) return true;
                    if (DrawRemoveUnrecoverableAction(record, key, state.Status)) return true;
                }
                return false;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSelectOutputAction(state.Output);
                DrawSelectSourcesAction(state.AvailableSources);
                if (DrawSuspendAction(record, state.Status)) return true;
                GUILayout.FlexibleSpace();
                if (DrawRebuildAction(record, rebuildEvaluation)) return true;
                if (DrawFullyDetachAction(record, state.Status)) return true;
                if (DrawRemoveUnrecoverableAction(record, key, state.Status)) return true;
            }
            return false;
        }

        private bool DrawAvatarLifecycleActions(IList<MMCBuildRecord> records)
        {
            var hasBuilt = false;
            var hasCleanupPending = false;
            MMCSourceOwnershipConflict ownershipConflict = null;
            foreach (var record in records)
            {
                if (record == null) continue;
                if (record.Lifecycle == MMCBuildLifecycle.Built) hasBuilt = true;
                if (record.Lifecycle == MMCBuildLifecycle.CleanupPending) hasCleanupPending = true;
                if (ownershipConflict == null)
                    _sourceOwnershipConflictCache.TryGetValue(record, out ownershipConflict);
            }
            if (ownershipConflict != null)
                EditorGUILayout.HelpBox(
                    MMCBuildDatabaseService.FormatSourceOwnershipConflict(ownershipConflict),
                    MessageType.Error);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(OptimizerLocalization.T("Avatar Operations"),
                    EditorStyles.miniBoldLabel, GUILayout.Width(120f));
                using (new EditorGUI.DisabledScope(!hasBuilt || hasCleanupPending ||
                                                    ownershipConflict != null))
                {
                    if (GUILayout.Button(OptimizerLocalization.T("Suspend Avatar For Editing"),
                            EditorStyles.miniButton, GUILayout.Height(21f)))
                    {
                        if (!EditorUtility.DisplayDialog(
                            OptimizerLocalization.T("Suspend Avatar For Editing"),
                            OptimizerLocalization.T("Suspend Avatar Confirmation"),
                            OptimizerLocalization.T("Suspend For Editing"),
                            OptimizerLocalization.T("Cancel"))) return false;
                        if (!MMCAvatarLifecycleService.SuspendAvatar(records, out var message))
                            EditorUtility.DisplayDialog("Mesh Material Combiner", message, "OK");
                        else ShowNotification(new GUIContent(message));
                        InvalidateManagedBuildCache();
                        return true;
                    }
                }
                using (new EditorGUI.DisabledScope(records.Count == 0 || hasCleanupPending ||
                                                    ownershipConflict != null))
                {
                    if (GUILayout.Button(OptimizerLocalization.T("Prepare Avatar For Upload"),
                            EditorStyles.miniButton, GUILayout.Height(21f)))
                    {
                        if (!EditorUtility.DisplayDialog(
                                OptimizerLocalization.T("Prepare Avatar For Upload"),
                                OptimizerLocalization.T("Prepare Avatar Confirmation"),
                                OptimizerLocalization.T("Rebuild"),
                                OptimizerLocalization.T("Cancel"))) return false;
                        if (!MMCAvatarLifecycleService.RebuildAvatar(records, out var message))
                            EditorUtility.DisplayDialog("Mesh Material Combiner", message, "OK");
                        else ShowNotification(new GUIContent(message));
                        InvalidateManagedBuildCache();
                        return true;
                    }
                }
            }
            return false;
        }

        private void DrawAvatarReductionPreview(string groupKey, IList<MMCBuildRecord> records)
        {
            var recordCount = 0;
            if (records != null)
                foreach (var record in records)
                    if (record != null) recordCount++;
            if (recordCount == 0)
                return;

            if (!_managedAvatarStatsExpanded.TryGetValue(groupKey, out var expanded))
                expanded = false;
            expanded = EditorGUILayout.Foldout(expanded,
                OptimizerLocalization.T("Avatar Reduction Preview") +
                " (" + string.Format(OptimizerLocalization.T("History Records Included"), recordCount) + ")",
                true);
            _managedAvatarStatsExpanded[groupKey] = expanded;
            if (!expanded) return;

            if (!_avatarReductionPreviewCache.TryGetValue(groupKey, out var preview))
            {
                preview = new AvatarReductionPreviewData
                {
                    Available = TryAggregateAvatarStatistics(records, out var before, out var after,
                        out var includedCount),
                    Before = before,
                    After = after,
                    RecordCount = includedCount
                };
                _avatarReductionPreviewCache[groupKey] = preview;
            }
            if (!preview.Available)
            {
                EditorGUILayout.HelpBox(OptimizerLocalization.T("Avatar Reduction Preview Unavailable"),
                    MessageType.Info);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(OptimizerLocalization.T("Avatar Reduction Preview"),
                    EditorStyles.boldLabel);
                DrawAvatarReductionLine(OptimizerLocalization.T("Renderer Count"),
                    preview.Before.RendererCount, preview.After.RendererCount);
                DrawAvatarReductionLine(OptimizerLocalization.T("Material Slots"),
                    preview.Before.MaterialSlots, preview.After.MaterialSlots);
                DrawAvatarReductionLine(OptimizerLocalization.T("Unique Materials"),
                    preview.Before.UniqueMaterials, preview.After.UniqueMaterials);
                DrawAvatarReductionLine(OptimizerLocalization.T("Vertices"),
                    preview.Before.Vertices, preview.After.Vertices);
                DrawAvatarReductionLine(OptimizerLocalization.T("Triangles"),
                    preview.Before.Triangles, preview.After.Triangles);
            }
        }

        private static bool TryAggregateAvatarStatistics(IList<MMCBuildRecord> records,
            out OptimizationStatistics before, out OptimizationStatistics after, out int count)
        {
            before = new OptimizationStatistics();
            after = new OptimizationStatistics();
            count = 0;
            if (records == null) return false;

            var outputs = new HashSet<GameObject>();
            foreach (var record in records)
            {
                if (record == null) continue;
                var output = MMCBuildDatabaseService.ResolveOutput(record);
                if (output == null) return false;
                outputs.Add(output);
            }

            var sourceRenderers = new List<RendererEntry>();
            var seenSourceRenderers = new HashSet<Renderer>();
            var intermediateOutputs = new HashSet<GameObject>();
            foreach (var record in records)
            {
                if (record == null || record.Recipe == null || !record.Recipe.IsValid ||
                    !MMCRebuildService.TryResolveEntries(record.Recipe, out var entries, out _))
                    return false;
                foreach (var entry in entries)
                {
                    if (entry == null || entry.Renderer == null) continue;
                    var sourceObject = entry.Renderer.gameObject;
                    if (outputs.Contains(sourceObject))
                    {
                        intermediateOutputs.Add(sourceObject);
                        continue;
                    }
                    if (seenSourceRenderers.Add(entry.Renderer)) sourceRenderers.Add(entry);
                }
                count++;
            }

            before = MeshAnalyzer.CalculateBefore(sourceRenderers, true);
            var seenOutputRenderers = new HashSet<Renderer>();
            var outputMaterials = new HashSet<Material>();
            foreach (var output in outputs)
            {
                if (intermediateOutputs.Contains(output)) continue;
                var renderer = output.GetComponent<Renderer>();
                if (renderer == null || !seenOutputRenderers.Add(renderer)) continue;
                var mesh = GetRendererMesh(renderer);
                if (mesh == null) return false;
                after.RendererCount++;
                after.MaterialSlots += renderer.sharedMaterials != null
                    ? renderer.sharedMaterials.Length
                    : 0;
                after.Vertices += mesh.vertexCount;
                after.Triangles += MeshAnalyzer.CountTriangles(mesh);
                if (renderer.sharedMaterials == null) continue;
                foreach (var material in renderer.sharedMaterials)
                    if (material != null) outputMaterials.Add(material);
            }
            after.UniqueMaterials = outputMaterials.Count;
            return count > 0 && (before.RendererCount > 0 || after.RendererCount > 0);
        }

        private static Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            var filter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
            return filter != null ? filter.sharedMesh : null;
        }

        private static void DrawAvatarReductionLine(string label, long before, long after)
        {
            var delta = after - before;
            EditorGUILayout.LabelField(label,
                $"{before:N0} → {after:N0}   ({delta:+#,0;-#,0;0})");
        }

        private static List<MMCBuildRecord> GetAvatarGroupRecords(MMCBuildDatabase database,
            string groupKey)
        {
            var result = new List<MMCBuildRecord>();
            if (database == null || database.Builds == null) return result;
            for (var i = 0; i < database.Builds.Count; i++)
            {
                var record = database.Builds[i];
                if (record != null && GetAvatarGroupKey(record, i) == groupKey) result.Add(record);
            }
            return result;
        }

        private static void DrawSelectOutputAction(GameObject output)
        {
            using (new EditorGUI.DisabledScope(output == null))
            {
                if (!GUILayout.Button(OptimizerLocalization.T("Select Output"),
                        EditorStyles.miniButton, GUILayout.MinWidth(100f), GUILayout.Height(19f))) return;
                Selection.activeGameObject = output;
                EditorGUIUtility.PingObject(output);
            }
        }

        private static void DrawSelectSourcesAction(IList<GameObject> sources)
        {
            using (new EditorGUI.DisabledScope(sources == null || sources.Count == 0))
            {
                if (!GUILayout.Button(OptimizerLocalization.T("Select Sources"),
                        EditorStyles.miniButton, GUILayout.MinWidth(100f), GUILayout.Height(19f))) return;
                Selection.objects = new List<GameObject>(sources).ToArray();
                EditorGUIUtility.PingObject(sources[0]);
            }
        }

        private bool DrawRebuildAction(MMCBuildRecord record, MMCRebuildEvaluation evaluation)
        {
            var canRebuild = evaluation != null &&
                             record.Lifecycle != MMCBuildLifecycle.CleanupPending &&
                             evaluation.State != MMCRebuildState.RecipeMissing &&
                             evaluation.State != MMCRebuildState.SourcesMissing &&
                             evaluation.State != MMCRebuildState.SceneNotLoaded &&
                             evaluation.State != MMCRebuildState.SourceConflict;
            using (new EditorGUI.DisabledScope(!canRebuild))
            {
                var label = OptimizerLocalization.T("Rebuild");
                if (!GUILayout.Button(label, EditorStyles.miniButton,
                        GUILayout.MinWidth(100f), GUILayout.Height(19f))) return false;
            }

            if (!EditorUtility.DisplayDialog(OptimizerLocalization.T("Rebuild"),
                    OptimizerLocalization.T("Rebuild Confirmation"),
                    OptimizerLocalization.T("Rebuild"), OptimizerLocalization.T("Cancel"))) return false;
            if (MMCRebuildService.Rebuild(record, out var message))
                ShowNotification(new GUIContent(message));
            else
                EditorUtility.DisplayDialog("Mesh Material Combiner", message, "OK");
            InvalidateManagedBuildCache();
            return true;
        }

        private bool DrawSuspendAction(MMCBuildRecord record, MMCBuildStatus status)
        {
            var canSuspend = record.Lifecycle == MMCBuildLifecycle.Built &&
                             status != MMCBuildStatus.OutputMissing &&
                             status != MMCBuildStatus.SourceMissing &&
                             status != MMCBuildStatus.SourceParentMissing &&
                             status != MMCBuildStatus.SceneNotLoaded;
            using (new EditorGUI.DisabledScope(!canSuspend))
            {
                if (!GUILayout.Button(OptimizerLocalization.T("Suspend For Editing"),
                        EditorStyles.miniButton, GUILayout.MinWidth(96f), GUILayout.Height(19f))) return false;
            }
            if (!EditorUtility.DisplayDialog(OptimizerLocalization.T("Suspend For Editing"),
                    OptimizerLocalization.T("Suspend For Editing Confirmation"),
                    OptimizerLocalization.T("Suspend For Editing"),
                    OptimizerLocalization.T("Cancel"))) return false;
            if (MMCBuildDatabaseService.SuspendForEditing(record, out var message))
                ShowNotification(new GUIContent(message));
            else
                EditorUtility.DisplayDialog("Mesh Material Combiner", message, "OK");
            InvalidateManagedBuildCache();
            return true;
        }

        private bool DrawFullyDetachAction(MMCBuildRecord record, MMCBuildStatus status)
        {
            var canDetach = status != MMCBuildStatus.SourceMissing &&
                            status != MMCBuildStatus.SourceParentMissing &&
                            status != MMCBuildStatus.SceneNotLoaded;
            using (new EditorGUI.DisabledScope(!canDetach))
            {
                var previousBackground = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1f, 0.55f, 0.5f);
                var clicked = GUILayout.Button(OptimizerLocalization.T("Fully Detach"),
                    EditorStyles.miniButton, GUILayout.MinWidth(96f), GUILayout.Height(19f));
                GUI.backgroundColor = previousBackground;
                if (!clicked) return false;
            }
            var generatedAssetCount = MMCBuildDatabaseService.CountExistingGeneratedAssets(record);
            var confirmation = string.Format(
                OptimizerLocalization.T("Fully Detach Confirmation"), generatedAssetCount);
            if (!EditorUtility.DisplayDialog(OptimizerLocalization.T("Fully Detach"), confirmation,
                    OptimizerLocalization.T("Fully Detach"),
                    OptimizerLocalization.T("Cancel"))) return false;
            if (MMCBuildDatabaseService.FullyDetach(record, out var message))
            {
                if (record.Lifecycle == MMCBuildLifecycle.CleanupPending)
                    EditorUtility.DisplayDialog("Mesh Material Combiner", message, "OK");
                else
                    ShowNotification(new GUIContent(message));
            }
            else
                EditorUtility.DisplayDialog("Mesh Material Combiner", message, "OK");
            InvalidateManagedBuildCache();
            return true;
        }

        private bool DrawRemoveUnrecoverableAction(MMCBuildRecord record, string key,
            MMCBuildStatus status)
        {
            if (!MMCBuildDatabaseService.CanRemoveUnrecoverableRecord(status)) return false;

            var previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.5f);
            var clicked = GUILayout.Button(OptimizerLocalization.T("Remove Unrecoverable Record"),
                EditorStyles.miniButton, GUILayout.MinWidth(135f), GUILayout.Height(19f));
            GUI.backgroundColor = previousBackground;
            if (!clicked) return false;

            if (!EditorUtility.DisplayDialog(OptimizerLocalization.T("Remove Unrecoverable Record"),
                    OptimizerLocalization.T("Remove Unrecoverable Record Confirmation"),
                    OptimizerLocalization.T("Remove Unrecoverable Record"),
                    OptimizerLocalization.T("Cancel"))) return false;

            if (!MMCBuildDatabaseService.RemoveUnrecoverableRecord(record, out var removeMessage))
                EditorUtility.DisplayDialog("Mesh Material Combiner", removeMessage, "OK");
            _managedBuildExpanded.Remove(key);
            InvalidateManagedBuildCache();
            return true;
        }

        private static void DrawStatusBadge(MMCBuildStatus status)
        {
            var content = new GUIContent(GetStatusLabel(status));
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = status == MMCBuildStatus.Valid
                    ? EditorStyles.boldLabel.fontSize
                    : EditorStyles.miniBoldLabel.fontSize,
                fixedHeight = 18f,
                padding = new RectOffset(4, 4, 1, 1)
            };
            var width = Mathf.Clamp(style.CalcSize(content).x + 8f, 92f, 180f);
            style.normal.textColor = GetStatusTextColor(status);
            GUILayout.Label(content, style, GUILayout.Width(width));
        }

        private static void DrawLifecycleBadge(MMCBuildLifecycle lifecycle)
        {
            if (lifecycle == MMCBuildLifecycle.Built) return;
            var label = lifecycle == MMCBuildLifecycle.SuspendedForEditing
                ? OptimizerLocalization.T("Lifecycle Suspended")
                : OptimizerLocalization.T("Lifecycle Cleanup Pending");
            var content = new GUIContent(label);
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 18f,
                padding = new RectOffset(4, 4, 1, 1)
            };
            style.normal.textColor = lifecycle == MMCBuildLifecycle.SuspendedForEditing
                ? new Color(0.3f, 0.7f, 0.95f)
                : new Color(0.95f, 0.35f, 0.3f);
            GUILayout.Label(content, style,
                GUILayout.Width(Mathf.Clamp(style.CalcSize(content).x + 8f, 92f, 180f)));
        }

        private static void DrawRebuildStatusBadge(MMCRebuildState state)
        {
            if (state == MMCRebuildState.UpToDate) return;
            string label;
            switch (state)
            {
                case MMCRebuildState.RecipeMissing:
                    label = OptimizerLocalization.T("Rebuild Info Missing");
                    break;
                case MMCRebuildState.SceneNotLoaded:
                    label = OptimizerLocalization.T("Status Scene Not Loaded");
                    break;
                case MMCRebuildState.SourceConflict:
                    label = OptimizerLocalization.T("Source Conflict");
                    break;
                default:
                    label = OptimizerLocalization.T("Cannot Rebuild");
                    break;
            }
            var content = new GUIContent(label);
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fixedHeight = 18f,
                padding = new RectOffset(4, 4, 1, 1)
            };
            var width = Mathf.Clamp(style.CalcSize(content).x + 8f, 88f, 160f);
            style.normal.textColor = state == MMCRebuildState.SceneNotLoaded
                ? new Color(0.95f, 0.65f, 0.2f)
                : state == MMCRebuildState.RecipeMissing
                    ? new Color(0.65f, 0.65f, 0.65f)
                    : new Color(0.95f, 0.35f, 0.3f);
            GUILayout.Label(content, style, GUILayout.Width(width));
        }

        private static Color GetStatusTextColor(MMCBuildStatus status)
        {
            switch (status)
            {
                case MMCBuildStatus.Valid:
                    return new Color(0.35f, 0.75f, 0.4f);
                case MMCBuildStatus.OutputDisabled:
                case MMCBuildStatus.OutputRendererDisabled:
                case MMCBuildStatus.SceneNotLoaded:
                case MMCBuildStatus.Modified:
                    return new Color(0.95f, 0.65f, 0.2f);
                default:
                    return new Color(0.95f, 0.35f, 0.3f);
            }
        }

        private MMCBuildResolvedState GetManagedBuildState(string key, MMCBuildRecord record)
        {
            if (_managedBuildCache.TryGetValue(key, out var state)) return state;
            state = MMCBuildDatabaseService.ResolveState(record);
            _managedBuildCache[key] = state;
            return state;
        }

        private MMCRebuildEvaluation GetRebuildEvaluation(string key, MMCBuildRecord record)
        {
            if (_rebuildEvaluationCache.TryGetValue(key, out var evaluation)) return evaluation;
            _sourceOwnershipConflictCache.TryGetValue(record, out var conflict);
            evaluation = MMCRebuildService.Evaluate(record, true, conflict);
            _rebuildEvaluationCache[key] = evaluation;
            return evaluation;
        }

        private void EnsureSourceOwnershipConflictCache(MMCBuildDatabase database)
        {
            if (database == null || _sourceOwnershipConflictCacheReady) return;
            foreach (var pair in MMCBuildDatabaseService.BuildSourceOwnershipConflictMap(database))
                _sourceOwnershipConflictCache[pair.Key] = pair.Value;
            _sourceOwnershipConflictCacheReady = true;
        }

        private int GetUnrecoverableRecordCount(MMCBuildDatabase database)
        {
            if (database == null || database.Builds == null) return 0;
            var count = 0;
            for (var i = 0; i < database.Builds.Count; i++)
            {
                var record = database.Builds[i];
                if (record == null) continue;
                var key = GetRecordKey(record, i);
                if (MMCBuildDatabaseService.CanRemoveUnrecoverableRecord(
                        GetManagedBuildState(key, record).Status)) count++;
            }
            return count;
        }

        private static string GetRecordKey(MMCBuildRecord record, int index)
        {
            return record != null && !string.IsNullOrEmpty(record.BuildId)
                ? record.BuildId
                : index.ToString();
        }

        private static string GetAvatarGroupKey(MMCBuildRecord record, int index)
        {
            var locator = record != null ? record.AvatarRoot : null;
            if (locator != null && !string.IsNullOrEmpty(locator.GlobalObjectId))
                return "global:" + locator.GlobalObjectId;
            if (locator != null && (!string.IsNullOrEmpty(locator.ScenePath) ||
                !string.IsNullOrEmpty(locator.HierarchyPath)))
                return "path:" + locator.ScenePath + "|" + locator.HierarchyPath;
            return "unknown:" + index;
        }

        private static string GetAvatarGroupName(MMCBuildRecord record)
        {
            if (record == null || record.AvatarRoot == null ||
                string.IsNullOrEmpty(record.AvatarRoot.DisplayName))
                return OptimizerLocalization.T("Unknown Avatar");
            return record.AvatarRoot.DisplayName;
        }

        private void InvalidateManagedBuildCache()
        {
            _managedBuildCacheDirty = false;
            _managedBuildCache.Clear();
            _rebuildEvaluationCache.Clear();
            _sourceOwnershipConflictCache.Clear();
            _sourceOwnershipConflictCacheReady = false;
            _avatarReductionPreviewCache.Clear();
            _historyRelatedCache.Clear();
            _managedBuildDatabase = null;
            Repaint();
        }

        private void QueueManagedBuildCacheInvalidation()
        {
            _managedBuildCacheDirty = true;

            // The merge screen does not consume history state. Defer all resolving
            // until the user actually opens the History tab.
            if (_mainView != MainView.History) return;

            // Inspector sliders and Prefab updates can publish many notifications in
            // consecutive frames. Wait until the burst settles, then refresh once.
            _managedBuildInvalidationDueTime = EditorApplication.timeSinceStartup +
                                               ManagedBuildInvalidationDelay;
            if (_managedBuildInvalidationQueued) return;
            _managedBuildInvalidationQueued = true;
            EditorApplication.update += FlushQueuedManagedBuildCacheInvalidation;
        }

        private void FlushQueuedManagedBuildCacheInvalidation()
        {
            if (this == null || !_managedBuildInvalidationQueued)
            {
                EditorApplication.update -= FlushQueuedManagedBuildCacheInvalidation;
                _managedBuildInvalidationQueued = false;
                return;
            }
            if (EditorApplication.timeSinceStartup < _managedBuildInvalidationDueTime) return;

            EditorApplication.update -= FlushQueuedManagedBuildCacheInvalidation;
            _managedBuildInvalidationQueued = false;
            if (_mainView == MainView.History && _managedBuildCacheDirty)
                InvalidateManagedBuildCache();
        }

        private void OnObjectChangesPublished(ref ObjectChangeEventStream stream)
        {
            for (var i = 0; i < stream.length; i++)
            {
                var eventType = stream.GetEventType(i);
                if (eventType == ObjectChangeKind.UpdatePrefabInstances)
                {
                    QueueManagedBuildCacheInvalidation();
                    return;
                }
                if (eventType != ObjectChangeKind.ChangeGameObjectOrComponentProperties) continue;

                stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var changeEvent);
                var changedObject = EditorUtility.InstanceIDToObject(changeEvent.instanceId);
                // History status only depends on GameObject active/tag state and the
                // output Renderer's enabled state. Material/Texture asset edits are
                // intentionally ignored here.
                if (changedObject is GameObject || changedObject is Renderer)
                {
                    QueueManagedBuildCacheInvalidation();
                    return;
                }
            }
        }

        private static string GetStatusLabel(MMCBuildStatus status)
        {
            switch (status)
            {
                case MMCBuildStatus.Valid: return OptimizerLocalization.T("Status Valid");
                case MMCBuildStatus.OutputDisabled: return OptimizerLocalization.T("Status Output Disabled");
                case MMCBuildStatus.OutputRendererDisabled: return OptimizerLocalization.T("Status Renderer Disabled");
                case MMCBuildStatus.OutputMissing: return OptimizerLocalization.T("Status Output Missing");
                case MMCBuildStatus.SourceMissing: return OptimizerLocalization.T("Status Source Missing");
                case MMCBuildStatus.SourceParentMissing: return OptimizerLocalization.T("Status Source Parent Missing");
                case MMCBuildStatus.SceneNotLoaded: return OptimizerLocalization.T("Status Scene Not Loaded");
                default: return OptimizerLocalization.T("Status Modified");
            }
        }

        private static GUIStyle GetStatusStyle(MMCBuildStatus status)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleRight };
            switch (status)
            {
                case MMCBuildStatus.Valid:
                    style.normal.textColor = new Color(0.35f, 0.75f, 0.4f);
                    break;
                case MMCBuildStatus.OutputDisabled:
                case MMCBuildStatus.OutputRendererDisabled:
                case MMCBuildStatus.SceneNotLoaded:
                case MMCBuildStatus.Modified:
                    style.normal.textColor = new Color(0.95f, 0.65f, 0.2f);
                    break;
                default:
                    style.normal.textColor = new Color(0.95f, 0.35f, 0.3f);
                    break;
            }
            return style;
        }

        private void DrawScenePreviewControls()
        {
            EditorGUILayout.Space(5f);
            EditorGUILayout.LabelField(OptimizerLocalization.T("Scene Preview"), EditorStyles.boldLabel);
            if (!_scenePreview.IsActive)
            {
                using (new EditorGUI.DisabledScope(_analysis == null || _analysis.Report.HasErrors))
                    if (GUILayout.Button(OptimizerLocalization.T("Create Scene Preview"), GUILayout.Height(24f))) CreateScenePreview();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(_scenePreview.Mode == ScenePreviewMode.Original, "Original",
                        EditorStyles.miniButtonLeft) && _scenePreview.Mode != ScenePreviewMode.Original)
                    _scenePreview.SetMode(ScenePreviewMode.Original);
                if (GUILayout.Toggle(_scenePreview.Mode == ScenePreviewMode.Optimized, "Optimized",
                        EditorStyles.miniButtonMid) && _scenePreview.Mode != ScenePreviewMode.Optimized)
                    _scenePreview.SetMode(ScenePreviewMode.Optimized);
                if (GUILayout.Toggle(_scenePreview.Mode == ScenePreviewMode.Both, "Both",
                        EditorStyles.miniButtonMid) && _scenePreview.Mode != ScenePreviewMode.Both)
                    _scenePreview.SetMode(ScenePreviewMode.Both);
                if (GUILayout.Button(OptimizerLocalization.T("Exit Preview"), EditorStyles.miniButtonRight)) ExitScenePreview();
            }
            EditorGUILayout.HelpBox("Preview assets are temporary and are not saved. Atlas preview is capped at 2048px.",
                MessageType.None);
        }

        private static void DrawStatistics(string title, OptimizationStatistics stats, OptimizationStatistics before)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.MinWidth(250f), GUILayout.ExpandWidth(true)))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                DrawStat("Renderer Count", stats.RendererCount, before?.RendererCount);
                DrawStat("Material Slots", stats.MaterialSlots, before?.MaterialSlots);
                DrawStat("Unique Materials", stats.UniqueMaterials, before?.UniqueMaterials);
                DrawStat("Vertices", stats.Vertices, before?.Vertices);
                DrawStat("Triangles", stats.Triangles, before?.Triangles);
            }
        }

        private static void DrawStat(string label, long value, long? before)
        {
            var delta = before.HasValue ? $"  ({value - before.Value:+#;-#;0})" : string.Empty;
            EditorGUILayout.LabelField(label, value.ToString("N0") + delta);
        }

        private static void DrawHistoryStatistics(OptimizationStatistics before, OptimizationStatistics after)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(OptimizerLocalization.T("Optimization Results") ?? "Optimization Results", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                DrawHistoryStatLine("Renderer Count", before.RendererCount, after.RendererCount);
                DrawHistoryStatLine("Material Slots", before.MaterialSlots, after.MaterialSlots);
                DrawHistoryStatLine("Unique Materials", before.UniqueMaterials, after.UniqueMaterials);
                DrawHistoryStatLine("Triangles", before.Triangles, after.Triangles);
                EditorGUI.indentLevel--;
            }
        }

        private static void DrawHistoryStatLine(string label, long before, long after)
        {
            var delta = after - before;
            EditorGUILayout.LabelField(label, $"{before:N0} \u2192 {after:N0}  ({delta:+#,0;-#,0;0})");
        }

        private void RefreshRenderers()
        {
            ExitScenePreview();
            // Target Rootモードでは、ユーザーが手動で変更したチェック状態を
            // Refresh後も保持する。Renderer参照をキーとして退避しておく。
            // Direct Objectsモードは初期OFF判定（activeInHierarchy && enabled）を
            // 常に適用するため、退避・復元は行わない。
            Dictionary<Renderer, bool> savedInclusion = null;
            if (!_directTargetMode && _entries.Count > 0)
            {
                savedInclusion = new Dictionary<Renderer, bool>(_entries.Count);
                foreach (var entry in _entries)
                    if (entry.Renderer != null) savedInclusion[entry.Renderer] = entry.Included;
            }
            _entries.Clear();
            _expanded.Clear();
            _hierarchyExpanded.Clear();
            var seen = new HashSet<Renderer>();
            if (_directTargetMode)
            {
                foreach (var target in _directTargets) AddCollectedRenderers(target, seen);
            }
            else
            {
                AddCollectedRenderers(_settings.TargetRoot, seen);
                // Refresh前のチェック状態を復元する
                if (savedInclusion != null)
                    foreach (var entry in _entries)
                        if (entry.Renderer != null && savedInclusion.TryGetValue(entry.Renderer, out var inc))
                            entry.Included = inc;
            }
            _analysis = null;
            Repaint();
        }

        private void AddDirectTarget(GameObject target)
        {
            // Objects already marked EditorOnly are source objects from a previous
            // merge and must not be re-added by multi-selection or drag-and-drop.
            if (target == null || IsEditorOnlyHierarchy(target) || _directTargets.Contains(target)) return;
            // A direct-object-only workflow still needs a local analysis root. Use the
            // first added object as that root; additional objects remain independently
            // collected below and are deduplicated by Renderer reference.
            if (_settings.TargetRoot == null) _settings.TargetRoot = target;
            ApplyAvatarDefaultNames(target);
            _directTargets.Add(target);
            _directTargetMode = true;
            RefreshRenderers();
        }

        private static bool IsEditorOnlyHierarchy(GameObject target)
        {
            if (target == null) return false;
            var current = target.transform;
            while (current != null)
            {
                if (current.gameObject.CompareTag("EditorOnly")) return true;
                current = current.parent;
            }
            return false;
        }

        private static string GetAvatarGroupName(GameObject target)
        {
            var avatarRoot = AvatarRootResolver.Resolve(target);
            return avatarRoot != null ? avatarRoot.name : "Avatar";
        }

        private void ApplyAvatarDefaultNames(GameObject target)
        {
            var avatarName = GetAvatarGroupName(target);
            _settings.GroupName = avatarName;
            _settings.MergedObjectName = avatarName + "_Merged";
        }

        private void AddCollectedRenderers(GameObject root, HashSet<Renderer> seen)
        {
            if (root == null) return;
            foreach (var entry in RendererCollector.Collect(root))
            {
                if (entry.Renderer == null || !seen.Add(entry.Renderer)) continue;
                // EditorOnly階層配下のRendererは除外する。
                // これにより、過去マージで生成されたEditorOnlyソースが
                // 再収集されて二重統合対象になることを防ぐ。
                if (IsEditorOnlyHierarchy(entry.Renderer.gameObject)) continue;
                // In Direct Objects mode, hidden or disabled sources start outside
                // the merge set. They remain source targets for hide/restore.
                entry.Included = entry.Renderer.gameObject.activeInHierarchy &&
                                 entry.Renderer.enabled;
                _entries.Add(entry);
            }
        }

        private void Analyze()
        {
            ExitScenePreview();
            EnsureMergedObjectName();
            _showAllValidationIssues = false;
            // Direct Objectsモードではチェック OFF のソースも非表示対象になるため、
            // _directTargetsをsourceTargetsとして渡してvalidationメッセージに反映する。
            var sourceTargets = _directTargetMode ? _directTargets : null;
            _analysis = OptimizationPipeline.Analyze(_settings, _entries, sourceTargets);
            Repaint();
        }

        private void CreateScenePreview()
        {
            try
            {
                _scenePreview.Build(_settings, _entries, _analysis);
                SceneView.RepaintAll();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Scene Preview", exception.Message, "OK");
            }
        }

        private void ExitScenePreview()
        {
            _scenePreview.Cleanup();
            SceneView.RepaintAll();
        }

        private void InvalidateAnalysis()
        {
            ExitScenePreview();
            _analysis = null;
        }

        private void ExecuteMerge()
        {
            try
            {
                ExitScenePreview();
                EnsureMergedObjectName();
                var sourceTargets = _directTargetMode ? _directTargets : null;
                OptimizationPipeline.Execute(_settings, _entries, sourceTargets, out _);
                var database = MMCBuildDatabaseService.Load();
                var latestBuildId = database != null && database.Builds.Count > 0
                    ? database.Builds[database.Builds.Count - 1].BuildId
                    : string.Empty;
                ResetAfterMerge();
                _mainView = MainView.History;
                _historyFilter = HistoryFilter.All;
                _historyScroll = Vector2.zero;
                _highlightedBuildId = latestBuildId;
                if (!string.IsNullOrEmpty(latestBuildId)) _managedBuildExpanded[latestBuildId] = false;
                ShowNotification(new GUIContent("Merge completed"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Mesh Material Combiner", exception.Message, "OK");
            }
        }

        private void EnsureMergedObjectName()
        {
            if (!string.IsNullOrWhiteSpace(_settings.MergedObjectName)) return;
            var avatarName = GetAvatarGroupName(_settings.TargetRoot);
            _settings.MergedObjectName = avatarName + "_Merged";
        }

        private void ResetAfterMerge()
        {
            ExitScenePreview();
            _entries.Clear();
            _directTargets.Clear();
            _expanded.Clear();
            _hierarchyExpanded.Clear();
            _pendingDirectTarget = null;
            _settings.TargetRoot = null;
            _settings.GroupName = "Avatar";
            _settings.MergedObjectName = string.Empty;
            _settings.ForceRepresentativeMaterial = null;
            _analysis = null;
            _showAllValidationIssues = false;
            _managedBuildCache.Clear();
            _managedBuildDatabase = null;
            _directTargetMode = true;
            _mergeScroll = Vector2.zero;
            _rendererScroll = Vector2.zero;
            Repaint();
        }

        private void ChooseOutputFolder()
        {
            var chosen = EditorUtility.OpenFolderPanel("Choose output folder under Assets", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(chosen)) return;
            var assets = System.IO.Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            var normalized = System.IO.Path.GetFullPath(chosen).Replace('\\', '/').TrimEnd('/');
            if (normalized != assets && !normalized.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid Output Folder", "Choose a folder under this project's Assets folder.", "OK");
                return;
            }
            _settings.OutputFolder = "Assets" + normalized.Substring(assets.Length);
            InvalidateAnalysis();
        }
    }
}
