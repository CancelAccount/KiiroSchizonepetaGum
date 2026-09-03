using RimWorld;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 口香糖心情加成的 ThoughtWorker（situational thought）。
    /// 参考绮罗族原 mod 的 ThoughtWorker_CatmintCenser 模式：
    /// 通过检查 pawn 身上是否有口香糖 hediff 来决定是否激活，
    /// </summary>
    public class ThoughtWorker_AteSchizonepetaGum : ThoughtWorker
    {
        /// <summary>
        /// 当前状态：检查 pawn 是否有口香糖 hediff，并根据种族选择 stage。
        /// stage 0：其他种族（baseMoodEffect = 3）
        /// stage 1：绮罗族（baseMoodEffect = 8）
        /// </summary>
        /// <param name="p">要检查的 pawn。</param>
        /// <returns>ThoughtState.ActiveAt(stageIndex) 或 false。</returns>
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            // 安全检查：pawn、health、hediffSet 任一为 null 则不激活
            if (p?.health?.hediffSet == null)
            {
                return false;
            }

            // 检查是否有口香糖 hediff（DefDatabase 缓存查找）
            HediffDef gumHediff = HediffDef.Named(Config.GumHediffDefName);
            if (!p.health.hediffSet.HasHediff(gumHediff))
            {
                return false;
            }

            // 绮罗族用 stage 1（+8），其他种族用 stage 0（+3）
            // 绮罗族 ThingDef defName 见 Config.KiiroRaceDefName（AlienRace 框架定义）
            if (p.def.defName == Config.KiiroRaceDefName)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            return ThoughtState.ActiveAtStage(0);
        }
    }
}
