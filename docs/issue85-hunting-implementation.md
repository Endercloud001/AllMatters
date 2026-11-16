# #85 巢穴追猎实施记录（部分实现）

更新：2026-11-13。本文记录 #85 已完成开发切片及其自动验证范围，供后续集成、实施和验收参考。#85 尚未完成；本记录不代表正式游戏、完整界面或整票恢复验收通过。自动化三端范围见文末 Holes 增量。

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

- Hive 顶层普通/精英和事件普通增强已由后续增量实现；Crusher/Rocket 固定 Artifact3及原包围/方向见本轮蟹片；实际完整场地布局仍待。
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

## Hive 事件开场增量（2026-11-13）

`070a0ae` 从 `c469b9a` 开始实施并集成：两层 EventRoom→CombatRoom、父ID匹配且已接受受罚 Unknown 根的 Hive 子战，统一以原生 PowerCmd 施加 Weak/Frail/Vulnerable各1；事件具名Elite/Boss也用普通规则，顶层Elite保留Weak2。原Hook任务及异常保留，原任务、每个命令await后重核run/房间/combat/map/根身份。无新增applied标记或存档格式。非Jigsaw空acts且已压战斗房的真实负例先双版越界，再恢复Root检查先于Act读取。

新增8项测试包括1–4combat角色×Artifact0–3、刚扣至0安全根与身份负例、构造事件Elite/Boss对照、真实共享Holes页经同步器选择AM_FIGHT及原coordinator同Hook/startup任务、真实Transient首攻与下一完整玩家回合，以及原Hook/首Weak命令实际依赖await中的失效和原异常透传。初始三项各1/history恰3；次回合public按原生跳首次tick仍各1，beta按原生各降0且精确新增三条-1，开场Hook仍仅1次、根/步数/房间历史不变。原生版本差异来自PowerCmd对SkipNextDurationTick的条件，未改生产来统一结果。

正式Standards硬规范0/smell0、Spec阻塞0/范围扩张0，均实读相同三路径807+/12-、当前两版API及原始日志；Spec审查者仅贡献注册1行和执行，未编写生产/新测试，实施者未自审。正常hooks前后patch一致。合入后同070a0ae双版各Hive10（旧Elite2+新增8）、ActTwo8、EventCombat4，共44 PASS；Build/Host/Engine/Metadata均0，日志兼容树 `.local/host-20261113/hive-integrated-{hive,acttwo,eventcombat}-{public,public-beta}-host.log`。新片红绿和最终10项原日志在保留的hive-event worktree `.local/executor-hive-event/`。

该事件开场片当时尚未覆盖Holes恢复，不能以当时Colosseum/DenseVegetation的4项回归代签。后续单owner恢复证据见下段；连续两独立子战、正式布局/UI和真实多端仍待本票后续。当前未重复全量509/507、未完成完整Check或最终同提交双版包，#85仍部分实现。

## Holes 单 owner 完整检查点（2026-11-13）

`8869289` 从 `1d6515e` 实施：只扩精确 FieldOfManSizedHoles/HolesEncounter/resume=true 组合，沿既有完整原生来源保存/恢复，有限父页与owner RNG保存在既有holesPage扩展。重建后父页保留原生锁定AM_FIGHT选项，实际调用不改变状态，直到真实Proceed/Resume结束专用叙述；奖励保存前刷新owner RNG，再由Ritsu ToSave Postfix导出。未新增全局恢复框架或存档格式。

7项新测试覆盖真实AM_FIGHT战前与奖励恢复、实际领取/Proceed及完成点重载、连续两次完整回退、安全Unknown本次扣至0、真实保存单字段变异的4种非法组合，以及受控真实pending-option正常完成与同entry换map拒绝。玩家与所有run/owner event/monster RNG均独立完整比较；beta显式IncludeFields包含counter和四个state，真实推进后断言快照变化。原生history以其JSON规则规范化空Variables，不删除字段。未通过改动原生衰减、奖励生成或等待API语义取得通过。

正式Standards与Spec初审各有1条非阻塞建议，复用现父页快照并补完整monster RNG敏感性后，双方增量复审均0发现。正常hooks仅对两生产文件排版，四文件1184+/9-。合入后同8869289两版各Holes14+Checkpoint8+EventCombat4，共52 PASS，Build/Host/Metadata均0；Holes每版1条Engine为预期旧请求拒绝，余四跑Engine0。日志兼容树 `.local/executor-holes-integrated/holes-integrated-{holes,checkpoint,eventcombat}-{public,public-beta}-host.log`；原红绿与六次完整monster RNG专项保留在Holes工作树 `.local/executor-holes-checkpoint/`。

范围仍限单owner。真实多owner/三进程恢复紧接本票后续，不转#88；奖励用例显式CompleteCurrentRoom只模拟宿主缺地图UI退出的边界，不能代签正式UI全链。连续独立事件战、正式布局/输入、真实客户端、完整项目Check与同提交最终包仍未完成。

