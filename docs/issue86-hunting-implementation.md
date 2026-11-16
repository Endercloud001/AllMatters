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

## Mystic 原生模型资源与三动作闭环（2026-11-14）

当前源码 `6aa65b4` 经 `7b08841` 集成，目标public0.107.1/beta0.113.0、RitsuLib0.6.11，匹配资源。覆盖此前Mystic“资源仅准备、尚未接入”的旧状态；本增量仍不关闭#86。

新增正式Mystic模型，基础HP56沿原生CreateCreature多人缩放一次，无初始能力/Minion。受罚Glory普通及事件根在原生Generate阶段追加唯一mutable模型，原怪/slot顺序保留，safe/nonJigsaw/其他幕/Elite/Boss排除。CAST→HEAL→BUFF循环分别以原生Rng逐活玩家独立六选一状态至其弃牌堆、受伤存活敌人含自身治疗12（无目标不取随机数仍推进）、全部存活敌人含自身固定Strength2；保留原生命令/历史及原生不额外人数增层的Strength规则。

固定资源源 `MT-SUPER-POWER/STS2-Balance-MOD@d9eff9e067b07689e722c5283e7c3e7c451961ca` 的Mystic五件，PNG/二进制骨骼不变，场景/atlas路径适配并去foreign script，NOTICE保留Balance/ActsFromPast/MegaCrit归属与原美术权利边界。真实Ritsu factory创建NCreatureVisuals，Spine Idle/Attack/Hit均实际播放，Attack/Hit完成回Idle；英中文名称及动作标题实际解析。

真实public原入口在模型接入前报expected1/actual0，日志实施树`.local/executor-mystic/red-public-host.log`；默认导出新增资源后原入口green。扩测试前两轮分别暴露owner二次绑定和额外角色过早进入原生牌组Populate的夹具错误；第三轮逐次卡/Rng预测吻合但覆盖不足，当前public CreateCreature在CurrentMapCoord为空时丢弃run seed，退化Act+CombatId。夹具补真实Jigsaw投影/visited coord后，不重设monster.Rng，保留64采样上限即可覆盖全部六状态及相同/不同玩家结果。这些夹具失败没有冒称产品red。

最终实施`final4-{public,public-beta}-host.log`各6 PASS，四诊断0；共享生成入口受影响的既有Overgrowth组`overgrowth-regression-{public,public-beta}-host.log`各6 PASS、四诊断0，均位于`mystic-native-20261114/.local/executor-mystic/`。集成`7b08841`执行默认资源导出`Test.ps1 -Branch All -Filter 'Jigsaw Mystic'`双版各6 PASS、Build/Host/Engine/Metadata全0；日志集成树`.local/executor-20261114/mystic-integrated-{public,public-beta}-host.log`。Asset not cached及编译/格式化提示按原日志保留，不写零warning。

双轴独立只读审查13文件637+/2−均0发现；正常hooks提交变为641+/2−，只将四个single-line if的return/continue换行，Standards增量复核确认无语义变化。1–4为原生combat角色而非真实多端；完整12普通场景及精英布局、完整战前/胜利奖励fresh恢复、真实端与设备仍属#86后续，不能以临时Marker/factory局部通过代签。

## Mystic 完整战前与非空胜利奖励 fresh（2026-11-14）

源码`9e52b1c`经`fa41442`集成，仅新MysticRecovery与Hello注册两文件610+，生产/共享checkpoint零改。目标public0.107.1/beta0.113.0、RitsuLib0.6.11，匹配PCK。首个真实受罚Glory普通session.Enter独立采样before-roll及原开场，真实CAST→HEAL→BUFF用克隆monster RNG预测状态牌/治疗12/随机活敌Strength2及真实history，再执行一次CAST停HEAL；实际损血、金币、手牌移弃牌、run/owner RNG改变后保存。独立完整开场含全部玩家保存字段、五堆有序card.ToSerializable、Energy/Block/powers、run/owner/monster RNG、原Act/map/history与敌人slot/HP/powers/intent。

首个新LoadRunLobby ready经原SetupSavedMultiplayer/GenerateMap/LoadIntoLatestMapCoord取得新run/session/room/Mystic身份，完整开场相等，Mystic唯一、HP56缩放一次、无初始能力/Minion、CAST重置；再次真实三动作证明循环与RNG原样，重复进入不扣步/不重建。真实全敌死亡→原CheckWinCondition自然prefinished保存→原Offer生成非空金卡及实际roll出的药水；原奖励真skip后第二新大厅从胜利点恢复，不增加opening/startup、无敌人。保存前room/map与奖励生成后完整玩家/RNG/history及奖励内容分阶段精确比较；原SelectLocalReward实际领取，独立预采样预测金币、卡身份/升级、药水及本端首次发现，完整原牌组保持；重复完成stack选择抛原InvalidOperationException且完整状态不变，原Proceed自然完成根一次，没有测试手调CompleteCurrentRoom或末尾补save。

独立Standards/Spec初审无硬问题；采纳共享内存profile污染建议，完整SerializableProgress JSON往返切断可变统计对象后原ProgressState.FromSerializable重建独立profile，finally还原原引用并核身份/完整原内容不变。两轴18行增量通过；原战斗/奖励oracle未改。正常hook成功，提交patch与已审patch完全一致。首beta最小专项1与初final双版各7均全0；最终profile-final双版各7（旧Mystic6+新fresh1）PASS、Build/Host/Engine/Metadata0，无RID/ObjectDB，日志实施树`mystic-fresh-20261114/.local/executor-mystic-fresh/profile-final-{public,public-beta}-host.log`。

集成`fa41442`实际执行`Test.ps1 -Branch All -Filter 'Jigsaw Mystic' -SkipResourceExport`，双版各7 PASS、四诊断0、无RID/ObjectDB；日志集成树`.local/executor-20261114/mystic-fresh-integrated-{public,public-beta}-host.log`。证据为单owner同进程两次新run/session/lobby；普通非空奖励恢复不代签正式领奖UI、新进程/真实多端、全部12普通及精英场景布局，#86仍partial。

## Glory 正式布局与原生输入（2026-11-14）

源码`a050e7b`合入`21a563f`：原生scene的Mystic追加位置按实际怪物目标框和HP条确定，自动布局只作用受罚Glory普通战，保留原生场景标记；实测Axebot目标框和Construct血条遮挡后修复。12普通及3精英拆成独立case，各自覆盖safe/hunted、1/4角色和1280×720/1680×720，核真实HP文本、可见与尺寸、意图数字和描边包络、画布边界、原生鼠标选择及方向导航；原生玩家间已有重叠按同场safe面积对照，不将其当新增错误。

单个普通大case曾触发60秒watchdog且无PASS；拆分后完整双版各15 PASS。Spec审查发现数字文本及可辨尺寸未约束，返修后以同场safe原生文字高度及实际实体缩放为基准，保留真实HP当前/最大值、文本完整、意图内容与边界检查；Standards核双版API记录，返修复审均无阻塞。实施双版各15及相邻Forest1＋Mystic2（双版合计各3）PASS，集成后双版各15 PASS，Build/Host/Engine/Metadata均0；日志分别位于`glory-layout-20261114/.local/executor-glory-layout/`及集成树`.local/integration-slices/glory-layout-integrated-{beta,public}-host.log`。正常hooks仅调整单行continue格式，审查确认无语义变化；真实设备及多游戏端仍待，不能代签整票。
