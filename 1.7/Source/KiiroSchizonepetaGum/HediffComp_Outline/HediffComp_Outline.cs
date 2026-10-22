using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 口香糖描边特效组件：只负责把宿主 pawn 注册/注销到描边管理器（KiiroGumOutlineManager）。
    /// 实际绘制由 Manager 每帧统一执行（GameComponentUpdate 阶段，见 KiiroGumOutlineGameComponent）。
    /// </summary>
    public class HediffComp_Outline : HediffComp
    {
        /// <summary>获取属性配置（描边颜色/宽度/孔洞半径的 def 级默认值）。</summary>
        public HediffCompProperties_Outline Props => (HediffCompProperties_Outline)props;

        /// <summary>hediff 创建时调用（新建 + 存档恢复两条路径都会走到）：注册到描边管理器。</summary>
        public override void CompPostMake()
        {
            base.CompPostMake();
            KiiroGumOutlineManager.Register(this);
        }

        /// <summary>每 tick 调用：复活兜底重新注册。
        /// 死亡时（Notify_PawnDied）已注销，而复活不会重建 hediff 对象、CompPostMake
        /// 不会再次调用——此处检测到未注册则补注册。
        /// 性能：尸体期间 pawn 不 tick（Corpse 不驱动 InnerPawn.Tick），本方法不执行，
        /// 死亡状态零开销；存活期间仅一次小列表 Contains 检查（目标数通常小于50）。</summary>
        /// <param name="severityAdjustment">父级 hediff 的 severity 修正量（透传给基类）。</param>
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (!KiiroGumOutlineManager.IsRegistered(this))
            {
                KiiroGumOutlineManager.Register(this);
            }
        }

        /// <summary>宿主死亡时调用（官方 HediffSet.Notify_PawnDied 传播，L398-404）：
        /// 立即注销，避免 Manager 每帧为死亡目标做空校验。
        /// 复活后由 CompPostTick 兜底重新注册（若 hediff 仍在）。</summary>
        /// <param name="dinfo">致死伤害信息。</param>
        /// <param name="culprit">致死的 hediff（可为 null）。</param>
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            KiiroGumOutlineManager.Unregister(this);
        }

        /// <summary>hediff 被移除（药效消退/治愈/清除）时调用：从描边管理器注销。</summary>
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            KiiroGumOutlineManager.Unregister(this);
        }
    }
}
