# Job System 移行 実装計画

Air Sticker のデカール貼り付け処理(ポリゴン分割)を Worker Thread (ThreadPool) から Unity Job System + Burst へ移行するかの評価と、段階的な実装計画。

- 作成日: 2026-07-31
- ステータス: **Step 3a 完了(2026-07-31、実装・テスト・見た目・計測すべて確認済み。Burst 無効の並列化だけでワーカー計算 9.63ms→1.98ms ≈ 4.9x)**。残: 3c(Burst 有効化)、IL2CPP/実機計測、Step 0 の残計測(静的メッシュ/テレイン/連続 Launch)

## 運用ルール

- **コミットは計測後に行う。** 各 Step の実装が完了したら、まず計測基盤(`AirStickerPerformanceLog.Enabled = true`)で代表シナリオを計測し、効果とリグレッションの有無を確認してからコミットする(2026-07-31 ユーザー指示)。実装直後の未計測状態ではコミットしない。

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
| 6 | メッシュアップロード (`ExecutePostProcessingAfterWorkerThread`) | メイン(**分割なし**) | Step 2 で MeshData 化(単一 Apply)。タンジェント計算はワーカーの 5 へ移動(`Optimize` は Step 1 で削除)。`DecalMesh.cs` |

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
- [x] Launch 中に破棄されたプロジェクタのゴーストデカール修正(Codex レビュー指摘、2026-07-31)
  - プロジェクタが Launch 中に破棄されるとコルーチン(アップロード)は止まるがワーカースレッドの追記は完走するため、キャンセル済みデカールの頂点がプール共有の `DecalMesh` の CPU バッファに残り、同じ (receiver, renderer, material) への次の Launch で一緒にアップロードされて出現する既存バグがあった
  - 修正: ワーカー起動直前に各 `DecalMesh` の頂点/インデックス数をスナップショット(`SnapshotBufferSizes`)し、`DecalProjectorLauncher` が「プロジェクタ終了+ワーカー完了」を検知した時点で未アップロードの追記分を巻き戻す(`AirStickerProjector.RollbackAppendedGeometryIfPending`)。巻き戻しはワーカー完了後にメインスレッドで行うためスレッド競合はない
  - ワーカー失敗(`_workerThreadFailed`)時も同様に巻き戻すようにし、例外時の中途半端な追記が次の Launch で出現する問題も解消
- [x] プロジェクタ破棄時のワーカースレッド並走の修正(プール化レビューでの指摘、2026-07-31)
  - プロジェクタが Launch 中に破棄されるとコルーチンは止まるが ThreadPool のワークアイテムは走り続ける。一方 `DecalProjectorLauncher.IsCurrentRequestFinished` は「プロジェクタが死んだ/Canceled」で終了扱いにして次の Launch を開始するため、前の Launch のワーカーと次の Launch のワーカーが並走し、静的プールバッファを同時に読み書きするデータ競合があった(プール化以前は Execute ごとに新規確保していたため無害だった)
  - 修正: `AirStickerProjector.IsWorkerThreadRunning`(internal、volatile な `_executeLaunchingOnWorkerThread` を公開)を追加し、`IsCurrentRequestFinished` が「プロジェクタが死んでいてもワーカースレッドのフラグが下りるまで false を返す」よう変更。Unity オブジェクト破棄後も C# インスタンスのフィールドは読めることを利用
  - 副次効果として、破棄されたプロジェクタのワーカーと次の Launch が同一 `DecalMesh` へ同時 append する既存レースも塞がる
