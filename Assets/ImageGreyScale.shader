// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/UI/Grayscale"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _EffectAmount ("Effect Amount", Range (0, 1)) = 1.0
        _BrightnessAmount ("Brightness Amount", Range(0.0, 3)) = 1.0

        // Fill properties
        _FillAmount ("Fill Amount", Range(0,1)) = 0.5
        [KeywordEnum(None, Horizontal, Vertical, Radial90, Radial180, Radial360)] _FillType ("Fill Type", Float) = 0
        [KeywordEnum(Left, Right, Top, Bottom)] _FillOrigin ("Fill Origin", Float) = 0
        _FillSmoothness ("Fill Smoothness", Range(0,0.2)) = 0.02

        _ColorMask ("Color Mask", Float) = 15
        [PerRendererData]_SoftMask("_SoftMask", 2D) = "white" {}

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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _FILLTYPE_NONE _FILLTYPE_HORIZONTAL _FILLTYPE_VERTICAL _FILLTYPE_RADIAL90 _FILLTYPE_RADIAL180 _FILLTYPE_RADIAL360
            #pragma multi_compile _FILLORIGIN_LEFT _FILLORIGIN_RIGHT _FILLORIGIN_TOP _FILLORIGIN_BOTTOM

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            uniform sampler2D _SoftMask;

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                half2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 softMaskUV : TEXCOORDX;
                float fillAmount : TEXCOORD2;
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;

            bool _UseClipRect;
            float4 _ClipRect;

            bool _UseAlphaClip;
            uniform float _EffectAmount;
            uniform float _BrightnessAmount;
            
            // Fill uniforms
            uniform float _FillAmount;
            uniform float _FillType;
            uniform float _FillOrigin;
            uniform float _FillSmoothness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                float4 wPos = mul(unity_ObjectToWorld, IN.vertex);

                OUT.texcoord = IN.texcoord;
                OUT.fillAmount = _FillAmount;

                #ifdef UNITY_HALF_TEXEL_OFFSET
                OUT.vertex.xy += (_ScreenParams.zw - 1.0) * float2(-1, 1);
                #endif

                OUT.color = IN.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                // Apply fill logic
                float2 uv = IN.texcoord;
                float fillAlpha = 1.0;
                
                #if defined(_FILLTYPE_NONE)
                    // No fill - show full texture
                    fillAlpha = 1.0;
                #elif defined(_FILLTYPE_HORIZONTAL)
                    // Horizontal fill
                    #if defined(_FILLORIGIN_LEFT)
                        fillAlpha = smoothstep(IN.fillAmount + _FillSmoothness, IN.fillAmount, uv.x);
                    #elif defined(_FILLORIGIN_RIGHT)
                        fillAlpha = smoothstep(IN.fillAmount + _FillSmoothness, IN.fillAmount, 1.0 - uv.x);
                    #else // Default: Left
                        fillAlpha = smoothstep(IN.fillAmount + _FillSmoothness, IN.fillAmount, uv.x);
                    #endif
                #elif defined(_FILLTYPE_VERTICAL)
                    // Vertical fill
                    #if defined(_FILLORIGIN_BOTTOM)
                        fillAlpha = smoothstep(IN.fillAmount + _FillSmoothness, IN.fillAmount, uv.y);
                    #elif defined(_FILLORIGIN_TOP)
                        fillAlpha = smoothstep(IN.fillAmount + _FillSmoothness, IN.fillAmount, 1.0 - uv.y);
                    #else // Default: Bottom
                        fillAlpha = smoothstep(IN.fillAmount + _FillSmoothness, IN.fillAmount, uv.y);
                    #endif
                #elif defined(_FILLTYPE_RADIAL90) || defined(_FILLTYPE_RADIAL180) || defined(_FILLTYPE_RADIAL360)
                    // Radial fill
                    float2 center = float2(0.5, 0.5);
                    float2 dir = uv - center;
                    float angle = atan2(dir.y, -dir.x) + 3.14159265;
                    float normalizedAngle = angle / (6.28318530); // 2π
                    
                    // Normalize angle to 0-1 range
                    if (normalizedAngle < 0) normalizedAngle += 1.0;
                    
                    // Apply fill amount based on fill type
                    float targetAngle = IN.fillAmount;
                    
                    #if defined(_FILLTYPE_RADIAL90)
                        targetAngle = IN.fillAmount * 0.25;
                    #elif defined(_FILLTYPE_RADIAL180)
                        targetAngle = IN.fillAmount * 0.5;
                    #endif
                    
                    // Apply origin offset for radial fill
                    #if defined(_FILLORIGIN_TOP)
                        normalizedAngle = normalizedAngle + 0.25;
                    #elif defined(_FILLORIGIN_RIGHT)
                        normalizedAngle = normalizedAngle + 0.5;
                    #elif defined(_FILLORIGIN_BOTTOM)
                        normalizedAngle = normalizedAngle + 0.75;
                    #endif
                    // Normalize after offset
                    normalizedAngle = normalizedAngle - floor(normalizedAngle);
                    
                    // Radial fill with smooth edge on both start and end
                    if (targetAngle > 0.001) {
                        // Handle wrap-around case
                        if (normalizedAngle > targetAngle) {
                            // Check if we're close to wrapping around (near 0)
                            if (normalizedAngle > (1.0 - _FillSmoothness)) {
                                float edgeDistance = (normalizedAngle - targetAngle);
                                fillAlpha = smoothstep(_FillSmoothness, 0.0, edgeDistance);
                            } else {
                                fillAlpha = smoothstep(targetAngle + _FillSmoothness, targetAngle, normalizedAngle);
                            }
                        } else {
                            // Normal case: smooth both edges
                            float alpha1 = smoothstep(0.0 - _FillSmoothness, 0.0, normalizedAngle);
                            float alpha2 = smoothstep(targetAngle + _FillSmoothness, targetAngle, normalizedAngle);
                            fillAlpha = alpha1 * alpha2;
                        }
                    } else {
                        fillAlpha = 0.0;
                    }
                #endif

                if (_UseClipRect)
                    color *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);

                if (_UseAlphaClip)
                    clip(color.a - 0.001);

                float3 brtColor = color.rgb * _BrightnessAmount;
                color.rgb = lerp(brtColor, dot(brtColor, float3(0.3, 0.59, 0.11)), _EffectAmount);
                color.a *= fillAlpha; // Apply fill alpha
                return color;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}