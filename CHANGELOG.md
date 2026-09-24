# Changelog

All notable changes to Mesh Material Combiner are documented in this file.

## [1.2.2] - 2026-09-24

### Added

- 統合履歴ごとにソースの所有範囲を記録し、同じソースまたはその親子階層が別の統合履歴と重複している場合に「ソース競合」として検出する機能を追加。
- 既存履歴の競合状態を統合履歴・管理画面に表示し、競合中の個別再統合・一時解除・一括操作を停止する安全対策を追加。競合解消のための「完全解除」は引き続き利用可能。

### Changed

- Atlasサイズの上限を4096に変更し、8192を選択できないように変更。
- 検証結果をError、Warning、Infoの順で表示するように変更。
- 一時解除中または`CleanupPending`のアバターに対するVRChatアップロード停止機能を削除。アップロード前の状態確認と一括統合は手動で行う方式に変更。

### Security

- 生成アセット削除時のパス検証を強化。正規化後の出力先確認、フォルダ除外、許可拡張子の制限、保存済みGUIDとの照合を追加。
- 新規統合と再統合の実行前にソース競合を再検証し、一時解除中などの管理対象ソースを別の統合へ取り込めないように変更。
- 履歴から解決したOutput、Source、OriginalParent、Recipe内Rendererが対象アバターと同じScene・Hierarchy内にあるかを操作前に検証する処理を追加。
- Outputが`AvatarRoot/__MeshMaterialCombiner`直下にない場合は、一時解除・完全解除・再統合・Revision復元を停止するように変更。
- 履歴内のSceneパスとGlobalObjectIdが示すScene GUIDを照合し、不一致時は対象種別、Sceneパス、Hierarchyパス、理由を表示して処理を停止するように変更。
- 旧`MMCSuspendedBuildGuard`の移行用削除処理を、ロード済みの通常Scene上にある同Component自身だけに限定。Prefab、Prefab Stage、永続アセットには影響せず、Undo可能な方式に変更。

### Verified

- 空の`__MeshMaterialCombiner`削除処理が、子オブジェクトなし、Transform以外のComponentなし、Prefabではないことを実行直前に再検証し、確認ダイアログとUndoを経て削除することを確認。

## [1.2.1] - 2026-09-23

### Changed

- 簡易的な文言修正


## [1.2.0] - 2026-09-23

### Added

- 履歴・復元画面のアバター単位グループに、アバター全体の統合前後の削減プレビューを追加。初期状態は折りたたみ、展開時にRenderer、Material Slot、Unique Material、Vertices、Trianglesの前後値と差分を確認できるように変更。

### Changed

- 履歴・復元画面の一部文言を修正。
- アバター単位で統合状況（「統合完了」または未統合件数）を表示し、ステータス文字サイズを調整。
- 一時解除・一括統合は引き続き手動操作のみとし、アップロード時の自動統合は行わない。

### Fixed

- 検索対象ルートオブジェクトが未指定でも関連履歴が集計されていた問題を修正。対象ルート未指定時は関連件数を0件として、一括再統合を実行できないように変更。

## [1.1.4] - 2026-09-23

### Added

- 履歴・Recipe・Revision・生成アセットと統合先GameObjectを保持する「編集のため一時解除」を追加。
- アバター単位の一時解除と、依存順に全履歴を再ビルドする「アップロード準備」を追加。
- `Built`、`SuspendedForEditing`、`CleanupPending`の明示的なライフサイクル状態を追加。
- 一時解除中または削除未完了のアバターに対するVRChatビルド停止処理を追加。自動統合は実行しない。

### Changed

- 一時解除後の再ビルドでは編集後のソース状態を新しい復元基準として保存し、同じ統合先GameObjectを再利用するよう変更。
- 完全解除では全Revisionの生成アセットを削除対象とし、削除失敗時は`CleanupPending`として履歴を保持するよう変更。
- アバター一括操作を全件事前検証・依存順処理・失敗時ロールバック方式へ変更。

## [1.1.3] - 2026-09-23

### Added

- 履歴の「元に戻す」実行時に、ソースの復元とあわせて、その統合履歴が生成したMesh、Material、Texture、Prefabを削除できる機能を追加。
- 復元確認画面に「復元して生成ファイルを削除（推奨）」「復元のみ」「キャンセル」の選択肢を追加。
- 現在のRevisionだけでなく、履歴に記録された過去Revisionの生成ファイルも削除対象として整理する処理を追加。
- 生成ファイル削除後または統合失敗時のロールバック後に、ファイルが残っていない空のビルド名フォルダを自動削除する処理を追加。

### Changed

