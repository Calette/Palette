Shader "Hidden/Mistral Water/Helper/Map/Ocean Normal"
{
	Properties
	{
		_DisplacementMap ("Displacement Map", 2D) = "black" {}
		_HeightMap ("Height Map", 2D) = "black" {}
		_Length ("Wave Length", Float) = 512
		_Resolution ("Resolution", Float) = 512
	}

	SubShader
	{
		Pass
		{
			Cull Off
			ZWrite Off
			ZTest Off
			ColorMask RGBA

			CGPROGRAM

			#include "FFTCommon.cginc"

			#pragma vertex vert_quad
			#pragma fragment frag

			uniform sampler2D _DisplacementMap;
			uniform sampler2D _HeightMap;
			uniform float _Length;
			uniform float _Resolution;

			inline float3 GetVec(float4 tc)
			{
				float2 xz = tex2D(_DisplacementMap, tc).rb;
				float y = tex2D(_HeightMap, tc).r;
				return float3(xz.x, y, xz.y);
			}

			float4 frag(FFTVertexOutput i) : SV_TARGET
			{
				// uv坐标
				float texel = 1 / _Resolution;
				// 实际坐标
				float texelSize = _Length / _Resolution;

				// 得到当前点的xz位移
				float3 center = tex2D(_DisplacementMap, i.uv).rgb;

				// 获取上下左右四个点的位置与当前点的连线
				float3 right = float3(texelSize, 0, 0) + GetVec(i.uv + float4(texel, 0, 0, 0)) - center;
				float3 left = float3(-texelSize, 0, 0) + GetVec(i.uv + float4(-texel, 0, 0, 0)) - center;
				float3 top = float3(0, 0, -texelSize) + GetVec(i.uv + float4(0, -texel, 0, 0)) - center;
				float3 bottom = float3(0, 0, texelSize) + GetVec(i.uv + float4(0, texel, 0, 0)) - center;

				// 计算四个面的法向量
				float3 topRight = cross(right, top);
				float3 topLeft = cross(top, left);
				float3 bottomLeft = cross(left, bottom);
				float3 bottomRight = cross(bottom, right);

				//return float4(normalize(float3(-(tex2D(_DisplacementMap, i.texcoord).r), 1, -(tex2D(_DisplacementMap, i.texcoord).b))), 1.0);

				// 平均四个面的法向量
				return float4(normalize(topRight + topLeft + bottomLeft + bottomRight), 1.0);
			}

			ENDCG
		}
	}
}