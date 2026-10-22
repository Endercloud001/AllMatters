# 一代事件源码对照预研

调查日期：2026-10-20。以 [v0.1 范围表](../v0.1-scope.md) 为需求依据；下述上游行为只是实现参考，不覆盖该表；2026-10-20 Grill 第一轮确认，表格未修改部分默认沿用 Balance。此次只读参考源码，未执行参考仓库脚本，未复制事件代码或资源。未发现 `docs/adr/`。游戏原生类、双版本 API 与 Wiki 访问情况另见本轮范围文档中的调查记录。

以下路径中的 `B/` 代表 `../references/STS2-Balance-MOD/Sts2BalanceModCode/`，`A/` 代表 `../references/ActsFromThePast/ActsFromThePast/`；路径相对本文所在 `docs/research/`。参考仓库不纳入版本管理，因此换机器需按 [参考源码](../references.md) 准备。

## 逐事件对照

| 事件 | 源码证据与现状 | 按范围表需要处理的内容 |
| --- | --- | --- |
| 老乞丐 | `B/Events/OldBeggar.cs`：`IsAllowed` 要求所有玩家至少 75 金币；`GiveGold` 扣 75，再进入 `RemoveCard` 页；`RemoveCard` 用游戏删除选择器。`A/Acts/TheCity/Events/OldBeggar.cs` 普通模式相同，另含 RebalancedMode 附魔/诅咒选项。 | 保留普通模式语义；不要带入 AFTP 的模式、附魔或诅咒。还需验证无可删牌的边界：范围表只要求金币，上游会先扣钱再打开删牌界面。Grill 已确认无可删牌时禁用给金币，保留原入池条件。 |
| 诅咒书本 | `B/Events/CursedTome.cs`：Hive 注册，`IsAllowed` 只查 Act 2；`Page1Continue/Page2Continue/Page3Continue` 分别伤害 1/2/3；最后 `Obtain` 伤害 15 后随机奖励一本，`Stop` 另受 3。`GetRandomBook` 仅排除已有书，全持有时反而返回死灵之书。`A/Acts/TheCity/Events/CursedTome.cs` 普通模式同类流程，RebalancedMode 是另一套机制。 | 增加表中书籍持有条件，末页改为主动选择一本，移除随机兜底。上述阅读数值是本地源码证据，尚未以表中 Wiki 页面核实；完整取得累计 21、放弃累计 9 是源码算术结果，不应冒充 Wiki 核实。Q5 已确认全队无人持有任一本才出现，进入后每名玩家独立选择。 |
| 蒙面强盗 | `B/Events/MaskedBandits.cs`：共享事件，`IsAllowed` 符合 Act 2、层数 ≥ 23、全队无人持红面具；`Pay` 对 `Owner` 扣全部金币；`Fight` 显式奖励 25–35 金币和原生 RedMask。`B/Encounters/RedMaskBandits.cs` 生成 Pointy/Romeo/Bear，房间类型 Monster。`B/Monsters/Bear.cs` 的 `BearHugDexterity = -2` 已符合新表。AFTP `A/Acts/TheCity/Events/MaskedBandits.cs` 还有彩蛋和展示面具分支。 | 按表实现战斗图鉴归类；不要带入 AFTP 彩蛋。Grill 已确认表未修改部分默认沿用 Balance，额外金币按上游保留；共享选项每位玩家均承担费用、获得效果，战斗奖励每位参战者各得一份。必须验证结算次数。 |
| 增益研究者 | `B/Events/Augmenter.cs` 的 `IsAllowed` 符合 Act 2、全员至少两张可移除牌。`Transform` 只调用 `CardCmd.TransformToRandom`，没有升级；`Mutagens` 只给予遗物，不移除初始遗物。`B/Cards/Jax.cs` 为 0 费技能、失去 3 生命、获得 2 力量（升级为 3），没有固有。`B/Relics/MutagenicStrength.cs` 开战加 3 力量与 3 层 `MutagenicDecayPower`；后者 `AfterSideTurnEnd` 每回合减 1 力量，不符合新表。 | J.A.X. 默认固有；追踪变化产生的新牌再升级；移除初始遗物后给予突变之力；把衰退改为拥有者第三回合结束一次减 3，不能只改本地化。初始遗物已失去/替换时按已确认主动选项规则禁用；多个初始遗物的交换范围仍待确认。衰退防护交互默认沿用 Balance，保留 Debuff 与人工制品交互，仅修改扣除时点与数值。AFTP `A/Acts/TheCity/Events/Augmenter.cs` 同样不处理升级或替换初始遗物；其 `A/Powers/MutagenicStrengthPower.cs` 继承 TemporaryStrengthPower，也不能直接满足第三回合需求。 |
| 神圣泉水 | `B/Events/TheDivineFountain.cs` 的 `IsAllowed` 要求全员有可移除诅咒；`Drink` 只删除 `CardType.Curse && IsRemovable`。`A/SharedEvents/TheDivineFountain.cs` 另有 Guilty 排除与 RebalancedMode 增加最大生命选项。 | 表的“移除所有诅咒”与永恒/不可移除诅咒存在边界，尤其死灵诅咒。Q6 已确认尊重不可移除规则，保留永久诅咒及死灵诅咒；不要带入洗澡加生命分支。 |
| 牧师 | `B/Events/Cleric.cs` 与表基本一致；`CalculateVars` 对 25% 最大生命向下取整；`GenerateInitialOptions` 分别检查 35 金币、75 金币且至少一张可删牌；仅消费一次服务便结束。AFTP `A/Acts/Exordium/Events/Cleric.cs` 普通模式同类行为，RebalancedMode 提高准入且去掉离开。 | 保留表内选项；沿用向下取整可作为源码依据，需在实现说明中写明。35–74 金币只可治疗，净化锁定。 |
| 心灵绽放 | `B/Events/MindBloom.cs`：共享事件，Act 3；`CalculateVars` 单人阈值 41、多人 38；`Fight` 随机本局第一幕 Boss，给予 50 金币和稀有遗物，战后可进入 `MindBloomSecondFight`。`Gold` 加两张 Normality；`Heal` 加一张 Doubt。`Upgrade` 升级所有可升级牌并给 MarkOfTheBloom。AFTP `A/Acts/TheBeyond/Events/MindBloom.cs` 战斗为一代三个 Boss 随机，其余阈值和诅咒与 Balance 相同。 | 改为双 Lagavulin、100 金币、稀有遗物、稀有卡牌奖励；不需要上游二战模块或第一幕 Boss 包装器。单双人都按表中 38 分界。金币分支分别给凡庸与贪婪；治疗分支分别给执迷与悔恨，不能沿用 Doubt。执迷已核实对应原生 `Enthralled`，见下文事实核查。 |
| 变化大转盘 | `B/Events/WheelOfChange.cs`：`Play` 随机 6 结果并实际调用 WheelSpinMinigame；注释“暂不移植自定义 UI”已落后于实现。`ApplyResult` 遗物为 `new RelicReward(owner)`，诅咒为 Decay，伤害为最大生命 15%。`A/SharedEvents/WheelOfChange.cs` 基本相同。 | 明确限定普通/罕见遗物，诅咒换为表中霉运，伤害固定 7；100/200/300 金币与完全治疗可复用语义。Q10–Q11 已确认遗物先以 50% 选择普通/罕见，再从该档合法候选抽取；删牌无目标时显示结果、说明并结束，不重抽补偿；六种结果仍各 1/6。遗物空池处理仍待确认。 |
| 红面具大人之墓 | `B/Events/TombOfLordRedMask.cs`：`IsAllowed` 在 Act 3 后还排除任何持红面具玩家，因此 `WearMask` 的 222 金币分支在正常准入时不可达；`PayRespects` 扣全部金币。AFTP `A/Acts/TheBeyond/Events/TombOfLordRedMask.cs` 普通模式不排除红面具，付费仍是全部金币。 | 按表仅限制 Act 3，固定扣 150；余额不足时禁用献敬意（Grill 已确认）。已有面具玩家按表可选领取 222。是否仍展示其购买分支需明确；不可照搬导致 222 分支不可达的准入。 |
| 大图书馆 | `B/Events/TheLibrary.cs` 没有 Act 条件，采用共享池注册（注释声称 Act 3，实际注册不保证）；`Read` 从排除 ColorlessCardPool/CurseCardPool 的 `ModelDb.AllCardPools` 生成 20 张选择，`Sleep` 向下取整回复 33%。无打扫选项。AFTP `A/Acts/TheCity/Events/TheLibrary.cs` 只有本职业 20 选 1、回复 20% 且四舍五入。 | 按表 Act 2，保留跨职业与 33%；新增瓶装遗物和绑定牌机制。Grill 已确认必须排除当前角色牌及无色牌；使用真正的其他角色牌池，不能把 EventCardPool 混入。Q7 已确认仅其他四个原版角色，使用已解锁及人数合法的奖励牌，合并后逐牌抽取 20 张不重复候选。瓶装三种遗物本次在两版原生遗物类中未发现，不能当作现成依赖；Q12 已确认仅从未持有且有合法绑定目标的瓶子中等概率抽取，无任何可选瓶子则禁用打扫；目标及绑定规则仍需设计。 |
| 竞技场 | `B/Events/Colosseum.cs` 注册 Glory、`IsAllowed` 恒真；`Fight/Resume/HasCompletedFirstFight` 处理第一战返回；`FightAgain` 奖励稀有遗物、罕见遗物、100 金币。`B/Encounters/ColosseumFirstEncounter.cs` 是双奴隶贩子并禁奖励；第二战 `ColosseumSecondEncounter.cs` 为 Taskmaster + GremlinNob，Elite 类型。AFTP `A/Acts/TheCity/Events/Colosseum.cs` 限 Act 2、第 23 层后且仅单人。 | 第一战替换为原生活雾 + 潮湿邪教徒，无奖励；第二战改为仅在残杀千足虫、蜂群术士、感染棱柱三种原版第二幕精英完整遭遇中抽取，奖励改为 100 金币、稀有遗物、一组稀有卡牌；不移植原奴隶贩子/地精怪与资源。此项已由用户将原“第二阶段 Boss”改为精英；不纳入其他 Mod，候选池范围已确认。 |

