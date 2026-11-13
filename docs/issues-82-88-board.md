# #82–#88 当前调度看板

更新：2026-11-14。唯一维护者：集成主控。集成树：`AllMatters/.claude/worktrees/hunting-20261112`，分支`integration/issues-82-88-20261112`。当前源码HEAD=`7b08841`；流程入口已提交`776e2ef`，Holes源码`1956c17`保留。

目标：public v0.107.1 / beta v0.113.0 / RitsuLib 0.6.11。本轮已实际查询GitHub，#82–#88均OPEN；当前本地切片不自动关闭issue。后续主控更新此文首和卡片，不再另建当前状态表。

**当前活跃WIP 2/2；暂停1；Ready 0。** Waterfall `c973505`、蟹 `6a6d431` 均已集成到 `dd46174`，交叉回归双版蟹5＋Waterfall7 PASS。族母随从`3008bf6`已合入`4df655b`，集成双版Underdocks各7 PASS；Mystic`6aa65b4`已合入`7b08841`、集成双版各6 PASS；宝箱继续真实整房输入链，Queen重燃接入释放槽。两片写文件独立，不依赖暂停中的 Boss；主控唯一执行。本轮沿用既有批次宿主证据，不重启为新批次。

## 拉取入口

当前推进宝箱正式整房输入和Queen重燃；Mystic、族母、蟹与Waterfall自然致死已集成，不重复派发。其他候选按依赖、验收价值及文件所有权准备。

跨票验收盘点可由主控一次完成，用于发现#82/#83可收口项；不要把“盘点全部历史”作为后续每片的共同前置。详细操作见[批次流程](agents/batch-flow.md)。

## 活跃与就绪

|卡片/issue|行为|状态|owner / worktree|依赖与热点|下一动作/证据|开始时间|
|---|---|---|---|---|---|---|
|C83-full-chest / #83|匹配资源正式开箱输入→空Tween→Proceed自然完成→实际保存和fresh恢复；不重开箱/重复Face或Spoils/扣步|Doing|docs_standards / `treasure-full-input-20261114`，基`4df655b`|独占Treasure测试EmptyTween/PublicCollectionHost；共享host/生产先报主控，唯一执行者主控|替换裁剪public及手动完成代签；先public精确empty Tween，再beta，相关宝箱回归；旧非注入NRE须保留可见并修owner上下文|2026-11-14，派发时刻未采样|
|C87-queen-rekindle / #87|聚合体真实死亡一次强制重燃→独立50HP/10伤Minion火炬两只→当次不攻下次直攻、原激怒仅追加存活本源火炬各2、不补召|Doing|prepare_crabs / `queen-rekindle-20261114`，基`7b08841`|独占新Queen模块/TorchHead/测试/资源和对应localization键；Hello/NOTICE主控；旧Boss诊断模块只读|先真实死亡意图最窄red，再green/生命周期；主仓`.local/main-control-20261113-resume/queen-current-plan.md`已核权；两版当前API按需补；完整布局/恢复仍归#87|2026-11-14 01:40 +08:00|

## 候选池：尚不能派发实施

