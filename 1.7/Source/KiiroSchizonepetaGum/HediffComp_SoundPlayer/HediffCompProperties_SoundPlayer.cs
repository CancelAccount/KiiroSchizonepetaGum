using RimWorld;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 音效播放 HediffComp 的属性定义（XML 可配置参数）。
    /// 在 XML 的 HediffDef &gt; comps 中用
    /// &lt;li Class="KiiroSchizonepetaGum.HediffCompProperties_SoundPlayer"&gt; 引用。
    /// </summary>
    public class HediffCompProperties_SoundPlayer : HediffCompProperties
    {
        /// <summary>要播放的音效定义。
        /// 建议配 AudioGrain_Folder 以便从多条音频中随机选一条播放。</summary>
        public SoundDef soundDef;

        /// <summary>音效触发间隔（tick）。默认 SoundPlayerConfig.SoundIntervalTicksDefault（6000 tick，1x 速度下约 100 秒）。
        /// 注意：高倍速下该间隔对应的真实时间会缩短。</summary>
        public int intervalTicks = SoundPlayerConfig.SoundIntervalTicksDefault;

        /// <summary>音效时长（真实秒数）。
        /// 用于防止高倍速 mod 下前一个音效尚未播完就触发下一个，导致多重音效叠加。
        /// 默认 SoundPlayerConfig.SoundDurationRealDefault（10 秒）。应与实际音效 clip 的自然时长匹配。
        /// 前提：SoundDef 未设 tempoAffectedByGameSpeed（默认 false） </summary>
        public float soundDurationReal = SoundPlayerConfig.SoundDurationRealDefault;

        /// <summary>老吴日志的 RulePackDef。
        /// 配置后，每次触发音效时同步往人物日志（BattleLog）添加一条老吴记录。
        /// RulePackDef 的 rulesStrings 中多条 r_logentry 会被随机选一条生成文本。
        /// 不配则只播音效、不记日志。日志独立于音效开关（关闭音效仍记录）。</summary>
        public RulePackDef logEntryPack;

        /// <summary>打扰睡眠的半径（格）。
        /// 默认 SoundPlayerConfig.DisturbRadiusDefault（15 格）。半径内睡觉的人物会获得"睡眠被打扰"想法（Core 的 SleepDisturbed）。
        /// 无视墙体（纯水平距离判断，不检查视线）。设为 0 则不打扰睡眠。</summary>
        public float disturbRadius = SoundPlayerConfig.DisturbRadiusDefault;

        /// <summary>老吴日志条目的图标贴图路径（相对 Textures/ 目录）。
        /// 例如 "UI/Icons/LaoWuScream" 对应 Textures/UI/Icons/LaoWuScream.png。
        /// 不匹配则日志条目无图标。路径不带文件扩展名。</summary>
        public string logEntryIconPath;

        /// <summary>构造函数：绑定对应的 Comp 逻辑类。</summary>
        public HediffCompProperties_SoundPlayer()
        {
            compClass = typeof(HediffComp_SoundPlayer);
        }
    }
}