## 必要依赖与排除项

事件注册、资源地址及基础模板可参考 `B/Abstract/BalanceEventTemplate.cs`、`BalanceCardTemplate.cs`、`BalanceRelicTemplate.cs`、`BalancePowerTemplate.cs`、`BalanceMonsterTemplate.cs`、`BalanceEncounterTemplate.cs` 以及 `B/Extensions/ModAssetPaths.cs`。这些依赖 RitsuLib 自动注册与 scaffolding；只迁移实际所需接口和代码，资源根目录改用 AllMatters，不引入整个 Balance 模组运行时。

| 内容 | 最小源码入口 | 说明 |
| --- | --- | --- |
| 三本书 | `B/Relics/Necronomicon.cs`、`NilrysCodex.cs`、`Enchiridion.cs`，`B/Cards/Necronomicurse.cs` | 死灵书 `AfterObtained` 加永恒死灵诅咒，每回合首次 2 费以上攻击额外打出；宝典结束回合从 3 张本职业牌选牌洗入抽牌堆；英雄宝典首回合随机本职业能力牌本回合免费。Balance 的 NilrysCodex 还会升级全部候选，而 `A/Relics/NilrysCodex.cs` 不升级；Grill 已确认默认沿用 Balance，因此保留这项候选升级增强。 |
| 绽放印记 | `B/Relics/MarkOfTheBloom.cs` 的 `IHealHookListener.ModifyHealMultiplicative` | 仅将拥有者治疗倍率设为 0；依赖 RitsuLib 治疗 hook，测试常规治疗、事件治疗、最大生命变化后的回血及复活相关行为，不能只测试休息。 |
| J.A.X. 与突变之力 | `B/Cards/Jax.cs`、`B/Relics/MutagenicStrength.cs`、`B/Powers/MutagenicDecayPower.cs` | 都需要按新表实质修改，具体差异见上表。 |
| 强盗遭遇 | `B/Encounters/RedMaskBandits.cs`、`B/Monsters/Pointy.cs`、`Romeo.cs`、`Bear.cs`，`B/Runtime/Animations/FastAttackAnimation.cs` | 还需场景与三怪素材、音效及本地化；对应 scene 为 `Sts2BalanceMod/scenes/actsfromthepast-red_mask_bandits_event.tscn`，各怪由 `AssetProfile` 指向 `Sts2BalanceMod/monsters/<怪物>/`。图鉴与普通遭遇池要分开处理；模板 `BalanceEncounterTemplate.IsValidForAct` 返回 false，表示可按 ID 使用但不进入地图普通池。 |
| 双乐加维林 | `B/Monsters/Lagavulin.cs`、`B/Powers/AsleepLagavulinPower.cs` | 范围内新增双怪事件遭遇；Lagavulin 包含默认沉睡及 StartsAwake 两种配置，表未指定初始状态，不应擅用死去冒险者版本的醒来配置。资源入口 `Sts2BalanceMod/monsters/lagavulin/lagavulin.tscn`。源码尚使用 `B/Runtime/Combat/`，实施时按实际符号收敛依赖。 |
| 转盘呈现 | `B/Events/UI/WheelSpinMinigame.cs`、`NWheelSpinScreen.cs` | 如采用上游动画，后者依赖 `B/Runtime/Audio/`、`B/Runtime/Combat/`，以及 `Sts2BalanceMod/images/event_extras/{Wheel,WheelArrow,SpinButton}.png`、`sfx/events/wheel.ogg`；纯结果逻辑并不必须依赖这些呈现辅助类。 |
| 原生模型 | RedMask、LivingFog、DampCultist，诅咒及第二幕精英 | 用目标游戏原生类型；不要重新注册 RedMask。本轮已在两版实际 DLL 确认活雾、潮湿邪教徒、Greed/Normality/Regret/Writhe/BadLuck 及三种精英类型；诅咒中文名映射已见下文核查，关键词与行为按目标分支实际证据。 |
| 瓶装遗物 | 当前两套参考仓库未找到三种 Bottled 遗物实现 | 需要单独实现和素材来源调查；不能拿瓶装潜能药水替代，也不能据此扩大为移植所有一代遗物。 |

