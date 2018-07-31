Shader "Hidden/Mistral Water/Helper/Vertex/Spectrum"
{
	Properties
	{
		_Length ("Wave Length", Float) = 256
		_Resolution ("Ocean Resolution", int) = 256
		_Phase ("Last Phase", 2D) = "black" {}
		_Initial ("Intial Spectrum", 2D) = "black" {}
		_Choppiness ("Choppiness", Float) = 1
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
			uniform float _Length;
			uniform int _Resolution;
			uniform float _Choppiness;

			float4 frag(FFTVertexOutput i) : SV_TARGET
			{
				float n = (i.uv.x * _Resolution);
				float m = (i.uv.y * _Resolution);
				float2 wave = GetWave(n, m, _Length, _Resolution);
				float w = length(wave);

				float phase = tex2D(_Phase, i.uv).r;
				float2 pv = float2(cos(phase), sin(phase));
				float2 h0 = tex2D(_Initial, i.uv).rg;
				float2 h0conj = tex2D(_Initial, i.uv).ba;
				//h0conj = MultByI(h0conj);
				float2 h = MultComplex(h0, pv) + MultComplex(h0conj, Conj(pv));

				// ∑ -i * k.normalized * htilde(k,t) * exp(i * k·x))
				// - i * i = 1
				w = max(0.000001, w);
				float2 hx = -MultByI(h * wave.x / w) * _Choppiness;
				float2 hz = -MultByI(h * wave.y / w) * _Choppiness;
				return float4(hx, hz);
			}

			ENDCG
		}
	}
}
