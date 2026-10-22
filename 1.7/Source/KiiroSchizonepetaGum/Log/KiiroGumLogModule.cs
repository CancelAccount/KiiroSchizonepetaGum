namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 日志模块前缀常量：集中管理各功能模块的日志标签（日志分包）。
    /// 单 dll 承载多个功能模块，Player.log 中靠模块前缀区分来源；
    /// 模块名在此统一定义，避免调用点手写造成拼写漂移、日志无法检索。新增模块时只改这一处。
    /// </summary>
    public static class KiiroGumLogModule
    {
        /// <summary>核心基础设施与 mod 级事件（初始化、设置、构建/包信息）。</summary>
        public const string Core = "Core";

        /// <summary>人物描边（HediffComp_Outline / KiiroGumOutlineManager / shader 加载）。</summary>
        public const string Outline = "Outline";
    }
}
