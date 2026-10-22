namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// WorkGivers 模块配置：负责"受伤自动找口香糖"相关逻辑的常量。
    /// JobGiver_CatTakeGumWhenInjured / WorkGiver_TakeGumWhenInjured 使用。
    /// </summary>
    public static class WorkGiversConfig
    {
        /// <summary>猫 ThingDef defName。
        /// XML 来源：Core 原版 Races_Animal_CatGroup.xml → ThingDef/defName，仅猫的 JobGiver 使用。</summary>
        public const string CatDefName = "Cat";

        /// <summary>受伤猫搜索口香糖的节流间隔（tick）。防止每 tick 全图 BFS。
        /// 无 XML 来源：脚本内部性能参数，仅 JobGiver_CatTakeGumWhenInjured 使用。</summary>
        public const int CatSearchThrottleTicks = 60;

        /// <summary>猫搜索口香糖的最远距离（格）。
        /// 无 XML 来源：脚本内部参数，仅 JobGiver_CatTakeGumWhenInjured 使用。</summary>
        public const float CatSearchMaxDistance = 999f;

        /// <summary>节流哨兵值：标记"从未搜索过"，保证首次调用立即执行。
        /// 无 XML 来源：脚本内部参数。</summary>
        public const int NeverSearchedTick = -99999;
    }
}
