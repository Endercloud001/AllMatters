# #85 巢穴追猎实施记录（部分实现）

更新：2026-11-13。本文记录 #85 已完成的首六个开发切片及其自动验证范围，供后续集成、实施和验收参考。#85 尚未完成；本记录不代表正式游戏、多人、界面或整场恢复验收通过。

## 本次可集成范围

- Knowledge Demon：受罚巢穴顶层 Boss 中，Slap 为15（A9及以上16），Knowledge Overwhelming 为6×3（A9及以上7×3）。原状态机意图和原 DamageCmd 读取同一伤害 getter；保留原目标、命中次数、动画、音乐及动作推进。Ponder 原动作中的回复固定20，其他独立回复来源保持原值。
- The Insatiable：受罚顶层 Boss 的原 Liquify 动作保留原目标、PetOwner 回退和原生随机插入，Sandpit 为5，Draw/Discard 各4张 Frantic Escape，状态意图为8；后续仍进入原 Thrash。安全分支保持原值。
- Knowledge Demon 三轮选牌：MindRot/Calamity、Sloth/Tender、WasteAway/Disintegration21。仅在原 ChooseCurse 的原生 CardSelect 调用处改变受罚候选输入；原选择上下文、玩家来源、同步/等待及 UI 命令、原 OnChosen、外层逐玩家 Task.WhenAll 和轮次推进保留。Calamity/Tender 为仅供选择的展示适配卡，禁止由生成器或 modifier 生成。展示实例不进永久牌组或战斗牌堆，第三轮21只改本次实例，不改原生 canonical 模板。
- 测试宿主：EncounterCombatScope 增加默认true的可选初始化参数，使本票测试能在实际 PushRoom 和权威根房绑定后首次创建状态机；默认调用不变。没有放松多人/会话安全检查。

涉及文件：`src/Jigsaw/JigsawHuntingActTwo.cs`、`src/Jigsaw/JigsawKnowledgeChoices.cs`、`src/Tests/JigsawHuntingActTwoTests.cs`、`src/Tests/HeadlessCombatHost.cs`、`src/Tests/HelloWorldTests.cs`、Tender 图及 import、`THIRD_PARTY_NOTICES.md`。本批仅审上述范围；下一普通开场测试草稿未包含在已验切片中。

## 实际源码与实现依据

需求以 #63 resolution comment 6584771226 的 H 规则为准，后续以 #73 的完整战前恢复要求覆盖早期原型的战中恢复描述。原型仅提供研究输入，未用其测试通过替代正式实现验证。

本机按目标 DLL 真实反编译并核验 public v0.107.1 与 public-beta v0.111.0。public ProductVersion 为 `0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`，beta 为 `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`；查询位于忽略目录 `.local/api/queries/{public-v0.107.1,public-beta-v0.111.0}/`。

- KnowledgeDemon 的 Slap/Overwhelming 私有 getter 同时供意图和真实攻击使用。Ponder 私有 async 动作内唯一 CreatureCmd.Heal 调用作同签名局部替换，非全局治疗修改。
- ChooseCurse(Creature) 两版存在。外层 public 为 CurseOfKnowledge，beta 为 CurseOfKnowledgeMove；beta在 IsLiveCombat 时推进计数，实际 CombatState.IsLiveCombat 返回true。测试经真实动作循环推进，未反射修改计数器或状态。
- CardModel.Description getter 两版均为 `public LocString Description => new LocString("cards", base.Id.Entry + ".description");`。规范查询 public 第113行、beta 第127行；审查提出的 beta 证据缺口已用实际 ILSpy 导出补齐。
- Liquify 的实际 async 数据流只局部变更 Sandpit 数量、分堆阈值和总卡数，GenerateMoveStateMachine 的唯一对应 StatusIntent 参数同步；均核对两版真实方法。目标匹配不唯一时明确报错。

## 红绿与回归证据

每片先取得实际 public 行为 red，再加入最小生产变化。编译/夹具失败不当作行为 red。下列日志位于开发工作树忽略目录 `.local/issue85/`；它们是本轮开发证据，不要求随开发包分发。

|切片|真实 red|green 重点|
|---|---|---|
|Ponder|`ponder-red2-public.log`，实际30而非20|固定20、原攻击/Strength、另一独立Heal7不改|
|Liquify|`liquify-red-public.log`，Sandpit实际4而非5|5/双堆各4/意图8、原目标及后继、无deck/hand旁路|
|Knowledge 攻击|`knowledge-attacks-red-public.log`，受罚A8意图17而非15|安全/受罚×A8/A9的意图与真实HP伤害|
|第一轮|`pair-one-red-public.log`，仍原版D/M|MindRot/Calamity、原owner、能力1、展示不入堆|
|第二轮|`pair-two-red-public.log`，仍原版D/Sloth|实际第二次原生循环Sloth/Tender、Tender1/安全Sloth3|
|第三轮|`pair-three-red-public.log`，仍原版D/Waste|左右Waste1/Disintegration21及safe8、原生能力历史一次、三轮后无第四次诅咒|

