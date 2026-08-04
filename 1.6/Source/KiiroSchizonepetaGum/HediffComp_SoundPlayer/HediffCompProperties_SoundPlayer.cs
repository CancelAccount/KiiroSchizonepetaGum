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

        /// <summary>音效触发间隔（tick）。默认 3600 tick，1x 速度下约 60 秒。
        /// 注意：高倍速下该间隔对应的真实时间会缩短。</summary>
        public int intervalTicks = 3600;

        /// <summary>音效时长（真实秒数）。
        /// 用于防止高倍速 mod 下前一个音效尚未播完就触发下一个，导致多重音效叠加。
        /// 默认 10 秒。应与实际音效 clip 的自然时长匹配。
        /// 前提：SoundDef 未设 tempoAffectedByGameSpeed（默认 false） </summary>
        public float soundDurationReal = 10f;

        /// <summary>构造函数：绑定对应的 Comp 逻辑类。</summary>
        public HediffCompProperties_SoundPlayer()
        {
            compClass = typeof(HediffComp_SoundPlayer);
        }
    }
}
