# #82–#88 当前调度看板

更新：2026-11-14。唯一维护者：集成主控。集成树：`AllMatters/.claude/worktrees/hunting-20261112`，分支`integration/issues-82-88-20261112`。初始化实查HEAD=`29f0439`、源码=`1956c17`；本次新增流程文档未提交，不视为开发交付。

目标：public v0.107.1 / beta v0.113.0 / RitsuLib 0.6.11。本板按本地证据初始化，未查询当前远端；实施索引记载#82–#88 OPEN，不能据此自动关闭issue。后续主控更新此文首和卡片，不再另建当前状态表。

**当前活跃WIP 0/2；暂停1；Ready 0。下一动作是主控准备一个可派发片，不是再次运行Holes已完成组合。** 最近几轮Holes未完成项已收口，选择下一项须回到全批缺口。

## 拉取入口

默认建议先准备 #85 Crusher/Rocket 开场规则片：已有明确剩余规则、与已完Holes是不同接缝。第二条独立准备可选 #84 Waterfall首次致死Trigger补证；先核是否触及Boss共用host，未核前不并行写入。这是初始化候选优先级，主控核issue验收/影响链后可调整并写一句理由。

跨票验收盘点可由主控一次完成，用于发现#82/#83可收口项；不要把“盘点全部历史”作为后续每片的共同前置。详细操作见[批次流程](agents/batch-flow.md)。

## 活跃与就绪

|卡片/issue|行为|状态|owner / worktree|依赖与热点|下一动作/证据|开始时间|
|---|---|---|---|---|---|---|
|—|目前没有已准备且派发的切片|—|—|—|主控从下列候选完成文件级brief后移入Ready|—|

## 候选池：尚不能派发实施

|候选/issue|具体缺口与建议切片|准备前需核清|证据入口|
|---|---|---|---|
|C85-crab / #85|Crusher/Rocket受罚开场固定Artifact3，保留包围/方向；若两个怪物路径独立则分片|目标原生类型/开场方法、已有patch及测试文件、普通对照；共享注册owner|[巢穴记录](issue85-hunting-implementation.md)“仍须完成”；后续增量覆盖旧Hive/Holes待办|
|C84-waterfall / #84|首次致死→原生Trigger规则及未受罚对照|现有Waterfall规则已交付范围、真实致死入口、是否依赖问题host|[实施索引](issues-82-88-implementation.md)当前状态 Waterfall 段（暗港 Boss）|
|C84-minion / #84|Matriarch左侧Lagavulin或其他尚缺暗港规则，选一条完整行为|准确验收及敌方生成入口，勿一次承接整幕|[暗港记录](issue84-underdocks-implementation.md)“尚未完成”|
|C85-owner-ui / #85|一个owner选择屏生命周期/真实输入边界|现有UI接缝、生命周期和死亡/断线/迟到归属；资源/host所有权|[巢穴记录](issue85-hunting-implementation.md)；[覆盖地图](issues-82-88-verification-map.md)|
|C86-mystic / #86|Mystic正式资源/NOTICE与最小模型开场片|既有concrete-plan、许可来源、两目标API；资源准备不等于正式加载|[荣耀记录](issue86-hunting-implementation.md)“仍需继续”；集成树`.local/research/issue86-mystic-concrete-plan.md`|
|C87-queen / #87|独立TorchHead资源/生命周期最小行为|主仓`.local/main-control-20261113-resume/queen-current-plan.md`；与Boss问题host实际依赖|[Boss记录](issue87-boss-implementation.md)“仍需完成”；索引覆盖旧MultiClaw/Wither待办|
|C82-83-close / #82/#83|核剩余自动化/人工验收，选能收口的一项|后文已补证覆盖旧待办；人工步骤与当前自动化分别列明|[82审查](issue82-review.md)、[83审查](issue83-review.md)|
|C88-evidence / #88|现有矩阵逐项归属，只补实际缺口|各玩法的恢复/UI仍由原issue承担，#88做组合交付|[覆盖地图](issues-82-88-verification-map.md)、[兼容记录](issue88-compatibility-implementation.md)|

