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

- Hive 普通/精英开场规则、事件只普通增强；Crusher/Rocket 固定 Artifact3，保包围/方向及原效果、实际完整场地布局。普通开场仅有未运行测试草稿，尚无行为 red 或生产实现。
- 真实1–4玩家逐 owner 选择、原生网络同步与独立完成；不能在 singleplayer 塞入多角色绕过会话边界。
- 正式 Godot 选择屏的真实打开、选择、关闭/free、任务仅续一次、输入路由及回到战斗焦点；public需匹配其正式资源，beta PCK不能代签。Tender纹理、描述格式化和长文本布局仍待实际验证。
- 入口前死亡、等待期间死亡/断线/缺席、重复/迟到选择、退出/恢复后的旧任务清理；原生远端 WaitForRemoteChoice 无取消token，Dispose仅注销handler，不能以局部关闭token声称安全。
- 消费后续 integration 已评审的完整战前/胜利恢复接缝，验证同一完整点含 RNG、能力、个人状态和半份选择丢弃；本片未修改或合并共享 checkpoint。
- 最终双版组合检查、正式运行/人工设备验收及完整 #85 审查。Agent不启动Steam/游戏、不改实际安装或存档。

后续每个独立行为继续先真实 red→最小 green，按主控分配的原生测试窗口串行验证。具体开发工作材料见忽略目录 `first-slice.md`、`curse-continuation-plan.md`、`hive-opening-next-slice.md` 和 `current-review-scope.md`。