不需要移植 `B/Events/MindBloomSecondFight.cs`、`B/Encounters/MindBloomBossEncounter.cs` 和相关 Boss 包装怪物；不需要为竞技场移植 SlaverBlue/SlaverRed/Taskmaster/GremlinNob；不带 AFTP 的 RebalancedMode、彩蛋、初代整幕或 BaseLib 体系。`B/Patches/Relics/RedMaskEventOnlyPatch.cs` 改原生 RedMask 稀有度及遗物池，属于范围表没有要求的原生遗物调整，不因事件依赖 RedMask 而自动纳入。

## 多人、恢复与奖励风险

Balance 将 MaskedBandits、MindBloom、Colosseum 标为 `IsShared = true`，其余 8 事件为 false。共享池注册 `[RegisterSharedEvent]` 表示事件池位置，不等于多人共享决策；二者需分别验收。范围表中“所有玩家”是出现条件，不等于所有事件都共享决策。Grill 已确认：个人选项只影响选择者；必须统一选项的事件每位玩家承担对应费用、获得效果；共同战斗奖励每位参战者各得一份。原生同步器会对每位玩家的事件实例执行同一选项，`Owner` 操作不等于只影响一个人，见下文事实核查；仍需双端实机验证。

上游已经为连续事件战斗补过具体问题，不能只复制事件类：`B/Patches/Events/ColosseumCombatPatch.cs` 的 `ResumeEventsPatch` 在首战返回时重置 `EventCombatSynchronizer`，避免二次开战 already ready；`ReplayWriterPatch` 为第二战初始化回放；`RewardsPatch` 删除默认房间金币/遗物，避免叠加显式奖励。它们依赖私有 `_combatSynchronizer` 字段与 `CombatManager.StartCombatInternal`，必须对两版目标 API 核验。其背景替换补丁服务原一代竞技场场景，使用原生新战斗后不应机械带入。