- [x] `Mesh.Optimize()` の必要性見直し → 削除(2026-07-31)
  - デカールメッシュのインデックスは `DecalMesh.AddTrianglePolygonsToDecalMesh` が凸多角形ごとのトライアングルファンを `indexBase` 単調増加で先頭から順に emit しており、頂点参照はほぼ逐次。頂点キャッシュ効率は最適化前からほぼ上限にあり `Optimize()` の GPU 側効果は見込めない
  - 一方コストは毎 Launch 発生し、デカール積み重ねで頂点数が増えるほど悪化する(Unity 公式ドキュメントも「一度生成して何度も描画するメッシュ向け」としている)。効果ほぼゼロ・コスト再発型のため削除した
  - あわせて `RecalculateTangents()` が `SetUVs()` より**前**に呼ばれていた不具合を修正(タンジェント計算は UV0 依存。初回 Launch では UV 未設定のままタンジェントが計算されていた)。法線マップを使うデカールマテリアルの描画が正しくなる方向の変更
  - **効果測定済み(2026-07-31、エディタ Mono、大きめスキンメッシュ・同一シナリオ)**: メッシュアップロード(DecalMesh 毎の最大値)が 2回目 Launch で 0.26ms → **0.11ms**(約58%減)、1回目 Launch で 3.74ms → 0.84ms(初回は JIT ウォームアップ込みのため参考値)。ベースラインは DecalMesh 毎の頂点数を記録していないため厳密な同一メッシュ比較ではないが、シナリオ(入力 8191/生存 4849、2回連続 Launch)は同一
  - 同じ計測で、prepare ループのフレーム分割が既定値でオーバーヘッドを持たないこと(1.35/0.97ms、分割前の 1.36/0.96ms と同水準)、プールバッファが初回のみ成長(38.22ms)し2回目は 0.01ms になることも確認
- [x] `PrepareToRunOnWorkerThread` ループのフレーム分割(2026-07-31)
  - `TrianglePolygonsFactory.MaxGeneratedPolygonPerFrame`(既定 100,000)ごとに `yield return null` を挟む方式で、既存のポリゴン抽出と同じノブを共有。既定値では計測シナリオ(8,191 ポリゴン)の挙動は変わらず、巨大な受けオブジェクトでのみ分割が発動する
  - フレームまたぎ中に受けオブジェクトが破棄された場合は `LaunchingCanceled` で中断する
  - ボーン行列パレットの取得(`CalculateMatricesPallet`)を prepare ループの**後**へ移動し、ワーカースレッド開始と同じフレームでサンプリングされるようにした(ループがフレームをまたいでもスキニング行列が古くならない)

**期待効果**: メインスレッドの体感スパイクの大部分を削減。公開 API 変更なし。

### Step 2: アップロードの MeshData API 化

- [x] `DecalMesh` の頂点バッファ構築を `Mesh.AllocateWritableMeshData` / `ApplyAndDisposeWritableMeshData` へ移行(2026-07-31 実装・計測済み)
  - 頂点バッファを MeshData 上で直接構築し、`SetVertices`/`SetIndices`/`SetNormals`/`SetUVs`/`boneWeights` の複数回の Set 呼び出しを 1 回の Apply に置き換えた。静的メッシュは単一インターリーブストリーム(Position / Normal / Tangent / TexCoord0)
  - スキニングウェイトは `Mesh.boneWeights` プロパティではなく頂点属性(BlendWeight Float32x4 / BlendIndices UInt32x4)として頂点バッファへ直接格納し、`boneWeights` 代入時の頂点バッファ再レイアウトを排除。`bindposes` は Apply 後に従来どおり設定
  - **スキンメッシュのストリーム制約(実装時に発覚したバグと修正、2026-07-31)**: スキンメッシュでは全属性の単一ストリーム化を Unity が拒否し(エラー `Skinned mesh attributes use wrong streams`)、レイアウト不成立でメッシュが大きく崩壊した。スキニングで変形される属性(Position / Normal / Tangent)= stream 0、TexCoord0 = stream 1、スキンデータ(BlendWeight / BlendIndices)= stream 2 の 3 ストリーム分割が必須。修正済み。Step 3 で `MeshData` 構築をジョブ化する際もこのストリーム分割を維持すること
  - インデックスフォーマットは頂点数に応じて UInt16 / UInt32 を自動選択。旧実装は `Mesh` 既定の UInt16 のままだったため頂点数が 65,535 を超えると壊れていた(デカール積み重ねで到達し得る)潜在バグも解消
  - `RecalculateBounds()` は従来どおり Apply 後にメインスレッドで実行(バウンディングボックス計算のジョブ化は Step 3 で行う)
