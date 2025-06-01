// VoxelInstancer.cs
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(RandomVoxelGenerator))]
public class VoxelInstancer : MonoBehaviour
{
    [Header("实例化用 Mesh & Material")]
    public Mesh     instanceMesh;
    public Material instanceMaterial;

    // 每次 DrawMeshInstanced 最多 1023 个实例
    const int        MAX_INSTANCES_PER_BATCH = 1023;
    RandomVoxelGenerator generator;
    List<Matrix4x4[]>    batches = new List<Matrix4x4[]>();

    private void Awake()
    {
        generator = GetComponent<RandomVoxelGenerator>();
        BuildBatches();
    }

    // 根据 generator.VoxelPositions 拆分成多个矩阵数组
    void BuildBatches()
    {
        batches.Clear();
        var mats = new List<Matrix4x4>();

        foreach (var pos in generator.VoxelPositions)
        {
            // 每个立方体大小 1，且无旋转
            mats.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one));

            if (mats.Count == MAX_INSTANCES_PER_BATCH)
            {
                batches.Add(mats.ToArray());
                mats.Clear();
            }
        }

        if (mats.Count > 0)
            batches.Add(mats.ToArray());
    }

    private void Update()
    {
        // 渲染所有批次
        for (int i = 0; i < batches.Count; i++)
        {
            Graphics.DrawMeshInstanced(
                instanceMesh, 0,
                instanceMaterial,
                batches[i],
                batches[i].Length
            );
        }
    }
}