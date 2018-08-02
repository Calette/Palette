Shader "Hidden/Mistral Water/Helper/Vertex/Stockham"
{
	Properties
	{
		_Input ("Input Sampler", 2D) = "black" {}
		_TransformSize ("Transform Size", Float) = 256
		_SubTransformSize ("Log Size", Float) = 8
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
			// 相当于定义两个bool变量
			#pragma multi_compile _HORIZONTAL _VERTICAL

			uniform sampler2D _Input;
			uniform float _TransformSize;
			uniform float _SubTransformSize;

			inline float GetTwiddle(float ratio)
			{
				return -2 * PI * ratio;
			}

			float4 frag(FFTVertexOutput i) : SV_TARGET
			{
				float index;

				#ifdef _HORIZONTAL
					index = i.uv.x * _TransformSize;
				#else
					index = i.uv.y * _TransformSize;
				#endif

				// base = floor(x / n) * (n / 2)
				// offset = x % (n / 2)
				// x0 = base + offset
				float evenIndex = floor(index / _SubTransformSize) * (_SubTransformSize * 0.5) + fmod(index, _SubTransformSize * 0.5);
				// x1 = x0 + N / 2

				/*
				N = 8时,

				第一层是 0 1 2 3   4 5 6 7
				第二层是 0,4 1,5   2,6 3,7
				第三层是 0,2,4,6   1,3,5,7
				第四层是  0,1,2,3,4,5,6,7
				*/

				#ifdef _HORIZONTAL
					// A1: x1
					// A2: x0 + n / 2
					float4 even = tex2D(_Input, float2(evenIndex / _TransformSize, i.uv.y));
					float4 odd  = tex2D(_Input, float2((evenIndex + _TransformSize * 0.5) / _TransformSize, i.uv.y) );
				#else
					float4 even = tex2D(_Input, float2(i.uv.x, evenIndex / _TransformSize));
					float4 odd  = tex2D(_Input, float2(i.uv.x, (evenIndex + _TransformSize * 0.5) / _TransformSize));
				#endif

				// angel = -2 * PI * x / n
				/* 
				例:
				N = 8时,
				第一次变换:
				0 4
				index (0, 1) twiddle1
				index (1, 2) twiddle2
				twiddle1 = - twiddle2
				*/
				float twiddleV = GetTwiddle(index / _SubTransformSize);
				// W上标k下标N
				float2 twiddle = float2(cos(twiddleV), sin(twiddleV));

				// h,x
				float2 outputA = even.xy + MultComplex(twiddle, odd.xy);
				// z
				float2 outputB = even.zw + MultComplex(twiddle, odd.zw);

				return float4(outputA, outputB);
			}

			ENDCG
		}
	}
}
