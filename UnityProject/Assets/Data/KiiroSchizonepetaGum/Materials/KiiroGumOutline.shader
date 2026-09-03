// ============================================================================
// KiiroGumOutline — RimWorld 人物外描边 shader（单 pass 邻域膨胀）
//
// 输入：pawn atlas 合成帧（身体+衣物+发型+头饰的烘焙 RenderTexture），
//       mesh UV 已锁定在该 pawn 帧区域内（PawnTextureAtlasFrameSet.meshes）。
//
// 片元逻辑：
//   1. 自身 alpha > _AlphaCutoff → 人物像素 → 输出全透明
//   2. 否则扫描 (2R+1)² 方形邻域（R = _OutlineWidth + _HoleRadius，clamp 1~4
//      texel，最多 81 次采样）取 alpha 最大值 amax；
//      amax > _AlphaCutoff → 本像素在人物外 R texel 范围内 → 输出描边色
//      （输出 alpha = amax，继承图集边缘的抗锯齿渐变）
//   3. 发丝缝隙等宽 ≤ 2R 的内部细缝会被描边色填充，不产生独立描边线
//
// 采样边界：所有采样 UV clamp 到 _FrameUV（C# 每帧经 MaterialPropertyBlock
// 传入该 pawn 帧的 uvRect 边界），防止膨胀采样越界读到图集中相邻 pawn 的帧。
//
// 渲染状态（层序关键，勿随意改动）：
//   Queue = AlphaTest-1（2449）：先于 pawn 本体（Map/Cutout，2450）绘制、
//     后于 Opaque 地面/建筑绘制 → 本体不透明像素覆盖描边内部只露外圈；
//     ZTest LEqual → 被 y 更高的建筑正确遮挡（不穿墙）
//   ZWrite Off：描边层不写深度，不影响后续物体
//   Blend SrcAlpha OneMinusSrcAlpha：描边带半透明边缘抗锯齿
// ============================================================================

Shader "KiiroGumOutline"
{
    Properties
    {
        _MainTex ("Atlas (pawn composite frame)", 2D) = "white" {}
        _Color ("Tint (unused by outline)", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0.4, 0.8, 0.2, 1)
        _OutlineWidth ("Outline Width (texel)", Float) = 2
        _HoleRadius ("Hole Fill Radius (texel)", Float) = 1
        _AlphaCutoff ("Alpha Cutoff", Range(0.01, 0.99)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest-1" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 3.5：邻域扫描半径是材质参数（uniform），循环次数随参数变化，需 SM4 级循环支持
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            // Unity 自动填充：xy = 1/宽高（图集 texel 步长）
            float4 _MainTex_TexelSize;
            // 帧边界（C# 传入）：xy = uvMin，zw = uvMax
            float4 _FrameUV;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _HoleRadius;
            float _AlphaCutoff;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            /// 顶点变换（与官方 Map 系 shader 一致的最小实现）。
            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            /// 采样指定 UV 的 alpha，并 clamp 到帧边界内
            /// （防止膨胀采样越界读到图集中相邻 pawn 的帧）。
            /// 用 tex2Dlod 而非 tex2D：tex2D 是梯度指令（依赖 ddx/ddy），
            /// 不允许出现在迭代次数随 uniform 变化的循环里（否则编译报
            /// "gradient instruction used in a loop with varying iteration"）；
            /// tex2Dlod 显式取 mip 0，atlas 是无 mip 的 RenderTexture，两者行为一致。
            float SampleAlphaClamped(float2 uv)
            {
                float2 clamped = clamp(uv, _FrameUV.xy, _FrameUV.zw);
                return tex2Dlod(_MainTex, float4(clamped, 0.0, 0.0)).a;
            }

            /// 片元：非人物像素在膨胀带内 → 描边色；否则透明。
            fixed4 frag(v2f i) : SV_Target
            {
                // 自身即人物像素 → 输出全透明
                //（本体绘制在本层之后，此处透明保证描边绝不覆盖人物像素）
                float selfAlpha = SampleAlphaClamped(i.uv);
                bool isBody = selfAlpha > _AlphaCutoff;
                if (isBody)
                {
                    return fixed4(0, 0, 0, 0);
                }

                // 膨胀半径：描边宽度 + 孔洞填充（上限 4 texel，邻域最多 9×9 = 81 次采样）
                // radius 为 uniform（材质参数），循环次数所有片元一致，控制流统一
                int radius = (int)clamp(round(_OutlineWidth + _HoleRadius), 1.0, 4.0);
                float2 texel = _MainTex_TexelSize.xy;

                // 方形邻域扫描，取 alpha 最大值
                float maxAlpha = 0.0;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        float a = SampleAlphaClamped(i.uv + float2(dx, dy) * texel);
                        if (a > maxAlpha)
                        {
                            maxAlpha = a;
                        }
                    }
                }

                // 邻域内存在人物像素 → 本像素在描边带内 → 输出描边色
                //（alpha 继承邻域最大值，描边带外缘自然抗锯齿）
                if (maxAlpha > _AlphaCutoff)
                {
                    fixed4 col = _OutlineColor;
                    col.a *= maxAlpha;
                    return col;
                }
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
