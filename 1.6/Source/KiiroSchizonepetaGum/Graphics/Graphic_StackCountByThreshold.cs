using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 按堆叠数量阈值分级显示贴图的自定义 Graphic。
    /// 和原版展示机制一直，本类只是改变了堆叠时的数量阈值。
    /// 贴图加载（由 Graphic_Collection.Init 处理）：
    ///   GetAllInFolder(texPath) 搜索文件夹下所有贴图，
    ///   按文件名排序填充 subGraphics 数组。
    ///   例如 SchizonepetaGum_1.png / _2.png / _3.png → subGraphics[0..2]。
    /// </summary>
    public class Graphic_StackCountByThreshold : Graphic_StackCount
    {
        /// <summary>
        /// 根据物品堆叠数量选择对应的子贴图。
        /// 重写原版逻辑，使 3 贴图模式下按 1 / 2~GraphicConfig.MidStackThreshold / 超过分级。
        /// 非 3 贴图模式回退原版逻辑。
        /// </summary>
        /// <param name="thing">要绘制的物品（可为 null）。</param>
        /// <returns>对应堆叠数量的子贴图。</returns>
        public override Graphic SubGraphicFor(Thing thing)
        {
            // thing 为 null 时（理论上不会发生，防御性处理）回退首张贴图
            if (thing == null)
            {
                return this.subGraphics[0];
            }

            // 仅在 3 贴图模式下应用自定义阈值
            if (this.subGraphics.Length == 3)
            {
                if (thing.stackCount <= 1)
                {
                    // 单个：第1张贴图
                    return this.subGraphics[0];
                }
                if (thing.stackCount <= GraphicConfig.MidStackThreshold)
                {
                    // 少量(2~MidStackThreshold)：第2张贴图
                    return this.subGraphics[1];
                }
                // 大量(MidStackThreshold+1)：第3张贴图
                return this.subGraphics[2];
            }

            // 非 3 贴图模式回退原版逻辑
            return base.SubGraphicFor(thing);
        }
    }
}
