# #82–#88 当前调度看板

更新：2026-11-14。唯一维护者：集成主控。集成树：`AllMatters/.claude/worktrees/hunting-20261112`，分支`integration/issues-82-88-20261112`。当前源码HEAD=`2c44d4b`；流程入口已提交`776e2ef`，Holes源码`1956c17`保留。

目标：public v0.107.1 / beta v0.113.0 / RitsuLib 0.6.11。本轮已实际查询GitHub，#82–#88均OPEN；当前本地切片不自动关闭issue。后续主控更新此文首和卡片，不再另建当前状态表。

**当前活跃WIP 2/2；暂停1；Ready 0。** Waterfall `c973505`、蟹 `6a6d431` 均已集成到 `dd46174`，交叉回归双版蟹5＋Waterfall7 PASS。族母随从`3008bf6`已合入`4df655b`，集成双版Underdocks各7 PASS；Mystic`6aa65b4`已合入`7b08841`、集成双版各6 PASS；宝箱正式输入片已集成，Knowledge Tender真实owner选择屏已集成；Queen重燃已集成，Queen完整fresh已集成，拉正式四组合布局。两片写文件独立，不依赖暂停中的 Boss；主控唯一执行。本轮沿用既有批次宿主证据，不重启为新批次。

## 拉取入口

当前推进荣耀全部普通/精英正式四组合布局和知识恶魔选择计数/非终局奖励fresh恢复；Mystic模型/三动作、族母、蟹与Waterfall自然致死已集成，不重复派发这些既有行为。其他候选按依赖、验收价值及文件所有权准备。

跨票验收盘点可由主控一次完成，用于发现#82/#83可收口项；不要把“盘点全部历史”作为后续每片的共同前置。详细操作见[批次流程](agents/batch-flow.md)。

## 活跃与就绪

|卡片/issue|行为|状态|owner / worktree|依赖与热点|下一动作/证据|开始时间|
|---|---|---|---|---|---|---|
|C86-glory-layout / #86|全部12普通含新增Mystic及3精英原生scene→1/4角色×紧凑/宽屏，真实HP/意图含数字可读/独立target与原生鼠标/左右导航|Doing|prepare_crabs / `glory-layout-20261114`，基`f55305f`|独占新GloryLayout测试；Hello主控；已证red后授权窄改JigsawHuntingCombat.Postfix共享追加布局，原怪marker不擅动；复用Queen已审真实host/数字包络|两版编译0error；beta entry1首AxebotsNormal真实red：新增Mystic hitbox交原Axebot22×250、HP交目标框28×19logical；public未执行，作者据实窄修，日志实施树.local/executor-glory-layout/entry1-beta-host.log；最小`Jigsaw Glory native layout`2case；不得用临时marker/factory或抽样替代15原生场景；其他意图/真实端仍#86|2026-11-14 03:36 +08:00|
|C85-knowledge-fresh / #85|原Hive Knowledge Boss第一轮真实选择/私有计数与战斗扰动后保存→新大厅完整战前/第一对与counter0；真胜利非终局奖励第二fresh|Doing|docs_standards / `knowledge-fresh-20261114`，基`2c44d4b`|独占新KnowledgeRecovery测试；Hello主控；生产/共享checkpoint只读，真缺陷先报；原SetBossEncounter只在source设场景，恢复不设|已核KnowledgeDemon私有`_curseOfKnowledgeCounter`、原ActTwo/Forest/Queen入口；最小`Jigsaw Knowledge fresh`双版各1起，ActTwo8相关；owner UI/多人时序/首普通房仍#85后续|2026-11-14 03:49 +08:00|

## 候选池：尚不能派发实施

