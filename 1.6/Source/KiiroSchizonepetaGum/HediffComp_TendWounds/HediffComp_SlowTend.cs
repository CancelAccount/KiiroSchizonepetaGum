using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 每 15 tick 包扎一个正在流血的伤口（草药级别）。
    /// 参考原版 TendUtility.DoTend 的实现：
    ///   - hediff.TendableNow(false) 判断是否可包扎
    ///   - hediff.Bleeding 判断是否在流血（BleedRate > 1E-05f）
    ///   - hediff.BleedRate 获取流血速度（用于排序）
    ///   - hediff.Tended(quality, maxQuality, batchPosition) 执行包扎
    /// </summary>
    public class HediffComp_SlowTend : HediffComp
    {
        /// <summary>获取属性配置（把基类 props 转成本 comp 的属性类型）。</summary>
        public HediffCompProperties_SlowTend Props => (HediffCompProperties_SlowTend)props;

        /// <summary>复用列表，避免每次 tick 都 new 一个 List（参考原版 tmpHediffInjuries 写法）。</summary>
        private List<Hediff> tmpBleedingWounds = new List<Hediff>();

        /// <summary>
        /// 每 tick 调用。每 150 tick 执行一次包扎。
        /// </summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn.IsHashIntervalTick(150))
            {
                TendOneBleedingWound();
            }
        }

        /// <summary>
        /// 包扎流血速度最快的可包扎伤口。
        /// 先遍历所有伤口收集到缓存列表，按 BleedRate 降序排序后包扎第一个。
        /// </summary>
        private void TendOneBleedingWound()
        {
            // 安全检查
            if (Pawn == null || Pawn.health == null || Pawn.health.hediffSet == null)
            {
                return;
            }

            // 收集所有可包扎且正在流血的伤口到缓存
            tmpBleedingWounds.Clear();
            List<Hediff> hediffs = Pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                // - TendableNow(false)：可包扎
                // - Bleeding：正在流血（BleedRate > 1E-05f，避免包扎不流血的慢性伤口）
                if (hediff.TendableNow(false) && hediff.Bleeding)
                {
                    tmpBleedingWounds.Add(hediff);
                }
            }

            if (tmpBleedingWounds.Count <= 0)
            {
                return;
            }

            // 按流血速度降序排序
            // BleedRate 是 Hediff 基类的 public virtual float 属性（Hediff.cs:217）
            List<Hediff> sorted = tmpBleedingWounds
                .OrderByDescending(h => h.BleedRate)
                .ToList();

            // 包扎流血最快的伤口
            // 调用原版 Tended 方法执行包扎
            // 参考 TendUtility.DoTend 第 29 行：hediff.Tended(quality, maxQuality, i)
            sorted[0].Tended(Props.tendQuality, Props.tendQualityMax, 0);
        }
    }
}
