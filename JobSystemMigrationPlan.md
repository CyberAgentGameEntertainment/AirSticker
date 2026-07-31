# Job System 移行 実装計画

Air Sticker のデカール貼り付け処理(ポリゴン分割)を Worker Thread (ThreadPool) から Unity Job System + Burst へ移行するかの評価と、段階的な実装計画。

- 作成日: 2026-07-31
- ステータス: 計画(未着手)

## 背景と結論

現行実装はポリゴン分割を ThreadPool のワーカースレッド 1 本で非同期実行している。Job System + Burst へ移行した場合、**ワーカー計算そのものは 1/10 以下の wall-time が現実的**であり、データ構造(SoA + オフセット設計)も移行に有利な形をしている。

ただし、ユーザー体感に効いているコストの多くは**ワーカースレッドの外**にある:

- メインスレッドのメッシュアップロード(`Mesh.Optimize()` / `RecalculateTangents()`、分割なし)
- 毎 Launch の巨大なマネージドアロケーション(GC スパイクの原因)
- FIFO 直列実行によるキュー詰まり(多数同時デカール時の出現遅延)

これらは Job System 移行「だけ」では解決しないため、**計測 → 低リスク改善 → Job 化**の順で段階的に進める。

## 現状のコスト構造

1 回の Launch で走る処理:

| # | 処理 | スレッド | 備考 |
|---|---|---|---|
| 1 | 三角形ポリゴン抽出 (`TrianglePolygonsFactory`) | メイン(フレーム分割) | 初回のみ、以降は `ReceiverObjectTrianglePolygonsPool` |
| 2 | `PrepareToRunOnWorkerThread` ループ | メイン(**分割なし**) | 全ポリゴン O(N)。`AirStickerProjector.cs:210-211` |
| 3 | スキニング + ワールド変換 | ワーカー(1 本) | **全ポリゴン**対象。頂点ごとに 4x4 行列 4 本のブレンド。`ConvexPolygon.CalculatePositionsAndNormalsInWorldSpace` |
| 4 | ブロードフェーズ + バッファ複製 | ワーカー | 毎回巨大なマネージド確保(下記) |
| 5 | 6 平面クリップ (`SplitAndRemoveByPlane`) | ワーカー | ブロードフェーズ生存ポリゴンのみ |
| 6 | メッシュアップロード (`ExecutePostProcessingAfterWorkerThread`) | メイン(**分割なし**) | `SetVertices` + `RecalculateTangents` + `Optimize`。`DecalMesh.cs:67-91` |

### ボトルネック候補

1. **GC アロケーション** — `BroadPhaseConvexPolygonsDetection.Execute` が毎回 6 本のバッファを new する(`BroadPhaseConvexPolygonsDetection.cs:72-78`)。`Line` 構造体が約 172B(Vector3 x9 + BoneWeight x2)あるため、生存ポリゴン 1 個あたり 64 スロット x 約 252B ≈ 16KB。**生存 1,000 ポリゴンで 1 Launch あたり約 16MB のマネージド確保**となり、後の GC スパイクとして跳ね返る。
2. **メインスレッドのアップロードスパイク** — `Mesh.Optimize()` と `RecalculateTangents()` は頂点数に比例して重く、分割されていない。デカールが積み重なって `_numVertex` が増えるほど、貼るたびに全体を再アップロードするので悪化する。
3. **直列 FIFO** — `DecalProjectorLauncher` は 1 度に 1 プロジェクタしか走らせないため、弾痕のような同時多発要求では出現遅延が線形に伸びる。
4. **スキニングの適用範囲** — スキニングはブロードフェーズの**前に全ポリゴン**へ適用されるため、スキンメッシュ受けではワーカー内の最重量ループ(2 万トライアングルで Mono マネージドだと 10ms 台の見込み)。

## Job System + Burst 移行の評価

### 相性が良い点

- `ConvexPolygon` はすでに「共有バッファ + オフセット」の SoA 設計で、`NativeArray` への写像がほぼ素直にできる。壊す必要があるのはクラスのラッパーと `Component` 参照(ジョブにマネージド参照は持ち込めないため `rendererIndex` 等に置換)のみ。
- クリップ処理はポリゴン単位で完全に独立、かつ最大 64 頂点(`DefaultMaxVertex`)の固定ストライドなので、`NativeStream` のような可変長出力の仕掛けなしに `IJobParallelFor` が書ける。
- コルーチンのポーリング構造(`_executeLaunchingOnWorkerThread` フラグ)は `JobHandle.IsCompleted` のポーリングにそのまま置き換わり、ステートマシンや `DecalProjectorLauncher` は変更不要。

