// Aura de color alrededor de un sprite: el contorno engordado unos pixeles y
// pintado de un color liso. Se pone en una copia del sprite, detras del
// original (el player con la espada imbuida).
Shader "Sprites/Aura"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _AuraColor ("Color", Color) = (1,0.5,0.2,1)
        _Width ("Grosor (texels)", Range(0,4)) = 1.5
        _Amount ("Intensidad", Range(0,1)) = 0.6
        // Cuanto se ve el color tambien por dentro de la silueta.
        _Inner ("Relleno", Range(0,1)) = 0.15
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
        Blend SrcAlpha One

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _AuraColor;
            float _Width;
            float _Amount;
            float _Inner;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _Width;
                float propio = tex2D(_MainTex, IN.texcoord).a;
                float vecinos = 0;
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord + float2( d.x, 0)).a);
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord + float2(-d.x, 0)).a);
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord + float2(0,  d.y)).a);
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord + float2(0, -d.y)).a);
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord + d * 0.7).a);
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord - d * 0.7).a);
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord + float2(d.x, -d.y) * 0.7).a);
                vecinos = max(vecinos, tex2D(_MainTex, IN.texcoord + float2(-d.x, d.y) * 0.7).a);
                // Borde: donde hay vecinos pero no pixel propio. Dentro, algo de relleno.
                float borde = saturate(vecinos - propio);
                float a = (borde + propio * _Inner) * _Amount;
                return fixed4(_AuraColor.rgb, a * _AuraColor.a);
            }
        ENDCG
        }
    }
}
