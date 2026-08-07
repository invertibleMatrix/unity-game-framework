Shader "AK/UI/UISpotlight"
{
	Properties
	{
		[PerRendererData] _MainTex ("Texture", 2D) = "white" {}
		_Color ("Tint", Color) = (1,1,1,1)
		_Feather ("Feather", Float) = 15
		_HoleCount ("Hole Count", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"Queue"="Transparent"
			"IgnoreProjector"="True"
			"RenderType"="Transparent"
			"PreviewType"="Plane"
			"CanUseSpriteAtlas"="True"
		}

		Cull Off
		Lighting Off
		ZWrite Off
		ZTest [unity_GUIZTestMode]
		Blend SrcAlpha OneMinusSrcAlpha

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0

			#include "UnityCG.cginc"
			#include "UnityUI.cginc"

			#define MAX_HOLES 8

			struct appdata_t
			{
				float4 vertex   : POSITION;
				float4 color    : COLOR;
				float2 texcoord : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex    : SV_POSITION;
				fixed4 color     : COLOR;
				float2 texcoord  : TEXCOORD0;
				float4 screenPos : TEXCOORD1;
			};

			sampler2D _MainTex;
			fixed4    _Color;
			float     _Feather;
			float     _HoleCount;
			float4    _Holes[MAX_HOLES];

			v2f vert(appdata_t input)
			{
				v2f output;
				output.vertex = UnityObjectToClipPos(input.vertex);
				output.screenPos = ComputeScreenPos(output.vertex);
				output.texcoord = input.texcoord;
				output.color = input.color * _Color;
				return output;
			}

			fixed4 frag(v2f input) : SV_Target
			{
				fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;

				float2 pixelPos = (input.screenPos.xy / input.screenPos.w) * _ScreenParams.xy;

				float hole = 0.0;
				int holeCount = (int)_HoleCount;
				for (int i = 0; i < holeCount; i++)
				{
					float dist = distance(pixelPos, _Holes[i].xy);
					hole = max(hole, 1.0 - smoothstep(_Holes[i].w, _Holes[i].w + _Feather, dist));
				}

				color.a *= 1.0 - hole;
				return color;
			}
			ENDCG
		}
	}
}
