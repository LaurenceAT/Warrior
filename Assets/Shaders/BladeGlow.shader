// Brillo solo en la hoja de la espada.
//
// La espada va pintada dentro del mismo sprite que el personaje, asi que no se
// puede tenir por separado. Este shader se usa en una copia del sprite puesta
// encima (ver PlayerControler, contraataque): dibuja en _GlowColor solo los
// pixeles cuyo color coincide con uno de los tres tonos de la hoja (_Key1..3) y
// descarta todo lo demas. Las estelas de los tajos usan esos mismos tonos, asi
// que tambien salen rojas.
//
// Los tonos se comparan como Color de material: Unity los convierte al mismo
// espacio de color que la textura, asi funciona igual en Gamma y en Linear.
Shader "Sprites/BladeGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (1,0.15,0.15,1)
        _Amount ("Amount", Range(0,1)) = 1
        _Key1 ("Tono hoja 1", Color) = (0.761,0.784,0.941,1)
        _Key2 ("Tono hoja 2", Color) = (0.647,0.682,0.918,1)
        _Key3 ("Tono hoja 3", Color) = (0.522,0.565,0.624,1)
        _Tolerance ("Tolerancia", Range(0,0.3)) = 0.04
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
            fixed4 _GlowColor;
            float  _Amount;
            fixed4 _Key1;
            fixed4 _Key2;
            fixed4 _Key3;
            float  _Tolerance;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, IN.texcoord);
                float d = min(distance(t.rgb, _Key1.rgb), min(distance(t.rgb, _Key2.rgb), distance(t.rgb, _Key3.rgb)));
                if (t.a < 0.5 || d > _Tolerance) discard;

                fixed a = _GlowColor.a * _Amount;
                return fixed4(_GlowColor.rgb * a, a);
            }
        ENDCG
        }
    }
}
