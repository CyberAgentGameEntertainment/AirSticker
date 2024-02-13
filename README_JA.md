<p align="center">
<img src="Documentation/AirSticker_logo_color.png#gh-light-mode-only" alt="AirSticker">
<img src="Documentation/AirSticker_logo_dark.png#gh-dark-mode-only" alt="AirSticker">
</p>

# Air Sticker
[![license](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)
[![license](https://img.shields.io/badge/PR-welcome-green.svg)](hogehoge)
[![license](https://img.shields.io/badge/Unity-2020.3-green.svg)](#Requirements)


**ドキュメント** ([English](README.md), [日本語](README_JA.md)) <br/>
**技術ドキュメント** ([English](README_DEVELOPERS.md), [日本語](README_DEVELOPERS_JA.md)) <br/>

## Section 1 概要
Air StickerはURPのデカールのデメリットを補完するものとなっており、非常に軽量に動作するデカール処理です。<br/>
また、URPデカールはUnity2021以降でしか使えませんが、Air StickerはUnity2020からの動作をサポートします。<br/>

<br/>
<p align="center">
<img src="Documentation/fig-014.gif"><br>
</p>

<br/>
<p align="center">
<img src="Documentation/fig-015.gif"><br>
</p>

## Section 2 特徴
Air Stickerには多くのゲームで採用されている、典型的なメッシュ生成方式によるデカール処理が実装されています。<br/><br/>
メッシュ生成デカールは、デカールを貼り付ける対象のモデルに添った形状のメッシュをランタイムで生成して、そこにテクスチャを貼り付けることでデカール表現を実現します。<br/><br/>
一方Unityで実装されているデカール処理は、投影方式のDBufferデカールとスクリーンスペースデカールが実装されています。<br/><br/>
メッシュ生成方式と投影方式のデカールは、双方メリット/デメリットを持っています。<br/>
また、メッシュ生成方式と投影方式を併用すると、多くのデメリットを補うこともできます(詳細は2.1、2.2を参照)。<br/>

### 2.1 URPデカールとAir Stickerのメリットとデメリット
URPデカールとAir Stickerのメリット/デメリットは次のようになっています。

- **URPデカール**
  - **メリット**
    - デカールを貼る処理が高速
    - Zファイティングが起きない
  - **デメリット**
    - 完全なスキンアニメーション対応が難しい ( Air Stickerで補完できる )
    - ピクセル負荷が高い ( Air Stickerで補完できる )
    - カスタムシェーダーはそのままでは使えない( Air Stickerで補完できる )
- **Air Sticker**
  -  **メリット**
     - 処理が軽量( ただし、デカールメッシュ生成はラグがある )
     - 完全なスキンアニメーションを行える
     - カスタムシェーダーをそのまま使える
  - **デメリット**
    - デカールを貼る処理に数フレームかかる ( URPデカールで補完できる )
    - Zファイティングが起きる

このように、二つのデカールを併用することで、多くのデメリットを補完できます。<br/>

### 2.2 URPデカールとAir Stickerの併用
前節で見たように、二つのデカールの処理を併用することで、多くのデメリットを補完できます。<br/><br/>
ここでは併用の仕方として、次のモデルケースを提示します。

|手法|使用ケース|
|---|---|
|URPデカール| ・ オブジェクト座標系でデカールが移動する<br/> ・ Air Stickerによるメッシュ生成が終わるまでの時間稼ぎ|
|Air Sticker|オブジェクト座標系でデカールが移動しない|

下記の動画はこのモデルケースで実装しているプレイデモになります。

<br/>
<p align="center">
<img width="80%" src="Documentation/fig-001-ja.gif" alt="URPデカールとAir Stickerの使い分け"><br>
<font color="grey">URPデカールとAir Stickerの使い分け</font>
</p>

この動画ではレシーバーオブジェクト上でデカールが移動する場合と、メッシュ生成完了までの時間稼ぎの用途でURPデカールを使っています。<br/>
レシーバーオブジェクト上での位置が確定して、メッシュ生成が終わると、以降はAir Stickerによるデカールを表示しています。<br/>

メッシュ生成完了後からはAir Stickerを使用することによって、ランタイムパフォーマンスを大きく改善できます(詳細は2.3を参照)。<br/>

### 2.3 URPデカールとAir Stickerの描画パフォーマンス
Air Stickerはメッシュ生成に数フレームかかりますが、描画パフォーマンスは単なるメッシュ描画と同じです。<br/>
一方、URPデカールはメッシュ生成を行う必要はありませんが、デカール表示のために複雑な描画処理が実行されます。<br/><br/>
そのため、毎フレームの描画パフォーマンスではAir Stickerの方が有利になります。<br/><br/>
次の図はURPデカールとAir Stickerの描画パフォーマンスの計測結果です。<br/>
最も顕著に差がでたケースでは19ミリ秒ものパフォーマンスの向上が確認されています。
<p align="center">
<img width="80%" src="Documentation/fig-002.png" alt="パフォーマンス計測結果"><br>
<font color="grey">パフォーマンス計測結果</font>
</p>

## Section 3 インストール
インストールは以下の手順で行います。

1. Window > Package Manager を選択
2. 「+」ボタン > Add package from git URL を選択
3. 以下を入力してインストール
   * https://github.com/CyberAgentGameEntertainment/AirSticker.git?path=/Assets/AirSticker

<p align="center">
  <img width="60%" src="https://user-images.githubusercontent.com/47441314/143533003-177a51fc-3d11-4784-b9d2-d343cc622841.png" alt="Package Manager">
</p>

あるいはPackages/manifest.jsonを開き、dependenciesブロックに以下を追記します。

```json
{
    "dependencies": {
        "jp.co.cyberagent.air-sticker": "https://github.com/CyberAgentGameEntertainment/AirSticker.git?path=/Assets/AirSticker"
    }
}
```

バージョンを指定したい場合には以下のように記述します。

* https://github.com/CyberAgentGameEntertainment/AirSticker.git?path=/Assets/AirSticker#1.0.0

なお`No 'git' executable was found. Please install Git on your system and restart Unity`のようなメッセージが出た場合、マシンにGitをセットアップする必要がある点にご注意ください。

バージョンを更新するには上述の手順でバージョンを書き換えてください。  
バージョンを指定しない場合には、package-lock.jsonファイルを開いて本ライブラリの箇所のハッシュを書き換えることで更新できます。

```json
{
  "dependencies": {
      "jp.co.cyberagent.air-sticker": {
      "version": "https://github.com/CyberAgentGameEntertainment/AirSticker.git?path=/Assets/AirSticker",
      "depth": 0,
      "source": "git",
      "dependencies": {},
      "hash": "..."
    }
  }
}
```

## Section 4 使用方法
Air Stickerを使用するには次の２つのクラスが重要になってきます。
1. AirStickerSystemクラス
2. AirStickerProjectorクラス

### 4.1 AirStickerSystemクラス
Air Stickerを利用するためには、必ず、このコンポーネントが貼られたゲームオブジェクトを一つ設置する必要があります。

<p align="center">
<img width="50%" src="Documentation/fig-013.png" alt="AirStickerSystem"><br>
<font color="grey">AirStickerSystem</font>
</p>

### 4.2 AirStickerProjectorクラス
デカールを投影するためのコンポーネントです。デカールプロジェクタとして設置するゲームオブジェクトにこのコンポーネントを追加してください。

<p align="center">
<img width="50%" src="Documentation/fig-004.png" alt="AirStickerProjectorのインスペクタ"><br>
<font color="grey">AirStickerProjectorのインスペクタ</font>
</p>

AirStickerProjectorコンポーネントには5つのパラメータを設定することができます。
|パラメータ名|説明|
|---|---|
|Width|Projector バウンディングボックスの幅です。URPのデカールプロジェクタの仕様に準拠しています。<br/>詳細は[URPデカールのマニュアル](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal.html)を参照してください。 |
|Height|Projector バウンディングボックスの高さです。URPのデカールプロジェクタの仕様に準拠しています。<br/>詳細は[URPデカールのマニュアル](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal.html)を参照してください。|
|Depth|Projector バウンディングボックスの深度です。URPのデカールプロジェクタの仕様に準拠しています。<br/>詳細は[URPデカールのマニュアル](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal.html)を参照してください。|
|Receiver Objects| デカールテクスチャの貼り付け対象となるオブジェクト。<br/>AirStickerProjectorは設定されているレシーバーオブジェクトの子供(自身を含む)に貼られている全てのレンダラーを貼り付け対象とします。<br/><br/>そのため、レシーバーオブジェクトはMeshRendererやSkinMeshRendererなどのコンポーネントが貼られているオブジェクトを直接指定もできますし、レンダラーが貼られているオブジェクトを子供に含んでいるオブジェクトの指定でも構いません。<br/>処理するレンダラーの数が多いほど、デカールメッシュ生成の時間がかかるようになるため、貼り付ける範囲を限定できるときは、レンダラーが貼り付けられているオブジェクトの直接指定が推奨されます。<br/><br/>例えば、キャラエディットなどでキャラクターの顔にステッカーを貼り付けたい場合、キャラのルートオブジェクトを指定するよりも顔のレンダラーが貼られているオブジェクトを指定するとメッシュ生成の時間を短縮できます。|
|Z Offset In Decal Space|デカールを貼り付けるサーフェイスの空間でのZオフセットです。この値を調整することで、Zファイティングを軽減することができます。|
|Decal Material| デカールマテリアル。<br/>URPのデカールマテリアルとは意味あいが違うので注意してください。<br/>URPデカールではShader Graphs/Decalシェーダーが割り当てられたマテリアルしか使えません。<br/>しかし、Air Stickerでは通常のマテリアルが使えます。<br/>つまり、ビルトインのLitシェーダー、Unlitシェーダー、そして、ユーザーカスタムの独自シェーダーも利用できます。|
|Projection Backside|このチェックボックスにチェックが入っていると、裏面のメッシュにもデカールメッシュが投影されます。|
|Launch On Awake|このチェックボックスにチェックが入っていると、インスタンスの生成と同時にデカールの投影処理が開始されます。|
|On Finished Launch|デカールの投影終了時に呼び出されるコールバックを指定できます。|

次の動画はAirStickerProjectorをシーンに設置して使用する方法です。
<p align="center">
<img width="80%" src="Documentation/fig-012-ja.gif" alt="AirStickerProjectorの使用方法"><br>
<font color="grey">AirStickerProjectorの使用方法</font>
</p>


### 4.3 ランタイムでのAirStickerProjectorの生成
デカールのランタイムでの使用例として、FPSなどの弾痕を背景に貼り付ける処理があります。このような処理をAir Stickerで行うためには、背景と銃弾との衝突判定を行い、衝突点の情報を元にAirStickerProjectorコンポーネントを生成して、デカールメッシュを構築することで実現できます。<br/><br/>
AirStickerProjectorコンポーネントはAirStickerProjector.CreateAndLaunch()メソッドを呼び出すことで生成できます。</br>
CreateAndLaunch()メソッドのlaunchAwake引数にtrueを指定すると、コンポーネントの生成と同時にデカールメッシュの構築処理が開始されます。<br/><br/>
デカールメッシュの構築は非同期で行われます。そのため、デカールメッシュの構築処理の終了を監視したい場合は、AirStickerProjectorのNowStateプロパティを監視するか、メッシュ生成処理の終了時に呼び出しされる、onFinishedLaunchコールバックを利用する必要があります。<br/><br/>
次のコードは、AirStickerProjector.CreateAndLaunch()メソッドを利用して弾痕を背景に貼り付けるための疑似コードです。この疑似コードでは、CreateAndLaunch()メソッドの引数を使って終了を監視するコールバックを設定しています。<br/>
```C#
// hitPosition    弾丸と背景の衝突点
// hitNormal      衝突した面の法線
// receiverObject デカールを貼り付けるレシーバーオブジェクト
// decalMaterial  デカールマテリアル
void LaunchProjector( 
  Vector3 hitPosition, Vector3 hitNormal, 
  GameObject receiverObject, Material decalMaterial )
{
    var projectorObject = new GameObject("Decal Projector");
    // 法線方向に押し戻した位置にプロジェクターを設置。
    projectorObject.transform.position = hitPosition + hitNormal;
    // プロジェクターの向きは法線の逆向き
    projectorObject.transform.rotation = Quaternion.LookRotation( hitNormal * -1.0f );

    AirStickerProjector.CreateAndLaunch(
                    projectorObj,
                    receiverObject,
                    decalMaterial,
                    /*width=*/0.05f,
                    /*height=*/0.05f,
                    /*depth=*/0.2f
                    /*launchOnAwake*/true,
                    /*onCompletedLaunch*/() => { Destroy(projectorObj); });
}
```

***
<p align="right">
© Unity Technologies Japan/UC
</p>


