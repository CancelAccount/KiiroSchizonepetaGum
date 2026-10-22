namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 视觉特效模块（HediffComp_VisualEffect）配置：负责粒子特效的常量与默认值。
    /// HediffComp_VisualEffect / HediffCompProperties_VisualEffect 使用。
    /// </summary>
    public static class VisualEffectConfig
    {
        /// <summary>粒子生成间隔默认值（tick）。
        /// XML 来源：Defs/HediffDefs/Hediff_ChewingGum.xml → HediffDef/comps/li[Class=HediffCompProperties_VisualEffect]/particleIntervalTicks（已配置 300）。</summary>
        public const int ParticleIntervalTicksDefault = 300;

        /// <summary>粒子缩放默认值。
        /// XML 来源：同上 XML → .../particleScale（已配置 0.8）。</summary>
        public const float ParticleScaleDefault = 1f;

        /// <summary>粒子 x 轴随机偏移幅度默认值。
        /// XML 来源：同上 XML → .../particleRandomOffsetX（已配置 0.5）。</summary>
        public const float ParticleRandomOffsetXDefault = 0.5f;

        /// <summary>粒子 y 轴随机偏移幅度默认值。
        /// XML 来源：同上 XML → .../particleRandomOffsetY（已配置 0.4）。</summary>
        public const float ParticleRandomOffsetYDefault = 0.4f;
    }
}
