# #86 荣耀追猎：部分实施记录

记录日期：2026-11-13。工作树 `feat/issue86-hunting`，起点 `f754aa24dee2cc1f660b6153c834c1a571aadd94`。当前只记录四个已验证用例和对应两类精英改动，尚未提交；不代表 #86 整票完成、正式实机通过或发布许可。共享战斗/完整检查点接缝尚未从后续集成分支消费。

## 已实现及真实来源

|内容|实际实现与验证|明确边界|
|---|---|---|
|MechaKnight 开场 Artifact 固定 5|仅受罚 Glory 实际顶层 Elite 根房替换原开场授予。创建确切 mutable ArtifactPower，原生 PowerCmd.Apply 一次；临时 ConditionalWeakTable 仅对该实例的一次多人缩放豁免，消费后及 finally 清理|安全房保原生 3+(人数−1)。不全局改 Artifact，不使用跨 await 的目标级 AsyncLocal；同目标其他来源正常缩放|
|异步重入来源隔离|真实 BeforePowerAmountChanged hook 等待后给相同 target/applier/null card 的另一个 Artifact 来源授予 2；实际收到 2+(人数−1)。原生移除临时能力后续原开场，PowerReceivedEntry 依序记录独立来源及固定 5|临时移除仅是测试构造，生产没有先给后回收。该案例证明具体重入反例修复，不声称全部第三方 hook 通用兼容|
|MechaKnight 首招喷火|开场授予完成后通过 SetMoveImmediate 选择已有 FLAMETHROWER_MOVE。真实首次 RollMove→PerformMove；保留原生动作与意图对象|1–4 combat 角色、A0/A9、安全首 Charge 对照；public 首喷火直接伤害 0、beta 8/A9为12，每个目标实际 Hand 有 Burn 4。原 Windup15盾/+5力量→Heavy Cleave→Flamethrower 连续 Roll/Perform 通过|
|SoulNexus 开场 Territorial 1|postfix 等待原 AfterAddedToRoom Task 完成后，限定受罚 Glory 实际顶层 Elite，以原生 Apply 授予 1；保留原 Died 订阅。PowerReceivedEntry 验一次，安全零次|正式 public Hook.AfterTurnEnd / beta Hook.AfterSideTurnEnd：玩家方结束不加力量，两次敌方结束分别力量 1/2，不随角色数倍增。不是完整 readiness/回合调度测试，也未验证 Died 场景动画|

双版本方法体记录在本机 `.local/api/queries/{public-v0.107.1,public-beta-v0.111.0}/`。MechaKnight.AfterAddedToRoom 原方法仅 await 空基类后授予 Artifact3；原状态机 Charge→Flamethrower→Windup→Heavy→Flamethrower；public Flamethrower 只有 Burn4、beta 先原伤害再 Burn4。SoulNexus.AfterAddedToRoom 两版第43行，原 Died 订阅第46行；TerritorialPower 原生按参与方包含 Owner 时授予 Amount 力量，不另写能力行为。

## 红绿与最终自动证据

日志均相对本票 worktree `.local/issue86/`，为当时已执行记录，不是本次文档生成时重新运行。

|切片|真实 red|green|
|---|---|---|
|固定 Artifact5|`mecha-red-public.log`：public 原生初始3≠5，测试构建0/宿主1|首片双版各1，见 `mecha-green3-all.log`|
|同目标异步重入 Artifact|`mecha-reentrant-red-public.log`：两角色时另来源被旧目标级范围压为2，原生应3；构建0/宿主1|移除 AsyncLocal，确切实例范围；`mecha-reentrant-green2-all.log` 双版各2，包含首片回归|
|SoulNexus Territorial|`soul-red-public.log`：一角色受罚开场原0≠1；构建0/宿主1|`soul-green-all.log` 双版各1|
|首次原生喷火|`mecha-flame-red-public.log`：一角色受罚首次 Roll 仍 Charge；构建0/宿主1|`mecha-flame-green-all.log` 双版各1，随后扩大A0/A9矩阵并最终同组回归|

最终命令：`tools/Test.ps1 -Branch All -Filter 'Jigsaw hunting act three' -SkipResourceExport`，归档 `soul-mecha-final-all.log`，退出0；public 与 public-beta **各4项通过**。ReadTestLog 两版 Test BuildExit0、HostExit0、EngineErrors0、ModMetadataErrors0，FirstException/FirstDiagnostic/PreparationFailure 均 null。未执行本票完整 Check 或正式发布 Build，不把测试构建成功写成最终包已生成。历史第一次 reentrant green 的 using 位置编译失败已修，属于夹具/编译错误，不列作行为 red。

public v0.107.1/product `59260271157f76a2896f0eab5bc6ea1245d8b314`；beta v0.111.0/product `41cef1ea4657c524aa50e870df009e56337e8c32`。编译依赖 RitsuLib0.6.7；用户实际 Workshop0.6.10 仍待集中安装运行归属。public 宿主共享 beta PCK，不代签正式 public 场景资源。

