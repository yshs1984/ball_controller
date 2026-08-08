# ball_controller 詳細スペック

開発者・Claude Code向けの実装詳細集。`CLAUDE.md`には変わらない原則だけを置き、機能追加のたびに変わる詳細はここにまとめる。各章が独立して読めるよう構成しているので、作業内容に対応する章だけ読めばよい。

## 目次
1. [スクリプト仕様](#1-スクリプト仕様)
2. [シーン構成](#2-シーン構成)
3. [ビルド／デプロイ(GitHub Pages 向け WebGL)](#3-ビルドデプロイgithub-pages-向け-webgl)
4. [Unity Personal(無償版)ライセンスの取得手順](#4-unity-personal無償版ライセンスの取得手順)
5. [動作確認手順](#5-動作確認手順)

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
- `public void GenerateNewStage(int stage)`: `width`/`height`を+1(上限12で頭打ち)してから`Generate()`を呼ぶ。ステージクリアのたびに迷路が一回り大きくなる
- `public void ResetBall()`: 迷路は再生成せず`PlaceBallAndGoal()`だけ呼ぶ(落下リトライ用)
- 壁は`Assets/Materials/WallMaterial.mat`を使い`CreatePrimitive(PrimitiveType.Cube)`で動的生成する。`BuildWalls()`は列/行の境界線を基準にループし、1つの壁につき1つだけGameObjectを生成する(セル単位でループすると、内部の壁が隣接する2セルの両側から重複生成されてしまうため)
- **迷路外周に面する壁は生成しない**(ボールが端まで転がると床の外に落下できる仕様)
- `ballStartHeight`を`Start()`の最初(まだ一度も落下していない時点)で一度だけ記録し、以後のボール再配置は常にこの値を使う(落下でY座標がマイナスになった状態から再計算すると正しい高さに戻らないバグの対策)
- 迷路の大きさ(`width` / `height` / `cellSize` 等)は`[SerializeField]`でInspector調整可能
- **Editor上の非再生時のシーンビューでは迷路は見えない**(`Start()`での実行時生成のため)。Play時に毎回ランダムな迷路になる

### `GameManager.cs`
シーン内の空のGameObjectにアタッチする。ステージ進行とクリアタイム計測を管理する。

- `mazeGenerator`への参照(Inspectorでアサイン)を持つ
- ステージ数(`currentStage`、1始まり)とステージ開始時刻(`Time.time`)を保持
- `public void OnGoalReached()`(`GoalTrigger`から呼ばれる): クリアタイムを`Stage X Clear! Time: Y.YYs`として`Debug.Log`に出力し、ステージを進め、`mazeGenerator.GenerateNewStage()`を呼ぶ
- `public void OnBallFell()`(`FallDetector`から呼ばれる): `Fall! Retry Stage X`をログ出力し、`mazeGenerator.ResetBall()`で**同じ迷路のまま**ボールを戻す(ステージは進めない)
- 画面上のUI表示は無く、Consoleログのみ

### `FallDetector.cs`
ボールにアタッチする。

- `Update()`でボールのY座標を監視し、`fallThreshold`(既定 `-15`。落下の「間」を持たせるため意図的に深めに設定)を下回ったら`gameManager`(Inspectorでアサイン)の`OnBallFell()`を呼ぶ

---

## 2. シーン構成

スクリプトだけでは動作しない。シーン(`Assets/Scenes/SampleScene.unity`)側で以下が設定されている前提でスクリプトを書いてよい。

| GameObject | 主なコンポーネント | 役割 |
|---|---|---|
| `Ball` | `Sphere`メッシュ, `Rigidbody`, タグ`Player`, `BallController`, `FallDetector` | プレイヤーが操作するボール |
| `Goal` | `Cube`メッシュ, `Collider`(`Is Trigger`), `GoalTrigger` | ゴール判定 |
| `Floor` | `Cube`メッシュを平たく潰したもの, `Collider` | 床。`MazeGenerator`がサイズを迷路に合わせて変更する |
| `MazeGenerator`(空) | `MazeGenerator` | 迷路生成の起点。`wallMaterial`/`floor`/`ball`/`goal`をInspectorで参照 |
| `GameManager`(空) | `GameManager` | ステージ進行・タイマー管理。`mazeGenerator`をInspectorで参照 |
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