- [x] タンジェント計算を自前実装しジョブ化可能な形に(`RecalculateTangents` の置き換え)(2026-07-31 実装・計測済み)
  - `Core/DecalMeshTangentCalculator.cs`(internal static)を新設。三角形ごとの接空間累積 + Gram-Schmidt 直交化(Lengyel 法)で、Unity 組み込みの `RecalculateTangents()` と同系のアルゴリズム
  - 純粋な配列演算のみ(Unity API 不使用)なのでワーカースレッドで実行でき、Step 3 で `IJobParallelFor` へ素直に移植できる
  - 追記ジオメトリは自己完結(追記範囲外の頂点を参照する三角形がない)なので**追記分のみの差分計算**とし、`AddTrianglePolygonsToDecalMesh` の末尾(ワーカースレッド内)で実行。メインスレッドからタンジェント計算コストが消え、旧実装と異なりデカール積み重ね時も計算量が頂点総数に比例しない
  - 累積用一時バッファはブロードフェーズプールと同じ前提(同時実行は 1 Launch のみ)で static プール化し、毎 Launch の GC 確保なし
  - 縮退 UV(デカール平面にほぼ垂直なポリゴン)は累積をスキップし、法線に直交する任意軸へフォールバック

**期待効果**: アップロードスパイク縮小。Step 3 のジョブチェーンの受け皿になる。ジョブ移行と独立に着手可能。

#### 計測結果: Step 2(2026-07-31、エディタ Mono、大きめスキンメッシュ・入力 8191/生存 4849 ポリゴン、2回連続 Launch)

| 項目 | 1回目 Launch | 2回目 Launch | ベースライン(Step 1 完了時 1回目/2回目) |
|---|---|---|---|
| `PrepareToRunOnWorkerThread` | 1.42 ms | 0.90 ms | 1.35 / 0.97 ms |
| BroadPhase バッファ確保 | 7.86 ms(プール初回成長) | 0.01 ms | 38.22 / 0.01 ms |
| ワーカー: スキニング | 4.94 ms | 3.66 ms | ― |
| ワーカー: ブロードフェーズ | 13.44 ms | 2.80 ms | ― |
| ワーカー: クリップ+構築(タンジェント込み) | 8.68 ms | 3.17 ms | ― |
| うちタンジェント計算(全 DecalMesh 合計) | 0.78 ms | 0.08 ms | ―(旧実装はメインスレッドの `RecalculateTangents`) |
| ワーカー合計 | 27.06 ms | 9.63 ms | 65.70(プール化前)/ 8.65 ms |
| メッシュアップロード(DecalMesh 毎の最大値) | 1.92 ms | **0.09 ms**(1,168 頂点) | 0.84 / **0.11 ms** |

**分かったこと**:

1. **アップロード(2回目)は 0.11 → 0.09 ms。** タンジェント計算をメインスレッドから排除した上での微減。このシナリオは頂点数が小さいため絶対値の差は小さいが、旧実装で頂点総数に比例していた `RecalculateTangents` コストが消えたため、デカール積み重ね時の伸びが構造的に抑えられる。
2. **タンジェント計算のワーカー追加分は 2回目 0.08 ms と誤差レベル。** 差分計算(追記分のみ)のため、積み重ねで頂点総数が増えても増加しない。
3. **ワーカー合計 9.63 ms は Step 1 完了時(8.65 ms)と同水準。** 差分はブロードフェーズ純計算の実行ごとの揺らぎ範囲内で、リグレッションなし。
4. **1回目のアップロード増(0.84 → 1.92 ms)は MeshData 新パスの初回 JIT ウォームアップ。** Step 0 の知見どおり1回目 Launch の数値は比較に使わない。IL2CPP(AOT)では発生しない想定で、実機は Step 0 の残計測と合わせて確認する。
5. **見た目確認済み。** スキンメッシュのストリーム修正後、デモシーンの表示は正常(スキニング追従含む)。

**判断**: リグレッションなし・Step 2 の目的(メインスレッドからのタンジェント計算排除、単一 Apply 化、Step 3 の受け皿)を達成したためコミットする。

#### Codex レビュー記録(2026-07-31、コミット 8d125c1 に対して実施・指摘 2 件とも見送り)

1. **ワーカースレッド内の `Debug.Log`(major)** — `AddTrianglePolygonsToDecalMesh` 末尾のタンジェント計測ログがワーカースレッドで実行されるという指摘。`Debug.Log` はスレッドセーフが保証された API であり、Step 0/1 でコミット済みの計測コード(ワーカー内サマリログ・BroadPhase ログ・`LogException`)と同一パターンのため見送り。計測基盤の削除(残課題 2)と同時に自然消滅する
2. **UV 行列式の固定しきい値 `1e-8f`(minor)** — デカールサイズ・UV スケール依存で有効な三角形を縮退扱いし得るという指摘。UV はデカールボックスで正規化済みで、該当するのは辺がデカール幅の約 0.01% 未満の三角形のみ。現実的なメッシュでは発生せず、デカール面に垂直なポリゴンのフォールバックは意図した動作のため見送り。Step 3 のジョブ化で再実装する際に相対判定(項の大きさ × 1e-6)への変更を検討してもよい

