Shader "Unlit/NewUnlitShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			CGPROGRAM
			#pragma multi_compile_fwdbase	
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			#include "AutoLight.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
				float3 worldPos : TEXCOORD1;
				SHADOW_COORDS(2)
				float4 pos2 : TEXCOORD3;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.pos = UnityObjectToClipPos(v.vertex);
				o.pos2 = o.pos;
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				TRANSFER_SHADOW(o);
				return o;
			}
			
			struct Point
			{
				float4 pos : SV_POSITION;
				SHADOW_COORDS(0)
			};

			fixed4 frag (v2f i) : SV_Target
			{
				//return float4(i.worldPos, 1);
				//float4 shadowCoord = mul(unity_WorldToShadow[0], float4(i.worldPos, 1));

				Point p;
				//p.pos = UnityObjectToClipPos(mul(unity_WorldToObject, float4(i.worldPos, 1)));

				p.pos = mul(UNITY_MATRIX_VP, float4(i.worldPos, 1));

				//return p.pos;

				TRANSFER_SHADOW(p);

				//UNITY_LIGHT_ATTENUATION(atten, p, i.worldPos);
				float atten = SHADOW_ATTENUATION(p);

				return atten;

				UNITY_LIGHT_ATTENUATION(atten2, i, i.worldPos);
				return atten2;

				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}
	}
	FallBack "Specular"
}
