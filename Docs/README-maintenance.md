# README更新時の注意

このリポジトリには、利用者向けのREADMEが2箇所あります。

1. プロジェクト直下の `README.md`
2. VPMパッケージ内の `Packages/com.azipaworks.mesh-material-combiner/README.md`

内容を変更するときは、必ずこの2つのREADMEを同じ内容に更新してください。
VPMでインポートされたプロジェクトでは、アプリ内のReadmeボタンが次のパッケージ内READMEを参照します。

```text
Packages/com.azipaworks.mesh-material-combiner/README.md
```

`Packages/com.azipaworks.mesh-material-combiner/Editor/README.md` は実装メモ用であり、アプリ内のReadmeボタンの参照対象ではありません。
