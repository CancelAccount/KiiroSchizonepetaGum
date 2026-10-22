using RimWorld;
using Verse;
using Verse.AI;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 受伤的绮罗族自动去拿口香糖服用的 WorkGiver。
    /// 工作类型为 PatientBedRest（修养）。
    ///
    /// 触发条件：
    ///   1. pawn 是绮罗族（Kiiro_Race）
    ///   2. pawn 有需要包扎的伤口（HasHediffsNeedingTend）
    ///   3. pawn 身上没有口香糖 hediff（避免重复服用）
    ///   4. 地图上有可到达的口香糖
    ///
    /// 参考：
    ///   - WorkGiver_Scanner：基类，搜索物品模式
    ///   - WorkGiver_PatientGoToBedRecuperate：同 workType（PatientBedRest）
    ///   - DrugAIUtility.IngestAndTakeToInventoryJob：生成 Ingest Job 的写法
    /// </summary>
    public class WorkGiver_TakeGumWhenInjured : WorkGiver_Scanner
    {
        /// <summary>口香糖 ThingDef 缓存（避免每次调用都查 DefDatabase）。</summary>
        private static ThingDef _gumDef;

        /// <summary>获取口香糖 ThingDef（首次访问时缓存）。</summary>
        private static ThingDef GumDef => _gumDef ??= ThingDef.Named(Config.GumDefName);

        /// <summary>口香糖 hediff Def 缓存。</summary>
        private static HediffDef _gumHediffDef;

        /// <summary>获取口香糖 hediff Def（首次访问时缓存）。</summary>
        private static HediffDef GumHediffDef => _gumHediffDef ??= HediffDef.Named(Config.GumHediffDefName);

        /// <summary>
        /// 工作物品请求：搜索地图上的口香糖。
        /// 系统会自动找到所有匹配的 Thing，然后对每个调用 HasJobOnThing/JobOnThing。
        /// </summary>
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForDef(GumDef);

        /// <summary>
        /// 检查 pawn 是否可以对指定物品执行工作（拿口香糖服用）。
        /// 所有条件都满足才返回 true。
        /// </summary>
        /// <param name="pawn">执行工作的 pawn。</param>
        /// <param name="t">目标物品（应该是口香糖）。</param>
        /// <param name="forced">是否强制执行（玩家手动指定）。</param>
        /// <returns>是否可以执行工作。</returns>
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 条件 1：物品必须是口香糖
            if (t.def != GumDef)
            {
                return false;
            }

            // 条件 2：pawn 必须是绮罗族
            if (pawn.def.defName != Config.KiiroRaceDefName)
            {
                return false;
            }

            // 条件 3：pawn 必须有需要包扎的伤口
            // HasHediffsNeedingTend(false) = 检查是否有需要治疗的 hediff（非警报模式）
            if (pawn.health == null || !pawn.health.HasHediffsNeedingTend(false))
            {
                return false;
            }

            // 条件 4：pawn 身上不能已有口香糖 hediff（避免重复服用）
            if (pawn.health.hediffSet != null && pawn.health.hediffSet.HasHediff(GumHediffDef))
            {
                return false;
            }

            // 条件 5：pawn 必须能到达并预留物品
            // CanReserveAndReach 检查路径可达性和预留机制（避免多个 pawn 争抢同一物品）
            // NormalMaxDanger() 返回 pawn 可接受的正常危险等级（参考 WorkGiver_CookFillHopper.cs:78）
            if (!pawn.CanReserveAndReach(t, PathEndMode.Touch, pawn.NormalMaxDanger(), 1, -1, null, false))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 生成"去拿口香糖并服用"的 Job。
        /// 使用原版 Ingest JobDef，系统会自动处理"走到物品旁→拿取→食用"的完整流程。
        /// 参考 DrugAIUtility.IngestAndTakeToInventoryJob 第 14 行的写法。
        /// </summary>
        /// <param name="pawn">执行工作的 pawn。</param>
        /// <param name="t">目标物品（口香糖）。</param>
        /// <param name="forced">是否强制执行。</param>
        /// <returns>Ingest Job 或 null。</returns>
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            // 创建 Ingest Job，targetA = 口香糖
            // JobDefOf.Ingest 是原版的"食用"JobDef，会自动处理食用流程
            Job job = JobMaker.MakeJob(JobDefOf.Ingest, t);
            job.count = 1;
            return job;
        }
    }
}
