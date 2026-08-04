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
    ///   - hediff 存在期间，每 intervalTicks（默认 3600）尝试播放一次音效
    ///   - 随机选一条（由 SoundDef 的 AudioGrain_Folder 实现）
    ///   - 从 pawn 位置以 3D 空间音效播放（镜头靠近才听见，多 pawn 不会糊成一片）
    ///
    /// 防重叠机制（应对超高倍速）：
    ///   高倍速下 3600 tick 的真实时间可能小于音效时长（10s），
    ///   会导致前一个音效尚未播完就触发下一个，多重音效叠加。
    ///   解法：用 Time.realtimeSinceStartup（真实时间）判断上次播放是否已超过
    ///   soundDurationReal 秒，未超过则跳过本次触发。
    /// </summary>
    public class HediffComp_SoundPlayer : HediffComp
    {
        /// <summary>获取属性配置。</summary>
        public HediffCompProperties_SoundPlayer Props => (HediffCompProperties_SoundPlayer)props;

        /// <summary>上次播放音效的真实时间点（Time.realtimeSinceStartup）。
        /// 初始 -100f 确保首次达到间隔 tick 时即可触发，无需等待。</summary>
        private float lastPlayRealTime = -100f;

        /// <summary>
        /// 每 tick 调用：按间隔触发音效播放，带真实时间防重叠。
        /// </summary>
        public override void CompPostTick(ref float severityAdjustment)
        {
            // 玩家可在设置中关闭音效(老吴老吴老吴老吴)
            if (!KiiroSchizonepetaGumMod.Settings.enableSoundEffect)
            {
                return;
            }

            // pawn 不在地图上时不播放（如远行队中）
            if (Pawn == null || Pawn.Map == null)
            {
                return;
            }

            // 未到间隔 tick 数则跳过（IsHashIntervalTick 按 pawn 的 hash 均匀分散）
            if (!Pawn.IsHashIntervalTick(Props.intervalTicks))
            {
                return;
            }

            // 防重叠：前一个音效尚未播完（真实时间）则跳过本次
            // 高倍速 mod 下 3600 tick 的真实时间可能 < 10s，此判断避免叠加播放
            if (Time.realtimeSinceStartup - lastPlayRealTime < Props.soundDurationReal)
            {
                return;
            }

            // 触发音效：从 pawn 位置播放（3D 空间音效）
            // volumeFactor 应用玩家设置的音量系数（0~1），控制最大音量
            SoundInfo info = SoundInfo.InMap(new TargetInfo(Pawn), MaintenanceType.None);
            info.volumeFactor = KiiroSchizonepetaGumMod.Settings.soundVolume;
            Props.soundDef?.PlayOneShot(info);
            lastPlayRealTime = Time.realtimeSinceStartup;
        }
    }
}
