using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// HediffComp_SlowTend 的属性定义（XML 可配置参数）。
    /// 在 XML 的 HediffDef &gt; comps 中用
    /// &lt;li Class="KiiroSchizonepetaGum.HediffCompProperties_SlowTend"&gt; 引用。
    /// </summary>
    public class HediffCompProperties_SlowTend : HediffCompProperties
    {
        /// <summary>
        /// 包扎质量（0-1）。等同于药品的 MedicalPotency。
        /// </summary>
        public float tendQuality = 0.4f;

        /// <summary>
        /// 包扎质量上限（0-1）。等同于药品的 MedicalQualityMax。
        /// </summary>
        public float tendQualityMax = 0.5f;

        /// <summary>构造函数：绑定对应的 Comp 逻辑类。</summary>
        public HediffCompProperties_SlowTend()
        {
            compClass = typeof(HediffComp_SlowTend);
        }
    }
}
