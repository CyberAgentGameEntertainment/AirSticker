
# CyDecal(仮)技術ドキュメント

## Section 1 概要
このドキュメントはCyDecalの内部で使われているアルゴリズムなどの詳細を説明する、エンジニア向けのドキュメントです。<br/>

また、エンドユーザー向けのCyDecalの使用方法に関するドキュメントは下記を参照してください。<br/>

**使用方法** ([日本語](README.md))


## Section 2 アルゴリズム概要
CyDecalはデカールテクスチャを貼り付けるデカールメッシュを動的に生成して、デカール表現を行っています。<br/>
次の図のように、キャラクターにステッカーをデカールとして貼り付ける場合、そのステッカーがモデルに綺麗に添うようなデカールメッシュを動的に生成します。<br/>

<p align="center">
<img width="60%" src="Documentation/fig-000.png" alt="生成されたデカールメッシュ"><br>
<font color="grey">生成されたデカールメッシュ</font>
</p>


デカールメッシュの作成アルゴリズムのステップは次のようになっています。
1. デカールメッシュプールから編集するデカールメッシュを取得
2. デカールテクスチャを貼り付けるレシーバーオブジェクトのレンダラーから三角形ポリゴンスープを取得
3. デカールテクスチャを貼り付ける三角形ポリゴンの早期枝切り(ブロードフェーズ)
4. デカールボックスと三角形ポリゴンとの衝突判定
   1. 衝突していなければ終了
5. デカールボックスの情報から分割平面を定義
6. 5で定義した分割平面で衝突する三角形ポリゴンを分割して、多角形ポリゴンにしていく
7. 6で作られた多角形ポリゴン情報を元に、デカールメッシュを生成

また、4～7のポリゴン分割ついては「ゲームプログラミングのための3D数学」の「9.2デカールの貼り付け」を参考にしているため、アルゴリズムの概要と関連ソースコードの記述のみにとどめます。ポリゴン分割の詳細については参考文献を参照して下さい。

## Section 3 アルゴリズム詳細
Section 3では各種ステップの詳細を説明してきます。
### 3.1 デカールメッシュプールから編集するデカールメッシュを取得
CyRenderDecalFeatureが保持しているデカールメッシュプールから編集するデカールメッシュを取得します。<br/>
デカールメッシュはレシーバーオブジェクト、レンダラー、マテリアルのハッシュ値をキーとして、プールに登録されており、この値が同一であれば使いまわしされます。
また、このハッシュ値がプールに登録されていなければ、新しくデカールメッシュを作成します。<br/>
そのため、次のようなデカールの場合は一つのデカールメッシュとして扱われています。

<p align="center">
<img width="60%" src="Documentation/fig-008.png" alt="一つのデカールメッシュ"><br>
<font color="grey">一つのデカールメッシュ</font>
</p>
また、次のようなケースであれば、レシーバーオブジェクト、レンダラーは同じですが、デカールマテリアルが異なるため、二つのデカールメッシュとして扱われています。

<p align="center">
<img width="60%" src="Documentation/fig-009.png" alt="二つのデカールメッシュ"><br>
<font color="grey">二つのデカールメッシュ</font>
</p>


デカールメッシュの数＝ドローコールの数です。そのため、デカールメッシュの種類を減らすことが最適化の一つの指針になります。

[**プールからデカールメッシュを取得しているコード**]
```C#
/// <summary>
///     デカールメッシュのリストを取得
/// </summary>
/// <remarks>
///     デカールメッシュは貼り付けるターゲットオブジェクトとデカールマテリアルが同じ場合に共有されます。
///     また、全く新規のターゲットオブジェクトとマテリアルであれば、
///     新規のデカールメッシュを作成します。
/// </remarks>
/// <param name="decalMeshes">デカールメッシュの格納先</param>
/// <param name="projectorObject">デカールプロジェクター</param>
/// <param name="receiverObject">デカールを貼り付けるターゲットオブジェクト</param>
/// <param name="decalMaterial">デカールマテリアル</param>
/// <returns></returns>
public void GetDecalMeshes(
    List<CyDecalMesh> decalMeshes,
    GameObject projectorObject,
    GameObject receiverObject,
    Material decalMaterial)
{
    var renderers = receiverObject.GetComponentsInChildren<Renderer>();
    foreach (var renderer in renderers)
    {
        var hash = receiverObject.GetInstanceID()
                           + decalMaterial.name.GetHashCode()
                           + renderer.GetInstanceID();
        if (_decalMeshes.ContainsKey(hash))
        {
            decalMeshes.Add(_decalMeshes[hash]);
        }
        else
        {
            var newMesh = new CyDecalMesh(projectorObject, decalMaterial, renderer);
            decalMeshes.Add(newMesh);
            _decalMeshes.Add(hash, newMesh);
        }
    }
}
```

