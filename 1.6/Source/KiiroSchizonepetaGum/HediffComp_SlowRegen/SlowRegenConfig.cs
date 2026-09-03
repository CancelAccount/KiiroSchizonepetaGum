namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 缓慢再生模块（HediffComp_SlowRegen）配置：负责再生逻辑的常量与默认值。
    /// 仅本模块（HediffComp_SlowRegen / HediffCompProperties_SlowRegen）使用。
    /// </summary>
    public static class SlowRegenConfig
    {
        /// <summary>缓慢再生触发间隔（tick）。与原版 Anomaly regeneration 一致。
        /// 无 XML 来源：脚本内部触发频率。</summary>
        public const int SlowRegenIntervalTicks = 15;

        /// <summary>再生治疗量系数：每 tick 治疗量 = healAmountPerDay * 此值。
        /// 配合 SlowRegenIntervalTicks=15（每天 60000/15=4000 次），
        /// 即每天恢复 healAmountPerDay 点 hp。
        /// 无 XML 来源：原版 Anomaly regeneration 机制系数。</summary>
        public const float RegenHealFactorPerTick = 0.00025f;

        /// <summary>每天恢复 hp 默认值。
        /// XML 来源：Defs/HediffDefs/Hediff_ChewingGum.xml → HediffDef/comps/li[Class=HediffCompProperties_SlowRegen]/healAmountPerDay（已配置 10）。</summary>
        public const float HealAmountPerDayDefault = 20f;
    }
}
