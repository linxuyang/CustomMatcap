using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using Autodesk.Fbx;
using Unity.Plastic.Antlr3.Runtime.Misc;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public class SeaboardMeshCreator
{
    public List<MeshFilter> selectedMeshFilters = new List<MeshFilter>();
    public Transform water;
    public float waterHeightOffset = 0;
    private float finalHeight = 0;

    public float WaterHeight
    {
        get => water ? water.position.y : 0 + waterHeightOffset;
    }

    #region 计算生成海岸线

    // 通过水面与地面模型的相交，计算得到形成海岸线的每一段线段

    private List<Vector3> _temVector3List = new List<Vector3>();

    public struct Board
    {
        public Vector3 head;
        public Vector3 tail;
    }

    public List<Board> boards = new List<Board>();

    /// <summary>
    /// 标记boards中相同index的线段是否丢弃
    /// </summary>
    public bool[] boardsExcuse;

    public void CalculateSeaboardLines()
    {
        boards.Clear();
        finalHeight = WaterHeight;
        HashSet<int> idHashSet = new HashSet<int>();
        for (int i = 0; i < selectedMeshFilters.Count; i++)
        {
            MeshFilter meshFilter = selectedMeshFilters[i];
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            int id = meshFilter.GetInstanceID();
            if (!idHashSet.Add(id))
            {
                continue;
            }

            CalculateBySingleMesh(meshFilter.transform, mesh);
        }

        boardsExcuse = new bool[boards.Count];
    }

    void CalculateBySingleMesh(Transform transform, Mesh mesh)
    {
        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        int vertexIndex0, vertexIndex1, vertexIndex2;
        Vector3 vertex0, vertex1, vertex2;
        Vector3 normal0, normal1, normal2, avgNormal;
        for (int i = 0; i < triangles.Length - 2; i += 3)
        {
            vertexIndex0 = triangles[i];
            vertexIndex1 = triangles[i + 1];
            vertexIndex2 = triangles[i + 2];
            vertex0 = vertices[vertexIndex0];
            vertex1 = vertices[vertexIndex1];
            vertex2 = vertices[vertexIndex2];
            normal0 = normals[vertexIndex0];
            normal1 = normals[vertexIndex1];
            normal2 = normals[vertexIndex2];
            vertex0 = transform.TransformPoint(vertex0);
            vertex1 = transform.TransformPoint(vertex1);
            vertex2 = transform.TransformPoint(vertex2);
            // 计算三个顶点的平均法线
            avgNormal = (normal0 + normal1 + normal2).normalized;
            // 平均法线转到世界空间
            avgNormal = transform.TransformDirection(avgNormal);
            // 计算平均法线在水平面上的方向
            avgNormal.y = 0;
            avgNormal.Normalize();
            CalculateBySingleTriangle(vertex0, vertex1, vertex2, avgNormal);
        }
    }

    void CalculateBySingleTriangle(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, Vector3 normal)
    {
        // 三角形与水平面平行的情况下视为与水面不相交
        if (Math.Abs(vertex0.y - vertex1.y) < float.Epsilon && Math.Abs(vertex1.y - vertex2.y) < float.Epsilon)
        {
            return;
        }

        // 计算各个顶点与水面的高度差
        float deltaY0 = vertex0.y - finalHeight;
        float deltaY1 = vertex1.y - finalHeight;
        float deltaY2 = vertex2.y - finalHeight;
        // 判断各个顶点是否与水面重合, 不重合记1, 重合记0
        // float.Epsilon 为浮点数精度误差
        int notEqual0 = (Math.Abs(deltaY0) > float.Epsilon) ? 1 : 0;
        int notEqual1 = (Math.Abs(deltaY1) > float.Epsilon) ? 1 : 0;
        int notEqual2 = (Math.Abs(deltaY2) > float.Epsilon) ? 1 : 0;
        // 计算不与水面重合的顶点数量
        int notEqualNum = notEqual0 + notEqual1 + notEqual2;
        // 计算顶点在水面上下的分布情况(水面之上记1, 水面之下记-1, 与水面重合记0)
        int signs = 0;
        signs += (int) Mathf.Sign(deltaY0) * notEqual0;
        signs += (int) Mathf.Sign(deltaY1) * notEqual1;
        signs += (int) Mathf.Sign(deltaY2) * notEqual2;
        if (notEqualNum == 3) // 没有顶点与水面重合
        {
            // 判断三个顶点是否都处于水面之上或水面之下
            if (Math.Abs(signs) == 3)
            {
                return;
            }

            // 当三个顶点的情况是:
            // 2个在水面上, 1个在水面下时, signs = 1, 此时单独处于水面下方的顶点是 _temVector3List[2] (从大到小排列)
            // 1个在水面上, 2个在水面下时, signs = -1, 此时单独处于水面上方的顶点是 _temVector3List[0]
            int singleIndex = signs + 1;
            int doubleIndex = (singleIndex + 1) % 3;
            AddBoard(vertex0, vertex1, vertex2, singleIndex, doubleIndex, normal);
        }
        else if (notEqualNum == 2) // 1个顶点与水面重合
        {
            // 若两个顶点(除掉刚好处于水面位置的顶点)不是分别处于水面上下, 则返回
            if (signs != 0)
            {
                return;
            }

            AddBoard(vertex0, vertex1, vertex2, 0, 1, normal);
        }
        else if (notEqualNum == 1) // 2个顶点与水面重合
        {
            // 若不与水面重合的顶点在水面之上, 则返回
            // 这样是为了避免一上一下两个三角形(共用两个顶点刚好与水面重合), 导致重复添加同一线段
            // 如果两个三角形在水面同一侧的则会出现重复添加或不添加的问题, 但这种极端情况极少出现
            if (signs > 0)
            {
                return;
            }

            // 计算不与水面重合的顶点的index(原始顺序)
            int index = (1 - notEqual1) + (1 - notEqual2) * 2;
            AddBoard(vertex0, vertex1, vertex2, index, normal);
        }
        // 3个顶点与水面重合的情况包含于三角形与水平面平行的情况, 已经在函数开头返回
    }

    void AddBoard(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, int singleIndex, int doubleIndex, Vector3 normal)
    {
        // 三个顶点按照Y轴坐标从大到小排列
        _temVector3List.Clear();
        _temVector3List.Add(vertex0);
        _temVector3List.Add(vertex1);
        _temVector3List.Add(vertex2);
        _temVector3List.Sort((vectorA, vectorB) => (int) Mathf.Sign(vectorB.y - vectorA.y));
        Board board = new Board();
        float lerp = (finalHeight - _temVector3List[doubleIndex].y) /
                     (_temVector3List[singleIndex].y - _temVector3List[doubleIndex].y);
        board.head = Vector3.Lerp(_temVector3List[doubleIndex], _temVector3List[singleIndex], lerp);
        doubleIndex++;
        lerp = (finalHeight - _temVector3List[doubleIndex].y) /
               (_temVector3List[singleIndex].y - _temVector3List[doubleIndex].y);
        board.tail = Vector3.Lerp(_temVector3List[doubleIndex], _temVector3List[singleIndex], lerp);
        FixBoardDirection(ref board, normal);
        boards.Add(board);
    }

    void AddBoard(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, int index, Vector3 normal)
    {
        _temVector3List.Clear();
        // 这里保持原本的顶点顺序(不做高度排序)
        _temVector3List.Add(vertex0);
        _temVector3List.Add(vertex1);
        _temVector3List.Add(vertex2);
        Board board = new Board();
        // 直接用与水面重合的两个顶点的坐标构建线段
        board.head = _temVector3List[(index + 1) % 3];
        board.tail = _temVector3List[(index + 2) % 3];
        FixBoardDirection(ref board, normal);
        boards.Add(board);
    }

    /// <summary>
    /// 调整海岸线段朝向(从水面方向看海岸线的, head在左, tail在右)
    /// </summary>
    /// <param name="board">海岸线</param>
    /// <param name="normal">生成该海岸线的三角形的法线在水平面上的朝向(已归一化)</param>
    void FixBoardDirection(ref Board board, Vector3 normal)
    {
        Vector3 boardDir = board.tail - board.head;
        // 法线叉乘海岸线朝向, 根据左手定则(Unity采用左手坐标系), 结果的Y分量小于0说明朝向正确
        if (Vector3.Cross(normal, boardDir).y < 0)
        {
            return;
        }

        // 朝向不正确的话就把head和tail调换
        var temp = board.tail;
        board.tail = board.head;
        board.head = temp;
    }

    #endregion


    #region 串联各个海岸线
    // 根据线段之间在空间上是否相连，将离散的海岸线段拼接成若干条开放或封闭的海岸线

    /// <summary>
    /// 经过剔除后剩余的所有海岸线段
    /// </summary>
    private List<Board> excusedBoards = new List<Board>();

    /// <summary>
    /// 所有合并后的海岸线的顶点队列
    /// </summary>
    private List<List<Vector3>> combinedBoards = new List<List<Vector3>>();

    /// <summary>
    /// 标记combinedBoards中相同index的海岸线是否为封闭图形
    /// </summary>
    private List<bool> isCombinedBoardsClosed = new List<bool>();

    public bool IsCombinedBoardsClosed(int index)
    {
        return isCombinedBoardsClosed[index];
    }

    /// <summary>
    /// 合并海岸线, 将离散的海岸线段拼接成若干条开放或封闭的海岸线
    /// </summary>
    public void CombineBoards()
    {
        ClearCombineBoards();
        ExcuseBoards();
        CombineWithExcuseBoards();
        CombineCollinearBoards();
    }

    /// <summary>
    /// 清理残留数据
    /// </summary>
    private void ClearCombineBoards()
    {
        excusedBoards.Clear();
        combinedBoards.Clear();
        isCombinedBoardsClosed.Clear();
    }

    /// <summary>
    /// 把boards复制一份, 并把被标记需要剔除的线段移除
    /// </summary>
    private void ExcuseBoards()
    {
        excusedBoards.AddRange(boards);
        for (int i = boards.Count - 1; i >= 0; i--)
        {
            if (boardsExcuse[i])
            {
                excusedBoards.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// 初步合并海岸线, 将离散的海岸线段按照首尾相连的模式拼接成若干段连续的海岸线
    /// </summary>
    private void CombineWithExcuseBoards()
    {
        int excusedBoardsCount = excusedBoards.Count;
        while (excusedBoardsCount > 0)
        {
            // 创建一条新的海岸线
            List<Vector3> combinedBoard = new List<Vector3>();
            // 海岸线末端顶点index
            int combinedBoardTailIndex = 0;

            // 先直接取出一段Board, 从数组尾部取, 方便一边循环一边remove
            Board board = excusedBoards[excusedBoardsCount - 1];
            excusedBoards.RemoveAt(excusedBoardsCount - 1);
            excusedBoardsCount--;

            // 把第一段Board作为海岸线最初的两个顶点
            combinedBoard.Add(board.head);
            combinedBoard.Add(board.tail);
            combinedBoardTailIndex += 1;

            // 在构建一条海岸线的过程中, 需要不停地遍历 excusedBoards, 找出其中能够接在海岸线首、尾的线段
            // 每找到一个符合条件的线段就把对应顶点加入 combinedBoard, 然后把该线段从 excusedBoards 中移除
            // 当在一次遍历中没有找到任何符合条件的线段时, 说明这条海岸线已经构建完毕, 结束循环并开始构建下一条海岸线
            // 当 excusedBoards 中的线段数量为0时, 也说明这条海岸线构建完毕, 结束循环

            // 标记是否完成搜索
            bool searchCompleted = excusedBoardsCount < 1;
            while (!searchCompleted)
            {
                // 标记在一次 excusedBoards 的遍历中, 是否没有找到任何符合条件的线段
                bool notCombinedInLoop = true;
                // 遍历 excusedBoards, 寻找符合条件的线段
                for (int i = excusedBoardsCount - 1; i >= 0; i--)
                {
                    board = excusedBoards[i];
                    // 线段的 head 能和海岸线的末端相接, 把线段的 tail 顶点加在海岸线的末端
                    if (board.head == combinedBoard[combinedBoardTailIndex])
                    {
                        combinedBoard.Add(board.tail);
                        combinedBoardTailIndex++;

                        excusedBoards.RemoveAt(i);
                        excusedBoardsCount--;

                        notCombinedInLoop = false;
                    }
                    // 线段的 tail 能和海岸线的前端相接, 把线段的 head 顶点加在海岸线的前端
                    else if (board.tail == combinedBoard[0])
                    {
                        combinedBoard.Insert(0, board.head);
                        combinedBoardTailIndex++;

                        excusedBoards.RemoveAt(i);
                        excusedBoardsCount--;

                        notCombinedInLoop = false;
                    }
                }

                // 如果没找到符合条件的线段或者剩余线段数量小于1, 则结束该海岸线的构建循环
                searchCompleted = notCombinedInLoop || excusedBoardsCount < 1;
            }

            // 判断海岸线是否闭合曲线(首尾顶点坐标是否相等)
            bool isClosed = combinedBoard[0] == combinedBoard[combinedBoardTailIndex];
            // 如果闭合的话把末端的顶点删掉(和前端的顶点重复了)
            if (isClosed)
            {
                combinedBoard.RemoveAt(combinedBoardTailIndex);
            }

            // 保存结果
            isCombinedBoardsClosed.Add(isClosed);
            combinedBoards.Add(combinedBoard);
        }
    }
    #endregion


    #region 减少各个海岸线的顶点

    [Range(0.0001f, 0.01f)]
    public float collinearCombineParam = 0.0001f;
    
    /// <summary>
    /// 最终的海岸线顶点队列(合并共线线段之后)
    /// </summary>
    private List<List<Vector3>> finalCombinedBoards = new List<List<Vector3>>();
    
    public List<List<Vector3>> CombinedBoards
    {
        get { return finalCombinedBoards; }
    }

    /// <summary>
    /// 合并相邻且共线的海岸线段
    /// </summary>
    public void CombineCollinearBoards()
    {
        HashSet<int> markRemoveIndexs = new HashSet<int>();
        finalCombinedBoards.Clear();
        for (int i = 0; i < combinedBoards.Count; i++)
        {
            markRemoveIndexs.Clear();
            List<Vector3> combinedBoard = combinedBoards[i];
            int vertexCount = combinedBoard.Count;
            int headIndex = 0;
            int tailIndex = 2;
            while (tailIndex < vertexCount)
            {
                int midIndex = tailIndex - 1;
                TryRemoveCollinearVertex(combinedBoard, ref headIndex, ref tailIndex, midIndex, markRemoveIndexs);
            }

            if (isCombinedBoardsClosed[i])
            {
                headIndex = vertexCount - 2;
                tailIndex = 0;
                while (tailIndex < 2)
                {
                    int midIndex = (tailIndex - 1 + vertexCount) % vertexCount;
                    TryRemoveCollinearVertex(combinedBoard, ref headIndex, ref tailIndex, midIndex, markRemoveIndexs);
                }
            }

            List<Vector3> finalCombinedBoard = new List<Vector3>();
            for (int j = 0; j < vertexCount; j++)
            {
                if (markRemoveIndexs.Contains(j))
                    continue;
                finalCombinedBoard.Add(combinedBoard[j]);
            }

            finalCombinedBoards.Add(finalCombinedBoard);
        }
    }

    /// <summary>
    /// 根据顶点坐标计算三个顶点形成的夹角是否接近水平角(以collinearCombineParam作为比较基准)
    /// 若满足条件则在removeIndexs中标记midIndex表示移除该序号的顶点将被移除, 然后顶点序号会进行相应的进位
    /// 若不满足条件则直接对顶点序号进行相应的进位
    /// </summary>
    /// <param name="combinedBoard">海岸线的顶点List</param>
    /// <param name="headIndex">前端顶点在combinedBoard中的序号</param>
    /// <param name="tailIndex">中间顶点在combinedBoard中的序号</param>
    /// <param name="midIndex">末端顶点在combinedBoard中的序号</param>
    /// <param name="removeIndexs">标记哪些序号的顶点将被移除的哈希表</param>
    private void TryRemoveCollinearVertex(List<Vector3> combinedBoard, ref int headIndex, ref int tailIndex,
        int midIndex, HashSet<int> removeIndexs)
    {
        Vector3 headPos = combinedBoard[headIndex];
        Vector3 midPos = combinedBoard[midIndex];
        Vector3 tailPos = combinedBoard[tailIndex];
        Vector3 headToMid = midPos - headPos;
        Vector3 midToTail = tailPos - midPos;
        if (Mathf.Abs(headToMid.x * midToTail.z - headToMid.z * midToTail.x) < collinearCombineParam)
        {
            removeIndexs.Add(midIndex);
            tailIndex++;
        }
        else
        {
            headIndex = midIndex;
            tailIndex++;
        }
    }
    #endregion


    #region 构造近岸浪花网格

    /// <summary>
    /// 每个顶点处的法线方向(垂直于海岸切线, 从岸边指向水面)
    /// </summary>
    public List<List<Vector3>> listOfNormals = new List<List<Vector3>>();

    /// <summary>
    /// 近岸网格的径向宽度
    /// </summary>
    public float meshWidth = 1f;
    
    /// <summary>
    /// 近岸网格的径向偏移
    /// </summary>
    public float widthOffset = 0f;
    
    /// <summary>
    /// 近岸网格前面的顶点
    /// </summary>
    public List<List<Vector3>> listOfFrontVertexs = new List<List<Vector3>>();

    /// <summary>
    /// 近岸网格后面的顶点
    /// </summary>
    public List<List<Vector3>> listOfBackVertexs = new List<List<Vector3>>();

    /// <summary>
    /// 计算各个海岸线每个顶点处的法线
    /// </summary>
    public void CalculateBoardsNormals()
    {
        listOfNormals.Clear();
        for (int i = 0; i < finalCombinedBoards.Count; i++)
        {
            List<Vector3> finalCombinedBoard = finalCombinedBoards[i];
            int vertexCount = finalCombinedBoard.Count;
            List<Vector3> normals = new List<Vector3>(vertexCount);
            bool isClose = IsCombinedBoardsClosed(i);
            Vector3 prior, current, next;
            
            // 第1个顶点的法线需要根据海岸线是否闭合特殊计算
            if (isClose)
            {
                prior = finalCombinedBoard[vertexCount - 1];
                current = finalCombinedBoard[0];
                next = finalCombinedBoard[1];
                normals.Add(CalculateNormalByThreeVertexs(prior, current, next));
            }
            else
            {
                current = finalCombinedBoard[0];
                next = finalCombinedBoard[1];
                normals.Add(CalculateNormalByTwoVertexs(current, next));
            }

            // 计算第2至倒数第2个顶点的法线
            for (int vertexIndex = 1; vertexIndex < vertexCount - 1; vertexIndex++)
            {
                prior = finalCombinedBoard[vertexIndex - 1];
                current = finalCombinedBoard[vertexIndex];
                next = finalCombinedBoard[vertexIndex + 1];
                normals.Add(CalculateNormalByThreeVertexs(prior, current, next));
            }
            
            // 最后1个顶点的法线同样需要根据海岸线是否闭合特殊计算
            if (isClose)
            {
                prior = finalCombinedBoard[vertexCount - 2];
                current = finalCombinedBoard[vertexCount - 1];
                next = finalCombinedBoard[0];
                normals.Add(CalculateNormalByThreeVertexs(prior, current, next));
            }
            else
            {
                current = finalCombinedBoard[vertexCount - 2];
                next = finalCombinedBoard[vertexCount - 1];
                normals.Add(CalculateNormalByTwoVertexs(current, next));
            }
            
            listOfNormals.Add(normals);
        }
        
        CalculateFrontAndBackVertexs();
    }

    /// <summary>
    /// 根据三个顶点计算current顶点对应的法线
    /// </summary>
    /// <param name="prior">上一个顶点坐标</param>
    /// <param name="current">当前顶点坐标</param>
    /// <param name="next">下一个顶点坐标</param>
    /// <returns>法线(已归一化)</returns>
    private Vector3 CalculateNormalByThreeVertexs(Vector3 prior, Vector3 current, Vector3 next)
    {
        Vector3 priorNormal = Vector3.Cross(Vector3.up, current - prior).normalized;
        Vector3 nextNormal = Vector3.Cross(Vector3.up, next - current).normalized;
        return (priorNormal + nextNormal).normalized;
    }

    /// <summary>
    /// 根据两个顶点计算current顶点对应的法线
    /// </summary>
    /// <param name="current">当前顶点坐标</param>
    /// <param name="next">下一个顶点坐标</param>
    /// <returns>法线(已归一化)</returns>
    private Vector3 CalculateNormalByTwoVertexs(Vector3 current, Vector3 next)
    {
        return Vector3.Cross(Vector3.up, next - current).normalized;
    }

    /// <summary>
    /// 计算近岸网格两侧的顶点坐标
    /// </summary>
    public void CalculateFrontAndBackVertexs()
    {
        listOfFrontVertexs.Clear();
        listOfBackVertexs.Clear();
        for (int boardIndex = 0; boardIndex < finalCombinedBoards.Count; boardIndex++)
        {
            List<Vector3> finalCombinedBoard = finalCombinedBoards[boardIndex];
            List<Vector3> normals = listOfNormals[boardIndex];
            List<Vector3> frontVertexs = new List<Vector3>();
            List<Vector3> backVertexs = new List<Vector3>();
            listOfFrontVertexs.Add(frontVertexs);
            listOfBackVertexs.Add(backVertexs);
            for (int vertexIndex = 0; vertexIndex < finalCombinedBoard.Count; vertexIndex++)
            {
                Vector3 vertex = finalCombinedBoard[vertexIndex];
                Vector3 normal = normals[vertexIndex];
                frontVertexs.Add(vertex + normal * (widthOffset + meshWidth));
                backVertexs.Add(vertex + normal * widthOffset);
            }
        }
    }

    #endregion

    #region 生成网格文件

    public void CreateSeaboardMeshs(string savingPath)
    {
        for (int boardIndex = 0; boardIndex < listOfFrontVertexs.Count; boardIndex++)
        {
            List<Vector3> frontVertexs = listOfFrontVertexs[boardIndex];
            List<Vector3> backVertexs = listOfBackVertexs[boardIndex];
            int lastVertexIndex = frontVertexs.Count - 1;
            bool isClose = IsCombinedBoardsClosed(boardIndex);
            
            // 计算所有顶点的平均值作为中心点(严格来讲这里得到的结果并非几何中心, 但是没什么关系)
            Vector3 center = Vector3.zero;
            for (int index = 0; index <= lastVertexIndex; index++)
            {
                center += frontVertexs[index];
                center += backVertexs[index];
            }
            center /= 2 * lastVertexIndex + 2;

            List<Vector3> vertexs = new List<Vector3>();
            float lengthOfFrontSide = 0;
            List<float> lengthFromHeadToAllFrontVertexs = new List<float>();
            Vector3 lastFrontVertex = Vector3.zero;
            for (int index = 0; index <= lastVertexIndex; index++)
            {
                Vector3 frontPos = frontVertexs[index] - center;
                vertexs.Add(frontPos);
                vertexs.Add(backVertexs[index] - center);
                if (index > 0)
                {
                    lengthOfFrontSide += Vector3.Distance(lastFrontVertex, frontPos);
                }
                lastFrontVertex = frontPos;
                lengthFromHeadToAllFrontVertexs.Add(lengthOfFrontSide);
            }

            if (isClose)
            {
                Vector3 frontPos = frontVertexs[0] - center;
                vertexs.Add(frontPos);
                vertexs.Add(backVertexs[0] - center);
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
            for (int index = 0; index < lastVertexIndex; index++)
            {
                int vertexIndex = index * 2;
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 3);
                triangles.Add(vertexIndex + 2);

                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 3);
                triangles.Add(vertexIndex);
            }

            if (isClose)
            {
                int vertexIndex = lastVertexIndex * 2;
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 3);
                triangles.Add(vertexIndex + 2);

                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 3);
                triangles.Add(vertexIndex);
            }
            
            Mesh newMesh = new Mesh();
            newMesh.SetVertices(vertexs);
            newMesh.SetUVs(0, uvs);
            newMesh.SetTriangles(triangles, 0);
            newMesh.RecalculateNormals();

            string name = $"seaboardMesh_{boardIndex}";
            GameObject newGameObject = new GameObject(name);
            newGameObject.AddComponent<MeshFilter>().sharedMesh = newMesh;
            newGameObject.AddComponent<MeshRenderer>();

            string exportPath = savingPath + $"/{name}.fbx";
            AssetDatabase.DeleteAsset(exportPath);
            ModelExporter.ExportObjects(exportPath, new Object[]{newGameObject});
            AssetDatabase.SaveAssets();
            Object.DestroyImmediate(newGameObject);
            GameObject fbxObj = AssetDatabase.LoadAssetAtPath<GameObject>(exportPath);
            newGameObject = Object.Instantiate(fbxObj);
            newGameObject.transform.position = center;
        }
        AssetDatabase.Refresh();
    }

    #endregion
}

