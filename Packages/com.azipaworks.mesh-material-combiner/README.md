# Mesh Material Combiner

<p align="center">
  <img src="Editor/mesh_material_combiner_icon.png" width="128" alt="Mesh Material Combiner icon">
</p>

<p align="center">
  <strong>VRChatアバター向け メッシュ・マテリアル最適化ツール</strong><br>
  元データを削除せずにメッシュ統合、マテリアル統合、Texture Atlas生成を行います。
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-2022.3-222c37?logo=unity&logoColor=white" alt="Unity 2022.3">
  <img src="https://img.shields.io/badge/VRChat%20SDK-Avatars-2d9bf0" alt="VRChat SDK Avatars">
  <img src="https://img.shields.io/badge/VPM-1.2.2-6f42c1" alt="VPM 1.2.2">
  <img src="https://img.shields.io/badge/License-MIT-green" alt="MIT License">
</p>

## 概要

Mesh Material Combinerは、VRChatアバターのRendererをまとめ、ドローコールとマテリアル数の削減を支援するUnity Editor拡張です。元のMesh・Material・Textureを直接変更せず、生成物を履歴付きで管理できます。

### 主な機能

| 機能 | 内容 |
| --- | --- |
| メッシュ統合 | `SkinnedMeshRenderer` と `MeshRenderer + MeshFilter` に対応 |
| マテリアル統合 | Safe / Compatible Merge / Force Single Slot |
| Texture Atlas | Main Texture、Normal、Emission、Maskなどに対応 |
| BlendShape | 同名BlendShapeの衝突方針を選択可能 |
| Preview & Validation | 統合前に結果と警告を確認 |
| History & Restore | 生成履歴の確認と復元 |

### 必要環境

- Unity `2022.3`
- VRChat SDK - Avatars `3.10.x` 以降

VCC / VPMから導入する場合は、パッケージID `com.azipaworks.mesh-material-combiner` を使用します。

### GitHub Release

GitHub Actionsの `Build Release` を実行すると、`package.json` のバージョンを使ってVPM用ZIP、UnityPackage、GitHub Releaseを自動生成します。
GitHubの自動リリースノートも有効になっているため、次回以降のReleaseには変更内容（What's Changed）とContributorsが表示されます。
`main` ブランチへ `package.json` の変更をPushした場合も、自動的にReleaseビルドが開始されます。

初回のみ、リポジトリの **Settings > Secrets and variables > Actions > Variables** に次のRepository variableを追加してください。

```text
PACKAGE_NAME=com.azipaworks.mesh-material-combiner
```

その後、GitHubの **Actions > Build Release > Run workflow** から実行します。

通常は `package.json` の `version` を上げてPushするだけで、自動ビルドを開始できます。

## 起動

Unityメニューから次を選択します。

`Tools > Azipa Tools > Mesh Material Combiner`

ウィンドウ右上の言語メニューから、日本語・English・中文・한국어を切り替えられます。`Readme`ボタンを押すと、このREADMEがInspectorで開きます。

## 基本的な使い方

1. `New Merge`タブを開きます。
2. 対象の選び方を選択します。
   - `Direct Objects`: GameObjectを個別に選択、または入力欄へドラッグします。
   - `Target Root`: アバターや衣装のルートを指定し、配下のRendererを検出します。
3. Renderer一覧で統合したい項目だけチェックします。初期状態はすべてOFFです。
4. 必要に応じて`Optimization Preset`、`BlendShape Collision`、`Merged Object Name`を設定します。
5. `Analyze`を押し、PreviewとValidationを確認します。
6. Errorがないことを確認して`Merge Meshes`を押します。

`Direct Objects`では、Hierarchyで複数選択した状態で`Add Selected`を押すと一括追加できます。`Selected Reset`で直接選択したオブジェクトをリセットできます。

## Renderer選択

検出対象は次のRendererです。

- `SkinnedMeshRenderer`
- `MeshRenderer + MeshFilter`

一覧にはGameObject名、Renderer種別、Mesh名、頂点数、Triangle数、Material Slot数、使用Material、Shader名が表示されます。チェックされたRendererだけがAnalyze、Preview、Mergeの対象です。

一覧上部の`All ON` / `All OFF`で対象を一括切り替えできます。

## Optimization Preset

### Safe（推奨）

- 異なるMaterialは統合しません。
- 同じMaterialを参照する重複Material Slotだけを削減します。
- 元のUVとMaterial設定を優先して保持します。
- Material Atlasは作成しません。

最も安全性が高い初期設定です。まずはこのモードを使用してください。

