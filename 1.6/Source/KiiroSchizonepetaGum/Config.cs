namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// Mod 全局共享常量：只存放跨模块引用的 Def 名称。
    /// 各模块内部的数值参数请放在对应模块自己的 config 类中
    /// （如 WorkGiversConfig、SlowTendConfig、SoundPlayerConfig 等），
    /// 注意，这里的常量只是单独拿出来参考，需要和xml中的值保持一致，因为实际运行时xml会进行注入。
    /// </summary>
    public static class Config
    {
        /// <summary>口香糖 ThingDef defName。
        /// XML 来源：Defs/Drugs/Drug_ChewingGum.xml → ThingDef/defName。
        /// 被 WorkGivers 模块的 WorkGiver / JobGiver 引用。</summary>
        public const string GumDefName = "Kiiro_SchizonepetaGum";

        /// <summary>口香糖 HediffDef defName。
        /// XML 来源：Defs/HediffDefs/Hediff_ChewingGum.xml → HediffDef/defName。
        /// 被 WorkGivers 模块与 ThoughtWorkers 模块引用。</summary>
        public const string GumHediffDefName = "Kiiro_SchizonepetaGumHigh";

        /// <summary>绮罗族 ThingDef defName。
        /// XML 来源：KiiroRace 种族 mod（AlienRace 框架）的 Race 定义，不在本 mod 内。
        /// 被 WorkGivers 模块与 ThoughtWorkers 模块引用。</summary>
        public const string KiiroRaceDefName = "Kiiro_Race";
    }
}