## Holes 三端战前保存与全新三端恢复（2026-11-13）

`f9ccd3c` / `e088feb` 只增加 `JigsawHuntingHolesMultiownerTests.cs` 并扩展既有 `JigsawRecoveryNetworkTests.cs` 分派。真实ENet三端先验证单票/混合票不执行，再一致投票登记三个真实Chosen任务，owner3延迟时无半份战前点或子房；其gold、owner event RNG和run RNG最终效果全部完成后捕获完整保存。随后结束原进程，启动三个新PID，经真实LoadRunLobby从host存档恢复父子房、完整玩家、run/owner/monster RNG、敌人开场、有限锁定父页、检查点、地图根房及history；一次开场、不二扣，实际调用旧锁定动作仍不改变完整状态。

两处oracle按原生阶段修正并各自保留完整比较：checkpoint在原生NativeEnter之前；首次入房的SyncPlayerDataMessage会在其后合并远端discovery，而fresh房栈直接重建。恢复Players expected取最后owner完成时的独立完整采样，并分别核对Before和真实canonical save；首次开场只允许已证的远端Mask discovery变化，其余完整玩家字段不变。新大厅active roster取当前原生大厅，使用LoadListener入房前独立完整验证的RestoredMap.Players；不排序数组、删字段或以恢复actual生成expected。三端面具实体均保留，临时诊断probe已移除。

初版及roster增量分别独立Standards/Spec审查，均0剩余发现；正常hooks后提交内容与已审diff一致。集成树同`e088feb`运行 `./tools/Test.ps1 -Branch <target> -Filter 'Jigsaw recovery native load lobby event-' -SkipResourceExport`，public v0.107.1及beta v0.113.0各2 PASS，覆盖原event-combat-lag与新增event-holes-lag，Build/Host/Engine/Metadata全0；日志 `.local/executor-holes-integrated/event-pair-final-{public,public-beta}-host.log`。这是自动化真实独立端进程；三端胜利奖励片见下段，连续独立事件战、正式布局/输入及真实客户端仍由#85后续承担，当前未重跑完整Check或最终同提交双版包。


## Holes 三端胜利奖励保存与全新三端恢复（2026-11-13）

`31e849a` 仅扩既有两测试文件328+/16-，沿用三端真实ENet投票/owner延迟/开场，再实际击杀并等待原生胜利保存。双版合法自动保存点在 AfterCombatVictory/玩家战后清理/history 更新之后、MarkPreFinished→SaveRun，先于奖励生成。领取完成栈只在session内；本片没有证明或虚构部分已领存档的重建边界。

四阶段来源：`CaptureRewards` 的既有保存回调包装在真正写入前独立采样完整玩家、run/owner RNG、history、map，再核对实际canonical保存及live稳定；原进程真实生成后三owner的完整奖励身份和完整状态构成fresh expected；新大厅Map.Players仅采用LoadListener在入房前以checkpoint+Connected独立完整核对的roster；领取前由完整来源数组、profile事前seen集合和两版PotionCmd/ProgressSaveManager规则预测每端药水发现记录。字段和顺序均保留，不从fresh actual回填恢复预期。

全新三个PID经真实LoadRunLobby加载同一个host胜利点，不重开战斗Hook，逐项比较完整奖励、玩家、run/owner RNG、父页、Point/Page、地图、history及原生房描述。真实同步领取核对金币、原完整牌组前缀、两张新增牌身份与升级、药水；已完成本端空stack的重复调用在发送消息前拒绝并完整不变。领取后全状态跨端比较按各端独立发现预期变换，这是一致性证据；独立领取oracle范围为前述库存及发现记录。真实Proceed结束三名owner专用叙述，完整expected只允许父页完成/清空选项和NativeHoles.ClearFight四字段变化，其余完整状态不变。

实施树同源码双版运行 `./tools/Test.ps1 -Branch <target> -Filter 'Jigsaw recovery native load lobby event-' -SkipResourceExport` 各3 PASS，含旧event-combat-lag、Holes战前、新event-holes-reward，Build/Host/Engine/Metadata全0。日志 Holes 树 `.local/executor-holes-reward/event-final-{public,public-beta}-host.log`；奖励证据beta `d598a811-4c7a-4593-af53-295a433fe167`、public `5e4d5be2-ee71-4de2-90e0-92b0db6100b8`，各三source capture+三fresh restore、六个.err为空。独立双轴0发现，正常hooks前后283个tracked C#字节不变、提交diff与已审一致。

集成树同 `31e849a` 串行复验相同 `event-` 组合，public / beta 各3 PASS，Build/Host/Engine/Metadata全0；最终日志 `.local/executor-holes-reward/integrated-event-{public,public-beta}-host.log`。奖励目录分别为public `5bda2fe6-1044-4435-8980-4c96945df0f1`、beta `2a588df1-0b24-44c0-9f1f-a54de92fe69a`，各三capture+三fresh、六.err为空。

