using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 描边渲染驱动组件。
    /// RimWorld 在创建/加载游戏时自动扫描 assembly 中所有 GameComponent 非抽象子类，
    /// 用 (Game) 构造函数反射实例化（Game.cs L452-467），无需任何手动注册。
    /// GameComponentUpdate 由 Game.UpdatePlay 每帧调用（游戏逻辑阶段，早于相机渲染），
    /// 在此处把描边 mesh 提交进当前帧渲染队列。
    /// </summary>
    public class KiiroGumOutlineGameComponent : GameComponent
    {
        /// <summary>调试标志：GameComponentUpdate 首次被调用的日志只输出一次（防每帧刷屏）。</summary>
        private static bool updateLogged;

        /// <summary>构造函数（引擎反射调用并传入 Game 实例，签名必须保留；
        /// GameComponent 基类无显式构造函数，无需显式调用 base）。</summary>
        /// <param name="game">当前游戏实例（保留参数以匹配引擎的反射实例化）。</param>
        public KiiroGumOutlineGameComponent(Game game)
        {
        }

        /// <summary>每帧调用：驱动描边管理器渲染。
        /// 位于游戏逻辑阶段、相机渲染之前，描边 mesh 经 Graphics.DrawMesh 入队后
        /// 由相机渲染时统一绘制，与 pawn 本体同帧分层覆盖。</summary>
        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            // 调试日志：确认引擎确实在每帧驱动本组件（排查 GameComponent 未实例化的情况）
            if (OutlineConfig.DebugLogging && !updateLogged)
            {
                updateLogged = true;
                Log.Message("[KiiroGumOutline] GameComponentUpdate 已开始每帧驱动 RenderOutlines");
            }
            KiiroGumOutlineManager.RenderOutlines();
        }
    }
}
