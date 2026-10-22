# 3. 动画 mod 导致描边与本体错位

> [← 返回总览](00-总览.md)
> **来源**：2026-10-22 用户「创意工坊有个很热门的人物动画 mod 会让小人执行动画，我们 mod 的 shader 没跟上导致描边与本体重合不上，看看有没有办法适配」
> **口径**：**已实施**：2026-10-23 实机验收通过（位置/旋转/帧朝向跟随动画 mod）；描边图层遮挡关系未通过，见 [§3.6](#36-决策表) D6
> **范围**：`KiiroSchizonepetaGum` 1.7 描边层的位置 / 旋转 / 帧朝向；不改着色器算法与烘焙流程
> **关联**：着色器侧迁移见 [01-特效着色器迁移.md](01-特效着色器迁移.md)；1.6 描边算法与选型见 [Outline_技术方案.md](Outline_技术方案.md)

## 3.1 诉求与现象

目标 mod 为 Yayo's Animation（Continued），工坊 ID `2877292196`，工作区路径
`e:\steam\steamapps\workshop\content\294100\2877292196`（随包附源码）。

| 场景 | 现状 | 期望 |
|---|---|---|
| 装了动画 mod 的 pawn 做呼吸/走路/后坐力动画 | 本体有位移与轻微倾斜，描边层仍钉在 `pawn.DrawPos` + 正立 | 描边与本体同步位移、同步倾斜 |
| 攻击/瞄准时朝向被动画锁定 | 描边取 `pawn.Rotation` 对应的图集帧，本体取锁定朝向的帧 | 两者用同一帧 |
| 未装动画 mod | 描边正常 | 不变 |

## 3.2 机制

**动画 mod 怎么做**（三个 Harmony patch，全部只改 `PawnRenderer` 的输出）：

```csharp
// GetBodyPosPatch.cs L14-L19：本体绘制位置追加偏移
public static void Postfix(PawnRenderer __instance, ref Vector3 __result, ...)
    => __result += ___pawn.GetData().posOffset;

// BodyAnglePatch.cs L13-L17：卧姿角度追加偏移
public static void Postfix(PawnRenderer __instance, ref float __result, Pawn ___pawn)
    => __result += ___pawn.GetData().angleOffset;

// ParallelGetPreRenderResultsPatch.cs L16-L27：锁定朝向
rotOverride = data.fixedRot ?? rotOverride;
// 同文件 L31-L50：Transpiler 把站立时的 bodyAngle 常量 0f 换成 angleOffset
```

**1.7 官方的对应机制**：`PawnRenderer` 把「本帧实际用于绘制的参数」写进私有字段
`results`，atlas 绘制路径直接消费这三个值（[PawnRenderer.cs](#39-证据表) L321-L326）：

```csharp
Rot4 facing = this.results.parms.facing;
Vector3 bodyPos = this.results.bodyPos;
float bodyAngle = this.results.bodyAngle;
GenDraw.DrawMeshNowOrLater(mesh, bodyPos, Quaternion.AngleAxis(bodyAngle, Vector3.up), mat, false);
```

而这三个值在 `ParallelGetPreRenderResults`（L402 写入）里由 `GetBodyPos` / `BodyAngle` /
`rotOverride` 算出——正是动画 mod 打补丁的三个点。**结论：`results` 是「本体最终画在哪、
转多少度、用哪一帧」的唯一真相源；但它在 `RenderPawnAt` 末尾即被清零，见 §3.3。**

`PawnCacheRenderer.RenderPawn` 走的 `RenderCache`（L467-L524）只用局部 `PawnDrawParms`，
**不写 `results`**——因此我们主动烘焙图集帧不会污染描边读取到的值。

## 3.3 现状排查

| 对象 | 结论 |
|---|---|
| 描边位置 | `pawn.DrawPos + AltitudeForLayer(-8)`，不含动画 `posOffset` |
| 描边旋转 | `Quaternion.identity`，不含 `angleOffset` |
| 帧朝向 | `frameSet.GetIndex(pawn.Rotation, ...)`，不含 `rotOverride`（`fixedRot`） |
| 帧烘焙 | `EnsureFrameFresh` 以 `pawn.Rotation` 烘焙，锁朝向时烘焙的帧与取用的帧不匹配 |
| `results` 生命周期 | `RenderPawnAt` 末尾（`PawnRenderer.cs#L374`）把 `results` 重置为 `default`；本模块在 `Application.onBeforeRender` 提交绘制，此时 `results.valid` 恒为 false → 实际主路径不是它 |
| 描边层海拔 | 原 `-8`（沉到本体下方）；1.7 着色器已自行挖空内部，应改为本体上方一层，改后取 `1`（`pos.y += AltitudeForLayer(1)`） |
| 同坑面 | 任何修改 `PawnRenderer.GetBodyPos` / `BodyAngle` / `rotOverride` 的 mod（GiddyUp、自动枪战、持械显示类）表现相同 |
| 已排除 | 着色器算法、图集帧烘焙精度、着色器未加载——错位只发生在动画 mod 生效时 |

## 3.4 候选做法

| 做法 | 落点 | 覆盖 | 副作用 | 结论 |
|---|---|---|---|---|
| A 读 `PawnRenderer.results`（私有字段，表达式树读） | `KiiroGumOutlineManager` | 所有改 `PawnRenderer` 的动画 mod | 依赖私有字段名，需兜底 | ✅ 推荐 |
| B 反射读 yayoAni 的 `PawnDrawData`（`posOffset`/`angleOffset`/`fixedRot`） | 同上 | 仅 yayoAni | 需按 mod 逐个适配；该 mod 仅到 1.6 | ❌ 原判（被 B′ 取代） |
| B′ 同上，作为 `results` 失效时的次选来源（反射读 `PawnDrawData`，复现动画 mod 的三处注入） | `KiiroGumOutlineManager` | 仅 yayoAni 系 | 需按 mod 逐个适配；无动画 mod 时探针为空，直接落到兜底 | ✅ 采用（2026-10-23） |
| C 硬引用 `yayoAni.dll` | csproj | 仅 yayoAni | 变成强制依赖，缺 mod 即 `TypeLoadException` | ❌ |
| D 不改，接受错位 | — | — | 装了动画 mod 的用户描边视觉失效 | ❌ |

## 3.5 影响面与副作用

| 维度 | 影响 |
|---|---|
| 兼容性 | 不新增程序集引用；反射解析失败时打一次警告并回退旧行为 |
| 性能 | 每目标每帧 4 次已编译委托调用，无装箱、无 GC |
| 存档 | 不新增持久化字段，无 Scribe 变更 |
| 加载序 | 对其它 mod 无加载顺序要求 |
| 显示 | 描边跟随位移/旋转/朝向；来源链 `results` → `PawnDrawData` → `pawn.DrawPos`，实际生效的是中间一级 |
| 遮挡 | 描边层海拔改为 `1`（本体上方），与低矮植被的前后遮挡关系仍不符合预期（未结，见 D7） |
| 回滚 | 删除探针类、恢复三处取值即可，不影响其它功能 |

## 3.6 决策表

| # | 决策点 | 结论 | 状态 |
|---|---|---|---|
| D1 | 适配信息来源 | 读 `PawnRenderer.results`，不依赖具体动画 mod 的程序集 | 已修订（见 D5） |
| D2 | 私有字段读取方式 | 表达式树编译成强类型委托（避免每帧 `FieldInfo.GetValue` 装箱） | ✅ 已拍板（2026-10-22） |
| D3 | 读取失败时的行为 | 回退 `pawn.DrawPos` + identity + `pawn.Rotation`，并打一次警告 | ✅ 已拍板（2026-10-22） |
| D4 | 是否给玩家开关 | 不新增开关：读取无副作用，失败自动回退 | ✅ 已拍板（2026-10-22） |
| D5 | `results` 在提交绘制时恒为 `default` 怎么办 | 姿态来源改为三级链：`results`（首选）→ `AnimationDrawDataProbe`（反射读 `PawnDrawData`，复现 `GetBodyPos`/`BodyAngle`/`PreRender` 三处注入）→ `pawn.DrawPos` 兜底 | ✅ 已拍板（2026-10-23） |
| D6 | 描边层海拔 | 由 `-8` 改为 `1`（本体上方一层，着色器已自行挖空内部）；实测遮挡关系仍未符合预期，见 D7 | ✅ 已拍板（2026-10-23） |
| D7 | 描边与低矮植被的遮挡关系如何修 | 未定：海拔/`renderQueue`/`depthTest` 三者取舍待排查 | ⏳ 待拍板 |

## 3.7 实施清单

| # | 落点 | 改动 | 验证点 |
|---|---|---|---|
| 1 | `KiiroGumOutlineManager` 内新增 `RenderResultProbe` | 反射解析 `results`，编译 `valid`/`bodyPos`/`bodyAngle`/`parms.facing` 读取委托 | 启动无警告，日志不出现回退提示 |
| 2 | `DrawOutlineFor` | 位置取 `results.bodyPos`，旋转取 `Quaternion.AngleAxis(bodyAngle, Vector3.up)` | 动画中描边与本体轮廓重合 |
| 3 | `DrawOutlineFor` | 帧索引改用 `results.parms.facing` | 锁定朝向时帧一致 |
| 4 | `EnsureFrameFresh` | 烘焙时传入实际 `facing`（而非 `pawn.Rotation`） | 锁朝向时不出现旧帧残留 |
| 5 | 实机验证 | 装 Yayo's Animation 后观察走路/攻击/卧姿 | 无错位、无一帧抖动、无描边残影 |
| 6 | `KiiroGumOutlineManager` 内新增 `AnimationDrawDataProbe` | 反射解析 yayoAni 的 `PawnDrawData`，读 `posOffset`/`angleOffset`/`fixedRot`，作为 `results` 失效时的次选来源 | 装动画 mod 后描边随呼吸/走路/后坐力同步位移与倾斜 |
| 7 | `OutlineConfig.OutlineAltitudeLayer` | `-8` → `1`，`pos.y += AltitudeForLayer(1)` | 描边不再沉到本体下方 |
| 8 | 实机验收（2026-10-23） | 装 Yayo's Animation 验证位移/倾斜/锁定朝向 | ✅ 位移与倾斜跟随通过；❌ 与低矮植被遮挡关系不符预期（见 D7） |

## 3.8 变更日志

| 日期 | 变更 | 依据 | 波及节 |
|---|---|---|---|
| 2026-10-22 | 新建本篇：定位动画 mod 错位根因并提出读 `results` 的通用方案 | 用户「看看有没有办法适配」+ 双方源码核对 | 全篇 |
| 2026-10-22 | D1–D4 拍板并落地：探针类、位置/旋转/朝向取值、烘焙朝向 | 用户「进行实现」 | §3.6、§3.7 |
| 2026-10-23 | 方案更正：`results` 在 `onBeforeRender` 阶段恒为 `default`，主路径改为 `AnimationDrawDataProbe`（D5）；描边海拔 `-8` → `1`（D6） | 源码核对 `PawnRenderer.cs#L374` + 实机错位复现 | §3.3、§3.4、§3.6、§3.7 |
| 2026-10-23 | 实机验收：描边跟随动画（位移/倾斜/锁定朝向）通过；描边图层遮挡关系未通过，登记为 D7 | 用户「除了描边图层遮挡关系均测试通过」 | §3.6 |

## 3.9 证据表

| 环节 | 依据 |
|---|---|
| 动画位移 | `workshop\content\294100\2877292196\Source\HarmonyPatches\PawnPatches\GetBodyPosPatch.cs#L14-L19` |
| 动画角度 | 同目录 `BodyAnglePatch.cs#L13-L17` |
| 锁定朝向 | 同目录 `ParallelGetPreRenderResultsPatch.cs#L16-L27`、`#L31-L50` |
| 动画数据字段 | `Source\Data\PawnDrawData.cs#L6-L13` |
| 1.7 结果字段 | `Source_Decompiled\1.7\Assembly-CSharp\Verse\PawnRenderer.cs#L948` |
| 结果结构 | 同文件 `#L954-L979`（`valid`/`bodyPos`/`bodyAngle`/`parms`） |
| 结果写入 | 同文件 `#L402`（`ParallelPreRenderPawnAt`） |
| `results` 每帧清零 | 同文件 `#L374`（`RenderPawnAt` 末尾重置为 `default`） |
| 动画位移注入点 | `workshop\content\294100\2877292196\Source\HarmonyPatches\PawnPatches\GetBodyPosPatch.cs#L17`（`__result += pdd.posOffset`） |
| 动画朝向注入点 | 同目录 `ParallelGetPreRenderResultsPatch.cs#L26`（`rotOverride = data.fixedRot ?? rotOverride`） |
| 描边层海拔取值 | `1.7\Source\KiiroSchizonepetaGum\HediffComp_Outline\OutlineConfig.cs`（`OutlineAltitudeLayer = 1`） |
| 官方 atlas 消费 | 同文件 `#L321-L326` |
| 烘焙不污染 | 同文件 `#L467-L524`（`RenderCache` 不写 `results`） |
| 朝向结构 | `Source_Decompiled\1.7\Assembly-CSharp\Verse\PawnDrawParms.cs#L75` |
| 目标 mod 版本 | `workshop\content\294100\2877292196\About\About.xml#L6-L11`（仅 1.3–1.6） |
