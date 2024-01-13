using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public class SeaboardsSettings : ScriptableObject
{
    public Transform waterParent;
    public bool beginPlacing = false;
    public List<Vector3> passPoints = new List<Vector3>();
    public float k = 1;
    public bool loop = false;
    public List<Vector3> controlPoints = new List<Vector3>();
    public float splitNum = 10;
    public List<Vector3> boardPositions = new List<Vector3>();
    public bool settingUp = false;
    public List<Vector3> normals = new List<Vector3>();
    public bool flip = false;
    public float width = 1;
    public float offset = 0;
    public List<Vector3> frontVertexs = new List<Vector3>();
    public List<Vector3> backVertexs = new List<Vector3>();
}

public class SeaboardsEditorWindow : EditorWindow
{
    private static readonly Vector2 MIN_SIZE = new Vector2(320, 300);
    private static readonly Vector2 MAX_SIZE = new Vector2(320, 320);
    private const string info = @"
        Scene窗口操作指南:
    [Shift] + 鼠标左键 : 放置顶点
    拖动顶点操作杆可以调整现有顶点位置
    [Ctrl] + [Z] : 撤销操作

        参数说明:
    闭合曲线 : 是否形成封闭曲线(需要顶点数量>2)
    形状系数 : 决定曲线的拐角半径大小
    光滑度 : 决定形成曲线的线段数量
    宽度 : 网格的径向宽度
    偏移 : 网格的径向偏移
    翻转网格 : 决定网格的朝向(确保蓝色的一边对着陆地)";

    private static SeaboardsEditorWindow instance;

    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle toggleStyle;
    private GUIStyle boxStyle;
    private GUIStyle infoStyle;

    private SeaboardsSettings settings;
    private SerializedObject settingsSo;
    private SerializedProperty waterParentSp;
    private SerializedProperty beginPlacingSp;
    private SerializedProperty passPointsSp;
    private SerializedProperty kSp;
    private SerializedProperty loopSp;
    private SerializedProperty controlPointsSp;
    private SerializedProperty splitNumSp;
    private SerializedProperty boardPositionsSp;
    private SerializedProperty settingUpSp;
    private SerializedProperty normalsSp;
    private SerializedProperty flipSp;
    private SerializedProperty widthSp;
    private SerializedProperty offsetSp;
    private SerializedProperty frontVertexsSp;
    private SerializedProperty backVertexsSp;

    [MenuItem("MC/放置岸边网格")]
    public static void OnpenWindow()
    {
        instance = GetWindow<SeaboardsEditorWindow>(true, "放置岸边网格");
        instance.minSize = MIN_SIZE;
        instance.maxSize = MAX_SIZE;
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        settings = CreateInstance<SeaboardsSettings>();
        settingsSo = new SerializedObject(settings);
        waterParentSp = settingsSo.FindProperty("waterParent");
        beginPlacingSp = settingsSo.FindProperty("beginPlacing");
        passPointsSp = settingsSo.FindProperty("passPoints");
        kSp = settingsSo.FindProperty("k");
        loopSp = settingsSo.FindProperty("loop");
        controlPointsSp = settingsSo.FindProperty("controlPoints");
        splitNumSp = settingsSo.FindProperty("splitNum");
        boardPositionsSp = settingsSo.FindProperty("boardPositions");
        settingUpSp = settingsSo.FindProperty("settingUp");
        normalsSp = settingsSo.FindProperty("normals");
        flipSp = settingsSo.FindProperty("flip");
        widthSp = settingsSo.FindProperty("width");
        offsetSp = settingsSo.FindProperty("offset");
        frontVertexsSp = settingsSo.FindProperty("frontVertexs");
        backVertexsSp = settingsSo.FindProperty("backVertexs");
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        waterParentSp.Dispose();
        boardPositionsSp.Dispose();
        settingsSo.Dispose();
        DestroyImmediate(settings);
    }