最终统一命令为 `tools/Test.ps1 -Branch All -Filter 'Jigsaw hunting act two' -SkipResourceExport`，当时只包含已验首六项。`knowledge-three-pairs-regression-public.log` 与 `knowledge-three-pairs-regression-public-beta.log` 均为 **6 passed，BuildExit 0，HostExit 0，EngineErrors 0，ModMetadataErrors 0，无异常**。每轮独立 `pair-{one,two,three}-green-{public,public-beta}.log` 也保留。未宣称全量 Check 已通过。

夹具曾过早生成状态机，且 beta 拒绝二次初始化；测试已改为只初始化一次，过程日志保留。三轮测试只使用 CardSelectCmd.UseSelector 观察原命令给出的选项及应用效果，不把它算作屏幕或网络证据。

## 资源与审查

Tender 图为用户提供的1,529,010字节原件 `tender.png`，按 #63 设计用于本项目展示卡，未裁切、缩放或转换。实际路径及独立来源声明见 `THIRD_PARTY_NOTICES.md` 的 Knowledge Demon Tender choice artwork 条目；代码的 GPL 不重新许可该美术。Calamity 卡面与原生能力文字/效果从安装游戏加载。图文件及 import 已接入资源路径，但本轮使用 SkipResourceExport，未将文件存在写成图像已加载或界面通过。

独立 Spec 审查对本片无偏差；Standards 的双版 Description 源码补证已复核通过，无硬发现。重复 scope 条件为非阻塞维护建议，未为建议重构已绿实现。本结论只适用本片，不能代签 whole #85。

## 仍须完成

- Hive 顶层普通开场已由下述增量实现；精英开场规则、事件只普通增强、Crusher/Rocket 固定 Artifact3及原包围/方向、实际完整场地布局仍待。
- 真实1–4玩家逐 owner 选择、原生网络同步与独立完成；不能在 singleplayer 塞入多角色绕过会话边界。
- 正式 Godot 选择屏的真实打开、选择、关闭/free、任务仅续一次、输入路由及回到战斗焦点；public需匹配其正式资源，beta PCK不能代签。Tender纹理、描述格式化和长文本布局仍待实际验证。
- 入口前死亡、等待期间死亡/断线/缺席、重复/迟到选择、退出/恢复后的旧任务清理；原生远端 WaitForRemoteChoice 无取消token，Dispose仅注销handler，不能以局部关闭token声称安全。
- 消费后续 integration 已评审的完整战前/胜利恢复接缝，验证同一完整点含 RNG、能力、个人状态和半份选择丢弃；本片未修改或合并共享 checkpoint。
- 最终双版组合检查、正式运行/人工设备验收及完整 #85 审查。Agent不启动Steam/游戏、不改实际安装或存档。

后续每个独立行为继续先真实 red→最小 green，按主控分配的原生测试窗口串行验证。具体开发工作材料见忽略目录 `first-slice.md`、`curse-continuation-plan.md`、`hive-opening-next-slice.md` 和 `current-review-scope.md`。

## Hive 普通开场增量（2026-11-13）

以部分提交 `4641f0f` 为起点，在原生 `Hook.BeforeCombatStart(IRunState, ICombatState?)` 原 Task 完成后，对受罚 Hive 顶层普通战斗的实际 combat 玩家逐一经原生 PowerCmd 施加 Weak1、Frail1、Vulnerable1。限定当前原生 run、同一当前 CombatState、非事件普通房及权威根房受罚状态；不改变通用会话安全检查，不从面具或剩余步数推断。没有新增一次性标记或保存字段，沿用每场原生开场调用时机。精英、事件及蟹的后续规则未在本增量冒充实现。

双版实际 API 查询：`Hook.BeforeCombatStart` public第220行/beta第311行，均先等待原生 BeforeCombatStart 与 BeforeCombatStartLate；CombatManager 原生首次调用位于public第273行/beta第594行。`SpinyToadNormal` 两版为真实 Hive 普通单怪遭遇，PowerCmd 对玩家的负面能力不走敌方多人缩放路径。