public class SeaboardMeshCreateWindow : EditorWindow
{
    private static readonly Vector2 MIN_SIZE = new Vector2(300, 300);
    private static readonly Vector2 MAX_SIZE = new Vector2(500, 500);
    private static SeaboardMeshCreateWindow instance;
    private Vector2 scrollpos = Vector2.one;
    private string savingPath = "Assets";
    private int dataPathLength;

    [MenuItem("MC/生成岸边网格")]
    public static void OnpenWindow()
    {
        instance = GetWindow<SeaboardMeshCreateWindow>(true, "生成岸边网格");
        instance.dataPathLength = Application.dataPath.Length - 6;
        instance.minSize = MIN_SIZE;
        instance.maxSize = MAX_SIZE;
    }

    private SeaboardMeshCreator creator = new SeaboardMeshCreator();
    private int state = 0;

    private int State
    {
        get { return state; }
        set
        {
            state = value;
            SceneView.RepaintAll();
        }
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnGUI()
    {
        switch (State)
        {
            case 0:
                OnFirstStateGUI();
                break;
            case 1:
                OnSecondStateGUI();
                break;
            case 2:
                OnThirdStateGUI();
                break;
            case 3:
                OnFourthStateGUI();
                break;
        }
    }

    private void OnFirstStateGUI()
    {
        DrawLandList();
        DrawWaterHeight();
        GUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("下一步", GUILayout.Width(80)))
        {
            creator.CalculateSeaboardLines();
            scrollpos = Vector2.one;
            State++;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OnSecondStateGUI()
    {
        DrawBoardIndexs();
        GUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("返回", GUILayout.Width(80)))
        {
            State--;
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("下一步", GUILayout.Width(80)))
        {
            creator.CombineBoards();
            State++;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OnThirdStateGUI()
    {
        DrawReduceBoardsVertexCount();
        GUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("返回", GUILayout.Width(80)))
        {
            State--;
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("下一步", GUILayout.Width(80)))
        {
            creator.CalculateBoardsNormals();
            State++;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void OnFourthStateGUI()
    {
        DrawMeshParams();
        GUILayout.Space(20);
        DrawMeshSavingPath();
        GUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("返回", GUILayout.Width(80)))
        {
            State--;
        }

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("生成网格", GUILayout.Width(160)))
        {
            creator.CreateSeaboardMeshs(savingPath);
            Close();
            DestroyImmediate(instance);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawLandList()
    {
        List<MeshFilter> landList = creator.selectedMeshFilters;
        GUILayout.Box("选择地形", GUILayout.Height(20), GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginVertical();
        for (int i = 0; i < landList.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            landList[i] =
                (MeshFilter) EditorGUILayout.ObjectField(landList[i], typeof(MeshFilter), true, GUILayout.Width(160));
            GUILayout.Space(20);
            if (GUILayout.Button("删除", GUILayout.Width(80)))
            {
                landList.RemoveAt(i);
            }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("新增", GUILayout.Width(80)))
        {
            landList.Add(null);
        }

        GUILayout.Space(20);
        EditorGUILayout.EndVertical();
    }

    private void DrawWaterHeight()
    {
        GUILayout.Box("设置水面高度", GUILayout.Height(20), GUILayout.ExpandWidth(true));
        EditorGUILayout.BeginVertical();
        creator.water =
            (Transform) EditorGUILayout.ObjectField("水面模型", creator.water, typeof(Transform), true,
                GUILayout.Width(300));
        creator.waterHeightOffset = EditorGUILayout.FloatField("高度偏移", creator.waterHeightOffset, GUILayout.Width(220));
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(creator.WaterHeight.ToString("最终高度"), GUILayout.Width(160));
        GUILayout.Label(creator.WaterHeight.ToString("F2"), GUILayout.Width(160));
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(20);
        EditorGUILayout.EndVertical();
    }

    private void DrawBoardIndexs()
    {
        GUILayout.Box("剔除不需要的边界线段", GUILayout.Height(20), GUILayout.ExpandWidth(true));
        GUILayout.Box("在Scene视图中检查所有边界线段\n在下方根据序号勾选需要剔除的线段\n然后点击下一步", GUILayout.Height(60),
            GUILayout.ExpandWidth(true));
        GUILayout.Space(20);
        scrollpos = EditorGUILayout.BeginScrollView(scrollpos);
        EditorGUILayout.BeginHorizontal();
        bool needRepaint = false;
        int horizonetalNum = 0;
        for (int i = 0; i < creator.boards.Count; i++)
        {
            horizonetalNum++;
            bool boolValue = EditorGUILayout.Toggle(creator.boardsExcuse[i], GUILayout.Width(15));
            if (boolValue != creator.boardsExcuse[i])
            {
                needRepaint = true;
                creator.boardsExcuse[i] = boolValue;
            }
            GUILayout.Label($"{i}", GUILayout.Width(20));
            GUILayout.Space(10);
            if (horizonetalNum == 5)
            {
                horizonetalNum = 0;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
            }
        }

        if (needRepaint)
        {
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
    }

    private void DrawReduceBoardsVertexCount()
    {
        GUILayout.Box("减少海岸线的顶点数", GUILayout.Height(20), GUILayout.ExpandWidth(true));
        GUILayout.Box("调整下方的[简化程度], 根据线段间拐角的大小删除不重要的顶点\n在Scene视图中可以试试查看删除后的效果\n然后点击下一步",
            GUILayout.Height(60), GUILayout.ExpandWidth(true));
        GUILayout.Space(20);
        float value = EditorGUILayout.Slider("简化程度", creator.collinearCombineParam, 0.0001f, 0.01f);
        if (Math.Abs(value - creator.collinearCombineParam) > float.Epsilon)
        {
            creator.collinearCombineParam = value;
            creator.CombineCollinearBoards();
            SceneView.RepaintAll();
        }
    }

    private void DrawMeshParams()
    {
        GUILayout.Box("设置近岸网格的宽度和偏移", GUILayout.Height(20), GUILayout.ExpandWidth(true));
        GUILayout.Box("调整下方的[宽度]来改变网格整体宽度\n调整下方的[偏移]来改变网格整体在径向方向上的偏移",
            GUILayout.Height(40), GUILayout.ExpandWidth(true));
        GUILayout.Space(20);
        bool needRepaint = false;
        float floatValue = EditorGUILayout.FloatField("宽度", creator.meshWidth);
        floatValue = Mathf.Max(0, floatValue);
        if (Mathf.Abs(floatValue - creator.meshWidth) > float.Epsilon)
        {
            needRepaint = true;
            creator.meshWidth = floatValue;
        }

        floatValue = EditorGUILayout.FloatField("偏移", creator.widthOffset);
        if (Mathf.Abs(floatValue - creator.widthOffset) > float.Epsilon)
        {
            needRepaint = true;
            creator.widthOffset = floatValue;
        }

        if (needRepaint)
        {
            creator.CalculateFrontAndBackVertexs();
            SceneView.RepaintAll();
        }
    }

    void DrawMeshSavingPath()
    {
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("选择路径", GUILayout.Width(80)))
        {
            string selectPath = EditorUtility.OpenFolderPanel("选择保存路径", "Assets", "");
            if (!string.IsNullOrEmpty(selectPath))
            {
                savingPath = selectPath.Remove(0, dataPathLength);
            }
        }
        EditorGUILayout.LabelField(savingPath, GUILayout.ExpandWidth(true));
        EditorGUILayout.EndHorizontal();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        switch (State)
        {
            case 0:
                break;
            case 1:
                DrawSeaboardLines();
                break;
            case 2:
                DrawVertexsOfCombinedBoards();
                break;
            case 3:
                DrawSeaboardMeshs();
                break;
        }
    }

    void DrawSeaboardLines()
    {
        GUIStyle label = new GUIStyle();
        label.fontSize = 20;
        label.richText = true;
        Handles.color = Color.magenta;
        for (int i = 0; i < creator.boards.Count; i++)
        {
            if (creator.boardsExcuse[i])
            {
                continue;
            }

            SeaboardMeshCreator.Board board = creator.boards[i];
            Handles.DrawLine(board.head, board.tail, 2f);
            Handles.SphereHandleCap(0, board.head, Quaternion.identity, 0.05f, EventType.Repaint);
            Handles.Label(0.5f * (board.head + board.tail), $"<color=#FF0000>{i}</color>", label);
        }
    }

    void DrawVertexsOfCombinedBoards()
    {
        Handles.color = Color.blue;
        List<List<Vector3>> combinedBoards = creator.CombinedBoards;
        for (int i = 0; i < combinedBoards.Count; i++)
        {
            float r = i % 3 / 2.0f;
            float g = i / 3 % 3 / 2.0f;
            float b = i / 9 % 3 / 2.0f;
            Handles.color = new Color(r, g, b);
            List<Vector3> vertexsOfCombinedBoard = combinedBoards[i];
            for (int j = 0; j < vertexsOfCombinedBoard.Count - 1; j++)
            {
                Handles.SphereHandleCap(0, vertexsOfCombinedBoard[j], Quaternion.identity, 0.05f, EventType.Repaint);
                Handles.DrawLine(vertexsOfCombinedBoard[j], vertexsOfCombinedBoard[j + 1], 4f);
            }

            Handles.SphereHandleCap(0, vertexsOfCombinedBoard[vertexsOfCombinedBoard.Count - 1], Quaternion.identity,
                0.05f, EventType.Repaint);
            if (creator.IsCombinedBoardsClosed(i))
            {
                Handles.DrawLine(vertexsOfCombinedBoard[0], vertexsOfCombinedBoard[vertexsOfCombinedBoard.Count - 1],
                    4f);
            }
        }
    }

    void DrawSeaboardMeshs()
    {
        Handles.color = Color.blue;
        List<List<Vector3>> listOfFrontVertexs = creator.listOfFrontVertexs;
        List<List<Vector3>> listOfBackVertexs = creator.listOfBackVertexs;
        for (int i = 0; i < listOfFrontVertexs.Count; i++)
        {
            List<Vector3> frontVertexs = listOfFrontVertexs[i];
            List<Vector3> backVertexs = listOfBackVertexs[i];
            int lastIndex = frontVertexs.Count - 1;
            for (int vertexIndex = 0; vertexIndex < lastIndex; vertexIndex++)
            {
                Handles.DrawLine(frontVertexs[vertexIndex], frontVertexs[vertexIndex + 1], 4f);
                Handles.DrawLine(backVertexs[vertexIndex], backVertexs[vertexIndex + 1], 4f);
            }

            if (creator.IsCombinedBoardsClosed(i))
            {
                Handles.DrawLine(frontVertexs[0], frontVertexs[lastIndex], 4f);
                Handles.DrawLine(backVertexs[0], backVertexs[lastIndex], 4f);
            }
            else
            {
                Handles.DrawLine(frontVertexs[0], backVertexs[0], 4f);
                Handles.DrawLine(frontVertexs[lastIndex], backVertexs[lastIndex], 4f);
            }
            
        }
    }
}