心灵绽放改为一次双怪战斗后，无需上游二战重置和回放状态；但仍需确保稀有卡牌奖励明确生成，而非依赖 `RoomType.Monster/Elite/Boss` 自动发普通奖励。强盗表外金币默认沿用 Balance；竞技场第二战按表替换精英奖励，默认掉落、药水概率与个人领取次数需逐项核对，避免奖励重复或缺失。源码 `MindBloomCombatPatch.RewardsPatch` 只过滤默认金币和遗物，不能确保“一组稀有卡牌”。

保存/加载至少覆盖诅咒书本每一阅读页、老乞丐已付费待删牌、转盘已确定结果尚未领奖、竞技场首战前/战后中场/第二战、瓶装遗物已拿取待选牌。重点确认恢复不会重扣金币、重加诅咒、重抽结果或重复发奖励。上游 `HasCompletedFirstFight` 利用当前地图点房间历史的遭遇 ID 与 TurnsTaken 判断首战完成，是可以研究的恢复线索，不等于已经证明所有断点正确。

采用既有事件状态、同步、游戏 RNG、模型 ID、存档及普通测试验证上述场景即可；本次没有提出额外 hash、冻结 contract、baseline 或 gate。正式复制代码/素材前遵守 [参考源码](../references.md) 的许可证及作者归属约定。

## Grill 事实核查（2026-10-20）

本节仅记录能由当前目标源码或资源直接回答的事实，不决定玩法。调查读取 public v0.107.1（ProductVersion 提交 `5926027`）与 public-beta v0.111.0（`41cef1e`）真实程序集；中文资源来自本机 **beta** `SlayTheSpire2.pck`，不能据此声明 public 中文文本已经独立核实。新增反编译证据位于忽略目录 `docs/local/api/grill-facts/`，public 位于其 `public/` 子目录。本轮未执行参考仓库脚本、游戏或联机测试。

### 中文诅咒名称与可移除规则

直接读取 beta PCK 的 `localization/zhs/cards.json` 后，核实以下 `.title` 键；两版原生类存在性由实际程序集确认。

| 表内中文名 | 本地化键 / ModelId.Entry | 原生类 |
| --- | --- | --- |
| 凡庸 | `NORMALITY` | `MegaCrit.Sts2.Core.Models.Cards.Normality` |
| 贪婪 | `GREED` | `MegaCrit.Sts2.Core.Models.Cards.Greed` |
| 执迷 | `ENTHRALLED` | `MegaCrit.Sts2.Core.Models.Cards.Enthralled` |
| 悔恨 | `REGRET` | `MegaCrit.Sts2.Core.Models.Cards.Regret` |
| 霉运 | `BAD_LUCK` | `MegaCrit.Sts2.Core.Models.Cards.BadLuck` |

**更正前期候选映射：`Writhe` 的 beta 中文标题为“苦恼”，不是“执迷”。** 心灵绽放“我无伤痛”应据表定位 `Enthralled`，不能使用 `Writhe`。这些键是 Entry；完整 ModelId 还含 Category，构造时优先使用原生 `ModelDb.Card<T>().Id`，避免手写推断。

两版 `CardModel.IsRemovable` 方法体均为 `!Keywords.Contains(CardKeyword.Eternal)`；两版 `Enthralled.CanonicalKeywords` 均包含 `Eternal`，两版 `Greed` 亦包含 `Eternal`。两版 `BadLuck` 的 `CanonicalKeywords` 也均含 `Eternal`（public 方法体本轮追加导出）。因此，**两版尊重原生可移除规则的泉水都不会清除执迷、贪婪、霉运**。这是原生实现事实；泉水最终玩法另见范围文档。

### 初始遗物的身份入口