本轮两次行为返工分别揭示夹具遗漏原生访问坐标（旧战前专用加载前缀曾掩盖）和领取后的本端药水发现规则；另一次末尾断言变量重名仅是编译失败，未算宿主red。详细等待/实验记主仓controller-state，不新增流程系统。该奖励切片当时未包括完成点第三轮 fresh；其新增证据见下节。连续独立事件战、正式布局/输入/真实客户端、全量Check与最终同提交包仍待，#85未整票完成。


## Holes 全员退出后的完成点保存（2026-11-13）

父事件结束没有使多人根房成为持久化完成点。真实 `NMapScreen.Open(false)` 经生产补丁提交本端 owner 的 `CompleteRoom` managed action；所有端完成认证后，live 根仍保留给下一目的地投票，旧保存也因此保留根节点。首 public 窄例实际在 host canonical 保存比较失败：只有 Map 与 Point.Map 各自的 CurrentNode、RootHunted、Completed 三字段未提升，其余完整玩家、RNG、history、Page 一致。

本增量仅对受罚 Holes 专用 AM_FIGHT 结束页补保存副本投影：当前会话、单层匹配父房、最后历史为 HolesEncounter、无战前 EventCombat、所有在局 owner 的真实退出认证和原生必要流程均完成，才复用已有宝箱的 copy-map／CaptureDocument／finally 恢复 live slot 路径。live 根、完成认证与下一目的地投票不被提前清除；未退出或仅两人退出时不提升，重复保存也只追加一次根完成，不增加探索步数。

|阶段|expected 来源|端点与 profile 归属|
|---|---|---|
|领取及 Proceed 后|上一阶段完整 expected，只应用已证父页完成／ClearFight 规则|每端各自保留领取前预测的发现数组；不能互换客户端 profile|
|未退出／两端退出的 host 保存|操作前完整状态；正常 ToSave 先以已独立核验的 live map 刷新 Point.Map|只有 host 保存并刷新 live slot；客户端仍保留旧 Point.Map 名单顺序|
|全员退出的完成保存|操作前完整 map 仅追加 root 到 Completed、清 CurrentNode／RootHunted，同时应用于 Point.Map|实际 host canonical save；完整 Players 含 host 视角的有序发现数组|
|第三轮全新大厅及入房|已独立验证的 host 完成保存；live Map.Players 仅采用 Listener 入房前独立完整核验的当前 roster|三个新进程均恢复同一个 host 文档，不继承第二轮各客户端的本地发现；Point.Map 保持保存归属|

测试复用真实胜利保存→新三端领取／Proceed，再由各端调用一次真实 `Open(false)`；宿主只提供裸 NMapScreen，未派发正式事件按钮或设备输入。根仍在 live 房内，OnEdited 不自动保存；测试显式调用原生 SaveManager.SaveRun(null)，仅替代已有隔离 IO，不声称自动 UI 保存。四次 host 实际保存覆盖无退出、两端退出、全员退出、重复完成；每次 canonical 的 Players／全部 run RNG／history／Page／Point／Map 完整比较，并核 live 状态只发生正常 Point.Map 刷新。

源码提交 `1956c17`，四文件226+/27-。固定异序名单后的窄例双版各1 PASS，最终 `event-` 相邻组合双版各4 PASS，Build／Host／Engine／Metadata 均0；日志 Holes 树 `.local/executor-holes-completed/ordered-budget-{public,public-beta}-host.log`、`ordered-event-final-{public,public-beta}-host.log`。最终组合完成点目录 public `02248181-7be4-4904-85e4-91461936f65a`、beta `9bb508e1-7242-40b8-b43f-f89bed9ab8c3`，每例三轮九个端点.err均空，第三轮三条 HOLES_COMPLETED_ENET 均为 MapRoom／opening=0／rewards=0。完整已领库存、host 发现数组、RNG、history、清理后 Page 与已完成根保留。共用宝箱路径 `treasure-hunting absent` 双版各1 PASS、四诊断0，日志同目录 `treasure-final-{public,public-beta}-host.log`。

返工保留两处真实差异：no-exit oracle 必须包含正常 ToSave 对 host Point.Map 的 live roster 刷新；Holes 保存副本 Restore 必须显式传入 ActivePlayers，避免默认 OriginalPlayers 重建使有序名单改变。新例每轮真实先连3、核host大厅后再连2，固定host `[1,3,2]`；客户端按各自独立核验的正常名单比较，不排序或删字段。固定异序首轮在第三轮启动遇总case60秒watchdog；仅该精确用例改180秒，每轮50秒及其他用例预算保持。