- 生成ファイルの出力先を `outputs/AvatarOptimizerGenerated` から `outputs/mmc_generated` へ変更。
- 旧出力先の履歴データベースを新しい場所へ移行し、旧パスにある既存生成物も引き続き復元・削除できるよう互換性を維持。
- 他のMMC履歴または現在開いているSceneから参照されている生成アセットは、復元時の削除対象から除外するよう変更。
- 生成アセットの削除を一括処理し、大規模Projectでの復元時間を短縮。

### Notes

- 生成アセットを別のPrefabなどから手動で参照している場合、その参照は自動保護されません。
- AssetDatabase上のファイル削除はUnity Undoでは元に戻せないため、必要な生成物を残す場合は「復元のみ」を選択してください。

## [1.1.2] - 2026-09-23

### Added

- 統合履歴（History）画面にて、各ビルドの統合前・統合後（Before → After）のRenderer数、Material数、Triangles（ポリゴン数）などの最適化結果（削減量）を表示する機能を追加。
- Rendererを持たないGameObjectを追加した際、UI上に警告メッセージ（HelpBox）を表示し、マージを実行できない理由をわかりやすくする機能を追加。

### Changed

- プレビューの「Before」統計に、マージ対象ではないが非表示処理の対象となるオブジェクトも含めるように変更。これにより、事実上削減された分が「After」で減少する形になり、より直感的な削減量をプレビューで確認可能に。
- プレビューの統計ボックス（Before / After）がウィンドウ幅に合わせて横に広がるようレイアウトを改善。

### Fixed

- `Refresh Renderers` ボタン押下時、Target Rootモードにおいては手動で変更したRendererのチェック状態（Included）が維持されるように修正。
- Direct ObjectsモードからTarget Rootモードへ切り替えた際、または `Selected Reset` を実行した際に、内部でTargetRootが残留し誤ってアバタールートとして使われてしまう問題を修正。
- `AddCollectedRenderers` において、対象オブジェクトの子孫にEditorOnlyが設定されている場合、誤って収集・統合されてしまう問題を修正。
- Direct Objectsモードで追加したGameObjectが、その後別の操作によりEditorOnlyに変更された場合、リストから自動で除外されるように修正。
- 履歴の再ビルド（Rebuild）実行時、最新の最適化結果（削減量）が正しくデータベースに上書き保存・表示更新されるように修正。

## [1.1.1] - 2026-09-23

### Changed

- Direct Objectsの複数選択時、非表示または無効化されているRendererは初期状態で統合対象から除外されるように変更。
- Renderer一覧のチェック状態を、メッシュ統合の対象指定だけに使用するよう整理。
- Direct Objectsで追加されたGameObjectは、チェック状態にかかわらずソース管理・非表示・EditorOnly・履歴記録の対象として扱うように変更。
- チェックを外したソースも復元時に元のActive状態とTagへ戻せるように変更。
- すでにEditorOnlyタグが付いているGameObject、およびEditorOnly階層配下のGameObjectを複数選択・ドラッグ＆ドロップの追加対象から除外。

## [1.1.0] - 2026-09-23

### Added

- Beta Force Single Slot preset with explicit representative-Material selection, automatic
  supported-Material fallback, cross-Shader diagnostics, unsupported-texture warnings,
  Scene Preview support, and rebuild-recipe persistence.
- Base texture support for `_MainTex`, `_BaseMap`, `_BaseColorMap`, and
  `_BaseColorTexture` when building atlases.
- Multi-texture atlases for lilToon and common Shader properties, including Normal,
  Alpha, Emission, Shadow, MatCap/Rim masks, Metallic/Smoothness, Outline, and
  Occlusion textures when they can be sampled safely from UV0.
- Texture-type-aware atlas import settings, Normal Map conversion, per-property
  default tiles, Scale/Offset baking, shared atlas layouts, and edge padding for
  mipmap bleed reduction.
- Force Single Slot validation that requires triangle topology and a supported
  representative Material before allowing the merge.
- Aggregated Analyze diagnostics for the selected representative Material,
  cross-Shader replacement, incompatible Material settings, unsupported base
  textures, non-atlased texture properties, and high-memory 8192 atlases.
- Prefab-safe source handling for merged outputs. Prefab Instance hierarchies are
  preserved, while unpacked source objects can be hidden and marked `EditorOnly`
  without changing their Renderer enabled state.
- Editor-side build history and restore management, including merged-output/source
  tracking, avatar grouping, root-based history filtering, output/source selection,
  revision navigation, and restore operations.
- Individual rebuild support that regenerates assets and safely replaces the existing
  merged Renderer while retaining previous revisions.
- Bulk rebuild for every build record found by the current root search, processed in
  dependency order with failure reporting.
- Safer history UI with compact build cards, clearer recoverability status, rebuild
  and restore actions, synchronized merged-object names, and duplicate-safe output
  naming.
