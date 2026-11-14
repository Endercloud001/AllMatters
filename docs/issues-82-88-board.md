# #82–#88 当前调度看板

更新：2026-11-14。唯一维护者：集成主控。集成树：`AllMatters/.claude/worktrees/hunting-20261112`，分支`integration/issues-82-88-20261112`。当前源码HEAD=`eb4837c`（Waterfall完整fresh已合入，集成双版各8 PASS四诊断0；蟹原场景保持双版各9）；交接审查起点为`1464953`，流程入口`776e2ef`、Holes源码`1956c17`保留。

目标：public v0.107.1 / beta v0.113.0 / RitsuLib 0.6.11。本轮已实际查询GitHub，#82–#88均OPEN；当前本地切片不自动关闭issue。后续主控更新此文首和卡片，不再另建当前状态表。

**当前在制2/2：Knowledge首轮双owner错峰选择、Matriarch正式布局/输入；技术诊断暂停1；Ready 0。** Glory和SoulFysh开场均已独立双轴审查、正常hooks提交并集成；SoulFysh完整专项集成双版各17及此前Underdocks各7 PASS，四诊断0。下节旧交接记录仅保留当时失败签名，当前动作以活跃卡为准。

## 拉取入口

Seapunk开场及完整fresh已集成，正式14场布局已修真实scene/自动行遮挡并完成实施双版各14，集成双版各14 PASS四诊断0；生成锚点有效不等于HP和目标输入可用。Aeonglass完整fresh已集成8481af1，实施及集成双版各1 PASS四诊断0、独立双轴无阻塞、正常hooks patch一致；TestSubject完整fresh已集成cc0bc71，实施/集成双版各1四诊断0；Phrog完整显示/输入已由3961924→22a2cc3集成、实施及集成双版各7四诊断0；Vantom实施及集成双版各12四诊断0；Matriarch完整fresh已集成；Vantom完整fresh已集成双版各13四诊断0，Phrog死亡前后名单重建已集成；帝皇蟹仅加效果/原场景保持对照已集成，Waterfall特殊复生fresh已集成，Knowledge双owner在制，原生休息遗物已集成双版遗物2+原native rest2四诊断0；主控继续唯一执行native/API/build/hooks。

### 2026-11-14 对话交接

- 集成树在写本交接前为`1464953`，交接改动只涉及本看板和实施索引；主仓`f7e915c`旧删除/未跟踪文件未动。已集成Forest `c7dbd18→2c44d4b`及Knowledge `5b88af8→2e61481`，各有独立双轴、正常hooks与集成双版专项；先前实施记录/索引已提交至`eeda0dc`、`1464953`。不要重跑文档变更所对应的行为用例。
- Glory树`glory-layout-20261114`基`f55305f`、分支`test/issue86-glory-layout-20261114`，**未提交**：改`src/Jigsaw/JigsawHuntingCombat.cs`中Mystic有scene追加坐标、新`src/Jigsaw/JigsawHuntingGloryLayout.cs`自动布局、`src/Tests/JigsawHuntingGloryLayoutTests.cs`及Hello注册。Godot导出留下多份`.import`工作区改动，提交前查实际numstat，勿误纳入。两版最近编译`compile4-{public,public-beta}.log`均0。最初beta`entry1-beta-host.log`在Axebots新增Mystic hitbox/HP交原Axebot真实red；`focus3-beta-host.log`证实scene修复已走完Axebots四组合，但在Construct Menagerie自动布局有新增HP遮挡。最新`automatic4-beta-host.log`在Devoted Sculptor Weak的1角色1680×720运行中命中`TestRunner`每用例60秒看门狗：Build0/Host1/Passed空，Engine/Metadata0，退出时4条CanvasItem RID与ObjectDB泄漏；**不能将途中已测场景记为15场景通过**。当前一个普通用例串行跑12遭遇×safe/hunted×1/4角色×两宽，下一步按遭遇拆为有独立PASS的原生用例，保全矩阵和oracle，再对新源码双版跑专项及受影响Forest/Mystic场景。public行为本轮未运行，尚无双轴审查/提交/合并。
- SoulFysh树`soulfysh-minions-20261114`基`1464953`、分支`feat/issue84-soulfysh-minions-20261114`，**未提交**：新`src/Jigsaw/JigsawHuntingSoulFysh.cs`、新`src/Tests/JigsawHuntingSoulFyshTests.cs`、Hello注册。beta默认资源导出`red-beta-host.log`取得真生成`expected=3, actual=1`；加生产后`green1-beta-host.log`原开场1 PASS，Build/Host/Engine/Metadata0，未测public行为；该次编译仍有新测试第82行三处CS8602警告，另有既有Phrog测试CS8604。当前正式测试只注册这个开场1例；未完成的扩展草稿留在忽略文件`.local/soulfysh-expanded.cs.txt`，不得把草稿当成已跑/已审。下一步读草稿与现候选，补真Boss死亡自然清Minion、safe/事件/过期来源及1/4角色两宽正式HP/意图/鼠标/焦点，先按实际缺口跑最小case，再双版相关Underdocks回归和双轴审查。两树都有Hello注册，串行合并时保留两条。
- #87 Boss原六项2绿1同症状红、随后三次probe绿且探针已撤回，故障仍未修；诊断B树与主仓`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`保留。没有区分假说的新证据前不重跑或签修复。最终完整Check、同提交双版包/许可和人工矩阵仍属#88，当前任何局部通过都不能关#82–#88整票。

