namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 音效播放模块（HediffComp_SoundPlayer）配置：负责音效逻辑的常量与默认值。
    /// 仅本模块（HediffComp_SoundPlayer / HediffCompProperties_SoundPlayer）使用。
    /// </summary>
    public static class SoundPlayerConfig
    {
        /// <summary>音效触发间隔默认值（tick）。
        /// XML 来源：Defs/HediffDefs/Hediff_ChewingGum.xml → HediffDef/comps/li[Class=HediffCompProperties_SoundPlayer]/intervalTicks（已配置 6000）。</summary>
        public const int SoundIntervalTicksDefault = 6000;

        /// <summary>音效时长默认值（真实秒），用于高倍速下防重叠。
        /// XML 来源：同上 XML → .../soundDurationReal（已配置 10）。</summary>
        public const float SoundDurationRealDefault = 10f;

        /// <summary>打扰睡眠半径默认值（格），0 表示不打扰。
        /// XML 来源：同上 XML → .../disturbRadius（已配置 15）。</summary>
        public const float DisturbRadiusDefault = 15f;

        /// <summary>音效哨兵值：标记"从未播放过"，保证首次到间隔即触发。
        /// 无 XML 来源：脚本内部参数。</summary>
        public const float NeverPlayedRealTime = -100f;
    }
}
