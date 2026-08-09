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

### `OnigiriMeshBuilder.cs`
ボールにアタッチする。ボールを「おむすび」(角を丸めた三角柱。下半分に海苔の帯)の見た目・当たり判定にする(Issue #31)。

- Unityに三角柱の標準プリミティブは無いため、`MazeGenerator`の壁と同様に**実行時にコードでメッシュを組み立てる**。`Awake()`で生成し、`MeshFilter`と`MeshCollider`の両方に同じメッシュを設定する
- **`MeshCollider.convex`は`true`にする必要がある**(非kinematicな`Rigidbody`と組み合わせるMeshColliderの制約)。角を丸めた三角柱も凸形状なので問題なく設定できる
- Y軸方向に押し出した三角柱(上から見ると正三角形)にしている。水平方向の力に対してどの向きからも対称に反応させるため。押し出し軸を寝かせると進行方向によって転がり方が変わってしまう
- 角は`cornerRadius`(既定 `0.18`)・`cornerSegments`(既定 `6`)で丸めている。正三角形はどの角も内角60度で合同なので、フィレット円の接点までの距離・中心までの距離は三角関数から求まる同じ式を3つの角すべてに使い回している(`BuildRoundedTriangleProfile()`)
- 海苔は**三角柱を貫く「四角柱」として切り出す**。Z方向の範囲(`noriStartRatio` / `noriEndRatio`。既定 `0`〜`0.45`で底辺側から45%)に加えて、**X方向の幅**(`noriWidthRatio`。既定 `0.45`)も指定し、厚み方向(Y)だけは全体を貫く。幅を1未満にすることで、上下の面に**三角形の輪郭に沿わない長方形**として現れるのが要点
  - 海苔の領域を三角形の断面に沿わせる(=下半分だけ黒)と「黒い三角柱」に見えてしまい、幅を絞らずZだけで切ると輪郭に沿った台形になってしまう。どちらもおにぎりに見えないので注意
  - 四角柱のX方向の面はおむすびの内部にあるため描画されない。海苔が見えるのは上下の面と、底辺側の側面のみ
- 切り分けは、輪郭が切断面と交わる位置に頂点を挿し込んでから座標で振り分けるだけでよい。**凸多角形を平面で切った断片はどちらも凸**なので、巡回順を保ったまま抽出して先頭頂点から扇状に三角形分割できる(一般的な多角形クリッピングは不要)
- 切り分けの端には一直線に並んだ頂点が残り、そのまま扇状分割すると**面積ゼロの三角形**がメッシュに紛れ込む(描画上は見えないがゴミになる)。`RemoveRedundantVertices()`で重複・一直線の頂点を間引いてから三角形化している
- `MeshRenderer`には2つの材質(`sharedMaterial`=既存の米色`BallMaterial`、`noriMaterial`=Inspectorでアサインする黒い`NoriMaterial`)を割り当て、メッシュを2つのサブメッシュに分けて描画する
- 頂点は面ごとに分離してフラットシェーディングにしている。頂点の巻き順は、Unity組み込みQuadメッシュの値から検算した規則「`Vector3.Cross(b - a, c - a)`が表向きの法線になる」に従っている(丸め対応後も、外向き判定の実測で全面が正しい向きであることを確認済み)
- `radius`(既定 `0.6`)と`thickness`(既定 `0.6`)は`[SerializeField]`で調整可能。**`thickness`を薄くすると底面が安定しすぎて一切転ばず、床を滑るだけの動きになる**。半径0.6に対して実測した結果: 厚み`0.4`/`0.5`は`moveForce`をどれだけ上げても(45まで試行)ほとんど転倒しない(滑るだけ)。`0.6`は既定の`moveForce=17`のままで転倒9回とよく転がる。`0.8`は転倒2回。**見た目の薄さを優先して`thickness`を下げる場合、`0.6`が転がる挙動を保てる下限**(見た目は薄くしたいが転がらなくなったというフィードバックを受けて確定した値)
- 角を丸める前の鋭角な三角柱は球より接地面の摩擦が大きく、球体時代の`BallController.moveForce = 10`のままではほとんど動き出せなかった。そのため`moveForce`は`17`に引き上げてある(球体+力10と同程度の機動力になる値を実測で求めた。角を丸めた後も同じ値で同程度の機動力になることを再確認済み)
- **Editor上の非再生時のシーンビューでは、このメッシュはまだ生成されていない**(`MazeGenerator`の迷路と同じ制約)

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
| `Ball` | 三角柱メッシュ(`OnigiriMeshBuilder`が実行時に生成), `MeshCollider`(convex), `Rigidbody`, タグ`Player`, `BallController`, `FallDetector`, `OnigiriMeshBuilder` | プレイヤーが操作するおむすび |
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

1. Unity EditorでPlayモードに入り、WASD(またはタップ&ドラッグ)でおむすびが加速・減速しながら、角を軸にゴロゴロと転がること(滑るだけで転ばない場合は`OnigiriMeshBuilder`の`thickness`が薄すぎる。ほとんど動き出せない場合は`BallController`の`moveForce`が足りない)
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
