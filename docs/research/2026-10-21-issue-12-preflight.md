# Issue #12 心灵绽放：开发前调研

日期：2026-10-21。结论：可以开始开发，主要参考代码和美术已有本地来源；不是原样复制事件。新增双乐加维林遭遇、奖励规则与保存恢复适配是主要工程工作。素材逐文件核查见 [源码与资源清单](2026-10-21-issue-12-source-assets.md)。本轮仅调研，未实现 #12，未启动游戏、修改安装或发布内容。

## 需求和当前基础

- 直接需求：[GitHub #12](https://github.com/Endercloud001/AllMatters/issues/12)，本次在线读取，无评论。前置 [#3](https://github.com/Endercloud001/AllMatters/issues/3) 已关闭，状态为 completed。
- 优先级沿用 [范围表](../v0.1-scope.md) > 补充说明 > [父规格](https://github.com/Endercloud001/AllMatters/issues/2) > 上游实现。37 层及以前富有，38 层起无伤痛，单双人统一；不重新讨论 Q1–Q20。issue 中“37 层前”的简写按父规格 A12 的 37/38 边界处理。
- 富有：999 金币、`Normality` 与 `Greed` 各一张；无伤痛：原生治疗回满、`Enthralled` 与 `Regret` 各一张。不得把执迷写成 `Writhe`。四种牌两版已有原生实现，复用模型和游戏资源，不复制游戏图像或代码。既有类型证据：`.local/api/event-preflight/{public,beta}-types.txt`；映射核对见 [已有调查](sts1-event-source-review.md)。
- 觉醒：升级所有可升级牌并获得绽放印记；治疗阻断仅针对持有者。
- 凯旋：一场两只一代乐加维林；每人 100 金币、随机稀有遗物、正常 Boss 卡牌规则的一份基础三选一，可跳过，保留原生修改。房间仍在事件流程内，不使用 Boss 房间类型触发过幕。
- 当前 HEAD 为 `496c8a8`（合并 #23 蒙面强盗）。工作区已有多项未提交改动，调研不整理或覆盖它们。[强盗验证记录](../testing/masked-bandits.md) 已记录两版各 75 项测试、构建和 beta 用户验收；该证据属于强盗，不能作为 #12 已通过的证据。

## 可复用实现

| 来源 | 可复用部分 | #12 必须处理的差异 |
| --- | --- | --- |
| `src/Events/MaskedBandits.cs` | `IsShared`、原生事件战斗入口、public/beta 参战者奖励归属适配 | 心灵绽放有普通插画和非战斗选项；不照搬 Combat layout、对白状态和面具奖励 |
| `src/Encounters/RedMaskBandits.cs` | `ModEncounterTemplate`、独立模型、槽位、可变怪物创建 | 新建双 Lagavulin 遭遇和双槽位布局，不能沿用三人怪物列表 |
| `src/Encounters/RedMaskBanditsPatches.cs` | 按具体遭遇过滤默认奖励、保留后续原生奖励钩子的模式 | 强盗只替换默认金币；#12 还需替换普通卡牌奖励，避免默认遗物/药水等不符合指定奖励的掉落。不得全局改其他战斗 |
| `src/Monsters/BanditMonster.cs` | RitsuLib 将 Node2D 场景转换为原生 `NCreatureVisuals` 的工厂用法 | 当前基类命名属于强盗，可复用工厂调用；不必为一个怪物引入大规模新抽象 |
| `src/Events/Cleric.cs`、`src/Relics/` 既有遗物 | 治疗、加牌/遗物、RitsuLib 模型和资源命名惯例 | 新增 MarkOfTheBloom 和治疗 listener；不只在心灵绽放按钮内阻断治疗 |
| `src/Tests/MaskedBanditsTests.cs`、`HeadlessCombatHost.cs`、`HelloWorldTests.cs` | 原生战斗入口、双方本地身份、资源/Spine 加载、合法房间序列化和框架初始化 | 新增独立睡眠、Boss 卡牌和保存场景；现有宿主替代房间切换，不能证明真实联机、回放或可玩性 |
| `src/Events/AllMattersLocalization.cs`、`src/Tooling/PackResources.gd` | 六类本地化表合并、原生纹理导入、原样打包 Spine/场景 | 添加本票键和资源、修正上游旧选项描述；沿用现有 PCK 流程即可 |

上游主来源为 [Balance](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD)。本次通过 GitHub `GET /repos/MT-SUPER-POWER/STS2-Balance-MOD/commits/main` 核实远程 main 与本地均为 [d9eff9e](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/commit/d9eff9e067b07689e722c5283e7c3e7c451961ca)，未更新或运行上游项目。

核心参考入口：[`Events/MindBloom.cs`](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Events/MindBloom.cs)、[`Monsters/Lagavulin.cs`](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Monsters/Lagavulin.cs)、[`Powers/AsleepLagavulinPower.cs`](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Powers/AsleepLagavulinPower.cs)、[`Relics/MarkOfTheBloom.cs`](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD/blob/d9eff9e067b07689e722c5283e7c3e7c451961ca/Sts2BalanceModCode/Relics/MarkOfTheBloom.cs)。完整路径和资源闭包见附件。

上游事件实际包含本局第一幕 Boss、50 金币、单人 41/多人 38 分界、两张凡庸、一张疑虑及第二战。它们均不能直接带入本票。不引入 `MindBloomSecondFight`、Boss 包装器或二战重置补丁。睡眠与进阶语义按 Lagavulin/AsleepLagavulinPower 参考实现保留，双怪各持有自己的计数和能力，原生创建链负责多人血量缩放。

## 美术可获得性与复用边界

以下路径均位于 `.local/references/STS2-Balance-MOD/Sts2BalanceMod/`，本轮未复制到正式资源目录。

| 资源 | 实际文件与状态 | 开发处理 |
| --- | --- | --- |
| 事件插画 | `images/events/MindBloom.png`，3440×1616，已目视查看 | 左侧人物与橙红烟火、右侧暗紫留白；适合继续评估事件背景，须在游戏内验证裁切和文字可读性 |
| 印记图标 | `images/relics/MarkOfTheBloom.png`，以及 `big/`、`outlines/` 版本齐全 | 迁入 AllMatters 路径并登记归属 |
| 乐加维林动画 | `monsters/lagavulin/` 中 `.tscn`、骨骼数据 `.tres`、`.spatlas`、`.spskel`、PNG 引用链齐全 | 已有文件，不必从零画怪物；双怪布局仍需新建。静态检查不代表 Spine 在目标运行时加载成功 |
| 睡眠能力 | `images/powers/AsleepLagavulinPower.png`，256×256；缺独立 `big/` 版本 | 在资源 profile 中明确复用同图，或补齐大图，不能保留上游不存在的路径 |
| 中英文文本 | 事件、怪物、能力、遗物文本有上游来源 | 事件数值、诅咒名称、单战斗和指定奖励必须按本项目重写 |

Balance 根 LICENSE 为 GPLv3，本项目也已采用 GPL-3.0-only；新增移植仍需在 `THIRD_PARTY_NOTICES.md` 按本票登记。现有声明见 [参考来源](../references.md) 和根来源文件。不能由仓库整体 GPL 声明推断所有底层游戏素材均已取得独立授权：乐加维林 Spine 有一代来源线索，其单独权属证明尚未找到，正式分发前需要补齐可追溯授权说明，或采用权属明确的替代美术。事件图和印记图也应按附件记录实际来源声明，不称为已完成独立权属审计。

ActsFromThePast 本地源码归档可作行为对照，但未见根 LICENSE，且该归档未带 PNG/场景素材；不能将它作为资源补齐或直接复制代码的默认来源。无需为了本票引入整套 AFTP/BaseLib 或额外素材包。

## 两目标 API 核查与具体风险

本次查询实际程序集：public `v0.107.1`，ProductVersion `0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`；本机 beta `v0.111.0`，ProductVersion `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`。环境为 Godot 4.5.1 mono、net9.0、RitsuLib 0.6.7。版本仅描述当前配置，不代表已验证未来版本。

### 共享结算和战斗生命周期

beta `EventSynchronizer.ChooseOptionForSharedEvent` 遍历玩家，各自执行其事件选项。因此非战斗分支只结算 `Owner`，不要再遍历全队。战斗奖励例外按现有两版入口处理：public 本地实例为全队准备奖励，beta 每份实例提交其 Owner 的奖励，由同步器汇总。证据：`src/Events/MaskedBandits.cs` 的 `Fight`，以及 `.local/api/queries/public-beta-v0.111.0/MegaCrit.Sts2.Core.Multiplayer.Game.EventSynchronizer.decompiled.cs:259`。

心灵绽放只有单场终结战斗，优先使用原生 `EnterCombatWithoutExitingEvent(..., false)`；无需继承上游二战恢复。实际 Layout、`ShouldCreateCombat`、父事件和退出链仍应在两版分别验证，不把强盗 Combat layout 的配置直接移到插画事件。public 内部战斗状态和 beta `EventCombatSynchronizer` 的差异见 [已有双分支调查](dual-branch-api.md#连续事件战斗的具体风险)。

### Boss 卡牌规则可复用，但不能把房间改成 Boss

两版都有 `CardReward(CardCreationOptions, int, Player, PlayerChoiceSynchronizer)`，且 `CanSkip` 默认 true。可按原生 Boss 卡牌参数构建，基础数量 3，由 `CardFactory.CreateForReward` 继续负责解锁、人数、升级和选项修改。不要先抽三张固定稀有牌再用手工列表构造器，否则会改变工厂/修改规则。

`CardCreationOptions.ForRoom(player, RoomType.Boss)` 返回角色牌池、`Source.Encounter` 和 `BossEncounter` 稀有度策略；这里复用的是卡牌生成参数，不是将实际房间改为 Boss。两版 `RewardsSet.WithRewardsFromRoom` 对最后一幕 Boss 有提前返回；伪装 Boss 既可能改变过幕，也可能连显式奖励一起跳过。

证据（本机导出不提交游戏源码）：

- `.local/api/queries/public-beta-v0.111.0/MegaCrit.Sts2.Core.Runs.CardCreationOptions.decompiled.cs:81`。
- 同目录 `MegaCrit.Sts2.Core.Rewards.RewardsSet.decompiled.cs:85`、`:136`、`:241`。
- 同目录 `MegaCrit.Sts2.Core.Rewards.CardReward.decompiled.cs:110`、`:146`。
- public 对照文件在 `.local/api/queries/public-v0.107.1/`。

### 新发现：beta 的战斗标记与额外奖励序列化冲突

具体失败路径：直接复制 beta 正常 Boss 卡牌构造方式，添加 `CardCreationFlags.IsFromCombat` 后放进 `CombatRoom.ExtraRewards`，在允许保存的 pre-finished 节点调用 `CombatRoom.ToSerializable()`，会继续调用 `CardReward.ToSerializable()`；后者拒绝 `IsCardReward` 以外的任何 flags，抛出 `NotImplementedException`。它也拒绝带自定义 filter 或空牌池的奖励。public 的序列化限制相同，但其正常奖励构造本身没有 beta 的 `IsFromCombat` 标记。

证据：beta `RewardsSet` 第 241 行；`CardReward` 第 333–346 行；`CombatRoom` 第 176–178 行；public `CardReward` 第 306–320 行。仅由方法体确认这条失败路径，本轮没有运行失败探针。

开发时先验证最小的“原生奖励生成 → 领取/跳过 → 合法保存恢复”例子，再选择按具体遭遇在奖励生成阶段替换普通 CardReward，或对本遭遇做最小保存适配。生成阶段替换可避免将带标记实例持久放入 ExtraRewards，但恢复后重建、RNG 和不重复领奖仍须实测；这只是候选方案，尚不是已实现结论。不能为绕过异常直接删除影响原生奖励修改行为的标记，也不需要新增 hash、冻结 contract 或 gate。

### 绽放印记已有治疗 hook

Balance 的 `MarkOfTheBloom` 实现 `IHealHookListener.ModifyHealMultiplicative`，比较 `context.Creature.Player` 与 `Owner`，仅持有者返回 0。RitsuLib 的 `CreatureCmdHealHookPatch` 对 `CreatureCmd.Heal(Creature, decimal, bool)` 安装前置 hook，`HealHook` 从 run/combat 模型分发，不要求每次治疗单独挂全局 listener。

来源：`.local/references/STS2-RitsuLib/src/Combat/Healing/{HealHook.cs,Patches/CreatureCmdHealHookPatch.cs}`，以及上方 MarkOfTheBloom 上游链接。实现时须用当前 0.6.7 两个 compat 模块实际测试牧师、休息、战斗治疗和队友；不要由接口存在就宣称全部生效。直接改 HP、增加最大生命或复活路径是否经过 Heal 属于调用链核查，不擅自扩大“无法治疗”的玩法范围。

## 建议开发顺序和验收重点

1. 先完成单场双怪遭遇、原生入口及准确奖励的最小验证，特别是 beta 卡牌标记与保存冲突；这是工程不确定性最高的部分。
2. 接入 Lagavulin、睡眠能力、双槽位场景和资源；测试默认 3 回合睡眠、8 金属化、未穿透格挡不醒、分别受伤唤醒、自然醒和原生多人缩放。不要设置死去冒险者版本的 `StartsAwake`。
3. 实现共享事件三类非战斗效果、37/38 边界、MarkOfTheBloom 与双语文本，保持费用/效果按 Owner 一次执行。
4. 针对上述风险补普通行为测试，复用现有 Godot 宿主；用两套真实 DLL 构建，加载 PCK/Spine 和两语言表；不另造测试框架。
5. 交付 beta 开发包与操作步骤，由用户手动安装、Steam 启动并检查图像、双怪布局/意图、独立唤醒、领奖与可保存节点。真实主客机、回放和完整 public 的证据按 issue/父规格分别记录，不能用单进程双玩家测试代替。

可沿用命令为 `./tools/Test.ps1 -Branch All`、`./tools/Build.ps1 -Branch public-beta`、`./tools/Build.ps1 -Branch public`。本轮未运行这些构建/玩法测试，因为没有修改功能；不复述为 #12 的通过证据。

## 本轮执行记录与边界

- 读取 GitHub #12/评论与 #3 状态；`gh issue view 12 --comments -R Endercloud001/AllMatters` 未获得可见内容，随后以已连接 GitHub 工具返回的正文和空评论列表为准。
- 运行 `./tools/ReviewScope.ps1`、`git status --short` 和 `git log -1`；读取规格、已有实现和验证记录，保留现有工作区改动。
- GitHub API 查询 Balance `commits/main`，与本地 `git log -1` 核对版本。
- 运行 `ExportApiReference.ps1` 查询两版 `CardReward` 构造器，并导出两版 `RewardsSet`、`CardReward`、`CardCreationOptions`，以及 beta `CombatRoom`、`CardFactory`。导出保留在忽略的 `.local/api/queries/`，未分发。
- 两次带 `-Member` 的批量导出因后续类型不含指定成员而以非零退出；改为不带成员筛选的导出并按文件检索。无匹配不解释为 API 不存在。
- 未下载新美术、运行参考仓库构建脚本、改动游戏或 Steam、创建评论/PR。资源可获得不等于本票已完成资源导入、画面验收或权属独立审计。
