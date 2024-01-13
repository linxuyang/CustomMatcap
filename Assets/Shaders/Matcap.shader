Shader "Unlit/Matcap"
{
    Properties
    {
    	_MainTex("MainTex", 2D) = "white" {}
    	_MainColor("MainColor", Color) = (1, 1, 1, 1)
    	_NormalMap("NormalMap", 2D) = "bump" {}
    	_NormalScale("NormalScale", Range(0, 2)) = 1
        [NoScaleOffset]_DiffuseMatCap("DiffuseMatCap", 2D) = "white" {}
    	_DiffuseStrength("DiffuseStrength", Range(0, 5)) = 1
        [NoScaleOffset]_SpecMatCap("SpecMatCap", 2D) = "black" {}
    	_SpecStrength("SpecStrength", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
        	Tags { "LightMode" = "UniversalForward" }
        	
            HLSLPROGRAM
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex Vert
			#pragma fragment Frag

			struct VertInput
            {
	            float3 positionOS : POSITION;
            	half2 uv : TEXCOORD0;
            	float3 normalOS : NORMAL;
            	float4 tangentOS : TANGENT;
            };
            
			struct FragInput
            { 
				float4 positionCS : SV_POSITION;
				half4 uv : TEXCOORD0;
				float3 tToV0 : TEXCOORD1;
				float3 tToV1 : TEXCOORD2;
				float3 viewDir : TEXCOORD3;
            };

            sampler2D _MainTex;
            sampler2D _NormalMap;
            sampler2D _DiffuseMatCap;
            sampler2D _SpecMatCap;

            half4 _MainTex_ST;
            half4 _NormalMap_ST;
			half3 _MainColor;
            half _NormalScale;
            half _DiffuseStrength, _SpecStrength;
				
			FragInput Vert(VertInput input)
			{
				FragInput output;
				output.positionCS = TransformObjectToHClip(input.positionOS);
				output.uv.xy = TRANSFORM_TEX(input.uv, _MainTex);
				output.uv.zw = TRANSFORM_TEX(input.uv, _NormalMap);
				float3 binormalOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w * GetOddNegativeScale();;
				float3x3 tToO = float3x3(input.tangentOS.xyz, binormalOS, input.normalOS);
				output.tToV0 = normalize(mul(tToO, UNITY_MATRIX_IT_MV[0].xyz));
				output.tToV1 = normalize(mul(tToO, UNITY_MATRIX_IT_MV[1].xyz));
				float3 viewDir = GetWorldSpaceViewDir(TransformObjectToWorld(input.positionOS));
				output.viewDir = TransformWorldToViewDir(-viewDir);
				return output;
			}
			
			half4 Frag(FragInput input) : SV_Target
			{
				half3 mainColor = tex2D(_MainTex, input.uv.xy).rgb * _MainColor;
				float3 normalTS = UnpackNormalScale(tex2D(_NormalMap, input.uv.zw), _NormalScale);
				normalTS = normalize(normalTS);
				input.viewDir = normalize(input.viewDir);
				float2 normalVS;
				normalVS.x = dot(input.tToV0, normalTS);
				normalVS.y = dot(input.tToV1, normalTS);

				normalVS = lerp(normalVS, clamp(normalVS + sign(input.viewDir) * 0.5, -1, 1), abs(input.viewDir));
				normalVS /= max(length(normalVS), 1);
				normalVS = normalVS * 0.5 + 0.5;
				half3 diffuseMatcap = tex2D(_DiffuseMatCap, normalVS).rgb;
				diffuseMatcap *= mainColor * _DiffuseStrength;
				half3 specMatcap = tex2D(_SpecMatCap, normalVS).rgb;
				specMatcap *= _SpecStrength;
				half3 finalColor = diffuseMatcap + specMatcap;
				return half4(finalColor, 1);
			}
            ENDHLSL
        }
    }
}