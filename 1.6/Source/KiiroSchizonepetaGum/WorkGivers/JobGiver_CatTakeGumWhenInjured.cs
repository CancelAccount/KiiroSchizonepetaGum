using RimWorld;
using Verse;
using Verse.AI;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 受伤的猫自动寻找并服用口香糖的 JobGiver。
    /// 通过 ThinkTreeDef 的 insertTag="Animal_PreMain" 注入到动物行为树。
    ///
    /// 猫没有中级训练度，因此用不了 WorkGiver。
    ///
    /// 触发条件：
    ///   1. pawn 是猫（Cat）
    ///   2. pawn 属于玩家阵营
    ///   3. pawn 有需要包扎的伤口（HasHediffsNeedingTend）
    ///   4. pawn 身上没有口香糖 hediff（避免重复服用）
    ///   5. 地图上有可到达的口香糖
    /// 其实这个寻找机制依旧无法让猫在部分情况下去主动找药（比如开启了复原残肢且猫有被毁器官无伤口时），但是考虑到猫的智商，就这样罢。
    /// 
    /// 参考：
    ///   - ThinkNode_JobGiver：基类，TryGiveJob 返回 Job
    ///   - JobGiver_PatientGoToBed：动物受伤后去床上休息的 JobGiver
    ///   - Animal.xml:98-100：ThinkNode_SubtreesByTag insertTag="Animal_PreMain"
    /// </summary>
    public class JobGiver_CatTakeGumWhenInjured : ThinkNode_JobGiver
    {
        /// <summary>猫的 ThingDef defName（见 Races_Animal_CatGroup.xml:6）。</summary>
        private const string CatDefName = "Cat";

        /// <summary>口香糖的 ThingDef defName（见 Drug_ChewingGum.xml）。</summary>
        private const string GumDefName = "Kiiro_SchizonepetaGum";

        /// <summary>口香糖 hediff 的 defName（见 Hediff_ChewingGum.xml）。</summary>
        private const string GumHediffDefName = "Kiiro_SchizonepetaGumHigh";

        /// <summary>口香糖 ThingDef 缓存（避免每次调用都查 DefDatabase）。</summary>
        private static ThingDef _gumDef;

        /// <summary>获取口香糖 ThingDef（首次访问时缓存）。</summary>
        private static ThingDef GumDef => _gumDef ??= ThingDef.Named(GumDefName);

        /// <summary>口香糖 hediff Def 缓存。</summary>
        private static HediffDef _gumHediffDef;

        /// <summary>获取口香糖 hediff Def（首次访问时缓存）。</summary>
        private static HediffDef GumHediffDef => _gumHediffDef ??= HediffDef.Named(GumHediffDefName);

        /// <summary>
        /// 尝试为猫生成"去拿口香糖并服用"的 Job。
        /// 所有条件满足才返回 Job，否则返回 null（行为树继续往下走）。
        /// </summary>
        /// <param name="pawn">执行 Job 的 pawn（应该是猫）。</param>
        /// <returns>Ingest Job 或 null。</returns>
        protected override Job TryGiveJob(Pawn pawn)
        {
            // 条件 1：必须是猫
            if (pawn.def.defName != CatDefName)
            {
                return null;
            }

            // 条件 2：必须是玩家阵营的（野基米有野人帮忙bushi）
            if (pawn.Faction != Faction.OfPlayer)
            {
                return null;
            }

            // 条件 3：必须有需要包扎的伤口
            // HasHediffsNeedingTend(false) = 检查是否有需要治疗的 hediff（非警报模式）
            if (pawn.health == null || !pawn.health.HasHediffsNeedingTend(false))
            {
                return null;
            }

            // 条件 4：身上不能已有口香糖 hediff（避免重复服用）
            if (pawn.health.hediffSet != null && pawn.health.hediffSet.HasHediff(GumHediffDef))
            {
                return null;
            }

            // 条件 5：地图上必须有可到达的口香糖
            // 用 GenClosest.ClosestThingReachable 代替遍历全部：
            //   - 内部用 BFS 区域搜索，从猫的位置向外扩展
            //   - 找到最近的满足条件的口香糖返回
            Thing gum2 = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(GumDef),
                PathEndMode.Touch,
                TraverseParms.For(pawn, pawn.NormalMaxDanger()),
                9999f,
                (Thing t) => pawn.CanReserve(t, 1, -1, null, false));

            if (gum2 == null)
            {
                return null;
            }

            // 创建 Ingest Job，系统会自动处理"走到→拿取→食用"流程
            Job job2 = JobMaker.MakeJob(JobDefOf.Ingest, gum2);
            job2.count = 1;
            return job2;
        }
    }
}