### Step 3: 本丸の Job + Burst 化

#### 確定方針(2026-07-31 ユーザー決定)

計画当初の前提「Unity 2020.3 縛り → Burst 1.6 系」は**崩れている**。実開発環境は **Unity 6000.3.19f1(Unity 6.3)** で、`packages-lock.json` に **Burst 1.8.29 / Collections 2.6.6 / Mathematics 1.3.3** が推移的依存として既に存在する。一方、配布 `package.json` は `update-unity6.3` / `v2.0.0` ブランチでも `"unity": "2020.3"` / 依存ゼロを維持しており、Burst/Collections 2.x(Unity 2022.3+ 必須)の追加はこの方針と衝突する。この分岐についてユーザーに確認し、以下を決定した:

- **進め方 = 段階式**。まず SoA 再設計 + `IJobParallelFor` 並列化を **Burst なし**で実装・計測し、Burst 依存の是非は計測後に別サブステップで判断する。これまでの「実装 → 計測 → コミット」の呼吸に合わせ、破壊的変更(Burst 依存追加)を最後まで遅延できる。
- **設計基準 = Unity 6.x 前提**。`package.json` の最小サポートを Unity 6.x へ引き上げ、`Unity.Mathematics`(float3/float4x4) を使って最初から **Burst 対応可能な形**で設計する。将来の 2020.3 配慮は捨てる。

**依存フットプリントの最小化**: `NativeArray<T>` / `IJobParallelFor` / `IJob` / `JobHandle` はコアモジュール(依存ゼロ)。固定ストライド 64 設計で可変長コンテナを避けるため **`com.unity.collections`(NativeList 等)は使わない**。追加する依存は `Unity.Mathematics` のみ(3a)、`com.unity.burst` は最終サブステップ(3c)。

#### 設計(新データモデル)

- **`Component` 参照 → グローバル receiverComponentIndex に統一**。現行の `rendererNo` はソース種別(MeshRenderer / SkinnedMeshRenderer / Terrain)ごとに別の index 空間で、実際のマッチングは `Component` 参照で行っている。これを受けオブジェクト配下の全コンポーネント横断の単一 index に統一し、ジョブからマネージド参照を排除、build 時のマッチングも index 比較にする。
- **`Line` 構造体を廃止**。エッジ i = (vert[i], vert[(i+1)%n]) はリング隣接で自明。`StartToEndVec` はオンザフライ計算。分割時に平面をまたぐ 2 エッジの端点データだけを分割前にローカルへ退避すれば等価。
- **per-vertex の world normal を除去**。クリップの補間 t は world position のみに依存し、faceNormal は位置から再計算、最終メッシュは model normal を使うため、per-vertex world normal は**出力に一切使われないデッドデータ**(検証済み)。クリップ作業セットは worldPos + modelPos + modelNormal + boneWeight のみに削減(メモリトラフィック削減)。
- **固定ストライド 64**(`DefaultMaxVertex`)を維持し `IJobParallelFor` を素直に書く。
- SoA 構成:
  - 受けごとの永続キャッシュ(factory が一度構築、プール保持): source model-space position/normal/boneWeight(triCount*3)、per-triangle の receiverComponentIndex、per-component の isSkinned。`NativeArray`(Persistent)。受け消滅時に Dispose。
  - per-launch 作業セット: 全三角形の worldPos(triCount*3)・faceNormal・survive フラグ、survivor の stride-64 バッファ。`NativeArray`(TempJob/Persistent プール)。

#### ジョブチェーン

