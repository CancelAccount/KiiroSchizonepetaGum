namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 图标绘制模块（Graphic_StackCountByThreshold）配置：负责堆叠贴图分级的常量。
    /// 仅本模块（Graphic_StackCountByThreshold）使用。
    /// </summary>
    public static class GraphicConfig
    {
        /// <summary>堆叠图标分级：堆叠数 2~此值显示第 2 张贴图，超过显示第 3 张。
        /// 无 XML 来源：脚本内部参数。
        /// 关联 XML：Defs/Drugs/Drug_ChewingGum.xml → ThingDef/graphicData 使用 Graphic_StackCountByThreshold，
        /// 贴图由 Textures/Things/Item/Drug/SchizonepetaGum/ 下 3 张 png 提供。</summary>
        public const int MidStackThreshold = 50;
    }
}
