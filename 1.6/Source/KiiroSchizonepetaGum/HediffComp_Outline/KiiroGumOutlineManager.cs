using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace KiiroSchizonepetaGum
{
    /// <summary>
    /// 描边渲染管理器（静态）：
    /// - 维护"需要描边的 pawn"注册表（由 HediffComp_Outline 生命周期驱动增删）
    /// - 每帧由 KiiroGumOutlineGameComponent.GameComponentUpdate 驱动，
    ///   对每个目标 pawn 取官方 atlas 合成帧（身体+衣物+发型+头饰的烘焙结果），
    ///   在 pawn 本体下层提交一次描边 mesh 绘制（Graphics.DrawMesh 入队，相机渲染时绘制）
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
    /// - 描边层高度 = pawn.DrawPos + AltitudeForLayer(-8)，位于本体之下被覆盖，只露出外圈
    /// - 站立时官方 bodyAngle 恒为 0（PawnRenderer L432），描边层用 identity 旋转即精确对齐
    /// - shader 参数经 MaterialPropertyBlock 按次提交，不污染 MaterialPool 缓存的共享材质
    ///
    /// StaticConstructorOnStartup：静态字段初始化用到了 MaterialPropertyBlock、
    /// Shader.PropertyToID 等 Unity API，必须保证在主线程执行——该属性让 RimWorld 在
    /// 游戏加载完成时主动触发静态构造（StaticConstructorOnStartupUtility）。
    /// </summary>
    [StaticConstructorOnStartup]
    public static class KiiroGumOutlineManager
    {
        /// <summary>是否支持描边特效的平台（当前仅支持 Windows）。
        /// 本 mod 只随包发布 _win 版 shader AB，RimWorld 的 AB 加载按平台后缀匹配
        /// （ModAssetBundlesHandler.BundleSuffixForCurrentOs），非 Windows 平台加载不到
        /// 描边 shader。为避免回退官方 Cutout 显示"彩色剪影衬底"，非 Windows 整体不渲染。</summary>
        private static readonly bool IsOutlineSupported =
            Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.WindowsEditor;

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

        /// <summary>调试日志（同 key 仅首次输出）。受 OutlineConfig.DebugLogging 开关控制。</summary>
        /// <param name="key">去重键（含 pawn ID 等唯一成分）。</param>
        /// <param name="message">日志内容。</param>
        private static void DebugLogOnce(string key, string message)
        {
            if (OutlineConfig.DebugLogging && debugLoggedOnce.Add(key))
            {
                Log.Message("[KiiroGumOutline] " + message);
            }
        }

        /// <summary>记录全局跳过原因（原因变化时输出一次，便于观察当前卡点与状态流转）。</summary>
        private static void DebugLogSkip(string reason)
        {
            if (OutlineConfig.DebugLogging && lastGlobalSkipReason != reason)
            {
                lastGlobalSkipReason = reason;
                Log.Message("[KiiroGumOutline] 跳过渲染：" + reason);
            }
        }

        /// <summary>全局条件恢复、开始正常遍历目标时输出（与 DebugLogSkip 配对）。</summary>
        private static void DebugLogResume()
        {
            if (OutlineConfig.DebugLogging && lastGlobalSkipReason != null)
            {
                lastGlobalSkipReason = null;
                Log.Message("[KiiroGumOutline] 恢复：全局条件满足，开始遍历描边目标");
            }
        }

        /// <summary>注册描边目标（hediff 创建/存档恢复时由 HediffComp_Outline 调用）。</summary>
        public static void Register(HediffComp_Outline comp)
        {
            if (comp != null && !targets.Contains(comp))
            {
                targets.Add(comp);
                if (OutlineConfig.DebugLogging)
                {
                    Log.Message($"[KiiroGumOutline] 注册描边目标：{comp.parent?.pawn?.LabelShort ?? "??"}（共 {targets.Count} 个）");
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
            if (targets.Remove(comp) && OutlineConfig.DebugLogging)
            {
                Log.Message($"[KiiroGumOutline] 注销描边目标：{comp.parent?.pawn?.LabelShort ?? "??"}（剩 {targets.Count} 个）");
            }
        }

        /// <summary>描边 shader：优先从 ShaderTypeDef（mod AB 包）加载，失败返回 null。
        /// 失败将导致整体停用描边（见 RenderOutlines 开头早退）：回退 shader 画出的层
        /// 两种失败路径：
        /// - def 缺失（XML 未加载）→ 直接 null
        /// - def 存在但 AB 未加载 → 官方 TryLoadShader 内部回退 Unity DefaultShader
        ///   （引用比较检测，ShaderDatabase.cs L106）→ 同样视为失败</summary>
        private static Shader OutlineShader
        {
            get
            {
                if (!outlineShaderResolved)
                {
                    outlineShaderResolved = true;
                    ShaderTypeDef def = DefDatabase<ShaderTypeDef>.GetNamedSilentFail(OutlineConfig.ShaderTypeDefDefName);
                    Shader shader = (def != null) ? def.Shader : null;
                    // 隐性回退检测：AB 未加载时官方返回 DefaultShader，并非我们的描边 shader
                    if (shader == ShaderDatabase.DefaultShader)
                    {
                        shader = null;
                    }
                    outlineShaderInt = shader;
                    if (OutlineConfig.DebugLogging)
                    {
                        if (shader != null)
                        {
                            Log.Message($"[KiiroGumOutline] 描边 shader 已加载：'{shader.name}'（来自 ShaderTypeDef {OutlineConfig.ShaderTypeDefDefName}）");
                        }
                        else if (def == null)
                        {
                            Log.Warning($"[KiiroGumOutline] ShaderTypeDef '{OutlineConfig.ShaderTypeDefDefName}' 不存在（XML 未加载），描边停用");
                        }
                        else
                        {
                            Log.Warning($"[KiiroGumOutline] 描边 shader 未加载（AB 包缺失，检测到官方回退 DefaultShader），描边停用");
                        }
                    }
                }
                return outlineShaderInt;
            }
        }

        /// <summary>每帧渲染所有目标 pawn 的描边层。
        /// 由 GameComponentUpdate（游戏逻辑阶段，早于相机渲染）调用，
        /// 描边 mesh 经 Graphics.DrawMesh 加入当前帧渲染队列，由相机统一绘制。</summary>
        public static void RenderOutlines()
        {
            // 非 Windows 平台不渲染描边（只随包发布 Windows 版 shader AB，见 IsOutlineSupported 注释）
            if (!IsOutlineSupported)
            {
                DebugLogSkip("平台不支持（当前非 Windows，仅 Windows 生效）");
                return;
            }
            // 描边 shader 加载失败（AB 包缺失/XML 缺失）→ 整体停止渲染：
            // 回退 shader 画出的层与本体重合被覆盖，只会浪费每帧 DrawMesh + GPU
            if (OutlineShader == null)
            {
                DebugLogSkip("描边 shader 未加载（AB 包缺失或 XML 缺失），描边停用");
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
                DrawOutlineFor(targets[i], i);
            }
        }

        /// <summary>绘制单个 pawn 的描边层。
        /// 目标暂时不满足绘制条件时静默跳过（不注销，状态恢复后自动续描）；
        /// 目标已彻底失效（pawn 销毁）时从注册表移除。</summary>
        /// <param name="comp">目标 hediff comp。</param>
        /// <param name="targetIndex">comp 在注册表中的下标（用于失效时移除）。</param>
        private static void DrawOutlineFor(HediffComp_Outline comp, int targetIndex)
        {
            Pawn pawn = comp.Pawn;

            // 目标彻底失效（hediff/pawn 已销毁）→ 从注册表移除兜底
            if (comp.parent == null || pawn == null || pawn.Destroyed)
            {
                // 失效路径不是每帧常态（仅在注销漏触发时补漏），但插值仍受编译期常量门槛保护
                if (OutlineConfig.DebugLogging)
                {
                    DebugLogOnce($"invalid-{targetIndex}", $"目标失效（注册表下标 {targetIndex}），已移除");
                }
                targets.RemoveAt(targetIndex);
                return;
            }
            // 暂时不可绘制：未在地图（远行队中）、死亡、躺卧倒地
            // （躺卧姿势的本体走渲染树特殊路径，描边层不适用，与官方 L439 条件保持一致）
            // 注意：不再要求 Humanlike —— 猫（动物）也能吃到口香糖获得 hediff，
            // 非类人目标由 EnsureFrameFresh 主动烘焙合成帧后同样描边
            if (!pawn.Spawned || pawn.Map == null || pawn.Dead)
            {
                if (OutlineConfig.DebugLogging)
                {
                    DebugLogOnce($"inactive-{pawn.thingIDNumber}", $"{pawn.LabelShort} 暂不绘制：未在地图上（远行队中/尸体）");
                }
                return;
            }
            if (pawn.GetPosture() != PawnPosture.Standing)
            {
                if (OutlineConfig.DebugLogging)
                {
                    DebugLogOnce($"posture-{pawn.thingIDNumber}", $"{pawn.LabelShort} 暂不绘制：非站姿（躺卧/倒地中）");
                }
                return;
            }

            // 取 atlas 合成帧（类人由官方烘焙；猫由本模块在脏帧时主动烘焙）
            if (!GlobalTextureAtlasManager.TryGetPawnFrameSet(pawn, out PawnTextureAtlasFrameSet frameSet, out _, true))
            {
                if (OutlineConfig.DebugLogging)
                {
                    DebugLogOnce($"nofs-{pawn.thingIDNumber}", $"{pawn.LabelShort}：TryGetPawnFrameSet 失败（图集无空闲帧？）");
                }
                return;
            }

            int index = frameSet.GetIndex(pawn.Rotation, PawnDrawMode.BodyAndHead);
            // 脏帧 → 主动烘焙后本帧即描边（复制官方 GetBlitMeshUpdatedFrame L231-242 逻辑；
            // 类人的官方本体渲染在本模块烘焙之后，dirty 已清会直接复用本模块的帧，无竞态）
            EnsureFrameFresh(pawn, frameSet, index);

            // 材质按 (atlas, shader) 缓存于 MaterialPool（官方 PawnRenderer L324 同款用法）；
            // 图集整体重建时 atlas 为新 RenderTexture 实例，自动生成新材质
            Material mat = MaterialPool.MatFrom(new MaterialRequest(frameSet.atlas, OutlineShader));
            if (mat == null)
            {
                if (OutlineConfig.DebugLogging)
                {
                    DebugLogOnce($"nomat-{pawn.thingIDNumber}", $"{pawn.LabelShort}：MaterialPool.MatFrom 返回 null");
                }
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

            // 描边层位置：pawn 绘制位 + 相对 body 层下移（官方 apparel extras 同款手法，PawnRenderer L326）
            Vector3 pos = pawn.DrawPos;
            pos.y += PawnRenderUtility.AltitudeForLayer(OutlineConfig.OutlineAltitudeLayer);

            // 站立时官方 bodyAngle 恒为 0（PawnRenderer L432），identity 旋转与本体精确对齐
            Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);

            // 提交绘制（drawNow:false → Graphics.DrawMesh 入队，当前帧相机渲染时绘制）
            GenDraw.DrawMeshNowOrLater(frameSet.meshes[index], matrix, mat, false, propertyBlock);

            // 成功提交一次即记录（含关键参数，供核对位置/朝向/帧区域）；
            // 此处在每目标每帧的正常绘制路径上，日志插值必须受编译期常量门槛保护
            //（OutlineConfig.DebugLogging = false 时整块被死代码消除，零字符串分配）
            if (OutlineConfig.DebugLogging)
            {
                DebugLogOnce($"drawn-{pawn.thingIDNumber}",
                    $"{pawn.LabelShort} 描边已提交：rotation={pawn.Rotation}，帧下标={index}，" +
                    $"uvRect={frameSet.uvRects[index]}，pos={pos}，shader='{OutlineShader.name}'");
            }
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
        private static void EnsureFrameFresh(Pawn pawn, PawnTextureAtlasFrameSet frameSet, int index)
        {
            if (!frameSet.isDirty[index])
            {
                return;
            }
            // 把 PawnCacheCamera 的绘制范围限定到该帧的图集区块，渲染一次 pawn
            Find.PawnCacheCamera.rect = frameSet.uvRects[index];
            Find.PawnCacheRenderer.RenderPawn(pawn, frameSet.atlas, Vector3.zero, 1f, 0f,
                pawn.Rotation, true, true, true, false, default, null, null, false);
            // 还原相机视口（全局共享相机，必须复位，否则影响后续渲染）
            Find.PawnCacheCamera.rect = new Rect(0f, 0f, 1f, 1f);
            frameSet.isDirty[index] = false;
        }
    }
}