**関連ソースコード**<br/>
[Assets/CyDecal/Runtime/Scripts/Core/CyDecalMeshPool.cs](Assets/CyDecal/Runtime/Scripts/Core/CyDecalMeshPool.cs)<br/>
[Assets/CyDecal/Runtime/Scripts/Core/CyDecalMesh.cs](Assets/CyDecal/Runtime/Scripts/Core/CyDecalMesh.cs)

### 3.2 デカールを貼り付けるレシーバーオブジェクトの三角形ポリゴンスープを取得
CyRenderDecalFeatureが保持している三角形ポリゴンスープのプールからレシーバオブジェクトの三角形ポリゴンスープを取得します。<br/>
このプールはレシーバーオブジェクトをキーとして、三角形ポリゴンスープが登録されており、すでに登録済みの場合は、使いまわしされます。また、新規のレシーバーオブジェクトであれば、レンダラーの情報から三角形ポリゴンスープが作成されます。<br/><br/>
ポリゴンスープが保持している頂点はワールド空間に変換されている必要があるため、メッシュの全頂点を空間変換するための行列演算が行われます。そのため、この処理は(特にスキンメッシュのモデル)非常に時間のかかるものとなっています。この処理によるスパイクを隠ぺいするために、ポリゴンスープの作成は数フレームにわたって分割して処理が実行されます。<br/><br/>
この処理が実行されるため、初めてデカールテクスチャを貼り付けるレシーバーオブジェクトが登録されるときのみ、デカール貼り付け完了までに遅延が発生します。ただし、レシーバーオブジェクトがワールド空間上移動した場合は再作成を行う必要があるため、再度遅延が発生します。<br/><br/>
次の図はポリゴンスーププールを可視化したものです。

<p align="center">
<img width="60%" src="Documentation/fig-010.png" alt="ポリゴンスーププール"><br>
<font color="grey">ポリゴンスーププール</font>
</p>

[**メッシュフィルターから三角形ポリゴン情報を収集しているコード**]
```C#
/// <summary>
///     MeshFilterから凸ポリゴン情報を登録する。
/// </summary>
/// <param name="meshFilters">レシーバーオブジェクトのメッシュフィルター</param>
/// <param name="meshRenderers">レシーバーオブジェクトのメッシュレンダラー</param>
/// <param name="convexPolygonInfos">凸ポリゴン情報の格納先</param>
private static IEnumerator BuildFromMeshFilter(MeshFilter[] meshFilters, MeshRenderer[] meshRenderers,
    List<ConvexPolygonInfo> convexPolygonInfos)
{
        ・
        ・
    　 省略
        ・
        ・
    foreach (var meshFilter in meshFilters)
    {
        var localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
        // メッシュのポリゴン情報を取得
        var mesh = meshFilter.sharedMesh;
        var numPoly = mesh.triangles.Length / 3;
        var meshTriangles = mesh.triangles;
        var meshVertices = mesh.vertices;
        var meshNormals = mesh.normals;
        for (var i = 0; i < numPoly; i++)
        {
            if ((newConvexPolygonNo + 1) % MaxGeneratedPolygonPerFrame == 0)
                // 1フレームに処理するポリゴンは最大でMaxGeneratedPolygonPerFrameまで
                yield return null;

                // メッシュのポリゴン情報をワールド空間に変換していく。
                    ・
                    ・
                   省略
                    ・
                    ・
            // 凸ポリゴン情報を追加する。
            newConvexPolygonInfos[newConvexPolygonNo] = new ConvexPolygonInfo
            {
                ConvexPolygon = new CyConvexPolygon(
                    vertices,
                    normals,
                    boneWeights,
                    meshRenderers[rendererNo])
            };
            newConvexPolygonNo++;
        }

        rendererNo++;
    }

    convexPolygonInfos.AddRange(newConvexPolygonInfos);
}
```
**関連ソースコード**<br/>
[Assets/CyDecal/Runtime/Scripts/Core/CyReceiverObjectTrianglePolygonsPool.cs](Assets/CyDecal/Runtime/Scripts/Core/CyReceiverObjectTrianglePolygonsPool.cs)<br/>
[Assets/CyDecal/Runtime/Scripts/Core/CyTrianglePolygonsFactory.cs](Assets/CyDecal/Runtime/Scripts/Core/CyTrianglePolygonsFactory.cs)
<br/>
[Assets/CyDecal/Runtime/Scripts/Core/CyConvexPolygon.cs](Assets/CyDecal/Runtime/Scripts/Core/CyConvexPolygon.cs)