两版 `CharacterModel.StartingRelics` 的真实签名为 `IReadOnlyList<RelicModel>`。它提供角色初始遗物定义，可据模型 ID 与玩家当前实际遗物核对；不能用玩家遗物栏第一项识别初始遗物。它是一个列表，不能在未检查具体角色前假定只包含一件，也未证明替换后的遗物仍被该属性列出。喝突变剂在原初始遗物缺失或被替换时已确定禁用交换；多件初始遗物先核查原版角色，不为其他 Mod 角色凭空扩展规则。

### 共享事件的 Owner 处理

两版 `EventSynchronizer.BeginEvent` 均遍历玩家，为每位玩家创建各自的 `canonicalEvent.ToMutable()` 实例并调用 `BeginEvent(player, ...)`；`EventModel.BeginEvent` 将传入玩家赋给该实例 `Owner`。两版 `ChooseOptionForSharedEvent` 均对所有玩家调用 `ChooseOptionForEvent(player, optionIndex)`，后者获取该玩家的事件实例并执行其 `eventOption.Chosen()`。

因此，**仅凭上游效果写成操作 `Owner`，不能断言共享事件只给一名玩家结算**：原生共享流程会在每位玩家的事件实例上执行相同选项。反过来，若在这种回调中再无条件遍历全队发放同一奖励，就有重复结算风险。该结论来自方法体；投票、断线、连续战斗返回后的完整实机行为仍未验证。beta 增加战斗同步器参数，两分支战斗恢复差异仍按 [双分支调查](dual-branch-api.md) 处理。

### 稀有卡牌奖励的原生入口及边界

beta `CardReward(CardCreationOptions options, int cardCount, Player player, PlayerChoiceSynchronizer? synchronizer = null)` 可显式指定候选张数、生成选项与玩家；构造时添加 `CardCreationFlags.IsCardReward`。`CanSkip` 默认值为 `true`；`Populate` 调用 `CardFactory.CreateForReward(player, OptionCount, options)`，并存在原生奖励修改 hook。因此“一组稀有卡牌奖励”不能只靠房间类型或创建一个泛化奖励来保证，应在实现时进一步查 `CardCreationOptions` 和游戏原生稀有奖励调用者，再确定配置。

追加核查：public 同样有上述构造器，`CanSkip = true`，`Populate` 通过 `CardFactory.CreateForReward` 生成。两版 `RewardsSet` 的普通、精英、Boss 常规战斗分支均提供 **3 张候选**；beta 在 `ForRoom` 结果上增加 `IsFromCombat` 标记。两版 `CardCreationOptions.ForRoom(player, RoomType.Boss)` 使用角色自身牌池及 `BossEncounter` 稀有度类型，但禁止直接以 `RoomType.Event` 调用。两版 `GetPossibleCards` 会经 `GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)`，保留解锁及多人限制。`ForNonCombatWithDefaultOdds` / `ForNonCombatWithUniformOdds` 默认附加 `NoUpgradeRoll`，因此不能把非战斗选牌的默认升级策略当成战斗奖励策略。以上证明原生入口和常规张数；`BossEncounter` 精确概率、奖励修改后是否仍全稀有、升级概率及本事件最终需求仍需核实，未运行验收。

### 第二幕三种原生精英完整遭遇

两版 `Acts.Hive.GenerateAllEncounters` 均包含以下类；两版完整方法体均已导出。中文标题直接核实自 beta PCK `localization/zhs/encounters.json`，不是按英文类名猜译。

| 中文标题 / 本地化键 | 完整 Encounter 类型（统一前缀 `MegaCrit.Sts2.Core.Models.Encounters.`） | 两版 `GenerateMonsters` 初始组成 |
| --- | --- | --- |
| 残杀千足虫 / `DECIMILLIPEDE_ELITE.title` | `DecimillipedeElite` | `DecimillipedeSegmentFront`、`DecimillipedeSegmentMiddle`、`DecimillipedeSegmentBack`，占据 `segment1/2/3`；遭遇还随机并错开三节的 `StarterMoveIdx`，有专用场景与镜头配置。 |
| 蜂群术士 / `ENTOMANCER_ELITE.title` | `EntomancerElite` | 初始为一个 `Entomancer` 可变实例。 |
| 感染棱柱 / `INFESTED_PRISMS_ELITE.title` | `InfestedPrismsElite` | 初始为一个 `InfestedPrism` 可变实例；遭遇额外声明 `Tainted` 附魔覆盖图资源。 |

上述均为 `RoomType.Elite`。初始组成不等于整场怪物数量固定；完整怪物行为、召唤、状态与额外资源交给原生遭遇。特别不能用单个千足虫节替代 `DecimillipedeElite`，也不能仅用怪物类名重建另外两种遭遇并跳过遭遇资源声明。本轮没有跑这三场战斗。

### 上游乐加维林的现成睡眠与多人入口

以下是 Balance 参考源码事实，不覆盖范围文档中的修改：

