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

            struct appdata
            {
                float3 vertex     : POSITION;
                uint   instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                uint   instanceID : SV_InstanceID;
            };

            v2f vert(appdata v)
            {
                v2f o;
                // 从 Buffer 取出这个实例的中心（局部空间）
                float3 center = _VoxelPositions[v.instanceID];

                // 缩放立方体顶点
                float3 scaledVertex = v.vertex * _InstanceScale;

                // 先 Object→World，再平移到 center（局部）→得到世界坐标
                float4 worldPos = mul(unity_ObjectToWorld, float4(scaledVertex, 1));
                worldPos.xyz += center;

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

                // 最终颜色 = 随机色 × BaseTint
                return randCol * _Color;
            }
            ENDCG
        }
    }
}