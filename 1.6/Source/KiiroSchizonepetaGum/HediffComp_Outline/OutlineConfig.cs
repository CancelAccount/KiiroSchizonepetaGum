using UnityEngine;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 描边模块（HediffComp_Outline / KiiroGumOutlineManager）配置：常量与默认值。
    /// 数值依据 1.6 反编译源码核实，详细依据见各常量注释。
    /// </summary>
    public static class OutlineConfig
    {
        /// <summary>描边 ShaderTypeDef 的 defName。
        /// XML 来源：Defs/ShaderTypeDefs/KiiroGum_Outline.xml → ShaderTypeDef/defName。</summary>
        public const string ShaderTypeDefDefName = "KiiroGum_Outline";

        /// <summary>描边层相对 pawn body 层的高度偏移（层号，非世界单位）。
        /// 换算见 PawnRenderUtility.AltitudeForLayer(layer) = clamp(layer,-10,100) × 0.0003658537。
        /// 官方 apparel extras 使用 -10/90（PawnRenderer L326），取 -8 避开官方已占层值，
        /// 保证描边层位于 pawn 本体之下、被本体不透明像素覆盖，只露出外圈描边带。</summary>
        public const float OutlineAltitudeLayer = -8f;

        /// <summary>镜头过远时停用描边的判定系数（相对相机最大 RootSize）。
        /// ZoomRootSize ≥ config.sizeRange.max × 本系数时不渲染（默认 0.6×60 = 36）：
        /// 远景时 pawn 屏幕尺寸过小、描边不可辨，关闭以节省性能。
        /// 用相对值而非绝对值的写法参照官方远景剪影启用判定（SilhouetteUtility L122：
        /// ZoomRootSize ≥ sizeRange.max × 0.9），自动适配玩家自定义的相机缩放范围。</summary>
        public const float FarCameraDisableFactor = 0.6f;

        /// <summary>描边宽度默认值（图集 texel 单位）。
        /// XML 来源：Defs/HediffDefs/Hediff_ChewingGum.xml → HediffDef/comps/li[Class=HediffCompProperties_Outline]/outlineWidth。</summary>
        public const float OutlineWidthDefault = 2f;

        /// <summary>描边宽度最小值（图集 texel 单位，玩家设置滑条下限）。</summary>
        public const float OutlineWidthMin = 1f;

        /// <summary>描边宽度最大值（图集 texel 单位，玩家设置滑条上限）。
        /// 上限与 shader 邻域扫描硬上限（半径 ≤ 4 texel）联动，见 KiiroGumOutline.shader。</summary>
        public const float OutlineWidthMax = 4f;

        /// <summary>描边颜色默认值（与 Hediff XML 默认值保持一致，供设置类/Properties 共用）。</summary>
        public static readonly Color OutlineColorDefault = new Color(0.4f, 0.8f, 0.2f);

        /// <summary>设置界面的描边颜色预设色板（点击即切换，避免自制取色器的复杂度）。</summary>
        public static readonly Color[] OutlineColorPresets =
        {
            new Color(0.4f, 0.8f, 0.2f),  // 荆芥绿（默认）
            Color.white,                   // 纯白
            new Color(1f, 0.85f, 0.3f),    // 金
            new Color(0.75f, 0.4f, 1f),    // 紫
            new Color(1f, 0.35f, 0.35f),   // 红
            new Color(0.3f, 0.9f, 1f)      // 青
        };

        /// <summary>色板单元格高度（设置界面色板行高，UI 常量）。</summary>
        public const float ColorSwatchHeight = 24f;

        /// <summary>描边模块调试日志开关。
        /// 排查渲染问题时置 true：向控制台/Player.log 输出注册、全局早退原因、
        /// shader 加载结果、绘制提交等关键节点日志（前缀 [KiiroGumOutline]）。
        /// 日志带字符串拼接（每帧路径有轻微开销），定位完毕后改回 false 再发布。</summary>
        public const bool DebugLogging = false;
    }
}