### Compatible Merge

- Shaderが同じで、描画設定・Shader Keyword・Atlas化されないMaterial設定が互換のMaterialを統合します。
- Main Textureに加えて、対応するNormal、Emission、Shadow、MaskなどのAtlasを同じ配置で作成し、UVを再配置します。
- 安全に統合できないMaterialは別グループとして保持します。

見た目を維持できる互換条件を満たしたMaterialだけをAtlas統合したい場合に使用します。SafeよりMaterial Slotを削減できますが、AnalyzeとScene Previewで結果を確認してください。

### Force Single Slot（Beta）

このモードはBeta機能です。統合後の表示をScene Previewと実機環境で確認してから使用してください。

- 選択したすべてのMaterialを、1つのMaterial Slotへ強制的に統合します。
- `Representative Material`のShaderと、Atlas化されないMaterial設定を統合結果に使用します。
- `Representative Material`が未指定の場合は、Base Textureを扱えるMaterialを自動選択します。
- Main Texture、Normal、Emission、Shadow、各種Maskなど、代表Shaderが対応するTextureを同じ配置のAtlasへまとめます。
- MatCap本体や非UV0・UVアニメーションを使用するTextureなど、Atlas化できない項目には代表Materialの設定を使用します。
- 異なるShaderや描画設定は代表Materialの設定へ置き換わるため、見た目が大きく変わる可能性があります。
- Triangles以外のMesh topologyが含まれる場合は実行できません。

Analyze結果に表示される代表Material、置き換え対象のShader、未対応Texture Propertyの警告を確認し、Scene Previewで見た目を確認してから使用してください。

## Material統合とTexture Atlas

使用しているMain Textureが異なるMaterialを統合する場合、それぞれのTextureを1枚のTexture Atlasへ並べます。MeshのUVも、元のMaterialに対応するAtlas領域へ自動的に再配置します。

例：

```text
統合前
Material A → Texture A
Material B → Texture B
Material C → Texture C

統合後
Merged Material → Atlas Texture

┌───────────┬───────────┐
│ Texture A │ Texture B │
├───────────┼───────────┤
│ Texture C │   Empty   │
└───────────┴───────────┘
```

- Material Aを使用していた面はTexture Aの領域へ移動します。
- Material Bを使用していた面はTexture Bの領域へ移動します。
- Material Cを使用していた面はTexture Cの領域へ移動します。
- 同じ統合MaterialになったSubMeshは、実際に1つのSubMeshへまとめられます。

プリセットごとの扱いは次のとおりです。

- `Safe`: 異なるMaterialは統合せず、同じMaterialを参照する重複Slotだけを削減します。
- `Compatible Merge`: ShaderとAtlas化されない設定が互換なら、対応Textureが異なっていても複数Atlasを生成して統合します。
- `Force Single Slot（Beta）`: Shaderが異なっていても、代表Materialを使用して1 Material Slotへ強制統合します。

Base Textureとして対応するPropertyは次のとおりです。

- `_MainTex`
- `_BaseMap`
- `_BaseColorMap`
- `_BaseColorTexture`

主な追加Atlas対象は次のとおりです。実際にTextureが設定されているPropertyだけ生成されます。

- Normal: `_BumpMap`、`_Bump2ndMap`
- Alpha・Layer Mask: `_AlphaMask`、`_Main2ndBlendMask`、`_Main3rdBlendMask`
- Emission: `_EmissionMap`、`_EmissionBlendMask`、`_Emission2ndMap`、`_Emission2ndBlendMask`
- Shadow: `_ShadowColorTex`、`_Shadow2ndColorTex`、`_Shadow3rdColorTex`、`_ShadowStrengthMask`
- MatCap Mask: `_MatCapBlendMask`、`_MatCap2ndBlendMask`
- Rim: `_RimColorTex`
- Metallic・Smoothness: `_MetallicGlossMap`、`_SmoothnessTex`
- Outline: `_OutlineTex`、`_OutlineWidthMask`
- Generic Shader: `_OcclusionMap`を含む一般的なProperty

NormalとMaskはLinear Textureとして保存し、Normal Mapには専用の変換とImporter設定を使用します。TextureごとのScale／OffsetはAtlas画像側へ焼き込み、Atlas境界にはMipMapの色移りを抑えるPaddingを追加します。

MatCap本体、画面・視線依存Texture、UV1～UV3、スクロール・回転などUVアニメーションを使用するTextureは単純なAtlas化の対象外です。Compatible Mergeでは再現できない設定差があるMaterialを別グループに分けます。Force Single Slotでは代表Materialの設定へ置き換わる可能性があるため、Analyzeの警告とScene Previewを確認してください。