## 部分审查与暂存边界

主控已组织独立 Standards 与 Spec 双轴审查四个用例及对应生产改动，两轴无硬发现；测试初始化重复仅为非阻塞维护建议，本片不扩大重构。

当前已审暂存代码精确为 `src/Jigsaw/JigsawHuntingActThree.cs`（80行）、`src/Tests/JigsawHuntingActThreeTests.cs`（282行/四case）、`src/Tests/HelloWorldTests.cs`（Concat一行）。本实施记录尚未暂存。未暂存 Flail 新测试草稿、Mystic 五资源或资源导出产生的既有 `.import` 变化。提交号待实际提交后补录，不预写。

1–4 为原生 combat 角色输入，不是1–4真实游戏端，也不是创建怪物前的完整HP缩放测试。session 保持单实际 owner，未放宽 #82 原生联网/角色名单安全检查。没有启动游戏或 Steam，没有安装或改真实存档。

## 仍需继续的 #86 工作

- Flail：已实现并通过下述独立行为验证，尚待本增量独立审查。
- Mystic：普通/事件战追加一只基础56HP、无初始能力、非Minion，CAST→HEAL→BUFF。六状态逐活玩家独立随机、受伤存活含自身治疗12、存活敌人含自身力量2。资源仅准备，尚未正式接入/加载/NOTICE；不以文件存在写成通过。
- 本票各场景完整战前与胜利奖励点恢复、事件子战/同名精英普通增强、安全根快照、HP缩放一次、RNG及一次增强仍须真实端点验证；先正常消费主控最终共同接缝，不另造检查点或第二追猎开关。
- 全12个 Glory 普通原生 scene 及事件追加、精英实际布局，完整1/4人紧凑/宽屏的真实意图/血条/目标/焦点，正式 public资源与真实端/设备集中验收仍未交付。不能用单怪 EncounterScope、临时 Marker 或少数代表怪代签。

部分实现可以先审查集成以消费后续接缝；不关闭 #86、不承诺整票完成，也不将这些剩余实现转交 #88 兜底。

## Flail 增量（2026-11-13）

以部分提交 `a5ba509` 为起点，仅追加连枷动作 postfix：原生两击完成后，对追猎 Glory 顶层精英战的全部实际 combat 玩家各原生施加 Tangled1。保留原生目标、伤害、Artifact 阻挡/消耗、Entangled 附魔与额外能耗、玩家回合末移除逻辑。

public 真实 red 位于 `.local/issue86/flail-red-public.log`：hunted 分支缺 Tangled 断言失败，exit1。此前清理断言误把所有原生攻击牌视为1费的 fixture 问题已改为精确检查本测试已有/后生成 Strike，不计为行为 red。beta 的多角色 SetUpCombat 前未初始化状态导致 hook 空引用也已按现有宿主时序修正，不改生产机制。

Flail 覆盖1–4 combat角色 × 安全/追猎 × 有无Artifact，共16组；在原生目标参数刻意只传一名玩家时，仍验证原两次9伤害命中所有原生对手、全部玩家施加能力、已有与新增攻击牌1能量不足而2能量可用、技能不变、玩家回合末清理。run/session始终仅一个实际owner，不代表多端。

定向双版本 `.local/issue86/flail-green-all.log` exit0；统一 `.local/issue86/flail-final-all.log` exit0，各5 PASS。两版 ReadTestLog 均 BuildExit0、HostExit0、EngineErrors0、ModMetadataErrors0，FirstException/FirstDiagnostic/PreparationFailure为空。未运行完整Check，未安装模组或启动真实游戏。当前Flail增量仅精确暂存生产、测试及本记录，未提交；此前四用例审查结论不自动覆盖本增量。

### Flail 独立双轴审查

2026-11-13，两名独立只读审查者分别检查 `git diff --cached a5ba509 -- src/Jigsaw/JigsawHuntingActThree.cs src/Tests/JigsawHuntingActThreeTests.cs docs/issue86-hunting-implementation.md`，读取实际完整源码、标准及双版反编译方法；均未自行运行测试。

- Standards：无阻塞项。核实双版 `FlailMove(IReadOnlyList<Creature>)` 的 Task 签名、原生命令、能力来源及测试实际断言；唯一非阻塞建议是把全体HP总差进一步细化为逐玩家18伤害/两次伤害History断言。原方法体与未替换原动作提供补充依据，当前总差断言本身不单独证明每名玩家两击。
- Spec：当前切片满足 #73 引用的 #63 H 表“连枷骑士使用连枷时，额外给所有玩家1层缠结”。等待原动作、对全部实际combat玩家施加、顶层受罚Glory精英范围、原生Artifact/攻击附魔能耗/末清理保留，未发现实现错误或范围扩张。

两轴均明确：单人传输下的战斗角色测试不等于真实多端；Mystic、完整布局与整场恢复仍是 #86 未完成项，不由本次通过代签。
