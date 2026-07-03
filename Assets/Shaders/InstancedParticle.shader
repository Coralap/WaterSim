Shader "Custom/InstancedParticle" {
    Properties {
        _Radius ("Particle Radius", Float) = 0.15
    }
    SubShader {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            StructuredBuffer<float3> particleBuffer;
            StructuredBuffer<float4> colorBuffer;

            float _Radius;

            v2f vert (appdata v, uint instanceID : SV_InstanceID) {
                v2f o;
                float3 bufferPos = particleBuffer[instanceID];

                float3 worldPos = (v.vertex.xyz * _Radius * 2.0) + bufferPos;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.uv = v.uv;
                o.instanceID = instanceID;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                float2 centerOffset = i.uv - float2(0.5, 0.5);

                if (length(centerOffset) > 0.5)
                    discard;

                return colorBuffer[i.instanceID];
            }
            ENDCG
        }
    }
}
