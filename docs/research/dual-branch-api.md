# public / public-beta API 准备与比较

日期：2026-10-20。对应实施计划第 1 项。**已补齐 public 的实际程序集参考、双目标编译引用、关键 API 比较和 RitsuLib 候选运行包，并选择分别构建方案；两分支游戏内运行验证仍未完成。**

## 版本与资料来源

本次重新查询 [Steam 分支元数据](https://api.steamcmd.net/v1/info/2868840)：public Build ID 为 23811903，public-beta 为 24724944。GitHub 的 [BSchneppe/sts2-ref-assemblies](https://github.com/BSchneppe/sts2-ref-assemblies) 发布了匹配 public 0.107.1 与 beta 0.111.0 的开发引用。本机 beta 的 release_info.json 为 v0.111.0，commit 与 beta 包版本后缀一致。

进一步从 [iryuko/sts2-mod-dev 的 x86_64 游戏引用目录](https://github.com/iryuko/sts2-mod-dev/tree/main/references/game-dlls/sts2/x86_64) 下载了带方法实现的 sts2.dll，以及 shared 目录中的 0Harmony.dll、GodotSharp.dll。该仓库的 [release_info.json](https://github.com/iryuko/sts2-mod-dev/blob/main/references/api-notes/app/release_info.json) 标记 v0.107.1 / commit 59260271；下载 DLL 的 ProductVersion 包含相同构建提交号，关键事件与卡牌 API 也与 0.107.1 引用包一致。这构成可用的 public 实现参考；来源是第三方 GitHub 开发仓库，不是本机经 Steam 安装验证的完整 public 游戏。

| 材料 | 本地位置（相对 AllMatters_docs） | 证据能力 |
| --- | --- | --- |
| public 引用包 0.107.1 | local/api/0.107.1/ref/net9.0/ | sts2.dll、0Harmony.dll、GodotSharp.dll；可比较签名、编译，不能运行游戏或观察方法实现 |
| public 实际程序集参考 | local/api/public-github-0.107.1/ | sts2.dll 含方法实现；构建提交号与上游所存 v0.107.1 元数据一致；附带两份依赖 DLL 和 release_info.json，不能视为完整游戏安装 |
| beta 引用包 0.111.0-beta-41cef1ea | local/api/0.111.0-beta-41cef1ea/ref/net9.0/ | 同上；用于与 public 做同类引用比较 |
| 本机 beta 真实程序集 | E:/SteamLibrary/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64/ | 可核对实际方法体；本次只读，未更改游戏安装 |
| RitsuLib 0.6.7 variant-pack | local/dependencies/RitsuLib-0.6.7/ | 已下载并解压，包含 shared、两个目标的 compat 及其他原包内容；尚未安装或运行 |

来源：[引用包说明](https://github.com/BSchneppe/sts2-ref-assemblies/blob/main/README.md)、[public tag](https://github.com/BSchneppe/sts2-ref-assemblies/tree/v0.107.1/ref)、[beta tag](https://github.com/BSchneppe/sts2-ref-assemblies/tree/v0.111.0-beta-41cef1ea/ref)、[public NuGet 包](https://www.nuget.org/packages/BSchneppe.Sts2.ReferenceAssemblies/0.107.1)、[beta NuGet 包](https://www.nuget.org/packages/BSchneppe.Sts2.ReferenceAssemblies/0.111.0-beta-41cef1ea)、[RitsuLib 发布包](https://github.com/BAKAOLC/STS2-RitsuLib/releases/tag/v0.6.7)。引用包文件来自 NuGet；GitHub 提供源码、版本 tag 与生成方式。没有把第三方上传的完整游戏目录当成已验证的 public 安装。

这些包的 DLL 是去除实现的引用，不能将它们复制进游戏替换原文件。引用包还可能省去冗余的 override 定义，因此“某个派生类的导出文件没有该方法”不代表真实类没有覆盖该方法。

## 比较方法与本地证据

在 `local/tools/` 安装了 ilspycmd 9.1.0.7988，未安装全局工具。使用仓库 `tools/ExportApiReference.ps1` 导出 23 个相关类型的签名，分别保存在两版 `local/api/<version>/signatures/`。按声明行进行初步比较后人工核对关键方法，差异摘录保存在 `local/api/api-differences.json`。这不是完整 API 兼容性证明。

类型覆盖：EventModel、ActModel、ModelDb、ModHelper、ModManager、CardModel、PowerModel、RelicModel、AbstractModel、HelloWorld、HelloWorldPower、CardCmd、CardPileCmd、PowerCmd、RelicCmd、RewardsCmd、CombatRoom、EventRoom、RunManager、EventSynchronizer、CombatReplayWriter、RewardsSet、CombatManager。

public 实际程序集的 23 个类型实现导出在 `local/api/public-github-0.107.1/implementation/`。本机 beta 额外导出了 HelloWorld、HelloWorldPower、EventRoom、CombatRoom、RunManager 的实际实现到 `local/api/installed-beta/`。本地反编译文件、引用包和运行包均已被 docs/local 忽略，不会进入公开仓库或 Mod 安装包。

重新导出的示例（仓库根目录）：

```powershell
./tools/ExportApiReference.ps1 `
  -AssemblyPath './docs/local/api/0.107.1/ref/net9.0/sts2.dll' `
  -OutputDir './docs/local/api/0.107.1/signatures' `
  -IlspyPath './docs/local/tools/ilspycmd.exe'
```

## 与移植直接相关的 API

以下类型都在 `MegaCrit.Sts2.Core` 下；签名摘录省略命名空间和部分返回值。

| 位置 | public 0.107.1 | beta 0.111.0 | 对 AllMatters 的影响 |
| --- | --- | --- | --- |
| EventModel.BeginEvent | `(Player, bool isPreFinished)` | `(Player, EventCombatSynchronizer?, bool isPreFinished)` | 初始化调用与 Harmony 目标不能照搬 |
| EventCombatSynchronizer | 类型不存在于该引用程序集 | 存在 | 上游连续事件战斗补丁只能作为 beta 参考 |
| EventModel 内部战斗状态 | `_combatStateForCombatLayout`；有 GenerateInternalCombatState / ResetInternalCombatState | `_combatSynchronizer`；前述两个方法不存在 | public 连续战斗重置流程需要从真实实现重新核对 |
| EventSynchronizer 构造 | 玩家集合之后为 localPlayerId、uint seed | 增加 IRunState；seed 为 ulong | 若直接构造同步器，需要分目标代码 |
| EventSynchronizer | 无下面两个方法 | 增加 GenerateInternalCombatStateIfNecessary / BeforeExitingRoom | 不得从 beta 调用面推断 public 房间清理流程 |
| ModManager | 无 AssociateAssemblyWithMod | 有 `(string modId, Assembly)` | 通过 RitsuLib 的程序集关联兼容层注册模型 |
| ModelDb.Init | 无参数 | `Type[]? injectedModelTypes = null` | Harmony 方法定位及模型初始化接入需区分签名 |
| CardPileCmd.Add，集合+CardPile 重载 | 最后参数 skipVisuals | 末尾新增 isChangingOwners | 即使参数有默认值，编译后的调用签名也不同 |
| CardCmd.Exhaust | 返回 Task | 返回 Task<CardPileAddResult?> | 检查死灵诅咒等消耗链和 Harmony 补丁返回类型 |
| CardModel.CreateDupe | 无参数 | 要求 Player newOwner | 只有用到复制卡牌时才在相应调用处适配 |
| AbstractModel 的牌堆结果修改钩子 | PileType + CardPilePosition | 改为 CardLocation | 若移植依赖覆盖这些钩子，分别编译对应覆盖方法 |
| AbstractModel 的伤害修改钩子 | 无 CardPlay 参数 | 新增 CardPlay? 参数 | 与怪物、能力相关的覆盖方法须逐项检查 |

以下关键接口在本次签名比较中一致：

- `BeforeHandDraw(Player, PlayerChoiceContext, ICombatState)`，以及 `AfterPlayerTurnStart` 系列。
- `ModHelper.AddModelToPool`，事件的 `IsAllowed`、`GenerateInitialOptions`、`Resume(AbstractRoom)`。
- `CardPileCmd.AddGeneratedCardToCombat` / `AddGeneratedCardsToCombat`，及常用的单卡 Add 到牌堆重载。
- `EventRoom.Resume(AbstractRoom, IRunState?)`、`EventSynchronizer.ResumeEvents(AbstractRoom)`、`RewardsSet.WithRewardsFromRoom(AbstractRoom)`。

同名同签名不能证明执行时序或私有字段一致。特别是 EventRoom.Resume 的签名没变，内部事件战斗模型却已经变化。

两个引用包都存在 `AfterSideTurnEnd`；不能直接采用其他 Mod 文档关于“public 一定使用 AfterTurnEnd”的描述替代本次程序集证据。

## 你好世界的实现依据

两版实际程序集导出的 HelloWorld 和 HelloWorldPower 文件逐行比较均无差异。HelloWorld.OnPlay 在播放能力动画后应用 1 层 HelloWorldPower；OnUpgrade 只添加 Innate。HelloWorldPower 是 Counter 能力，在 BeforeHandDraw 里检查所属玩家和 AmountOnTurnStart，使用角色卡池中已解锁的 Common 卡牌、CardMultiplayerConstraint 与 RunState.Rng.CombatCardGeneration，经 CardFactory.GetDistinctForCombat 生成，再调用 AddGeneratedCardsToCombat 加到手牌。

据此，不能只在卡牌升级方法中改描述：现有能力并未显式区分来源卡是否升级，需要在实现任务中明确区分两类来源并保留原版随机流程。两版共同拥有上述回合钩子和生成命令，是可以优先复用的接入位置。固有是否保留、两种能力同时存在时如何展示和叠加仍属于待明确的玩法决定。

## 连续事件战斗的具体风险

上游 `Sts2BalanceModCode/Patches/Events/ColosseumCombatPatch.cs` 与 `MindBloomCombatPatch.cs` 的 ResumeEvents Postfix 直接注入 `_combatSynchronizer` 并调用 ResetState / InitializeForEvent。这一做法依赖 beta 的 EventCombatSynchronizer 类型。两个补丁还在第二战前恢复 CombatReplayWriter，并按房间过滤奖励。

因此 public 适配不仅是更改参数：必须核对其内部 CombatState 创建/重置、事件恢复、回放记录与额外奖励。RitsuLib 的生命周期通知不能代替这条流程。

已从 public 实现确认：EventModel.GenerateInternalCombatState 建立独立 CombatState 并加入玩家与怪物；ResetInternalCombatState 移除生物并清空状态。EventRoom.Exit 在 Combat layout 时调用该清理方法；Resume 调用 EventSynchronizer.ResumeEvents 后重建事件界面。EnterCombatWithoutExitingEvent 要求共享事件，且禁止 Combat layout 的事件设置 shouldResumeAfterCombat=true。这说明不能把 ResetInternalCombatState 当成 beta ResetState 的通用替换；竞技场、心灵绽放须遵循 public 的非 Combat layout 返回路径，并分别处理新战斗状态和回放初始化。这里只确认实现约束，尚未写移植补丁或实战验证。

上游证据：[竞技场补丁](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/main/Sts2BalanceModCode/Patches/Events/ColosseumCombatPatch.cs)、[心灵绽放补丁](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/main/Sts2BalanceModCode/Patches/Events/MindBloomCombatPatch.cs)。

## 本轮采用方式

**先采用同一份业务源码、分别构建 public / public-beta 两个 AllMatters DLL。** 图片和本地化可共用；已确认不同的游戏 API 调用和覆盖方法集中在具体兼容位置。暂不增加运行时反射加载器、第二套模型注册体系或泛化兼容框架。

RitsuLib 采用已发布的 0.6.7 作为接入候选，分别引用 `Compat.0.107.1` 和 `0.111.0` 编译模块，玩家端使用完整 variant-pack。当前尚未把依赖加入 AllMatters 生产项目；在第一个事件的实现任务中一起接入、声明 manifest 并验证加载。完整来源与机制见 [RitsuLib 调查](ritsulib-compatibility.md)。

这不是永久要求两个发行包；等实际移植调用面足够明确后，才有依据判断是否值得合并为一个通用包。

## 验证结果与剩余工作

| 检查 | 结果 | 范围 |
| --- | --- | --- |
| AllMatters public 骨架构建 | 通过，0 警告、0 错误 | 使用 0.107.1 引用 DLL |
| AllMatters public 骨架构建，实际 DLL | 通过，0 警告、0 错误 | 使用 GitHub 来源的 0.107.1 实际 DLL |
| AllMatters beta 骨架构建 | 通过，0 警告、0 错误 | 使用本机实际 beta DLL |
| RitsuLib 0.6.7 public API 编译探针 | 通过，0 警告、0 错误 | public 引用 DLL + 对应 compat 模块 |
| RitsuLib 0.6.7 public 实际 DLL 编译探针 | 通过，0 警告、0 错误 | GitHub 来源的 public 实际 DLL + 对应 compat 模块 |
| RitsuLib 0.6.7 beta API 编译探针 | 通过，0 警告、0 错误 | 实际 beta DLL + 对应 compat 模块 |
| public 相关方法体 | 已检查 | GitHub 实际 DLL，版本依据见上文；不是完整游戏运行证明 |
| public 加载与游戏内玩法 | 未验证 | 仍缺本地完整 public 游戏环境 |
| beta 游戏内加载与移植玩法 | 未验证 | 本轮未启动游戏；玩法尚未实现 |

编译探针位于 `local/compat-probe/`，只编译不运行，覆盖程序集注册、Godot 脚本注册、RegisterSharedEvent、EventModel 子类、PowerModel.BeforeHandDraw 和生成牌命令。它验证的是这些具体接入点，不是 11 个事件完整兼容。

构建日志在 `local/api/{public-build,public-real-build,beta-build,ritsulib-public-probe,ritsulib-public-real-probe,ritsulib-beta-probe}.log`。`Paths.local.props` 的 PublicGameDataDir 已配置为 GitHub 来源的本地 0.107.1 实际 DLL 目录，并注明不是完整游戏安装；BetaGameDataDir 仍指向现有游戏。两目标构建结果分别在 dist/public 和 dist/public-beta，均只打包 AllMatters DLL 与 manifest，不包含游戏引用或 RitsuLib。

第 1 项的程序集准备、API 比较、RitsuLib 选择与分别构建方案已有依据，可继续实施第 2 项。之后仍需从自己的 Steam 安装准备独立 public 开发副本，用于真实加载和玩法验收；本次没有切换 Steam 分支、下载完整游戏、修改存档或将任何依赖写入 mods。连续战斗还需结合对应移植实现完成多人、保存恢复与奖励回归。