1. **skinning + broadphase 融合 `IJobParallelFor`**(三角形単位・並列): ボーン行列パレットをブレンドして worldPos を計算 → faceNormal → broadphase カリング(面法線 + 平面距離 + 球-三角形距離)→ survive フラグ。boneMatricesPallet はジャグ配列を `NativeArray<float4x4>` + per-component オフセットにフラット化。
2. **コンパクション**(survive フラグの prefix-sum → survivor index、scatter で stride-64 へ集約)。prefix-sum は単スレッド(安価)、scatter は `IJobParallelFor` 可。
3. **クリップ `IJobParallelFor`**(survivor 単位・並列): 6 平面を順に適用。`SplitAndRemoveByPlane` を stride-64 NativeArray スライス上に移植(Line なし・world normal なし)。
4. **メッシュ構築**: DecalMesh(= 1 receiverComponentIndex)ごとに survivor を fan 展開 → MeshData。タンジェント計算(既存 `DecalMeshTangentCalculator` を移植)。
5. コルーチンの `_executeLaunchingOnWorkerThread` ポーリングを `JobHandle.IsCompleted` ポーリングに置換(ステートマシン・`DecalProjectorLauncher` は変更不要)。

#### サブステップ(実装順)

- [~] **3a**: 基盤 + 新 SoA データモデル + ジョブ化(**Burst 無効**)+ JobHandle 化 + テスト移植。MSBuild でコンパイル確認。**進行中**。以下の内訳:
  - [x] 基盤: `package.json` を `"unity": "6000.0"` + `com.unity.mathematics` 依存へ、`AirSticker.asmdef` に `Unity.Mathematics` 参照追加。MSBuild(VS2022)でのコンパイル検証下地を整備(`*.csproj` はワイルドカード + `Library/ScriptAssemblies/*.dll` 参照。gitignore 済みで Unity 再生成)。float3+NativeArray+IJobParallelFor+Schedule のスモークテスト通過。
  - [x] 新 SoA データモデル: `Core/Jobs/ReceiverConvexPolygonsMesh.cs`(永続ソース、`Component` → グローバル componentIndex 統一)、`Core/Jobs/DecalMeshJobBuffers.cs`(per-launch プールバッファ、`AirStickerSystem` 所有想定でライフタイム管理)。
  - [x] ジョブ実装(Burst 無効): `Core/Jobs/DecalGeometryMath.cs`(純関数: 球-三角形距離 / Unity 意味論の `NormalizeSafe`)、`Core/Jobs/SkinningBroadPhaseJob.cs`(スキニング + faceNormal + broadphase、world normal 未計算)、`Core/Jobs/ConvexPolygonClipping.cs`(`SplitAndRemoveByPlane` を stride-64 スライスへ移植、Line 廃止 / world normal 除去 / 旧実装の t 再利用の癖まで忠実再現)、`Core/Jobs/ConvexPolygonClipJob.cs`(三角形単位で seed + 6 平面クリップ、非 survivor は早期 return。compaction なしで stride-64 を三角形数で確保 = 旧 survivor 基準プールより小メモリ)。
  - [x] 差分テスト: `Assets/Tests/TestConvexPolygonClipping.cs`。旧 `ConvexPolygon.SplitAndRemoveByPlane` / `BroadPhaseConvexPolygonsDetection` と新実装を同一入力で走らせ出力一致を検証(単一平面の両ブランチ網羅 + デカールボックス 6 平面列 + 球-三角形距離)。両実装が共存する今のうちに移植の等価性を固定する。**human のエディタ実行で PASS 確認が次の関門**。
  - [x] メッシュ構築ステージのジョブ化: `Core/Jobs/DecalMeshTangents.cs`(タンジェント計算の NativeArray 移植、差分テスト `TestDecalMeshTangents.cs` 付き)+ `Core/Jobs/DecalMeshBuildJob.cs`(serial `IJob`: clip 出力 → fan 展開 + UV + zOffset + タンジェント → 追記ジオメトリの NativeArray 出力)。**オフメイン維持**(Step 2 のタンジェント排除を退行させない)のため serial IJob。出力インデックスは出力配列ローカル空間で、main が DecalMesh へ merge する際に `(既存頂点数 - 出力頂点オフセット)` を加えてメッシュ絶対 index へ変換。
  - **全 compute stage(skinning / broadphase / clip / build)実装・コンパイル済み。数値ロジック(距離・クリップ・タンジェント)は差分テストで検証済み。** 統合スイッチも完了(以下すべて実装・コンパイル済み):
  - [x] `TrianglePolygonsFactory` を新 SoA 出力へ改修: `ReceiverConvexPolygonsMesh` を構築(model 空間 position/normal/boneWeight を `NativeArray<float3>`/`BoneWeight`、per-triangle componentIndex、per-component isSkinned)。プールを `ReceiverConvexPolygonsMesh` 保持 + GC 時 / `AirStickerSystem.OnDestroy` で Dispose。
  - [x] オーケストレーター `Core/Jobs/DecalMeshJobPipeline.cs`: per-component パレット構築(`float4x4` フラット化)+ 2 セグメント(skinning+clip / build)スケジュール + カウント + merge。`BuildClipPlanes` はここへ移動。
  - [x] `AirStickerProjector`: ThreadPool ワーカー + フラグポーリングを 2 セグメント + `JobHandle.IsCompleted` ポーリングへ置換。`AirStickerPerformanceLog.Enabled` 時のみ同期 `Complete()` + Stopwatch で実 compute 時間を計測(Burst on/off 比較用)、通常時は非同期。
  - [x] `DecalMesh`: `AppendFromJobOutput`(NativeArray 出力 → 永続 CPU バッファへ append、index を `既存頂点数 - 出力オフセット` で補正)。旧 `AddTrianglePolygonsToDecalMesh` 削除。
  - [x] 旧 `ConvexPolygon`/`Line`/`BroadPhaseConvexPolygonsDetection`/ThreadPool パスを削除。ロールバック機構も削除(新設計は追記が全てジョブ完了後の main なのでゴースト発生せず不要)。
  - [x] 既存 EditMode テストを整理: `TestConvexPolygon` / `TestBroadPhaseConvexPolygonsDetection` 削除、`TestConvexPolygonClipping` を旧依存なしのスタンドアロン版(プロパティ検証 + 既知値)へ書き換え。
  - [x] **受け消滅時の use-after-free 対策**: プールの `NativeArray` 明示 Dispose により、Launch 中に受けが死ぬと GC がジョブ実行中の source を解放し得る(旧マネージド実装では無害だった新規ハザード)。`ReceiverConvexPolygonsMesh.InUse` ピンを追加し、Launch 中(schedule〜seg2 完了)はピン、GC は解除まで破棄を延期。projector 破棄時も解除。
  - 検証状況: **全 3 プロジェクト(AirSticker / Tests / Assembly-CSharp+Demo)MSBuild グリーン**。公開 API 維持。数値等価性・見た目・ジョブセーフティ(editor の collections checks)・実挙動は **human のエディタ実行が必須**(下記 3b)。
