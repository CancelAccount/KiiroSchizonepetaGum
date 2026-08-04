using RimWorld;
using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 口香糖自愈视觉特效组件。
    ///
    /// 效果：间歇粒子
    ///   - 用 FleckMaker.AttachedOverlay 实现
    ///   - 每 N tick 在 pawn 身上生成一个粒子
    ///   - 生成时在 x/y 轴方向叠加随机偏移
    ///   - 粒子自动按 fadeIn/solid/fadeOut 动画播放并消失（一次性，无需清理）
    ///
    /// 受 mod 设置控制（enableVisualEffect），关闭后不生成粒子。
    /// </summary>
    public class HediffComp_VisualEffect : HediffComp
    {
        /// <summary>获取属性配置。</summary>
        public HediffCompProperties_VisualEffect Props => (HediffCompProperties_VisualEffect)props;

        /// <summary>
        /// 每 tick 调用：间歇生成粒子。
        /// 受 mod 设置控制（enableVisualEffect），关闭后不执行任何特效逻辑。
        /// </summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            // 设置界面关闭了特效 → 跳过全部逻辑
            if (KiiroSchizonepetaGumMod.Settings == null || !KiiroSchizonepetaGumMod.Settings.enableVisualEffect)
            {
                return;
            }

            // pawn 不在地图上时不生成特效（如远行队中）
            if (Pawn == null || Pawn.Map == null)
            {
                return;
            }

            // 间歇生成粒子
            if (Props.particleFleckDef != null && Pawn.IsHashIntervalTick(Props.particleIntervalTicks))
            {
                SpawnParticle();
            }
        }

        /// <summary>在 pawn 身上生成一个粒子 fleck。
        /// 生成时在 x 轴（左右）、y 轴（高度）方向叠加随机偏移，让粒子分布更自然。</summary>
        private void SpawnParticle()
        {
            // 以配置的基础偏移为起点，叠加 x/y 轴随机偏移

            Vector3 offset = Props.particleOffset;
            if (Props.particleRandomOffsetX > 0f)
            {
                offset.x += Rand.Range(-Props.particleRandomOffsetX, Props.particleRandomOffsetX);
            }
            if (Props.particleRandomOffsetY > 0f)
            {
                offset.y += Rand.Range(-Props.particleRandomOffsetY, Props.particleRandomOffsetY);
            }

            // FleckMaker.AttachedOverlay 会在 thing 位置生成一个跟随的 fleck
            // fleck 是一次性的，生成后自动按 fadeIn/solid/fadeOut 动画播放并消失
            FleckMaker.AttachedOverlay(
                Pawn,
                Props.particleFleckDef,
                offset,
                Props.particleScale);
        }
    }
}
