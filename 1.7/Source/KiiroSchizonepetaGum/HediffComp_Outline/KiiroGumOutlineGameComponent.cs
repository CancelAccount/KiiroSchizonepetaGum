using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 描边渲染驱动组件。
    /// RimWorld 在创建/加载游戏时自动扫描 assembly 中所有 GameComponent 非抽象子类，
    /// 用 (Game) 构造函数反射实例化（Game.cs L452-467），无需任何手动注册。
    ///
    /// 绘制挂到 Application.onBeforeRender（Waylith 的 UnityWorld.PostLateUpdate 每帧触发，
    /// UnityStubs Application.InvokeBeforeRender），而不是 GameComponentUpdate：
    /// 前者每渲染帧一次、晚于 DynamicDrawManager 的 ParallelPreDraw（results 已算好当帧值），
    /// 后者只在逻辑 tick（60/s）触发，帧率高时会有大量帧没有描边、且读到的是上一帧参数。
    /// </summary>
    public class KiiroGumOutlineGameComponent : GameComponent
    {
        /// <summary>是否已订阅每帧渲染回调（静态，防止多次构造重复订阅）。</summary>
        private static bool beforeRenderHooked;

        /// <summary>构造函数（引擎反射调用并传入 Game 实例，签名必须保留；
        /// GameComponent 基类无显式构造函数，无需显式调用 base）。</summary>
        /// <param name="game">当前游戏实例（保留参数以匹配引擎的反射实例化）。</param>
        public KiiroGumOutlineGameComponent(Game game)
        {
            HookBeforeRender();
        }

        /// <summary>初始化完成时再次确保订阅（读档后 GameComponent 实例可能重建）。</summary>
        public override void FinalizeInit()
        {
            base.FinalizeInit();
            HookBeforeRender();
        }

        /// <summary>每逻辑 tick 调用：只刷新图集合成帧。
        /// 烘焙内部触发 PawnCacheCamera.Render()（嵌套渲染），不能放在渲染阶段的
        /// onBeforeRender 回调里；绘制提交由 HookBeforeRender 的每帧回调负责。</summary>
        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            KiiroGumOutlineManager.RefreshFrames();
        }

        /// <summary>把描边提交挂到每帧渲染前回调。
        /// 订阅后跨存档复用：RenderOutlines 内部对非游玩状态/无目标等情况已做早退。</summary>
        private static void HookBeforeRender()
        {
            if (beforeRenderHooked)
            {
                return;
            }
            beforeRenderHooked = true;
            Application.onBeforeRender += KiiroGumOutlineManager.RenderOutlines;
            KiiroGumLog.Debug(KiiroGumLogModule.Outline,
                "已订阅 Application.onBeforeRender（每渲染帧驱动 RenderOutlines）");
        }
    }
}
