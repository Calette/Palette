Shader "Hidden/Height"
{
	Properties
	{
		_Anim ("Displacement Map", 2D) = "black" {}
		_Height ("Height Map", 2D) = "black" {}
		_U("u", float) = 0
		_V("v", float) = 0
	}

	SubShader
	{
		Pass
		{
			ZWrite Off
			ZTest Off

			CGPROGRAM

			#include "UnityCG.cginc"

			#pragma vertex vert
			#pragma fragment frag

			struct data
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
			};

			uniform sampler2D _Anim;
			uniform sampler2D _Height;
			float _U;
			float _V;

			v2f vert(data v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				return o;
			}

			float4 frag(v2f i) : SV_TARGET
			{
				float2 uv = float2(_U, _V);

				float4 pos;
				pos.y = tex2D(_Height, uv).r / 8;
				pos.xz = tex2D(_Anim, uv).rb / 8;

				return pos;
			}

			ENDCG
		}
	}
}