跨票验收盘点可由主控一次完成，用于发现#82/#83可收口项；不要把“盘点全部历史”作为后续每片的共同前置。详细操作见[批次流程](agents/batch-flow.md)。

## 活跃与就绪

|卡片/issue|行为|状态|owner / worktree|依赖与热点|下一动作/证据|开始时间|
|---|---|---|---|---|---|---|
|C85-knowledge-owner / #85|两真实owner首轮错峰选择，先选端释放屏幕，另一端继续等待，最后各自效果且仅推进一次|Doing/合法双owner准备返修|`glory_layout` / `knowledge-owner-20261114`，基`f1eb1dd`|新KnowledgeMultiOwnerTests/Hello；生产和公共宿主只读|brief `.local/batch-scope-audit/knowledge-owner-next-slice.md`；蟹已流出，选择原生双端等待/owner归属独有缺口，新树干净/ignore检查及ReviewScope已做，owner已派发；资源已默认导出；首beta编译12个准备错误已修，compile2双版0错；新独立完整五堆/RNG oracle已补；entry1-beta在真ENet握手后地图AncientIncomplete夹具失败，Build0/Host1/无PASS/EngineMetadata0，owner核全员完成顺序，尚未到屏幕/public未跑；手工动作链不签原coordinator调度，复用单owner旧证据|2026-11-14 拉取|
|C84-matriarch-layout / #84|真实新增睡眠Lagavulin的睡/醒态完整显示、原中心鼠标及含玩家焦点环|Doing/独立树实施|`matriarch_fresh_prepare` / `matriarch-layout-20261114`，基`eb4837c`|新MatriarchLayoutTests/Hello；默认tests-only，实证red才报生产范围|brief `.local/batch-scope-audit/matriarch-layout-next-slice.md`；已核无scene/slot、真实醒态Bounds变化，先safe/hunted紧凑实测，后四组合双版；新树干净/ignore检查和ReviewScope已做，owner已派发，复用Underdocks7/fresh1|2026-11-14 拉取|

## 候选池：尚不能派发实施

|候选/issue|具体缺口与建议切片|准备前需核清|证据入口|
|---|---|---|---|

## 暂停与等待

|卡片/issue|状态/owner|当前事实|恢复条件|证据|
|---|---|---|---|---|
|P87-boss / #87|诊断暂停；无人继续运行；保留B树改动|原六项2绿1同症状红；随后3次probe绿、未捕获原错，probe已撤回，未修复|一个能区分现有假说的新观测/实验设计或新失败证据；主控限定预算后拉取，不盲目再跑|主仓`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`及controller末节；B=`boss-warmed-callback-20261113`|
|W88-delivery / #88|等待范围收口|最新完整Check、同源码双版包/许可及正式端交付未完成|#84–#87适用剩余行为收口、当前矩阵明确；按现行交付执行|[实施索引](issues-82-88-implementation.md)、[开发入口](development.md)|

