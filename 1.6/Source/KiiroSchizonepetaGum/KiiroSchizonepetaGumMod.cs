using RimWorld;
using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// Mod 主类：RimWorld 启动时会自动扫描 assembly 中所有 Mod 派生类并实例化。
    /// 负责注册 mod 设置界面、提供全局 Settings 访问点供 comp 运行时读取。
    /// </summary>
    public class KiiroSchizonepetaGumMod : Mod
    {
        /// <summary>全局设置实例（comp 运行时通过 KiiroSchizonepetaGumMod.Settings 读取配置）。
        /// private set 保证只有本类构造函数能赋值，外部只读。</summary>
        public static KiiroSchizonepetaGumSettings Settings { get; private set; }

        /// <summary>构造函数：RimWorld 加载 mod 时调用。
        /// base(contentPack) 必须调用以完成 Mod 基类初始化。
        /// GetSettings&lt;T&gt;() 是 Mod 基类的 protected 方法，返回 T 类型的设置实例。
        /// </summary>
        /// <param name="contentPack">本 mod 的内容包（RimWorld 自动传入）。</param>
        public KiiroSchizonepetaGumMod(ModContentPack contentPack) : base(contentPack)
        {
            Settings = GetSettings<KiiroSchizonepetaGumSettings>();
        }

        /// <summary>画设置界面：玩家在"选项 → Mod 设置"点击本 mod 时调用。
        /// 用 Listing_Standard 排列控件，符合 RimWorld 原版设置界面的视觉风格。
        /// </summary>
        /// <param name="inRect">RimWorld 分配给本 mod 的绘制区域。</param>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new();
            listing.Begin(inRect);

            // 复选框：是否允许修复断肢
            // CheckboxLabeled(label, ref checkOn, tooltip)
            // label 和 tooltip 用翻译 key，RimWorld 根据游戏语言加载对应翻译
            listing.CheckboxLabeled(
                "KiiroSchizonepetaGum_HealMissingPartsLabel".Translate(),
                ref Settings.healMissingParts,
                "KiiroSchizonepetaGum_HealMissingPartsDesc".Translate()
            );

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>设置窗口标题：显示在"Mod 设置"列表里本 mod 对应的按钮文字。
        /// 返回非空字符串才会出现在设置列表里；返回 null/空则不显示按钮。
        /// </summary>
        public override string SettingsCategory()
        {
            return "KiiroSchizonepetaGum_SettingsCategory".Translate();
        }
    }
}
