using System;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter))]
public class Voxelizer : MonoBehaviour
{
    [Header("Compute Shader & 源 Mesh")]
    public ComputeShader voxelCompute;
    public MeshFilter sourceMeshFilter;  // 如果挂在同一个物体上，可留空

    [Header("体素分辨率")]
    public Vector3Int splitCount = new Vector3Int(32, 32, 32);

    [Header("实例缩放模式")]
    public bool autoScale = true;       // true → 按 instanceScale 自动算 splitCount
    public Vector3 instanceScale = Vector3.one * 0.9f;  // 用户定义的每个 voxel 大小

    [Header("实例化设置")]
    public Mesh instanceMesh;
    public Material instanceMaterial;
    public Color voxelColor = Color.white;

    [Header("运行时状态")]
    public float instanceStep = 0.0f;
    public float animSpeed = 1.0f;
    public float animDelay = 0.0f;

    [HideInInspector] public bool voxelRender = false;
    [HideInInspector] public bool animCompleted = false; // 实例数量

    // 私有 GPU 资源
    private RenderTexture projYZ, projXZ, projXY, volumeTex;
    private ComputeBuffer triBuffer, voxelPosBuffer, argsBuffer;
    private int kernelClear, kernelX, kernelY, kernelZ, kernelCombine, kernelReorder;
    private Bounds drawBounds;
    private MaterialPropertyBlock mpb;
    private int voxelCount = 0; // 用于存储有效体素数

    void Start()
    {
        InitializeMeshData();          // 拆三角形数据到 StructuredBuffer
        ComputeSplitCount();           // 计算 splitCount
        CreateTextures();             // 创建投影 & 体素 RWTexture
        BindKernelsAndResources();    // 找 Kernels 并绑定 Buffer/Texture
        CreateBuffers();              // 创建 Append Buffer 和 Indirect Args Buffer
        DispatchComputeAndReadback(); // Dispatch ComputeShader 并异步回读
        SetupDrawBounds();            // 计算 world 空间下的包围盒
        InitializeMaterialBlock();    // 初始化 MaterialPropertyBlock
    }

    void Update()
    {
        if (voxelRender)
        {
            instanceStep += (float)animSpeed * Time.deltaTime * 10.0f;
            if (instanceStep >= voxelCount + animDelay * 10.0f)
            {
                instanceStep = 0;
                animCompleted = true; // 动画完成标志
                voxelRender = false; // 重置渲染状态
            }
            RenderVoxels();           // 绘制实例
        }
        if (animCompleted && !gameObject.GetComponent<MeshRenderer>().enabled)
        {
            // 如果动画完成，可以在这里添加其他逻辑
            Debug.Log("Voxelization 动画已完成");
            gameObject.GetComponent<MeshRenderer>().enabled = true; // 启用 MeshRenderer
            gameObject.GetComponent<Voxelizer>().enabled = false; // 禁用 Voxelizer
        }
    }

    void OnDisable()
    {
        ReleaseResources();           // 释放 GPU 资源
    }

    // 拆三角形数据到 StructuredBuffer
    void InitializeMeshData()
    {
        var mesh = sourceMeshFilter != null
                    ? sourceMeshFilter.sharedMesh
                    : GetComponent<MeshFilter>().sharedMesh;
        var verts = mesh.vertices;
        var inds = mesh.triangles;
        int triCount = inds.Length / 3;
        Vector3[] triVerts = new Vector3[inds.Length];
        for (int i = 0; i < inds.Length; i++)
            triVerts[i] = verts[inds[i]];

        triBuffer = new ComputeBuffer(triVerts.Length, sizeof(float) * 3);
        triBuffer.SetData(triVerts);
        voxelCompute.SetInt("_TriangleCount", triCount);
    }

    // 计算 splitCount
    // splitCount = bounds.size / instanceScale
    void ComputeSplitCount()
    {
        var bounds = (sourceMeshFilter != null
                        ? sourceMeshFilter.sharedMesh
                        : GetComponent<MeshFilter>().sharedMesh).bounds;
        if (autoScale)
        {
            splitCount = new Vector3Int(
                Mathf.CeilToInt(bounds.size.x / instanceScale.x),
                Mathf.CeilToInt(bounds.size.y / instanceScale.y),
                Mathf.CeilToInt(bounds.size.z / instanceScale.z)
            );
        }
    }

    // 创建投影 & 体素 RWTexture
    void CreateTextures()
    {
        projYZ = NewRWTexture2D(splitCount.y, splitCount.z);
        projXZ = NewRWTexture2D(splitCount.x, splitCount.z);
        projXY = NewRWTexture2D(splitCount.x, splitCount.y);
        volumeTex = NewRWTexture3D_Int(splitCount.x, splitCount.y, splitCount.z);
    }