- Startup notification for unused empty `__MeshMaterialCombiner` objects across all
  loaded Scene Hierarchies, including inactive objects and untracked avatars.
- Dedicated cleanup window with Scene/Hierarchy paths, all candidates checked by
  default, per-object review, Prefab/Preview exclusions, confirmation, and Undo.
- Multilingual cleanup notifications and improved warning-panel layout with review
  and temporary-ignore actions.
- Removed the obsolete `MMCSource` component-based history implementation in favor
  of editor-managed build history.

### Changed

- The preset lineup is now `Safe (Recommended)`, `Compatible Merge`, and
  `Force Single Slot (Beta)`. The unstable Aggressive option has been removed
  from the UI.
- Safe is now the default preset for new merges. Existing Aggressive rebuild
  recipes are migrated to Compatible Merge without changing serialized enum values.
- Balanced has been renamed to Compatible Merge to describe its compatibility-based
  material grouping more clearly.
- Force Single Slot is now presented as a Beta feature in the preset selector and
  in Japanese, English, Chinese, and Korean descriptions.
- Force Single Slot rebuild recipes now retain the selected representative Material,
  so individual and bulk rebuilds use the same Shader and non-atlased settings.
- Base Color is baked into each base-texture atlas tile, and the generated Material's
  base color and texture scale/offset are normalized for atlas sampling.
- Compatible Merge compatibility now preserves non-atlased lilToon/Shader settings by
  separating Materials whose keywords, scalar settings, colors, unsupported textures,
  UV modes, or UV animation settings cannot be represented by the generated atlases.
- README documentation now explains how different base textures are arranged into
  an atlas, how UVs and SubMeshes are remapped, how each optimization preset behaves,
  which non-base textures are not currently atlased, and how history refresh,
  rebuild, restore, revisions, and generated-Material edits are handled.

### Fixed

- Editing generated Material properties no longer triggers a full build-history
  resolution pass. History cache refreshes are now limited to relevant Scene changes,
  deferred outside the History tab, and debounced during consecutive Editor events.
- Force Single Slot now chooses a supported Material as its actual first/template
  Material, preventing an unsupported first source Material from producing an atlas
  that the generated Material could not sample.
- Merged `SkinnedMeshRenderer.localBounds` are now calculated in the merged Root
  Bone's local space, preventing bounds offsets when source renderers use different
  Renderer transforms or Root Bones.

## [1.0.0] - 2026-09-21

### Added

- Non-destructive mesh merging for `SkinnedMeshRenderer` components.
- Conversion and merging of `MeshRenderer + MeshFilter` components into a skinned merged renderer.
- Bone deduplication, bone-index remapping, bindpose handling, and blend shape preservation.
- Blend shape collision modes:
  - Merge Same Name
  - Prefix By Renderer Name (Recommended)
  - Keep Separate
- Material compatibility grouping with Safe, Balanced, and Aggressive presets.
- Main texture atlas generation and UV remapping for supported materials.
- SubMesh and material-slot reduction.
- lilToon material adapter and generic `_MainTex` material support.
- Direct Object selection mode and Target Root hierarchy mode.
- Renderer list with per-renderer inclusion checkboxes, statistics, and material details.
- Analyze validation for missing assets, mesh readability, material compatibility, UVs,
  negative scale, animation references, bone references, and blend shape collisions.
- Aggregated bone diagnostics and responsive validation display.
- Scene Preview with Original, Optimized, and Both display modes.
- Automatic `Combined+EditorOnly` source containers for selected source objects.
- Generated output under:

  `Assets/Azipa Works/tools/MeshMaterialCombiner/outputs/AvatarOptimizerGenerated/`

- Output hierarchy based on avatar root, build name, and timestamp:

  `AvatarRoot/BuildName_yyyyMMdd_HHmmss_fff/`

- Custom `Merged Object Name` setting for generated GameObjects, Meshes, and Prefabs.
- README button, multilingual UI support, and localized README sections.
- Undo support and progress reporting during merge operations.
- Related-history filtering by dragged avatar or clothing root objects.
- Rebuild recipes containing the selected Renderers, preset, atlas size, and blend shape mode.
- Source dependency fingerprints and Prefab/asset update detection.
- Safe per-output rebuild that keeps the existing merged GameObject and swaps its generated data only after validation succeeds.
- Dependency-ordered bulk rebuild for changed outputs.
- Generated-asset revisions with previous/next revision switching.

### Notes

- Original GameObjects, Renderers, Meshes, Materials, Textures, Prefabs, Animator,
  Avatar Descriptor, and VRChat components are not directly modified.
- Safe mode preserves different Materials and only reduces duplicate references where possible.
- Aggressive mode may use representative material properties and can change appearance.