独立 Standards 无硬违规，阶段分派分散为非阻塞维护建议；Spec 无阻塞，host roster、保存副本名单及watchdog增量均分别复审0新增发现。正常预hook及commit hook成功，283个tracked C#字节未变，提交diff与已审patch一致。本片未验证部分领取恢复或正式客户端输入，#85仍部分完成。

集成树同源码 `1956c17` 串行复验双版 `event-` 各4 PASS，Build／Host／Engine／Metadata全0；实际日志 `.local/executor-holes-completed/integrated-event-{public,public-beta}-host.log`。完成点目录 public `b6ef480e-09d6-40b9-9bd3-49e09c0e05ec`、beta `dc22b61a-bfbc-4f44-966f-5f206f7dfda2`，各三轮九份.err为空、第三轮三端均 MapRoom／opening=0／rewards=0。

## 帝皇蟹双臂开场固定 Artifact3（2026-11-14）

源码 `6a6d431` 经 `dd46174` 合入。包装 Crusher/Rocket 原 `AfterAddedToRoom()` Task，等待原开场全部完成后，各以本次精确 mutable Artifact 实例授予3层；缩放例外只消费一次，独立和嵌套来源继续原生人数缩放。原方向、CrabRage、玩家 Surrounded、背景显现及当前双版不同首招完整保留。beta0.113.0 的原 Rocket 首招为 CALCULATING，public0.107.1 为 TARGETING_RETICLE，未用旧beta源码代签。

首个真实 public red 已先确认左右/怒气/包围及真实背景，再在受罚 Crusher Artifact实际0≠3失败；另一个真实生命周期 red 在原开场方向命令暂停后移出敌人名单、保留 CombatState 引用，旧适配仍追加 Artifact。修复重核原 run/room/combat/map/node/pending、仍在敌方名单、存活且 combat未结束，原异常照常传播，没有永久 applied 标记。

新5例覆盖 safe/hunted×1–4个combat角色、真实双臂/槽位及原开场、独立/重入 Artifact、非Jigsaw/异幕/真实事件父子栈（仍只有普通增强），以及原Task暂停后的移除、死亡、换map/room/run、关闭会话和原依赖异常。使用真实 Kaiser 背景节点；房间仅提供背景所有权，未挂完整战斗UI，不声称正式布局或远端联机。

实施树 `crab-opening-20261114/.local/executor-crab/` 保存实际日志。最终 `Test.ps1 -Branch All -Filter 'Jigsaw hunting crab' -SkipResourceExport` 双版各5 PASS；受影响 ActTwo8/Hive12/ActThree5/旧Waterfall6 各目标均通过，Build/Host/Engine/Metadata全0。真实背景有 Asset not cached WARN，未称零警告。期间缺using与init-only赋值两次编译准备失败均保留，不冒称行为red。

独立 Standards、Spec 对 fixed `776e2ef` 的三文件 staged diff 均0发现；正常 commit hooks 成功，提交差异与已审差异完全相同。集成 `dd46174` 后串行蟹各5及包含新自然致死的 Waterfall各7 PASS，四诊断全0；集成日志 `.local/executor-20261114/crab-integrated-{crab,Waterfall}-{public,public-beta}-host.log`。完整正式布局/输入、蟹战前与奖励fresh恢复、真实远端及整批Check/同提交包仍属后续，#85未被局部测试关闭。

## 帝皇蟹仅效果强化与原场景保持（2026-11-14）

用户明确本方案只增加效果、没有新增怪物；本片据此收口为 tests-only，所有生产布局、贴图和场景资源保持不变。旧移动 Crusher 的未提交方案曾在自身断言下双版各9通过，但 Spec 查明双臂美术在独立 Kaiser 背景，移动目标节点会使美术与目标错位。该方案已完整归档到蟹实施树 `.local/executor-crab-layout/withdrawn-layout/` 并从 src/index 撤出，未提交；整体移动玩家的候选也取消。旧方案的通过不作为当前方案证据。

最终源码 `4e4bf4c` 经 `f1eb1dd` 合入，仅新增 `JigsawHuntingCrabLayoutTests.cs` 908行与 Hello 注册1行。四例分别覆盖1/4角色、1280/1680×720，每例先 safe 再 hunted；双臂原 marker/scale、玩家、目标代理、body、意图静态容器、实际可见完整 HP、背景及实际 Spine 根 transform 均同相位精确对照，唯一开场差异为双臂各 Artifact3。每相位实际可见 HP 键集合也须相等，整条消失不能跳过矩形检查而通过。

鼠标从 safe 原始目标中心真实派发，必须返回原双臂实例；左右方向遍历含玩家的完整焦点环，输入前后敌人/怪物/招式身份、完整玩家与五堆、各 RNG 保持。safe 原场景共有的交叠按同相位保存两个完整矩形及 intersection，hunted 要求精确一致且键集合相同。实际4角色原生 Crusher/玩家4 hitbox交叠约4.29逻辑像素、聚焦玩家4时 HP/Crusher目标交叠约16.29；这两项是原生共有事实，本 Mod 不据此重排原场景，也未宣称消除它们。