    private void OnGUI()
    {
        boxStyle ??= new GUIStyle(GUI.skin.box)
        {
            fontSize = 15, normal = {textColor = Color.white}, alignment = TextAnchor.MiddleCenter
        };
        settingsSo.Update();
        EditorGUI.BeginChangeCheck();
        if (!beginPlacingSp.boolValue)
        {
            GUILayout.Box("在下方选择想要放置海浪网格的水面\n海浪网格会被自动创建在水面节点中", boxStyle, GUILayout.Height(40),
                GUILayout.ExpandWidth(true));
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("选择水面GameObject", GUILayout.Width(120));
            waterParentSp.objectReferenceValue = EditorGUILayout.ObjectField(waterParentSp.objectReferenceValue,
                typeof(Transform), true, GUILayout.Width(160));
            EditorGUILayout.EndHorizontal();
            if (waterParentSp.objectReferenceValue != null)
            {
                GUILayout.Space(10);
                if (GUILayout.Button("开始放置岸边网格", GUILayout.Width(160)))
                {
                    beginPlacingSp.boolValue = true;
                }
            }
        }
        else
        {
            GUILayout.Space(10);
            if (GUILayout.Button("结束放置岸边网格", GUILayout.Width(160)))
            {
                beginPlacingSp.boolValue = false;
                ClearDatas();
            }

            GUILayout.Space(10);
            infoStyle ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 12, normal = {textColor = Color.white}, alignment = TextAnchor.UpperLeft
            };
            GUILayout.Box(info, infoStyle, GUILayout.Height(250),
                GUILayout.ExpandWidth(true));
        }

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }

        settingsSo.ApplyModifiedProperties();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        settingsSo.Update();
        DrawScreenGUI();
        Event current = Event.current;
        if (current.type == EventType.Repaint)
        {
            DrawSceneGUI();
        }
        else
        {
            ReadInputs(current);
        }

        settingsSo.ApplyModifiedProperties();
    }

    void DrawSceneGUI()
    {
        if (!beginPlacingSp.boolValue)
        {
            return;
        }

        Color oriColor = Handles.color;
        if (settingUpSp.boolValue)
        {
            int vertexCount = frontVertexsSp.arraySize;
            bool isClose = loopSp.boolValue && vertexCount > 2;
            int lastIndex = vertexCount - 1;
            Handles.color = Color.red;
            for (int vertexIndex = 0; vertexIndex < lastIndex; vertexIndex++)
            {
                Handles.DrawLine(GetSPArrayElement(frontVertexsSp, vertexIndex),
                    GetSPArrayElement(frontVertexsSp, vertexIndex + 1), 2f);
            }
            Handles.color = Color.blue;
            for (int vertexIndex = 0; vertexIndex < lastIndex; vertexIndex++)
            {
                Handles.DrawLine(GetSPArrayElement(backVertexsSp, vertexIndex),
                    GetSPArrayElement(backVertexsSp, vertexIndex + 1), 2f);
            }

            if (isClose)
            {
                Handles.color = Color.red;
                Handles.DrawLine(GetSPArrayElement(frontVertexsSp, 0), GetSPArrayElement(frontVertexsSp, lastIndex),
                    2f);
                Handles.color = Color.blue;
                Handles.DrawLine(GetSPArrayElement(backVertexsSp, 0), GetSPArrayElement(backVertexsSp, lastIndex), 2f);
            }
            else
            {
                Handles.color = Color.red;
                Handles.DrawLine(GetSPArrayElement(frontVertexsSp, 0), GetSPArrayElement(backVertexsSp, 0), 2f);
                Handles.DrawLine(GetSPArrayElement(frontVertexsSp, lastIndex),
                    GetSPArrayElement(backVertexsSp, lastIndex), 2f);
            }
        }
        else
        {
            SerializedProperty vertexsSP;
            bool loop = false;
            if (passPointsSp.arraySize > 2)
            {
                vertexsSP = boardPositionsSp;
                loop = loopSp.boolValue;
            }
            else
            {
                vertexsSP = passPointsSp;
            }
            Handles.color = Color.yellow;
            for (int i = 0; i < vertexsSP.arraySize - 1; i++)
            {
                Handles.DrawLine(GetSPArrayElement(vertexsSP, i),
                    GetSPArrayElement(vertexsSP, i + 1), 2f);
            }
            if (loop)
            {
                Handles.DrawLine(GetSPArrayElement(vertexsSP, vertexsSP.arraySize - 1),
                    GetSPArrayElement(vertexsSP, 0), 2f);
            }
        }
        Handles.color = oriColor;
    }

    void DrawScreenGUI()
    {
        if (!beginPlacingSp.boolValue)
        {
            return;
        }

        if (!settingUpSp.boolValue)
        {
            int arraySize = passPointsSp.arraySize;
            if (arraySize > 0)
            {
                Handles.color = Color.green;
                bool needRecalculate = false;
                for (int i = 0; i < arraySize; i++)
                {
                    Vector3 pos = GetSPArrayElement(passPointsSp, i);
                    Vector3 handleValue = Handles.PositionHandle(pos, Quaternion.identity);
                    handleValue.y = pos.y;
                    if (!handleValue.Equals(pos))
                    {
                        SetSPArrayElement(passPointsSp, i, handleValue);
                        needRecalculate = true;
                    }
                }

                if (needRecalculate && arraySize > 2)
                {
                    RecalculateControlPoints();
                }
            }
        }

        Handles.BeginGUI();
        labelStyle ??= new GUIStyle(GUI.skin.label) {fontSize = 30, clipping = TextClipping.Overflow};
        toggleStyle ??= new GUIStyle(GUI.skin.toggle) {fontSize = 20, normal = {textColor = Color.white}};
        buttonStyle ??= new GUIStyle(GUI.skin.button) {fontSize = 30};

        Rect rect = new Rect(0f, 0f, 200f, 70f);
        GUI.Label(rect, "按住[Shift] + 点击左键 : 放置顶点\n拖动顶点操作杆可以调整现有顶点位置", labelStyle);

        if (settingUpSp.boolValue)
        {
            rect = new Rect(Screen.width - 120f, Screen.height - 340f, 120f, 300f);
            GUI.BeginGroup(rect);

            rect.x = 0;
            rect.y = 0;
            GUI.Box(rect, String.Empty);

            rect.x = 15;
            rect.y = 5;
            rect.width = 50;
            rect.height = 20;
            GUI.Label(rect, "宽度");

            rect.x = 75;
            GUI.Label(rect, "偏移");

            rect.x = 30;
            rect.y = 30;
            rect.width = 20;
            rect.height = 140;
            float sliderValue = GUI.VerticalSlider(rect, widthSp.floatValue, 20, 1);
            if (Math.Abs(sliderValue - widthSp.floatValue) > float.Epsilon)
            {
                SetupWidth(sliderValue);
            }

            rect.x = 90f;
            sliderValue = GUI.VerticalSlider(rect, offsetSp.floatValue, 10, -10);
            if (Math.Abs(sliderValue - offsetSp.floatValue) > float.Epsilon)
            {
                SetupOffset(sliderValue);
            }

            rect.x = 15;
            rect.y = 175;
            rect.width = 50;
            rect.height = 20;
            GUI.Label(rect, widthSp.floatValue.ToString("F2"));

            rect.x = 75;
            GUI.Label(rect, offsetSp.floatValue.ToString("F2"));

            rect.x = 10;
            rect.y = 210f;
            rect.width = 100f;
            rect.height = 20f;
            bool toggleValue = GUI.Toggle(rect, flipSp.boolValue, "翻转网格", toggleStyle);
            if (toggleValue != flipSp.boolValue)
            {
                SetupFlip(toggleValue);
            }

            rect.y = 250f;
            rect.height = 40f;
            if (GUI.Button(rect, "生成", buttonStyle))
            {
                BuildMesh();
            }

            GUI.EndGroup();
        }
        else
        {
            rect.x = Screen.width - 200f;
            rect.y = Screen.height - 180f;
            rect.width = 200f;
            rect.height = 180f;
            GUI.BeginGroup(rect);
            rect.x = 0f;
            rect.y = 0f;
            GUI.Box(rect, string.Empty);

            int passPointCount = passPointsSp.arraySize;
            if (passPointCount > 2)
            {
                rect.x = 80f;
                rect.y = 5f;
                rect.width = 120f;
                rect.height = 20f;
                bool toggleValue = GUI.Toggle(rect, loopSp.boolValue, "闭合曲线", toggleStyle);
                if (toggleValue != loopSp.boolValue)
                {
                    SetupLoop(toggleValue);
                }

                rect.x = 5f;
                rect.y = 30f;
                rect.width = 80f;
                rect.height = 20f;
                GUI.Label(rect, "形状系数");

                rect.y = 60f;
                GUI.Label(rect, "光滑度");

                rect.x = 70f;
                rect.y = 30f;
                rect.width = 120f;
                rect.height = 20f;
                float sliderValue = GUI.HorizontalSlider(rect, kSp.floatValue, 0.1f, 1f);
                if (Mathf.Abs(sliderValue - kSp.floatValue) > float.Epsilon)
                {
                    SetupK(sliderValue);
                }

                rect.y = 60f;
                sliderValue = GUI.HorizontalSlider(rect, splitNumSp.floatValue, 2f, 20f);
                if (Mathf.Abs(sliderValue - splitNumSp.floatValue) > float.Epsilon)
                {
                    SetupSplitNum(sliderValue);
                }
            }

            if (passPointCount > 1)
            {
                rect.x = 20f;
                rect.y = 90f;
                rect.width = 160f;
                rect.height = 40f;
                if (GUI.Button(rect, "确定", buttonStyle))
                {
                    SetupSingleBoard();
                }
            }

            GUI.EndGroup();
        }

        Handles.EndGUI();
    }

    void ReadInputs(Event currentEvent)
    {
        if (!beginPlacingSp.boolValue)
        {
            return;
        }

        if (settingUpSp.boolValue)
        {
            return;
        }

        if (!currentEvent.shift)
        {
            return;
        }

        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);
        if (currentEvent.button == 0 && currentEvent.type == EventType.MouseUp)
        {
            Camera currentCamera = Camera.current;
            Vector2 mousePos = currentEvent.mousePosition;
            mousePos.x /= currentCamera.pixelWidth;
            mousePos.y /= currentCamera.pixelHeight;
            CameraRay ray = GetCameraRay(currentCamera, mousePos);
            Vector3 targetPos = ray.origin +
                                ray.direction * (settings.waterParent.position.y - ray.origin.y) / ray.direction.y;
            AddSPArrayElement(passPointsSp, targetPos);
            if (passPointsSp.arraySize > 2)
            {
                RecalculateControlPoints();
            }
        }
    }

    struct CameraRay
    {
        public Vector3 origin;
        public Vector3 direction;
    }

    static CameraRay GetCameraRay(Camera camera, Vector2 mousePos)
    {
        CameraRay ray = new CameraRay();
        mousePos = 2 * mousePos - Vector2.one;
        mousePos.y *= -1;
        Vector2 nearClipPlaneSize = new Vector2();
        float screenRate = (float) camera.pixelWidth / camera.pixelHeight;
        if (camera.orthographic)
        {
            nearClipPlaneSize.y = camera.orthographicSize;
            nearClipPlaneSize.x = nearClipPlaneSize.y * screenRate;
            ray.direction = Vector3.forward;
            ray.origin = new Vector3(nearClipPlaneSize.x * mousePos.x, nearClipPlaneSize.y * mousePos.y,
                camera.nearClipPlane);
        }
        else
        {
            nearClipPlaneSize.y = Mathf.Tan(Mathf.Deg2Rad * 0.5f * camera.fieldOfView) * camera.farClipPlane;
            nearClipPlaneSize.y *= camera.nearClipPlane / camera.farClipPlane;
            nearClipPlaneSize.x = nearClipPlaneSize.y * screenRate;
            ray.direction = new Vector3(nearClipPlaneSize.x * mousePos.x, nearClipPlaneSize.y * mousePos.y,
                camera.nearClipPlane);
            ray.direction.Normalize();
            ray.origin = Vector3.zero;
        }

        ray.direction = camera.transform.TransformDirection(ray.direction);
        ray.origin = camera.transform.TransformPoint(ray.origin);
        return ray;
    }

    void SetupLoop(bool loop)
    {
        loopSp.boolValue = loop;
        RecalculateControlPoints();
    }

    void SetupK(float k)
    {
        kSp.floatValue = k;
        RecalculateControlPoints();
    }

    void SetupSplitNum(float splitNum)
    {
        bool needRecalculate = Mathf.RoundToInt(splitNum) != Mathf.RoundToInt(splitNumSp.floatValue);
        splitNumSp.floatValue = splitNum;
        if (needRecalculate)
        {
            RecalculateCurvePoints();
        }
    }

    void SetupSingleBoard()
    {
        CalculateNormals();
        CalculateMeshVertexs();
        settingUpSp.boolValue = true;
    }

    void SetupWidth(float width)
    {
        widthSp.floatValue = width;
        frontVertexsSp.ClearArray();
        backVertexsSp.ClearArray();
        CalculateMeshVertexs();
    }

    void SetupOffset(float offset)
    {
        offsetSp.floatValue = offset;
        frontVertexsSp.ClearArray();
        backVertexsSp.ClearArray();
        CalculateMeshVertexs();
    }

    void SetupFlip(bool flip)
    {
        flipSp.boolValue = flip;
        frontVertexsSp.ClearArray();
        backVertexsSp.ClearArray();
        CalculateMeshVertexs();
    }

    void RecalculateControlPoints()
    {
        Vector3[] passPoints = SPArrayToArray(passPointsSp);
        Vector3[] controlPoints = PiecewiseBezier.GenerateControlPoints(passPoints, loopSp.boolValue, kSp.floatValue);
        ArrayToSPArray(controlPoints, controlPointsSp);
        RecalculateCurvePoints();
    }

    void RecalculateCurvePoints()
    {
        Vector3[] passPoints = SPArrayToArray(passPointsSp);
        Vector3[] controlPoints = SPArrayToArray(controlPointsSp);
        Vector3[] curvePoints =
            PiecewiseBezier.GenerateCurvePoints(passPoints, controlPoints, loopSp.boolValue,
                Mathf.RoundToInt(splitNumSp.floatValue));
        ArrayToSPArray(curvePoints, boardPositionsSp);
    }

    void CalculateNormals()
    {
        Vector3[] vertexs;
        if (passPointsSp.arraySize < 3)
        {
            vertexs = SPArrayToArray(passPointsSp);
        }
        else
        {
            vertexs = SPArrayToArray(boardPositionsSp);
        }

        int vertexCount = vertexs.Length;
        bool isClose = loopSp.boolValue && vertexCount > 2;
        Vector3 prior, next;

        // 第1个顶点的法线需要根据海岸线是否闭合特殊计算
        if (isClose)
        {
            prior = vertexs[vertexCount - 1];
            next = vertexs[1];
            AddSPArrayElement(normalsSp, 0, CalculateNormal(prior, next));
        }
        else
        {
            prior = vertexs[0];
            next = vertexs[1];
            AddSPArrayElement(normalsSp, 0, CalculateNormal(prior, next));
        }

        // 计算第2至倒数第2个顶点的法线
        for (int vertexIndex = 1; vertexIndex < vertexCount - 1; vertexIndex++)
        {
            prior = vertexs[vertexIndex - 1];
            next = vertexs[vertexIndex + 1];
            AddSPArrayElement(normalsSp, vertexIndex, CalculateNormal(prior, next));
        }

        // 最后1个顶点的法线同样需要根据海岸线是否闭合特殊计算
        if (isClose)
        {
            prior = vertexs[vertexCount - 2];
            next = vertexs[0];
            AddSPArrayElement(normalsSp, vertexCount - 1, CalculateNormal(prior, next));
        }
        else
        {
            prior = vertexs[vertexCount - 2];
            next = vertexs[vertexCount - 1];
            AddSPArrayElement(normalsSp, vertexCount - 1, CalculateNormal(prior, next));
        }
    }

    /// <summary>
    /// 根据前后顶点计算当前顶点对应的法线
    /// </summary>
    /// <param name="prior">上一个顶点坐标</param>
    /// <param name="next">下一个顶点坐标</param>
    /// <returns>法线(归一化)</returns>
    private Vector3 CalculateNormal(Vector3 prior, Vector3 next)
    {
        return Vector3.Cross(Vector3.up, next - prior).normalized;
    }

    void CalculateMeshVertexs()
    {
        Vector3[] vertexs;
        if (passPointsSp.arraySize < 3)
        {
            vertexs = SPArrayToArray(passPointsSp);
        }
        else
        {
            vertexs = SPArrayToArray(boardPositionsSp);
        }

        for (int vertexIndex = 0; vertexIndex < vertexs.Length; vertexIndex++)
        {
            Vector3 vertex = vertexs[vertexIndex];
            Vector3 normal = GetSPArrayElement(normalsSp, vertexIndex);
            int flipSign = flipSp.boolValue ? -1 : 1;
            float width = widthSp.floatValue;
            float offset = offsetSp.floatValue;
            AddSPArrayElement(frontVertexsSp, vertexIndex, vertex + normal * (width + offset) * flipSign);
            AddSPArrayElement(backVertexsSp, vertexIndex, vertex + normal * offset * flipSign);
        }
    }

    Vector3 GetSPArrayElement(SerializedProperty spArray, int index)
    {
        return spArray.GetArrayElementAtIndex(index).vector3Value;
    }

    void SetSPArrayElement(SerializedProperty spArray, int index, Vector3 value)
    {
        spArray.GetArrayElementAtIndex(index).vector3Value = value;
    }

    void AddSPArrayElement(SerializedProperty spArray, int index, Vector3 value)
    {
        spArray.InsertArrayElementAtIndex(index);
        spArray.GetArrayElementAtIndex(index).vector3Value = value;
    }

    void AddSPArrayElement(SerializedProperty spArray, Vector3 value)
    {
        AddSPArrayElement(spArray, spArray.arraySize, value);
    }

    Vector3[] SPArrayToArray(SerializedProperty spArray)
    {
        Vector3[] array = new Vector3[spArray.arraySize];
        for (int i = 0; i < spArray.arraySize; i++)
        {
            array[i] = GetSPArrayElement(spArray, i);
        }

        return array;
    }

    void ArrayToSPArray(Vector3[] array, SerializedProperty spArray)
    {
        spArray.ClearArray();
        for (int i = 0; i < array.Length; i++)
        {
            AddSPArrayElement(spArray, i, array[i]);
        }
    }

    void BuildMesh()
    {
        CreateMeshFbx();
        ClearDatas();
    }

    void ClearDatas()
    {
        settingUpSp.boolValue = false;
        frontVertexsSp.ClearArray();
        backVertexsSp.ClearArray();
        normalsSp.ClearArray();
        boardPositionsSp.ClearArray();
        controlPointsSp.ClearArray();
        passPointsSp.ClearArray();
    }

    void CreateMeshFbx()
    {
        int vertexCount = frontVertexsSp.arraySize;
        int lastVertexIndex = vertexCount - 1;
        bool isClose = loopSp.boolValue && vertexCount > 2;
        bool flip = flipSp.boolValue;

        // 计算所有顶点的平均值作为中心点(严格来讲这里得到的结果并非几何中心, 但是没什么关系)
        Vector3 center = Vector3.zero;
        for (int index = 0; index < vertexCount; index++)
        {
            center += GetSPArrayElement(frontVertexsSp, index);
            center += GetSPArrayElement(backVertexsSp, index);
        }

        center /= 2 * vertexCount;

        List<Vector3> vertexs = new List<Vector3>();
        float lengthOfFrontSide = 0;
        List<float> lengthFromHeadToAllFrontVertexs = new List<float>();
        Vector3 lastFrontVertex = Vector3.zero;
        for (int index = 0; index < vertexCount; index++)
        {
            Vector3 frontPos = GetSPArrayElement(frontVertexsSp, index) - center;
            vertexs.Add(frontPos);
            vertexs.Add(GetSPArrayElement(backVertexsSp, index) - center);
            if (index > 0)
            {
                lengthOfFrontSide += Vector3.Distance(lastFrontVertex, frontPos);
            }

            lastFrontVertex = frontPos;
            lengthFromHeadToAllFrontVertexs.Add(lengthOfFrontSide);
        }

        if (isClose)
        {
            Vector3 frontPos = GetSPArrayElement(frontVertexsSp, 0) - center;
            vertexs.Add(frontPos);
            vertexs.Add(GetSPArrayElement(backVertexsSp, 0) - center);
            lengthOfFrontSide += Vector3.Distance(lastFrontVertex, frontPos);
        }

        List<Vector2> uvs = new List<Vector2>();
        for (int index = 0; index <= lastVertexIndex; index++)
        {
            float xOfUv = lengthFromHeadToAllFrontVertexs[index] / lengthOfFrontSide;
            uvs.Add(new Vector2(xOfUv, 0));
            uvs.Add(new Vector2(xOfUv, 1));
        }

        if (isClose)
        {
            uvs.Add(new Vector2(1, 0));
            uvs.Add(new Vector2(1, 1));
        }

        List<int> triangles = new List<int>();
        int indexDelta0 = flip ? 2 : 3;
        int indexDelta1 = flip ? 3 : 2;
        int indexDelta2 = flip ? 3 : 1;
        int indexDelta3 = flip ? 1 : 3;
        for (int index = 0; index < lastVertexIndex; index++)
        {
            int vertexIndex = index * 2;
            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + indexDelta0);
            triangles.Add(vertexIndex + indexDelta1);

            triangles.Add(vertexIndex + indexDelta2);
            triangles.Add(vertexIndex + indexDelta3);
            triangles.Add(vertexIndex);
        }

        if (isClose)
        {
            int vertexIndex = lastVertexIndex * 2;
            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + indexDelta0);
            triangles.Add(vertexIndex + indexDelta1);

            triangles.Add(vertexIndex + indexDelta2);
            triangles.Add(vertexIndex + indexDelta3);
            triangles.Add(vertexIndex);
        }

        Mesh newMesh = new Mesh();
        newMesh.SetVertices(vertexs);
        newMesh.SetUVs(0, uvs);
        newMesh.SetTriangles(triangles, 0);
        newMesh.RecalculateNormals();

        string exportFolder = GetFolderPath();
        string modelName = GetModelName();
        string fullPath = exportFolder + modelName;
        int modelIndex = 0;
        while (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(fullPath + $"{modelIndex}.fbx")))
        {
            ++modelIndex;
        }

        modelName += modelIndex;
        fullPath += $"{modelIndex}.fbx";

        GameObject newGameObject = new GameObject(modelName);
        newGameObject.AddComponent<MeshFilter>().sharedMesh = newMesh;
        newGameObject.AddComponent<MeshRenderer>();

        ModelExporter.ExportObjects(fullPath, new Object[] {newGameObject});
        AssetDatabase.SaveAssets();
        DestroyImmediate(newGameObject);
        GameObject fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(fullPath);
        newGameObject = Instantiate(fbxObj, waterParentSp.objectReferenceValue as Transform, true);
        newGameObject.name = modelName;
        newGameObject.transform.position = center;
        AssetDatabase.Refresh();
    }

    string GetFolderPath()
    {
        string folder = "Assets/Models/";
        return folder;
    }

    string GetModelName()
    {
        string modelName = "Model_Foam_";
        return modelName;
    }
}