### 見込める効果(概算)

| 要因 | 効果 |
|---|---|
| Burst 単体 | 数学中心ループで Mono(エディタ)比 5〜20x、IL2CPP 比でも 2〜6x |
| `IJobParallelFor` 並列化 | ポリゴン独立なのでコア数にほぼ線形(モバイル 4〜8 コアで 3〜6x) |
| 合計 | **ワーカー計算の wall-time は 1/10 以下が現実的** |
| `NativeArray` 化 | 毎 Launch の GC 確保が消滅 |
| `Mesh.AllocateWritableMeshData`(2020.1+) | 頂点バッファ構築をジョブ内へ移し、アップロードスパイクを縮小 |
| ThreadPool 廃止 | Unity のジョブワーカーとの CPU 競合を解消(モバイルで有効) |

### 体感への翻訳(重要)

ワーカー計算はすでに非同期なので、速くなって縮むのは「デカール出現までのフレーム数」と「FIFO キューの消化速度」。

- 単発のデカールを時々貼る用途 → 体感差はほぼ出ない
- スキンメッシュ受けに多数同時に貼る用途 → 効果大

### 移行コスト・リスク

| 項目 | 内容 |
|---|---|
| パッケージ依存 | `NativeArray` はコアモジュールで依存ゼロだが、Burst は `com.unity.burst` への依存追加が必要。現在 `jp.co.cyberagent.air-sticker` は依存ゼロのパッケージなので利用者影響を含めて判断する。Jobs + NativeArray のみ(Burst なし)の中間形態も可能だが、速度向上の主因は Burst なので効果は半減以下 |
| 最小サポートバージョン | Unity 2020.3 縛りだと Burst 1.6 系に固定。動作検証が必要 |
| 公開 API の破壊 | `ConvexPolygon` は public で、`Assets/Tests/TestConvexPolygon.cs` と `TestBroadPhaseConvexPolygonsDetection.cs` が直接参照。構造体 SoA 化は破壊的変更でメジャーバージョンアップ相当。テストは仕様の記録として、先に新データ構造へ移植してから本体を書き換える |
| `Line` 構造体 | 頂点データを重複保持(実質各頂点を約 4 回持つ)。Burst 化のついでに廃止し、エッジをオンザフライ計算する。メモリトラフィック半減で高速化にも効く |
| Burst 制約 | マネージド参照・例外処理が制限される。エラーパス(`Debug.LogException`)の設計変更が必要 |

## 実装計画

### Step 0: 計測(最初にやる・すべての判断の前提)

- [x] `Stopwatch` + `Debug.Log` によるログベース計測を 4 箇所に追加(`ProfilerMarker` ではなくログ出力方式を採用)
  - `PrepareToRunOnWorkerThread` ループ(`AirStickerProjector.ExecuteLaunch:210-217`)
  - ワーカースレッド実行時間(スキニング / ブロードフェーズ / クリップ+構築を個別に、`AirStickerProjector.ExecuteLaunch:229-273`)
  - ブロードフェーズのバッファ確保(`BroadPhaseConvexPolygonsDetection.Execute`)
  - `ExecutePostProcessingAfterWorkerThread`(アップロード、`DecalMesh.cs`)
  - 計測は既定で無効。`AirStickerPerformanceLog.Enabled = true;` を設定すると `Debug.Log` に `[AirSticker][Perf]` プレフィックスで出力される(`Core/AirStickerPerformanceLog.cs`)
- [ ] 代表シナリオでベースライン取得
  - [x] 大きめスキンメッシュ(単発 Launch、同一オブジェクトへの2回目 Launch)
  - [ ] 静的メッシュ
  - [ ] テレイン
  - [ ] 連続 Launch(キュー詰まり再現)
- [x] エディタ(Mono)とビルド(IL2CPP)の両方で計測(大きめスキンメッシュのみ済み。静的メッシュ/テレイン/連続 Launch は未計測)

