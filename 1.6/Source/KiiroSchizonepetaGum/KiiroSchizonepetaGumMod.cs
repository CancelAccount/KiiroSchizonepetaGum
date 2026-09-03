using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

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
            listing.CheckboxLabeled(
                "KiiroSchizonepetaGum_HealMissingPartsLabel".Translate(),
                ref Settings.healMissingParts,
                "KiiroSchizonepetaGum_HealMissingPartsDesc".Translate()
            );

            // 复选框：是否开启视觉特效
            listing.CheckboxLabeled(
                "KiiroSchizonepetaGum_VisualEffectLabel".Translate(),
                ref Settings.enableVisualEffect,
                "KiiroSchizonepetaGum_VisualEffectDesc".Translate()
            );

            // 复选框：是否开启老吴音效
            listing.CheckboxLabeled(
                "KiiroSchizonepetaGum_SoundEffectLabel".Translate(),
                ref Settings.enableSoundEffect,
                "KiiroSchizonepetaGum_SoundEffectDesc".Translate()
            );

            // 滑块：音效音量（0~1），拖动调节最大音量
            // SliderLabeled(label, val, min, max, labelPct, tooltip) 返回滑动后的新值
            // ToStringPercent() 把小数转换为百分比字符串
            Settings.soundVolume = listing.SliderLabeled(
                "KiiroSchizonepetaGum_SoundVolumeLabel".Translate() + ": " + Settings.soundVolume.ToStringPercent(),
                Settings.soundVolume, 0f, 1f, 0.25f,
                "KiiroSchizonepetaGum_SoundVolumeDesc".Translate()
            );

            // 分隔空行后是描边设置区
            listing.Gap(12f);

            // 复选框：是否开启人物描边
            listing.CheckboxLabeled(
                "KiiroSchizonepetaGum_OutlineLabel".Translate(),
                ref Settings.enableOutline,
                "KiiroSchizonepetaGum_OutlineDesc".Translate()
            );

            // 滑条：描边宽度（贴图像素级，1~4）
            Settings.outlineWidth = listing.SliderLabeled(
                "KiiroSchizonepetaGum_OutlineWidthLabel".Translate() + ": " + Settings.outlineWidth.ToString("0.0"),
                Settings.outlineWidth, OutlineConfig.OutlineWidthMin, OutlineConfig.OutlineWidthMax, 0.25f,
                "KiiroSchizonepetaGum_OutlineWidthDesc".Translate()
            );

            // 色板：描边颜色预设
            DrawOutlineColorPalette(listing);

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        /// <summary>画描边颜色预设色板：一排色块，点击即切换描边颜色。
        /// 用预设色板而非自制取色器，保持设置界面简单。</summary>
        /// <param name="listing">当前设置界面的布局器。</param>
        private void DrawOutlineColorPalette(Listing_Standard listing)
        {
            listing.Label("KiiroSchizonepetaGum_OutlineColorLabel".Translate(),
                -1f, "KiiroSchizonepetaGum_OutlineColorDesc".Translate());

            Rect row = listing.GetRect(OutlineConfig.ColorSwatchHeight);
            Color[] presets = OutlineConfig.OutlineColorPresets;
            float cellWidth = row.width / presets.Length;
            for (int i = 0; i < presets.Length; i++)
            {
                // 每个预设色块占一行内的一格（留 2px 间隙区分相邻色块）
                Rect cell = new Rect(row.x + cellWidth * i, row.y, cellWidth - 2f, row.height);
                Widgets.DrawBoxSolid(cell, presets[i]);
                // 当前选中的颜色画白色边框标识
                if (presets[i] == Settings.outlineColor)
                {
                    Widgets.DrawBox(cell, 1);
                }
                if (Widgets.ButtonInvisible(cell, false))
                {
                    Settings.outlineColor = presets[i];
                    SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
            }
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