public static class PiecewiseBezier
{
    public static Vector3[] GenerateControlPoints(Vector3[] passPoints, bool loop, float k)
    {
        int passPointsNum = passPoints.Length;
        Vector3[] controlPoints = new Vector3[passPointsNum * 2 - (loop ? 0 : 2)];

        Vector3 offset;
        for (int i = 1; i < passPointsNum - 1; i++)
        {
            Vector3 lastPoint = passPoints[i - 1];
            Vector3 currentPoint = passPoints[i];
            Vector3 nextPoint = passPoints[i + 1];
            offset = k * 0.25f * (nextPoint - lastPoint);
            controlPoints[i * 2 - 1] = currentPoint - offset;
            controlPoints[i * 2] = currentPoint + offset;
        }

        // 根据是否闭合曲线首尾的控制点计算方式有所不同
        if (loop)
        {
            Vector3 lastPoint = passPoints[passPointsNum - 1];
            Vector3 currentPoint = passPoints[0];
            Vector3 nextPoint = passPoints[1];
            offset = k * 0.25f * (nextPoint - lastPoint);
            controlPoints[passPointsNum * 2 - 1] = currentPoint - offset;
            controlPoints[0] = currentPoint + offset;

            lastPoint = passPoints[passPointsNum - 2];
            currentPoint = passPoints[passPointsNum - 1];
            nextPoint = passPoints[0];
            offset = k * 0.25f * (nextPoint - lastPoint);
            controlPoints[passPointsNum * 2 - 3] = currentPoint - offset;
            controlPoints[passPointsNum * 2 - 2] = currentPoint + offset;
        }
        else
        {
            offset = k * 0.5f * (controlPoints[1] - passPoints[0]);
            controlPoints[0] = passPoints[0] + offset;
            offset = k * 0.5f * (controlPoints[passPointsNum * 2 - 4] - passPoints[passPointsNum - 1]);
            controlPoints[passPointsNum * 2 - 3] = passPoints[passPointsNum - 1] + offset;
        }

        return controlPoints;
    }

