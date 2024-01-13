using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public class MixMatcapsShader : ShaderGUI
{
    protected MaterialEditor materialEditor { get; set; }
    protected MaterialProperty mainTexProp { get; set; }
    protected MaterialProperty mainColorProp { get; set; }
    protected MaterialProperty normalScaleProp { get; set; }
    protected MaterialProperty normalMapProp { get; set; }
    protected MaterialProperty mixMapProp { get; set; }
    protected MaterialProperty mixMatTypeProp { get; set; }
    protected MaterialProperty diffuseMatCapsProp { get; set; }
    protected MaterialProperty specMatCapsProp { get; set; }
    protected MaterialProperty diffuseStrengthsProp { get; set; }
    protected MaterialProperty specStrengthsProp { get; set; }
    public bool m_FirstTimeApply = true;
    private const string k_KeyPrefix = "UniversalRP:Material:UI_State:";
    private string m_HeaderStateKey = null;

    protected string headerStateKey
    {
        get { return m_HeaderStateKey; }
    }

    SavedBool m_SurfaceInputsFoldout;
    SavedBool m_MatcapsFoldout;
    SavedBool m_AdvancedFoldout;

    public void FindProperties(MaterialProperty[] properties)
    {
        mainTexProp = FindProperty("_MainTex", properties, false);
        mainColorProp = FindProperty("_MainColor", properties, false);
        normalMapProp = FindProperty("_NormalMap", properties, false);
        normalScaleProp = FindProperty("_NormalScale", properties, false);
        mixMapProp = FindProperty("_MixMap", properties, false);
        mixMatTypeProp = FindProperty("_MixMatType", properties, false);
        diffuseMatCapsProp = FindProperty("_DiffuseMatCaps", properties, false);
        specMatCapsProp = FindProperty("_SpecMatCaps", properties, false);
        diffuseStrengthsProp = FindProperty("_DiffuseStrengths", properties, false);
        specStrengthsProp = FindProperty("_SpecStrengths", properties, false);
    }

    public override void OnGUI(MaterialEditor materialEditorIn, MaterialProperty[] properties)
    {
        if (materialEditorIn == null)
            throw new ArgumentNullException("materialEditorIn");
        FindProperties(properties);
        materialEditor = materialEditorIn;
        Material material = materialEditor.target as Material;
        if (m_FirstTimeApply)
        {
            OnOpenGUI(material, materialEditorIn);
            m_FirstTimeApply = false;
        }

        ShaderPropertiesGUI(material);
    }

    public virtual void OnOpenGUI(Material material, MaterialEditor materialEditorIn)
    {
        // Foldout states
        m_HeaderStateKey = k_KeyPrefix + material.shader.name; // Create key string for editor prefs
        m_SurfaceInputsFoldout = new SavedBool($"{m_HeaderStateKey}.SurfaceInputsFoldout", true);
        m_MatcapsFoldout = new SavedBool($"{m_HeaderStateKey}.MatcapsFoldout", true);
        m_AdvancedFoldout = new SavedBool($"{m_HeaderStateKey}.AdvancedFoldout", false);

        foreach (var obj in materialEditorIn.targets)
            MaterialChanged((Material) obj);
    }

    public void ShaderPropertiesGUI(Material material)
    {
        if (material == null)
            throw new ArgumentNullException("material");

        m_SurfaceInputsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_SurfaceInputsFoldout.value, Styles.surfaceInputs);
        if (m_SurfaceInputsFoldout.value)
        {
            DrawBaseProperties();
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        
        m_MatcapsFoldout.value = EditorGUILayout.BeginFoldoutHeaderGroup(m_MatcapsFoldout.value, Styles.matcapsInputs);
        if (m_MatcapsFoldout.value)
        {
            MatcapsProperties();
            EditorGUILayout.Space();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        m_AdvancedFoldout.value =
            EditorGUILayout.BeginFoldoutHeaderGroup(m_AdvancedFoldout.value, Styles.advancedLabel);
        if (m_AdvancedFoldout.value)
        {
            DrawAdvancedOptions();
            EditorGUILayout.Space();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();

        if (EditorGUI.EndChangeCheck())
        {
            foreach (var obj in materialEditor.targets)
                MaterialChanged((Material) obj);
        }
    }

    public void DrawBaseProperties()
    {
        materialEditor.ShaderProperty(mainTexProp, Styles.baseMap);
        materialEditor.ShaderProperty(mainColorProp, Styles.baseColor);
        EditorGUILayout.Space();
        materialEditor.ShaderProperty(normalMapProp, Styles.normalMap);
        if (normalMapProp.textureValue != null)
        {
            materialEditor.ShaderProperty(normalScaleProp, Styles.normalScale);
        }
        EditorGUILayout.Space();
    }

    public void MatcapsProperties()
    {
        materialEditor.ShaderProperty(mixMapProp, Styles.mixMap);
        DoPopup(Styles.mixMatType, mixMatTypeProp, Enum.GetNames(typeof(MixMatType)));
        EditorGUILayout.Space();
        materialEditor.ShaderProperty(diffuseMatCapsProp, Styles.diffuseMatcap);
        materialEditor.ShaderProperty(specMatCapsProp, Styles.specMatcap);
        EditorGUILayout.Space();
        Vector4 diffuseStrengthTemp = new Vector4();
        Vector4 specStrengthTemp = new Vector4();
        for (int i = 0; i < mixMatTypeProp.floatValue + 2; i++)
        {
            EditorGUILayout.LabelField("材质:" + i);
            diffuseStrengthTemp[i] = EditorGUILayout.Slider(Styles.diffuseStrength, diffuseStrengthsProp.vectorValue[i], 0f, 5f);
            specStrengthTemp[i] = EditorGUILayout.Slider(Styles.specStrength, specStrengthsProp.vectorValue[i], 0f, 2f);
            EditorGUILayout.Space();
        }

        for (int j = (int)mixMatTypeProp.floatValue + 2; j < 4; j++)
        {
            diffuseStrengthTemp[j] = diffuseStrengthsProp.vectorValue[j];
            specStrengthTemp[j] = specStrengthsProp.vectorValue[j];
        }
        diffuseStrengthsProp.vectorValue = diffuseStrengthTemp;
        specStrengthsProp.vectorValue = specStrengthTemp;
    }

    public void DrawAdvancedOptions()
    {
        materialEditor.EnableInstancingField();
    }


    public void MaterialChanged(Material material)
    {
        if (material == null)
            throw new ArgumentNullException("material");

        SetMaterialKeywords(material);
    }

    public static void SetMaterialKeywords(Material material)
    {
        // Clear all keywords for fresh start
        material.shaderKeywords = null;

        MixMatType mixMatType = (MixMatType) material.GetFloat("_MixMatType");
        if (mixMatType == MixMatType.Three)
        {
            CoreUtils.SetKeyword(material, "_MIX_THREE_MATCAP", true);
        }
        else if(mixMatType == MixMatType.Four)
        {
            CoreUtils.SetKeyword(material, "_MIX_FOUR_MATCAP", true);
        }
    }

    public void DoPopup(GUIContent label, MaterialProperty property, string[] options)
    {
        DoPopup(label, property, options, materialEditor);
    }

    public static void DoPopup(GUIContent label, MaterialProperty property, string[] options,
        MaterialEditor materialEditor)
    {
        if (property == null)
            throw new ArgumentNullException("property");

        EditorGUI.showMixedValue = property.hasMixedValue;

        var mode = property.floatValue;
        EditorGUI.BeginChangeCheck();
        mode = EditorGUILayout.Popup(label, (int) mode, options);
        if (EditorGUI.EndChangeCheck())
        {
            materialEditor.RegisterPropertyChangeUndo(label.text);
            property.floatValue = mode;
        }

        EditorGUI.showMixedValue = false;
    }
}