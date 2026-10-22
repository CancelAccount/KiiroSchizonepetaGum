#version 450

// ============================================================================
// KiiroGumOutline — RimWorld 1.7 人物外描边（单 pass 邻域膨胀）
//
// 输入：pawn atlas 合成帧（身体+衣物+发型+头饰的烘焙 RenderTexture），
//       mesh UV 已锁定在该 pawn 帧区域内（PawnTextureAtlasFrameSet.meshes）。
//
// 片元逻辑（与 1.6 的 Unity shader 完全等价）：
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
// 渲染状态见 Defs/ShaderDefs/KiiroGum_Outline.xml：
//   renderQueue 2899 = 本体 Cutout（2900）之前一档；
//   描边层高度略高于本体（C# 侧 AltitudeForLayer(1)），遮挡关系与本体一致，
//   人物像素处由本 shader 挖空输出全透明，因此不会覆盖本体。
// ============================================================================

layout(location = 1) in vec2 vUv;

layout(location = 0) out vec4 outColor;

// 材质参数（std140）。_MainTex_ST 由引擎按 flipTextureST 自动填充为
// (0, 1, 1, -1)，其余由 C# 经 MaterialPropertyBlock 每帧提交
layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _OutlineColor;  // @color @default = vec4(0.4, 0.8, 0.2, 1.0)
    vec4 _FrameUV;       // @default = vec4(0.0, 0.0, 1.0, 1.0)
    vec4 _MainTex_ST;    // @default = vec4(0.0, 0.0, 1.0, 1.0)
    float _OutlineWidth; // @default = 2.0
    float _HoleRadius;   // @default = 1.0
    float _AlphaCutoff;  // @default = 0.5
} u;

layout(set = 2, binding = 1) uniform sampler2D _MainTex;

// 采样 alpha，并把 uv clamp 在帧区域内。
// 用 textureLod(mip 0) 而非 texture()：atlas 是没有 mip 的 RenderTexture，
// 且隐式梯度指令不允许出现在循环次数随 uniform 变化的循环内。
float sampleAlpha(vec2 uv, vec2 uvMin, vec2 uvMax)
{
    return textureLod(_MainTex, clamp(uv, uvMin, uvMax), 0.0).a;
}

void main()
{
    // 1.7 默认 flipTextureST = true（_MainTex_ST = (0,1,1,-1)），
    // 必须与官方 Cutout 一样做 uv * ST.zw + ST.xy，否则采样上下颠倒
    vec2 scale = u._MainTex_ST.zw;
    vec2 offset = u._MainTex_ST.xy;
    vec2 uv = vUv * scale + offset;

    // 帧边界同样要先做 ST 变换；scale.y 为负时两端点会互换，故取 min/max
    vec2 frameA = u._FrameUV.xy * scale + offset;
    vec2 frameB = u._FrameUV.zw * scale + offset;
    vec2 uvMin = min(frameA, frameB);
    vec2 uvMax = max(frameA, frameB);

    // 自身即人物像素 → 输出全透明
    //（本体绘制在本层之后，此处透明保证描边绝不覆盖人物像素）
    float selfAlpha = sampleAlpha(uv, uvMin, uvMax);
    if (selfAlpha > u._AlphaCutoff)
    {
        outColor = vec4(0.0);
        return;
    }

    // 膨胀半径：描边宽度 + 孔洞填充（上限 4 texel，邻域最多 9×9 = 81 次采样）
    vec2 texel = 1.0 / vec2(textureSize(_MainTex, 0));
    int radius = int(clamp(round(u._OutlineWidth + u._HoleRadius), 1.0, 4.0));

    // 方形邻域扫描，取 alpha 最大值
    float maxAlpha = 0.0;
    for (int dy = -radius; dy <= radius; dy++)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            float a = sampleAlpha(uv + vec2(float(dx), float(dy)) * texel, uvMin, uvMax);
            maxAlpha = max(maxAlpha, a);
        }
    }

    // 邻域内存在人物像素 → 本像素在描边带内 → 输出描边色
    //（alpha 继承邻域最大值，描边带外缘自然抗锯齿）
    if (maxAlpha > u._AlphaCutoff)
    {
        vec4 col = u._OutlineColor;
        col.a *= maxAlpha;
        outColor = col;
        return;
    }
    outColor = vec4(0.0);
}
