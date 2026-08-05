using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 带自定义图标的老吴日志条目。
    ///
    /// 继承 BattleLogEntry_Event，override IconFromPOV 返回自定义贴图。
    /// 基类 LogEntry.IconFromPOV 默认返回 null（无图标），本子类改为返回配置的贴图。
    ///
    /// 序列化：iconPath 通过 Scribe_Values 保存，存档加载后图标仍能正确显示。
    /// </summary>
    public class BattleLogEntry_LaoWuScream : BattleLogEntry_Event
    {
        /// <summary>图标贴图路径（相对 Textures/ 目录）。序列化保存。</summary>
        private string iconPath;

        /// <summary>无参构造函数，供 RimWorld 反序列化使用（必须存在，否则存档加载失败）。</summary>
        public BattleLogEntry_LaoWuScream() : base()
        {
        }

        /// <summary>运行时构造：创建带图标的老吴日志条目。</summary>
        /// <param name="subject">主体（老吴）</param>
        /// <param name="eventDef">事件 RulePackDef（生成日志文本）</param>
        /// <param name="initiator">发起者</param>
        /// <param name="iconPath">图标贴图路径（相对 Textures/，不带扩展名）</param>
        public BattleLogEntry_LaoWuScream(Thing subject, RulePackDef eventDef, Thing initiator, string iconPath)
            : base(subject, eventDef, initiator)
        {
            this.iconPath = iconPath;
        }

        /// <summary>返回图标贴图。
        /// override 基类 LogEntry.IconFromPOV 的 null 默认值。
        /// 贴图路径为空或贴图缺失时返回 null（无图标，不报错）。</summary>
        public override Texture2D IconFromPOV(Thing pov)
        {
            if (iconPath.NullOrEmpty())
            {
                return null;
            }
            return ContentFinder<Texture2D>.Get(iconPath, reportFailure: false);
        }

        /// <summary>序列化：保存/加载 iconPath，保证存档加载后图标仍在。</summary>
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref iconPath, "iconPath");
        }
    }
}
