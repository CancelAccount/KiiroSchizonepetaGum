using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 描边渲染管理器（静态）：
    /// - 维护"需要描边的 pawn"注册表（由 HediffComp_Outline 生命周期驱动增删）
    /// - 合成帧刷新由 KiiroGumOutlineGameComponent.GameComponentUpdate（每逻辑 tick）驱动，
    ///   绘制提交由 Application.onBeforeRender（每渲染帧）驱动：
    ///   对每个目标 pawn 取官方 atlas 合成帧（身体+衣物+发型+头饰的烘焙结果），
    ///   在 pawn 本体上方一层提交一次描边 mesh 绘制（Graphics.DrawMesh 入队，相机渲染时绘制）
    ///
    /// 设计要点（均已对照 1.6 反编译源码核实，引用行号见各处注释）：
    /// - 官方 atlas 缓存路径仅在镜头较远时生效（PawnRenderer L439：ZoomRootSize > 18，
    ///   值越大镜头越远；近景官方走渲染树高清绘制）。本模块不设缩放门槛：
    ///   帧新鲜度由 SetAllGraphicsDirty → TryMarkPawnFrameSetDirty 无条件标记（L834-847，
    ///   与缩放无关），配合 EnsureFrameFresh 主动烘焙，近景/远景描边均与本体一致
    /// - 非类人 pawn（本 mod 的猫，Humanlike=false）官方不烘焙、也不走 atlas 缓存路径，
    ///   但 PawnTextureAtlas.TryGetFrameSet 对 pawn 类型零限制（PawnTextureAtlas.cs L72），
    ///   PawnCacheRenderer.RenderPawn → RenderCache 对任何 pawn 都走同一渲染树（PawnRenderer.cs L519），
    ///   因此脏帧时由本模块主动烘焙（复制官方GetBlitMeshUpdatedFrame L231-242），即可获得与地图渲染一致的合成帧
    /// - 描边层位置/旋转/朝向取自 PawnRenderer 的预渲染结果（bodyPos / bodyAngle / parms.facing），
    ///   与官方 atlas 绘制路径（PawnRenderer L321-L326）完全一致，因此动画类 mod 的
    ///   posOffset / angleOffset / rotOverride 修正会自动跟随；读取失败时回退 pawn.DrawPos + identity
    /// - 描边层高度 = bodyPos + AltitudeForLayer(1)，略高于本体所在的 0 层；
    ///   描边带不与人物像素重叠（shader 采样到人物像素即输出全透明），无需沉到本体下方
    /// - shader 参数经 MaterialPropertyBlock 按次提交，不污染 MaterialPool 缓存的共享材质
    ///
    /// StaticConstructorOnStartup：静态字段初始化用到了 MaterialPropertyBlock、
    /// Shader.PropertyToID 等 Unity API，必须保证在主线程执行——该属性让 RimWorld 在
    /// 游戏加载完成时主动触发静态构造（StaticConstructorOnStartupUtility）。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class KiiroGumOutlineManager
    {
        /// <summary>描边层位置/旋转/帧朝向的探测：读取 PawnRenderer 的私有预渲染结果。
        /// 官方在渲染阶段把「本帧实际用于绘制的 bodyPos / bodyAngle / facing」写入
        /// PawnRenderer.results（1.7 反编译 PawnRenderer.cs L948/L954-L979，写入点 L402，
        /// atlas 绘制路径 L321-L326 直接使用这三个值）。
        /// 动画类 mod（如 Yayo's Animation）通过 Harmony 修改 GetBodyPos（追加 posOffset）、
        /// BodyAngle（追加 angleOffset）、ParallelGetPreRenderResults 的 rotOverride（固定朝向），
        /// 最终结果都落进这里——因此读它即可跟随任意动画 mod，无需硬依赖具体 mod 的程序集。
        ///
        /// 用表达式树编译成强类型委托：读取发生在每目标每帧，若改用 FieldInfo.GetValue，
        /// 每帧都会装箱整个 PreRenderResults（struct）并产生 GC 压力。
        /// 探测失败（字段改名/未来版本结构变化）时自动回退到 pawn.DrawPos + identity。
        /// </summary>
        private static class RenderResultProbe
        {
            /// <summary>是否已尝试过解析（含失败，失败后不再重试）。</summary>
            private static bool resolveAttempted;

            /// <summary>results.valid：预渲染结果是否有效（首次绘制前为 false）。</summary>
            private static Func<PawnRenderer, bool> getValid;

            /// <summary>results.bodyPos：本帧 body 绘制位置（已含动画 mod 的位移修正）。</summary>
            private static Func<PawnRenderer, Vector3> getBodyPos;

            /// <summary>results.bodyAngle：本帧 body 旋转角（已含动画 mod 的角度修正）。</summary>
            private static Func<PawnRenderer, float> getBodyAngle;

            /// <summary>results.parms.facing：本帧实际使用的朝向（已含 rotOverride）。</summary>
            private static Func<PawnRenderer, Rot4> getFacing;

            /// <summary>探针是否可用（供诊断日志区分「读到动画参数」与「回退旧行为」）。</summary>
            public static bool IsReady
            {
                get
                {
                    Resolve();
                    return getValid != null;
                }
            }

            /// <summary>读取本帧渲染参数。失败或不适用时返回 false，由调用方回退默认值。</summary>
            /// <param name="renderer">目标 pawn 的渲染器。</param>
            /// <param name="bodyPos">输出：body 绘制位置。</param>
            /// <param name="bodyAngle">输出：body 旋转角（度）。</param>
            /// <param name="facing">输出：实际朝向。</param>
            public static bool TryGet(PawnRenderer renderer, out Vector3 bodyPos, out float bodyAngle, out Rot4 facing)
            {
                bodyPos = default(Vector3);
                bodyAngle = 0f;
                facing = Rot4.South;
                Resolve();
                if (getValid == null || renderer == null)
                {
                    return false;
                }
                if (!getValid(renderer))
                {
                    return false;
                }
                bodyPos = getBodyPos(renderer);
                bodyAngle = getBodyAngle(renderer);
                facing = getFacing(renderer);
                return true;
            }

            /// <summary>解析私有字段并编译读取委托（进程内仅执行一次）。</summary>
            private static void Resolve()
            {
                if (resolveAttempted)
                {
                    return;
                }
                resolveAttempted = true;
                try
                {
                    FieldInfo resultsField = typeof(PawnRenderer).GetField(
                        "results", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (resultsField == null)
                    {
                        LogFallback("PawnRenderer.results 字段不存在");
                        return;
                    }
                    Type resultsType = resultsField.FieldType;
                    FieldInfo validField = resultsType.GetField("valid");
                    FieldInfo bodyPosField = resultsType.GetField("bodyPos");
                    FieldInfo bodyAngleField = resultsType.GetField("bodyAngle");
                    FieldInfo parmsField = resultsType.GetField("parms");
                    FieldInfo facingField = typeof(PawnDrawParms).GetField("facing");
                    if (validField == null || bodyPosField == null || bodyAngleField == null
                        || parmsField == null || facingField == null)
                    {
                        LogFallback("PreRenderResults / PawnDrawParms 字段结构变化");
                        return;
                    }
                    // 编译读取委托
                    ParameterExpression rendererParam = Expression.Parameter(typeof(PawnRenderer), "renderer");
                    Expression resultsExpr = Expression.Field(rendererParam, resultsField);
                    getValid = Expression.Lambda<Func<PawnRenderer, bool>>(
                        Expression.Field(resultsExpr, validField), rendererParam).Compile();
                    getBodyPos = Expression.Lambda<Func<PawnRenderer, Vector3>>(
                        Expression.Field(resultsExpr, bodyPosField), rendererParam).Compile();
                    getBodyAngle = Expression.Lambda<Func<PawnRenderer, float>>(
                        Expression.Field(resultsExpr, bodyAngleField), rendererParam).Compile();
                    getFacing = Expression.Lambda<Func<PawnRenderer, Rot4>>(
                        Expression.Field(Expression.Field(resultsExpr, parmsField), facingField),
                        rendererParam).Compile();
                }
                catch (Exception e)
                {
                    LogFallback(e.Message);
                }
            }

            /// <summary>记录回退原因（一次性）。</summary>
            /// <param name="reason">失败原因。</param>
            private static void LogFallback(string reason)
            {
                KiiroGumLog.Warn(KiiroGumLogModule.Outline, "无法读取 PawnRenderer 预渲染结果（" + reason
                    + "），描边将不跟随动画 mod 的位移/旋转");
            }
        }

        /// <summary>动画 mod 绘制数据探测：直接读取 Yayo's Animation 挂在 pawn 上的
        /// PawnDrawData（posOffset / angleOffset / fixedRot），等价复现它对 PawnRenderer 的三处注入。
        ///
        /// 为什么需要它：官方在 RenderPawnAt 末尾（PawnRenderer.cs L374）把 results 重置为
        /// default，而本模块的绘制提交发生在 Application.onBeforeRender（相机渲染前），
        /// 此时 results.valid 必然为 false，读不到本帧真正的 bodyPos / bodyAngle。
        /// 于是改为直接取动画 mod 算好的数据，按它注入的方式还原：
        /// GetBodyPos Postfix → bodyPos += posOffset；BodyAngle Postfix → bodyAngle += angleOffset；
        /// PreRender Prefix → facing = fixedRot ?? pawn.Rotation。
        ///
        /// 数据来源两条：优先 Prepatcher 注入到 Pawn 上的 PawnDrawData 字段（字段名由
        /// Prepatcher 生成、不可预知，故按字段类型匹配），未装 Prepatcher 时回退到
        /// PawnDataUtility.GetData 的静态字典。全程反射访问，不硬依赖动画 mod 程序集：
        /// 未安装时 Resolve 失败，TryGet 返回 false，调用方回退 DrawPos。
        /// </summary>
        private static class AnimationDrawDataProbe
        {
            /// <summary>是否已尝试解析（含失败，失败后不再重试）。</summary>
            private static bool resolveAttempted;

            /// <summary>Prepatcher 注入到 Pawn 上的绘制数据字段（null = 未注入）。</summary>
            private static FieldInfo pawnDataField;

            /// <summary>PawnDataUtility.GetData(Pawn) 兜底读取方法（null = 不可用）。</summary>
            private static MethodInfo getDataMethod;

            /// <summary>PawnDrawData.posOffset：相对 bodyPos 的位移修正。</summary>
            private static FieldInfo posOffsetField;

            /// <summary>PawnDrawData.angleOffset：附加旋转角（度）。</summary>
            private static FieldInfo angleOffsetField;

            /// <summary>PawnDrawData.fixedRot：强制朝向（null 表示不锁定）。</summary>
            private static FieldInfo fixedRotField;

            /// <summary>探针是否可用（动画 mod 未安装时为 false，由调用方回退）。</summary>
            public static bool IsReady
            {
                get
                {
                    Resolve();
                    return posOffsetField != null;
                }
            }

            /// <summary>读取动画 mod 为指定 pawn 算出的绘制修正。
            /// 未安装动画 mod / 数据结构变化时返回 false。</summary>
            /// <param name="pawn">目标 pawn。</param>
            /// <param name="posOffset">输出：位置修正。</param>
            /// <param name="angleOffset">输出：角度修正（度）。</param>
            /// <param name="fixedRot">输出：强制朝向（null 表示沿用 pawn.Rotation）。</param>
            public static bool TryGet(Pawn pawn, out Vector3 posOffset, out float angleOffset,
                out Rot4? fixedRot)
            {
                posOffset = Vector3.zero;
                angleOffset = 0f;
                fixedRot = null;
                Resolve();
                if (posOffsetField == null || pawn == null)
                {
                    return false;
                }
                object data;
                try
                {
                    data = ReadData(pawn);
                }
                catch (Exception)
                {
                    // 目标 pawn 刚被销毁等瞬时异常：本帧按零修正处理，不打断绘制流程
                    return false;
                }
                if (data == null)
                {
                    return false;
                }
                try
                {
                    object rawPos = posOffsetField.GetValue(data);
                    if (rawPos is Vector3 v)
                    {
                        posOffset = v;
                    }
                    if (angleOffsetField != null)
                    {
                        object rawAngle = angleOffsetField.GetValue(data);
                        if (rawAngle is float f)
                        {
                            angleOffset = f;
                        }
                    }
                    if (fixedRotField != null)
                    {
                        fixedRot = fixedRotField.GetValue(data) as Rot4?;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
                return true;
            }

            /// <summary>取指定 pawn 的 PawnDrawData 实例（装箱返回）。
            /// 优先读 Prepatcher 注入字段，其次调用 PawnDataUtility.GetData。</summary>
            /// <param name="pawn">目标 pawn。</param>
            private static object ReadData(Pawn pawn)
            {
                if (pawnDataField != null)
                {
                    return pawnDataField.GetValue(pawn);
                }
                if (getDataMethod != null)
                {
                    return getDataMethod.Invoke(null, new object[] { pawn });
                }
                return null;
            }

            /// <summary>解析动画 mod 类型与数据字段（进程内仅执行一次）。</summary>
            private static void Resolve()
            {
                if (resolveAttempted)
                {
                    return;
                }
                resolveAttempted = true;
                try
                {
                    Type dataType = GenTypes.GetTypeInAnyAssembly("YayoAnimation.Data.PawnDrawData");
                    if (dataType == null)
                    {
                        // 未安装动画 mod：静默停用（描边照常工作，只是不跟随动画）
                        return;
                    }
                    posOffsetField = dataType.GetField("posOffset");
                    angleOffsetField = dataType.GetField("angleOffset");
                    fixedRotField = dataType.GetField("fixedRot");
                    if (posOffsetField == null)
                    {
                        KiiroGumLog.Warn(KiiroGumLogModule.Outline, "动画 mod 的 PawnDrawData 缺少 posOffset 字段，"
                            + "描边将不跟随动画位移");
                        return;
                    }

                    // Prepatcher 注入字段：字段名由 Prepatcher 生成，按字段类型匹配最稳
                    foreach (FieldInfo field in typeof(Pawn).GetFields(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (field.FieldType == dataType)
                        {
                            pawnDataField = field;
                            break;
                        }
                    }
                    if (pawnDataField == null)
                    {
                        Type utilType = GenTypes.GetTypeInAnyAssembly("YayoAnimation.Data.PawnDataUtility");
                        getDataMethod = utilType?.GetMethod("GetData",
                            BindingFlags.Public | BindingFlags.Static);
                        if (getDataMethod == null)
                        {
                            KiiroGumLog.Warn(KiiroGumLogModule.Outline, "未找到动画 mod 的绘制数据入口"
                                + "（Prepatcher 字段与 PawnDataUtility.GetData 均不可用），"
                                + "描边将不跟随动画位移");
                        }
                    }
                }
                catch (Exception e)
                {
                    KiiroGumLog.Warn(KiiroGumLogModule.Outline, "解析动画 mod 绘制数据失败：" + e.Message);
                }
            }
        }

        /// <summary>已注册的描边目标（hediff comp 持有宿主 pawn 引用）。</summary>
        private static readonly List<HediffComp_Outline> targets = new List<HediffComp_Outline>();

        /// <summary>复用的属性块（主线程每帧复用，Graphics.DrawMesh 在提交时拷贝其内容，无 GC）。</summary>
        private static readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

        // shader 属性 ID 缓存（避免每帧做字符串查找）
        private static readonly int PropOutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int PropOutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly int PropHoleRadius = Shader.PropertyToID("_HoleRadius");
        private static readonly int PropFrameUV = Shader.PropertyToID("_FrameUV");

        /// <summary>描边 shader 成功加载后的缓存（null = 尚未尝试加载或加载失败）。</summary>
        private static Shader outlineShaderInt;

        /// <summary>描边 shader 是否已尝试过加载（防止加载失败后每帧重复探测）。</summary>
        private static bool outlineShaderResolved;

        /// <summary>调试日志：已输出过的 key 集合（同 key 仅首次输出，防每帧刷屏）。</summary>
        private static readonly HashSet<string> debugLoggedOnce = new HashSet<string>();

        /// <summary>调试日志：上一次全局跳过原因（变化时输出，便于观察状态流转与恢复）。</summary>
        private static string lastGlobalSkipReason;

        /// <summary>调试日志（同 key 仅首次输出）。
        /// [Conditional("DEBUG")]：Release 构建时调用点连同实参求值被整体移除，零开销。</summary>
        /// <param name="key">去重键（含 pawn ID 等唯一成分）。</param>
        /// <param name="message">日志内容。</param>
        [Conditional("DEBUG")]
        private static void DebugLogOnce(string key, string message)
        {
            if (KiiroGumDebug.EnableLog && debugLoggedOnce.Add(key))
            {
                KiiroGumLog.Debug(KiiroGumLogModule.Outline, message);
            }
        }

        /// <summary>记录全局跳过原因（原因变化时输出一次，便于观察当前卡点与状态流转）。</summary>
        /// <param name="reason">当前跳过原因。</param>
        [Conditional("DEBUG")]
        private static void DebugLogSkip(string reason)
        {
            if (KiiroGumDebug.EnableLog && lastGlobalSkipReason != reason)
            {
                lastGlobalSkipReason = reason;
                KiiroGumLog.Debug(KiiroGumLogModule.Outline, "跳过渲染：" + reason);
            }
        }

        /// <summary>全局条件恢复、开始正常遍历目标时输出（与 DebugLogSkip 配对）。</summary>
        [Conditional("DEBUG")]
        private static void DebugLogResume()
        {
            if (KiiroGumDebug.EnableLog && lastGlobalSkipReason != null)
            {
                lastGlobalSkipReason = null;
                KiiroGumLog.Debug(KiiroGumLogModule.Outline, "恢复：全局条件满足，开始遍历描边目标");
            }
        }

        /// <summary>注册描边目标（hediff 创建/存档恢复时由 HediffComp_Outline 调用）。</summary>
        public static void Register(HediffComp_Outline comp)
        {
            if (comp != null && !targets.Contains(comp))
            {
                targets.Add(comp);
                if (KiiroGumDebug.EnableLog)
                {
                    KiiroGumLog.Debug(KiiroGumLogModule.Outline,
                        $"注册描边目标：{comp.parent?.pawn?.LabelShort ?? "??"}（共 {targets.Count} 个）");
                }
            }
        }

        /// <summary>查询指定 comp 是否已注册（供 HediffComp_Outline.CompPostTick 复活兜底用）。</summary>
        public static bool IsRegistered(HediffComp_Outline comp)
        {
            return targets.Contains(comp);
        }

        /// <summary>注销描边目标（hediff 移除时由 HediffComp_Outline 调用）。</summary>
        public static void Unregister(HediffComp_Outline comp)
        {
            if (targets.Remove(comp) && KiiroGumDebug.EnableLog)
            {
                KiiroGumLog.Debug(KiiroGumLogModule.Outline,
                    $"注销描边目标：{comp.parent?.pawn?.LabelShort ?? "??"}（剩 {targets.Count} 个）");
            }
        }

        /// <summary>描边 shader：从 ShaderDef（1.7 的 GLSL 程序注册入口）解析，失败返回 null。
        /// 失败将导致整体停用描边（见 RenderOutlines 开头早退），两条失败路径：
        /// - ShaderDef 缺失（Defs/ShaderDefs 的 XML 未加载）→ 直接 null
        /// - ShaderDef 存在但 GLSL 程序未注册（Shaders/ 目录缺失或编译失败）→ 同样 null
        /// 1.7 不再存在「AB 内 Unity shader 加载失败回退 DefaultShader」的路径：
        /// ShaderDef.Shader 在未注册时会静默返回一个没有材质布局的空壳 Shader，
        /// 无法靠对象比较识别，故改用 ShaderRegistry.TryResolve 直接询问引擎注册表。</summary>
        private static Shader OutlineShader
        {
            get
            {
                if (!outlineShaderResolved)
                {
                    outlineShaderResolved = true;
                    ShaderDef def = DefDatabase<ShaderDef>.GetNamedSilentFail(OutlineConfig.ShaderDefDefName);
                    Shader shader = null;
                    bool registered = false;
                    if (def != null)
                    {
                        registered = Waylith.Rendering.ShaderRegistry.TryResolve(def.defName, out _);
                        if (registered)
                        {
                            shader = def.Shader;
                        }
                    }
                    outlineShaderInt = shader;
                    // 成功加载属调试信息（Debug 构建输出，Release 构建调用点被移除）；
                    // 加载失败会导致描边整体停用，属必须让玩家看到的问题，保持 Warn 级始终输出
                    if (shader != null)
                    {
                        KiiroGumLog.Debug(KiiroGumLogModule.Outline,
                            $"描边 shader 已加载：'{shader.name}'（来自 ShaderDef {OutlineConfig.ShaderDefDefName}）");
                    }
                    else if (def == null)
                    {
                        KiiroGumLog.Warn(KiiroGumLogModule.Outline,
                            $"ShaderDef '{OutlineConfig.ShaderDefDefName}' 不存在（XML 未加载），描边停用");
                    }
                    else
                    {
                        KiiroGumLog.Warn(KiiroGumLogModule.Outline,
                            $"ShaderDef '{OutlineConfig.ShaderDefDefName}' 存在但程序未注册"
                            + "（Shaders/ 下的 GLSL 未找到或编译失败），描边停用");
                    }
                }
                return outlineShaderInt;
            }
        }

        /// <summary>每渲染帧提交所有目标 pawn 的描边层。
        /// 由 Application.onBeforeRender 触发（晚于 ParallelPreDraw、早于相机渲染），
        /// 描边 mesh 经 Graphics.DrawMesh 加入当前帧渲染队列，由相机统一绘制。</summary>
        public static void RenderOutlines()
        {
            RenderTargets(false);
        }

        /// <summary>每逻辑 tick 刷新图集合成帧（不含绘制）。
        /// 与绘制分开：烘焙内部会触发 PawnCacheCamera.Render()（嵌套渲染），
        /// 放在 onBeforeRender 的渲染阶段执行不安全，故留在 GameComponentUpdate。</summary>
        public static void RefreshFrames()
        {
            RenderTargets(true);
        }

        /// <summary>遍历所有目标执行一次处理。</summary>
        /// <param name="bakeOnly">true=只刷新合成帧（tick 阶段）；false=只提交绘制（渲染阶段）。</param>
        private static void RenderTargets(bool bakeOnly)
        {
            // 描边 shader 解析失败（ShaderDef 缺失 / GLSL 程序未注册）→ 整体停止渲染：
            // 回退 shader 画出的层与本体重合被覆盖，只会浪费每帧 DrawMesh + GPU
            if (OutlineShader == null)
            {
                DebugLogSkip("描边 shader 未加载（ShaderDef 缺失或 GLSL 程序未注册），描边停用");
                return;
            }
            // 无目标或设置关闭 → 零开销直接返回
            if (targets.Count == 0)
            {
                DebugLogSkip("无注册目标（还没有 pawn 吃到口香糖/存档恢复未完成）");
                return;
            }
            if (KiiroSchizonepetaGumMod.Settings == null || !KiiroSchizonepetaGumMod.Settings.enableOutline)
            {
                DebugLogSkip("mod 设置中描边开关已关闭");
                return;
            }
            // 非游玩状态（主菜单/加载中）不绘制
            if (Current.ProgramState != ProgramState.Playing || Find.CameraDriver == null)
            {
                DebugLogSkip("非游玩状态（主菜单/加载中）");
                return;
            }
            // 镜头过远时不渲染：pawn 屏幕尺寸过小、描边不可辨，省性能。
            // 阈值 = 相机最大 RootSize × 系数（相对值写法参照官方远景剪影判定
            // SilhouetteUtility L122，自动适配玩家自定义的相机缩放范围）
            float maxRootSize = Find.CameraDriver.config.sizeRange.max;
            if (Find.CameraDriver.ZoomRootSize >= maxRootSize * OutlineConfig.FarCameraDisableFactor)
            {
                DebugLogSkip("镜头过远（远景停用描边）");
                return;
            }

            DebugLogResume();

            // 倒序遍历：DrawOutlineFor 内部可能顺手移除无效目标，倒序删除安全
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                DrawOutlineFor(targets[i], i, bakeOnly);
            }
        }

        /// <summary>处理单个 pawn：刷新合成帧（bakeOnly）或提交描边绘制。
        /// 目标暂时不满足绘制条件时静默跳过（不注销，状态恢复后自动续描）；
        /// 目标已彻底失效（pawn 销毁）时从注册表移除。</summary>
        /// <param name="comp">目标 hediff comp。</param>
        /// <param name="targetIndex">comp 在注册表中的下标（用于失效时移除）。</param>
        /// <param name="bakeOnly">true=只刷新合成帧，不提交绘制。</param>
        private static void DrawOutlineFor(HediffComp_Outline comp, int targetIndex, bool bakeOnly)
        {
            Pawn pawn = comp.Pawn;

            // 目标彻底失效（hediff/pawn 已销毁）→ 从注册表移除兜底
            if (comp.parent == null || pawn == null || pawn.Destroyed)
            {
                // 失效路径不是每帧常态（仅在注销漏触发时补漏）
                DebugLogOnce($"invalid-{targetIndex}", $"目标失效（注册表下标 {targetIndex}），已移除");
                targets.RemoveAt(targetIndex);
                return;
            }
            // 暂时不可绘制：未在地图（远行队中）、死亡、躺卧倒地
            // （躺卧姿势的本体走渲染树特殊路径，描边层不适用，与官方 L439 条件保持一致）
            // 注意：不再要求 Humanlike —— 猫（动物）也能吃到口香糖获得 hediff，
            // 非类人目标由 EnsureFrameFresh 主动烘焙合成帧后同样描边
            if (!pawn.Spawned || pawn.Map == null || pawn.Dead)
            {
                DebugLogOnce($"inactive-{pawn.thingIDNumber}", $"{pawn.LabelShort} 暂不绘制：未在地图上（远行队中/尸体）");
                return;
            }
            if (pawn.GetPosture() != PawnPosture.Standing)
            {
                DebugLogOnce($"posture-{pawn.thingIDNumber}", $"{pawn.LabelShort} 暂不绘制：非站姿（躺卧/倒地中）");
                return;
            }
            // 游泳时（人类游泳 job / 非人类在水中用游泳图形）：本体沉入水中只露头，
            // 官方走渲染树特殊路径（NoBody，只画头，PawnRenderer L425-426），完整站立
            // 合成帧与本体不一致 → 跳过描边。注意游泳时 GetPosture() 仍是 Standing，
            // 故必须在此单独判定，不能并入上面的站姿检查。
            if (pawn.Swimming || pawn.DrawNonHumanlikeSwimmingGraphic)
            {
                DebugLogOnce($"swimming-{pawn.thingIDNumber}", $"{pawn.LabelShort} 暂不绘制：游泳中（本体仅露头，无全身轮廓）");
                return;
            }
            // 本体渲染 alpha < 1（隐身/半透明）→ 跳过描边：实心描边会在本体透明/半透明时
            // 突兀地暴露 pawn。判断依据用 InvisibilityUtility.GetAlpha（本体实际渲染 tint alpha，
            // PawnRenderer.GetDrawParms L643 同款来源）而非 IsPsychologicallyInvisible：
            // 后者对"心理隐身但对玩家可见"的 pawn（如绮罗夜行衣 visibleToPlayer=true，
            // HediffComp_Invisibility.GetAlpha 恒返回 1）也返回 true，会误伤——夜行衣本体
            // 正常可见（alpha=1），描边应保留。GetAlpha < 1 才能同时正确区分两种情况。
            float bodyAlpha = InvisibilityUtility.GetAlpha(pawn);
            if (bodyAlpha < 1f)
            {
                DebugLogOnce($"invisible-{pawn.thingIDNumber}", $"{pawn.LabelShort} 暂不绘制：本体渲染 alpha={bodyAlpha:F2}（隐身/半透明中）");
                return;
            }

            // 取 atlas 合成帧（类人由官方烘焙；猫由本模块在脏帧时主动烘焙）
            if (!GlobalTextureAtlasManager.TryGetPawnFrameSet(pawn, out PawnTextureAtlasFrameSet frameSet, out _, true))
            {
                DebugLogOnce($"nofs-{pawn.thingIDNumber}", $"{pawn.LabelShort}：TryGetPawnFrameSet 失败（图集无空闲帧？）");
                return;
            }

            // 本帧实际绘制参数：位置/旋转/朝向由 PawnRenderer 在渲染阶段算好后写入私有
            // results 字段（动画 mod 的 posOffset / angleOffset / rotOverride 同样落在那里），
            // 读取它即可与本体精确对齐。
            // 注意 results 的生命周期：官方在 RenderPawnAt 末尾（PawnRenderer.cs L374）把它
            // 重置为 default，而本模块是在 Application.onBeforeRender（相机渲染前）提交绘制，
            // 此时 results.valid 恒为 false —— 因此 results 仅作首选来源，读不到时按动画 mod
            // 的原始绘制数据复现同一套修正。
            Vector3 bodyPos;
            float bodyAngle;
            Rot4 facing;
            string poseSource;
            if (RenderResultProbe.TryGet(pawn.Drawer.renderer, out bodyPos, out bodyAngle, out facing))
            {
                poseSource = "results";
            }
            else if (AnimationDrawDataProbe.TryGet(pawn, out Vector3 posOffset, out float angleOffset,
                out Rot4? fixedRot))
            {
                // 等价复现动画 mod 的三处注入（Yayo's Animation）：
                // GetBodyPos Postfix → __result += posOffset
                // BodyAngle Postfix   → __result += angleOffset
                // PreRender Prefix    → rotOverride = fixedRot ?? rotOverride
                bodyPos = pawn.DrawPos + posOffset;
                bodyAngle = angleOffset;
                facing = fixedRot ?? pawn.Rotation;
                poseSource = "anim";
            }
            else
            {
                bodyPos = pawn.DrawPos;
                bodyAngle = 0f;
                facing = pawn.Rotation;
                poseSource = "default";
            }

            int index = frameSet.GetIndex(facing, PawnDrawMode.BodyAndHead);
            // tick 阶段：只刷新合成帧。烘焙内部会触发 PawnCacheCamera.Render()（嵌套渲染），
            // 必须留在逻辑阶段；帧新鲜后，官方与描边共用同一张合成帧，无竞态。
            // 烘焙朝向必须与取用朝向一致，否则锁朝向的动画会残留其它朝向的帧内容
            if (bakeOnly)
            {
                EnsureFrameFresh(pawn, frameSet, index, facing);
                return;
            }

            // 材质按 (atlas, shader) 缓存于 MaterialPool（官方 PawnRenderer L324 同款用法）；
            // 图集整体重建时 atlas 为新 RenderTexture 实例，自动生成新材质
            Material mat = MaterialPool.MatFrom(new MaterialRequest(frameSet.atlas, OutlineShader));
            if (mat == null)
            {
                DebugLogOnce($"nomat-{pawn.thingIDNumber}", $"{pawn.LabelShort}：MaterialPool.MatFrom 返回 null");
                return;
            }

            // 描边参数：玩家设置的颜色/宽度优先于 def 级默认值
            KiiroSchizonepetaGumSettings settings = KiiroSchizonepetaGumMod.Settings;
            propertyBlock.SetColor(PropOutlineColor, settings.outlineColor);
            propertyBlock.SetFloat(PropOutlineWidth,
                Mathf.Clamp(settings.outlineWidth, OutlineConfig.OutlineWidthMin, OutlineConfig.OutlineWidthMax));
            propertyBlock.SetFloat(PropHoleRadius, comp.Props.holeRadius);

            // 帧边界传给 shader，用于把邻域膨胀采样 clamp 在帧区域内（防止读到相邻 pawn 的帧）
            Rect uvRect = frameSet.uvRects[index];
            propertyBlock.SetVector(PropFrameUV,
                new Vector4(uvRect.xMin, uvRect.yMin, uvRect.xMax, uvRect.yMax));

            // 描边层位置：body 绘制位（已含动画位移）+ 相对本体层上抬 1 层
            //（本体 body 节点的 baseLayer 为 0；描边略高一层既避开共面深度比较，
            // 又保证与本体处于同一遮挡关系，不会被低矮植被单独盖住）
            Vector3 pos = bodyPos;
            pos.y += PawnRenderUtility.AltitudeForLayer(OutlineConfig.OutlineAltitudeLayer);

            // 旋转与官方 atlas 绘制路径一致（PawnRenderer L326）：绕 bodyPos 施加 bodyAngle
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.AngleAxis(bodyAngle, Vector3.up), Vector3.one);

            // 提交绘制（drawNow:false → Graphics.DrawMesh 入队，当前帧相机渲染时绘制）
            GenDraw.DrawMeshNowOrLater(frameSet.meshes[index], matrix, mat, false, propertyBlock);

            // 成功提交一次即记录（含关键参数，供核对位置/朝向/帧区域）；
            // 此处在每目标每帧的正常绘制路径上，插值开销只存在于 Debug 构建（Release 调用点被移除）
            DebugLogOnce($"drawn-{pawn.thingIDNumber}",
                $"{pawn.LabelShort} 描边已提交：facing={facing}，bodyAngle={bodyAngle}，帧下标={index}，" +
                $"uvRect={frameSet.uvRects[index]}，pos={pos}，shader='{OutlineShader.name}'");

            // 诊断：按固定间隔打印渲染参数，用于确认 bodyPos / bodyAngle 是否随动画变化。
            // hasAnim=PawnRenderer.HasAnimation（= renderTree.currentAnimation != null）：
            // 1.7 官方动画系统一旦接管该 pawn，动画施加在渲染树节点上而不写入 results，
            // 此时描边读 results 拿不到任何位移；动画 mod 也会主动让行（yayoAni CheckAni L37）。
            if (Find.TickManager.TicksGame % OutlineConfig.DiagnosticsIntervalTicks == 0)
            {
                KiiroGumLog.Debug(KiiroGumLogModule.Outline,
                    $"[诊断] tick={Find.TickManager.TicksGame}"
                    + $" {pawn.LabelShort} moving={pawn.pather?.Moving}"
                    + $" job={pawn.CurJobDef?.defName ?? "-"}"
                    + $" src={poseSource}"
                    + $" probe={RenderResultProbe.IsReady}/{AnimationDrawDataProbe.IsReady}"
                    + $" hasAnim={pawn.Drawer.renderer.HasAnimation}"
                    + $" animAsmLoaded={IsAnimationModAssemblyLoaded()}"
                    + $" coreInit={IsAnimationCoreInitialized()}"
                    + $" patches[{GetPatchSummary("GetBodyPos")}"
                    + $" | {GetPatchSummary("BodyAngle")}"
                    + $" | {GetPatchSummary("ParallelGetPreRenderResults")}]"
                    + $" pdd[{GetAnimationDrawData(pawn)}]"
                    + $" drawPos={pawn.DrawPos} bodyPos={bodyPos} delta={bodyPos - pawn.DrawPos}"
                    + $" bodyAngle={bodyAngle:F2} facing={facing} pawnRot={pawn.Rotation}");
            }
        }

        /// <summary>诊断缓存：动画 mod（yayoAni）程序集是否已加载。null=尚未探测。</summary>
        private static bool? animationModAssemblyLoaded;

        /// <summary>诊断用：检查当前 AppDomain 是否已加载 yayoAni 程序集。
        /// 用于区分「mod 未加载（1.7 无版本目录/未回退）」与「已加载但 Harmony 补丁未生效」。
        /// 结果缓存，避免反复遍历程序集。</summary>
        private static bool IsAnimationModAssemblyLoaded()
        {
            if (animationModAssemblyLoaded.HasValue)
            {
                return animationModAssemblyLoaded.Value;
            }
            bool found = false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = asm.GetName().Name;
                if (name != null && name.IndexOf("yayo", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = true;
                    break;
                }
            }
            animationModAssemblyLoaded = found;
            return found;
        }

        /// <summary>诊断缓存：PawnRenderer 各注入点的补丁摘要（方法名 → 摘要文本）。</summary>
        private static readonly Dictionary<string, string> patchSummaries = new Dictionary<string, string>();

        /// <summary>诊断用：带缓存的补丁摘要查询。</summary>
        /// <param name="methodName">PawnRenderer 上的方法名。</param>
        private static string GetPatchSummary(string methodName)
        {
            string cached;
            if (patchSummaries.TryGetValue(methodName, out cached))
            {
                return cached;
            }
            string summary = QueryPatchSummary(typeof(PawnRenderer), methodName);
            patchSummaries[methodName] = summary;
            return summary;
        }

        /// <summary>诊断用：反射查询指定方法上的 Harmony 补丁摘要，
        /// 形如 "GetBodyPos(前0/后1/转0:com.yayo.yayoAni)"。
        /// 注意 Harmony.GetPatchInfo 返回的是公开的 <c>Patches</c> 类型，其字段为
        /// Prefixes/Postfixes/Transpilers/Finalizers（PascalCase 的 ReadOnlyCollection）；
        /// 内部类 <c>PatchInfo</c> 才有小写 postfixes 字段。早期探针误用小写字段名，
        /// 反射取不到值而恒返回 0，是「补丁数为 0」误判的来源。</summary>
        /// <param name="type">目标类型。</param>
        /// <param name="methodName">目标方法名。</param>
        private static string QueryPatchSummary(Type type, string methodName)
        {
            try
            {
                Type harmonyType = GenTypes.GetTypeInAnyAssembly("HarmonyLib.Harmony");
                MethodInfo getPatchInfo = harmonyType?.GetMethod("GetPatchInfo",
                    BindingFlags.Public | BindingFlags.Static);
                MethodInfo target = type?.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (getPatchInfo == null || target == null)
                {
                    return methodName + "(api-missing)";
                }
                object patches = getPatchInfo.Invoke(null, new object[] { target });
                if (patches == null)
                {
                    return methodName + "(none)";
                }
                Type patchesType = patches.GetType();
                int prefixes = CountCollectionField(patchesType, patches, "Prefixes");
                int postfixes = CountCollectionField(patchesType, patches, "Postfixes");
                int transpilers = CountCollectionField(patchesType, patches, "Transpilers");
                PropertyInfo ownersProp = patchesType.GetProperty("Owners");
                System.Collections.IEnumerable owners =
                    ownersProp?.GetValue(patches, null) as System.Collections.IEnumerable;
                var ownerNames = new List<string>();
                if (owners != null)
                {
                    foreach (object owner in owners)
                    {
                        ownerNames.Add(owner as string ?? "?");
                    }
                }
                return $"{methodName}(前{prefixes}/后{postfixes}/转{transpilers}:{string.Join("+", ownerNames)})";
            }
            catch (Exception e)
            {
                return methodName + "(err:" + (e.InnerException?.Message ?? e.Message) + ")";
            }
        }

        /// <summary>诊断用：读取 Patches 对象上某个集合字段的元素数量（字段缺失返回 -1）。</summary>
        private static int CountCollectionField(Type patchesType, object instance, string fieldName)
        {
            FieldInfo field = patchesType.GetField(fieldName);
            System.Collections.ICollection collection =
                field?.GetValue(instance) as System.Collections.ICollection;
            return (collection != null) ? collection.Count : -1;
        }

        /// <summary>诊断用：把动画 mod 为指定 pawn 算出的绘制修正格式化为一行文本。
        /// 与绘制路径共用 AnimationDrawDataProbe，因此这里的读数就是描边实际使用的值：
        /// 若 pdd 非零而 delta 为 0，说明读取链（Prepatcher 字段 / GetData）没打通。</summary>
        /// <param name="pawn">目标 pawn。</param>
        private static string GetAnimationDrawData(Pawn pawn)
        {
            if (!AnimationDrawDataProbe.TryGet(pawn, out Vector3 pos, out float angle, out Rot4? rot))
            {
                return "unavailable";
            }
            return "pos=" + pos + " ang=" + angle.ToString("F2") + " rot=" + (rot.HasValue ? rot.Value.ToString() : "-");
        }

        /// <summary>诊断用：判断动画 mod 的 Core（Mod 子类）是否已实例化。
        /// 已实例化说明 RimWorld 真的加载了该程序集并执行了构造函数，
        /// 此时 PatchAll 必然跑过；配合 patch 数量即可区分「没加载」与「加载了但补丁失败」。</summary>
        private static bool? animationCoreInitialized;

        private static bool IsAnimationCoreInitialized()
        {
            if (animationCoreInitialized.HasValue)
            {
                return animationCoreInitialized.Value;
            }
            bool ok = false;
            try
            {
                Type coreType = GenTypes.GetTypeInAnyAssembly("YayoAnimation.Core");
                FieldInfo settingsField = coreType?.GetField("settings", BindingFlags.Public | BindingFlags.Static);
                ok = settingsField != null && settingsField.GetValue(null) != null;
            }
            catch
            {
                ok = false;
            }
            animationCoreInitialized = ok;
            return ok;
        }

        /// <summary>若指定帧过期则立即重新烘焙（复制官方 PawnRenderer.GetBlitMeshUpdatedFrame
        /// L231-242 的完整逻辑，参数逐字一致）。
        /// 类人 pawn 的本体渲染在相机渲染阶段才会调用 GetBlitMeshUpdatedFrame，
        /// 本模块烘焙在前（GameComponentUpdate 早于渲染），dirty 清除后官方会直接复用
        /// 本模块烘焙的帧，双方共用同一合成帧，无竞态、无重复烘焙。
        /// 猫等非类人 pawn 官方从不烘焙，本方法成为其唯一合成帧来源。</summary>
        /// <param name="pawn">要烘焙的 pawn。</param>
        /// <param name="frameSet">该 pawn 的合成帧集。</param>
        /// <param name="index">当前朝向/绘制模式对应的帧下标。</param>
        /// <param name="facing">当前实际绘制朝向（锁朝向的动画会给出与 pawn.Rotation 不同的值）。</param>
        private static void EnsureFrameFresh(Pawn pawn, PawnTextureAtlasFrameSet frameSet, int index, Rot4 facing)
        {
            if (!frameSet.isDirty[index])
            {
                return;
            }
            // 把 PawnCacheCamera 的绘制范围限定到该帧的图集区块，渲染一次 pawn
            Find.PawnCacheCamera.rect = frameSet.uvRects[index];
            Find.PawnCacheRenderer.RenderPawn(pawn, frameSet.atlas, Vector3.zero, 1f, 0f,
                facing, true, true, true, false, default, null, null, false);
            // 还原相机视口（全局共享相机，必须复位，否则影响后续渲染）
            Find.PawnCacheCamera.rect = new Rect(0f, 0f, 1f, 1f);
            frameSet.isDirty[index] = false;
        }
    }
}
