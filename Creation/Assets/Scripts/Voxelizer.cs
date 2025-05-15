using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
public class Voxelizer : MonoBehaviour
{
    [Header("Compute Shader & 源 Mesh")]
    public ComputeShader voxelCompute;
    public MeshFilter     sourceMeshFilter;  // 如果挂在同一个物体上，可留空

    [Header("体素分辨率")]
    public Vector3Int splitCount = new Vector3Int(32, 32, 32);

    [Header("实例化设置")]
    public Mesh     instanceMesh;
    public Material instanceMaterial;        // 勾选 Enable GPU Instancing
    public Color    voxelColor = Color.white;

    [Header("实例缩放模式")]
    public bool    autoScale    = true;               // true → 按 AABB/分辨率自动算
    public Vector3 manualScale  = Vector3.one * 0.9f; // 或者手动指定

    // 运行时用的最终缩放
    Vector3 instanceScale;

    /// <summary>
    /// Compute 完成后置为 true，Update 才会触发绘制
    /// </summary>
    [HideInInspector] public bool isCompleted = false;

    // --- 私有 GPU 资源 ---
    RenderTexture projYZ, projXZ, projXY, volumeTex;
    ComputeBuffer triBuffer, voxelPosBuffer, argsBuffer;
    int kernelClear, kernelX, kernelY, kernelZ, kernelCombine;
    Bounds drawBounds;
    MaterialPropertyBlock mpb;

    void Start()
    {
        // 1) 拆三角形数据到 StructuredBuffer
        var mesh    = sourceMeshFilter != null 
                        ? sourceMeshFilter.sharedMesh 
                        : GetComponent<MeshFilter>().sharedMesh;
        var verts   = mesh.vertices;
        var inds    = mesh.triangles;
        int triCount = inds.Length / 3;

        // 展开顶点数组
        Vector3[] triVerts = new Vector3[inds.Length];
        for (int i = 0; i < inds.Length; i++)
            triVerts[i] = verts[inds[i]];

        triBuffer = new ComputeBuffer(triVerts.Length, sizeof(float) * 3);
        triBuffer.SetData(triVerts);

        // 2) 创建投影 & 体素 RWTexture
        projYZ    = NewRWTexture2D(splitCount.y, splitCount.z);
        projXZ    = NewRWTexture2D(splitCount.x, splitCount.z);
        projXY    = NewRWTexture2D(splitCount.x, splitCount.y);
        volumeTex = NewRWTexture3D(splitCount.x, splitCount.y, splitCount.z);

        // 3) 找 Kernels 并绑定 Buffer/Texture
        kernelClear   = voxelCompute.FindKernel("CS_Clear");
        kernelX       = voxelCompute.FindKernel("CS_ProjectX");
        kernelY       = voxelCompute.FindKernel("CS_ProjectY");
        kernelZ       = voxelCompute.FindKernel("CS_ProjectZ");
        kernelCombine = voxelCompute.FindKernel("CS_Combine");

        // 三角 Buffer 绑定到投影 Kernels
        voxelCompute.SetBuffer(kernelX, "_TriangleVerts", triBuffer);
        voxelCompute.SetBuffer(kernelY, "_TriangleVerts", triBuffer);
        voxelCompute.SetBuffer(kernelZ, "_TriangleVerts", triBuffer);
        voxelCompute.SetInt   ("_TriangleCount", triCount);

        // Clear Kernel 绑定所有 RWTexture
        voxelCompute.SetTexture(kernelClear, "_Volume", volumeTex);
        voxelCompute.SetTexture(kernelClear, "_ProjYZ", projYZ);
        voxelCompute.SetTexture(kernelClear, "_ProjXZ", projXZ);
        voxelCompute.SetTexture(kernelClear, "_ProjXY", projXY);

        // 各投影 Kernel 绑定它们对应的纹理
        voxelCompute.SetTexture(kernelX, "_ProjYZ", projYZ);
        voxelCompute.SetTexture(kernelY, "_ProjXZ", projXZ);
        voxelCompute.SetTexture(kernelZ, "_ProjXY", projXY);

        // Combine Kernel 绑定所有纹理
        voxelCompute.SetTexture(kernelCombine, "_Volume", volumeTex);
        voxelCompute.SetTexture(kernelCombine, "_ProjYZ", projYZ);
        voxelCompute.SetTexture(kernelCombine, "_ProjXZ", projXZ);
        voxelCompute.SetTexture(kernelCombine, "_ProjXY", projXY);

        // AABB + 分辨率
        var bounds = mesh.bounds;
        voxelCompute.SetVector("_AABBMin", bounds.min);
        voxelCompute.SetVector("_AABBMax", bounds.max);
        voxelCompute.SetInts("_SplitCount",
            splitCount.x, splitCount.y, splitCount.z);

        // 4) 创建 Append Buffer 和 Indirect Args Buffer
        int maxVoxels = splitCount.x * splitCount.y * splitCount.z;
        voxelPosBuffer = new ComputeBuffer(
            maxVoxels,
            sizeof(float) * 3,
            ComputeBufferType.Append
        );
        voxelPosBuffer.SetCounterValue(0);
        voxelCompute.SetBuffer(kernelCombine, "_VoxelPositions", voxelPosBuffer);

        argsBuffer = new ComputeBuffer(
            1,
            sizeof(uint) * 5,
            ComputeBufferType.IndirectArguments
        );
        uint idxCount   = (uint)instanceMesh.GetIndexCount(0);
        uint startIndex = (uint)instanceMesh.GetIndexStart(0);
        uint baseVertex = (uint)instanceMesh.GetBaseVertex(0);
        // argsBuffer 格式： [indexCount, instanceCount, startIndex, baseVertex, 0]
        argsBuffer.SetData(new uint[]{ idxCount, 0, startIndex, baseVertex, 0 });

        // 5) Dispatch ComputeShader（一次性体素化）
        int tx = Mathf.CeilToInt(splitCount.x / 8f);
        int ty = Mathf.CeilToInt(splitCount.y / 8f);
        int tz = Mathf.CeilToInt(splitCount.z / 8f);

        voxelCompute.Dispatch(kernelClear,   tx, ty, tz);
        voxelCompute.Dispatch(kernelX,
            Mathf.CeilToInt(splitCount.y / 8f),
            Mathf.CeilToInt(splitCount.z / 8f), 1);
        voxelCompute.Dispatch(kernelY,
            Mathf.CeilToInt(splitCount.x / 8f),
            Mathf.CeilToInt(splitCount.z / 8f), 1);
        voxelCompute.Dispatch(kernelZ,
            Mathf.CeilToInt(splitCount.x / 8f),
            Mathf.CeilToInt(splitCount.y / 8f), 1);
        voxelCompute.Dispatch(kernelCombine, tx, ty, tz);

        // 6) 拷贝计数到 argsBuffer 并异步回读
        ComputeBuffer.CopyCount(voxelPosBuffer, argsBuffer, sizeof(uint));
        isCompleted = false;
        AsyncGPUReadback.Request(argsBuffer, req =>
        {
            if (req.hasError) Debug.LogError("Voxelizer 回读错误");
            else             isCompleted = true;
        });

        // —— 计算 instanceScale ——  
        // 自动模式：每个 voxel 的大小 = AABB 尺寸 / 分割数
        if (autoScale)
        {
            instanceScale = new Vector3(
                bounds.size.x / splitCount.x,
                bounds.size.y / splitCount.y,
                bounds.size.z / splitCount.z
            );
        }
        else
        {
            instanceScale = manualScale;
        }

        // 7) 计算 world 空间下的包围盒（用于渲染剔除）
        var worldCenter = transform.TransformPoint(bounds.center);
        var worldSize   = Vector3.Scale(bounds.size, transform.lossyScale);
        drawBounds = new Bounds(worldCenter, worldSize);

        // 8) 材质属性块（用于绑定 ComputeBuffer）
        mpb = new MaterialPropertyBlock();
    }