## BlendShape Collision

同名BlendShapeが複数Meshにある場合の扱いを選択できます。

- `Merge Same Name`: 同名BlendShapeを統合します。
- `Prefix By Renderer Name（Recommended）`: Renderer名を付けて名前を分けます。
- `Keep Separate`: 同名でも分けて保持します。

## Merged Object Name

統合先GameObjectの名前を指定します。Target RootまたはDirect Objectsを設定すると、初期値として`<AvatarName>_Merged`が設定されます。

この名前は、Scene上の統合先GameObjectと生成Prefab内の統合先GameObjectに使用されます。同名オブジェクトが存在する場合は、既存オブジェクトを上書きせず連番名が付けられます。

## 元データを削除しない処理とPrefab

元のMesh・Material・Texture・Prefabアセットは直接変更しません。統合対象のScene上のソースGameObjectについては、統合後の二重表示を防ぐため、非表示化や`EditorOnly`タグ設定を行う場合があります。

- 元のMesh、Material、Texture
- 元のPrefabアセット
- Avatar Descriptor、Animator、PhysBone、Constraintなどのコンポーネント

生成物は次の場所へ新規作成されます。

```text
Assets/Azipa Works/MeshMaterialCombinerGenerated/outputs/mmc_generated/
└ AvatarRoot/
   └ BuildName_yyyyMMdd_HHmmss_fff/
      ├ Meshes/
      ├ Materials/
      ├ Textures/
      └ Prefabs/
```

Prefab Instanceのソースは階層を移動せず、対象GameObjectを非表示にして`EditorOnly`タグを付けます。Unpack済みのソースは、元の親ごとに`Combined+EditorOnly`を作成してまとめ、その親を非表示・`EditorOnly`にします。Rendererの`enabled`状態は変更しません。

生成された統合オブジェクトはアバタールート直下の`__MeshMaterialCombiner`に配置されます。

## Preview

`Scene Preview`では保存前の結果を確認できます。

- `Original`: 元のRendererを表示
- `Optimized`: 統合結果を表示
- `Both`: 元と統合結果を同時に表示

Preview用の一時オブジェクトとアセットは、Preview終了時に削除されます。

## 履歴・復元

`History & Restore`タブでは、統合先とソースの関係をEditor管理の履歴として確認できます。履歴はGameObjectへ専用コンポーネントを追加せず、Editor側のデータベースで管理します。

主な機能は次のとおりです。

- アバター別の履歴表示
- 対象ルートを指定した関連履歴検索
- 統合先GameObjectの選択
- ソースオブジェクトの選択
- 編集のため一時解除
- 統合を完全解除
- アバター単位の一時一括解除（改変時）と一括統合（アップロード前）
- 個別の再統合
- 検索結果に含まれる関連履歴の一括再統合
- アバター全体の削減プレビュー（折りたたみ表示）
- Revisionの切り替え
- 復元できない追跡記録の整理

履歴には、統合対象Renderer、Optimization Preset、Atlas解像度、BlendShape設定、Representative Material、出力名が保存されます。再統合では現在のソースから新しいMesh、Material、Texture、Prefabを生成し、既存の統合先Rendererへ差し替えます。過去のRevisionは保持され、必要に応じて切り替えられます。

履歴ごとに次の2種類の解除方法を使用できます。

- `編集のため一時解除`：ソースの親、Sibling位置、表示状態、タグを復元します。統合先GameObjectは同一性を維持するため削除せず、非アクティブ化します。履歴、Recipe、Revision、生成アセットは保持されます。
- `統合を完全解除`：ソースを復元し、統合先GameObjectを削除します。現在および過去Revisionの生成アセットを整理し、正常終了後に履歴を削除します。

一時解除後に再統合すると、編集後のソース状態を次回の復元基準として更新し、新しいMesh、Material、Texture、Prefabを生成します。成功後はソースを再び非表示・`EditorOnly`化し、保持していた同じ統合先GameObjectを再利用します。失敗時は一時解除状態を維持します。

アバター見出しの`一時一括解除（改変時）`では、そのアバターの履歴を依存関係の逆順でまとめて解除します。`一括統合（アップロード前）`では依存順に全履歴を再統合します。全件の事前検証後に処理し、途中で失敗した場合は一括操作全体をロールバックします。

