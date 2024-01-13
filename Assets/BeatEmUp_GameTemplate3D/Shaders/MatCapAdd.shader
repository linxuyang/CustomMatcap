Shader "Custom/MatcapAdditive"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _MatCap ("MatCap (RGB)", 2D) = "white" {}
        _AOMap ("Ambient Occlusion Map (RGB)", 2D) = "white" {}
    }

    Subshader
    {
        Tags
        {
            "RenderType"="Opaque"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma fragmentoption ARB_precision_hint_fastest
            // #pragma shader_feature MATCAP_ACCURATE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float3 positionOS : POSITION;
                half2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 tToV0 : TEXCOORD1;
                float3 tToV1 : TEXCOORD2;
                half fogFactor : TEXCOORD4;
            };

            sampler2D _MainTex;
            sampler2D _AOMap;
            sampler2D _BumpMap;
            sampler2D _MatCap;

            half4 _MainTex_ST;
            half4 _BumpMap_ST;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.positionOS);

                o.uv.xy = TRANSFORM_TEX(v.uv, _MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv, _BumpMap);

                float3 binormalOS = cross(v.normalOS, v.tangentOS.xyz) * v.tangentOS.w * GetOddNegativeScale();;
                float3x3 tToO = float3x3(v.tangentOS.xyz, binormalOS, v.normalOS);
                o.tToV0 = normalize(mul(tToO, UNITY_MATRIX_IT_MV[0].xyz));
                o.tToV1 = normalize(mul(tToO, UNITY_MATRIX_IT_MV[1].xyz));
                o.fogFactor = ComputeFogFactor(o.pos.z); //雾参数，剪裁空间的z代表离相机距离

                return o;
            }

            inline half3 GammaToLinearSpace(half3 sRGB)
            {
                return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);
            }

            inline float LinearToGammaSpaceExact (float value)
            {
                if (value <= 0.0F)
                    return 0.0F;
                else if (value <= 0.0031308F)
                    return 12.92F * value;
                else if (value < 1.0F)
                    return 1.055F * pow(value, 0.4166667F) - 0.055F;
                else
                    return pow(value, 0.45454545F);
            }

            half4 frag(v2f i) : COLOR
            {
                half4 tex = tex2D(_MainTex, i.uv.xy);
                half4 _AOTex = tex2D(_AOMap, i.uv.xy);
                half3 normals = UnpackNormal(tex2D(_BumpMap, i.uv.zw));

                half2 capCoord = half2(dot(i.tToV0, normals), dot(i.tToV1, normals));
                float4 mc = (tex + (tex2D(_MatCap, capCoord * 0.5 + 0.5) * 2.0) - 1.0) * _AOTex;

                mc.rgb = MixFog(mc.rgb, i.fogFactor);
                return mc;
            }
            ENDHLSL
        }
    }

}