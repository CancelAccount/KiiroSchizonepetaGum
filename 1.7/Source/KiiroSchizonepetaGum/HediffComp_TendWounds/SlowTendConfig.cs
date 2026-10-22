namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 慢速包扎模块（HediffComp_SlowTend）配置：负责包扎逻辑的常量与默认值。
    /// 本模块（HediffComp_SlowTend / HediffCompProperties_SlowTend）使用。
    /// </summary>
    public static class SlowTendConfig
    {
        /// <summary>慢速包扎触发间隔（tick）。
        /// 无 XML 来源：脚本内部触发频率（Hediff_ChewingGum.xml 注释描述"每 150 帧包扎一个流血伤口"）。</summary>
        public const int SlowTendIntervalTicks = 150;

        /// <summary>包扎质量默认值（0-1），等同药品 MedicalPotency。
        /// XML 来源：Defs/HediffDefs/Hediff_ChewingGum.xml → HediffDef/comps/li[Class=HediffCompProperties_SlowTend]/tendQuality（已配置 0.4）。</summary>
        public const float TendQualityDefault = 0.4f;

        /// <summary>包扎质量上限默认值（0-1），等同药品 MedicalQualityMax。
        /// XML 来源：同上 XML → .../tendQualityMax（已配置 0.5）。</summary>
        public const float TendQualityMaxDefault = 0.5f;
    }
}
