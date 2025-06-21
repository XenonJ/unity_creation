Shader "Custom/InstancedVoxel"
{
    Properties
    {
        _Color           ("Voxel Color",     Color)  = (1,1,1,1)
        _InstanceScale   ("Instance Scale",  Vector) = (1,1,1,0)
        _InstanceStep    ("Instance Step",   Int)  = 0
    }
    SubShader
    {
        LOD 100

        Pass
        {
            // Tags { "LightMode"="ForwardBase" }
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            // 体素中心列表，由脚本绑定
            StructuredBuffer<float3> _VoxelPositions;
            // 实例化统一属性
            float4 _Color;
            float3 _InstanceScale;
            // 本地→世界矩阵
            float4x4 _LocalToWorld;
            // 实例化进度
            uint _InstanceStep;

            struct appdata
            {
                float3 vertex     : POSITION;
                float3 normal     : NORMAL;
                uint   instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float3 normal     : TEXCOORD0;
                uint   instanceID : TEXCOORD1;
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

                // 4.5) 计算法线
                o.normal = normalize(mul((float3x3)_LocalToWorld, v.normal));

                // 5) 最后投影到裁剪空间
                o.pos = UnityWorldToClipPos(worldPos);
                o.instanceID = v.instanceID;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 根据进度丢弃
                if (i.instanceID >= _InstanceStep)
                    discard;

                // 1) 随机色
                float3 randCol = float3(
                    frac(i.instanceID * 0.345f),
                    frac(i.instanceID * 0.567f),
                    frac(i.instanceID * 0.789f)
                );
                float3 baseCol = randCol * _Color.rgb;

                // 2) 环境光
                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.xyz;

                // 3) 主定向光漫反射
                float3 N = normalize(i.normal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float  lambert = saturate(dot(N, L));
                float3 diff = lambert * _LightColor0.rgb;

                // 4) 合成
                float3 final = baseCol * (ambient + diff);

                return float4(final, _Color.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}