实施树 `crab-layout-20261114/.local/executor-crab-layout/unchanged-final-{public,public-beta}-host.log` 是最终909行源码的实际相关验证：双版各9 PASS（原开场5＋新4），Build/Host/Engine/Metadata均0；此前 `unchanged1-beta` 最小1例亦四诊断0。独立 Standards/Spec 已实读最终差异，可见 HP 键集合增量分别复审关闭；正常 hooks 提交后 reviewed-final.patch 与 committed-final.patch 全文一致。集成同源码双版 `crab` 各9 PASS、四诊断0，日志 `.local/integration-slices/crab-native-integrated-{public,public-beta}-host.log`；蟹完整战前/奖励 fresh、正式游戏端仍属 #85，局部通过不关闭整票。

## Knowledge Tender 真实 owner 选择屏（2026-11-14）

源码`1570ab2`经`35bb69d`集成，目标public0.107.1/beta0.113.0、RitsuLib0.6.11，匹配PCK。仅新测试与Hello注册两文件417+，生产零改。单owner受罚Hive顶层KnowledgeBoss原五步循环第二轮关闭TestMode/AutoSlayer、清空selector；真实PerformMove打开原Sloth/Tender屏、正式卡图与格式化文本，原350ms等待后viewport鼠标点Tender hitbox。原生PowerReceivedEntry只一次、Tender1/Sloth0，展示牌不入牌组/堆；原CardsSelected→Overlay.Remove→QueueFree释放一次，重复release不重加，next原招Slap。真实怪物Hitbox.GuiInput在屏前/屏中/退出后作输入对照。

宿主准备中的编译、空RunSet额外preload、空GlobalUi wrapper吞鼠标、缺action/hover parent、Ritsu挂树时roomContainer未绑定分别分类保留，不冒称产品red。最终真实NGame viewport、HoverTipsContainer、24个按需新增并finally撤销的原生action、先roomContainer后挂树保留真实hover/owner lifecycle；仅隔离NGame启动/Steam/audio和空地图生命周期，没有卸Ritsu事件或改共享HeadlessUiHost。entry5虽1PASS仍Engine13/RID3、entry6/7虽1PASS仍Engine1，均未签绿。

最终实施树`knowledge-tender-ui-20261114/.local/executor-tender/owner-final-{public,public-beta}-host.log`双版各1 PASS、四诊断0无RID/ObjectDB；相关`acttwo-final-*`双版各8 PASS四诊断0。独立Standards/Spec无阻塞；SkipNativeLifecycle命名增量仅同为false方法拆名，两轴源码复核无行为变化，复用上述行为证据。正常commit hook成功，提交patch与已审patch完全一致。

集成`35bb69d`串行执行`Test.ps1 -Branch All -Filter 'Jigsaw Knowledge native Tender' -SkipResourceExport`双版各1 PASS及`-Filter 'Jigsaw hunting act two'`双版各8 PASS，Build/Host/Engine/Metadata均0，无RID/ObjectDB。日志集成树`.local/executor-20261114/tender-integrated-{public,public-beta}-host.log`和`tender-acttwo-integrated-*`。本片仅1920×1080、当前语言、单owner鼠标；多人选择时序/死亡缺席断线、键盘控制器、完整布局/恢复和正式端仍待#85，不能据此关闭整票。

## Knowledge 选择计数与非终局奖励 fresh（2026-11-14）

源码`5b88af8`经`2e61481`集成，仅新增KnowledgeRecovery测试及Hello注册两文件652+，生产与共享checkpoint未改。目标public0.107.1/beta0.113.0、RitsuLib0.6.11，匹配资源。源局实际Hive、非最后幕，只在source设置原`KnowledgeDemonBoss`遭遇；普通`session.Enter`前独立采样before-roll，原开场独立保存完整玩家/五堆有序卡牌/Energy/Block/powers、run/owner/monster RNG、Act/map/history、敌人HP/slot/intent及私有诅咒计数和IsBurnt。

原首轮展示MindRot/Calamity，实际选择Calamity经原OnChosen和外层Task完成，Calamity1、counter1、下一招SLAP。能力expected在动作前从完整快照独立追加，不从事后powers取值；展示牌不入永久或五个战斗牌堆。随后真实损血/金币/移牌/RNG扰动并保存，新LoadRunLobby ready沿原setup/map/load创建新run/session/room/demon，恢复完整原开场、counter0、首轮诅咒/满HP/无旧能力，不二扣；再次真实选择确为第一对而非Sloth/Tender。