|候选/issue|具体缺口与建议切片|准备前需核清|证据入口|
|---|---|---|---|
|C82-rest-native / #82|真实原生modifier/遗物入口组合保禁用条件、追加项/加成/额外次数；无合法项正式退出另核真实来源|#82未指定禁Heal遗物，旧候选前提过窄；禁Smith单独仍留Heal，不能代签无合法项。现RestExit手删Heal/Mend及手加额外Heal仅故障模拟；两版现有源码尚未找到真实无合法项来源，CoffeeDripper空cache不可用|[82审查](issue82-review.md)、`JigsawHuntingServices.RestChoicesRemain/JigsawHuntingRestExit`，2026-11-14只读候选复核|
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
|#84 密林首普通完整fresh|`c7dbd18`，合入`2c44d4b`|首真实受罚Overgrowth普通session.Enter，完整开场五堆/RNG/根房同点，真BITE/GROW source counter1→新大厅counter0；末敌孢子Vulnerable2→原清理/回血6→真胜利第二fresh非空领奖/Proceed|双轴0发现、hook patch一致；专项双版各1及相关各6全0，集成双版fresh各1四诊断0无RID；[恢复增量](issue84-overgrowth-implementation.md#密林首普通完整战前与胜利奖励-fresh2026-11-14)，单owner同进程，其他敌人/布局/真实端待|
|#87 Queen正式四组合布局|`372c488`，合入`f55305f`|真Kill/重燃两Torch，原Queen/聚合体保留，1/4角色×1280/1680宽720；真实HP/意图图标及10伤文本含溢出描边不交不出屏，精确鼠标target与含players全环左右导航|实证两Torch HP交叠70.2logical后仅左marker1150→1050，血条间距19.8；双轴/数值增量0阻塞、hook patch一致，默认导出集成双版Queen各9四诊断0无RID；[布局增量](issue87-boss-implementation.md#queen-正式布局与原生输入2026-11-14)，非全部意图/实体设备/真实端|
|#86 Mystic完整fresh|`9e52b1c`，合入`fa41442`|首原生普通受罚遭遇，独立完整开场/五堆/能量/Block/powers/RNG，实战CAST→HEAL→BUFF扰动后新大厅回CAST；真胜利非空奖励第二fresh领取/重复拒绝/原Proceed|双轴无阻塞、profile内存独立克隆还原补齐，正常hook patch一致；实施profile-final及集成双版各7四诊断0无RID；[恢复增量](issue86-hunting-implementation.md#mystic-完整战前与非空胜利奖励-fresh2026-11-14)，单owner同进程，不代签12场景布局/真实多端|
|#85 Tender真实owner屏|`1570ab2`，合入`35bb69d`|单owner第二轮无selector旁路，真鼠标Tender能力恰一次、原screen释放、重复release不重施加、恢复怪物hitbox输入|双轴无阻塞，hook patch完全一致；集成Tender1+ActTwo8双版四诊断0无RID；[owner屏增量](issue85-hunting-implementation.md#knowledge-tender-真实-owner-选择屏2026-11-14)；多人/控制器/完整恢复未代签|
|#87 Queen完整fresh|`5ac1e5d`，合入`09d91c2`|真实session.Enter/战中保存，两次新LoadRunLobby；完整玩家存档与五堆顺序/Energy/Block/powers、RNG/Act/敌人开场重建、重燃标记重置、终局胜利点不重开|双轴P2牌堆缺口已修复复审；正常hook patch一致，实施piles-final及集成双版各1全0无RID；[恢复增量](issue87-boss-implementation.md#queen-完整战前与终局胜利点-fresh2026-11-14)；单owner同进程新大厅，不代签新进程/真实多端或非空领奖|
|#87 Queen一次重燃|`8ef5ff7`，合入`8e8b480`|真实聚合体死亡替换6个Standard意图一次，独立TorchHead50HP/10伤/Minion，原coordinator出生不攻/下轮直攻，女王激怒仅本源活火炬+2、死亡自然清理|双轴无阻塞，正常hook十处return换行已复核；默认导出集成双版各7 PASS四诊断0，无CanvasItem RID泄漏；[Queen增量](issue87-boss-implementation.md#queen-原生重燃与独立火炬2026-11-14)，完整布局/恢复/真实端仍归#87|
|#83 正式整房开箱输入|`fb58274`，合入`2915a6a`|匹配双版正式场景，真实鼠标开箱/Proceed，原空Tween与早按负例，自然完成/保存，新大厅回MapRoom不重复Face/Spoils/扣步|正常hooks，提交patch与已审一致；Standards setup异常清理P2已修复复审；实施返修/集成双版各11 PASS四诊断0；[宝箱增量](issue83-treasure-implementation.md#正式整房输入与自然保存恢复2026-11-14)，真实双游戏端/设备仍待|
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
|#83|空箱/Face/Spoils、异步等待、恢复与双端证据已集成|正式整房输入/动画与单owner自然保存已集成；真实双游戏端/设备与退出归属仍待|
|#84|暗港/密林多片、共享检查点、Waterfall自然致死规则|其余随从/规则、事件、适用恢复/网络与正式布局|
|#85|Knowledge/Hive、蟹开场；Holes三种三端恢复已集成|owner UI/连续事件、正式布局/输入/自动UI保存、其他战斗恢复与适用真实端|
|#86|Mecha/Soul/Flail/Mystic原生动作及资源双版专项已集成|全场景/精英布局、完整战前/奖励fresh恢复和真实端|
|#87|Aeonglass/TestSubject、MultiClaw、Wither已集成|Boss间歇问题、完整恢复/布局/真实端；Queen重燃与TorchHead生命周期已集成|
|#88|兼容目标资源、Compass/Flight/控制台/每日字段已集成|组合验收归属、最终Check/双版包/许可、人工矩阵；不替原票承担缺失玩法|

人工验收项仍属于各issue，由主控整理可执行批次再交用户；不能为避免停顿就宣布人工通过，也不让用户每完成一个自动化片来回传话。

## 状态更新约定

主控派发/发审/暂停/集成时修改对应行并链接实际证据；卡片owner回报由主控落板，其他Agent不并发写板。使用已有Git提交和日志，不添加hash、冻结contract或独立审批系统。若改动尚未提交，明确工作副本状态。

Ready→Doing时填实际开始时间；结束填集成时间及证据。初始0/2和Ready数量随状态变动一起更新。候选按实际新发现合并/拆分，不用卡片数量计算完成百分比。下一主控只需本板、当前目标卡及相关源文档，不读所有研究报告。
