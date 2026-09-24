# VPAI自動インポーターの作成

anatawa12の **VPMPackageAutoInstaller（VPAI）** を使うと、UnityPackageをインポートするだけで、VPMリポジトリの登録とパッケージのインストールを自動化できます。

## Mesh Material Combiner用リンク

最新版の安定版（`1.0.0`以上）をインストールするリンクは次のとおりです。

```text
https://api.anatawa12.com/create-vpai/?name=MeshMaterialCombiner-{}-installer.unitypackage&repo=https%3A%2F%2Fazipappa.github.io%2Fazipa-tools-vpm%2Findex.json&package=com.azipaworks.mesh-material-combiner&version=%3E%3D1.0.0
```

URL内の `{}` は、VPAIが選択したパッケージバージョンに置き換えます。

## バージョン範囲の例

```text
1.2.x       # 1.2系の最新版
1.x         # 1系の最新版
>=1.0.0     # 1.0.0以上の最新版（メジャー更新も許可）
```

メジャーバージョンの互換性を保証できない間は、`1.x` または `1.2.x` のように範囲を絞ります。

## 内部で使われる設定

VPAIの `config.json` 相当の設定は次の内容です。

```json
{
  "vpmRepositories": [
    "https://azipappa.github.io/azipa-tools-vpm/index.json"
  ],
  "vpmDependencies": {
    "com.azipaworks.mesh-material-combiner": ">=1.0.0"
  },
  "includePrerelease": false,
  "silentIfInstalled": false,
  "minimumUnity": "2022.3"
}
```

## 利用条件と注意

- Listingの `index.json` が先に公開されている必要があります。
- VCCまたはVPM Resolverが導入済みのVRChatプロジェクトで使用します。
- `silentIfInstalled: false` のため、通常はインストール確認ダイアログが表示されます。
- 既に範囲内のバージョンがインストール済みの場合、必ず更新されるとは限りません。更新はVCC/VPMから行います。
- インポート時にEditorコードが実行されるため、信頼できる配布元のUnityPackageだけを使用します。

## 公式情報

- [VPMPackageAutoInstaller](https://github.com/anatawa12/VPMPackageAutoInstaller)
- [VPM Package Listing](https://vcc.docs.vrchat.com/guides/create-listing/)
