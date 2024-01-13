using System;
using UnityEditor;
using UnityEngine.Assertions;
using UnityEngine;


public enum SurfaceType
{
    Opaque,
    Transparent
}

public enum BlendMode
{
    Alpha, // Old school alpha-blending mode, fresnel does not affect amount of transparency
    Premultiply, // Physically plausible transparency mode, implemented as alpha pre-multiply
    Additive,
    Multiply
}

public enum RenderFace
{
    Front = 2,
    Back = 1,
    Both = 0
}

public enum MixMatType
{
    Two,
    Three,
    Four
}

class Styles
{
    // Catergories

    public static readonly GUIContent surfaceOptions = new GUIContent("Surface Options", "控制材质的基础渲染类型(是否半透明, 单双面渲染等)");

    public static readonly GUIContent surfaceType = new GUIContent("表面类型", "不透明或半透明");

    public static readonly GUIContent blendingMode = new GUIContent("混合模式", "控制半透明物体颜色与前景颜色的混合方式");

    public static readonly GUIContent cullingText = new GUIContent("剔除", "选择渲染几何体的正面或背面");

    public static readonly GUIContent alphaClipText = new GUIContent("透明度裁剪", "根据纹理贴图的透明度裁剪模型");

    public static readonly GUIContent alphaClipThresholdText = new GUIContent("裁剪阈值", "透明度低于阈值的区域将被裁剪");

    public static readonly GUIContent receiveShadowText = new GUIContent("接收阴影", "接收其他物体造成的阴影");

    public static readonly GUIContent surfaceInputs = new GUIContent("Surface Inputs", "基础表面纹理参数");

    public static readonly GUIContent baseMap = new GUIContent("基础纹理", "物体表面纹理");

    public static readonly GUIContent baseColor = new GUIContent("基础色调", "物体表面色调");

    public static readonly GUIContent normalMap = new GUIContent("法线纹理", "控制物体表面法线");
    
    public static readonly GUIContent normalMetalSmoothMap = new GUIContent("法线金属光滑度", "控制物体表面法线(RG)、金属度(B)和光滑度(A)");
    
    public static readonly GUIContent normalScale = new GUIContent("法线纹理强度", "增强或减弱法线纹理对模型表面法线的影响");
    
    public static readonly GUIContent metallic = new GUIContent("金属度", "控制模型整体金属度");
    
    public static readonly GUIContent smoothness = new GUIContent("光滑度", "控制模型整体光滑度");
    
    public static readonly GUIContent emissionColor = new GUIContent("自发光颜色", "自发光颜色");
    
    public static readonly GUIContent occlusion = new GUIContent("环境光遮蔽", "整体环境光遮蔽");
    
    public static readonly GUIContent emissionAOMap = new GUIContent("自发光-环境光遮蔽", "控制物体表面自发光(RGB)、环境光遮蔽(A)");
    
    public static readonly GUIContent environmentReflection = new GUIContent("环境反射", "反射环境间接光");

    public static readonly GUIContent advancedLabel = new GUIContent("Advanced", "渲染底层相关的技术参数");

    public static readonly GUIContent queueSlider = new GUIContent("渲染优先级", "数值越小越优先渲染");
    
    public static readonly GUIContent matcapsInputs = new GUIContent("Matcap Inputs", "快照材质参数");
    
    public static readonly GUIContent mixMap = new GUIContent("材质混合贴图", "根据贴图4通道的值为物体表面切换不同的材质");
    
    public static readonly GUIContent mixMatType = new GUIContent("材质混合类型", "2/3/4种材质混合");
    
    public static readonly GUIContent diffuseMatcap = new GUIContent("漫反射Matcap", "只包含了漫反射以及环境反射的Matcap贴图");
    
    public static readonly GUIContent specMatcap = new GUIContent("高光Matcap", "只包含了高光反射的Matcap贴图");
    
    public static readonly GUIContent diffuseStrength = new GUIContent("漫反射强度", "控制漫反射的亮度");

    public static readonly GUIContent specStrength = new GUIContent("高光强度", "控制高光反射的亮度");
}