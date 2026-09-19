Shader "Custom/ClassicalHoldGradient"
{
    Properties
    {
        _MainTex ("Tail Texture", 2D) = "white" {}
        _Color ("Hand Tint", Color) = (1,1,1,1)
        _NearColor ("Near Gradient", Color) = (1,0.78,0.34,1)
        _FarColor ("Far Gradient", Color) = (0.20,0.025,0.04,0)
        _FlowStrength ("Pressed Flow", Range(0,1)) = 0
        _FlowSpeed ("Flow Speed", Float) = 0.65
        _FlowColor ("Flow Color", Color) = (1,0.76,0.28,1)
        _PreserveGold ("Preserve Texture Gold", Range(0,1)) = 1
        _Emission ("Tail Inner Glow", Range(0,4)) = 1.2
        _EdgeGlow ("Tail Edge Glow", Range(0,4)) = 0.9
        _TailFadeStrength ("Longitudinal Fade", Range(0,1)) = 1
        _WorldClipEnabled ("World Clip Enabled", Range(0,1)) = 0
        _WorldClipMinZ ("World Clip Min Z", Float) = -100000
        _WorldClipMaxZ ("World Clip Max Z", Float) = 100000
        _FlatFill ("Flat Gesture Fill", Range(0,1)) = 0
        _SmoothBody ("Smooth Body", Range(0,1)) = 1
        _CoreWidth ("Central Light Width", Range(0.02,0.45)) = 0.25
        _CoreGlow ("Central Light Glow", Range(0,3)) = 0.65
        _CoreJudgedBoost ("Judged Core Boost", Range(0,3)) = 1.45
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
          Blend SrcAlpha OneMinusSrcAlpha
          Cull Off
          ZWrite Off
          ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _NearColor;
            fixed4 _FarColor;
            float _FlowStrength;
            float _FlowSpeed;
            fixed4 _FlowColor;
            float _PreserveGold;
            float _Emission;
            float _EdgeGlow;
            float _TailFadeStrength;
            float _WorldClipEnabled;
            float _WorldClipMinZ;
            float _WorldClipMaxZ;
            float _FlatFill;
            float _SmoothBody;
            float _CoreWidth;
            float _CoreGlow;
            float _CoreJudgedBoost;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                if (_WorldClipEnabled > 0.5)
                {
                    clip(i.worldPos.z - _WorldClipMinZ);
                    clip(_WorldClipMaxZ - i.worldPos.z);
                }
                fixed4 tex = tex2D(_MainTex, i.uv);
                float longitudinal = smoothstep(0.0, 1.0, i.uv.y);
                if (_WorldClipEnabled > 0.5)
                {
                    // Anchor the gradient to the visible runway.  UV-based
                    // fading restarts whenever a streamed mesh is recycled and
                    // creates both seams and frames with no visible gradient.
                    float visibleLength = max(0.001, _WorldClipMaxZ - _WorldClipMinZ);
                    longitudinal = smoothstep(0.0, 1.0,
                        saturate((i.worldPos.z - _WorldClipMinZ) / visibleLength));
                }
                // _FlowStrength is retained for material compatibility, but it
                // now represents a static pressed-glow amount. Nothing here is
                // driven by _Time, so the glass never turns into a water ripple.
                float activation = saturate(_FlowStrength);

                float peak = max(tex.r, max(tex.g, tex.b));
                float floorValue = min(tex.r, min(tex.g, tex.b));
                float luminance = dot(tex.rgb, float3(0.299, 0.587, 0.114));
                float2 px = _MainTex_TexelSize.xy * 1.5;
                float neighbourAlpha = min(
                    min(tex2D(_MainTex, i.uv + float2(px.x, 0)).a,
                        tex2D(_MainTex, i.uv - float2(px.x, 0)).a),
                    min(tex2D(_MainTex, i.uv + float2(0, px.y)).a,
                        tex2D(_MainTex, i.uv - float2(0, px.y)).a));
                float rim = saturate(tex.a - neighbourAlpha);
                float goldRatio = tex.g / max(0.001, tex.r);
                float goldMask = smoothstep(0.28, 0.48, goldRatio) *
                                 (1.0 - smoothstep(0.92, 1.04, goldRatio)) *
                                 smoothstep(0.08, 0.32, tex.r - tex.b) *
                                 smoothstep(0.22, 0.72, peak);

                // The authored hold texture contains a strong water pattern.
                // SmoothBody keeps its alpha silhouette but removes that RGB
                // detail, replacing it with a quiet static glass highlight.
                float textureDetail = lerp(0.58, 1.28, smoothstep(0.04, 0.90, luminance));
                float lateral = saturate(i.uv.x);
                float distanceFromCentre = abs(lateral * 2.0 - 1.0);
                float centreDepth = pow(saturate(1.0 - distanceFromCentre), 1.65);
                float outerBevel = smoothstep(0.72, 0.985, distanceFromCentre);
                float innerBevel = smoothstep(0.48, 0.76, distanceFromCentre) *
                                   (1.0 - smoothstep(0.78, 0.94, distanceFromCentre));
                // Two deliberately asymmetric, broad reflections give the
                // surface optical depth without looking tiled or animated.
                float primaryReflection = pow(
                    saturate(1.0 - abs(lateral - 0.30) * 8.0), 4.0);
                float secondaryReflection = pow(
                    saturate(1.0 - abs(lateral - 0.69) * 12.0), 5.0);
                float glassProfile = 0.70 + centreDepth * 0.10 +
                                     innerBevel * 0.09 +
                                     primaryReflection * 0.10 +
                                     secondaryReflection * 0.045;
                float detail = lerp(textureDetail, glassProfile, _SmoothBody);
                // Preserve the original body through 80% of its visible length,
                // then perform the entire gradient in the final 20%.
                float gradientProgress = smoothstep(0.80, 0.90, longitudinal);
                float3 gradientTint = lerp(_Color.rgb, _FarColor.rgb, gradientProgress);
                float3 themedTint = lerp(_Color.rgb, gradientTint, _TailFadeStrength);
                float3 themed = themedTint * detail;
                float highlight = smoothstep(0.72, 1.0, peak) *
                                  (0.35 + 0.65 * smoothstep(0.18, 0.82, floorValue)) *
                                  (1.0 - _SmoothBody);
                themed = lerp(themed, fixed3(1,1,1), highlight * 0.72);

                half4 output;
                // Preserve only the metallic gold detected in the original
                // texture. Its irregular coloured interior is still discarded.
                float preservedGold = goldMask * _PreserveGold;
                output.rgb = lerp(themed, tex.rgb, preservedGold);
                float innerCore = lerp(smoothstep(0.14, 0.9, luminance), 0.66 + centreDepth * 0.20,
                    _SmoothBody) * tex.a;
                output.rgb += _Color.rgb * (innerCore * _Emission * 0.32 + rim * _EdgeGlow);
                float glassEdgeLight = outerBevel * 0.36 + innerBevel * 0.14;
                float pressedLustre = glassEdgeLight +
                                      primaryReflection * 0.16 +
                                      secondaryReflection * 0.08;
                output.rgb += _FlowColor.rgb * pressedLustre * activation * tex.a *
                              (0.34 + _Emission * 0.16);
                // A narrow light pillar gives the smooth Hold a strong optical
                // centre. It is always faintly present and becomes brighter
                // once the head is judged/held. FlatFill (Trill) replaces this
                // result below, preserving its intentionally dark centre.
                float centreDistance = abs(lateral - 0.5);
                float column = 1.0 - smoothstep(
                    _CoreWidth * 0.55, _CoreWidth, centreDistance);
                float columnCore = 1.0 - smoothstep(
                    _CoreWidth * 0.12, _CoreWidth * 0.42, centreDistance);
                // Idle: a dense, deeper hand-coloured core. Once judgment
                // starts, it opens into a pale luminous version of that same
                // red/blue instead of merely increasing white emission.
                float judgedCore = smoothstep(0.35, 0.72, activation);
                float columnIntensity = _CoreGlow *
                    (0.72 + judgedCore * _CoreJudgedBoost);
                float3 deepColumnTint = _Color.rgb * 0.36;
                float3 brightColumnTint = lerp(_Color.rgb, fixed3(1,1,1), 0.74);
                float3 columnTint = lerp(deepColumnTint, brightColumnTint, judgedCore);
                // An idle core must actually read as deep glass. Additive light
                // alone can only brighten it, so first compress the underlying
                // colour inside the broad 50%-width column.
                float idleCoreDensity = column * (1.0 - judgedCore) * 0.72;
                output.rgb = lerp(output.rgb, _Color.rgb * 0.22, idleCoreDensity);
                output.rgb += columnTint *
                    (column * 0.52 + columnCore * 0.48) *
                    columnIntensity * tex.a;
                float tailFade = lerp(1.0, lerp(1.0, 0.012, gradientProgress), _TailFadeStrength);
                output.a = tex.a * _Color.a * tailFade * i.color.a;
                if (_FlatFill > 0.5)
                {
                    // Muted glass body with a dark centre and broad side light.
                    // This is position-based, never mesh-edge-based.
                    float tipGlow = saturate(i.color.r);
                    float lateralPosition = saturate(i.color.b);
                    float flatDistanceFromCentre = abs(lateralPosition * 2.0 - 1.0);
                    float sideReach = lerp(0.14, 1.0,
                        smoothstep(0.02, 0.80, flatDistanceFromCentre));
                    float sideGlow = sideReach * tipGlow;
                    float3 mutedHandTint = lerp(_Color.rgb, fixed3(0.72,0.74,0.78), 0.12);
                    output.rgb = mutedHandTint * 0.038;
                    output.rgb += mutedHandTint * sideGlow * (0.82 + _EdgeGlow * 0.56);
                    output.rgb += mutedHandTint * activation * sideGlow * 0.18;
                    // An opaque dark glass centre hides track/triangle lines;
                    // only the broad side regions gain light and transparency.
                    output.a = _Color.a * lerp(0.52, 0.95, sideGlow);
                }
                return output;
            }
            ENDCG
        }
    }
    FallBack Off
}
