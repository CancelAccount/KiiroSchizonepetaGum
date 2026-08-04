using RimWorld;
using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 视觉特效 HediffComp 的属性定义（XML 可配置参数）。
    /// 在 XML 的 HediffDef > comps 中用
    /// <li> class="KiiroSchizonepetaGum.HediffCompProperties_VisualEffect" </li> 引用。
    /// </summary>
    public class HediffCompProperties_VisualEffect : HediffCompProperties
    {
        /// <summary>间歇粒子的 FleckDef。</summary>
        public FleckDef particleFleckDef;

        /// <summary>粒子生成间隔（tick）。默认 300 tick ≈ 5 秒。</summary>
        public int particleIntervalTicks = 300;

        /// <summary>粒子在 pawn 身上的偏移（x=左右, y=高度, z=前后）。</summary>
        public Vector3 particleOffset = new Vector3(0f, 0f, 0f);

        /// <summary>粒子生成时在 x 轴（左右）方向的随机偏移幅度。
        /// 实际偏移在 [-particleRandomOffsetX, +particleRandomOffsetX] 区间内均匀随机。 </summary>
        public float particleRandomOffsetX = 0.5f;

        /// <summary>粒子生成时在 y 轴（高度）方向的随机偏移幅度。
        /// 实际偏移在 [-particleRandomOffsetY, +particleRandomOffsetY] 区间内均匀随机。 </summary>
        public float particleRandomOffsetY = 0.4f;

        /// <summary>粒子缩放比例。</summary>
        public float particleScale = 1f;

        /// <summary>构造函数：绑定对应的 Comp 逻辑类。</summary>
        public HediffCompProperties_VisualEffect()
        {
            compClass = typeof(HediffComp_VisualEffect);
        }
    }
}
