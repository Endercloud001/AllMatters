# #82–#88 开发前置参考

调研日期：2026-11-12。代码起点：`main`，`f7e915c`（合并 PR #103）。本轮读取七张工单、父规格和相关规范来源，完成代码、原型、资源及双版 API 调查；未实施追猎功能、修改生产代码、提交、安装或启动游戏。本文是实现导航，需求仍以 GitHub 工单及明确修订为准。

## 1. 后续从哪里开始

1. 阅读目标工单全文及最新评论，再读 [父规格 #73](https://github.com/Endercloud001/AllMatters/issues/73) 对应章节和 Further Notes。本文记录的是本次调查状态，不复制一份完整规格。
2. 查下表的真实阻塞项；读第 3 节共同缺口，再跳转本票章节。以当前代码验证入口仍在，不能将原型收口、工单关闭或同名函数当成正式实现完成。
3. 实施前复用 `tools/ReviewScope.ps1` 和 `docs/development.md` 的双版开工冒烟；测试输出目录共用，两版及多个实施者之间串行运行。本文末尾记录本轮结果。
4. 写 Harmony patch 前，使用第 7 节现成工具导出目标双版方法体，核实签名及调用顺序；本轮已查 15 个类型，不代表所有未来 patch 目标均已检查。
5. 每票按一个真实行为贯穿领域、原生适配、正式 UI/输入、保存与测试。#88 汇总组合证据，不能代替前票应完成的恢复、布局或可自动验证工作。

### 开发范围、验收批次与依赖

用户本轮将七票作为一个开发批次；原工单的人工验收分组仍保留。可统一规划开发，但无需每完成一票就要求用户安装游戏。

|工单|实现范围|工单明示硬阻塞|原人工批次|验证归属|
|---|---|---|---|---|
|[#82 T09](https://github.com/Endercloud001/AllMatters/issues/82)|限步、根房安全、面具、商店/休息限制、HUD、设置余额控制台|#78，本次查询 CLOSED|4|V04/07/08/09/12|
|[#83 T10](https://github.com/Endercloud001/AllMatters/issues/83)|空宝箱、非奖励任务、动画/远端完成、准确兼容范围|#82|4|V08/11/14|
|[#84 T11](https://github.com/Endercloud001/AllMatters/issues/84)|密林与暗港追猎、布局及整场恢复|#82|5|V04/09/10/14|
|[#85 T12](https://github.com/Endercloud001/AllMatters/issues/85)|巢穴、知识恶魔选择及整场恢复|#82|5|V04/09/10/14|
|[#86 T13](https://github.com/Endercloud001/AllMatters/issues/86)|荣耀普通/精英、术士及整场恢复|#82|5|V04/09/10/14|
|[#87 T14](https://github.com/Endercloud001/AllMatters/issues/87)|荣耀三 Boss、专用布局及本票整场恢复|#82|5|V04/09/10/14|
|[#88 T15](https://github.com/Endercloud001/AllMatters/issues/88)|双版组合验收、实施/证据索引、包及许可声明|#79、#81、#83–#87|6|V01–V14|

本次 #79 为 CLOSED、#81 为 OPEN。关闭工单不表示人工验收已通过。`docs/batches.md` 将 #82–#88 概括为可并行，不能据此跳过上表明确依赖；该文不是本批排程依据。来源票 #60/#63/#66–#72 仅为规范/研究输入，不自动新增硬阻塞。

推荐顺序：#82 先贯通权威生命周期；#83 继续非战斗链；战斗侧优先在 #84 取首个真实普通受罚遭遇打通战前/奖励完整点，再让 #85–#87 复用可用接缝；最后 #88 合验。这里的先后是减少共享文件冲突的工程建议，没有新增 #84→#85/#86/#87 的 tracker 阻塞。

## 2. 权威资料与实际可用位置

|遇到的问题|必须到达的来源|本机便捷入口 / 恢复方法|
|---|---|---|
|限步、面具、服务及完整逐敌数值/目标|[H 唯一完整表 #63](https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226)|旧 `docs/v0.1.2-battle-enhancement-in-hunting.md` 仅作搜索辅助，以后续 resolution 为准|
|战前完整点、原生进度与兼容|[#67 resolution](https://github.com/Endercloud001/AllMatters/issues/67#issuecomment-6629243377)、[#73 完整检查点章节](https://github.com/Endercloud001/AllMatters/issues/73)|`../AllMatters_docs/research/v0.1.2-integration-decision.md`；可 `git show 5d4b2c4:docs/research/v0.1.2-integration-decision.md`|
|已有原型回答了什么、哪些仍须正式集成|[追猎 CLOSEOUT](https://github.com/Endercloud001/AllMatters/blob/0ca54c4/prototypes/hunting-boundaries/CLOSEOUT.md)、同提交 INTEGRATION-INPUTS|`../AllMatters_prototype70_continue/prototypes/hunting-boundaries/`；`git show 0ca54c4:prototypes/hunting-boundaries/INTEGRATION-INPUTS.md`|
|追猎提示、教程、正式输入/布局|[U 唯一详细要求](https://github.com/Endercloud001/AllMatters/blob/bad6e14/prototypes/jigsaw-tutorial-input/REQUIREMENTS.md)|`../AllMatters_prototype71/prototypes/jigsaw-tutorial-input/REQUIREMENTS.md`|
|V01–V14、S 六类/H 十三组/U 八组最小输入与责任|[完整验证交接 §§4–7](https://github.com/Endercloud001/AllMatters/blob/8efe31c3d5fc99a2425b84b063d6abb13b7814ae/docs/research/v0.1.2-verification-handoff.md)|`../AllMatters_docs/research/v0.1.2-verification-handoff.md`；`git show 8efe31c3:docs/research/v0.1.2-verification-handoff.md`|
|细看历史、术士/火炬头原始算法与许可|[#66 研究入口](https://github.com/Endercloud001/AllMatters/issues/66#issuecomment-6600755901)|`../AllMatters_research66/docs/research/v0.1.2-doormaker-history.md`、`v0.1.2-sts1-mystic-torchhead.md`；均可从 `864b5d7:docs/research/` 读取|

路径相对 `AllMatters/` 根目录。此次 `Get-Item docs` 显示它是普通目录、无 LinkType/Target，并非 AGENTS 历史描述的 junction；主仓 `docs/research/`、`docs/adr/`、`.local/references/` 当前不存在。研究材料仍在同级目录及 Git 来源中，使用上述位置即可，不必重做研究或修改目录结构。

覆盖顺序：#73 及明确修订 → 它纳入的最新详细来源 → 最新原型收口及其证明范围。尤其 H 旧“保留战中阶段”由 I/#73 的“整场回战前”更新；旧原型早期“未验”文字由 CLOSEOUT 更新，但原型人工通过仍不能代签正式拼图集成。

## 3. 当前架构与全批共享缺口

继续沿用四个职责：拼图跑局负责规则与权威状态；布局生成负责造池/搜索；原生跑局适配负责房间/奖励/战斗/版本；检查点保存恢复负责原生与 Mod 同点捕获、写入及加载。UI/传输呈现或提交操作，不各自决定随机、扣步或追猎合法性。无需为本批先冻结新接口。

|当前代码|可复用行为|本批需要接上的部分|
|---|---|---|
|`src/Jigsaw/JigsawRun.cs`：`BeginEnter/ConfirmEnter/CompleteRoom/CreateSave/RestoreCore`；`JigsawTypes.cs`|成功确认才计步、首次节点进度独立、节点/进入身份、实际房型、原队员/在局名单|`RemainingSteps` 仅 `max(0,Budget-Steps)`，`Budget` 只读；保存没有正式追猎/按时结果/根房安全事实。#82 设置余额不能通过改 Steps 或历史实现|
|`JigsawRun.ClearHuntingData/RestoreMaskForEndgame`|已有同名占位方法可供定位|两者为空壳，正式生命周期接线仍需补齐；RestoreMaskForEndgame 没有调用，TransitionToNextAct 当前只有测试调用。旧注释 #84 不能改变 #82 生命周期归属|
|`ExplorationStateRecovery.IsHunting`、`LobbyRecovery`、`JigsawRecoveryPresentationTests`|恢复投影与多人展示已有测试入口|仍以 `Steps > Budget` 推断追猎；#82 要一并迁移，避免恢复/UI 与权威规则相互矛盾|
|`JigsawExploration.cs`；session `CompleteCurrentRoom`|实际房型、商店真实离开、单人及 managed action 多人完成路径|发面具必须在全员真正完成时发生，不能等下一次目的地投票，也不能只挂单人 `CompleteRoom`|
|`JigsawNativeSession.CapturePendingEntry/RetryEntry/LoadCheckpoint`、`JigsawNativeEntryReload.Load`|进入前原生保存拷贝、RunState 整份重建、原生网络/大厅恢复链|进入失败恢复不等于追猎整场恢复。各战斗票须实证自己的战前和胜利奖励完整点|
|`JigsawCheckpoint.Capture`、`JigsawSaveSource`、`JigsawCaptureNativePoint`|Ritsu 扩展与原生同文件、同来源主/backup、串行保存|追猎状态与同次原生玩家/房间/RNG一起保存。延用已有进入身份与事务，避免另造奖励账本或独立追猎文件|
|`JigsawNativeShopPersistence`、`JigsawNativeShopSync`|每店/每玩家真实库存、交易安全点、动态删牌价格与恢复|限制层只改变可执行操作和呈现，不能伪造 Used、售罄、交易历史或补货|
|`Presentation/JigsawStepCounter.cs`、本地化 `static_hover_tips.json`|正式顶栏余额数字/悬浮提示|严格采用 U 表中的 N/1/0 与末 Boss 解除句；安全/追猎/按时作为权威判断，不新增额外标签或发面具对话；调试信息仅留控制台|
|`JigsawResetCommand`、`JigsawEditorAction`|`am_jigsaw_reset`、Host/阶段/接受顺序/旧消息防护|`am_steps` 尚无实现；#82 增加，#88 汇总最终帮助与双版实际执行证据|

共享热点为 `JigsawRun/JigsawTypes/JigsawNativeSession/JigsawEditorAction/JigsawCheckpoint`、恢复投影及资源注册/本地化。只读研究可共享工作区；并行实施遵守 `docs/development.md` 的独立 worktree 约定，由主会话串行合入热点。各票消费同一根房受罚事实，不能各造开关。

### 每张战斗票都必须带上的最小闭环

真实成功进入 → 确定本根房受罚快照 → 同次原生玩家/RNG/Mod 完整战前点 → 原生怪物创建/人数缩放一次/固定层一次 → 真正胜利奖励点 → 退出恢复。

恢复回整场战前，旧战中阶段、选择、个人计数不能和战前玩家混拼；探索不二扣、随机不重抽、加怪不重复、奖励点不重开。事件同名精英/Boss走当前幕普通增强，不触发专属地图 Boss 清面具。第二 Boss 仍计探索、不重判超时。#84–#87 各自负责本票敌人全闭环，恢复责任不集中推给 #87 或 #88。

## 4. #82 / #83：非战斗实施准备

### #82 限步、面具与服务

**先打通的行为切片**：剩 1 步进入非 Boss，当前根房含子事件战仍安全；一人滞后时不发面具；全员真实完成后对原队员各发唯一面具；下一次进入才受罚；同完整点恢复保持这些事实。再扩展最后一步第一/单 Boss 按时、第二 Boss、新幕及末地图 Boss 奖励前解除。

- 额度 15/14/13、显示最低 0、0 后仍探索；余额与探索次数/首次进度分离。唯一面具不入普通池、不交换，缺席原角色保有。清除依地图终点身份，不依怪物名字。
- 商店全部商品含免费、原版删牌及发送/接收入口都拒绝；隐藏商品和禁用外观保留真实货架。问号按实际房型；事件购物/治疗放行，刚耗尽额度的根房仍可服务。
- 休息保原本可用 Heal/Mend、禁用条件、加成/附奖/额外次数；遗物新增禁项也在执行前拒绝，不写历史；单人不新增多人愈合。
- `am_steps N` 是设当前余额，不是增加 N、重配默认预算或改历史。只接受单个非负整数；缺/多参、负数、小数、非数字、溢出/非法阶段原子拒绝。Host 经既有权威链同步；0 不直接发面具，调高不清追猎；在下一正常完整点保存，不强写任意战中快照。

**关键方法体实证**（均已双版导出）：

|目标|发现及对实施的影响|
|---|---|
|`RestSiteSynchronizer.ChooseOption(Player,int)`|私有方法先执行 `OnSelect`，成功后写历史；须在执行前过滤。仅禁 UI 无法保护远端执行|
|`RestSiteRoom.Exit`|public 仅完成校验便立即返回；beta 等 `AfterAllRestSitesCompleted`。public Exit 不是全员完成；额外次数也不能把首次选择当完成|
|`MerchantEntry.OnTryPurchaseWrapper`|两版有 inventory/ignoreCost 参数；免费入口也要限制|
|`MerchantCardRemovalEntry.OnTryPurchaseWrapper`、`OneOffSynchronizer.DoMerchantCardRemoval`|删牌存在三参包装器及 OneOff 接收入口，不能仅覆盖普通商品包装器|

**复用输入**：原型 worktree `src/Tests/PrototypeHunting{ShopProbe,ServiceTests,RestProbe,RestExtrasTests,MaskTests,VisitTests}.cs`。正式测试从 `JigsawDomainTests/JigsawExplorationTests`、`JigsawMerchantExitTests`、`JigsawNativeShopPersistenceTests`、`JigsawNetworkTests`、`JigsawMultiplayerRestoreTests` 和现有 reset 用例取准备代码。内存 CWT/原型强制 Restricted 开关不进入生产。

### #83 空宝箱与非奖励链

**先打通的行为切片**：真正受罚宝箱进入 → Face 正常消耗 → Spoils 非奖励任务完成 → 金币/遗物/附加奖励全零 → 真空 Tween 结束并通知远端 → 各端可继续 → 完整点恢复不再消耗或发奖。

本次双版方法体确认 `OneOffSynchronizer.DoTreasureRoomRewards` 的 `ShouldGenerateTreasure=false` 在 `TryHandleSpoilsMap` 前返回。简单关生成会漏掉任务移除/完成历史等非奖励流程。当前 Mod 的 `NlothsHungryFace/NlothTreasure` 需要构造组合真入口复验，不能直接推广已有空箱测试。

`TreasureRoomRelicSynchronizer.CompleteWithNoRelics()` 会发 `RelicsAwarded(empty)` 并结束等待；只清列表不等于完成动画或远端同步。

原型 `PrototypeHuntingTreasureTests` 已记录具体反例：异步 hook 可直接 `GainGold(11)` 或 `Obtain<Anchor>` 绕过列表；底层全局取消 Obtain 若返回未入库对象，调用者读 Owner 或检查真实入库会失败，后续非奖励任务也可能停住。正式实现必须按**具体支持来源**适配并列兼容清单，不能宣称未知第三方通用拦截。

复用原型 `PrototypeHuntingTreasure{Tests,AnimationTests,ScreenTests}.cs` 的异步任务、实际空 Tween 与两条等待链输入；正式入口复用 `NativeNlothTests`、原生奖励/房间完成观察。原型 FTUE、地图外围替代、全局 NormalPath/NormalCall 不是生产方案。

## 5. #84–#87：逐票战斗准备

以下列出实施分解、易漏边界及源码入口；完整数值、进阶阈值与目标规则仍从 H 权威表读取，不以本节摘要替代表格。

### #84 密林与暗港

- 密林：普通/事件追加真菌兽，28 HP、孢子 2、咬 6/长 3 力及确定的首手/连咬规则；死亡阻止不触发，最后敌人死亡也结算孢子。再覆盖 Byrdonis、BygoneEffigy、PhrogParasite 五虫、Vantom 双 Inklet、CeremonialBeast 二阶段咆哮、KinPriest 原目标法球。
- 暗港：普通/事件追加 Seapunk；PhantasmalGardener 固定胆小 9、SkulkingColony 保硬壳加覆甲、TerrorEel 同一原目标；族母先原生人数缩放，再 max/current HP 同时向下取半、保持沉睡，左加已有 Lagavulin 并设 Minion；SoulFysh 左右随从及 Minion；WaterfallGiant 只清可见且当前为负面的能力、保隐藏流程。
- 普通追加敌人不是额外 Minion；明确要求的随从才设 Minion。固定能力层不能被原生多人缩放再增长；全体 HP 只缩一次。

**实查差异**：`InfestedPower.AfterDeath` public 先等死亡动画再 Add，beta 先 Add/隐藏再 Reveal；原生均只有四虫，`Wriggler` 状态机也只识别至 wriggler4。五虫必须覆盖原生召唤空过、首行动交替规则和真实第五槽。`SkittishPower.ShouldScaleInMultiplayer=true`，不是只有 Artifact 会自动人数增层。

**复用**：正式 `src/Monsters/Lagavulin.cs`、`src/Powers/AsleepLagavulinPower.cs`；布局接线参考 `src/Encounters/MindBloomEncounter.cs` 和对应 `mind_bloom.tscn`，两槽不能冒充五槽。原型 `PrototypeHunting{Models,TableTests,SpawnTests,FiveWormProbe,FatalTests,CombatEndTests,LayoutTests}.cs` 提供行为/负例；全布局反例见 `LAYOUT-NEXT-RESEARCH.md`，临时 Marker 与 210px 经验不直接用于正式多尺寸布局。

**实现顺序**：普通遭遇完整恢复 → 孢子最后死亡链 → 五虫真实原生命令 → 左右随从 → 全部表项/原目标 → 1/4 人完整战场布局与可点击目标。

### #85 巢穴与知识恶魔

- 普通开场各玩家弱/脆/易伤各 1，精英弱 2；事件只普通增强。Crusher/Rocket 固定人工制品各 3，保包围/方向和原效果。
- TheInsatiable 首回合沙坑 5、原目标 Draw/Discard 各 4 张 FranticEscape；保 PetOwner 回退、插入随机及后续流程，意图也应对应 8 张。
- KnowledgeDemon 三轮逐存活玩家选牌对：MindRot/Calamity、Sloth/Tender、WasteAway/Disintegration21；展示牌不入任何牌堆，普通 Calamity 不实现 `IChoosable`，需适配对应能力。伤害进阶差异及固定思考回复 20 按 H。

**实查差异**：public 私有选择方法 `CurseOfKnowledge`，beta 为 `CurseOfKnowledgeMove`，后者需 LiveCombat 才推进计数；原生 Ponder 为 30×人数，正式要求固定 20。不能依同名假设写双版 patch。

**复用**：原型 `PrototypeHuntingKnowledgeTests`、`PrototypeHuntingMovesTests`、`PrototypeHuntingChoiceScreenTests` 的三轮输入、展示适配、原生弹层 Push→选择→Remove。先原生开场/恢复，再蟹/沙虫，再正式选择屏/焦点；死亡/缺席、多人选择结束、战斗退出清理都只能续接一次。恢复时丢弃半份选择，恢复同一完整战前点 RNG，不保存半个弹窗结果。

### #86 荣耀普通战与精英

- 术士 HP56，施法→治疗→增幅循环；每活玩家独立等概率生成 `Dazed/Burn/Void/Toxic/Beckon/Slimed` 之一入弃牌。治疗从受伤存活敌人含自己抽取、回复12，无目标空过仍推进；增幅从活敌含自己抽取加2力量。沿原生怪物随机流保存/恢复，不由 UI 抽取。
- MechaKnight 固定人工5、首手喷火；SoulNexus Territorial1；FlailKnight 对所有玩家 Tangled1，保新生成攻击牌及回合末清理。

**实查差异**：public 喷火原生没有直接伤害，beta 有（A0 基础8），两版都保4 Burn；追猎只改指定项，不统一成另一版本。追加术士正常非 Minion、正式槽位可选/可点。

**复用**：原型 `PrototypeHunting{Models,ModelTests,TableTests,CoverageTests}.cs` 的六分支、受伤/死亡/自己目标、无人可治疗和正常敌人负例。先完整普通遭遇/三循环，再目标/RNG、三精英动作、战前/胜利恢复和完整布局。STS1 Healer 原 AI 与本方案不同，不能照搬 Java/libGDX 或 AFTP 脚手架。

### #87 荣耀 Boss

|对象|实现切片与必要边界|可复用原型输入|
|---|---|---|
|实验体 TestSubject|阶段二用胆小10/吮吸3替换能力；爪击 N 和进阶差异；阶段三清阶段二能力。真正第二复生完成后，首个完整玩家方回合计1，3/6/9附细看，回合末清理；正常起手、非抽牌取牌放行|`PrototypeHuntingStageTests` 的 Continuous/Stage/NativePlayerTurns/AfterRevive；`PrototypeHuntingMovesTests`；怪物持有过滤与计时输入|
|女王 Queen|聚合体实际死亡且未被阻止，覆盖当前全部六种意图为重燃，整场一次。左双火炬 HP50/伤10/Minion；召唤当次不行动，下一次直接攻击；激怒女王及存活火炬各+2力，死火炬不补|`PrototypeHuntingQueenProbe` 的 Override/Prevented/Enrage；`PrototypeHuntingQueenTests`；`PrototypeHuntingTorch`|
|永世沙漏 Aeonglass|无初始人工；每玩家各自初值/显示/触发/重置12；五状态入手、溢出与凋萎强化链；退出整体回战前，奖励点不重开|`PrototypeHuntingClockTests` 的真实12次 AutoPlay、逐玩家、满手溢出和两次升级输入|

**实查差异与风险**：

- `TestSubject.RespawnMove` 先增 Respawns 再等待动画/Revive；看到数值2不等于复生完成。必须覆盖挂起复生期间不计时、中途半回合不追计、原 Task 完成后才启动第三阶段。
- `Queen.AfterDeath` 原生只在特定意图改激怒，且未按 `wasRemovalPrevented` 排除；`EnrageMove` 已给女王自己+2力，追加适配不能再加一次。
- `WitheringPresencePower.DisplayAmount` 读 `DynamicVars.CardsLeft`，原初值/重置均6。只改 `Amount` 无法实现12；触发原生只加凋萎，需覆盖五牌生成而保原生入手钩子。
- public 沙漏 EbbDamage 低/高进阶为26/32，beta为22/26；未指定修改项保留原版差异。
- public 敌方结束会自行开始下个玩家回合，beta 外层协调。原型曾因 beta 重复 StartTurn 计数2，正式调度不能搬宿主显式启动逻辑。细看使用全队共享玩家方序号，沙漏则是逐玩家计数。

每版测试完整两死复生及9回合、六意图×1–4人、死亡阻止/重复通知、逐玩家11/12/13次出牌、胜利/战前恢复。正式火炬/玩家完整场景、意图/血条/目标与焦点按紧凑/宽屏验证，临时 Marker 不作完成证据。

## 6. 资源复用与许可准备

|资源|此次实际可找到的位置/来源|可复用与交付限制|
|---|---|---|
|面具、孢子云、柔嫩|`../AllMatters_docs/research/assets/hunting/{architect-mask,spore-cloud,tender}.png`|原件在场；正式资源接入须记录具体来源及使用/分发权。未因用户曾提供或原型可加载自动认定完整许可|
|真菌兽/术士/火炬头准确物种|`../AllMatters_prototype70/.local/resource-search/ActsFromThePast.pck`；原型 `RESOURCE-SEARCH.md`；AFTP0.13.0 对应 `2ec5d3f79f663897b82dee181395b8da4c62a9b3`|原型已加载 Spine4.2.11，并非本轮重新加载。AFTP 无明确独立 LICENSE，准确加载不代表可分发；不把整 PCK 或未核权素材放正式包|
|STS1 原始术士/火炬头|research66 的 `v0.1.2-sts1-mystic-torchhead.md`|原 JAR/Spine3.4.02 不能直接在 Godot 使用；可参考状态机思想，非正式资源转换成果或授权|
|乐加维林|生产 `src/Monsters/Lagavulin.cs` 和现有资源/`THIRD_PARTY_NOTICES.md`|复用已有实体和来源声明；现有授权边界不扩大到新物种/美术|
|细看历史实现|`v0.1.2-doormaker-history.md`|独立实现“怪物持有、放正常起手、禁额外抽牌”的思路；恢复 Mod 无明确 LICENSE，不复制整 Boss/图标|

本轮不联系作者、不分发专有方法体。交付负责人核权后才将具体素材加入开发包；缺许可应准确列出，不把技术可用、行为测试通过写成可发布。若替代资产影响既定行为，回相应来源确认，不自行降低规则。

## 7. 本轮 API 准备与可复用命令

目标分别为 public v0.107.1（ProductVersion 提交 `59260271157f76a2896f0eab5bc6ea1245d8b314`）和 public-beta v0.111.0（`41cef1ea4657c524aa50e870df009e56337e8c32`）。真实 DLL 可用于方法体和编译；public 完整匹配资源/正式运行证据另计。

默认 `Paths.local.props` 的 IlspyPath 指旧坏启动器，首次调用因缺少 `.store/.../ilspycmd.dll` 失败。已找到现有可运行 `ilspy-batch3`，通过显式参数完成双版导出，无需改配置或再安装：

```powershell
./tools/ExportApiReference.ps1 -Branch public -IlspyPath .local/tools/ilspy-batch3/ilspycmd.exe -Types MegaCrit.Sts2.Core.Multiplayer.Game.RestSiteSynchronizer
# public-beta 同法；其他目标替换完整类型名
```

输出在 `.local/api/queries/public-v0.107.1/`、`public-beta-v0.111.0/`，文件为完整类型名加 `.decompiled.cs`。本轮新导出并核对每版15类型：

- `Multiplayer.Game` 下 RestSiteSynchronizer、TreasureRoomRelicSynchronizer、OneOffSynchronizer；`Rooms.RestSiteRoom`；`Entities.Merchant` 下 MerchantEntry、MerchantCardRemovalEntry。
- `Models.Monsters` 下 KnowledgeDemon、MechaKnight、Wriggler、TestSubject、Queen、Aeonglass。
- `Models.Powers` 下 InfestedPower、SkittishPower、WitheringPresencePower。

`ApiInspector` 两版也确认 CreatureCmd.Add 重载存在。其签名查询仅枚举公共/受保护成员，查不到私有 Curse/ChooseOption 不代表方法不存在。后续新增 patch 仍要查其精确目标、参数及调用者，不能拿本轮某类型成功替整个模块背书。

## 8. #88：组合验收与交付准备

使用普通 Markdown 表索引证据即可：V编号、原S/H/U场景、实现票、提交/包、版本/构建、人数与宿主或真实端、输入/预期/实际、日志/退出码、用户结果、失败修复归属、未验范围。沿用既有 Git、类型、接受序列和保存事务；本轮未发现新增 hash、冻结 contract、baseline 或 gate 的必要场景。

|证据范围|本批责任/入口|组合检查重点|
|---|---|---|
|V01–V03 编辑/生成/正式网络|既有 #74–#80，#88 核对|原型浏览器、codec、loopback 不代签正式 Godot 或每版真实3端|
|V04–V06 保存/领奖/探索|#81 及前票，#82–#87 扩各自端点|正常主/缺主/坏主移走/移动失败/写失败；单/多人同步异步；3存档2在局；部分 Ancient 整体回退与完整奖励点|
|V07–V10 商店/根房/追猎/逐敌|#78、#82、#84–#87|双端一人滞后；战前/胜利同点；每版1–4人原生命令；正式1/4人紧凑/宽屏完整目标布局|
|V11–V14 宝箱/UI/终局/许可|#83、#79、各UI所属票、#88|具体直接发奖来源；三比例、720p至4K、长文案、非单颜色、重绑焦点；双版终局、两遗物/Flight、其他模式回归|

逐项保留原分组，不能只写“V矩阵已过”：S六类对应 V03/V04/V05/V06–07/V02+12/V08–10；H十三组对应 V10(1–8)、V07(9)、V08(10/12)、V11(11)、V08–09+12(13)；U八组对应 V12(1–5/7)、V02(6)、V08–09(8)。每组原始输入和预期查第2节验证交接。

### 正式测试宿主与已知盲区

- `HeadlessCombatHost.HeadlessTestSetup` 提供 Run/Combat、真实命令、图集作用域和 RNG 观察；同目标内比较随机状态，不做跨版本数值比较。
- 其全局 `HeadlessCombatNotEnding` 强制 `IsEnding=false`，不能直接证明最后孢子、真实死亡胜利或奖励点。参考原型 CombatEnd/NativeEnding 输入，采用能走真实 Ending 的最小准备。
- `EncounterCombatScope` 当前 `.Single()` 取怪，不能原样用于女王/多怪遭遇。只补必要准备，不另建大型通用框架。
- 保存复用 `JigsawNativeTests.FirstRoomFlow/BackupSource`；UI 复用 `HeadlessUiHost`、`JigsawPresentationTests.RoutedInput` 的真实入树与 `Input.ParseInputEvent`，直接调用 `_Input` 只证明局部逻辑。
- `JigsawNeowRewards.cs` 当前只见 WingedBoots 排除；黄金罗盘和 Flight 不组合尚须追查准确模型/入口并补回归。不能凭一个断言签“两遗物已排除”。

### 后续验证和交付操作

现有测试用自定义 Cases，先查实际名称再用 `-Filter`，确认匹配/通过数；不要预设不存在的追猎测试名。新实现按每票最小切片扩大到受影响矩阵，收尾执行项目要求的双目标检查/构建与审查。

```powershell
pwsh -NoProfile -File ./tools/ReviewScope.ps1
pwsh -NoProfile -File ./tools/Test.ps1 -Branch All
pwsh -NoProfile -File ./tools/ReadTestLog.ps1 -Path .local/tests/public-beta-last.log
pwsh -NoProfile -File ./tools/ReadTestLog.ps1 -Path .local/tests/public-last.log
# 实施交付时按 development.md 执行完整检查，不是本研究文档的玩法验收：
pwsh -NoProfile -File ./tools/Check.ps1
```

#88 核对 `am_jigsaw_reset` 与 #82 最终余额命令的注册/执行、帮助、权限、Host限制、合法阶段、0/1/N及非法输入，分别保留两版事实。原生控制台限制、Mod标记、成就/解锁/排行规则继续保留。

统一包与文档对应复审后同一实现提交，沿用 `dist/batch-N-integration/AllMatters-{public,public-beta}/` 和 `docs/batch-N-test-guide.md`（N按正式交付分组明确）。包仅含 DLL/PCK/manifest/LICENSE/THIRD_PARTY_NOTICES；RitsuLib完整variant-pack独立安装。Agent只输出到开发目录、提供安装步骤，用户手动安装/启动及真实端操作。#88 交付验收包和声明，不自动正式发布。

共享beta PCK宿主、用户曾声明的public界面通过、完整匹配public正式运行分别记录；真实3端、探索2/3端、服务双端、实体手柄责任按适用场景留证。模拟输入不代签实体设备，不要求本用户提供其没有的设备；不承诺跨版联机、战中任意续打或未知第三方通用兼容。

## 9. 本轮验证记录与后续第一步

本轮已运行 `ReviewScope.ps1`（退出0），读取时 HEAD 与本地 origin/main 无提交差异；工作区原有删除及未跟踪文档保留。已验证可用反编译器版本9.1.0.7988，完成双版各15类型的方法体输出；源码证据仅存 `.local/`。

本轮执行 `Test.ps1 -Branch All`，整体退出0；随后两次 `ReadTestLog.ps1` 均退出0。结果只衡量当前既有宿主/用例，不证明本批尚未实施功能：

|目标|通过数|测试构建/宿主退出码|ModMetadataErrors|EngineErrors|
|---|---|---|---|---|
|public-beta v0.111.0|403|0 / 0|0|22|
|public v0.107.1|405|0 / 0|0|23|

日志没有 FAIL 用例、准备失败或 FirstException，但并非无诊断：beta 首条为多人局战外死亡提示，public 首条为 NCreatureVisuals C# 脚本无法实例化。原日志保留这些上下文；本轮未修改宿主或扩大为完整资源/正式运行通过声明。public 仍有共享beta资源的证据边界。

本轮证据副本留在 `.local/research/batch82-88/`：`public-beta-smoke.log`、`public-smoke.log`；完整调度输出 `.local/research/batch82-88-smoke-console.log`。GitHub 原文缓存位于 `.local/research/batch82-88/issues/`；三路详细研究笔记为 `.local/research/batch82-88-{noncombat,combat,boss-delivery}.md`，其首次工具失败状态以本文所记显式覆盖后成功结果为准。

最终校验：七票均有独立实现准备入口；主要引用路径已核存在；双版方法体各16份（含此前 RewardsSetSynchronizer，本轮新增15份）均非空；文档代码围栏成对、无行尾空白；`git diff --quiet -- src tools Paths.local.props` 返回0。三路子代理已复核，关于终局占位方法、U提示和恢复RNG的措辞已修正。

下一位实施者先领 #82：复查工单/当前代码与冒烟；把当前余额、按时结果、全局追猎和根房受罚快照在领域/保存/恢复投影中表达清楚，再以“剩1入普通根房、全队完成后发面具、下一进入受罚、同点恢复”切片贯通。资源核权、具体宝箱来源适配及真实端/设备证据按对应票推进，不用重新开展已经完成的玩法访谈。
