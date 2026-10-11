// Текучий фон меню в духе Balatro: спираль + многократное искажение пространства («жидкая краска»).
// С параметрами по умолчанию узор бесшовно повторяется каждые 120 с.
// Цвета — из палитры игры. Рисуется на UI Image во весь экран (GameUI.BuildMenu).
// Прототип с той же математикой: Tools/ArtGen/menu_swirl_preview.py
Shader "RecipeSlicer/MenuSwirl"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Deep ("Основа (глубокий шалфей)", Color) = (0.310, 0.420, 0.290, 1)      // #4F6B4A
        _Warm ("Второй цвет (терракота)", Color) = (0.784, 0.392, 0.231, 1)       // #C8643B
        _Vein ("Прожилки (горчичный)", Color) = (0.851, 0.643, 0.255, 1)          // #D9A441
        _Shadow ("Тени (тёплый графит)", Color) = (0.231, 0.204, 0.188, 1)        // #3B3430
        _Gloss ("Блики (крем)", Color) = (0.965, 0.922, 0.827, 1)                 // #F6EBD3
        _Scale ("Масштаб узора", Float) = 4.5
        _Swirl ("Закрутка к центру", Float) = 3.0
        _Spin ("Скорость вращения", Float) = 0.0523599   // 2π/120: оборот ровно за 2 минуты
        _Flow ("Скорость течения", Float) = 0.3665191    // 2π·7/120: течение повторяется каждые 2 минуты
        _Warp ("Сила искажения", Float) = 0.7
        _Brightness ("Яркость", Range(0.3, 1.5)) = 1.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Deep, _Warm, _Vein, _Shadow, _Gloss;
            float _Scale, _Swirl, _Spin, _Flow, _Warp, _Brightness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float gauss (float x, float center, float width)
            {
                float d = (x - center) / width;
                return exp(-d * d);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = (i.uv - 0.5) * float2(aspect, 1.0) * _Scale;

                // спираль: закрутка сильнее к центру + медленное вращение
                float r = length(p);
                float a = atan2(p.y, p.x) + _Swirl / (r + 0.7) + t * _Spin;
                float2 q = float2(cos(a), sin(a)) * r;

                // многократное искажение пространства — «текучая краска»
                float ft = t * _Flow;
                [unroll]
                for (int k = 0; k < 6; k++)
                {
                    float2 n;
                    n.x = q.x + _Warp * sin(q.y * 2.0 + ft * (8.0 / 7.0) + k * 1.3);
                    n.y = q.y + _Warp * cos(q.x * 1.8 - ft * (6.0 / 7.0) + k * 0.7);
                    q = n;
                }

                float f1 = 0.5 + 0.5 * sin(q.x * 1.6 + q.y * 1.2);          // какой цвет: основа или терракота
                float f2 = 0.5 + 0.5 * sin(length(q) * 2.24 - ft * (9.0 / 7.0));    // прожилки и тени

                float m = smoothstep(0.45, 0.55, f1);
                float3 col = lerp(_Deep.rgb, _Warm.rgb, m);

                float vein = gauss(f2, 0.5, 0.06) * smoothstep(0.3, 0.7, f1);
                col = lerp(col, _Vein.rgb, 0.8 * vein);

                float shade = smoothstep(0.0, 0.4, f2) * smoothstep(1.0, 0.6, f2);
                col = lerp(_Shadow.rgb, col, 0.5 + 0.5 * shade);

                float gloss = gauss(f1, 0.9, 0.03) * 0.5;                    // тонкие кремовые блики «жидкости»
                col = lerp(col, _Gloss.rgb, gloss);

                float vig = 1.0 - 0.35 * pow(saturate(r / (_Scale * 0.9)), 2.0);  // мягкая виньетка
                col *= vig * _Brightness;

                return fixed4(col, 1.0) * i.color;
            }
            ENDCG
        }
    }
    Fallback "UI/Default"
}
