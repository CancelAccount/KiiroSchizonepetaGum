using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 复刻原版 Anomaly 的 regeneration 机制（见 Pawn_HealthTracker.HealthTick 第1250-1293行），
    /// </summary>
    public class HediffComp_SlowRegen : HediffComp
    {
        /// <summary>复用列表，避免每次 tick 都 new 一个 List（参考原版 tmpHediffInjuries 写法）。</summary>
        private List<Hediff_Injury> tmpInjuries = new();

        /// <summary>复用列表，用于缺失部位修复阶段。</summary>
        private List<Hediff_MissingPart> tmpMissingParts = new();

        /// <summary>获取属性配置（把基类 props 转成本 comp 的属性类型）。</summary>
        public HediffCompProperties_SlowRegen Props => (HediffCompProperties_SlowRegen)props;

        /// <summary>
        /// 鼠标悬停在 hediff 上时，tooltip 底部显示再生速度。
        /// 参考 HediffComp_TendDuration.CompTipStringExtra 的写法。
        /// </summary>
        public override string CompTipStringExtra =>
            "KiiroSchizonepetaGum_RegenRate".Translate(Props.healAmountPerDay);

        /// <summary>
        /// 每 tick 调用。每 15 tick 执行一次再生（与原版间隔一致）。
        /// </summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn.IsHashIntervalTick(SlowRegenConfig.SlowRegenIntervalTicks))
            {
                TryRegenerate();
            }
        }

        /// <summary>执行一次再生治疗：先治伤口，剩余量可选修复断肢。</summary>
        private void TryRegenerate()
        {
            // 安全检查，如果没有伤口或缺失部位，或者Pawn死亡，直接返回
            if (Pawn == null || Pawn.Dead || Pawn.health == null || Pawn.health.hediffSet == null)
            {
                return;
            }

            // 计算本次可用再生量
            // 原版系数 0.00025（见 SlowRegenConfig.RegenHealFactorPerTick）：
            // 每 SlowRegenConfig.SlowRegenIntervalTicks tick 触发一次，
            // healAmountPerDay=N 对应每天恢复 N hp
            float remaining = Props.healAmountPerDay * SlowRegenConfig.RegenHealFactorPerTick;
            if (remaining <= 0f)
            {
                return;
            }

            // 阶段一：治愈伤口（Hediff_Injury）
            HealInjuries(ref remaining);

            // 阶段二：剩余量用于修复缺失部位（可选，默认关闭）
            // 由 mod 设置界面控制（KiiroSchizonepetaGumMod.Settings.healMissingParts）
            // null 检查防御 mod 未初始化的极端情况（实际不会发生，Mod 在游戏启动时就构造了）
            if (KiiroSchizonepetaGumMod.Settings != null && KiiroSchizonepetaGumMod.Settings.healMissingParts && remaining > 0f)
            {
                HealMissingPart();
            }
        }

        /// <summary>依次治愈伤口，直到再生量耗尽（对应Core第1263-1274行）。</summary>
        /// <param name="remaining">本次剩余的再生量（引用传递，函数内会扣减）。</param>
        private void HealInjuries(ref float remaining)
        {
            tmpInjuries.Clear();
            // 获取所有伤口（predicate 恒 true，和原版一致）
            Pawn.health.hediffSet.GetHediffs<Hediff_Injury>(ref tmpInjuries, (Hediff_Injury h) => true);

            foreach (Hediff_Injury injury in tmpInjuries)
            {
                if (remaining <= 0f)
                {
                    break;
                }
                // 治疗量不超过伤口当前严重度，也不超过剩余再生量
                float healAmount = Mathf.Min(remaining, injury.Severity);
                remaining -= healAmount;
                injury.Heal(healAmount);
                // 通知系统已再生（用于显示/UI 更新）
                Pawn.health.hediffSet.Notify_Regenerated(healAmount);
            }
        }

        /// <summary>修复一个缺失部位（对应原版第1277-1290行，扩展支持外星种族躯干缺失情况）。</summary>
        private void HealMissingPart()
        {
            tmpMissingParts.Clear();
            // 筛选可修复的 MissingPart：
            // ── 原版假设（躯干缺失 = 必死亡，故过滤 parent==null）：
            //    h.Part.parent != null
            //    && 父部位无伤口 / 父部位不缺 / 父部位非义肢
            // ── 扩展（外星种族 / 机械体 / 特殊种族可能躯干缺失却存活）：
            //    当 h.Part.parent == null（根节点，通常是躯干）时：
            //    跳过父部位相关检查（没有父部位），仅保留自身类型检查即可
            Pawn.health.hediffSet.GetHediffs<Hediff_MissingPart>(ref tmpMissingParts,
                (Hediff_MissingPart h) =>
                {
                    BodyPartRecord parent = h.Part.parent;
                    if (parent == null)
                    {
                        // 根节点缺失（如躯干）：无父部位可查，直接视为候选（种族既然能活着说明允许）
                        return true;
                    }
                    // 非根节点：走原版父部位约束链
                    return !tmpInjuries.Any((Hediff_Injury x) => x.Part == parent)
                        && Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_MissingPart>(parent) == null
                        && Pawn.health.hediffSet.GetFirstHediffMatchingPart<Hediff_AddedPart>(parent) == null;
                });

            if (tmpMissingParts.Count <= 0)
            {
                return;
            }
            // 修复检测到的第一个缺失部位
            Hediff_MissingPart missingPart = tmpMissingParts[0];
            BodyPartRecord part = missingPart.Part;
            // 移除缺失部位 hediff
            Pawn.health.RemoveHediff(missingPart);
            // 添加一个 Misc hediff 表示该部位正在恢复（原版做法）
            Hediff newHediff = Pawn.health.AddHediff(HediffDefOf.Misc, part, null, null);
            // 设置严重度：取「当前血量-1」或「当前血量*0.9」的较大值（保留一点伤害）
            float partHealth = Pawn.health.hediffSet.GetPartHealth(part);
            newHediff.Severity = Mathf.Max(partHealth - 1f, partHealth * 0.9f);
            Pawn.health.hediffSet.Notify_Regenerated(partHealth - newHediff.Severity);
        }
    }
}
