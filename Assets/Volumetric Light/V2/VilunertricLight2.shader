﻿Shader "Hidden/VilunertricLight2"
{
	Properties
	{
		_MainTex("Base (RGB)", 2D) = "white" {}
	}
	SubShader
	{
		Tags{ "RenderType" = "Opaque" }

		// No culling or depth
		//Cull Off ZWrite Off ZTest Always

		Pass
		{
			CGPROGRAM
            #pragma multi_compile_fwdbase	
            #pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			#include "UnityShadowLibrary.cginc"

			#define MieScattering(cosAngle, g) g.w * (g.x / (pow(g.y - g.z * cosAngle, 1.5)))
			#define Bias 0.01

			sampler3D _NoiseTexture;
			float4 _NoiseData;
			float4 _NoiseVelocity;
			sampler2D _DitherTexture;

			sampler2D _MainTex;
			sampler2D _CameraDepthTexture;
			float4x4 _InverseViewProjectionMatrix;
			float _MaxLength;
			float _SampleCount;
			float _VolumetricIntensity;
			float3 _NearLeftBottom;
			float3 _NearU;
			float3 _NearV;
			float4 _MieG;

			float3 _EyeWorldDir;

			UNITY_DECLARE_SHADOWMAP(_ShadowMapTexture);
			float4 _ShadowMapTexture_TexelSize;
			#define SHADOWMAPSAMPLER_AND_TEXELSIZE_DEFINED

			float4 GetWorldPositionFromDepthValue(float2 uv, float linearDepth)
			{
				/*
				_ProjectionParams 投影参数
				x = 1,如果投影翻转则x = -1
				y是camera近裁剪平面
				z是camera远裁剪平面
				w是1/远裁剪平面
				*/
				float camPosZ = _ProjectionParams.y + (_ProjectionParams.z - _ProjectionParams.y) * linearDepth;

				// unity_CameraProjection._m11 = near / t，其中t是视锥体near平面的高度的一半。
				// 投影矩阵的推导见：http://www.songho.ca/opengl/gl_projectionmatrix.html。
				// 这里求的height和width是坐标点所在的视锥体截面（与摄像机方向垂直）的高和宽，并且
				// 假设相机投影区域的宽高比和屏幕一致。
				float height = 2 * camPosZ / unity_CameraProjection._m11;
				float width = _ScreenParams.x / _ScreenParams.y * height;

				float camPosX = width * uv.x - width / 2;
				float camPosY = height * uv.y - height / 2;
				float4 camPos = float4(camPosX, camPosY, camPosZ, 1.0);
				return mul(unity_CameraToWorld, camPos);
			}

			inline float4 GetWeights(float3 wpos)
			{
				// dir
				float3 fromCenter0 = wpos.xyz - unity_ShadowSplitSpheres[0].xyz;
				float3 fromCenter1 = wpos.xyz - unity_ShadowSplitSpheres[1].xyz;
				float3 fromCenter2 = wpos.xyz - unity_ShadowSplitSpheres[2].xyz;
				float3 fromCenter3 = wpos.xyz - unity_ShadowSplitSpheres[3].xyz;
				// length
				float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));

				// 和unity_ShadowSplit的SqRadii比较
				// 小于表示在范围内,weight = 1
				float4 weights = float4(distances2 < unity_ShadowSplitSqRadii);
				// 如果在前面的unity_ShadowSplit也包含了该点,则weight = 0
				weights.yzw = saturate(weights.yzw - weights.xyz);
				return weights;
			}

			inline float4 getShadowCoord(float4 wpos, fixed4 cascadeWeights)
			{
				float3 sc0 = mul(unity_WorldToShadow[0], wpos).xyz;
				float3 sc1 = mul(unity_WorldToShadow[1], wpos).xyz;
				float3 sc2 = mul(unity_WorldToShadow[2], wpos).xyz;
				float3 sc3 = mul(unity_WorldToShadow[3], wpos).xyz;
				float4 shadowMapCoordinate = float4(sc0 * cascadeWeights[0] + sc1 * cascadeWeights[1] + sc2 * cascadeWeights[2] + sc3 * cascadeWeights[3], 1);

				// clip的z是(1, 0)
				#if defined(UNITY_REVERSED_Z)
				    // 假如超出了有阴影的范围noCascadeWeights = 1
					float  noCascadeWeights = 1 - dot(cascadeWeights, float4(1, 1, 1, 1));
					// 返回(0, 0, 1, 0)
					shadowMapCoordinate.z += noCascadeWeights;
				#endif

				shadowMapCoordinate.z -= Bias;
				return shadowMapCoordinate;
			}

			inline fixed GetAttenuation(float4 worldPos)
			{
				fixed4 attenuation;

				float4 shadowCoord = getShadowCoord(worldPos, GetWeights(worldPos.xyz));

				attenuation = UNITY_SAMPLE_SHADOW(_ShadowMapTexture, shadowCoord);

				attenuation = lerp(_LightShadowData.r, 1.0, attenuation);

				return attenuation;
			}

			float GetDensity(float3 currentPoint)
			{
				float density = 1;

				// _NoiseData.x(Noise Scale)太大会导致每次GPU读取tex3D时击中cache几率太低？
				float noise = tex3D(_NoiseTexture, frac(currentPoint * _NoiseData.x + float3(_Time.y * _NoiseVelocity.x, 0, _Time.y * _NoiseVelocity.y)));

				noise = saturate(noise - _NoiseData.z) * _NoiseData.y;
				density = saturate(noise);

				float a = saturate((currentPoint.y - 100) * -0.01);
				return density * a;
				return density;
			}

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float depth = tex2D(_CameraDepthTexture, i.uv).r;
				//return depth;

				//float linearDepth = Linear01Depth(depth);
				//return 1 - linearDepth;

				float4 pos = mul(_InverseViewProjectionMatrix, float4(i.uv.x * 2 - 1, 1 - i.uv.y * 2, depth, 1));

				//float4 worldPos = GetWorldPositionFromDepthValue(i.uv, linearDepth);
				float3 worldPos = pos.rgb / pos.w;
				//return float4(worldPos, 1.0);

				// 已经取得的世界空间近裁面坐标
				// 简化成摄像机位置
				//float3 startPos = _NearLeftBottom + i.uv.x * _NearU + i.uv.y * _NearV;   
				float3 startPos = _WorldSpaceCameraPos.xyz;
				//return float4(startPos, 1);

				float3 direction = normalize(worldPos - startPos);
				//return float4(direction, 1);

				float m_length = min(_MaxLength, length(worldPos - startPos));
				//return m_length / _MaxLength;

				//return shadowCoord;

				float perNodeLength = m_length / _SampleCount;

				float intensity = 0;

#ifdef DITHER_4_4
				float2 interleavedPos = fmod(floor(i.pos), 4.0);
				float offset = tex2D(_DitherTexture, interleavedPos / 4.0).w;
#else
				float2 interleavedPos = fmod(floor(i.pos), 8.0);
				float offset = tex2D(_DitherTexture, interleavedPos / 8.0).w;
#endif

				float3 currentPoint = startPos + perNodeLength * offset;

				for (int index = 0; index < _SampleCount; ++index)
				{   
					currentPoint += direction * perNodeLength;

					float extinction = index * perNodeLength * 0.005;

					float density = GetDensity(currentPoint);

					// 高度
					density *= exp(clamp(currentPoint.y * -0.1, -100, 0));

					// 获得当前坐标的阴影遮挡信息
					intensity += GetAttenuation(float4(currentPoint, 1)) * exp(-extinction) * density;
				}
				float cosAngle = saturate(dot(_WorldSpaceLightPos0.xyz, direction));
				//return cosAngle;

				//return MieScattering(cosAngle, _MieG);

				//return intensity * _VolumetricIntensity;

				intensity *= MieScattering(cosAngle, _MieG) * _VolumetricIntensity * m_length / _SampleCount;

				//return intensity;
				//return worldPos;
				//return density;

				//return tex2D(_DitherTexture, fmod(floor(i.pos), 8.0) / 8.0).w * float4(1, 1, 1, 1);

				//return float4(fmod(floor(i.uv * float2(1920, 1080)), 8.0) / 8.0, 0, 1);

				//return float4(fmod(floor(.x), 8.0) / 8.0, 0, 0, 1);

				//return offset * float4(1, 1, 1, 1);

				//intensity /= (1 + intensity);

				return intensity * _LightColor0;
			}
			ENDCG
		}
	}
	FallBack "Specular"
}
