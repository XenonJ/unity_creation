Shader "Custom/InstancedVoxel"
{
    Properties
    {
        _Color           ("Voxel Color",     Color)  = (1,1,1,1)
        _InstanceScale   ("Instance Scale",  Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            // 体素中心列表，由脚本绑定
            StructuredBuffer<float3> _VoxelPositions;
            // 实例化统一属性
            float4 _Color;
            float3 _InstanceScale;
            // 本地→世界矩阵
            float4x4 _LocalToWorld;

            struct appdata
            {
                float3 vertex     : POSITION;
                uint   instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                uint   instanceID : SV_InstanceID;
            };

            v2f vert(appdata v)
            {
                v2f o;

                // 1) 取出这个实例在本地空间的中心
                float3 center = _VoxelPositions[v.instanceID];

                // 2) 按 _InstanceScale 缩放立方体顶点
                float3 scaledVertex = v.vertex * _InstanceScale;

                // 3) 在本地空间加上中心偏移
                float4 localPos = float4(scaledVertex + center, 1);

                // 4) 用我们传进来的 _LocalToWorld 矩阵把它变换到世界空间
                float4 worldPos = mul(_LocalToWorld, localPos);

                // 5) 最后投影到裁剪空间
                o.pos = UnityWorldToClipPos(worldPos);
                o.instanceID = v.instanceID;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 根据 instanceID 生成伪随机 RGB
                float r = frac(i.instanceID * 0.345f);
                float g = frac(i.instanceID * 0.567f);
                float b = frac(i.instanceID * 0.789f);
                float4 randCol = float4(r, g, b, 1);
                return randCol * _Color;
            }
            ENDCG
        }
    }
}