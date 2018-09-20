Shader "Hidden/Blend"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Exposure("WhiteDegree", Float) = 2
	}
		SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			sampler2D _MainTex;
			sampler2D _Src;
			float _Exposure;

			fixed4 frag (v2f i) : SV_Target
			{
				fixed3 col = tex2D(_MainTex, i.uv).rgb;
				fixed3 src = tex2D(_Src, i.uv).rgb;
				col += src;

				if (_Exposure > 0)
					col = float3(1.0, 1.0, 1.0) - exp(-col * _Exposure);
				//col /= (fixed3(1, 1, 1) + col);
				//col = col / (col.r * 0.27 + col.g * 0.67 + col.b * 0.06);
				return fixed4(col, 1.0);
			}
			ENDCG
		}
	}
}
