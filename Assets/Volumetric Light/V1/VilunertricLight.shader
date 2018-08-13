Shader "Hidden/VilunertricLight"
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

				return shadowMapCoordinate;
			}

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

			sampler3D _NoiseTexture;
			float4 _NoiseData;
			float4 _NoiseVelocity;

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


			//#define UNITY_USE_CASCADE_BLENDING 0
			//#define UNITY_CASCADE_BLEND_DISTANCE 0.1

			/*
			struct Point 
			{
				float4 pos : SV_POSITION;
				float3 worldPos : TEXCOORD1;
				SHADOW_COORDS(2)
			};
			*/

			/*
			inline float3 computeCameraSpacePosFromDepthAndInvProjMat(float4 uv)
			{
				float zdepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv.xy);

#if defined(UNITY_REVERSED_Z)
				zdepth = 1 - zdepth;
#endif

				// View position calculation for oblique clipped projection case.
				// this will not be as precise nor as fast as the other method
				// (which computes it from interpolated ray & depth) but will work
				// with funky projections.
				float4 clipPos = float4(uv.zw, zdepth, 1.0);
				clipPos.xyz = 2.0f * clipPos.xyz - 1.0f;
				float4 camPos = mul(unity_CameraInvProjection, clipPos);
				camPos.xyz /= camPos.w;
				camPos.z *= -1;
				return camPos.xyz;
			}
			*/

			/*
			// 没有多层级阴影(大概)
			inline fixed4 getCascadeWeights(float3 wpos, float z)
			{
				// View坐标系的z和_LightSplitsNear/Far比较,只有在_LightSplits中 weights = 1
				fixed4 zNear = float4(z >= _LightSplitsNear);
				fixed4 zFar = float4(z < _LightSplitsFar);
				fixed4 weights = zNear * zFar;
				return weights;
			}
			*/

			inline fixed GetAttenuation(float4 worldPos)
			{
				fixed4 attenuation;

				//float z = mul(unity_WorldToCamera, worldPos).z;


				//shadowCoord = mul(unity_WorldToShadow[0], worldPos);
				//float4 shadowCoord = getShadowCoord(worldPos, getCascadeWeights(worldPos.xyz, z));
				float4 shadowCoord = getShadowCoord(worldPos, GetWeights(worldPos.xyz));

				attenuation = UNITY_SAMPLE_SHADOW(_ShadowMapTexture, shadowCoord);

				attenuation = lerp(_LightShadowData.r, 1.0, attenuation);

				return attenuation;
			}

			float GetDensity(float3 currentPoint)
			{
				float density = 1;

				float noise = tex3D(_NoiseTexture, frac(currentPoint * _NoiseData.x + float3(_Time.y * _NoiseVelocity.x, 0, _Time.y * _NoiseVelocity.y)));

				noise = saturate(noise - _NoiseData.z) * _NoiseData.y;
				density = saturate(noise);
				return density;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				//return float4(i.uv, 0, 1);
				//float4 color = tex2D(_MainTex, i.uv);
				float depth = tex2D(_CameraDepthTexture, i.uv).r;


				//return depth;
				float linearDepth = Linear01Depth(depth);
				//return 1 - linearDepth;

				float4 worldPos = GetWorldPositionFromDepthValue(i.uv, linearDepth);
				//return worldPos;

				/*
				#if UNITY_UV_STARTS_AT_TOP
					i.uv.y = 1 - i.uv.y;
				#endif

				// NDC (-1, 1)
				// 通过NDC坐标反推世界坐标
			    float4 temp = mul(_InverseViewProjectionMatrix, float4(float2(i.uv.x * 2 - 1, i.uv.y * 2 - 1), 1 - linearDepth, 1));
				float3 worldPos = temp.xyz;

				float3 viewdir = normalize(worldPos - _WorldSpaceCameraPos.xyz);

				float distance = linearDepth / dot(_EyeWorldDir, viewdir);

				worldPos = _EyeWorldDir + viewdir * distance;
				*/


				
				// 已经取得的世界空间近裁面坐标
				// 简化成摄像机位置
				//float3 startPos = _NearLeftBottom + i.uv.x * _NearU + i.uv.y * _NearV;   
				float3 startPos = _WorldSpaceCameraPos.xyz;
				//return float4(startPos, 1);

				float3 direction = normalize(worldPos - startPos);    
				//return float4(direction, 1);

				float m_length = min(_MaxLength, length(worldPos - startPos));
				//return m_length / 10;



				//return shadowCoord;



				/*
				Point p;

				p.pos = mul(_InverseViewProjectionMatrix, worldPos);


				float2 screenUV = ComputeNonStereoScreenPos(p.pos);


				p.worldPos = worldPos;
				//return p.pos;

				TRANSFER_SHADOW(p);

				//return float4(p._ShadowCoord, 1);

				UNITY_LIGHT_ATTENUATION(atten, p, worldPos);
				//float atten = SHADOW_ATTENUATION(p);

				return atten;
				*/



				float perNodeLength = m_length / _SampleCount;          
				float3 currentPoint = startPos;    

				float intensity = 0;

				for (int index = 0; index < _SampleCount; ++index) 
				{    
					currentPoint += direction * perNodeLength;

					float extinction = index * perNodeLength * 0.005;

					float density = GetDensity(currentPoint);

					// 获得当前坐标的阴影遮挡信息
					intensity += GetAttenuation(float4(currentPoint, 1)) * exp(-extinction) * density;
				}
				float cosAngle = saturate(dot(_WorldSpaceLightPos0.xyz, direction));

				//return cosAngle;

				//return MieScattering(cosAngle, _MieG);

				intensity *= MieScattering(cosAngle, _MieG) * _VolumetricIntensity * m_length / _SampleCount;

				//return intensity;
				//return worldPos;
				//return density;

				float4 color = tex2D(_MainTex, i.uv);
				color.rgb += intensity * _LightColor0;

				return color;
			}
			ENDCG
		}
	}
	FallBack "Specular"
}
