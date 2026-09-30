// Oscuridad de las zonas oscuras de la cueva: negro por encima del escenario,
// con agujeros de luz suaves (el player, las antorchas encendidas, las
// hogueras). Las luces llegan en _Luces (x, y en el mundo, radio, intensidad).
// Va en un quad que cubre la camara; los personajes y los efectos se dibujan
// por encima (siempre legibles).
Shader "Warrior/OscuridadCueva"
{
    Properties
    {
        _Color ("Color de la oscuridad", Color) = (0.01, 0.01, 0.03, 1)
        _Oscuridad ("Intensidad", Range(0, 1)) = 0.9
        _Borde ("Suavidad del borde (0-1)", Range(0.05, 1)) = 0.45
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float2 mundo : TEXCOORD0; };

            fixed4 _Color;
            float _Oscuridad;
            float _Borde;
            float4 _Luces[8];
            float _Cantidad;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.mundo = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float luz = 0;
                for (int k = 0; k < 8; k++)
                {
                    if (k >= (int)_Cantidad) break;
                    float4 l = _Luces[k];
                    float d = distance(i.mundo, l.xy) / max(0.01, l.z);
                    // 1 dentro, se apaga suave hacia el borde del radio.
                    float f = 1 - smoothstep(1 - _Borde, 1, d);
                    luz = max(luz, f * l.w);
                }
                float a = _Oscuridad * saturate(1 - luz);
                return fixed4(_Color.rgb, a);
            }
        ENDCG
        }
    }
}
