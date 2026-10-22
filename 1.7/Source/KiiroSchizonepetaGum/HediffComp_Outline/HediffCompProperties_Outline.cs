using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 描边组件的 XML 配置类。
    /// 挂在 HediffDef 的 comps 上，提供描边样式的 def 级默认值（可被其他 mod 的 XML patch 覆盖）。
    /// 玩家在 mod 设置中的颜色/宽度设置优先于 def 级默认值。
    /// </summary>
    public class HediffCompProperties_Outline : HediffCompProperties
    {
        /// <summary>描边颜色（def 级默认值，玩家设置优先）。</summary>
        public Color outlineColor = OutlineConfig.OutlineColorDefault;

        /// <summary>描边宽度（图集 texel 单位，def 级默认值，玩家设置优先）。</summary>
        public float outlineWidth = OutlineConfig.OutlineWidthDefault;

        /// <summary>内部孔洞填充半径（图集 texel 单位）。
        /// 发丝缝隙等宽 ≤ 2×(outlineWidth+holeRadius) 的内部细缝会被描边色填充，不产生独立描边线。</summary>
        public float holeRadius = 1f;

        /// <summary>构造函数：指定 comp 实现类，RimWorld 加载 XML 后按此类型实例化组件。</summary>
        public HediffCompProperties_Outline()
        {
            compClass = typeof(HediffComp_Outline);
        }
    }
}
