using System.Diagnostics;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 统一日志输出工具（mod 级单份，供全部功能模块复用）。
    /// 输出格式为「着色 mod 前缀 + 模块前缀 + 消息」，模块前缀取 KiiroGumLogModule 常量，
    /// 便于在 Player.log 中按模块检索（日志分包）。
    /// 前缀用本包主题色着色（游戏内日志窗口支持富文本）；正文默认不染色，保留日志级别配色，
    /// 需要强调的一次性日志（如启动信息）可用 Emphasize 整段着色。
    /// mod 前缀优先取翻译 key，语言系统未就绪时回退硬编码常量。
    /// 调试级日志走 Debug()，[Conditional("DEBUG")] 使 Release 编译时调用点被整体移除
    /// （连同实参求值），零开销且不产生 unreachable 代码。
    /// </summary>
    public static class KiiroGumLog
    {
        /// <summary>翻译 key：日志前缀（见 Languages/*/Keyed/KiiroSchizonepetaGum.xml）。</summary>
        private const string PrefixKey = "KiiroGum.LogPrefix";

        /// <summary>语言系统未就绪时的兜底前缀。</summary>
        private const string FallbackPrefix = "[KiiroGum]";

        /// <summary>主题色（取描边默认色 0.4,0.8,0.2 对应的荆芥绿）：前缀与需要强调的正文共用。</summary>
        public const string ThemeColor = "#66CC33";

        /// <summary>当前日志前缀：翻译成功用译文，失败或语言未就绪时用兜底常量。
        /// mod 构造函数阶段语言系统可能尚未加载，故每次都做降级判断。</summary>
        private static string Prefix
        {
            get
            {
                string translated = SafeTranslate(PrefixKey);
                return (translated != PrefixKey && !string.IsNullOrEmpty(translated))
                    ? translated
                    : FallbackPrefix;
            }
        }

        /// <summary>安全翻译：语言系统未就绪或抛异常时返回 key 本身。</summary>
        /// <param name="key">翻译 key。</param>
        private static string SafeTranslate(string key)
        {
            try
            {
                if (LanguageDatabase.activeLanguage != null)
                {
                    return key.Translate();
                }
            }
            catch
            {
                // 语言系统异常时静默降级到兜底前缀
            }
            return key;
        }

        /// <summary>拼接「着色 mod 前缀 + 模块前缀」，供各日志方法复用。</summary>
        /// <param name="module">模块前缀，取 KiiroGumLogModule 常量。</param>
        private static string Tag(string module)
        {
            return "<color=" + ThemeColor + ">" + Prefix + "[" + module + "]</color>";
        }

        /// <summary>用主题色强调正文（游戏内日志窗口支持富文本）。
        /// 仅用于启动信息这类一次性日志；普通消息与警告仍保留日志级别自带的配色。</summary>
        /// <param name="text">待着色文本。</param>
        public static string Emphasize(string text)
        {
            return "<color=" + ThemeColor + ">" + text + "</color>";
        }

        /// <summary>普通消息：初始化、状态变更等关键信息，始终输出。</summary>
        /// <param name="module">模块前缀，取 KiiroGumLogModule 常量。</param>
        /// <param name="msg">消息正文。</param>
        public static void Msg(string module, string msg)
        {
            Log.Message(Tag(module) + " " + msg);
        }

        /// <summary>警告：可恢复的异常状况，始终输出。</summary>
        /// <param name="module">模块前缀，取 KiiroGumLogModule 常量。</param>
        /// <param name="msg">消息正文。</param>
        public static void Warn(string module, string msg)
        {
            Log.Warning(Tag(module) + " " + msg);
        }

        /// <summary>错误：功能不可用等需要玩家注意的问题，始终输出。</summary>
        /// <param name="module">模块前缀，取 KiiroGumLogModule 常量。</param>
        /// <param name="msg">消息正文。</param>
        public static void Err(string module, string msg)
        {
            Log.Error(Tag(module) + " " + msg);
        }

        /// <summary>异常：附完整堆栈。</summary>
        /// <param name="module">模块前缀，取 KiiroGumLogModule 常量。</param>
        /// <param name="msg">消息正文。</param>
        /// <param name="ex">待记录的异常。</param>
        public static void Exception(string module, string msg, System.Exception ex)
        {
            Log.Error(Tag(module) + " " + msg + ": " + ex);
        }

        /// <summary>调试日志：仅 Debug 构建生成调用。
        /// [Conditional] 使 Release 编译移除整个调用点（连同实参求值），零开销；
        /// 运行时另受 KiiroGumDebug.EnableLog 开关约束。</summary>
        /// <param name="module">模块前缀，取 KiiroGumLogModule 常量。</param>
        /// <param name="msg">消息正文。</param>
        [Conditional("DEBUG")]
        public static void Debug(string module, string msg)
        {
            if (KiiroGumDebug.EnableLog)
            {
                Log.Message(Tag(module) + " [调试] " + msg);
            }
        }
    }
}