## 暂停与等待

|卡片/issue|状态/owner|当前事实|恢复条件|证据|
|---|---|---|---|---|
|P87-boss / #87|诊断暂停；无人继续运行；保留B树改动|原六项2绿1同症状红；随后3次probe绿、未捕获原错，probe已撤回，未修复|一个能区分现有假说的新观测/实验设计或新失败证据；主控限定预算后拉取，不盲目再跑|主仓`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`及controller末节；B=`boss-warmed-callback-20261113`|
|W88-delivery / #88|等待范围收口|最新完整Check、同源码双版包/许可及正式端交付未完成|#84–#87适用剩余行为收口、当前矩阵明确；按现行交付执行|[实施索引](issues-82-88-implementation.md)、[开发入口](development.md)|

暂停项不占活跃槽位，但持续显示年龄/恢复条件。Boss历史开始时间未统一计量，写“未计量”，不能凭本板创建日期重置。原组故障不自动阻塞所有Queen准备或其他issue。

## 最近集成：切片完成不等于issue关闭

|issue/切片|源码提交|已证范围|剩余边界/证据|
|---|---|---|---|
|#85 Holes战前三端fresh|`e088feb`|三端真实保存、新三端大厅恢复、完整状态及roster语义|[Holes记录](issue85-hunting-implementation.md)对应章节|
|#85 Holes奖励三端fresh|`31e849a`|自动胜利点、fresh奖励、同步领取、Proceed|部分领取存档边界未证，不自动生成新片；先核验收是否要求|
|#85 Holes根完成第三fresh|`1956c17`|全员真实Open(false)→显式host保存→第三轮三端MapRoom；固定异序名单；集成双版event-各4 PASS|裸地图宿主/显式保存，不代签正式按钮输入或自动UI保存；[Holes完成片](issue85-hunting-implementation.md)|

旧切片见[实施索引](issues-82-88-implementation.md)，不把全部历史提交复制成卡片。确认本片对需求无新增缺口后移到历史链接；保留整票尚未满足项。

## Issue级完成边界

|Issue|本地已有成果摘要|仍需收口（非详尽验收替代）|
|---|---|---|
|#82|限步/面具/服务限制、迁移和控制台已集成|当前目标相关回归、适用输入/网络与交付证据|
|#83|空箱/Face/Spoils、异步等待、恢复与双端证据已集成|当前目标正式整房输入/动画、实际玩家端保存退出归属|
|#84|暗港/密林多片、共享检查点、Waterfall规则|其余随从/规则、自然Trigger/事件、适用恢复/网络与正式布局|
|#85|Knowledge/Hive等；Holes三种三端恢复已集成|Crusher/Rocket、owner UI/连续事件、正式输入/自动UI保存与适用真实端|
|#86|Mecha/Soul/Flail及双版专项已集成|Mystic、全场景/精英布局、适用恢复和真实端|
|#87|Aeonglass/TestSubject、MultiClaw、Wither已集成|Boss间歇问题、Queen独立TorchHead/重燃/激怒、生命周期/恢复/布局|
|#88|兼容目标资源、Compass/Flight/控制台/每日字段已集成|组合验收归属、最终Check/双版包/许可、人工矩阵；不替原票承担缺失玩法|

人工验收项仍属于各issue，由主控整理可执行批次再交用户；不能为避免停顿就宣布人工通过，也不让用户每完成一个自动化片来回传话。

## 状态更新约定

主控派发/发审/暂停/集成时修改对应行并链接实际证据；卡片owner回报由主控落板，其他Agent不并发写板。使用已有Git提交和日志，不添加hash、冻结contract或独立审批系统。若改动尚未提交，明确工作副本状态。

Ready→Doing时填实际开始时间；结束填集成时间及证据。初始0/2和Ready数量随状态变动一起更新。候选按实际新发现合并/拆分，不用卡片数量计算完成百分比。下一主控只需本板、当前目标卡及相关源文档，不读所有研究报告。
