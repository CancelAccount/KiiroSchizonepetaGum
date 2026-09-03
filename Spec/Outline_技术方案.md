# KiiroSchizonepetaGum — 人物外描边特效技术方案

## 1. 需求概述

- 为带有指定 hediff（`Kiiro_SchizonepetaGumHigh`，荆芥口香糖状态）的 pawn 渲染**外描边**。
- 描边对象是**人物与衣物的合成外观**（含服装、发型、头饰），而非仅身体贴图。
- **遮挡剔除**：只描最外层可见轮廓，不描衣物褶皱、发丝缝隙等内部细节；衣物遮住的身体部分不产生描边。
- 性能压力尽量交给 GPU（描边检测在 shader 片元阶段完成，CPU 每帧仅提交一次绘制调用）。
- 描边样式：外描边（轮廓线完全在人物外侧，不覆盖人物像素），颜色/宽度可在 mod 设置中调节。
- 功能并入现有 `KiiroSchizonepetaGum` mod，不新建 mod。

## 2. 技术背景（源码调研结论）

以下结论均来自对 RimWorld 1.6 反编译源码（`Source_Decompiled`）的核实：

### 2.1 pawn 的渲染路径（关键）

`Verse.PawnRenderer.RenderPawnAt()` 中 pawn 在地图上的实际绘制有两条路径：

| 路径 | 触发条件 | 绘制内容 |
|---|---|---|
| **atlas 缓存路径**（默认） | 类人 + 相机缩放 > 18 + 无 portrait/雕像/干尸/爬行/游泳/动画 | 直接绘制一张 **atlas 合成帧**（`frameSet.atlas` + `frameSet.meshes[index]`） |
| 渲染树路径 | 其余情况（远距离、非类人、特殊姿态） | 遍历 `PawnRenderTree` 逐层绘制 |

> 结论：**大多数情况下 pawn 的最终画面 = 一张"身体+衣物+发型+头饰烘焙合成"的贴图（atlas 帧）**，这正是我们需要的描边输入源。

### 2.2 atlas 合成帧（可公开访问）

- `Verse.GlobalTextureAtlasManager.TryGetPawnFrameSet(Pawn, out PawnTextureAtlasFrameSet, out bool, bool)` — **public**，获取 pawn 的合成外观帧集。
- `Verse.PawnTextureAtlasFrameSet` 字段全部 **public**：
  - `RenderTexture atlas` — 全局共享图集，该 pawn 的合成外观烘焙在其中（按 4 朝向 × 2 绘制模式 = 8 帧）。
  - `Rect[] uvRects[8]`、`Mesh[] meshes[8]` — 每帧的 UV 区域与动画网格。
  - `bool[] isDirty[8]` — 脏标记，帧内容过期时由官方渲染管线重新烘焙。
  - `int GetIndex(Rot4, PawnDrawMode)` — 按朝向/模式取帧下标。
- 帧烘焙由 `RimWorld.PawnCacheRenderer.RenderPawn(...)` + `Find.PawnCacheCamera`（cullingMask=0、手动渲染）完成，官方已维护其生命周期，mod 无需自建相机。

### 2.3 每帧绘制时机

