Shader "Test/NewUnlitShader"
{
	Properties
	{
		_Anim("Displacement Map", 2D) = "black" {}
		_Height("Height Map", 2D) = "black" {}
		_Bump("Normal Map",2D) = "bump" {}
		_White("White Cap Map", 2D) = "black" {}
	}
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float4 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float4 color : TEXCOORD1;
				float3 posWorld : TEXCOORD2;
			};

			uniform sampler2D _Anim;
			uniform sampler2D _Height;
			uniform sampler2D _Bump;
			uniform sampler2D _White;

			v2f vert(appdata v)
			{
				v2f o;

				float3 pos = v.vertex;
				pos.y += tex2Dlod(_Height, v.texcoord).r / 8;
				pos.xz += tex2Dlod(_Anim, v.texcoord).rb / 8;

				o.pos = UnityObjectToClipPos(pos);
				o.posWorld = mul(unity_ObjectToWorld, pos);

				o.color = tex2Dlod(_White, v.texcoord).r;

				o.uv = v.texcoord;

				return o;
			}

			fixed4 frag(v2f i) : SV_Target
			{
				//return fixed4(i.normal, 1);
				//return i.color;
				fixed4 col = fixed4(i.uv, 1, 1) + i.color;
				return col;
			}
			ENDCG
		}
	}
}
