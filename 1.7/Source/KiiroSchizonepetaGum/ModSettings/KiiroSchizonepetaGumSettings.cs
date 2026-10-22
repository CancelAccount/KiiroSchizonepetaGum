using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// Mod 设置数据存储类。
    /// 继承 ModSettings 后，RimWorld 会自动把实例序列化到
    /// %APPDATA%\..\Ludeon Studios\RimWorld by Ludeon Studios\ModSettings\Cancelation.KiiroSchizonepetaGum.xml，
    /// 玩家在"选项 → Mod 设置"界面改的值会持久化。
    /// </summary>
    public class KiiroSchizonepetaGumSettings : ModSettings
    {
        /// <summary>是否允许口香糖修复缺失部位（断肢）。
        /// 默认 false（只治伤口，不断肢重生）。
        /// 开启后治愈伤口剩余的再生量会修复一个 MissingPart。
        /// </summary>
        public bool healMissingParts = false;

        /// <summary>是否开启治疗特效。 </summary>
        public bool enableVisualEffect = true;

        /// <summary>是否开启老吴音效。</summary>
        public bool enableSoundEffect = true;

        /// <summary>音效音量系数（0~1）。</summary>
        public float soundVolume = 0.5f;

        /// <summary>是否开启人物外描边特效。</summary>
        public bool enableOutline = true;

        /// <summary>描边颜色（玩家设置，优先于 def 级默认值）。</summary>
        public Color outlineColor = OutlineConfig.OutlineColorDefault;

        /// <summary>描边宽度（图集 texel 单位，1~4，玩家设置，优先于 def 级默认值）。</summary>
        public float outlineWidth = OutlineConfig.OutlineWidthDefault;

        /// <summary>RimWorld 序列化钩子：保存/读取设置数据。
        /// 必须调用 base.ExposeData() 以保证基类数据也被序列化。
        /// 第三个参数 false 是默认值（当存档里找不到该 key 时使用）。
        /// </summary>
        public override void ExposeData()
        {
            Scribe_Values.Look(ref healMissingParts, "healMissingParts", false);
            Scribe_Values.Look(ref enableVisualEffect, "enableVisualEffect", true);
            Scribe_Values.Look(ref enableSoundEffect, "enableSoundEffect", true);
            Scribe_Values.Look(ref soundVolume, "soundVolume", 0.25f);
            Scribe_Values.Look(ref enableOutline, "enableOutline", true);
            Scribe_Values.Look(ref outlineColor, "outlineColor", OutlineConfig.OutlineColorDefault);
            Scribe_Values.Look(ref outlineWidth, "outlineWidth", OutlineConfig.OutlineWidthDefault);
            base.ExposeData();
        }
    }
}