真实 public red：`.local/issue85/hive-opening-red-public-host.log` 中 BuildExit0、HostExit1，合法受罚根绑定成功后，实际 hook 尚未给出三种能力，断言失败。无编译或夹具错误冒充行为 red。最小修复后，`hive-opening-green-all.log` 定向双版各1通过；统一 `tools/Test.ps1 -Branch All -Filter 'Jigsaw hunting act two' -SkipResourceExport` 的 `hive-opening-final-all.log` 退出0，双版各7通过。最终归档 `hive-opening-final-{public,public-beta}.log` 的 ReadTestLog 均 BuildExit0、HostExit0、EngineErrors0、ModMetadataErrors0，FirstException/FirstDiagnostic/PreparationFailure为空。

新用例实际读取三种能力和原生 History（各一次Amount1、目标为玩家），未受罚对照均无附加；之后同目标另一原生命令施加 Weak4，实际总量安全4/受罚5，证明未全局替换其他来源。测试仍是单实际owner，使用真实 native Hook 和命令；未证明1–4正式联机、事件分支、实际界面、整场恢复或手动游戏验收。本增量仅精确暂存生产、测试及本记录供独立双轴审查，尚未提交或运行完整Check。

### Hive 普通开场独立双轴审查

2026-11-13，两名只读审查者分别检查 `git diff --cached 4641f0f -- src/Jigsaw/JigsawHuntingActTwo.cs src/Tests/JigsawHuntingActTwoTests.cs docs/issue85-hunting-implementation.md`，实际读取源码、仓库标准、规格和双版方法体，没有重跑测试。

- Spec：当前顶层普通开场切片无阻塞项；符合 H 表 Weak/Frail/Vulnerable 各1，原Task后追加、当前run及CombatState身份和权威根房判定正确，独立Weak来源保留。1–4玩家、Artifact、完整开场调度及恢复后一次仍待后续验证。
- Standards：无阻塞项。双版Harmony目标签名与实际调用已核，测试清理覆盖所有await。唯一非阻塞命名建议是将已扩展到普通房的测试helper `BossRoot`/局部`boss` 改为通用目标房名，避免误读；本次不为此改变已绿行为。

本结论不覆盖知识恶魔旧六例的重新审查，也不代表 #85 整票完成。

### 集成后非 Jigsaw 开场回归修复（2026-11-13）

集成 `7d5070a` 的完整 Check 在 public/beta 均实际失败：既有 `NativeCounterTests.Ember` 的原生 `Hook.BeforeCombatStart` 使用无幕列表的普通测试 run，新增全局开场 postfix 在确认 Jigsaw 会话之前读取 `RunState.Act`，触发 `ArgumentOutOfRangeException`。这是本次增强引入的作用域检查顺序错误，不能归咎为既有 Ember 夹具或以放宽会话检查修复。原始双版日志及完整 Check 控制台先归档至 `.local/integration-slices/hive-guard-regression-20261113/original-{fullcheck-public,fullcheck-public-beta,hook-console}.log`。

新增真实负例 `Jigsaw hunting act two native opening leaves a non Jigsaw run with no acts untouched`：创建 `RunState.CreateForTest(..., acts: [])`，确认无 Jigsaw session，调用原生开场 hook 并断言未附加三项能力。该负例先在未修生产上重现相同异常，`no-acts-red-public.log` 为 BuildExit0/HostExit1，非编译错误。

最小修复只重排同一 guard：先确认原生 RunState、owner 及 `RootIsHunted`（内含对应 run 的 session 查询），再读取 Act 和房间；当前 run、同一 CombatState、顶层普通房及事件排除条件均保留。未更改其他无直接失败证据的 guard，也未新增保存字段、标记或 gate。

修复后 `tools/Test.ps1 -Branch All -Filter 'Jigsaw hunting act two' -SkipResourceExport` 双版各8通过，既有 `Native counters ember tea grants Strength and Dexterity for five event combats` 双版各1通过。归档同目录 `act-two-green-{public,public-beta}.log` 与 `ember-green-{public,public-beta}.log`，ReadTestLog 均 BuildExit0、HostExit0、EngineErrors0、ModMetadataErrors0，三项异常/诊断/准备字段为空。完整 Check 尚待主控重跑，本轮专项通过不能把原失败完整检查改写为通过。

独立Standards与Spec分别实际读取本次三文件暂存差异、相关会话/原生/测试源码及归档红绿日志，均0发现、无阻塞项。确认没有更改Ember夹具、扩大作用域或新增状态；新增负例直接调用真实Hook。两审查者只读，未重跑测试，整票partial和完整Check重跑责任不变。
