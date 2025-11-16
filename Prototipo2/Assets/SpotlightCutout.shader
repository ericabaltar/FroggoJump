Shader "UI/SpotlightCutout"
{
    Properties
    {
        _Color   ("Tint", Color) = (0,0,0,1)   // color del fondo (negro con alpha)
        _Center  ("Center (viewport 0..1)", Vector) = (0.5, 0.5, 0, 0)
        _Radius  ("Radius", Float) = 0.35    // radio del agujero
        _Feather ("Feather", Float) = 0.1      // borde suave del agujero
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

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0; // usaremos uv como coords en 0..1 del rect
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
            // coord 0..1 del rect
            float2 p = i.uv;
            float2 c = _Center.xy;

            // aspect = width / height
            float aspect = _ScreenParams.x / _ScreenParams.y;

            // CORRECCIÓN DE ASPECTO:
            // escala la distancia en X por el aspect para que el círculo sea isotrópico
            float2 d2 = float2( (p.x - c.x) * aspect, (p.y - c.y) );
            float d = length(d2);

            // borde suave
            float a = smoothstep(_Radius - _Feather, _Radius, d);

            fixed4 col = _Color;
            col.a *= saturate(a);
            return col;
        }

            ENDCG
        }
    }
}