**判断基準**: どのコストが支配的かで Step 1〜3 の優先度を決める。

#### 計測結果: 大きめスキンメッシュ(2026-07-31、エディタ Mono)

入力 8191 ポリゴン、ブロードフェーズ生存 4849 ポリゴンの受けオブジェクトに対し、同一シーン内で2回連続 Launch した結果。

| 項目 | 1回目 Launch | 2回目 Launch |
|---|---|---|
| `PrepareToRunOnWorkerThread`(メイン、分割なし) | 1.36 ms | 0.96 ms |
| ワーカー: スキニング | 14.27 ms | 3.93 ms |
| ワーカー: ブロードフェーズ(バッファ確保込み) | 76.74 ms | 58.37 ms |
| うちバッファ確保のみ | 48.63 ms | 55.23 ms |
| ワーカー: クリップ+構築 | 17.91 ms | 3.40 ms |
| ワーカー合計 | 108.92 ms | 65.70 ms |
| メッシュアップロード(DecalMesh 毎、最大値) | 3.74 ms | 0.26 ms |

**分かったこと**:

1. **ブロードフェーズのバッファ確保が支配的コスト。** 2回目 Launch ではワーカー合計 65.70ms のうち 55.23ms(**約84%**)がバッファ確保だけで占められる。スキニング/クリップの処理時間が1回目→2回目で大きく縮んだ(JIT ウォームアップの影響と推測)後も、バッファ確保時間はほぼ変化しない(48.63ms→55.23ms)ため、これは「処理量に比例した実コスト」であり、繰り返し計測しても消えないボトルネックと判断できる。Step 1 の「ブロードフェーズバッファの再利用・プール化」が最優先で効果が見込める。
2. **1回目 Launch の数値は JIT ウォームアップで水増しされている。** スキニング(14.27→3.93ms)・クリップ+構築(17.91→3.40ms)が2回目で3〜5倍縮小。同一 input/survived 数でこの差が出るのは処理量ではなく初回 JIT コンパイルコストのため。今後のベースライン比較では初回 Launch の数値を単純比較に使わないこと。
3. **メッシュアップロードは今回のシナリオでは軽微。** 最大でも 3.74ms で、ブロードフェーズのバッファ確保に比べ影響が小さい。ただしデカール積み重ね時の `_numVertex` 増加による影響は連続 Launch シナリオで別途確認が必要。

#### 計測結果: 大きめスキンメッシュ(2026-07-31、IL2CPP / Android実機 Pixel 8a)

同一シナリオ(入力 8191 ポリゴン、生存 4849 ポリゴン)を実機で3回連続 Launch。

| 項目 | 1回目 | 2回目 | 3回目 |
|---|---|---|---|
| `PrepareToRunOnWorkerThread` | 2.34 ms | 1.73 ms | 1.76 ms |
| ワーカー: スキニング | 4.65 ms | 3.98 ms | 8.91 ms |
| ワーカー: ブロードフェーズ(バッファ確保込み) | 78.93 ms | 56.69 ms | 65.03 ms |
| うちバッファ確保のみ | 68.69 ms | 35.43 ms | 53.73 ms |
| うち純粋なブロードフェーズ計算(差分) | 10.24 ms | 21.26 ms | 11.30 ms |
| ワーカー: クリップ+構築 | 5.30 ms | 3.20 ms | 2.64 ms |
| ワーカー合計 | 88.88 ms | 63.87 ms | 76.58 ms |
| メッシュアップロード(DecalMesh 毎、最大値) | 1.05 ms | 0.63 ms | 0.81 ms |

**分かったこと**:

1. **IL2CPP(AOT)では JIT ウォームアップ的な減衰が見られない。** スキニング(4.65/3.98/8.91ms)・クリップ+構築(5.30/3.20/2.64ms)は3回とも近い値で、Editor のような「1回目だけ極端に遅い」パターンが出ない。これは Editor(Mono)の1回目だけ突出して遅かった原因が JIT コンパイルコストであるという前回の推測を裏付ける。
2. **ブロードフェーズのバッファ確保は実機でも支配的。** バッファ確保だけで broadPhase 全体の 63〜87%(68.69/78.93, 35.43/56.69, 53.73/65.03)を占め、純粋な計算コストは 10〜21ms 程度に留まる。Editor だけでなく IL2CPP / Android 実機でも「ブロードフェーズバッファのプール化(Step 1)」が最優先で効くことが確認できた。
3. **実行ごとのばらつきが大きい。** バッファ確保だけで 35.43〜68.69ms と2倍近い揺れがある。モバイル端末側の GC/メモリアロケータ挙動やスレッド スケジューリングの揺らぎと考えられ、単発比較ではなく複数回の平均・中央値で見るべき。
4. **メッシュアップロードは実機でも軽微。** 最大 1.05ms 程度で、Editor の結果(最大 3.74ms)と同様に他コストに比べ小さい。