- `GameComponent.GameComponentUpdate()` 由 `Verse.Game.UpdatePlay()` 每帧调用（源码 [Game.cs](file:///e:/steam/steamapps/common/RimWorld/Source_Decompiled/Assembly-CSharp/Verse/Game.cs) 第 686 行），早于相机渲染阶段。
- 在该回调中调用 `GenDraw.DrawMeshNowOrLater(mesh, matrix, mat, drawNow: false)` 即调用 Unity `Graphics.DrawMesh` 加入当前帧渲染队列，相机渲染时绘制。
- 由于描边层先于 pawn 本体绘制入队，且描边层 y 高度低于 body 层（正交俯视相机下 y 越大越靠近相机），**描边层自然位于 pawn 下层**，被 pawn 不透明像素覆盖、只露出外圈。

## 3. 整体架构

```
┌─────────────────────────── 渲染帧（每帧） ───────────────────────────┐
│  GameComponentUpdate（游戏逻辑阶段，早于渲染）                        │
│    └─ KiiroGumOutlineManager：遍历目标 pawn 列表                      │
│         ├─ 取合成帧: GlobalTextureAtlasManager.TryGetPawnFrameSet     │
│         │    → atlas 贴图（身体+衣物+发型合成）                        │
│         ├─ 确保帧新鲜: isDirty → PawnCacheRenderer.RenderPawn         │
│         │    （复制官方逻辑，谁先渲染谁负责，无竞态）                   │
│         ├─ 描边材质: MaterialPool.MatFrom(atlas, KiiroOutlineShader)  │
│         ├─ 矩阵: 位置=pawn.DrawPos(下层y) + 朝向角                    │
│         └─ GenDraw.DrawMeshNowOrLater(mesh, matrix, mat, false)      │
│                                                                      │
│  渲染阶段：pawn 本体绘制 → 覆盖描边层内部，露出外圈描边线              │
└───────────────────────────────────────────────────────────────────────┘

┌─────────── 目标注册（hediff 生命周期） ───────────┐
│  HediffComp_Outline                                │
│    ├─ CompPostMake：pawn 注册进 Manager            │
│    ├─ CompPostTick：校验 pawn 存活/在地图          │
│    └─ 移除/死亡/卸载：自动注销                     │
└─────────────────────────────────────────────────────┘
```

## 4. 模块设计

### 4.1 Unity 侧 — 描边 shader（AB 包）

**文件**：`KiiroOutline.shader`（在 RimWorld Mod SDK Unity 工程中编写，导出 AB 包）

**Shader 属性**：

| 属性 | 类型 | 说明 |
|---|---|---|
| `_MainTex` | 2D | 输入图集（atlas RenderTexture），mesh UV 已锁定到目标 pawn 帧区域 |
| `_OutlineColor` | Color | 描边颜色 |
| `_OutlineWidth` | Float | 外描边宽度（图集 texel 步长，默认 2~3） |
| `_HoleRadius` | Float | 内部孔洞容忍半径（图集 texel，用于 erode+dilate 填孔，默认 1~2） |
| `_AlphaCutoff` | Float | alpha 不透明阈值（默认 0.5） |

**片元逻辑（遮挡剔除核心，第一版实现）**：

```
1. solid(p) = 采样 alpha > _AlphaCutoff                 // 该像素是否属于人物外观
2. 邻域膨胀：R = _OutlineWidth + _HoleRadius，扫描 (2R+1)² 邻域取 alpha 最大值 amax
   - 非 solid 且 amax > _AlphaCutoff → 本像素在膨胀带内 → 输出 _OutlineColor
     （输出 alpha = amax，继承图集边缘的抗锯齿渐变）
   - 发丝缝隙等宽 ≤ 2R 的内部细缝被描边色填充，不产生独立描边线
3. 其余像素 → 透明输出
```

> 第一版采用**单 pass 方形邻域膨胀**（每像素 ≤ 81 次采样）。原设想的严格 erode+dilate（opening，
> 缝隙处直接透明露出地面）在单 pass 中需要 (2H+1)⁴ 量级嵌套采样，性价比低；若实际观感需要
> "缝隙留白"语义，v2 再引入两 pass 中间 RT 方案。

**邻域采样边界**：所有采样 UV 一律 clamp 到 `_FrameUV`（C# 每帧经 MaterialPropertyBlock 传入该 pawn
帧的 uvRect 边界），防止膨胀采样越界读到图集中相邻 pawn 的帧。

**渲染状态**：`ZWrite Off`、`Blend SrcAlpha OneMinusSrcAlpha`（描边带不透明、其余透明）、`Cull Off`。

**性能**：每像素采样 ≈ 两次邻域扫描（各约 `(2R+1)²` 次） + 外扩扫描（`8 × W` 次）。描边层 mesh 仅覆盖目标 pawn 的屏幕区域（约几万像素），GPU 负担可忽略；CPU 每帧仅提交一次 `Graphics.DrawMesh`。

**AB 包约定**：
- 工程内路径：`Assets/Data/KiiroSchizonepetaGum/Materials/KiiroGumOutline.shader`。查找规则（已核实 `ContentFinder.TryFindAssetInModBundles` + `GenFilePaths.ContentPath<Shader>`）：完整 asset 路径为 `Assets/Data/<Mod文件夹名>/Materials/<shaderPath>.shader`，即 `ShaderTypeDef.shaderPath = KiiroGumOutline`（不带扩展名；**注意目录是 `Materials/` 而非 `Shaders/`**）。
- 导出 AB 包文件放入 mod 目录 `1.6/AssetBundles/`（启动时自动加载，见 `ModAssetBundlesHandler`）。**平台策略（已决策）：仅发布 Windows 版 `kiirogum_win`（必须带 `_win` 后缀）**——RimWorld 按当前 OS 匹配文件名后缀加载，非 Windows 平台加载不到该 shader，配合 C# 端平台判定（`KiiroGumOutlineManager.IsOutlineSupported`）整体不渲染描边，实现"仅 Windows 有此特效、其余平台静默"以减小包体。
- **Unity 版本建议 2022.3.33f1**（RimWorld 1.6 官方 Mod SDK 版本）。2022.3.58fc1 同主版本，AB 实际大多可加载，但官方不保证"新构建→旧运行时"的向前兼容，稳妥起见装 33f1。

### 4.2 C# 侧（mod 源码）

源码目录 `1.6/Source/KiiroSchizonepetaGum/HediffComp_Outline/`：

| 文件 | 类 | 职责 |
|---|---|---|
| `OutlineConfig.cs` | `OutlineConfig` | 模块常量：层高 -8、远景停用系数 0.7、宽度 1~4、色板、调试开关 |
| `HediffCompProperties_Outline.cs` | `HediffCompProperties_Outline` | XML 配置：描边颜色、宽度、孔洞半径（默认值） |
| `HediffComp_Outline.cs` | `HediffComp_Outline` | 生命周期：pawn 注册/注销到管理器 |
| `KiiroGumOutlineManager.cs` | `KiiroGumOutlineManager`（静态） | 目标列表管理；每帧渲染描边层的核心逻辑 |
| `KiiroGumOutlineGameComponent.cs` | `KiiroGumOutlineGameComponent : GameComponent` | 在 `GameComponentUpdate()` 调用 Manager 的每帧绘制 |

**关键绘制逻辑（已按 1.6 反编译源码核实）**：

```csharp
// 每帧对每个目标 pawn：
// 0. 全局早退：非 Windows / shader 加载失败 / 设置关闭 / 镜头过远（ZoomRootSize ≥ 相机最大 RootSize × 0.7）
if (Find.CameraDriver.ZoomRootSize >= Find.CameraDriver.config.sizeRange.max * 0.7f) return;  // 远景 pawn 过小，停用省性能
if (!GlobalTextureAtlasManager.TryGetPawnFrameSet(pawn, out PawnTextureAtlasFrameSet fs, out _, true))
    return;
int index = fs.GetIndex(pawn.Rotation, PawnDrawMode.BodyAndHead);
EnsureFrameFresh(pawn, fs, index);   // 脏帧主动烘焙（复制官方 GetBlitMeshUpdatedFrame L231-242；近景不依赖官方缓存路径，猫的唯一帧来源）
Material mat = MaterialPool.MatFrom(new MaterialRequest(fs.atlas, 描边Shader));  // MaterialRequest 支持 RenderTexture（官方同款）
Vector3 pos = pawn.DrawPos;
pos.y += PawnRenderUtility.AltitudeForLayer(-8f);       // 相对 body 层下移（官方 apparel extras 用 -10/90，取 -8 避开）
Matrix4x4 matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);  // 站立时官方 bodyAngle 恒为 0（PawnRenderer L432 已核实），identity 精确对齐
mpb.SetVector(ShaderID.FrameUV, new Vector4(rect.xMin, rect.yMin, rect.xMax, rect.yMax));  // 帧边界，供 shader clamp 邻域采样
GenDraw.DrawMeshNowOrLater(fs.meshes[index], matrix, mat, false, mpb);
```

> **缩放语义注意**：`ZoomRootSize` 值越大镜头越远（官方 PawnRenderer L439 的 `> 18` 是"拉远才用 atlas 缓存"）。本模块不再设近景门槛——外观变化由 `SetAllGraphicsDirty → TryMarkPawnFrameSetDirty` 无条件标记帧过期（L834-847，与缩放无关），配合主动烘焙，近景/中景/远景描边均与本体一致；仅"镜头过远"（pawn 屏幕尺寸过小、描边不可辨）时停用。

**接入现有设置**：`KiiroSchizonepetaGumSettings` 增加 `enableOutline / outlineColor / outlineWidth`，mod 设置界面增加对应控件（沿用现有 `Listing_Standard` 风格）。

### 4.3 XML 侧

| 文件 | 内容 |
|---|---|
| 新增 `1.6/Defs/ShaderTypeDefs/KiiroGum_Outline.xml` | `ShaderTypeDef`：`shaderPath = KiiroOutline`（引用 AB 包内 shader） |
| 修改 `1.6/Defs/HediffDefs/Hediff_ChewingGum.xml` | 在 `comps` 增加 `<li Class="KiiroSchizonepetaGum.HediffCompProperties_Outline">` |
| 语言文件 | 新增 Keyed 翻译：设置项标签/描述（中英） |

## 5. 遮挡剔除原理（对应需求）

| 需求 | 实现机制 |
|---|---|
| 衣物遮住身体 → 不描边 | atlas 帧 = 合成结果，被衣服覆盖处已是衣服像素；描边检测的是合成图最外层 alpha 边缘 |
| 内部褶皱/发丝不描边 | shader 内 erode+dilate：先腐蚀填平直径小于 `2×_HoleRadius` 的内部孔洞与细缝，再膨胀恢复整体尺寸，仅剩最外层轮廓 |
| 场景物体遮挡 | 描边层位于 pawn body 层之下（y 更低 + 绘制顺序更早），上层物体自然覆盖其内部；行为与原版 pawn 渲染一致（2D 层排序，非真实深度） |
| 不覆盖人物像素 | 外描边带判定在 `S'` 外侧，人物内部像素不输出描边色 |

## 6. 性能分析

| 环节 | 开销 |
|---|---|
| CPU | 每目标 pawn 每帧一次：字典查询（frameSet）+ 材质查找（MaterialPool 缓存）+ 一次 `Graphics.DrawMesh` 提交；无逐像素计算 |
| GPU | 描边 shader 片元约百次采样，但仅覆盖 pawn 屏幕区域（≈几万像素），远小于全屏后处理；合批由引擎材质排序承担 |
| 帧新鲜度 | 依赖官方 atlas 缓存（仅外观/动画变化时重烘焙），不额外增加烘焙频率 |
| 设置关闭 | `enableOutline=false` 时 Manager 直接跳过，零开销 |

## 7. 实施步骤

**阶段 0（前置，用户进行）**：安装 Unity 2022.3.33f1；创建/打开 RimWorld Mod SDK Unity 工程；配置 mod 名称与 AB 导出路径（`1.6/AssetBundles/`）。

**阶段 1 — C# 渲染框架（已完成）**：
1. 编写 `HediffCompProperties_Outline` / `HediffComp_Outline` / `KiiroGumOutlineManager` / `GameComponent` / `OutlineConfig`。
2. 调试日志体系（`OutlineConfig.DebugLogging`）：注册/全局跳过原因/shader 加载/烘焙/绘制提交全链路埋点，同 key 去重防刷屏。
3. 修改 csproj、设置类、XML comp、语言文件。

**阶段 2 — 自定义描边 shader（Unity 侧，已完成）**：
4. 编写 `KiiroGumOutline.shader`（邻域膨胀 + 外描边带；`tex2Dlod` 采样以避开"变长循环内梯度指令"编译错误）。
5. Unity 中导入，配置 `ShaderTypeDef` 路径，用导出脚本（`UnityProject/Assets/Editor/BuildKiiroGumAssetBundles.cs`，自动清空输出目录防旧包残留）导出 `kiirogum_win` 到 `1.6/AssetBundles/`。
6. shader 加载失败时整体停用（不回退 Cutout，见风险表）。

**阶段 3 — 参数化与打磨**：
7. mod 设置：描边开关/颜色/宽度；shader 参数通过 `MaterialPropertyBlock` 或 `MaterialRequest` 传入。
8. 多朝向、躺姿、死亡、远行队、暂停等边界情况验证。

## 8. 风险与对策

| 风险 | 对策 |
|---|---|
| 2022.3.58 构建的 AB 与游戏运行时（33f1）不兼容 | 安装 33f1 构建；若坚持 58 需实测加载是否报错 |
| `bodyAngle`（含 idle 摆动角度）为 PawnRenderer 私有，描边层角度只能近似 | **已消除**：核实 PawnRenderer L432，站立时官方 bodyAngle 恒为 0（仅躺卧非 0，而躺卧走渲染树不描边），描边层用 identity 旋转即精确对齐 |
| 非类人/特殊姿态 pawn 走渲染树路径，atlas 帧可能缺失或与本体视觉不一致 | **已解决（支持猫）**：核实 `PawnTextureAtlas.TryGetFrameSet` 对 pawn 类型零限制（L72），`PawnCacheRenderer.RenderPawn → RenderCache` 对任何 pawn 走同一渲染树（PawnRenderer L519）——非类人目标（猫）由 `EnsureFrameFresh` 主动烘焙获得合成帧后同样描边；躺卧/死亡等特殊姿态仍跳过（本体走渲染树特殊路径） |
| 帧新鲜度渲染时序与官方烘焙交错 | **已解决（主动烘焙）**：复制官方 `GetBlitMeshUpdatedFrame` L231-242 逻辑（`EnsureFrameFresh`）；本模块在 `GameComponentUpdate`（早于相机渲染）烘焙并清 dirty，官方本体渲染时直接复用，共用同一帧，无竞态、无重复烘焙 |
| shader 加载失败（AB 缺失/XML 缺失） | **已决策：直接停用描边**。回退 shader 画出的层与本体同 mesh/同位置且在下层，被本体逐像素覆盖完全不可见，只会浪费每帧 DrawMesh；通过引用比较检测官方隐性回退 `DefaultShader`（ShaderDatabase L106）后置 null 早退 |
| 描边层穿墙/穿地形 | 与 pawn 本体同为 2D 层排序，行为与原版一致，属引擎固有行为，不做额外处理 |
| atlas 图集整体重建导致旧材质引用失效 | MaterialPool 按 (texture, shader) 键缓存，图集重建后新键自动生成新材质 |

## 9. 测试验证计划

1. **基础**：类人 pawn 获得口香糖 hediff → 外描边出现；移除/消退 → 描边消失。**猫（动物）食用后同样有描边**（非类人主动烘焙路径）。
2. **合成外观**：换装（外套/披风/头饰）→ 描边跟随最外层变化；内部褶皱不描边。
3. **朝向/姿态**：四朝向正常；躺卧、倒地、死亡状态暂不描边（渲染树特殊路径，属已知取舍）。
4. **遮挡**：衣物遮身不外露；发丝缝隙不描边。
5. **缩放**：近景/中景均渲染（主动烘焙，不依赖官方 atlas 缓存条件）；镜头拉到最远两档（≥ 相机最大 RootSize × 0.7）描边停用。
6. **平台/AB**：Windows 正常；AB 缺失时控制台输出停用警告且完全无渲染（无回退副作用）。
7. **性能**：数十个目标 pawn 时帧耗时无明显变化；关闭开关后零开销。
8. **设置**：颜色/宽度实时生效并持久化。