真实击杀唯一Boss→原CheckWinCondition、战后清理及BurningBlood回血6→自然prefinished保存；原非终局Hive Boss生成非空奖励，保存前boundary与奖励生成后内容/玩家/RNG分阶段取expected。源skip不付库存，第二新大厅不重开战斗，真实同步领取、重复拒绝、原Proceed完成根一次，不手调Complete或末尾补Save。两版实际均为金币100及卡牌两项，没有roll出药水；代码保留原药水可选分支和独立profile发现预测，不把未出现的药水领取写成已执行。profile完整JSON克隆隔离，finally恢复原引用及完整原内容。

独立Standards/Spec实读完整652+及双版原生消费者均0发现；正常hook成功，提交patch与已审patch完全一致。实施树`knowledge-fresh-20261114/.local/executor-knowledge-fresh/entry1-beta-host.log`和`final-public-host.log`各1 PASS，`related-final-{public,public-beta}-host.log`各8 PASS，均Build/Host/Engine/Metadata0、无RID/ObjectDB。集成`2e61481`专项双版各1及相关ActTwo各8 PASS、四诊断0无RID，日志集成树`.local/executor-20261114/knowledge-fresh-integrated-{public,public-beta}-host.log`与`knowledge-acttwo-integrated-*`。单owner同进程两次新大厅，不代签正式选择UI、未完成的多owner选择、真实远端/新进程或Hive首普通完整恢复。

## Hive 首普通完整 fresh（2026-11-14）

测试源码`c2cb995`经`2cf965c`集成，生产未改。真实原生acts=[Overgrowth,Hive,Glory]、CurrentActIndex=1与JigsawCheckpoint.Act=1，准备期仅一次原生RoomSet选SpinyToadNormal，之后走实际session.Enter。初始Weak/Frail/Vulnerable各1且仅一次由原生PowerReceivedEntry独立核定；完整当前powers取同阶段原生首回合快照，保留public/beta自然tick差别，不用强设层数统一版本。

真Spikes使IsSpiny=true/Thorns5并进入Explosion待行动，再额外Weak4、伤血、移牌及扰动run/owner/monster RNG后实际SaveRun。新LoadRunLobby新run/session/room/怪物完整回原开场，核五堆顺序/Energy/Block/powers、敌HP/动作/RNG、Act/map/history/根房同点，普通遭遇计数不再消费；重复Enter不重施三负面、不二扣。自然致死核原BurningBlood回血6、胜利点保存先于奖励，第二新大厅不重开战斗并重现非空金币/卡，真实领取、重复拒绝/完整状态不变及原Proceed一次；本seed未roll药水。

实施树`.local/executor-hive-fresh/first-{beta,public}-host.log`双版各1 PASS；集成树`.local/integration-slices/hive-fresh-integrated-{beta,public}-host.log`双版各1 PASS，Build/Host/Engine/Metadata均0。Standards/Spec独立读取644+/0两文件及适用API/规格，源码无阻塞；正常hooks成功，提交patch与已审暂存完全一致。Profile独立JSON克隆及完整原引用还原/全局清理保留。单owner同进程新大厅，不代签真实多端、所有Hive场地、Knowledge多人屏或正式奖励按钮；#85仍部分完成。

## Knowledge 双 owner 错峰选择在制交接（2026-11-14）

本片未提交、未合入，停止点为2026-11-14 15:49。实施树`knowledge-owner-20261114`，分支`test/issue85-knowledge-owner-20261114`，HEAD=`f1eb1ddd2c5f9ce4edc62261dafec9f23513d6e4`。仅暂存新增`src/Tests/JigsawKnowledgeMultiOwnerTests.cs`671行及Hello注册1行，生产零改，无源码未暂存差异；资源导出.import未暂存。owner原为`glory_layout`，跨对话按实际Git接手，不依赖旧agent继续运行。

两真实ENet进程分别拥有NetId1/2，经真实RunLobby和PlayerChoiceSynchronizer，各显示原MindRot/Calamity屏；host真实鼠标选Calamity后自身屏释放/焦点回战斗，child仍待选、原PerformMove仍未完成，child再真实选MindRot。两端各能力1、实际PowerReceived两条正确归属、屏各移除/释放一次、完整五堆/卡牌身份/其他状态与run/owner/monster RNG独立前后不变；手工原RollMove到Slap。夹具手工创建CombatState/SetUpTest及合法Boss map后Restore，不是原地图进入/coordinator自然调度；不得把手工下一招写成自然调度已证。

此前entry5行为PASS仍有执行器取消和child菜单NRE；entry7因child `CleanUp(false)`实际立即断网，使host peer不移除/质量tracker向断开peer发包100条，不能签绿。成功路径改`CleanUp(true)`并先释放RunLobby后，entry8行为1PASS但Engine3：host退出期仍订阅JigsawNativeSession.OnPlayerDisconnected，收到child正常离线后沿Disconnect→CompleteRoom→SynchronizeMasks→RelicCmd.Obtain触发缺遗物UI的NRE。**不是预存CompleteRoom队列**。最终在双方全部效果/五堆/RNG/Slap断言、原执行器settle及done屏障结束后，host先`session.Close()`卸订阅，再发finish；child正常优雅退出，host仍等peer移除与child退出码0，不丢队列、不屏蔽错误，也不声称验证选择等待期间断线。