**次にやること**: 静的メッシュ・テレイン・連続 Launch(キュー詰まり)の計測を Editor / IL2CPP 両方で継続する。ただし大きめスキンメッシュについては Editor・実機の両方で「ブロードフェーズバッファ確保が支配的」という結論が一致したため、Step 1 のバッファプール化から着手して問題ない。

### Step 1: 構造変更なしの改善(効果/コスト比が最良)

- [x] ブロードフェーズバッファの再利用・プール化(GC 対策)
  - `BroadPhaseConvexPolygonsDetection` に静的なプールバッファ(`_pooledPositionBuffer` 等6本)を追加し、`Execute()` 内の `new` を `EnsureCapacity`(必要サイズ未満の場合のみ倍々で再確保)に置き換えた
  - `DecalProjectorLauncher` が常に1 Launch(=1 Execute呼び出し)しか同時実行しないこと、かつ結果が同じワーカースレッド呼び出し内で `DecalMesh` へコピーされ切ってから次の `Execute` が呼ばれることを前提に、静的プールの使い回しを安全と判断
  - `Execute` の公開シグネチャは変更なし。EditMode テスト(`TestBroadPhaseConvexPolygonsDetection`)はそのまま通る想定
  - **効果測定済み(2026-07-31、エディタ Mono、大きめスキンメッシュ・入力8191/生存4849ポリゴンの2回目 Launch)**: BroadPhase バッファ確保 55.23ms → **0.01ms**。ワーカー合計(skinning+broadPhase+clip+build)も 65.70ms → **8.65ms**(約7.6倍)に短縮。プール化のみで Step 3 の目標値(1/10以下)にほぼ到達しており、期待通りの効果を確認
- [x] プロジェクタ破棄時のワーカースレッド並走の修正(プール化レビューでの指摘、2026-07-31)
  - プロジェクタが Launch 中に破棄されるとコルーチンは止まるが ThreadPool のワークアイテムは走り続ける。一方 `DecalProjectorLauncher.IsCurrentRequestFinished` は「プロジェクタが死んだ/Canceled」で終了扱いにして次の Launch を開始するため、前の Launch のワーカーと次の Launch のワーカーが並走し、静的プールバッファを同時に読み書きするデータ競合があった(プール化以前は Execute ごとに新規確保していたため無害だった)
  - 修正: `AirStickerProjector.IsWorkerThreadRunning`(internal、volatile な `_executeLaunchingOnWorkerThread` を公開)を追加し、`IsCurrentRequestFinished` が「プロジェクタが死んでいてもワーカースレッドのフラグが下りるまで false を返す」よう変更。Unity オブジェクト破棄後も C# インスタンスのフィールドは読めることを利用
  - 副次効果として、破棄されたプロジェクタのワーカーと次の Launch が同一 `DecalMesh` へ同時 append する既存レースも塞がる
- [ ] `Mesh.Optimize()` の必要性見直し(頂点キャッシュ最適化の効果 vs 毎回のコスト)
- [ ] `PrepareToRunOnWorkerThread` ループのフレーム分割(`MaxGeneratedPolygonPerFrame` と同様の方式)

**期待効果**: メインスレッドの体感スパイクの大部分を削減。公開 API 変更なし。

### Step 2: アップロードの MeshData API 化

- [ ] `DecalMesh` の頂点バッファ構築を `Mesh.AllocateWritableMeshData` / `ApplyAndDisposeWritableMeshData` へ移行
- [ ] タンジェント計算を自前実装しジョブ化可能な形に(`RecalculateTangents` の置き換え)

**期待効果**: アップロードスパイク縮小。Step 3 のジョブチェーンの受け皿になる。ジョブ移行と独立に着手可能。

