using RimWorld;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 缓慢自愈 HediffComp 的属性定义（XML 可配置参数）。
    /// 复刻原版 Anomaly 的 regeneration 机制，但不依赖 Anomaly DLC。
    /// 在 XML 的 HediffDef &gt; comps 中用 &lt;li Class="KiiroSchizonepetaGum.HediffCompProperties_SlowRegen"&gt; 引用。
    /// </summary>
    public class HediffCompProperties_SlowRegen : HediffCompProperties
    {
        /// <summary>每天恢复的 hp 总量。
        /// 与原版 regeneration 语义一致：值=N 表示每天恢复 N hp（默认 20）。
        /// 计算方式：每 15 tick 触发一次，每次治疗量 = N * 0.00025，
        /// 每天 60000/15=4000 次，故 N*0.00025*4000 = N hp/天。
        /// </summary>
        public float healAmountPerDay = 20f;

        /// <summary>构造函数：绑定对应的 Comp 逻辑类。</summary>
        public HediffCompProperties_SlowRegen()
        {
            compClass = typeof(HediffComp_SlowRegen);
        }
    }
}
