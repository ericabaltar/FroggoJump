Shader "UI/SpotlightCutoutBG"
{
    Properties
    {
        _Color   ("Overlay Color (fallback)", Color) = (0,0,0,1)
        _Center  ("Center (viewport 0..1)", Vector)  = (0.5, 0.5, 0, 0)
        _Radius  ("Radius", Float) = 0.25
        _Feather ("Feather", Float) = 0.12

        _BgTex   ("Background Texture", 2D) = "white" {}
        _BgTint  ("Background Tint", Color) = (1,1,1,1)
        _BgMix   ("Background Mix (0=Color,1=Texture)", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float4 _Center;   // xy en viewport (0..1)
            float  _Radius;
            float  _Feather;

            sampler2D _BgTex;
            fixed4 _BgTint;
            float  _BgMix;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0; // UVs de la Image (0..1 pantalla completa)
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Posición 0..1 del rect y centro
                float2 p = i.uv;
                float2 c = _Center.xy;

                // Corrige óvalo -> círculo con aspect ratio
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 d2 = float2( (p.x - c.x) * aspect, (p.y - c.y) );
                float d = length(d2);

                // Máscara: 0 dentro del círculo (agujero transparente), 1 fuera
                float a = smoothstep(_Radius - _Feather, _Radius, d);

                // Color base fuera del círculo: mezcla entre Color sólido y textura
                fixed4 bg = tex2D(_BgTex, i.uv) * _BgTint;
                fixed4 baseCol = lerp(_Color, bg, _BgMix);

                // Alpha sólo fuera del círculo
                baseCol.a *= saturate(a);

                return baseCol;
            }
            ENDCG
        }
    }
}