    // 找 Kernels 并绑定 Buffer/Texture
    void BindKernelsAndResources()
    {
        kernelClear = voxelCompute.FindKernel("CS_Clear");
        kernelX = voxelCompute.FindKernel("CS_ProjectX");
        kernelY = voxelCompute.FindKernel("CS_ProjectY");
        kernelZ = voxelCompute.FindKernel("CS_ProjectZ");
        kernelCombine = voxelCompute.FindKernel("CS_Combine");
        kernelReorder = voxelCompute.FindKernel("CS_Reorder");

        voxelCompute.SetBuffer(kernelX, "_TriangleVerts", triBuffer);
        voxelCompute.SetBuffer(kernelY, "_TriangleVerts", triBuffer);
        voxelCompute.SetBuffer(kernelZ, "_TriangleVerts", triBuffer);
        voxelCompute.SetTexture(kernelClear, "_Volume", volumeTex);
        voxelCompute.SetTexture(kernelClear, "_ProjYZ", projYZ);
        voxelCompute.SetTexture(kernelClear, "_ProjXZ", projXZ);
        voxelCompute.SetTexture(kernelClear, "_ProjXY", projXY);
        voxelCompute.SetTexture(kernelX, "_ProjYZ", projYZ);
        voxelCompute.SetTexture(kernelY, "_ProjXZ", projXZ);
        voxelCompute.SetTexture(kernelZ, "_ProjXY", projXY);
        voxelCompute.SetTexture(kernelCombine, "_Volume", volumeTex);
        voxelCompute.SetTexture(kernelCombine, "_ProjYZ", projYZ);
        voxelCompute.SetTexture(kernelCombine, "_ProjXZ", projXZ);
        voxelCompute.SetTexture(kernelCombine, "_ProjXY", projXY);
        voxelCompute.SetTexture(kernelReorder, "_Volume", volumeTex);

        var bounds = (sourceMeshFilter != null
                        ? sourceMeshFilter.sharedMesh
                        : GetComponent<MeshFilter>().sharedMesh).bounds;
        voxelCompute.SetVector("_AABBMin", bounds.min);
        voxelCompute.SetVector("_AABBMax", bounds.max);
        voxelCompute.SetInts("_SplitCount",
            splitCount.x, splitCount.y, splitCount.z);
    }

    // 创建 Append Buffer 和 Indirect Args Buffer
    void CreateBuffers()
    {
        int maxVoxels = splitCount.x * splitCount.y * splitCount.z;
        voxelPosBuffer = new ComputeBuffer(
            maxVoxels,
            sizeof(float) * 3,
            ComputeBufferType.Append
        );
        voxelPosBuffer.SetCounterValue(0);
        voxelCompute.SetBuffer(kernelReorder, "_VoxelPositions", voxelPosBuffer);

        argsBuffer = new ComputeBuffer(
            1,
            sizeof(uint) * 5,
            ComputeBufferType.IndirectArguments
        );
        uint idxCount = (uint)instanceMesh.GetIndexCount(0);
        uint startIndex = (uint)instanceMesh.GetIndexStart(0);
        uint baseVertex = (uint)instanceMesh.GetBaseVertex(0);
        argsBuffer.SetData(new uint[] { idxCount, 0, startIndex, baseVertex, 0 });
    }

    // Dispatch ComputeShader 并异步回读
    void DispatchComputeAndReadback()
    {
        int tx = Mathf.CeilToInt(splitCount.x / 8f);
        int ty = Mathf.CeilToInt(splitCount.y / 8f);
        int tz = Mathf.CeilToInt(splitCount.z / 8f);

        voxelCompute.Dispatch(kernelClear, tx, ty, tz);
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
        voxelCompute.Dispatch(kernelReorder, 1, 1, 1);

        ComputeBuffer.CopyCount(voxelPosBuffer, argsBuffer, sizeof(uint));
        voxelRender = false;
        AsyncGPUReadback.Request(argsBuffer, req =>
        {
            if (req.hasError) Debug.LogError("Voxelizer 回读错误");
            else
            {
                var data = req.GetData<uint>().ToArray();
                uint indexCount = data[0];
                uint aliveVoxelCnt = data[1];
                uint startIndex = data[2];
                uint baseVertex = data[3];
                Debug.Log($"活跃体素数 = {aliveVoxelCnt}");
                voxelCount = (int)aliveVoxelCnt;

                voxelRender = true;
            }
        });
    }

    // 计算 world 空间下的包围盒
    void SetupDrawBounds()
    {
        var bounds = (sourceMeshFilter != null
                        ? sourceMeshFilter.sharedMesh
                        : GetComponent<MeshFilter>().sharedMesh).bounds;
        var worldCenter = transform.TransformPoint(bounds.center);
        var worldSize = Vector3.Scale(bounds.size, transform.lossyScale);
        drawBounds = new Bounds(worldCenter, worldSize);
    }

    // 初始化 MaterialPropertyBlock
    void InitializeMaterialBlock()
    {
        mpb = new MaterialPropertyBlock();
    }

    // 绘制实例
    void RenderVoxels()
    {
        mpb.Clear();
        mpb.SetBuffer("_VoxelPositions", voxelPosBuffer);
        mpb.SetColor("_Color", voxelColor);
        mpb.SetVector("_InstanceScale", instanceScale);
        mpb.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        mpb.SetInt("_InstanceStep", (int)instanceStep);
        Graphics.DrawMeshInstancedIndirect(
            instanceMesh, 0,
            instanceMaterial,
            drawBounds,
            argsBuffer,
            0,
            mpb,
            ShadowCastingMode.Off,
            false
        );
    }

    // 释放 GPU 资源
    void ReleaseResources()
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
            RenderTextureFormat.RInt, RenderTextureReadWrite.Linear)
        {
            dimension = TextureDimension.Tex2D,
            enableRandomWrite = true
        };
        rt.Create();
        return rt;
    }

    // Helper: 创建可写 RWTexture3D
    RenderTexture NewRWTexture3D(int w, int h, int d)
    {
        var rt = new RenderTexture(w, h, 0,
            RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
        {
            dimension = TextureDimension.Tex3D,
            volumeDepth = d,
            enableRandomWrite = true
        };
        rt.Create();
        return rt;
    }
    
    // Helper: 创建可写 RWTexture3D（整数格式）
    RenderTexture NewRWTexture3D_Int(int w, int h, int d)
    {
        var rt = new RenderTexture(w, h, 0,
            RenderTextureFormat.RInt,
            RenderTextureReadWrite.Linear)
        {
            dimension = TextureDimension.Tex3D,
            volumeDepth = d,
            enableRandomWrite = true
        };
        rt.Create();
        return rt;
    }
}