当前有效证据仅实施树`.local/executor-knowledge-owner/entry9-beta-host.log`：public-beta0.113.0，Build0/Host0/1 PASS/Engine0/Metadata0；console同前缀。子进程`.local/knowledge-multi-owner/75edea27a8a44bdfa0eda5b0cab5dda1/2.log`1 PASS，`2.err`实际0字节，测试已断言子进程退出码0。public行为尚未运行，尚无本片正式双轴审查、hooks提交或集成；不要把entry9用于其他目标或新源码签字。

接手下一命令（该实施树，资源未变且PCK已有效导出）：`./tools/Test.ps1 -Branch public -Filter 'Jigsaw Knowledge two native owners stagger first-round screens' -SkipResourceExport`；再双版`Jigsaw hunting act two`8例和`Jigsaw Knowledge native Tender`1例，按实际结果留原始日志。随后独立Standards/Spec读取同一`git diff --cached f1eb1dd -- src/Tests/JigsawKnowledgeMultiOwnerTests.cs src/Tests/HelloWorldTests.cs`，正常hooks提交、串行合并及集成验证。死者/缺席/等待中死亡断线、第二三轮多owner、真实游戏端和完整#85仍未验。唯一调度见[看板](issues-82-88-board.md)。
### 2026-11-14 implement 接续验证与集成

实施提交1d22277经e93d304合入。public错峰1、ActTwo双版各8、Tender双版各1均严格通过，Build/Host/Engine/Metadata全0；beta错峰复用entry9。实施树.local/tests/archives/knowledge-stagger-public-20261114-172517352-6a69d2120ac54b5c8fe8299c84bcf8d9、knowledge-acttwo-20261114-172547358-c6b44d492ace43d9820b0f67cd565444、knowledge-tender-20261114-172621129-223e520264454194bc882170762bff46含原始日志与summary。public child c32d1dc783494c14849d29e50663d065 的2.err为空且父测试断言退出0。独立Standards/Spec均0发现，正常hooks成功，reviewed.patch与提交diff逐字一致。当前集成双版错峰专项进行中，未代签自然coordinator调度、等待中断线、第二三轮多人或完整#85。

集成e93d304的实际严格专项双版各1 PASS，四诊断全0；归档 .local/tests/archives/knowledge-owner-integrated-20261114-173527158-d598714f64cd459da98a9b97dfc305cc/summary.json 及同目录两目标原始日志。没有扩跑模型矩阵。

## Knowledge 双owner原生首轮调度（2026-11-14）

2c45c7c经845d00e合入，单测试文件151+/32-，旧manual错峰分支保留。新增两真实ENet进程从原生Launch、ManagedNetActions能力协商、session.Enter及首轮TurnStarted进入；通过原按钮对应的ActionQueueSynchronizer.RequestEnqueue(EndPlayerTurnAction)结束回合，不手工PerformMove/RollMove。host先选Calamity后child仍持原屏，Enemy侧/readyCount1/moveCalls1保持；child选MindRot后仅一次返回round2，下一招Slap，原生角色首轮5/7及MindRot后5/4独立核验。屏幕释放、能力归属、原任务settle/child退出及全局ActionTypes原缓存引用恢复均保留。

实施归档knowledge-native-turn-20261114/.local/tests/archives/knowledge-native-beta-mindrot-20261114-182459340-bf14288dc9c249b78053424d202214d9、knowledge-native-public-first-20261114-182531196-81196120114a41e29b7c56fbcbae432c各1；旧manual双版各1在knowledge-native-manual-regression-20261114-182616487-0cd90dd998174fb390dd7253ddeb9fed，均strict四诊断0。原入房/能力协商/动作注册及手牌预期准备失败保留，不称产品red。额度中断后两独立轴重新实审，0阻塞；正常hooks成功，已审patch与提交字节一致，原.import保留。

集成845d00e双版各1严格通过，原日志/summary在.local/tests/archives/knowledge-native-integrated-20261114-230007005-a30f34f77598419097a21956b994e1e0。仅首轮自然调度，不签等待死亡/断线、后两轮多owner或完整#85。

## 帝皇蟹完整战前与非空奖励fresh（2026-11-14）

859096a经f2906d4合入，两文件685+。真实Kaiser遭遇只有Crusher/Rocket两臂，保持原背景/slot/方向/版本首招，非第三敌。原生入房开场前独立完整Boundary及开场后Opening采样，源战两臂真实动作/roll、玩家与五堆/卡牌身份/HP/block/powers/monster与run/player RNG实际扰动后保存；第一新LoadRunLobby恢复完整原开场、每臂Artifact3一次，不拼旧阶段或二扣。恢复战通过逐臂足量Unblockable/Unpowered Damage原生致死，保留BeforeDeath/AfterDeath及胜利消费者，不用force Kill。