### 3.3 デカールを貼り付ける三角形ポリゴンの早期枝切り(ブロードフェーズ)
このステップでは、デカールボックスの起点となる座標と各ポリゴンの頂点との距離の計算により、このステップ以降に処理する三角形ポリゴンを早期枝切りするためのブロードフェーズが実行されます。<br/>
ブロードフェーズによる安価な計算による早期枝切りが行われることによって、後のステップの複雑な処理の計算量を下げることができるため、大幅な高速化が期待できます。

また、デカールボックスとは、デカールを貼り付ける空間を現わすボックスです。<br/>
<p align="center">
<img width="60%" src="Documentation/fig-011.png" alt="デカールボックス"><br>
<font color="grey">デカールボックス</font>
</p>

[**早期枝切を行っているコード**]
```C#
// 三角形ポリゴン情報でのループ
foreach (var convexPolygonInfo in convexPolygonInfos)
{
    if (Vector3.Dot(decalSpaceNormalWs, convexPolygonInfo.ConvexPolygon.FaceNormal) < 0)
    {
        // デカールボックスの向きと真逆を向いているポリゴン。
        // 枝切りの印をつける。
        convexPolygonInfo.IsOutsideClipSpace = true;
        continue;
    }

    var v0 = convexPolygonInfo.ConvexPolygon.GetVertexPosition(0);
    v0 -= originPosInDecalSpace;
    if (v0.sqrMagnitude > threshold)
    {
        var v1 = convexPolygonInfo.ConvexPolygon.GetVertexPosition(1);
        v1 -= originPosInDecalSpace;
        if (v1.sqrMagnitude > threshold)
        {
            var v2 = convexPolygonInfo.ConvexPolygon.GetVertexPosition(2);
            v2 -= originPosInDecalSpace;
            if (v2.sqrMagnitude > threshold)
                // 全ての頂点が範囲外。
                convexPolygonInfo.IsOutsideClipSpace = true;
        }
    }
}
```

### 3.4 デカールボックスと三角形ポリゴンとの衝突判定
このステップでは、デカールボックスの起点からボックスが向いている方向に向かってレイを飛ばして、衝突点を検出します<br/>
ここで衝突しない場合は以下の処理はスキップされて、デカールは貼り付けられません。<br/>

[**衝突判定しているコード**]
```C#
/// <summary>
///     デカールボックスの中心を通るレイとレシーバーオブジェクトの三角形オブジェクトの衝突判定を行う。
/// </summary>
/// <param name="hitPoint">衝突点の格納先</param>
/// <returns>trueが帰ってきたら衝突している</returns>
private bool IntersectRayToTrianglePolygons(out Vector3 hitPoint)
{
    hitPoint = Vector3.zero;
    // レイの作成
    var trans = transform;
    var rayStartPos = trans.position;
    var rayEndPos = rayStartPos + trans.forward * depth;
    // 枝切りされたポリゴン情報に対して衝突検出を行う。
    foreach (var triPolyInfo in _broadPhaseConvexPolygonInfos)
        if (triPolyInfo.ConvexPolygon.IsIntersectRayToTriangle(out hitPoint, rayStartPos, rayEndPos))
        {
            _basePointToNearClipDistance = Vector3.Distance(rayStartPos, hitPoint);
            _basePointToFarClipDistance = depth - _basePointToNearClipDistance;
            return true;
        }

    return false;
}
```

