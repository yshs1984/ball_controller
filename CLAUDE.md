# ball_controller

Unity製のボール転がしゲーム。WASDでボールを操作し、ゴールに到達するとクリアになる。GitHub Pages上でUnity WebGLビルドを公開する構成を取る。

- Unityバージョン: **Unity 6 (6000.x LTS)**。正はリポジトリ内の `ProjectSettings/ProjectVersion.txt` で、CIのビルドバージョンもこれに合わせる
- 入力方式: **旧 Input Manager**(`Input.GetAxis`)を使う。新しい Input System パッケージは導入しない
- GitHubリポジトリは `yshs1984/ball_controller`。公開URLは `https://yshs1984.github.io/ball_controller/` というサブパス配下になる

## プロジェクト構成／アーキテクチャ境界

- 標準的なUnityプロジェクト構成(`Assets/`, `Assets/Scenes/`, `Assets/Scripts/`, `ProjectSettings/` 等)を使う
- スクリプトは `Assets/Scripts/` 配下にフラットに置く。現状は数本程度のスクリプトしかないため、`Player/` `UI/` のようなサブフォルダでの過剰な分割はしない
- `BallController.cs`: プレイヤーが操作するボールにアタッチする。`Rigidbody` の `AddForce` で加速度ベースの移動を行う。WASD(および矢印キー)の入力をX-Z平面上の力に変換する
- `GoalTrigger.cs`: ゴールオブジェクトの `Collider`(`Is Trigger` = true)にアタッチする。`OnTriggerEnter` でボールとの接触を検知し `Debug.Log("Clear!")` を出力する
- カメラ追従スクリプト(`CameraFollow.cs` 等): メインカメラにアタッチし、`LateUpdate` でボールのTransformを追従する。オフセットと簡易的なスムージングのみを行う

### Unity固有の実装ルール

- **入力の読み取りは `Update`、`Rigidbody` への力の適用は `FixedUpdate`** に分ける。物理演算を `Update` で回すとフレームレート依存の挙動になるため、この分離は必ず守る
- カメラの追従は `LateUpdate` に置く。`Update` に置くと追従が1フレーム遅れてカメラが揺れる
- ボールの識別はタグ `Player` で行う(`other.CompareTag("Player")`)。`==` による文字列比較ではなく `CompareTag` を使う
- Inspectorで調整したい値(移動加速度、カメラのオフセット等)は `[SerializeField] private` なフィールドとして公開する。`public` フィールドは使わない

### シーン側の前提

スクリプトだけでは動作しない。以下はシーン上での設定が前提になっており、スクリプトはこれを満たしている前提で書いてよい。

- ボール: `Sphere` に `Rigidbody` とタグ `Player` を設定
- ゴール: `Collider` の `Is Trigger` を有効化
- 床: `Collider` を持つ静的オブジェクト

## 命名規約

- クラス名とファイル名は一致させる(Unityの制約)
- コメントは日本語で書く
- 要求されていない機能(ジャンプ、スコア表示、UI、効果音など)は先回りして追加しない。必要になった時点で別途相談する

## ビルド／デプロイ方針(GitHub Pages 向け WebGL)

- Player SettingsのビルドターゲットはWebGLを使用する
- **圧縮形式は必ず `Disabled`、または `Gzip` + Decompression Fallback 有効にする。** GitHub Pagesは静的ホスティングで `Content-Encoding` ヘッダーを設定できないため、Unityの既定であるBrotli圧縮のままビルドするとブラウザ側でロードに失敗する。WebGLビルドがPages上で真っ黒なまま止まる場合、まずここを疑う
- `.github/workflows/webgl-pages-deploy.yml` が `game-ci/unity-builder` でWebGLビルドを行い、`actions/deploy-pages` でGitHub Pagesにデプロイする(リポジトリのPages設定は `build_type: workflow` に切り替え済み)
- トリガーは `Assets/**` `ProjectSettings/**` `Packages/**` の変更時のみ。Unityプロジェクト本体がまだ存在しない現状では起動しない
- Unity Licenseの認証情報(`UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD` のGitHub Secrets)は登録済み
- **Unity Personal(無償版)のライセンス取得手順**: CI上で要求ファイルを生成する方式(`game-ci/unity-request-activation-file`)は廃止されている。代わりに、ローカルのUnity Hubで発行された `.ulf` ファイルをそのまま使う
  1. Unity Hub > Preferences > Licenses > 「Get a free personal license」でPersonalライセンスを取得
  2. OSごとの保存場所からファイルを取得: Windows `C:\ProgramData\Unity\Unity_lic.ulf` / Mac `/Library/Application Support/Unity/Unity_lic.ulf` / Linux `~/.local/share/unity3d/Unity/Unity_lic.ulf`
  3. その中身を `UNITY_LICENSE` に、Unityアカウントのメールアドレス・パスワードを `UNITY_EMAIL` / `UNITY_PASSWORD` に登録する(Personal版は `.ulf` だけでなくアカウント認証情報も併用する)
  4. Google等のSSOでアカウントを作りパスワードが無い場合は、[id.unity.com](https://id.unity.com) のログイン画面で「パスワードをお忘れですか?」からパスワードを設定する(SSOログインはそのまま使い続けられる)

### Git管理上の注意

- `.gitignore`(Unity標準、`Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/` 等を除外)を使用する
- **`.meta` ファイルは必ずコミットする。** Unityは全アセットの参照を `.meta` のGUIDで解決しているため、これを除外するとシーンとスクリプトの紐付けが壊れる
- **ビルド成果物(`Build/`)を `main` にコミットしない。** 公開物はCIが毎回ビルドし直して `gh-pages` 側に置く
- エディタ外(このリポジトリを直接編集するエージェント等)で `.cs` を新規作成した場合、対応する `.meta` はUnity Editorが次に開いたときに生成される。生成された `.meta` も忘れずコミットする

## 動作確認

自動テストは用意していない。変更後は以下を手動で確認する。

1. Unity EditorでPlayモードに入り、WASDでボールが加速・減速しながら転がること
2. カメラがボールを追従し、カクつきや振動がないこと
3. ボールをゴールに入れ、Consoleに `Clear!` が出力されること
4. WebGL関連の変更をした場合は、ビルド後にGitHub Pages上でも実際にロードできることを確認する(ローカルの `file://` 直開きではWebGLビルドは動作しないため、ローカル確認にはHTTPサーバー経由が必要)

## 禁止事項・注意点

- 要求されていない機能を先回りして実装しない
- スクリプトはコンポーネント単位で単一責任に保つ(1スクリプト1機能)
- `ProjectSettings/` 配下を手作業で書き換えない。設定変更はUnity Editorの画面から行う
