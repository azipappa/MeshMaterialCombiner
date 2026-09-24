Shader "Hidden/AzipaWorks/MeshMaterialCombiner/AtlasCopy"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScaleOffset ("Scale Offset", Vector) = (1,1,0,0)
        _NormalMap ("Normal Map", Float) = 0
        _PackNormalRuntime ("Pack Normal For Runtime", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _ScaleOffset;
            float _NormalMap;
            float _PackNormalRuntime;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 sampled = tex2D(_MainTex,
                    input.uv * _ScaleOffset.xy + _ScaleOffset.zw);
                if (_NormalMap > 0.5)
                {
                    float3 normal = UnpackNormal(sampled);
                    fixed3 encoded = normal * 0.5 + 0.5;
                    if (_PackNormalRuntime > 0.5)
                    {
                        #if defined(UNITY_NO_DXT5nm)
                            return fixed4(encoded, 1.0);
                        #else
                            return fixed4(1.0, encoded.y, 1.0, encoded.x);
                        #endif
                    }
                    return fixed4(encoded, 1.0);
                }
                return sampled;
            }
            ENDCG
        }
    }
}
