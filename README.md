<p align="center">
<img src="Documentation/header.png" alt="CyDecal(仮)">
</p>

# CyDecal(仮)
[![license](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE.md)
[![license](https://img.shields.io/badge/PR-welcome-green.svg)](hogehoge)
[![license](https://img.shields.io/badge/Unity-2020.3-green.svg)](#Requirements)

**Documents** ([English](README.md), [日本語](README_JA.md)) <br/>
**Technical Documents** ([English](README_DEVELOPERS.md), [日本語](README_DEVELOPERS_JA.md)) <br/>

## Section 1 Summary
CyDecal is a decal system that addresses the limitations of URP decals and has a low impact on performance.<br/>
Also, URP decals can only be used with Unity2021 or higher, while CyDecal supports operation with Unity2020 or higher.<br/>

## Section 2 Feature
CyDecal implements decal processing using the typical mesh generation method used in many games.<br/>

Mesh-generated decals realize decal expression by generating a mesh at runtime with a shape that matches the model to which the decal is to be applied, and then applying a texture to it.<br/>

On the other hand, the decal process implemented in Unity implements projected DBuffer decals and screen space decals.<br/>

Both mesh generation and projection decals have advantages/disadvantages.<br/>
The mesh generation and projection methods can also be used together to compensate for many disadvantages. (see section 2.1 and 2.2 for details).<br/>

### 2.1 Advantages and Disadvantages of URP Decal and CyDecal
The advantages/disadvantages of URP decals and CyDecal are as follows.

- **URP Decal**
  - **Advantages**
    - Fast-applied decal.
    - Z-fighting doesn't happen.
  - **Demerit**
    - Difficult to support full skin animation. ( Can be complemented with CyDecal. )
    - Pixel shaders are overloaded.( Can be complemented with CyDecal. )
    -  Custom shaders cannot be used as is.( Can be complemented with CyDecal. )
- **CyDecal**
  -  **Advantages**
     - Lightweight processing.( However, decal mesh generation is laggy )
     - Full skin animation is possible.
     - Custom shaders can be used without modification.
  - **Demerit**
    - The process of applying decals takes time. ( Can be complemented with URP decals. )
    - Z-fighting happen.

Thus, the two decals can be used together to complement many of the disadvantages.<br/>

### 2.2 Combination of URP decal and CyDecal
As we saw in the previous section, the two decal treatments can be used together to complement the limitations of URP decals.<br/><br/>

The following model case is presented here as a way of combining the two.

|Method|Use Case|
|---|---|
|URP Decal| ・ Decal moves in the object space.<br/> ・ An alternative method until the mesh generation by CyDecal is finished.|
|CyDecal|Decal don't moves in object space.|

The following movie demonstrates the implementation of this model case.

<br/>
<p align="center">
<img width="80%" src="Documentation/fig-001.gif" alt="Combination of URP decal and CyDecal"><br>
<font color="grey">Combination of URP decal and CyDecal</font>
</p>

In this movie, URP decal is used when the decal moves on the receiver object and to buy time until mesh generation is complete.<br/>
Once the position on the receiver object is determined and mesh generation is finished, the decal by CyDecal is displayed thereafter.<br/>

Once mesh generation is complete, CyDecal can be used to greatly improve runtime performance, a critical issue for mobile games (see Section 2.3 for details).<br/>

### 2.3 URP Decal and CyDecal rendering performance

On the other hand, URP decals do not require mesh generation, but a complex drawing process is performed to display the decals.<br/><br/>
Therefore, the mesh generation method is more advantageous in terms of frame-by-frame rendering performance.<br/><br/>

The following figure shows the measured rendering performance of URP Decal and CyDecal.<br/>
In all cases, CyDecal was superior, with the most significant difference being a performance improvement of 19 ms.
<p align="center">
<img width="80%" src="Documentation/fig-002.png" alt="Performance Measurement Results"><br>
<font color="grey">Performance Measurement Results</font>
</p>


## Section 3 How to use
CyDecal can be used by importing the Assets/CyDecal folder into your own project.<br/>
The following two cmponents are the most important of these.
1. CyDecalSystem
2. CyDecalProjector 

### 3.1 CyDecalSystem
To use CyDecal, you must always install one game object with this component attach.

<p align="center">
<img width="80%" src="Documentation/fig-013.png" alt="CyDecalSystem"><br>
<font color="grey">CyDecalSystem</font>
</p>

### 3.2 CyDecalProjector
This component is used to project decals. Add this component to the game object to be installed as a decal projector.

<p align="center">
<img width="50%" src="Documentation/fig-004.png" alt="CyDecalProjector inspector"><br>
<font color="grey">CyDecalProjector inspector</font>
</p>

Five parameters can be set for the CyDecalProjector component.

|Parameter name|Description|
|---|---|
|Width|Width of the Projector bounding box.This complies with URP's decal projector specifications.<br/>For more information, see [Manual for URP Decals](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal. html).|
|Height|Height of the Projector bounding box.This complies with URP's decal projector specifications.<br/>For more information, see [Manual for URP Decals](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal. html).|
|Depth|Depth of the Projector bounding box.This complies with URP's decal projector specifications.<br/>For more information, see [Manual for URP Decals](https://docs.unity3d.com/ja/Packages/com.unity.render-pipelines.universal@14.0/manual/renderer-feature-decal. html).|
|Receiver Object| The object to which the decal texture will be applied.<br/>CyDecalProjector targets all renderers pasted to children of the configured receiver object (including itself).<br/><br/>Therefore, the receiver object can be specified directly as an object to which a component such as MeshRenderer or SkinMeshRenderer is attached, or it can be an object that contains an object to which a renderer is attached as a child.<br/>The more renderers you process, the longer it will take to generate the decal mesh.Therefore, if the object to which the decal texture is to be applied can be restricted, it is more advantageous to specify that object directly as the receiver object.<br/><br/>For example, if you want to put a sticker on a character's face in a character edit, you can save mesh generation time by specifying the object to which the face renderer is attached rather than specifying the character's root object.|
|Decal Material| URP decals can only use materials with Shader Graphs/Decal shaders assigned, while CyDecal can use regular materials.<br/>This means that built-in Lit shaders, Unlit shaders, and user-custom, proprietary shaders are also available.|
|Launch On Awake|If this checkbox is checked, the decal projection process is started at the same time the instance is created.|
|On Finished Launch|You can specify a callback to be called at the end of the decal projection.|

The following video shows how to use CyDecalProjector in a scene.
<p align="center">
<img width="80%" src="Documentation/fig-012.gif" alt="How to use CyDeaclProjector"><br>
<font color="grey">How to use CyDeaclProjector</font>
</p>


### 3.3 How to generate CyDecalProjector in-game
An example of an in-game use of decals is the process of applying bullet holes to a background, such as in a FPS. <br/>
Such a process can be accomplished by determining the collision between the background and the bullet, generating a CyDecalProjector component based on the collision point information, and constructing a decal mesh.<br/><br/>
CyDecalProjector components can be created by calling the CyDecalProjector.CreateAndLaunch() method.</br>
If true is specified for the launchAwake argument of the CreateAndLaunch() method, the decal mesh construction process is started at the same time the component is created.<br/><br/>
The decal mesh construction process takes several frames.Therefore, if you want to monitor the end of the decal mesh construction process, you must monitor the NowState property of CyDecalProjector or use the onFinishedLaunch callback, which is called when the mesh generation process is finished.<br/><br/>
The following code is a pseudo code to paste bullet holes on the background using the CyDecalProjector.CreateAndLaunch() method. This code sets up a callback that monitors for termination using the arguments of the CreateAndLaunch() method.<br/>
```C#
// hitPosition    Bullet and background collision point.
// hitNormal      Normal of the collided surface.
// receiverObject Receiver object to which decal is applied.
// decalMaterial  Material with decal texture set
void LaunchProjector( 
  Vector3 hitPosition, Vector3 hitNormal, 
  GameObject receiverObject, Material decalMaterial )
{
    var projectorObject = new GameObject("Decal Projector");
    // Install the projector at the position pushed back in the normal direction.
    projectorObject.transform.position = hitPosition + hitNormal;
    // Projector is oriented in the opposite direction of the normal.
    projectorObject.transform.rotation = Quaternion.LookRotation( hitNormal * -1.0f );

    CyDecalProjector.CreateAndLaunch(
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