    public static Vector3[] GenerateCurvePoints(Vector3[] passPoints, Vector3[] controlPoints, bool loop, int splitNum)
    {
        int passPointNum = passPoints.Length;
        int curvePointsNum = passPointNum + (passPointNum - (loop ? 0 : 1)) * (splitNum - 1);
        Vector3[] curvePoints = new Vector3[curvePointsNum];
        float lerpStep = 1f / splitNum;
        if (loop)
        {
            for (int i = 0; i < passPointNum; i++)
            {
                Vector3 p0 = passPoints[i];
                Vector3 p1 = controlPoints[i * 2];
                Vector3 p2 = controlPoints[i * 2 + 1];
                Vector3 p3 = passPoints[(i + 1) % passPointNum];
                for (int j = 0; j < splitNum; j++)
                {
                    curvePoints[i * splitNum + j] = Bezier(p0, p1, p2, p3, j * lerpStep);
                }
            }
        }
        else
        {
            for (int i = 0; i < passPointNum - 1; i++)
            {
                Vector3 p0 = passPoints[i];
                Vector3 p1 = controlPoints[i * 2];
                Vector3 p2 = controlPoints[i * 2 + 1];
                Vector3 p3 = passPoints[i + 1];
                for (int j = 0; j < splitNum; j++)
                {
                    curvePoints[i * splitNum + j] = Bezier(p0, p1, p2, p3, j * lerpStep);
                }
            }

            curvePoints[curvePointsNum - 1] = passPoints[passPointNum - 1];
        }

        return curvePoints;
    }

    private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t0 = (1 - t) * (1 - t) * (1 - t);
        float t1 = 3 * t * (1 - t) * (1 - t);
        float t2 = 3 * t * t * (1 - t);
        float t3 = t * t * t;
        return p0 * t0 + p1 * t1 + p2 * t2 + p3 * t3;
    }
}