<p align="center">
<img src="Documentation/header.png" alt="CyCelLook(仮)">
</p>

# CyDecal(仮)

## Section 1 概要
CyDecalはURPのデカールのデメリットを補完するものとなっており、非常に軽量に動作するデカール処理です。<br/>
なお、エンジニア向けの技術ドキュメントは下記を参照してください。<br/>
**技術ドキュメント** ([日本語](README_DEVELOPERS.md))

## Section 2 特徴
CyDecalは多くのゲームで採用されている、メッシュ生成方式による古典的なデカール処理です。一方Unityで実装されているデカール処理はディファードデカールと呼ばれる処理です。<br/>
メッシュ生成方式とディファードデカールは、双方メリット/デメリットを持っており、両方を併用することで、多くのデメリットを相殺することができます。<br/>
使い分けの方針として、次の指針に従うことをおススメします。

|手法|使用ケース|
|---|---|
|URPデカール| ・ レシーバーオブジェクト空間でデカールが移動する<br/> ・ CyDecalによるメッシュ生成が終わるまでの時間稼ぎ|
|CyDecal|レシーバーオブジェクト空間でデカールが移動しない|

下記の動画はURPプロジェクターとCyDecalの使い分けを分かりやすく説明しているものとなります。

<br/>
<p align="center">
<img width="80%" src="Documentation/fig-001.gif" alt="URPデカールとCyDecalの使い分け"><br>
<font color="grey">URPデカールとCyDecalの使い分け</font>
</p>

この動画ではレシーバーオブジェクト上でデカールが移動する場合と、メッシュ生成による時間稼ぎの用途でURPデカールを使っています。<br/>
レシーバーオブジェクト上での位置が確定して、メッシュ生成が終わると、以降はCyDecalによるデカールを表示しています。<br/>


### 2.1 URPデカールとCyDecalのメリットとデメリット
URPデカールは投影式のデカール処理となっており、下記のようなメリット/デメリットがあります。
- **URPデカール**
  - **メリット**
    - デカールを貼る処理が高速
    - Zファイティングが起きない
  - **デメリット**
    - 完全なスキンアニメーション対応が難しい ( CyDecalで相殺できる )
    - ピクセル負荷が高いため、デカールをズームアップすると大きな処理落ちが発生する ( CyDecalで相殺できる )
    - カスタムシェーダーはそのままでは使えない( CyDecalで相殺できる )
- **CyDecal**
  -  **メリット**
     - 処理が軽量(ただし、デカールメッシュ生成は重い)
     - 完全なスキンアニメーションを行える
     - カスタムシェーダーをそのまま使える
  - **デメリット**
    - デカールを貼る処理に時間がかかる ( URPデカールで相殺できる )
    - Zファイティングが起きる

このように、二つのデカールを併用することで、多くのデメリットを相殺することができます。<br/>
特にCyDecalを使用することによって、モバイルゲームにおいて、致命的となるランタイムパフォーマンスの悪化という問題を大きく改善することができます。<br/>
次の図はURPデカールとCyDecalのランタイムパフォーマンスの計測結果です。<br/>
全てのケースで、CyDecalが優位な結果が出ており、最も顕著に差がでたケースでは19ミリ秒もの高速化が確認されています。
<p align="center">
<img width="80%" src="Documentation/fig-002.png" alt="パフォーマンス計測結果"><br>
<font color="grey">パフォーマンス計測結果</font>
</p>


## Section 3 使用方法
CyDecalはAssets/CyDecalフォルダを自身のプロジェクトに取り込むことで利用できるようになります。<br/>
その中でも次の２つのファイルが重要になってきます。
1. CyRenderDecalFeature.cs
2. CyDecalProjector.cs

### 3.1 CyRenderDecalFeature.cs
URPのレンダリングパイプラインにデカール描画の機能を追加するためのスクリプトです。<br/>
CyDecalを利用する場合は、必ずこのスクリプトをURPレンダラーに追加する必要があります。<br/>

<p align="center">
<img width="50%" src="Documentation/fig-003.png" alt="feature追加"><br>
<font color="grey">feature追加</font>
</p>

### 3.2 CyDecalProjector.cs
デカールを投影するためのコンポーネントです。デカールプロジェクタとして設置するゲームオブジェクトにこのコンポーネントを追加してください。
<p align="center">
<img width="50%" src="Documentation/fig-004.png" alt="CyDecalProjectorのインスペクタ"><br>
<font color="grey">CyDecalProjectorのインスペクタ</font>
</p>
CyDecalProjectorコンポーネントには5つのパラメータを設定することができます。
|パラメータ名|説明|
|---|---|
|Width|Projector バウンディングボックスの幅です。URPのデカールプロジェクタの仕様に準拠しています。<br/>詳細は[URPデカールのマニュアル](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal.html)を参照してください。 |
|Height|Projector バウンディングボックスの高さです。URPのデカールプロジェクタの仕様に準拠しています。<br/>詳細は[URPデカールのマニュアル](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal.html)を参照してください。|
|Depth|Projector バウンディングボックスの深度です。URPのデカールプロジェクタの仕様に準拠しています。<br/>詳細は[URPデカールのマニュアル](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal.html)を参照してください。|
|Receiver Object| デカールテクスチャを貼り付ける対象となるオブジェクト。|
|Decal Material| デカールマテリアル。<br/>URPのデカールマテリアルとは意味あいが違うので注意してください。<br/>URPデカールではShader Graphs/Decalシェーダーが割り当てられたマテリアルしか使えません。<br/>しかし、CyDecalでは通常のマテリアルが使えます。<br/>つまり、ビルトインのLitシェーダー、Unlitシェーダー、そして、ユーザーカスタムの独自シェーダーも利用することができます。|





