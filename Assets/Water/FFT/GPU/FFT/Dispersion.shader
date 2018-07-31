/*
Dispersion

我们注意到如果在水面渲染过程中修改风向或是波长的话, 将会导致Dispersion发生骤变, 水面也会产生抖动和闪烁. 
因此我们将交替使用两张Dispersion Texture来保证在修改参数前后的连续性.
用法就是用_DeltaTime来计算相差的deltaPhase进行平滑过渡
*/
Shader "Hidden/Mistral Water/Helper/Vertex/Dispersion"
{
	Properties
	{
		_Length ("Wave Length", Float) = 256
		_Resolution ("Ocean Resolution", int) = 256
		_DeltaTime ("Delta Time", Float) = 0.016
		_Phase ("Last Phase", 2D) = "black" {}
	}

	SubShader
	{
		Pass
		{
			Cull Off
			ZWrite Off
			ZTest Off
			ColorMask R

			CGPROGRAM

			#include "FFTCommon.cginc"

			#pragma vertex vert_quad
			#pragma fragment frag

			uniform float _DeltaTime;
			uniform float _Length;
			uniform int _Resolution;
			uniform sampler2D _Phase;

			inline float GetDispersion(float oldPhase, float newPhase)
			{
				return fmod(oldPhase + newPhase, 2 * PI);
			}

			float4 frag(FFTVertexOutput i) : SV_TARGET
			{
				float n = (i.uv.x * _Resolution);
				float m = (i.uv.y * _Resolution);

				float deltaPhase = CalcDispersion(n, m, _Length, _DeltaTime, _Resolution);
				float phase = tex2D(_Phase, i.uv).r;

				return float4(GetDispersion(phase, deltaPhase), 0, 0, 0);
			}

			ENDCG
		}
	}
}
