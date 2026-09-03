using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 口香糖音效播放组件。
    ///
    /// 行为：
    ///   - hediff 存在期间，每 intervalTicks（默认 6000）尝试播放一次音效
    ///   - 随机选一条（由 SoundDef 的 AudioGrain_Folder 实现）
    ///   - 从 pawn 位置以 3D 空间音效播放（镜头靠近才听见，多 pawn 不会糊成一片）
    ///
    /// 防重叠机制（应对超高倍速）：
    ///   高倍速下 6000 tick 的真实时间可能小于音效时长（10s），
    ///   会导致前一个音效尚未播完就触发下一个，多重音效叠加。
    ///   解法：用 Time.realtimeSinceStartup（真实时间）判断上次播放是否已超过
    ///   soundDurationReal 秒，未超过则跳过本次触发。
    /// </summary>
    public class HediffComp_SoundPlayer : HediffComp
    {
        /// <summary>获取属性配置。</summary>
        public HediffCompProperties_SoundPlayer Props => (HediffCompProperties_SoundPlayer)props;

        /// <summary>上次播放音效的真实时间点（Time.realtimeSinceStartup）。
        /// 初始 SoundPlayerConfig.NeverPlayedRealTime 确保首次达到间隔 tick 时即可触发，无需等待。</summary>
        private float lastPlayRealTime = SoundPlayerConfig.NeverPlayedRealTime;

        /// <summary>
        /// 每 tick 调用：按间隔触发音效播放，带真实时间防重叠。
        /// </summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            // pawn 不在地图上时不触发（如远行队中）
            if (Pawn == null || Pawn.Map == null)
            {
                return;
            }

            // 未到间隔 tick 数则跳过（IsHashIntervalTick 按 pawn 的 hash 均匀分散）
            if (!Pawn.IsHashIntervalTick(Props.intervalTicks))
            {
                return;
            }

            // 防重叠：前一个事件尚未过完（真实时间）则跳过本次
            // 高倍速 mod 下 6000 tick 的真实时间可能 < 10s，此判断避免叠加
            if (Time.realtimeSinceStartup - lastPlayRealTime < Props.soundDurationReal)
            {
                return;
            }

            // === 触发老吴事件：音效 + 日志 ===

            // 音效（受设置开关控制；关声音时仍记录日志）
            // 玩家可在设置中关闭音效
            if (KiiroSchizonepetaGumMod.Settings.enableSoundEffect)
            {
                SoundInfo info = SoundInfo.InMap(new TargetInfo(Pawn), MaintenanceType.None);
                info.volumeFactor = KiiroSchizonepetaGumMod.Settings.soundVolume;
                Props.soundDef?.PlayOneShot(info);
            }

            // 日志：往人物日志（BattleLog）添加一条老吴记录
            // 文本由 RulePackDef 的 rulesStrings 随机选一条生成
            // 使用 BattleLogEntry_LaoWuScream 子类以支持自定义图标
            // 独立于音效开关
            if (Props.logEntryPack != null)
            {
                Find.BattleLog.Add(new BattleLogEntry_LaoWuScream(Pawn, Props.logEntryPack, Pawn, Props.logEntryIconPath));
            }

            // 打扰半径内睡觉的人物（无视墙体），给予"睡眠被打扰"想法
            DisturbNearbySleepers();

            lastPlayRealTime = Time.realtimeSinceStartup;
        }

        /// <summary>
        /// 打扰半径内睡觉的人物，给予"睡眠被打扰"想法（Core 的 SleepDisturbed）。
        /// 无视墙体（纯水平距离判断，不检查视线）。
        /// </summary>
        private void DisturbNearbySleepers()
        {
            if (Props.disturbRadius <= 0f)
            {
                return;
            }

            // 遍历地图上所有已生成的 pawn
            foreach (Pawn other in Pawn.Map.mapPawns.AllPawnsSpawned)
            {
                // 跳过自己
                if (other == Pawn)
                {
                    continue;
                }
                // 无视墙体，纯水平距离判断
                if (!other.Position.InHorDistOf(Pawn.Position, Props.disturbRadius))
                {
                    continue;
                }
                // 跳过醒着的（只打扰睡觉的）
                if (other.Awake())
                {
                    continue;
                }
                // 跳过无心情系统的（如动物、机械族）
                if (other.needs?.mood == null)
                {
                    continue;
                }
                // 跳过死眠中的（核心行为：死眠不被打扰）
                if (other.Deathresting)
                {
                    continue;
                }
                // 给予"睡眠被打扰"想法
                other.needs.mood.thoughts.memories.TryGainMemory(ThoughtDefOf.SleepDisturbed, null, null);
            }
        }
    }
}
