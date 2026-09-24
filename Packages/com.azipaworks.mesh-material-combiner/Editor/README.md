# Mesh Material Combiner — Implementation Notes

ユーザー向けの操作方法と現在の制限事項は、ツール直下の
[`README.md`](../README.md)を参照してください。EditorWindow右上の`Readme`ボタンも同じREADMEをInspectorで開きます。

## Current behavior

- Unity 2022.3向けEditor専用ツールです。
- `SkinnedMeshRenderer`と`MeshRenderer + MeshFilter`を1つの`SkinnedMeshRenderer`へ統合します。
- 元のMesh、Material、Texture、Prefabアセットは直接変更しません。
- 生成先は`Assets/Azipa Works/MeshMaterialCombinerGenerated/outputs/mmc_generated`固定です。
- Scene上の統合結果は、対象から特定したアバタールート直下の`__MeshMaterialCombiner`へ配置します。
- Prefab Instanceのソースは移動せず、その場で非表示・`EditorOnly`化します。
- Unpack済みソースは、元の親ごとの`Combined+EditorOnly`へ移動し、そのコンテナを非表示・`EditorOnly`化します。
- 元Rendererの`enabled`値は変更しません。

## Optimization presets

- `Safe (Recommended)`: 異なるMaterialを統合せず、同じMaterialを参照する重複Slotだけを削減します。
- `Compatible Merge`: Shaderと非Atlas設定が互換のMaterialだけを複数Texture Atlasで統合します。
- `Force Single Slot (Beta)`: 対応入力を代表Materialの1 Slotへ強制統合します。見た目が変わる可能性があります。

旧`Aggressive`の列挙値は保存済み履歴との互換性のため内部に残しています。UIには表示せず、旧履歴の再ビルド時に`Compatible Merge`へ移行します。

## History and rebuild

履歴本体はEditor側の`MMCBuildDatabase`で管理します。Renderer選択とビルド設定を保存し、個別再ビルド、アバター単位の一時解除・アップロード準備、Revision切り替え、ソース状態の復元を行います。

MMCはVRChatビルド・アップロードへ介入しません。一時解除中や`CleanupPending`の状態確認と再統合は、履歴画面から手動で行います。

履歴状態は`History & Restore`タブで遅延解決します。Material・TextureアセットのProperty変更では履歴キャッシュを破棄せず、関連するScene変更だけをデバウンスして更新します。
