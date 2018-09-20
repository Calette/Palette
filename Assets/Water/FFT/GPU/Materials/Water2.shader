Shader "Custom/Water2"
{
	Properties
	{
		_Anim ("Displacement Map", 2D) = "black" {}
		_Height ("Height Map", 2D) = "black" {}
		_Bump ("Normal Map",2D) = "bump" {}
		_White ("White Cap Map", 2D) = "black" {}
		_LightWrap ("Light Wrapping Value", Float) = 1
		_Tint ("Color Tint(50,70,80)(60,75,85)(75,95,105)(80,100,110)(100,155,170)", Color) = (0.5, 0.65, 0.75, 1)
		_SpecularColor ("Specular Color", Color) = (1, 0.25, 0, 1)
		_Glossiness ("Glossiness", Float) = 64
		_WhiteDegree ("WhiteDegree", Float) = 1
		_RimColor("Rim Color", Color) = (0, 0, 1, 1)
		_RimDegree("Rim Degree", Float) = 1.5

		_WaveColor("Wave Color", Color) = (0.8, 0.9, 0.6, 1)
		_SeaHeight("SeaHeight", Float) = 0
		_WaveColorDegree("_WaveColorDegree", Float) = 1
	}

	SubShader
	{
		CGINCLUDE

		#include "UnityCG.cginc"
		#include "Lighting.cginc"
		#include "UnityPBSLighting.cginc"

		sampler2D _Anim;
		sampler2D _Height;
		sampler2D _Bump;
		sampler2D _White;

		float4 _Tint;
		float4 _SpecularColor;
		float4 _WaveColor;
		float _Glossiness;
		float _WhiteDegree;
		float _LightWrap;
		fixed4 _RimColor;
		float _RimDegree;
		float _SeaHeight;
		float _WaveColorDegree;

		struct VertexInput
		{
			float4 vertex : POSITION;
			float4 texcoord : TEXCOORD0;
		};

		struct VertexOutput
		{
			float4 pos : SV_POSITION;
			float4 uv : TEXCOORD0;
			float4 color : TEXCOORD1;
			float3 posWorld : TEXCOORD2;
		};

		VertexOutput vert(VertexInput v)
		{
			VertexOutput o;

			o.uv = v.texcoord;

			float3 pos = v.vertex;
			pos.y += tex2Dlod(_Height, v.texcoord).r / 8;
			pos.xz += tex2Dlod(_Anim, v.texcoord).rb / 8;

			o.pos = UnityObjectToClipPos(pos);
			o.posWorld = mul(unity_ObjectToWorld, pos);

			o.color = tex2Dlod(_White, v.texcoord).r;

			return o;
		}

		ENDCG

		Pass
		{
			Tags{ "LightMode" = "ForwardBase" }

			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			float4 frag(VertexOutput i) : COLOR
			{
				//return tex2D(_Height, i.uv);

				float3 normal = normalize(UnityObjectToWorldNormal(tex2Dlod(_Bump, i.uv).rgb));
				//return float4(normal, 1);

				float3 lightDir = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz, _WorldSpaceLightPos0.w));

				//return float4(i.posWorld.x / 50, i.posWorld.y / 10, i.posWorld.z / 50, 1);
				//return float4(lightDir, 1);
				float3 distance = _WorldSpaceCameraPos.xyz - i.posWorld.xyz;

				float3 view = normalize(distance);

				float4 diffuse = saturate(dot(normal, lightDir));
				diffuse = pow(saturate(diffuse * (1 - _LightWrap) + _LightWrap), 2 * _LightWrap + 1) * _Tint * _LightColor0;

				// 高光用blinn光照模型效果会好一些
				float3 H = normalize(view + lightDir);
				float NdotH = saturate(dot(normal, H));
				float4 specular = _SpecularColor * saturate(pow(NdotH, _Glossiness)) * _LightColor0;

				float4 rim = _RimColor * pow(max(0, 1 - saturate(dot(normal, view))), _RimDegree);

				//return specular;
				float4 white = saturate(pow(i.color / 2, 2));
				//return white;

				// 参考 https://www.shadertoy.com/view/Ms2SD1
				// 波浪
				//float atten = max(1.0 - dot(distance, distance) * 0.001, 0.0);
				float3 color = _WaveColor * (i.posWorld.y - _SeaHeight) * _WaveColorDegree;// * atten;

				//diffuse = lerp(diffuse, _WaveColor, i.color.r);

				return diffuse + specular + white + rim + float4(color, 1);
				//return diffuse + specular + pow(i.color / 2, _WhiteDegree) + rim;
			}

			ENDCG
		}


		Pass
		{
			Tags{ "LightMode" = "ForwardAdd" }

			Blend One One

			CGPROGRAM

			#define POINT
			
			#include "Autolight.cginc" 

			#pragma vertex vert      
			#pragma fragment frag  

			float4 frag(VertexOutput i) : COLOR
			{
				float3 normal = normalize(UnityObjectToWorldNormal(tex2Dlod(_Bump, i.uv).rgb));
				//return float4(normal, 1);

				float3 lightDir = normalize(lerp(_WorldSpaceLightPos0.xyz, _WorldSpaceLightPos0.xyz - i.posWorld.xyz, _WorldSpaceLightPos0.w));

				//return float4(i.posWorld.x / 50, i.posWorld.y / 10, i.posWorld.z / 50, 1);
				//return float4(lightDir, 1);
				float3 view = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);

				float4 diffuse = saturate(dot(normal, lightDir));
				diffuse = pow(saturate(diffuse * (1 - _LightWrap) + _LightWrap), 2 * _LightWrap + 1) * _Tint * _LightColor0;

				float3 H = normalize(view + lightDir);
				float NdotH = saturate(dot(normal, H));
				float4 specular = _SpecularColor * saturate(pow(NdotH, _Glossiness)) * _LightColor0;
				float4 rim = _RimColor * pow(max(0, 1 - saturate(dot(normal, view))), 1.5);

				//return specular;
				float4 white = pow(i.color / 2, 1);

				UNITY_LIGHT_ATTENUATION(attenuation, 0, i.posWorld);

				return (diffuse + specular + white + rim) * attenuation * _LightColor0;
				//return diffuse + specular + pow(i.color / 2, _WhiteDegree) + rim;
			}

			ENDCG
		}
	}
}