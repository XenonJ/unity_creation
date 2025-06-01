// RandomVoxelGenerator.cs
using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
public class RandomVoxelGenerator : MonoBehaviour
{
    [Header("体素分辨率")]
    public int resX = 16;
    public int resY = 16;
    public int resZ = 16;

    [Header("填充概率（0~1）")]
    [Range(0f, 1f)] public float fillProbability = 0.1f;

    // 生成出来的体素中心坐标列表
    [HideInInspector] public List<Vector3> VoxelPositions = new List<Vector3>();

    // 在编辑器和运行时都能重新生成
    public void Generate()
    {
        VoxelPositions.Clear();
        for (int x = 0; x < resX; x++)
            for (int y = 0; y < resY; y++)
                for (int z = 0; z < resZ; z++)
                {
                    if (Random.value < fillProbability)
                    {
                        // 这里体素大小默认为 1，坐标即格子中心
                        VoxelPositions.Add(new Vector3(x + 0.5f, y + 0.5f, z + 0.5f));
                    }
                }
    }

    private void OnValidate()
    {
        // 编辑器里改参数时自动刷新
        Generate();
    }

    private void Awake()
    {
        // 进入 Play Mode 时也生成一次
        Generate();
    }
}