    void Update()
    {
        if (!autoScale)
        {
            instanceScale = manualScale;
        }

        if (!isCompleted) return;

        // —— 绑定体素中心列表 & 颜色 & 缩放 ——  
        mpb.Clear();
        mpb.SetBuffer("_VoxelPositions", voxelPosBuffer);
        mpb.SetColor ("_Color",            voxelColor);
        mpb.SetVector("_InstanceScale",    instanceScale);

        // —— 间接实例化绘制 ——  
        Graphics.DrawMeshInstancedIndirect(
            instanceMesh, 0,
            instanceMaterial,
            drawBounds,
            argsBuffer,
            0,      // args offset
            mpb     // 材质属性块
        );
    }

    void OnDisable()
    {
        triBuffer?.Release();
        voxelPosBuffer?.Release();
        argsBuffer?.Release();

        DestroyImmediate(projYZ);
        DestroyImmediate(projXZ);
        DestroyImmediate(projXY);
        DestroyImmediate(volumeTex);
    }

    // Helper: 创建可写 RWTexture2D
    RenderTexture NewRWTexture2D(int w, int h)
    {
        var rt = new RenderTexture(w, h, 0,
            RenderTextureFormat.RInt, RenderTextureReadWrite.Linear);
        rt.dimension         = TextureDimension.Tex2D;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }

    // Helper: 创建可写 RWTexture3D
    RenderTexture NewRWTexture3D(int w, int h, int d)
    {
        var rt = new RenderTexture(w, h, 0,
            RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
        rt.dimension         = TextureDimension.Tex3D;
        rt.volumeDepth       = d;
        rt.enableRandomWrite = true;
        rt.Create();
        return rt;
    }
}