暂停项不占活跃槽位，但持续显示年龄/恢复条件。Boss历史开始时间未统一计量，写“未计量”，不能凭本板创建日期重置。原组故障不自动阻塞所有Queen准备或其他issue。

## 最近集成：切片完成不等于issue关闭

|issue/切片|源码提交|已证范围|剩余边界/证据|
|---|---|---|---|
|#84 Waterfall复生/爆炸态完整fresh|`2fb2b0a`，合入`eb4837c`|真实复生/原About捕获15及pressure6后保存，新大厅回有限原HP/PRESSURIZE/零旧能力；再复生恰Artifact1/挡Weak，原爆炸胜利及第二大厅非空领奖/Proceed|独立双轴P2闭环、正常hooks patch一致；实施related双版各8、8行返修fresh各1及集成双版各8四诊断0；[记录](issue84-underdocks-implementation.md#waterfall-复生爆炸态与非空奖励完整-fresh2026-11-14)。单owner同进程，不签正式端|
|#85 帝皇蟹原场景保持|`4e4bf4c`，合入`f1eb1dd`|仅双臂Artifact3，safe/hunted同相位原生场景/可见HP集合/原中心鼠标及全环焦点相同，保留共同原生交叠|独立双轴无阻塞、正常hooks patch一致；实施及集成双版crab各9四诊断0；[记录](issue85-hunting-implementation.md#帝皇蟹仅效果强化与原场景保持2026-11-14)。生产/资源零改，fresh及真实端待|
|#84 Vantom普通墨宝完整fresh|`d390f50`，合入`44413ea`|真实四招/三Wound/Strength/StateLog与墨宝部分死后完整三敌开场fresh；Boss死仍须杀完普通墨宝，第二大厅非空奖励领取/Proceed|双轴无阻塞、正常hooks patch一致；实施双版各1及集成Vantom双版各13四诊断0；[记录](issue84-overgrowth-implementation.md#vantom-普通墨宝与非空奖励完整-fresh2026-11-14)。单owner同进程，不签真实端/进程/正式奖励输入|
|#82 真实休息遗物/双run自然离开|`09fa14f`，合入`76e49d2`|原Tent Heal→精确Mend/禁Smith+Dig；safe原袋精确Dig；真ENet两run各完成/投票/原ready自然下一根|双轴两P2闭环、正常hooks patch一致；实施beta1+1/public2及集成遗物2+原native rest2双版四诊断0、child无诊断/stderr0；[记录](issue82-hunting-implementation.md#真实休息遗物与双-run-原生离开2026-11-14)。正式UI仅host，真实游戏双UI/设备待|
|#84 Matriarch醒态/随从计数完整fresh|`050314d`，合入`9364122`|真Damage醒body/shell、随从SleepTurnCount2、完整五堆/RNG/双方能力扰动后第一新大厅原开场；真胜利第二大厅空终局不重开|双轴无阻塞、正常hooks patch一致；实施/集成双版fresh各1及集成Underdocks各7四诊断0；[记录](issue84-underdocks-implementation.md#matriarch-醒态与睡眠随从完整-fresh2026-11-14)。单owner同进程，不签布局/真实端/非空奖|
|#84 Vantom三敌完整显示/原生输入|`d9e72b2`，合入`307e829`|Boss/SecondBoss×1/4角色×两宽八布局及原四例；正式HP/意图、精确鼠标与含玩家全环，resize/input模型身份/RNG不变|实施及集成双版各12 PASS四诊断0；独立双轴闭环、正常hooks patch一致；[记录](issue84-overgrowth-implementation.md#vantom-与墨宝完整显示及原生输入2026-11-14)。初始hook模型不变为源码审查；完整fresh/全部后续意图/真实端待|
|#84 Phrog完整fresh|`9236f53`，合入`9c17a70`|旧战只剩三虫后新大厅回活Phrog零虫，再死五新虫/末虫才胜利；第二新大厅非空原Elite领奖/重复拒绝/Proceed|实施及集成双版原Phrog组各8 PASS四诊断0；独立双轴及原caller观察/Task.CompletedTask缓存增量通过，正常hooks patch一致；[记录](issue84-overgrowth-implementation.md#phrog-死亡生成与残虫完整-fresh2026-11-14)，单owner同进程，不签P87修复/正式端|
|#84 Phrog五虫完整显示/原生输入|`3961924`，合入`22a2cc3`|原非零死亡/五虫链，1/4角色两宽完整HP与意图描边、精确鼠标target、含玩家左右全环及模型/RNG不变|初始六对及新增完整HP相交red均已修；独立双轴返修闭环、正常hooks patch一致；实施/集成双版各7四诊断0；[记录](issue84-overgrowth-implementation.md#phrog-五虫完整显示与原生输入2026-11-14)。不签暂停Boss诊断、完整fresh/真实端|
|#87 TestSubject完整fresh|`d0ca778`，合入`cc0bc71`|真实两复生/第三形态clock3细看拒抽→fresh第一形态/clock缺席，再两复生新clock1可抽，终局第二fresh不重开|双轴无阻塞、正常hooks patch一致；实施及集成双版各1四诊断0；[记录](issue87-boss-implementation.md#testsubject-第三阶段与细看完整-fresh2026-11-14)，终局空奖/单owner同进程，不签暖Phrog修复或真实端|
|#87 Aeonglass完整fresh|`fff3232`，合入`8481af1`|真实个人旧counter11/五状态/Ebb护甲与全战斗扰动，新大厅完整开场counter12，真实胜利第二大厅终局不重开|独立双轴无阻塞、正常hooks patch一致；实施及集成双版各1四诊断0；[记录](issue87-boss-implementation.md#aeonglass-个人计数与终局完整-fresh2026-11-14)，终局空奖励，不签非空领奖/真实端|
|#84 Seapunk正式14场布局|`02b5f60`，合入`10797aa`|14普通逐场safe/hunted×1/4角色×两宽，正式HP/意图描边、鼠标精确目标及含玩家左右环，scene原marker保持|实施及集成双版各14、相邻Glory/SoulFysh scene各1+1四诊断0；双轴含无scene增量无阻塞，正常hooks；玩家display对豁免边界见[记录](issue84-underdocks-implementation.md#seapunk-正式布局与原生输入2026-11-14)，真实端待|
|#85 Hive首普通完整fresh|`c2cb995`，合入`2cf965c`|真实Act1/SpinyToad进入、三负面原History各一次、竖刺扰动后完整五堆/RNG/根房回滚、两新大厅非空领取/重复拒绝/原Proceed|独立双轴源码无阻塞、正常hooks patch一致；实施/集成双版各1四诊断0；[记录](issue85-hunting-implementation.md#hive-首普通完整-fresh2026-11-14)，无药水roll/真实多端/正式奖励输入|
|#84 Seapunk完整fresh|`97cf05a`，合入`d7fec1c`|已有同类/原生scene两入口、完整五堆/RNG/slot/原marker/消耗计数、正常追加需击败、两新大厅非空领取/重复拒绝/原Proceed|独立双轴0阻塞，正常hooks patch一致；实施各2/集成各7双版四诊断0；[记录](issue84-underdocks-implementation.md#seapunk-已有同类与-scene-完整-fresh2026-11-14)，无药水roll/真实多端/正式奖励输入|
|#84 SoulFysh完整fresh/最终Boss边界|`84124b9`合`8643b20`，oracle协调`3e83ea5`合`44ce260`|三fresh单Boss/dual-first/SecondBoss完整五堆/RNG/隐身/Minion/自然清理、非空金币卡领取/重复拒绝/原Proceed；补2/3角色四开场|独立双轴含Seapunk oracle增量无阻塞，正常hooks patch一致；实施及集成双版各17四诊断0；[记录](issue84-underdocks-implementation.md#soulfysh-完整-fresh-与最终-boss-边界2026-11-14)，本seed药水未roll，真实多端/奖励按钮仍待|
|#84 Seapunk普通/事件开场|`a7c9c52`，合入`efdc880`|普通/Unknown相对原生+1、14池×1–4角色、三scene实际slot、原生三招及非Minion需击败|独立双轴含旧oracle增量通过，正常hooks已审patch一致；实施/集成双版各5四诊断0，相邻入口1+1+scene1+Underdocks7双版通过；[记录](issue84-underdocks-implementation.md#seapunk-普通与事件原生开场2026-11-14)，完整fresh及正式14场布局已由新记录补证，真实端待|
|#84 SoulFysh左右随从及布局|`2d3658c`，合入`d9b80b4`|Boss/SecondBoss 1/4角色真实开场、左右随从Minion1、Boss自然死亡清理、来源边界及1/4角色两宽正式输入|双轴无阻塞、正常hooks仅换行；实施及集成双版各10 PASS，Underdocks回归各7 PASS，四诊断0；[记录](issue84-underdocks-implementation.md#soulfysh-左右随从与正式布局2026-11-14)，2/3角色及完整fresh已由下条记录补证，真实端仍待|
|#86 Glory正式布局|`a050e7b`，合入`21a563f`|12普通含Mystic及3精英逐场safe/hunted、1/4角色、1280/1680宽，真实HP与意图文本、鼠标目标及方向导航|独立双轴及返修复审无阻塞，正常hooks；实施与集成双版各15 PASS、四诊断0；同源码Forest1＋Mystic2，双版合计各3 PASS；[布局记录](issue86-hunting-implementation.md#glory-正式布局与原生输入2026-11-14)，真实设备/多游戏端仍待|
|#85 Knowledge计数/非终局奖励fresh|`5b88af8`，合入`2e61481`|原首选Calamity/counter1/SLAP保存→新大厅完整原开场counter0/首对；自然胜利第二fresh金币100/卡领取、重复拒绝/原Proceed|双轴0发现、hook patch一致；实施及集成fresh双版各1/ActTwo各8四诊断0无RID；[恢复增量](issue85-hunting-implementation.md#knowledge-选择计数与非终局奖励-fresh2026-11-14)，单owner同进程，本次无药水roll，普通Hive/多owner/UI未代签|
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
|#83|空箱/Face/Spoils、异步等待、正式整房输入与单owner自然保存恢复已集成|真实双游戏端/设备与退出归属仍待|
|#84|暗港/密林多片、共享检查点、Waterfall自然致死、SoulFysh左右随从及1/4角色正式布局|其他布局/事件、适用恢复/网络、真实多端/正式奖励输入|
|#85|Knowledge/Hive、蟹开场、Knowledge Tender owner屏与fresh、Holes三种三端恢复已集成|连续事件、其他正式布局/输入/自动UI保存、其他战斗恢复与适用真实端|
|#86|Mecha/Soul/Flail/Mystic原生动作、Mystic完整fresh、12普通/3精英正式布局及资源双版专项已集成|其他适用恢复和真实端；布局自动证据不代签真实设备|
|#87|Aeonglass/TestSubject、MultiClaw、Wither、Queen重燃/TorchHead/完整fresh/布局已集成|Boss间歇问题、其他适用恢复/布局/真实端|
|#88|兼容目标资源、Compass/Flight/控制台/每日字段已集成|组合验收归属、最终Check/双版包/许可、人工矩阵；不替原票承担缺失玩法|

人工验收项仍属于各issue，由主控整理可执行批次再交用户；不能为避免停顿就宣布人工通过，也不让用户每完成一个自动化片来回传话。

## 状态更新约定

主控派发/发审/暂停/集成时修改对应行并链接实际证据；卡片owner回报由主控落板，其他Agent不并发写板。使用已有Git提交和日志，不添加hash、冻结contract或独立审批系统。若改动尚未提交，明确工作副本状态。

Ready→Doing时填实际开始时间；结束填集成时间及证据。初始0/2和Ready数量随状态变动一起更新。候选按实际新发现合并/拆分，不用卡片数量计算完成百分比。下一主控只需本板、当前目标卡及相关源文档，不读所有研究报告。
