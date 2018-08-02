Shader "Hidden/Mistral Water/Helper/Vertex/Spectrum Height"
{
	Properties
	{
		_Phase ("Last Phase", 2D) = "black" {}
		_Initial ("Intial Spectrum", 2D) = "black" {}
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

			uniform sampler2D _Phase;
			uniform sampler2D _Initial;

			// 蝴蝶网络第一层 A
			// h(x, t) = ∑ htilde(k,t) * exp(i * k·x))
			// A = htilde(k,t) * exp(i * k·x))
			// htilde(k,t) = h0 * exp{i * w(k) * t} + h0mk_Conj * exp{-i * w(k) * t}
			// 返回值是一个复数(参考Gerstner Wave,猜测x是高度(sinθ),y是位移量(cosθ))
			float4 frag(FFTVertexOutput i) : SV_TARGET
			{
				float phase = tex2D(_Phase, i.uv).r;
				// exp{i * w(k) * t} = exp{i * phase} = cos(phase) + i * sin(phase)
				float2 pv = float2(cos(phase), sin(phase));

				float2 h0 = tex2D(_Initial, i.uv).rg;
				float2 h0conj = tex2D(_Initial, i.uv).ba;
				float2 h = MultComplex(h0, pv) + MultComplex(h0conj, Conj(pv));

				/*
				!!以上算出来的是htilde(k,t)
				但是,根据离散傅里叶变换,蝴蝶网络第一层的公式:
				A = f[n] * exp(-2iπnm / N)
				N = 1
				exp(-2iπnm / N) = 1
				A = f[n]
				所以
				A = htilde(k,t) = h0 * exp{i * w(k) * t} + h0mk_Conj * exp{-i * w(k) * t}
				*/

				return float4(h, 0, 0);
			}

			ENDCG
		}
	}
}