VRChat SDKのビルド・アップロードはMMCから停止しません。アップロード前に`統合履歴・管理`で状態を確認し、必要に応じて`一括統合（アップロード前）`を明示的に実行してください。自動再統合は行われません。

他のMMC履歴または現在開いているSceneから参照されている生成アセットは削除せず保持します。ただし、生成アセットを別のPrefabなどから手動で参照している場合、その参照は自動保護されません。生成ファイルの削除はUnity Undoでは元に戻せないため、必要な生成物がある場合は事前にバックアップしてください。削除に失敗した履歴は`CleanupPending`として保持され、`統合を完全解除`を再実行できます。

削除後に `mmc_generated/AvatarRoot/BuildName_yyyyMMdd_HHmmss_fff/` 配下へファイルが残っていない場合は、空のビルド名フォルダも削除されます。アバター名フォルダ自体は保持されます。

アバターや衣装のPrefabを更新した場合は、対象ルートで履歴を検索し、個別の`再ビルド`または検索結果の一括再ビルドを実行してください。一括再ビルドは「変更あり」と判定された項目だけではなく、検索結果に含まれる履歴を依存順にすべて処理します。

### 履歴表示の更新

Editorの操作を重くしないため、統合履歴の状態解決は主に`History & Restore`タブを表示したときに行います。

- `New Merge`タブでは、ナビゲーション表示のための全履歴走査を行いません。
- GameObjectの表示・タグ、Rendererの有効状態、Hierarchy、Prefab Instanceなど、復元状態に関係するScene変更だけを監視します。
- 連続する変更通知は短時間まとめ、履歴キャッシュを1回だけ更新します。
- MaterialやTextureのProperty編集だけでは、履歴全件を再走査しません。
- 必要な場合は履歴画面の`Refresh`で明示的に状態を更新できます。

## 使用していない統合オブジェクトの掃除

Mesh Material Combinerを開いたとき、ロード済みSceneのHierarchy全体から空の`__MeshMaterialCombiner`を検索します。履歴で追跡されていないアバターも検索対象です。

候補が見つかると、ヘッダー下部に通知が表示されます。`確認する`を押すと専用画面が開き、削除対象を確認できます。

- 候補は初期状態ですべてチェックされています。
- 不要な項目だけチェックを外せます。
- Scene名と完全なHierarchyパスを確認できます。
- `チェックしたオブジェクトを削除`で削除します。
- 削除はUndoに対応します。
- Prefab Instance、Prefab編集ステージ、Preview Sceneは対象外です。
- `今回は無視`は、現在のウィンドウを開いている間だけ有効です。ウィンドウを開き直すと再検索されます。

## Validation

`Analyze`では次の問題を確認します。

- Missing Mesh / Material
- Mesh Read/Write無効
- ShaderやMaterial設定の非互換
- UV範囲、Negative Scale、Normal/Tangent
- BlendShape名の衝突
- Bone参照やBone数
- AnimationからのRenderer、Material、BlendShape参照

Errorがある場合はMergeを実行できません。Warningは内容を確認してから判断してください。

## 制限事項

- `Compatible Merge`と`Force Single Slot`は、対応Propertyごとに同じ解像度のAtlasを生成します。使用Textureが多いMaterialでは生成アセット数とメモリ使用量が増えます。
- Atlas配置は現在Grid方式です。
- MatCap本体、画面・視線依存Texture、UV1～UV3、UVアニメーションなど、UV0へ安全に焼き込めないTextureはAtlas化しません。
- lilToon以外のShaderは、対応している一般的なTexture Propertyの範囲で処理します。
- `Force Single Slot`はBeta機能です。Shaderや非Atlas設定を代表Materialへ統一するため、見た目が変わる可能性があります。
- Triangles以外のTopologyは`Force Single Slot`で統合できません。
- Animation参照の自動リターゲットは行いません。参照がある場合はValidationのWarningを確認してください。
- Prefabやソースアセットの更新後、統合結果は自動再生成されません。履歴画面から再ビルドしてください。
- 生成済みMaterialを手動編集しても、保存済みの再ビルド設定や元Materialには反映されません。再ビルドすると、新しく生成されたMaterialへ差し替わります。
- 生成アセットは通常の操作では自動削除されません。履歴から元に戻す際に、削除するか保持するかを選択できます。

## Undo

Merge、ソース処理、履歴からの復元、統合オブジェクトの掃除は可能な範囲でUnity Undoに対応しています。ただし、復元時に削除したProject内の生成ファイルはUnity Undoで元に戻せません。重要なPrefabやScene、生成ファイルは操作前にバックアップを作成してください。
