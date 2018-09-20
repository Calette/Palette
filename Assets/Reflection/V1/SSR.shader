Shader "Hidden/SSR"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
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

			sampler2D _MainTex;
			sampler2D _CameraDepthTexture;
			//sampler2D _CameraDepthNormalsTexture;
			// 可以反射rgb = 0.5
			sampler2D _CameraGBufferTexture1;
			// 法向量
			sampler2D _CameraGBufferTexture2;
			// color buffer
			sampler2D _CameraGBufferTexture3;
			//float4x4 _InverseViewProjectionMatrix;
			float4x4 _ProjectionMatrix;
			float4x4 _InverseProjectionMatrix;
			//float4x4 _InverseViewMatrix;
			float4x4 _ViewMatrix;

			float _MaxRayDistance;
			float _SampleCount;
			float _Thickness;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				float depth = tex2D(_CameraDepthTexture, i.uv).r;
				//return depth * float4(1, 1, 1, 1);

				// 转换到NDC(但z是从1到0), y轴是反的
				//float4 pos = mul(_InverseViewProjectionMatrix, float4(i.uv.x * 2 - 1, 1 - i.uv.y * 2, depth, 1));
				
				//float3 worldPos = pos.rgb / pos.w;

				// view击中点
				float4 pos = mul(_InverseProjectionMatrix, float4(i.uv.x * 2 - 1, 1 - i.uv.y * 2, depth, 1));
				float3 rayOrigin = pos.xyz / pos.w;

				/*
				// 从_CameraDepthNormalsTexture中获取normal
				//float3 decodedNormal;
				//float decodedDepth;
				//DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, i.uv), decodedDepth, decodedNormal);

				float3 normal = DecodeViewNormalStereo(float4(tex2D(_CameraDepthNormalsTexture, i.uv)));
				// world normal
				//normal = mul(_InverseViewMatrix, float4(normal, 0));
				normal = mul((float3x3)_InverseViewMatrix, normal);
				*/

				// 从GBuffer中获取normal
				float3 worldNormal = tex2D(_CameraGBufferTexture2, i.uv).rgb * 2.0 - 1.0;

				// 是向量（w = 0）可以用float3x3来进行矩阵计算
				float3 viewNormal = mul((float3x3)_ViewMatrix, worldNormal);

				float3 rayDirection = normalize(reflect(rayOrigin, viewNormal));

				// 如果_MaxRayDistance位置小于near平面，则rayLength = near - rayOrigin.z
				// 也就是rayLength是near平面上的位置
				// 如果_MaxRayDistance位置大于near平面，则rayLength = _MaxRayDistance
				float rayLength = ((rayOrigin.z + rayDirection.z * _MaxRayDistance) > -_ProjectionParams.y) ?
					(-_ProjectionParams.y - rayOrigin.z) / rayDirection.z : _MaxRayDistance;

				float3 perNodeLength = rayLength / _SampleCount;

				float3 currentPoint = rayOrigin;

				float2 uv;

				float Screendepth, currentDepth;

				float3 col;

				float get;

				for (int index = 0; index < _SampleCount; ++index)
				{
					currentPoint += rayDirection * perNodeLength;

					pos = mul(_ProjectionMatrix, currentPoint);

					uv = pos.xy / pos.w;

					uv = float2(uv.x / 2 + 0.5, 0.5 - uv.y / 2);

					Screendepth = tex2D(_CameraDepthTexture, uv).r;

					pos = mul(_InverseProjectionMatrix, float4(uv.x * 2 - 1, 1 - uv.y * 2, Screendepth, 1));

					currentDepth = pos.z / pos.w;

					if (currentPoint.z < currentDepth)
						if (currentPoint.z > currentDepth - _Thickness) 
							if (get == 0) {
								get = 1;
								col = tex2D(_MainTex, uv).rgb;
							}
				}

				//return float4(uv, 0, 1);
				//return -rayOrigin.z * float4(1, 1, 1, 1);
				//return tex2D(_CameraDepthTexture, i.uv).r * float4(1, 1, 1, 1);

				float3 color = tex2D(_CameraGBufferTexture3, i.uv);

				if (tex2D(_CameraGBufferTexture1, i.uv).r > 0)
					if(col.r > 0)
						color = col;

				return float4(color, 1);
			}
			ENDCG
		}
	}
}
