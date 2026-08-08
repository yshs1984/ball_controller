# ball_controller

Unity製のボール転がしゲーム。GitHub Pages上でUnity WebGLビルドを公開する。各スクリプトの詳細仕様・シーン構成・ビルド手順は [`docs/SPEC.md`](docs/SPEC.md) を参照。全体を読む必要はなく、作業内容に対応する章だけ読めばよい。

- Unityバージョン: **Unity 6 (6000.x LTS)**。正はリポジトリ内の `ProjectSettings/ProjectVersion.txt`
- 入力方式: **旧 Input Manager**(`Input.GetAxis`)を使う。新しい Input System パッケージは導入しない
- GitHubリポジトリは `yshs1984/ball_controller`。公開URLは `https://yshs1984.github.io/ball_controller/`

## コーディング規約

- スクリプトは `Assets/Scripts/` 配下にフラットに置く
- クラス名とファイル名は一致させる(Unityの制約)
- コメントは日本語で書く
- スクリプトはコンポーネント単位で単一責任に保つ(1スクリプト1機能)
- Inspectorで調整したい値は `[SerializeField] private` で公開する。`public` フィールドは使わない
- **入力の読み取りは `Update`、`Rigidbody` への力の適用は `FixedUpdate`** に分ける(物理演算を`Update`で回すとフレームレート依存になる)
- カメラの追従は `LateUpdate` に置く(`Update`だと1フレーム遅れて揺れる)
- タグ判定は `CompareTag` を使う(`==` による文字列比較はしない)
- 要求されていない機能を先回りして実装しない。追加案はIssueに起票するに留める

## WebGL / GitHub Pages 必須事項

- **圧縮形式は必ず `Disabled`(または `Gzip` + Decompression Fallback)にする。** GitHub Pagesは `Content-Encoding` ヘッダーを設定できないため、既定のBrotli圧縮のままだとブラウザ側でロードに失敗する

## Git管理上の注意

- `.meta` ファイルは必ずコミットする(Unityは全アセットの参照をGUIDで解決しているため)
- ビルド成果物(`Build/`)を `main` にコミットしない
- `ProjectSettings/` 配下を手作業で書き換えない。設定変更はUnity Editorの画面から行う(WebGL圧縮形式など、直接編集せざるを得ない場合は変更後に必ずEditorで開いて壊れていないか確認する)