**関連ソースコード**<br/>
[Assets/CyDecal/Runtime/Scripts/CyDecalProjector.cs](Assets/CyDecal/Runtime/Scripts/CyDecalProjector.cs)<br/>
[Assets/CyDecal/Runtime/Scripts/Core/CyConvexPolygon.cs](Assets/CyDecal/Runtime/Scripts/Core/CyConvexPolygon.cs)


### 3.5 デカールボックスの情報から分割平面を定義
続いて、衝突点の情報とデカールボックスの幅、高さなどの情報を元に、デカールボックスを構築する6平面の情報を構築します。分割平面の定義の詳細は「ゲームプログラミングのための3D数学」の「9.2.1 デカールメッシュの構築」を参照してください。

[**分割平面を定義しているコード**]
```C#
private void BuildClipPlanes(Vector3 basePoint)
{
    var trans = transform;
    var decalSpaceTangentWS = _decalSpace.Ex;
    var decalSpaceBiNormalWS = _decalSpace.Ey;
    var decalSpaceNormalWS = _decalSpace.Ez;
    // Build left plane.
    _clipPlanes[(int)ClipPlane.Left] = new Vector4
    {
        x = decalSpaceTangentWS.x,
        y = decalSpaceTangentWS.y,
        z = decalSpaceTangentWS.z,
        w = width / 2.0f - Vector3.Dot(decalSpaceTangentWS, basePoint)
    };
        ・
        ・
       省略
        ・
        ・
    // Build back plane.
    _clipPlanes[(int)ClipPlane.Back] = new Vector4
    {
        x = decalSpaceNormalWS.x,
        y = decalSpaceNormalWS.y,
        z = decalSpaceNormalWS.z,
        w = _basePointToFarClipDistance - Vector3.Dot(decalSpaceNormalWS, basePoint)
    };
}
```
**関連ソースコード**<br/>
[Assets/CyDecal/Runtime/Scripts/CyDecalProjector.cs](Assets/CyDecal/Runtime/Scripts/CyDecalProjector.cs)

### 3.6 5で定義した分割平面で衝突する三角形ポリゴンを分割して、多角形ポリゴンにしていく
ここでは、三角形ポリゴンの各辺と６枚の分割平面との交差を判定を行って分割していき、凸多角形ポリゴンにしていきます。三角形ポリゴンの分割の詳細は「ゲームプログラミングのための3D数学」の「9.2.2 ポリゴンのクリッピング」を参照してください。
<p align="center">
<img width="80%" src="Documentation/fig-007.png" alt="凸多角形ポリゴンを三角形ポリゴンとして扱う"><br>
<font color="grey">凸多角形ポリゴンを三角形ポリゴンとして扱う</font>
</p>

**関連ソースコード**<br/>
[Assets/CyDecal/Runtime/Scripts/CyDecalProjector.cs](Assets/CyDecal/Runtime/Scripts/CyDecalProjector.cs)

### 3.7 6で作られた多角形ポリゴン情報を元に、デカールメッシュを生成
三角形ポリゴンの分割で得られた、凸多角形ポリゴンの頂点情報を元に、三角形ポリゴンを生成していき、最終的なデカールメッシュを生成します。凸多角形ポリゴンはトライアングルファンの三角形の集合と扱うことができるため、この特性を利用して、デカールメッシュに新たな三角形を追加していきます。凸多角形ポリゴンから三角形ポリゴンの構築の詳細は「ゲームプログラミングのための3D数学」の「9.2.2 ポリゴンのクリッピング」を参照してください。
<p align="center">
<img width="80%" src="Documentation/fig-006.png" alt="凸多角形ポリゴンを三角形ポリゴンとして扱う"><br>
<font color="grey">凸多角形ポリゴンを三角形ポリゴンとして扱う</font>
</p>

**関連ソースコード**<br/>
[Assets/CyDecal/Runtime/Scripts/Core/CyDecalMesh.cs](Assets/CyDecal/Runtime/Scripts/Core/CyDecalMesh.cs)

