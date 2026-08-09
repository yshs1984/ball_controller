# ball_controller 詳細スペック

開発者・Claude Code向けの実装詳細集。`CLAUDE.md`には変わらない原則だけを置き、機能追加のたびに変わる詳細はここにまとめる。各章が独立して読めるよう構成しているので、作業内容に対応する章だけ読めばよい。

## 目次
1. [スクリプト仕様](#1-スクリプト仕様)
2. [シーン構成](#2-シーン構成)
3. [ビルド／デプロイ(GitHub Pages 向け WebGL)](#3-ビルドデプロイgithub-pages-向け-webgl)
4. [Unity Personal(無償版)ライセンスの取得手順](#4-unity-personal無償版ライセンスの取得手順)
5. [動作確認手順](#5-動作確認手順)
6. [Unity MCPサーバーの接続(WSL環境向け)](#6-unity-mcpサーバーの接続wsl環境向け)

---

## 1. スクリプト仕様

すべて `Assets/Scripts/` 配下にフラットに配置。

### `BallController.cs`
プレイヤーが操作するボールにアタッチする。`Rigidbody` の `AddForce` で加速度ベースの移動を行う。

- `Update()`でWASD(および矢印キー、`Input.GetAxis`)の入力を読み取る
- 加えて画面のタップ&ドラッグ(`Input.GetMouseButton`系)にも対応し、キーボード入力と合算する。WebGLではタッチがマウスイベントとしてエミュレートされるため、同じコードでPC/スマホ両対応になる
- `FixedUpdate()`で`Rigidbody.AddForce`によりX-Z平面上の力に変換する
- 端末の傾き(ジャイロ)操作は未対応(WebGLでの実装コストが高いため見送り。Issue化して先送りしている)

### `GoalTrigger.cs`
ゴールオブジェクトの`Collider`(`Is Trigger` = true)にアタッチする。

- `OnTriggerEnter`でボール(タグ`Player`)との接触を検知し`Debug.Log("Clear!")`を出力する
- `gameManager`(Inspectorでアサイン)の`OnGoalReached()`を呼び、ステージ進行を通知する

### `CameraFollow.cs`
メインカメラにアタッチする。

- `LateUpdate()`で`target`(ボールの`Transform`)を追従する
- `offset`(既定 `(0, 9, -7)`。迷路全体が見渡しやすい高めのアングル)と`smoothSpeed`による簡易的なスムージングのみを行う

### `MazeGenerator.cs`
シーン内の空のGameObjectにアタッチする。穴掘り法(recursive backtracker)で迷路を生成する。

- `public void Generate()`: 迷路生成の一連処理(`ClearWalls()` → `GenerateMaze()` → `BuildWalls()` → `PlaceFloor()` → `PlaceBallAndGoal()`)。`Start()`から初回呼び出しされる
- `public void GenerateNewStage(int stage)`: 「本来の(正方形に近い)進行」を表す`progressWidth`/`progressHeight`をそれぞれ+1(上限12で頭打ち)する。通常ステージは`width`/`height`にこの値をそのまま使う。`stage`が`NarrowStageInterval`(既定3)の倍数のときは「細道チャレンジ」として、短辺を`NarrowStageShortSide`(既定3)に固定し、長辺を`progressWidth + progressHeight`(上限12)にすることで**迷路全体を細長い形**にし、Consoleに`Stage X: 細道チャレンジ!`を出力する。`cellSize`(通路幅)自体は変更しない。`progressWidth`/`progressHeight`は細道チャレンジを挟んでも常に伸び続けるため、次の通常ステージのサイズ感は不自然にならない
- `public void ResetBall()`: 迷路は再生成せず`PlaceBallAndGoal()`だけ呼ぶ(落下リトライ用)
- 壁は`Assets/Materials/WallMaterial.mat`を使う。`BuildWalls()`は列/行の境界線を基準にループし(セル単位だと内部の壁が隣接する2セルの両側から重複してしまうため)、壁ごとに個別のGameObjectは作らず`CombineInstance`でまとめて**1つのメッシュに結合**する。結合結果は`Walls`という1つのGameObjectにまとめ、`MeshFilter`/`MeshRenderer`(描画)と`MeshCollider`(当たり判定、壁は動かないので非convexのまま)を1組だけ持たせる。壁の数が増えてもドローコール・GameObject数が増えないようにするための最適化(Issue #13)
- **迷路外周に面する壁は生成しない**(ボールが端まで転がると床の外に落下できる仕様)
- `ballStartHeight`を`Start()`の最初(まだ一度も落下していない時点)で一度だけ記録し、以後のボール再配置は常にこの値を使う(落下でY座標がマイナスになった状態から再計算すると正しい高さに戻らないバグの対策)
- ボールの再配置は**`Rigidbody.position`に代入する**(`Transform.position`ではない)。Ballの`Rigidbody`は`Interpolate`が有効なため、`Transform.position`へ直接代入しても次フレームの補間処理で物理エンジン側の古い位置に巻き戻されてしまう(Issue #19の原因)。`Rigidbody.position`への代入は物理エンジンの記録ごとテレポートさせる正しい方法
- 迷路の大きさ(`width` / `height` / `cellSize` 等)は`[SerializeField]`でInspector調整可能
- **Editor上の非再生時のシーンビューでは迷路は見えない**(`Start()`での実行時生成のため)。Play時に毎回ランダムな迷路になる

### `GameManager.cs`
シーン内の空のGameObjectにアタッチする。ステージ進行とクリアタイム計測を管理する。

- `mazeGenerator` / `uiManager`への参照(Inspectorでアサイン)を持つ
- ステージ数(`currentStage`、1始まり)とステージ開始時刻(`Time.time`)を保持
- `public void OnGoalReached()`(`GoalTrigger`から呼ばれる): クリアタイムを`Stage X Clear! Time: Y.YYs`として`Debug.Log`に出力し、ステージ数を進めてから`uiManager.ShowClear()`を呼ぶ。その後`clearToNextStageDelay`(既定2秒。`UIManager`の`clearDisplayDuration`と揃えてある)待ってから`mazeGenerator.GenerateNewStage()`と次ステージ開始を行う(`AdvanceStageAfterClear()`コルーチン)。`ShowClear()`と`ShowStageAnnouncement()`を同じフレームで連続実行すると、後者が前者の表示を即座に打ち消してしまうため、間隔を空けている
- `public void OnBallFell()`(`FallDetector`から呼ばれる): `Fall! Retry Stage X`をログ出力し、`mazeGenerator.ResetBall()`で**同じ迷路のまま**ボールを戻す(ステージは進めない)
- `StartStage()`(ステージ開始時、内部から呼ばれる)で`uiManager.ShowStageAnnouncement(currentStage)`を呼ぶ
- Consoleログに加えて、画面上の一時的なテキスト表示(`UIManager.cs`)も行う

### `FallDetector.cs`
ボールにアタッチする。

- `Update()`でボールのY座標を監視し、`fallThreshold`(既定 `-15`。落下の「間」を持たせるため意図的に深めに設定)を下回ったら`gameManager`(Inspectorでアサイン)の`OnBallFell()`を呼ぶ

### `UIManager.cs`
シーン内の空のGameObjectにアタッチする。画面上に一時的なテキストを表示する。

- `stageText` / `clearText`(いずれも`UnityEngine.UI.Text`、Inspectorでアサイン)を持つ
- `public void ShowStageAnnouncement(int stage)`: 「ステージ X」を`stageAnnounceDuration`(既定3秒)だけ表示してから自動的に消す(コルーチン)
- `public void ShowClear()`: 「ステージクリア!」を`clearDisplayDuration`(既定2秒)だけ表示してから自動的に消す
- どちらも、表示中に別の表示が呼ばれたら現在のコルーチンを停止して切り替える(表示が重ならないようにする)
- Canvas/Textの作成自体はUnity Editor上での手動作業(GameObject > UI > ...)で行い、`UIManager`コンポーネントの`Stage Text` / `Clear Text`欄にInspectorでドラッグ&ドロップする運用(Canvas関連はUnity組み込みUIパッケージのGUIDに依存する箇所が多く、シーンファイルの直接編集ではリスクが高いため)

---

## 2. シーン構成

スクリプトだけでは動作しない。シーン(`Assets/Scenes/SampleScene.unity`)側で以下が設定されている前提でスクリプトを書いてよい。

| GameObject | 主なコンポーネント | 役割 |
|---|---|---|
| `Ball` | `Sphere`メッシュ, `Rigidbody`, タグ`Player`, `BallController`, `FallDetector` | プレイヤーが操作するボール |
| `Goal` | `Cube`メッシュ, `Collider`(`Is Trigger`), `GoalTrigger` | ゴール判定 |
| `Floor` | `Cube`メッシュを平たく潰したもの, `Collider` | 床。`MazeGenerator`がサイズを迷路に合わせて変更する |
| `MazeGenerator`(空) | `MazeGenerator` | 迷路生成の起点。`wallMaterial`/`floor`/`ball`/`goal`をInspectorで参照 |
| `GameManager`(空) | `GameManager` | ステージ進行・タイマー管理。`mazeGenerator` / `uiManager`をInspectorで参照 |
| `UIManager`(空) | `UIManager` | 画面上のテキスト表示。`stageText` / `clearText`はEditor上でCanvas配下に作成したTextをInspectorでアサイン |
| `Main Camera` | `Camera`, `CameraFollow` | `target`にBallの`Transform`をInspectorでアサイン |

マテリアルは`Assets/Materials/`配下(`FloorMaterial` / `BallMaterial` / `GoalMaterial` / `WallMaterial`、いずれもURP Litシェーダー)。

---

## 3. ビルド／デプロイ(GitHub Pages 向け WebGL)

- Player SettingsのビルドターゲットはWebGL
- **圧縮形式は必ず`Disabled`(またはGzip + Decompression Fallback)。** GitHub Pagesは`Content-Encoding`ヘッダーを設定できないため、既定のBrotli圧縮のままだとブラウザ側でロードに失敗する。WebGLビルドがPages上で真っ黒なまま止まる場合、まずここを疑う
- `.github/workflows/webgl-pages-deploy.yml`が`game-ci/unity-builder`でWebGLビルドを行い、`actions/deploy-pages`でGitHub Pagesにデプロイする(リポジトリのPages設定は`build_type: workflow`)
- トリガーは`Assets/**` `ProjectSettings/**` `Packages/**`の変更時のみ
- Unity Licenseの認証情報(`UNITY_LICENSE` / `UNITY_EMAIL` / `UNITY_PASSWORD`のGitHub Secrets)は登録済み

---

## 4. Unity Personal(無償版)ライセンスの取得手順

CI上で要求ファイルを生成する方式(`game-ci/unity-request-activation-file`)は廃止されている。代わりに、ローカルのUnity Hubで発行された`.ulf`ファイルをそのまま使う。

1. Unity Hub > Preferences > Licenses > 「Get a free personal license」でPersonalライセンスを取得
2. OSごとの保存場所からファイルを取得:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - Mac: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. その中身を`UNITY_LICENSE`に、Unityアカウントのメールアドレス・パスワードを`UNITY_EMAIL` / `UNITY_PASSWORD`に登録する(Personal版は`.ulf`だけでなくアカウント認証情報も併用する)
4. Google等のSSOでアカウントを作りパスワードが無い場合は、[id.unity.com](https://id.unity.com)のログイン画面で「パスワードをお忘れですか?」からパスワードを設定する(SSOログインはそのまま使い続けられる)

---

## 5. 動作確認手順

自動テストは用意していない。変更後は以下を手動で確認する。

1. Unity EditorでPlayモードに入り、WASD(またはタップ&ドラッグ)でボールが加速・減速しながら転がること
2. カメラがボールを追従し、カクつきや振動がないこと
3. 迷路の壁(内部・外周とも想定通り)が表示され、マゼンタ(シェーダーエラー)になっていないこと
4. ボールをゴールに入れ、Consoleに`Stage X Clear! Time: Y.YYs`が出力され、一回り大きい新しい迷路が生成されること
5. ボールを端から落下させ、Consoleに`Fall! Retry Stage X`が出力され、**同じ迷路のまま**ボールが正しい高さでスタート位置に戻ること
6. WebGL関連の変更をした場合は、ビルド後にGitHub Pages上でも実際にロードできることを確認する(ローカルの`file://`直開きではWebGLビルドは動作しないため、ローカル確認にはHTTPサーバー経由が必要)

---

## 6. Unity MCPサーバーの接続(WSL環境向け)

Unity Editor上でUnity MCPを有効化すると、Consoleログの取得やInspectorの値の参照などをClaude Codeから直接行えるようになる(通常はスクリーンショットや手動での値の報告が必要)。

Claude CodeがWSL上、Unity EditorがWindows上で動いている構成では、以下の手順で接続する。

1. Unity Editor側でMCPを有効化する(`Edit > Project Settings > AI`等)。リレー用の実行ファイルがWindows側の`<Windowsユーザーフォルダ>\.unity\relay\relay_win.exe`に配置される
2. WSL側からは`/mnt/c/...`形式でこのパスにアクセスできる(例: `/mnt/c/Users/<ユーザー名>/.unity/relay/relay_win.exe`)
3. WSL側のターミナルで以下を実行し、MCPサーバーとして登録する

   ```bash
   claude mcp add unity-mcp -- "/mnt/c/Users/<ユーザー名>/.unity/relay/relay_win.exe" --mcp
   ```

4. `claude mcp list` で `unity-mcp` が `✔ Connected` になっていれば成功
5. **登録した接続は次回以降の新しいセッションから有効になる**(登録した同じセッション内ではすぐには使えない)

### 注意
- Unity MCP導入時に、個人のアカウント情報を含む`.claude.json`や、リレー用の`relay_win.exe`(約100MB)がリポジトリ直下に誤って置かれることがある。`.gitignore`で除外済みだが、`git status`で紛れ込んでいないか都度確認する
- 詳しい活用方法・調査状況はIssue #33を参照
