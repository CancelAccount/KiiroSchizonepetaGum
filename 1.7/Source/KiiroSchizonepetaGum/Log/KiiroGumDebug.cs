namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 调试开关中枢：区分调试包与发行包。
    /// 包类型由编译配置自动驱动（Debug 配置定义 DEBUG 宏，Release 不定义），发版无需手动改常量，
    /// 与 Tools/Build-Release.ps1 产出的两套包（Assemblies / AssembliesDebug）一一对应。
    /// </summary>
    public static class KiiroGumDebug
    {
#if DEBUG
        /// <summary>是否为调试构建（编译期常量：Release 下折叠为 false，相关分支被消除）。</summary>
        public const bool DebugBuild = true;
#else
        /// <summary>是否为调试构建（编译期常量：Release 下折叠为 false，相关分支被消除）。</summary>
        public const bool DebugBuild = false;
#endif

        /// <summary>当前程序集包类型名：随编译配置自动切换，供启动日志显示。</summary>
        public const string BuildTypeName = DebugBuild ? "调试版（Debug）" : "发行版（Release）";

        /// <summary>当前日志级别名：调试包输出 Debug 级，发行包仅 Info / Warning。</summary>
        public const string LogLevelName = DebugBuild ? "Debug" : "Info";

#if DEBUG
        /// <summary>调试构建下默认开启调试日志；需要静默时可运行时关闭。</summary>
        private static bool enableLog = true;
#else
        /// <summary>发行包不启用调试日志（getter 恒 false，此字段不生效）。</summary>
        private static bool enableLog;
#endif

        /// <summary>调试日志运行时开关：读取受 DebugBuild 门控，发行包恒为 false。</summary>
        public static bool EnableLog
        {
            get { return DebugBuild && enableLog; }
            set { enableLog = value; }
        }
    }
}