- `B/Monsters/Lagavulin.cs` 的 `_startsAwake` 默认为 `false`。`AfterAddedToRoom` 默认施加 8 层 `MetallicizePower` 与 3 层 `AsleepLagavulinPower`；只有显式设置 `StartsAwake` 才走清醒开局。血量为普通 109–111、ToughEnemies 112–115；攻击为普通 18、DeadlyEnemies 20，虚弱吸取按对应进阶为 -1/-2 力量与敏捷。
- `B/Powers/AsleepLagavulinPower.cs.AfterDamageReceived` 仅在 `target == Owner` 且 `UnblockedDamage > 0` 时唤醒该拥有者，移除其金属化和睡眠，并调用 `WakeUpFromDamage` 进入眩晕再接攻击。未穿透格挡的伤害不触发这条路径。未发现“同伴受伤一起唤醒”的遍历逻辑，两只怪各有自身睡眠能力与状态。
- 睡眠倒计时在 `AfterSideTurnEnd` 检查 `side == Owner.Side` 后减一，到零调用自然醒；自然醒没有受伤唤醒中的 `CreatureCmd.Stun`。第一轮玩家方开始时睡眠能力会按金属化层数先加格挡。因此不能将倒数误写为每位玩家个人回合结束各减一。
- 参考 `Lagavulin` 与 `BalanceMonsterTemplate` 未额外实现自定义多人 HP 倍率。两版原生 `CombatState.CreateCreature` 路径会按遭遇、`Players.Count` 和当前幕索引调用 `Creature.ScaleMonsterHpForMultiplayer`；多于一人时更新最大和当前 HP。beta `ScaleHpForMultiplayer` 使用 `hp * playerCount * MultiplayerScalingModel.GetMultiplayerScaling(encounter, actIndex)`。因此移植后应核对正常战斗创建链是否实际经过原生缩放；仅看到参考怪物基准 HP 不能断言多人仍保持单人血量，也不应在怪物里先叠一遍倍率。

以上未证明 RitsuLib 基类及自定义遭遇在两个目标上的完整运行链已通过，也未核实最终具体倍率；这些留给实现后的最小场景与联机验证。

## Grill 第一轮决定的实现影响（2026-10-20）