### Step 3: 本丸の Job + Burst 化

- [ ] `ConvexPolygon` を index ベースの struct SoA に再設計
  - `Component` 参照 → `rendererIndex` に置換
  - `Line` 構造体を廃止、エッジはオンザフライ計算
  - 固定ストライド(最大 64 頂点)を維持し `IJobParallelFor` を素直に書く
- [ ] ジョブチェーン構築: スキニング → ブロードフェーズ → クリップ → メッシュ構築
- [ ] コルーチンのポーリングを `JobHandle.IsCompleted` に置き換え
- [ ] `package.json` に `com.unity.burst` 依存を追加(バージョンはUnity 2020.3 互換の 1.6 系)
- [ ] EditMode テストを新データ構造へ移植(先行して行い、リグレッション検出に使う)
- [ ] メジャーバージョンアップとしてリリース

**着手判断**: Step 0 の計測結果と「多数同時デカール需要があるか」で決める。

## 受け入れ基準(案)

- Step 1 完了時: 1 Launch あたりのマネージド確保量が 1/10 以下、メインスレッドの最大フレーム時間スパイクが計測可能に減少
- Step 3 完了時: 代表シナリオ(スキンメッシュ 2 万トライアングル)でワーカー計算 wall-time が 1/10 以下、既存 EditMode テスト全パス、デモシーン 3 種の見た目に差分なし

## レビュー指摘の残課題(Job System 移行完了後に対応)

2026-07-31 のブランチレビュー(Step 0/1 実装後)で挙がった指摘のうち未対応のもの。**Step 3 完了後にまとめて対応する。**

1. **静的プールの恒久的メモリ保持** — `BroadPhaseConvexPolygonsDetection` の静的プールバッファは一度成長すると解放されない(実機ビルドではアプリ終了まで、エディタはドメインリロードまで)。計測シナリオ(生存 4849 ポリゴン)で `64 スロット × 4849 × 約 252B ≈ 78MB`、倍々成長のため最悪その約 2 倍。`AirStickerSystem.OnDestroy()` から呼ぶ `ReleaseBuffers()` のような明示解放フック、またはしきい値超過時のトリムを追加する。Step 3 の `NativeArray` 化でバッファ設計自体を見直すため、そのタイミングで一緒に解決するのが効率的
2. **Demo03 の計測ログ常時 ON** — `Demo03.Start()` の `AirStickerPerformanceLog.Enabled = true;` はエージングテストでコンソールを埋め、`Debug.Log` のコスト(特にワーカースレッド内)が「連続 Launch(キュー詰まり再現)」の計測値を歪める。Step 0 の計測がすべて完了したら削除するか UI トグル化する
3. **`AirStickerPerformanceLog` が配布パッケージの公開 API** — `Assets/AirSticker` 配下は UPM パッケージとして配布されるため、public static クラスは一度リリースすると利用者が依存し得る。移行完了後に削除するか、残す場合は一時的な計測基盤である旨を明記する。XML コメントが参照する本ドキュメントはパッケージ利用者(`?path=/Assets/AirSticker`)には配布されない点も直す
4. **`_broadPhaseConvexPolygonInfos` のクリア** — Launch 完了後もプールバッファを指す `ConvexPolygon` ラッパーを保持し続ける(現状読み手はいないため実害なし)。`_convexPolygonInfos` と同様に Launch 末尾で null クリアし、プール化コメントの前提(消費し切ってから次が走る)と実態を一致させる。Step 3 のデータ構造再設計で自然に消える可能性もある

## 参照

- `Assets/AirSticker/Runtime/Scripts/AirStickerProjector.cs` — 状態機械・ワーカースレッド起動
- `Assets/AirSticker/Runtime/Scripts/Core/ConvexPolygon.cs` — SoA バッファ・クリップ処理・スキニング
- `Assets/AirSticker/Runtime/Scripts/Core/BroadPhaseConvexPolygonsDetection.cs` — ブロードフェーズと毎回のバッファ確保
- `Assets/AirSticker/Runtime/Scripts/Core/DecalMesh.cs` — メッシュアップロード
- `Assets/AirSticker/Runtime/Scripts/Core/Line.cs` — 廃止候補の構造体
- `README_DEVELOPERS.md` — アルゴリズム詳細