|候选/issue|具体缺口与建议切片|准备前需核清|证据入口|
|---|---|---|---|
|C85-owner-ui / #85|一个owner选择屏生命周期/真实输入边界|现有UI接缝、生命周期和死亡/断线/迟到归属；资源/host所有权|[巢穴记录](issue85-hunting-implementation.md)；[覆盖地图](issues-82-88-verification-map.md)|
|C82-rest-native / #82|原生禁Heal遗物＋追猎禁项，正式输入自然离开并保存恢复；不手删Heal/Mend|窄读尚未找到两版原生禁Heal来源：CoffeeDripper旧缓存空，现有模型中Midas仅删Smith；候选前提待核，不能人为RemoveAll换green。既有正式Proceed链和后续恢复可复用|[82审查](issue82-review.md)、`JigsawHuntingServices.RestChoicesRemain/JigsawHuntingRestExit`|
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
|#86 Mystic原生闭环|`6aa65b4`，合入`7b08841`|普通/事件受罚Glory追加基础HP56、无初始能力；CAST六等概率独立、HEAL12/空过、BUFF2完整循环；原生资源factory/Spine/双语|双轴0发现；hook仅四处单行if格式换行已增量复核；集成默认导出资源双版各6 PASS、四诊断0；[荣耀增量](issue86-hunting-implementation.md#mystic-原生模型资源与三动作闭环2026-11-14)，12场景布局/完整fresh恢复未代签|
|#84 族母左侧随从|`3008bf6`，合入`4df655b`|原生生成左侧本Mod Lagavulin；睡眠/原生HP保留、固定Minion1、族母死后自然清理；safe/event/非Jigsaw/独立来源排除、无acts不越界|双轴0发现；正常hooks、提交diff与已审patch一致；集成双版Underdocks各7 PASS，四诊断0；[暗港增量](issue84-underdocks-implementation.md#matriarch-左侧睡眠随从2026-11-14)，正式布局/完整恢复仍待|
|#85 帝皇蟹双臂开场|`6a6d431`，合入`dd46174`|各Artifact3不额外多人增层；真实背景/方向/首招、1–4角色、独立及重入来源、事件/过期await|双轴0发现；2026-11-14集成双版蟹5＋Waterfall7 PASS，四诊断0；[蟹片记录](issue85-hunting-implementation.md#帝皇蟹双臂开场固定-artifact32026-11-14)；正式输入/布局/恢复仍待|
|#84 Waterfall自然致死|`c973505`|首次真实Damage自动Trigger后保留正面/隐藏与Artifact1，safe清理及最终死亡原生；1/4角色、实际挡Weak|2026-11-14集成；双版各7 PASS，Build/Host/Engine/Metadata0；集成`.local/executor-20261114/waterfall-integrated-*-host.log`；[暗港增量](issue84-underdocks-implementation.md#waterfall-首次自然致死尾部修复2026-11-14)|
|#85 Holes战前三端fresh|`e088feb`|三端真实保存、新三端大厅恢复、完整状态及roster语义|[Holes记录](issue85-hunting-implementation.md)对应章节|
|#85 Holes奖励三端fresh|`31e849a`|自动胜利点、fresh奖励、同步领取、Proceed|部分领取存档边界未证，不自动生成新片；先核验收是否要求|
|#85 Holes根完成第三fresh|`1956c17`|全员真实Open(false)→显式host保存→第三轮三端MapRoom；固定异序名单；集成双版event-各4 PASS|裸地图宿主/显式保存，不代签正式按钮输入或自动UI保存；[Holes完成片](issue85-hunting-implementation.md)|

旧切片见[实施索引](issues-82-88-implementation.md)，不把全部历史提交复制成卡片。确认本片对需求无新增缺口后移到历史链接；保留整票尚未满足项。

## Issue级完成边界

|Issue|本地已有成果摘要|仍需收口（非详尽验收替代）|
|---|---|---|
|#82|限步/面具/服务限制、迁移和控制台已集成|当前目标相关回归、适用输入/网络与交付证据|
|#83|空箱/Face/Spoils、异步等待、恢复与双端证据已集成|当前目标正式整房输入/动画、实际玩家端保存退出归属|
|#84|暗港/密林多片、共享检查点、Waterfall自然致死规则|其余随从/规则、事件、适用恢复/网络与正式布局|
|#85|Knowledge/Hive、蟹开场；Holes三种三端恢复已集成|owner UI/连续事件、正式布局/输入/自动UI保存、其他战斗恢复与适用真实端|
|#86|Mecha/Soul/Flail/Mystic原生动作及资源双版专项已集成|全场景/精英布局、完整战前/奖励fresh恢复和真实端|
|#87|Aeonglass/TestSubject、MultiClaw、Wither已集成|Boss间歇问题、Queen独立TorchHead/重燃/激怒、生命周期/恢复/布局|
|#88|兼容目标资源、Compass/Flight/控制台/每日字段已集成|组合验收归属、最终Check/双版包/许可、人工矩阵；不替原票承担缺失玩法|

人工验收项仍属于各issue，由主控整理可执行批次再交用户；不能为避免停顿就宣布人工通过，也不让用户每完成一个自动化片来回传话。

## 状态更新约定

主控派发/发审/暂停/集成时修改对应行并链接实际证据；卡片owner回报由主控落板，其他Agent不并发写板。使用已有Git提交和日志，不添加hash、冻结contract或独立审批系统。若改动尚未提交，明确工作副本状态。

Ready→Doing时填实际开始时间；结束填集成时间及证据。初始0/2和Ready数量随状态变动一起更新。候选按实际新发现合并/拆分，不用卡片数量计算完成百分比。下一主控只需本板、当前目标卡及相关源文档，不读所有研究报告。
