// Sprite normal, pero con un destello de color encima.
//
// _FlashAmount va de 0 (sprite tal cual) a 1 (silueta entera del color de
// _FlashColor). Se anima desde HitFlash con un MaterialPropertyBlock, que no
// crea una copia del material por cada enemigo.
//
// Es unlit a proposito: el proyecto no usa luces 2D, asi que no hace falta
// pasar por el pipeline iluminado y asi el destello sale plano y limpio.
Shader "Sprites/Flash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FlashColor ("Flash Color", Color) = (1,1,1,1)
        _FlashAmount ("Flash Amount", Range(0,1)) = 0
        // Variantes de color de los enemigos (ficha): giro de tono en grados,
        // saturacion y brillo. Por defecto no cambian nada.
        _Tono ("Tono", Range(-180,180)) = 0
        _Saturacion ("Saturacion", Range(0,2)) = 1
        _Brillo ("Brillo", Range(0,2)) = 1
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _FlashColor;
            float  _FlashAmount;
            float  _Tono;
            float  _Saturacion;
            float  _Brillo;

            // Gira el tono alrededor del eje gris (conserva la luminosidad).
            float3 GirarTono(float3 c, float grados)
            {
                float a = radians(grados);
                const float3 k = float3(0.57735, 0.57735, 0.57735);
                float ca = cos(a);
                return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Variante de color (sin efecto con los valores por defecto).
                c.rgb = GirarTono(c.rgb, _Tono);
                float gris = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = saturate(lerp(gris.xxx, c.rgb, _Saturacion) * _Brillo);

                // El destello sustituye el color pero respeta la transparencia,
                // asi solo se pinta la silueta del sprite y no un cuadrado.
                c.rgb = lerp(c.rgb, _FlashColor.rgb * c.a, _FlashAmount);

                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}