玩法依据见 [范围文档已确认规则](../v0.1-scope.md#grill-已确认规则2026-10-20第一轮)。本文保留上游实际行为作为对照，不用当前需求改写历史源码事实。

- 三种第二幕精英在 public/beta 类清单中均有 `DecimillipedeElite`、`EntomancerElite`、`InfestedPrismsElite`；中文映射与初始组成已由上节核实。候选单位是完整遭遇，尤其残杀千足虫不能按“1只精英”裁剪怪物数量。
- 大图书馆必须排除当前角色牌池与无色牌，不能照搬上游“只排除无色和诅咒”的过滤。
- 默认继承 Balance 已解决尼利宝典候选升级、转盘六结果等概率、未修改遗物效果等取舍；表格明确引用 Wiki 的诅咒书本流程仍需事实核实。
- 无法完整执行的主动选项应禁用；不要通过提高事件入池门槛处理，也不要先扣金币再让选牌落空。Q11 已确定无可移除牌时的转盘删牌结果仅说明后结束，不重抽、不补偿。

## Grill 第二轮决定的实现影响（2026-10-20，Q5–Q12）

- 诅咒书本的全队无书准入，与进入后各玩家独立选书分开处理；泉水严格使用可移除诅咒集合。
- 大图书馆候选先筛选其他四个原版角色的解锁/人数合法奖励牌，再合池逐牌抽样，20 张之间不重复。不能让上游按默认稀有度赔率生成的流程悄悄改变已确认的抽样策略；Q17 已确认不足 20 张展示全部、零候选禁用阅读。
- 三种精英完整遭遇等概率且不受图鉴发现影响。事件稀有卡牌奖励按正常 Boss 奖励策略配置；费用、金币及遗物仍按事件表，不把整个房间伪装为 Boss 房间。
- 转盘遗物普通/罕见先各 50%，再从所选档的合法候选抽取；Q16 已确认单类空池改抽另一类，两类均空则说明后结束，不重抽、不补偿。删牌无目标时维持原结果并结束。
- 瓶子仅在未持有且存在合法绑定目标的种类中等概率抽取，无种类可选则禁用打扫。Q13 已确认排除已有固有与已绑定牌，获得瓶子后必须完成绑定，不可取消。

## Grill 补充事实：初始遗物、Boss 奖励与原版书本（2026-10-20）

以下补充核查只记事实，玩法决定以范围文档为准；没有改写父代理正在记录的决定。

### 五名原版角色的初始遗物

逐一导出 public 0.107.1 与 beta 0.111.0 的五个 `Characters.*.StartingRelics` 方法体，**每名角色在两版都只返回一件**：Ironclad → `BurningBlood`；Silent → `RingOfTheSnake`；Regent → `DivineRight`；Necrobinder → `BoundPhylactery`；Defect → `CrackedCore`。证据在 `docs/local/api/grill-facts/` 与其 `public/` 子目录的对应角色导出文件。因此“原版角色同时定义多件初始遗物”不是当前两目标版本的真实场景；多件规则只影响其他 Mod 角色或未来新增角色，不能仅因 API 类型为列表就当作 v0.1 原版阻塞项。遗物在实际牌局中失去或替换仍须单独检查当前持有状态。

### 正常 Boss 卡牌奖励的基础规则与修改入口

两版 `Odds.CardRarityOdds.GetBaseOdds(BossEncounter, rarity)` 均为 Common=0、Uncommon=0、Rare=1；`Roll` 对 BossEncounter 忽略已有稀有概率偏移（offset=0）。两版常规 `RewardsSet` 都调用 3 候选的 `CardReward`，采用所属角色牌池；候选经解锁、多人限制、去重和玩家数过滤。上述是未被效果修改的生成基础，不能取代后续 hook。

两版 `Factories.CardFactory.CreateForReward` 会逐张通过 `Hook.ModifyCardRewardCreationOptions`，再按可能牌池与概率创建实例，并在未指定 `NoUpgradeRoll` 时调用 `RollForUpgrade(..., baseChance: 0)`。两版 `RollForUpgrade` **只给非稀有牌增加按当前幕计算的基础升级率**；稀有牌原始概率保留为 0，之后仍调用 `Hook.ModifyCardRewardUpgradeOdds`，并按 `rng.NextFloat() <= odds` 判断。准确表述是“稀有奖励基础升级概率为 0，保留原生升级修改 hook”，不能写成任何效果下都不升级。

工厂及奖励展示还会运行 `TryModifyCardRewardOptions` / `AfterModifyingCardRewardOptions` 等原生路径，选择结束时也读取 `ShouldAllowSelectingMoreCardRewards`。beta 常规战斗奖励在生成选项上带 `IsFromCombat`；public 常规调用没有该标记。实现“与正常 Boss 同规则”的事件奖励应保留分支各自的生成/修改语义，房间仍是事件所需类型；**不能为了稀有牌把实际遭遇或房间改为 Boss，也不能用只过滤稀有模板、禁用 hook 的自制选牌界面冒充正常奖励**。这些是方法体推导的接入约束，尚未通过事件实机验证。

### 诅咒书本：本机一代原版一手证据

本轮通过网络尝试指定灰机 Wiki 的 MediaWiki `action=parse&prop=wikitext` 接口，仍不可访问；wiki.gg 页面返回 403。没有把搜索摘要当作 Wiki 已核实。随后发现本机已安装一代游戏，读取 `E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar`，用 `javap -c -p` 只读检查 `com.megacrit.cardcrawl.events.city.CursedTome`，没有启动游戏。`CardCrawlGame` 静态版本字符串为 **[V2.3.4] (12-18-2022)**。本地字节码记录为 `docs/local/api/grill-facts/sts1-CursedTome.javap.txt`，不提交游戏源码。

- 初页有阅读与离开。点击阅读只进入 PAGE_1，不扣生命；其后 PAGE_1/2/3 分别显示一个继续选项，点击依次通过 `DamageInfo(..., HP_LOSS)` 失去 1、2、3 生命。中间三页没有停止选项。
- 三次继续后，LAST_PAGE 才提供拿走与停止。拿走额外失去 10 生命，**一代进阶 ≥15 时为 15**；停止额外失去 3。基础累计拿走为 16（高进阶 21），停止为 9；此为各次 HP_LOSS 的名义值相加，未绕过遗物减免或死亡流程。
- 原版方法顺序是先调用玩家 `damage`，再执行 `randomBook`；事件类没有按当前生命阈值禁用拿走，也没有局部 `currentHealth/isDead` 判断。这里只证明事件代码顺序，未完整追踪一代死亡/复活调度，更不能据此替二代决定跨页或致死后继续奖励的行为。
- 同 JAR 的 `localization/zhs/events.json` 中 `Cursed Tome.OPTIONS` 与上述 1/2/3、停止3及拿走动态数值一致。原版最后随机取书；本 Mod 的主动三选一仍以范围表为准。

已由原版一手字节码补足扣血与退出节点，但**Wiki 页面本身仍未成功读取**。一代 A15 不能直接当成二代某个进阶编号；二代采用什么不利事件层级以及致死时点，应结合已确认规则与二代 API 核查，不向用户索取可自行查证的 API 事实。

### 瓶装可借鉴的原生步骤，及尚未找到的绑定实现

beta `Relics.DollysMirror.AfterObtained` 使用 `CardSelectCmd.FromDeckGeneric`、`CardSelectorPrefs(SelectionScreenPrompt, 1)` 与过滤器完成获得遗物后的单卡选择，并处理空结果；之后通过原生命令创建及加入牌组。它可作为取得时选牌入口参考，但不是持久绑定。beta `Whetstone.AfterObtained` 只筛选可升级攻击牌、按原生 RNG 洗牌取两张并调用 `CardCmd.Upgrade`，**没有固有赋予或牌绑定功能**，不能直接充当瓶装实现。

beta `CardModel.AddKeyword` / `RemoveKeyword` 管理实例自身持久关键词状态，其文档明确区分基础关键词和外部钩子修饰。仅看到 AddKeyword 存在，尚不足以证明绑定关系、失去遗物时移除固有、重抽/复制/变化、读档均已正确。此次检查 Bookmark、Whetstone、DollysMirror 及若干原生遗物未找到“三种瓶装的完整等价实现”；不据此断言全体原生遗物均没有相近实现，也不新增另一套存档机制。绑定采用何种现成序列化身份和 hook，留到实现时定点核对。

## Grill 第三轮决定的实现影响（2026-10-20，Q13–Q17）

- 瓶装目标必须类型匹配、无固有且未被其他瓶子绑定；获得后不可取消选牌，需按已确认目标完成绑定。
- **不可把战斗外绑定限制扩大到战斗内。** 升级保留绑定；持有瓶子时禁止战斗外移除/变化，战斗中允许消耗/变化。失去瓶子仅撤销其关联及其赋予的固有；不能删除独立来源的固有。绑定存档身份与战斗副本的关系在实现时核对，Q18 已确认变化产物不继承绑定，永久牌组原牌保留绑定，下一场仍固有。
- 瓶装遗物只由图书馆打扫提供，不进入常规掉落、商店或其他事件的通用随机遗物候选。
- 转盘单类遗物候选耗尽时回退到另一类，两类皆空说明后结束，不重抽/补偿。图书馆有 1–19 张候选展示全部，零候选禁用阅读。
- 原版角色多初始遗物已排除为非本轮真实场景。原版书本扣血及退出节点已由本机一代 JAR 核实；Wiki 页面可读性、二代进阶映射及致死调度分别记录，不能将事实核查与玩法选择混为一项。

### 补充：二代进阶与事件致死流程的证据边界

两版 `AscensionLevel` 完整枚举相同：None=0、SwarmingElites=1、WearyTraveler=2、Poverty=3、TightBelt=4、AscendersBane=5、Inflation=6、Scarcity=7、ToughEnemies=8、DeadlyEnemies=9、DoubleBoss=10。**不存在一代 A15“更不利事件”的同名/等价枚举，不能自动把书本末页 15 生命映射到二代某级。** 本轮抽查的原生 `ColossalFlower`、`AbyssalBaths` 也未提供此映射；不据此声称所有原生事件都无进阶相关效果。固定采用 10、15 或新增分界属于玩法决定，既有决定见范围文档。

两版原生 `ColossalFlower.ObtainPollinousCore` 都是先等待伤害，再调用遗物获得；两版 `EventOption.ThatDoesDamage` 仅设置“当前生命≤名义伤害时红闪”的提示条件，不会自动禁用该选项。beta `CreatureCmd.Damage` 会等待 `Kill(killedCreatures)`；`Kill` 处理死亡/防死亡钩子，并在全员死亡时进入 `RunManager.OnEnded(false)` 与游戏结束界面。beta `RelicCmd.Obtain` 本身没有统一的死亡玩家提前返回。**不能由这些局部方法推出原生框架保证自动中止后续奖励，更不能声称死人可以正常拿书**；场景退出、异步回调与多人存活者的完整结果仍需实现时验证。应检查伤害后实际存活/运行状态，并遵守已确认的致死规则，不把名义伤害与死亡直接等同，也不跳过原生防死亡处理。

## Grill 第四轮决定的实现影响（2026-10-20，Q18–Q20）

- 瓶装的永久绑定始终指向永久牌组原牌；战斗内消耗/变化只影响当前战斗实例，变化产物不继承绑定，也不影响下一场原牌获得固有。必须验证读档与战斗实例创建链，避免按牌名或变化产物错误迁移绑定。
- 书本所有进阶末页拿走统一额外失去 15（名义累计 21），停止额外失去 3（累计 9）。这是用户明确采用 Balance 数值的决定，不是声称一代原版所有进阶都损失 21，也不映射一代 A15 到二代。
- 允许致死选择并显示提示；等待原生伤害、防死亡与复活完成后检查实际存活状态。存活才继续阅读/选书，确认死亡结束该玩家事件且不发书；不能依赖 `ThatDoesDamage` 自动禁用或 `RelicCmd.Obtain` 自动拦截。
- 本次 Q1–Q20 已收敛当前已知玩法问题。本文保留原版/上游事实和核查过程，遇到较早的“待确认”描述，以范围文档及后续已确认轮次为准。事件实际运行、联机、保存恢复和分支兼容仍待开发与验证。

## Grill 收尾（2026-10-20）

用户明确确认 Q1–Q20 已达成共同理解，本次 Grill 结束。玩法依据为范围文档；实现流程与准备度评估见 [实施计划](../implementation-plan.md#grill-后的流程与实现准备评估2026-10-20)。历史核查中的未知项不再覆盖后续明确决定，尚待实机验证的事项不等同于未决定玩法。

## 规格整理（2026-10-20）

已将 Q1–Q20、现有工程约束与验收场景整理为 [一代事件回归开发规格](../specs/sts1-event-return.md)。用户已确认事件可观察行为为主要自动化测试入口，真实房间/网络/回放/资源画面另行实机验证。本文继续承担事实及源码定位，规格承担可独立执行的行为与验收要求；发布信息见实施计划。