- [x] **3b(検証・計測)**: human がエディタで EditMode テスト全緑 + デモ 01〜04 の見た目に差分なし + ジョブセーフティ/例外なしを確認(2026-07-31)。代表シナリオ計測で **Burst 無効の並列化だけで約 4.9 倍**を確認(下記)。

#### 計測結果: Step 3a(2026-07-31、エディタ Mono、大きめスキンメッシュ・近似シナリオ、2回連続 Launch)

アップロードが 0.08ms/1346 頂点で Step 2 記録(0.09ms/1168 頂点)とほぼ一致するため、同一デモ・近似シナリオ(入力 8191/生存 4849 相当)と判断し Step 2 ベースラインと比較。**2回目 Launch(JIT ウォーム後)の値**で比較する(Step 0 の知見どおり 1回目は JIT ウォームアップ込みで比較に使わない — 参考: clip 11.07ms / build 3.67ms)。

| 項目 | Step 2(旧 ThreadPool・単一スレッド逐次) | Step 3a(Job 並列・**Burst 無効**) |
|---|---|---|
| skinning | 3.66 ms | ┐ |
| broadphase | 2.80 ms | ├ clip stage(3 つ融合・並列)= **1.56 ms** |
| clip | (下の clip+build に含む) | ┘ |
| build(fan+uv+tangent) | (clip+build 合計 3.17 ms) | build stage(serial・オフメイン)= **0.42 ms** |
| **ワーカー計算合計** | **9.63 ms** | **1.98 ms** |
| メッシュアップロード(main・最大) | 0.09 ms | 0.08 ms |

**分かったこと**:

