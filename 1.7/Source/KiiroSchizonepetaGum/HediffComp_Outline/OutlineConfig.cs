using UnityEngine;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 描边模块（HediffComp_Outline / KiiroGumOutlineManager）配置：常量与默认值。
    /// 数值依据 1.6 反编译源码核实，详细依据见各常量注释。
    /// </summary>
    public static class OutlineConfig
    {
        /// <summary>描边 ShaderDef 的 defName（1.7 起着色器改为 GLSL 源码 + ShaderDef 注册，
        /// 不再依赖 Unity AssetBundle）。
        /// XML 来源：Defs/ShaderDefs/KiiroGum_Outline.xml → ShaderDef/defName。</summary>
        public const string ShaderDefDefName = "KiiroGumOutline";

        /// <summary>描边 ShaderTypeDef 的 defName（1.6 的 AB 路径，1.7 仅作 ShaderDef 缺失时的回退）。
        /// XML 来源：Defs/ShaderTypeDefs/KiiroGum_Outline.xml → ShaderTypeDef/defName。</summary>
        public const string ShaderTypeDefDefName = "KiiroGum_Outline";

        /// <summary>描边层相对 pawn body 层的高度偏移（层号，非世界单位）。
        /// 换算见 PawnRenderUtility.AltitudeForLayer(layer) = clamp(layer,-10,100) × 0.0003658537。
        ///
        /// 取 1（略高于本体所在的 0 层）而不是负值，原因有两条：
        /// 1. shader 自身已做挖空——采样到人物像素（alpha &gt; _AlphaCutoff）时输出全透明，
        ///    因此描边覆盖在本体之上也不会把人物染成描边色，无需沉到本体下方避让；
        /// 2. 沉到本体下方（旧值 -8 ≈ -0.0029 世界单位）会落到地面/低矮植被层之下，
        ///    造成「人物本体压在荆棘上方、描边却被荆棘盖住」的割裂。
        /// 取正值而非 0 是为了避开与本体共面时的深度比较精度问题。</summary>
        public const float OutlineAltitudeLayer = 1f;

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

        /// <summary>诊断日志输出间隔（tick）：按该周期打印一次渲染参数，用于观察
        /// bodyPos / bodyAngle 是否随动画变化。取与 60 互质的奇数，
        /// 避免采样点总落在动画周期同一相位而误判「没有偏移」。
        /// 该日志属于 Debug 级：由 KiiroGumLog.Debug 输出，[Conditional("DEBUG")] 使其在
        /// Release 构建中连同字符串拼接一起被移除，Debug 构建再由 KiiroGumDebug.EnableLog 控制。</summary>
        public const int DiagnosticsIntervalTicks = 7;
    }
}