胜利保存前和非空奖励生成后分别独立采预期，第二新大厅恢复相同胜利/金卡奖励，原生同步领取、重复拒绝且状态不变、Proceed完成均有断言。真实Kaiser背景持有者与清理保留；只提供自有NAudioManager单例，原PlayOneShot在TestMode原生跳过音频，不跳死亡。profile完整隔离及finally恢复保留。

实施树crab-fresh-20261114/.local/tests/archives的crab-fresh-beta3-20261114-224250831-867305aa91464e8bbcb0b42a03f9cabc、crab-fresh-public1-20261114-224340584-806cdd7ef46d46d88d10b2b3ca52de85各1 PASS；crab-regression-20261114-224406263-c1431efe10c7463483c3f35b43c32fb1双版开场各5、crab-layout-regression-20261114-224515170-d1a58d39987540bf8e8c370d83fb7c6a双版布局各4。全部strict四诊断0，准备路径/canonical未生成怪物资源失败单独保留。Standards硬规范0/smell0，Spec0发现；正常hooks，已审/提交patch逐字节一致，.import保留未提交。

集成f2906d4双版fresh各1 strict通过，原日志和summary位于.local/tests/archives/crab-fresh-integrated-20261114-230726065-7f3a488b905f4a368658fffe5620bb02。仅单owner同进程两个新大厅，不签真实多端/新进程/正式奖励按钮与完整#85。

## Knowledge 双owner自然三轮贯穿（2026-11-15）

22dd5cc经673ce47合入，两文件143+/10-；复用原ENet runner保留旧manual/native首轮两例，新增一条完整三轮case。原生结束回合自然走至Curse round1/5/9，每轮host先选/child仍持屏等待，三对MindRot/Calamity、Sloth/Tender、WasteAway/Disintegration21及独立能力/History增量、owner归属和展示牌不入堆均核。第三轮后继续至round13：原moves12/choices6/每端屏释放3、下一Slap且无第四选择屏，不写私有计数或手工PerformMove/RollMove补推进。

只有精确新例预算180秒，保留每阶段20秒及旧例预算；实际原60秒watchdog中断于第三轮前的原生动画链。真实Slap缺相机Strong preset按现Matriarch纯相机隔离接缝处理，保留Damage/动画/回合消费者，finally卸patch。两次宿主/预算准备失败留原归档，不冒称玩法red。

实施树knowledge-three-rounds-20261114/.local/tests/archives的knowledge-three-beta-budget-20261114-233926415-911936f94a404b499113128fa1394c93及knowledge-three-public-20261114-234208249-950d1913b8ef4ec3a66fac59eafa8b3c双版新各1；旧manual/native各双版1，归档knowledge-three-regression-manual-20261114-234443718-f5dd8cfd892b427ab210c0a9214916f5及knowledge-three-regression-native-20261114-234532354-f12c7be8c8fc4111b390aa9ddd2914ca，全部strict四诊断0。独立双轴0发现，正常hooks，已审/提交patch逐字一致，原.import保留。集成673ce47双版新贯穿各1 strict通过，原日志/summary在.local/tests/archives/knowledge-three-integrated-20261114-235238176-09aab019d90249f5ba95365b387313ec。死亡/缺席、等待中断线及完整#85仍未签。

## Knowledge 选择开始前dead/absent资格（2026-11-15）

5d33563经a4bb2b9合入，仅ActTwoTests新增80行。safe/hunted×already-dead/absent四组合：B仍在run名单；dead时HP0且在PlayerCreatures集合，absent时从combat移除而不脱离run，先核两种前提。通过原生RollMove/PerformMove和既有selector，A固定第二项独立expected为safe MindRot1/hunted Calamity1，总请求恰一次归A；B无请求/效果，双owner牌组/五堆身份顺序不变，MonsterPerformedMoveEntry恰一且下一Slap。缺席仅指不在战斗目标集合，不冒签网络断线。

沿用ActOne原scaling夹具：合法单人root绑定后原生AddPlayerDebug(B)再加combat，保留ChooseCurse原guard，不patch资格。初始多owner绑定guard/重复设置RunState的动作前准备失败归档，未称产品red。实施树knowledge-targets-20261115/.local/tests/archives/knowledge-targets-acttwo-20261115-002354363-dca4842c670943588f0109ead0d51146双版各9 strict通过；最小beta新1亦通过。Standards/Spec独立0发现，正常hooks，已审与提交patch逐字一致，导入资源状态保留未暂存。

集成a4bb2b9双版ActTwo各9，Build/Host/Engine/Metadata均0；原日志/summary在.local/tests/archives/knowledge-targets-integrated-20261115-003344125-5e1f8bfa1a694adc95ad8fb5ab60bbc0。等待中死亡/断线及整票仍未签。