1. **ワーカー計算 9.63ms → 1.98ms(約 4.9 倍)を Burst 無しの `IJobParallelFor` 並列化だけで達成。** 段階式の狙い(並列化でコア数スケール)がエディタのコア数で明確に確認できた。skinning/broadphase/clip をポリゴン単位で並列化した効果。
2. **アップロードは不変(0.09→0.08ms)。** メインスレッドのコストに変化なし(Step 2 で確立したオフメイン・タンジェント/単一 Apply を維持)。
3. **リグレッションなし・見た目差分なし・ジョブセーフティエラーなし。** 数値ロジックの差分テスト(距離・クリップ・タンジェント)も全緑。
4. 計測は `AirStickerPerformanceLog.Enabled` 時のみ同期 `Complete()` で実 compute 時間を測る方式(通常時は非同期でメイン非ブロック)。build stage は現状 serial(未並列)で、将来の並列化候補だが 0.42ms と小さい。
5. **残: IL2CPP/実機**での計測(下記で実施)。3c の Burst 有効化と合わせて確認する。

#### 計測結果: Step 3a(2026-07-31、IL2CPP / Android 実機 Pixel 8a、Demo03、3回連続 Launch)

| 項目 | 1回目(warmup) | 2回目 | 3回目 |
|---|---|---|---|
| clip stage(skinning+broadphase+clip・並列) | 10.70 ms | 1.70 ms | 2.03 ms |
| build stage(fan+uv+tangent・serial) | 0.40 ms | 0.64 ms | 0.46 ms |
| ワーカー計算合計 | 11.10 ms | **2.34 ms** | **2.49 ms** |
| メッシュアップロード(main・最大) | 3.64 ms | 0.08 ms | 0.09 ms |

**分かったこと**:

1. **実機 steady-state(2/3回目)でワーカー計算 約 2.3–2.5ms**(Burst 無効・並列ジョブ)。**これが 3c の Burst A/B の実機ベースライン。**
2. **1回目(clip 10.70ms・upload 3.64ms)は cold 要因で突出。** IL2CPP は JIT なしだが、新設計のプール `NativeArray` を 1回目に初回書き込みするため cold cache + first-touch ページフォルト + ジョブワーカー初回スピンアップが乗る(alloc は計測窓外の main 側だが、ジョブの初回書き込みが窓内)。Step 0 の旧 ThreadPool 実機計測で 1回目突出が無かったのは、旧実装が確保済みマネージドバッファを再利用していたため。**steady-state 比較では 1回目を使わない**。
3. **build stage は実機でも 0.4–0.6ms と小さく**、serial(未並列)でもボトルネックでない。将来並列化の優先度は低い。
4. 旧パイプライン削除済みで同一シナリオの実機旧値は取得不可。計画 Step 0 の実機ログ(アロケ除去後の単一スレッド推定 ~20–28ms)との対比では、実機の多コア(Tensor G3)で並列化効果がエディタ(4.9x)以上に出ている見込み。

**判断**: Step 3a の目的(SoA + Job 化・Burst 無効の並列化効果の確認)をエディタ・実機の両方で達成、リグレッションなし・見た目差分なし。**コミット可**。
- [ ] **3c**: `com.unity.burst` 依存追加 + ジョブに `[BurstCompile]` 付与 + asmdef に `Unity.Burst` 参照。Burst 有効/無効の A/B を計測。効果を確認してコミット。
- [ ] **3d**: メジャーバージョンアップ(`package.json` を 2.0.0 へ)としてリリース。残課題(下記 4 件)もこのタイミングで解消。

**着手判断(記録)**: Step 1-2 だけで既にワーカー計算は 65.7ms → 8.65ms(目標 1/10 近く)に到達済みで、単発デカール用途では体感差は出ない。Step 3 の主効果は「多数同時 / 大規模スキンメッシュ」でのコア数スケールと IL2CPP での Burst SIMD。段階式にしたのは、この効果を計測で確かめてから破壊的な Burst 依存を判断するため。

#### 補足: MSBuild コンパイル検証について

エディタ起動中は Unity ヘッドレステスト実行が静かに失敗するため、実装中の検証は VS2022 MSBuild で `AirSticker.csproj` をビルドする方式を使う(数値等価性・見た目は human のエディタ実行が必要)。`*.csproj` は `.gitignore` 済み(Unity 再生成)なので、asmdef 変更後の csproj 同期のため一時的に `<Reference>` を手動追加してよい。`Unity.Mathematics.dll` 等は `Library/ScriptAssemblies/` にコンパイル済み。

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
