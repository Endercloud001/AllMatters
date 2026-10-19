# RitsuLib 双分支兼容调查

调查日期：2026-10-20。目标 API：public 0.107.1 与 public-beta 0.111.0；游戏分支实际版本及游戏 API 对比由同目录的版本调查记录负责。本报告核实 RitsuLib 的支持声明与加载机制，没有运行游戏或构建参考仓库。

## 结论与采用方式

**可采用 RitsuLib 0.6.7，两个游戏分支使用同一版本的库、各自对应的编译包。** 玩家运行时可安装完整 variant-pack，由库选择对应兼容模块。AllMatters 初期建议分别构建 public 与 beta：RitsuLib 的运行时选型解决库自身的差异，不会自动修复 AllMatters 直接调用游戏 API 的差异，也不能据此宣称一个 AllMatters DLL 已兼容两分支。

| 目标 | 编译期引用 | 运行时 |
| --- | --- | --- |
| public / 0.107.1 | `STS2.RitsuLib.Compat.0.107.1`，版本 `0.6.7` | 对应 compat runtime 包，或完整 variant-pack |
| public-beta / 0.111.0 | `STS2.RitsuLib`，版本 `0.6.7` | 默认 runtime 包，或完整 variant-pack |

证据：[v0.6.7 Release](https://github.com/BAKAOLC/STS2-RitsuLib/releases/tag/v0.6.7) 实际列出了主包和 `Compat.0.107.1` 的 `.nupkg`、`.github.zip` 及 `STS2-RitsuLib.0.6.7.variant-pack.zip`；[v0.6.7 的 Compatibility.props](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/build/RitsuLib.Compatibility.props#L3-L6) 声明目标 `0.111.0;0.110.0;0.109.0;0.107.1`，最高 API 为 `0.111.0`，共享模块基于 `0.107.1`。调查时 GitHub `main` 的同一配置也一致。

这证明**发布资产存在且源码声明支持**，仍需区别于实际编译和运行证据。本轮后续已下载 variant-pack，并使用其 RitsuLib.References.props 对两版进行接入点编译；结果见下文。未单独验证 nuget.org feed 的 RitsuLib 包可用性，未进行游戏内运行验证。

## 加载与安装

本地参考源码位置均相对 `AllMatters_docs/references/STS2-RitsuLib/`。

- `README.zh.md:70-101` 说明编译包与运行包的区分。玩家需保留完整 `mods/STS2-RitsuLib/`：根加载器、`shared/`、`compat/<api>/`、模块清单及 `assets.zip`，不能只复制一个 DLL。[上游安装说明](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/README.zh.md#L70-L101)
- `components/loader/Bootstrap.cs:47-80` 读取宿主版本，解析模块布局，在同一程序集加载上下文加载 Shared、Ui、Settings、Runtime 与兼容入口，然后注册 Godot 脚本并初始化框架。[Bootstrap](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/components/loader/Bootstrap.cs#L47-L80)
- `components/loader/BundleLayout.cs:46-53` 选择**小于等于宿主版本的最高目标**。已知宿主 `0.107.1` 会选 `0.107.1`，`0.111.0` 会选 `0.111.0`；宿主版本未知时会选包内最高目标。此算法不是对未来游戏版本的兼容保证。[BundleLayout](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/components/loader/BundleLayout.cs#L46-L53)
- `src/Compat/Sts2HostVersion.cs:35-69` 优先读取 `ReleaseInfoManager`，再尝试发布/launcher 元数据，最后回退游戏程序集版本。因此游戏内日志的 `Host=...; runtime=...` 是实际所选分支的直接检查点。[Sts2HostVersion](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/Compat/Sts2HostVersion.cs#L35-L69)
- `Bootstrap.cs:129-158` 优先调用游戏 `ModManager.AssociateAssemblyWithMod`；旧宿主无此方法时尝试修改 Mod 的 assemblies 列表，仍失败则补 reflection bridge。可见源码考虑了两 API 的模型类型发现差异。[程序集关联回退](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/components/loader/Bootstrap.cs#L129-L158)

上游 BundleLayout 已包含其模块一致性验证逻辑；本项目无需新增一套同类机制，也不应拆散或改写上游安装包。

两个目标均高于 `0.105.x`，manifest 可使用对象依赖：`"dependencies": [{ "id": "STS2-RitsuLib" }]`。[manifest 示例](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/README.zh.md#L46-L66)

## 与 v0.1 相关的接入点

### 事件、遗物和模型注册

`src/Scaffolding/Content/ModContentPackBuilder.cs` 提供 `Power<T>()`（约 666 行）、`SharedEvent<T>()`（843 行）、`ActEvent<TAct,TEvent>()`（852 行）、遗物注册和 `Apply()`（1911 行）。底层 `src/Content/ModContentRegistry.cs:1293-1374` 区分共享事件与限定章节事件；选择哪一种取决于各移植事件的出现条件，不能把全部事件统一注册进所有章节。[Builder](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/Scaffolding/Content/ModContentPackBuilder.cs#L843-L858)、[Registry](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/Content/ModContentRegistry.cs#L1293-L1374)

`ModEventTemplate` 继承游戏 `EventModel`，提供本地化键、遗物选项、portrait/layout/background/VFX 资源接口；没有包装完整的事件战斗同步和恢复流程。因此它适合公共事件表现层，涉及竞技场、强盗、心灵绽放等事件内战斗时，仍需适配游戏 `EventModel` 和战后房间流程。[ModEventTemplate](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/Scaffolding/Content/ModEventTemplate.cs)

### 能力钩子与战后通知

RitsuLib 提供 `SubscribeLifecycle<TEvent>`，返回值 dispose 可取消订阅；其 `Action<TEvent>` 回调是通知接口，不是可等待的异步游戏动作接口。新增能力优先覆写游戏 `PowerModel` 的相关钩子；「你好世界」需单独比较两分支的实际 `HelloWorldPower.BeforeHandDraw`，不要凭泛化的 SideTurnStartingEvent 替换原版执行顺序。[SubscribeLifecycle](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/RitsuLibFramework.cs#L242-L269)

`src/Lifecycle/CombatLifecycleContracts.cs:1-5` 的预处理仅切换旧版 `CombatState` / 新版 `ICombatState` 类型别名；两个目标都会保留 `CombatEndedEvent` 与 `CombatVictoryEvent`。`src/Lifecycle/Patches/CombatHookLifecyclePatches.cs:57-110` 在原版 `Hook.AfterCombatEnd` / `Hook.AfterCombatVictory` 返回的 Task 完成后发布通知。此顺序可用于清理或观察，不能单独保证事件恢复、奖励和多人同步正确。[战斗事件定义](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/Lifecycle/CombatLifecycleContracts.cs#L1-L101)、[发布时机](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/Lifecycle/Patches/CombatHookLifecyclePatches.cs#L57-L110)

框架还包含 `NCombatUiAnimOutCompat`，以反射兼容无参数 `AnimOut()` 与旧的 `AnimOut(CombatRoom)`；它是 internal 且用于 game-over 处理，不应把它当作 AllMatters 可以直接调用的事件恢复 API。[兼容实现](https://github.com/BAKAOLC/STS2-RitsuLib/blob/v0.6.7/src/Compat/NCombatUiAnimOutCompat.cs)

## 验证边界与下一步

1. 已核实 GitHub 最新发布为 `v0.6.7`（2026-10-08 UTC 发布），对应资产与兼容目标存在；已阅读本地源码及 GitHub 主分支/tag 配置。
2. 未运行 RitsuLib 源码脚本，未修改其源码，也未在此次调查构建 RitsuLib。
3. 将两个编译包分别用于双目标构建，检查 API 不同的直接调用；运行库使用同一个完整 variant-pack。
4. 安装游戏后分别确认加载日志中 Host/runtime 正确，再测试第一个简单事件、事件遗物获得、你好世界回合钩子。事件战斗恢复的多人/保存测试留到对应内容实现时进行。
5. 若希望最终只提供一个 AllMatters DLL，需要在 AllMatters 的实际直接调用面上证明二进制兼容；目前依据不足，先保留分别构建方案。

## 本机后续编译证据

主任务已下载并解压 `STS2-RitsuLib.0.6.7.variant-pack.zip` 到 `AllMatters_docs/local/dependencies/RitsuLib-0.6.7/`。使用包内 `RitsuLib.References.props`，分别设置 `RitsuLibReferenceTarget=0.107.1` 与 `0.111.0`；public 对应引用包及 GitHub 实际程序集、beta 本机实际程序集的接入点编译均为 0 警告、0 错误。

探针覆盖 CreateLogger、ModTypeDiscoveryHub.RegisterModAssembly、EnsureGodotScriptsRegistered、RegisterSharedEvent、EventModel 子类、PowerModel.BeforeHandDraw 与 AddGeneratedCardsToCombat。探针只编译未执行；AllMatters 生产项目尚未增加 RitsuLib 依赖，也尚未运行框架。详见 [双分支 API 报告](dual-branch-api.md) 的验证表和本地日志。
