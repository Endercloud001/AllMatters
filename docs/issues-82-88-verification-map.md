# #82–#88 验证覆盖地图：V14 / S6 / H13 / U8

首轮2026-11-13，2026-11-14补充当前缺口再核。下表保留首轮审计时的证据及缺口；当前实施提交以[实施索引](issues-82-88-implementation.md)和下述最新增量为准，历史W锚点不是当前分支状态。首轮审计仅只读调查，后续逐片实施；均未启动游戏、安装或改真实存档。编号沿来源；S1–S6、H1–H13、U1–U8在本文表示**六/十三/八个场景行**，不要与交接文档中 S1模型、S2注册、S3保存等证据级别混淆。D=领域行为，N=真实原生DLL/命令/保存，G=真实Godot树/输入，M=正式游戏真实端。ENet本机独立进程单列，不能自动代签M。

## 后续实施增量（均未作为整票交付）

### 2026-11-14 批次缺口再核（覆盖旧表的当前状态）

本次重新读取GitHub #82–#88正文（均OPEN；本机原文`.local/batch-scope-audit/issue-{82..88}.json`）与最新实施增量、当前测试入口，首次盘点时源码`d7fec1c`。目标仍为用户已确认的public0.107.1/beta0.113.0/RitsuLib0.6.11；工单旧beta0.111.0不覆盖当前目标。以下用于选择下一有价值切片，不给未逐项重跑的旧行补签，也不把历史Check视为当前完整Check。

|责任票|已落实且不重复开发|当前仍需核实/实施或集中验收|
|---|---|---|
|#82|原生服务限制/库存、真实休息及HUD输入、完整点/ENet、控制台已有实现与审查|真实原生禁项/附加选项及额外次数来源要准确；无合法选项现为故障模拟，未发现合法原生来源，不虚构CoffeeDripper。当前目标相关最终回归、正式双端与人工归属留原票。|
|#83|双版匹配资源正式整房鼠标开箱/Proceed、Tween与Face/Spoils、自然保存fresh已补；旧表“public整房未测”已过时|真实游戏双端/实体设备及支持来源准确清单保留，不能把未知第三方支付视为支持。无需为本票再造一遍正式开箱。|
|#84|Forest首普通、SoulFysh三种Boss、Seapunk已有同类/scene完整fresh均已集成；Seapunk开场及SoulFysh四组合正式布局见看板|Seapunk14场正式布局已集成且双版各14四诊断0；Forest五虫/墨宝等正式完整场地、Matriarch/Waterfall等特殊状态适用fresh/布局须按实际来源补；事件/多端责任不转#88。不能从SoulFysh单owner推全票恢复。|
|#85|Knowledge完整选择计数fresh、单owner Tender真实屏；Holes战前/奖励/完成点三轮ENet三端；蟹固定Artifact3已集成|普通Hive首真实受罚遭遇完整fresh已由c2cb995→2cf965c补证，实施/集成双版各1四诊断0；旧ActTwo.HiveNormalOpening仍是构造/直接Hook，只作开场单元证据；连续独立事件、其他特殊Boss恢复、正式选择多owner/焦点与布局另按实际缺口准备。|
|#86|Mystic首普通完整fresh与原生三招、Glory12普通+3精英正式布局及原生输入已集成|旧表“Mystic未生产/未布局”作废；保其他适用特殊状态恢复和真实设备/多游戏端归属，不无依据把15场再跑一次。|
|#87|Queen一次重燃/火炬、完整fresh、四组合正式布局，MultiClaw/Wither源行为已集成|Aeonglass个人计数完整fresh已由fff3232→8481af1补证，实施及集成双版各1四诊断0；TestSubject第三阶段完整fresh已由d0ca778→cc0bc71补证（实施/集成双版各1四诊断0）；适用正式布局尚缺；Boss原组间歇故障仍暂停未修，只有可区分新假说才恢复实验，不用probe绿签修。|
|#88|匹配public资源、两遗物/Flight/控制台/每日字段已实现，用户#93/#94既有人工结果保留|V01–14/S6/H13/U8旧表是历史审计；当前未齐整票证据继续由原票承担。最终同提交完整Check、双版包/许可及集中人工矩阵仍待；真实手柄责任明确列出，不要求当前用户补设备。|

本轮已完成 #85 **首普通Hive完整fresh**（c2cb995→2cf965c）；本片范围为：从已核两版`Hive.GenerateAllEncounters`及现有`SpinyToadNormal`入口入手，只在准备期选择合法原生队列，实际session.Enter→战中保存→新大厅完整开场/三种初始负面一次→自然胜利保存→第二新大厅非空领取/原Proceed。复用已有Forest/Seapunk恢复宿主，不改Hive生产、不重复已证开场1–4角色矩阵。Aeonglass个人旧计数重置及终局保存已集成补证；TestSubject完整fresh亦已集成补证；当前Phrog正式布局与#82原生休息遗物在制，当前动作见唯一看板；不依赖暂停的Phrog原组六项诊断。其他远期缺口不在此次盘点时细化全部文件级计划。

以下为2026-11-13历史增量（当时源码`1956c17`）；当前状态以前段再核和实施索引为准。目标public0.107.1/beta0.113.0/RitsuLib0.6.11，匹配public资源及辅助依赖已齐。商店场景、Phrog右侧布局、Hive精英Weak2、MultiClaw9/10及原生3+N、public启动Task观察修复、Waterfall清负面与Artifact1、Hive事件普通三项各1、Holes单owner战前/奖励恢复、Wither强化链及Holes三端战前/胜利奖励恢复均已独立双轴审查并正常集成；准确提交、专项数量和边界见[实施索引](issues-82-88-implementation.md#当前接续状态2026-11-13)。

Holes完成片 `1956c17` 补全已领／Proceed后全员真实 `NMapScreen.Open(false)` 认证→原生host SaveRun→第三轮全新三端 LoadRunLobby 自然 MapRoom。父事件完成与根完成保存分开：无退出／仅两端退出不提升，全员完成才投影保存副本，重复保存根只计一次且live投票根保留。expected按阶段及端点/profile归属核对完整Players／RNG／history／Page／Point／Map；固定host名单 `[1,3,2]` 覆盖保存副本顺序。实施树双版event-各4 PASS、宝箱共用恢复各1 PASS，四诊断均0，日志 Holes 树 `.local/executor-holes-completed/ordered-event-final-{public,public-beta}-host.log`、`treasure-final-{public,public-beta}-host.log`。独立双轴无阻塞，正常hooks差异无漂移。裸地图节点调用真实入口、显式native保存并隔离IO；正式输入／自动UI保存／部分领取恢复未证，详见[Holes完成片](issue85-hunting-implementation.md#holes-全员退出后的完成点保存2026-11-13)。

集成树同源码 `1956c17` 串行复验双版 `event-` 各4 PASS，Build／Host／Engine／Metadata全0；实际日志 `.local/executor-holes-completed/integrated-event-{public,public-beta}-host.log`。完成点目录 public `b6ef480e-09d6-40b9-9bd3-49e09c0e05ec`、beta `dc22b61a-bfbc-4f44-966f-5f206f7dfda2`，各三轮九份.err为空、第三轮三端均 MapRoom／opening=0／rewards=0。

Holes三端新增证据为真实ENet三进程投票/最后owner任务屏障/完整保存，再启动全新三进程经LoadRunLobby恢复；完整玩家、所有相关RNG、敌人、有限锁定父页、检查点、根房及history均比较，开场一次且旧动作inert。预期区分pre-opening checkpoint与首次原生discovery同步，并采用入房前独立完整验证的新大厅roster顺序。集成树同e088feb双版各event-combat-lag+event-holes-lag共4 PASS，Build/Host/Engine/Metadata全0，日志 `.local/executor-holes-integrated/event-pair-final-{public,public-beta}-host.log`。这是自动化宿主独立端进程证据；新增奖励片与Boss限定复现见下段。正式客户端/输入与最终全量Check/双版包仍未证。

Holes三端奖励片 `31e849a` 复用真实投票/最后owner任务，自动胜利保存后观察完整原生奖励，再从host保存点启动三个新PID恢复；完整source→fresh玩家、run/owner RNG、父页、Point/Page、地图、history、奖励身份比较。领取前预测每端本地药水首次发现记录；实际金币/奖励牌身份升级/药水、重复领取拒绝及Proceed后的完整允许变换均验证。实施树双版event-组合各3 PASS，四诊断均0，日志 Holes 树 `.local/executor-holes-reward/event-final-{public,public-beta}-host.log`。两轴独立审查0发现、正常hooks差异无漂移；不声称部分已领取存档可恢复；当时未覆盖的多端完成点第三轮fresh见上方 Holes 完成片。

集成树同 `31e849a` 串行复验相同 `event-` 组合，public / beta 各3 PASS，Build/Host/Engine/Metadata全0；最终日志 `.local/executor-holes-reward/integrated-event-{public,public-beta}-host.log`。奖励目录分别为public `5bda2fe6-1044-4435-8980-4c96945df0f1`、beta `2a588df1-0b24-44c0-9f1f-a54de92fe69a`，各三capture+三fresh、六.err为空。

Boss本轮原六项保持原runner，三次新进程前两次6PASS、第三次第六第一段Phrog HP链同症状失败（本轮1/3，非稳定率）；当前间歇复现、根因未知、未修复。三次均0 warning/约1.2秒构建，未做JIT变量/探针/生产补丁。日志 Boss 树 `.local/executor-boss-warmed/reward-window-original-{1,2,3}-public-host.log`，原条件有限预算停止。后续低扰动内存观测仅跑三个原组六项新进程，各6 PASS且四诊断0，首Kill均8对Notify、当前tracker/combat/owner匹配、mode true→false→true；未捕获带探针原错，不作修复或JIT结论。日志同目录 `mode-probe-{1,2,3}-public-host.log`，临时probe已备份后精确撤回，预算未扩大。

Holes在8869289同提交两版各Holes14+Checkpoint8+EventCombat4，共52 PASS，Build/Host/Metadata均0；Holes每版1条预期ownership拒绝Engine诊断，其余四跑Engine0。新增7例贯穿真实AM_FIGHT、完整玩家/run+event+monster RNG与初始敌人恢复、实际奖励身份/领取/Proceed、完成点重载、重复两次回退、安全扣0与非法组合；原生pending集合延迟完成效果进入保存点，换成同entry新map则拒绝旧任务且不写状态。奖励用例显式调用CompleteCurrentRoom模拟宿主缺失的地图UI退出，不代签正式界面。此历史组合未涵盖多owner；最新三端战前及奖励证据见上段，其余适用恢复仍属#85后续、不转#88；当前全量与最终包仍未重跑。

Waterfall在c469b9a受影响组合public19/beta18通过，实际是具名Boss；Hive在070a0ae同提交两版各Hive10+ActTwo8+EventCombat4，共44项通过，均Build/Host/Engine/Metadata0。真实AM_FIGHT选项、原生同Hook任务和完整下一回合已验证；public首次负面tick跳过与beta三条-1衰减均按原生保留。事件Elite/Boss普通增强的构造反例不代表自然事件池，1–4combat角色不代表真实多端。连续独立事件战、Waterfall首次致死自动Trigger及完整正式布局仍未证，仍由对应#84/#85后续片承担，不转嫁#88；Holes单owner恢复新增证据见上段。

同提交df6a1f9匹配资源双版全量已完整通过public509 / beta507，Build / Host / Metadata均0，Engine25 / 26；完整Check、同提交最终双版包、正式游戏端及#84–#88剩余机制/恢复/布局仍未交付。winner证据发布共享拒绝已真实最小red及有界处理、独立双轴审查、双版各3边界与7实际奖励/故障场景验证。两版引擎诊断分别分类，仍保留Visuals/动作队列/treasure空Tween/退出资源等实际限制，不能写零错误；准确来源见实施索引文首。以下全量失败均按历史检查点解释。

public全量已越过旧367/424/487失败，在496项PASS后因ENet子进程stdout日志AppendAllText文件占用异常退出1（Build0/Engine25/Metadata0，无最终通过汇总）。`c8f3bb1`改为持续drain双流、await终结后写证据，双版3项进程边界和7项实际ENet奖励/故障场景均通过且诊断0，同提交public全量随后推进至504项PASS，因第6个reward场景子进程3的winner临时文件重命名共享拒绝而退出1；Build0/Engine25/Metadata0，仍无最终通过汇总。旧输出回调异常未重现，新占用者未知，不认定两者同源。历史占用者未知，显式读句柄注入不冒称历史崩溃完整复现。beta495 PASS检查点有26条已分类EngineErrors，不能称0；新增RunRng断言和切片另有双版专项。Hive/MultiClaw仍不代签整票，完整恢复/正式布局/真实多端、完整Check及同提交双版包仍待。以下较早“当前”与旧版本/资源缺失段落均按历史检查点解释，不覆盖本段。

2026-11-13 新目标增量：用户确认 public v0.107.1 / beta v0.113.0；兼容检查点 `1d1dac0` 已集成，受影响专项双版各23通过、0失败并经独立两轴/返修复审；主控集成后 TestSubject双版各3通过。此前v0.111.0结果仍按历史版本解释。新版beta全量299项后首败旧坏档分类，完整Test/Check尚未通过；旧public全量262项后因资源/ENet错误失败，匹配public PCK未齐，Agent按用户要求调查补齐。V04下一片将保留新版原生Steam云存档安全策略，仅适配可恢复损坏夹具及拒绝回退负例，不以新增兜底放宽安全。完整细节见实施索引；不代签本表任意整组或正式多端验收。

最新集成锚点为 `bcd4060`：#85 的六case切片 `4641f0f`及普通开场`847cc845`、#86 的四case切片 `a5ba509`及Flail增量`c8b5fdd`、#87 的四case切片 `4a0c58b` 均已完成独立双轴审查并合入；#85/#86 首次合并后双版各10通过，Flail来源最新双版各5、Hive来源各7通过，后续合并专项尚待运行。#88 每日64种子原生抽样及字段完整序列化测试 `4aae096` 经 `f5d8dde` 合入，修复后双版各1通过。日志与准确边界见实施索引；下面首轮逐票叙述和W85–W87保留为历史来源，不再表示未提交。最终组合检查及同提交双版包仍待完成。

#84 暗港 Gardener/SkulkingColony/TerrorEel 双版各3和独立双轴审查通过，来源`936f557`经`551d85a`合入。密林24文件部分切片已暂存进入双轴审查，Vantom双版各3及随后Phrog回归各4通过；Phrog 真节点/非零原生死亡动画与1/4角色、两尺寸几何专项已证，public有40条已知共享beta PCK的NHotkeyIcon诊断。实际指针/焦点、Infested显示5、完整文本布局仍待，不能将这些局部结果当作完整G验收。

本轮最新更新至`2840635`：Matriarch生命切片`281110c`经`54a6cc0`合入，暗港最新双版各4；密林`2594715`正常消费新基后合入`2840635`，独立双轴与两次返修复审闭环，最终组合双版各25通过。首次组合发现无Jigsaw/无幕run.Act越界，已真实红绿并补原生生成反例；Phrog清理后双5，betaEngine0/public56全部同NHotkeyIcon。旧“Infested显示5”待办已撤回：原生Single能力图标和说明无数量显示，不能以Amount4推断可见缺陷；真实spawn5已证。当前整票仍partial，完整范围/证据以[实施索引](issues-82-88-implementation.md)和各票实现记录为准。

后续全量回归记录至本地`08b2e89`：Hive非会话guard已修复；Phrog两项顺序/真实canvas宿主问题经普通回归及双轴审查闭环，定向双7通过，public96条NHotkey诊断、Auto/Expand边界保留。正确源码full Check beta492通过，public在Boss首次五卡setup重复超时，正在诊断。不能以资源/Build均通过改写全量测试FAIL；详见[测试隔离与完整日志分类](issue84-phrog-test-isolation.md)。远端PR104最近同步为`3e45694`，后续本地提交尚未推送。

- 集成分支已含审查通过的 #82/#83、#88 兼容切片 `080104f` 和 #84 共享完整点 `d8fb141`（merge `31bb112`）。#84–#87 逐敌工作树尚未提交或合入；独立审查状态逐项列明，局部通过不能当作最终包事实。
- #84：主工作树的真菌、Byrdonis、Effigy、完整战前/胜利点、Colosseum第二场、Kin、BeastCry、五虫、DenseVegetation治疗后开战恢复、真实场景named Marker均已有局部双版通过。Kin真实同move重入、Byrdonis其他Regen来源反例已修到精确来源。Named Marker存在仍不证明整房hitbox、意图、血条、焦点或非零死亡动画。公共恢复正在提取无新怪物依赖的独立审查测试。
- #84 共享完整点：独立干净切片双版各8、关联Vegetation各13、真实三人在局延迟owner与全新三端加载各1通过，Build/Host及错误统计0；旧异步加载污染、胜利重复休整、缺失RNG默认接受均经实际red→green及独立复审。已集成，范围及证据见 [共享点记录](issue84-combat-checkpoint-seam.md)。旧主树未自动等价为已消费此修复。
- #84 暗港分工作树：Gardener双版各1例通过（内部1–4人数×安全/受罚），四原槽/首招/单次History9与同目标其他Skittish来源保原生缩放均通过；日志 `.claude/worktrees/issue84-underdocks/.local/issue84-underdocks/gardener-green-{public-beta,public}.log`。其余暗港规则待实现。
- #85：Ponder/Liquify/Knowledge A8/A9攻击双版各3例通过；Liquify真实沙坑5、原目标两牌堆各4、StatusIntent8，攻击意图与DamageCmd一致。日志 `issue85-hunting/.local/issue85/knowledge-attacks-green-{public-beta,public}.log`；普通/精英开场、蟹、逐玩家选择屏及全恢复仍待交。
- #86：Mecha开场及真实异步重入双版各2例通过；已移除AsyncLocal，缩放例外限确切mutable Artifact的一次调用，同目标其他来源正常缩放。日志 `issue86-hunting/.local/issue86/mecha-reentrant-green2-all.log`；整票其余规则仍未交。
- #87：Aeonglass 初值与真实混合 owner 第11/12/13次出牌双版各2例通过；五状态顺序、每owner重置12与显示事件、空手及9张手牌的原生溢出已证。Wither 特定升级链、实验体/女王及全恢复仍待交。
- #88：黄金罗盘真实候选排除与24种子普通前后对照、Flight 新局拒绝/普通模式保留/直接 Enable 无半session、普通 Flight 保存大厅通过与拼图 Flight 明确拒绝且不改原文件，兼容 filter 双版各3例通过；扩展七文件已通过独立 Standards/Spec 源码审查。控制台实际发现两命令及顺序重置双版各1例通过。全量 Check 已通过（beta434/public436，两版 Build/Host0；日志含故障注入及宿主资源诊断，详见兼容记录）；真实每日抽样、损坏备份恢复与正常多人激活的具体运行归属仍需核对。详见 [兼容记录](issue88-compatibility-implementation.md) 与 [命令参考](issues-82-88-console-reference.md)。

增量中的Build/Host均退出0；错误统计按各票日志及上文明确例外记录，不能统一推为EngineErrors为0。DLL目标/共享beta PCK/真实端限制与下表一致。日志位于相应worktree，本地路径不是可重分发测试资产。

## 当前代码及证据锚点

|锚点|实现/来源与日志|准确已证与限制|
|---|---|---|
|B3|main `f7e915c8d8b13c207171705dcf548435f6ab3dc8`，PR103已合；batch3实现分支 `fda79d2`。主仓 `.local/research/batch82-88/{public-beta-smoke,public-smoke}.log`；原worktree `.claude/worktrees/batch3-20261111/.local/minimal-perf/resumed-final-{public-beta,public}.log`|历史beta403/public405；同代码起点检查，不是本轮重跑。旧xUnit各27/Build的准确提交匹配仍按原交付记录，不能将403/405当新增追猎完成。`docs/batch-3-test-guide.md`负责完整旧范围。|
|A82|`46a2d5b605fe53fd8314bc837234b47bc691ff36`；集成记录 `f754aa2`。issue82 worktree `.local/issue82/check-final.log` beta418/public421；`notice-beta-final.log` 最终显示专项1；`review-enet.log` 双版各2；集成 worktree `.local/issue82/integration-{test,build-beta,build-public}.log` 双版追猎各14/Build|Check退出0、两轴复审通过。beta418早于最终显示修改，补最终beta专项1；Build在修改后。ENet为本机独立Godot进程3原角色/2在场，不是三真实玩家机器；真实G树与输入。|
|A83|`90c7970c16ea160e85d0257c53d7cdaf76233c0e`；集成文档 `2f95862408c7141a0c86269b955626981af284f8`。issue83 worktree `.local/issue83/final-check2.log` beta431/public433；`review-slices-final.log` 双版各11；`network-third.log` 双版各1；集成 worktree `.local/issue83/integration-{test,build-beta,build-public}.log` 各11/Build|最终Check/集成命令退出0，两轴通过；首次全量重复脚本注册失败已经修复，不能引用首轮失败为最终结果。真实ENet Host+Client/3原角色2在场、真实IO/加载；beta整房OpenChest/Proceed，public仅真实collection空Tween/通知。|
|W84|issue84-combat worktree仍基于 `90c7970`，未提交改动；控制器正在串行验证完整战斗点/事件子战斗|已有真菌/Byrdonis部分N通过及真实重抽反例，**尚未整票green/审查/集成**。本地图不给正在变化的测试总数；待84交最终提交及精确日志补入，不能将WIP记为集成事实。|
|W85|issue85-hunting HEAD `2f95862`，未提交首片；`.local/issue85/ponder-{red-public,red2-public,green-public,green-public-beta}.log`、`first-slice.md`|原生KnowledgeDemon Ponder healing source替换，受罚真实Boss治疗20；双版各1，BuildExit0/HostExit0。其余同怪治疗不改。只是首片，不是巢穴完整逐敌/布局/恢复。|
|W86|issue86-hunting HEAD `f754aa2`，未提交首片；`.local/issue86/mecha-red-public.log`、`mecha-green3-all.log`；`.local/tests/{public,public-beta}-last.log`当时摘要|原生Mecha初始化public真red3≠5→双版各1green；1–4 combat roles×安全/追猎，安全3+(人数−1)、追猎5，另目标Artifact2原生缩放。BuildExit/HostExit0，EngineErrors/ModMetadataErrors0。没有授予History断言；AsyncLocal target/applier/no-card源仍需核窄到确切power。不是联网/HP/喷火首招证明。|
|W87|issue87-hunting HEAD `f754aa2`，未提交首片；`.local/issue87/aeonglass-red-public.log`、`aeonglass-history-green-all.log`|public初计数6≠12真red→双版各1green。每owner独立CardsLeft/Display初12；真实History本次presence次数等人数、追猎Artifact0次/安全1次；安全初6/Artifact3+N。BuildExit/HostExit0，EngineErrors/ModMetadataErrors0。**只初值片**，未实现12次触发/reset/五状态牌。|
|H旧|原型 `0ca54c4` CLOSEOUT，后续HEAD `6d0545d`；`../AllMatters_prototype70_continue/prototypes/hunting-boundaries/`|十三组原型问题均有答案及负例，用户原型清单/public界面声明通过保留；不是正式生产逐敌全部已实现。共享beta资源、公/测调度、受控输入、临时Marker等限制不消失。|
|R93/94|#81最新comment6814733380，#93 comment6814738299，#94 comment6814739311；缓存 `.local/research/delivery-audit-issues/{81,93,94}.json`|#93完整奖励库存/回退、#94 Host+2 Clients六阶段×四名单24组合及额外场景**已获用户人工通过**，不可改成未测。仅其分版/设备/指定故障逐项环境归属尚未齐。#91/#92也已CLOSED，旧stub/per-player访问说法已被更新。|

集成为 `integration/issues-82-88-20261112` / `.claude/worktrees/hunting-20261112`，当前已集成范围见文首；main仍 `f7e915c`。远端PR104最近已知只到 `f754aa2`；未推送的本地进展不能记作远端交付。整票完成与否以实施索引明确列出的剩余项为准。

所有本批DLL目标为public v0.107.1/product `59260271157f76a2896f0eab5bc6ea1245d8b314`，beta v0.111.0/product `41cef1ea4657c524aa50e870df009e56337e8c32`。编译依赖RitsuLib0.6.7；用户实际Workshop0.6.10。public宿主共享beta PCK/Spine/替代场景所有权，**不是匹配public正式资源实机**。末尾的last.log可能被后续测试覆盖，优先引用各票独立归档日志。

## V01–V14

|V/主题|已有实现提交及证据锚点/运行入口|当前未验与下一责任|
|---|---|---|
|V01 几何共享编辑|B3正式领域/G树/输入；用户既有多人操作认可。A82/A83已受影响回归|不由新两票推出P/B各三真实端竞争或全部尺寸。88归属旧证据；新增战斗UI由84–87证明不破坏编辑/焦点。|
|V02 自动授权/生成/恢复|B3原生任务/后台/大厅恢复；R94认可保留。S原型模型仅辅助|各幕/预算/七形、生成中退出的分版实机字段未齐；88汇总既有case，不重做访谈/算法。84–87不得引入旧任务污染，受影响才复验。|
|V03 正式网络|B3真实ENet及R94 Host+2 Clients用户认可；A82各2 ENet、A83各1 ENet有端数及缺席原角色记录|P/B正式三游戏端Send/Broadcast、竞争/断线/Host退出逐项归属未齐。84–87新增追猎状态与恢复传播需自身接ENet/正式端结果，88组合。|
|V04 同源完整点/大厅|B3/R93/R94已验；A82 v2Hunting完整/旧v1明确迁移边界；A83半任务整点/完成点及真实LoadCheckpoint→SetUpSaved→MapRoom|不得把R93/94写未测。84–87必须各交实际受罚敌人、玩家/RNG/根房同次战前/奖励点；W84仍未提交，五故障/3存档2在局分版环境由88归属补齐。|
|V05 Ancient整体回退|B3原生奖励/副作用/缺席补领，R93人工通过；A82/83完整Check回归|剩分版两人一人先领、正式大厅环境归属，不重测替代已确认结论。84–87无权新造个人半点。|
|V06 可靠进入/首次进度|B3可靠进入、票列表与商店历史；A82 am_steps真实0/1/40/intMax、非法原子拒绝及ENet Host/Client|A82已覆盖完成根待投票设0，下一房包括首次Boss立即受罚，命令当刻无面具；用户已确认。84–87事件子战/恢复不二扣，88核两/三端失败分支归属。|
|V07 商店|B3库存/交易/动态价，R93库存人工认可；A82免费服务/删牌sender拒绝，真实货架隐藏、Used不伪造；ENet滞后可购物/真正完成后受罚|正式两端滞后、重访/恢复的P/B实机归属尚缺；不是现有库存从未验。84–87不改商店，88组合已有N/G/ENet。|
|V08 根房安全/完成/服务|A82权威RootHunted、最后安全根/事件栈、全队面具、public/beta休息不同出口及真实Heal/RegalPillow/DreamCatcher；A83非奖励任务来源到达/结束才允许完成|正式游戏双端一人滞后/离店/实际问号房型分版归属；84事件根追加/子战继承规则仍待完整端点，85–87消费快照不能从面具/0推断。|
|V09 战斗完整恢复|A82最终地图Boss原生victory hook→真实保存恢复面具不返还，同名不同room不清；A83提供可复用完整点但不是战斗证明|W84仅推进中；84–87每票交怪物/RNG/HP/固定层一次、事件子战与奖励点、实验体计时/女王/沙漏整场回退。完整真实击杀/双Boss/1–4人恢复待对应票，不转交88替写。|
|V10 逐敌/调度/布局|H旧原型十三组及负例；W84局部真菌/Byrdonis；W85 Ponder20、W86Artifact5、W87初12/noArtifact各双版1|84密林暗港、85巢穴、86荣耀、87三Boss均未整票完成。各逐敌1–4输入、nativeEnding死亡、N→G布局1/4人紧凑/宽屏、完整血条/意图/目标及M体验由84–87交，不以素材存在或Marker代签。|
|V11 空箱|A83完整实现/审查/Check；Face/Spoils、sync/async列表奖励、空Tween、真实双端完成/旧任务drain，直接GainGold7/Obtain Lantern穿透负例已执行|beta整房N/G通过；public正式整房/真实机器设备待集中。未知直接支付/未返回Task后台不支持，不能以最终清列表说全拦截。84–87无此实现债。|
|V12 UI/教程/输入|B3正式树/Presentation/Feedback/Recovery；A82真实hover N/1/0及地图5秒解除生命周期、真实休息/商店引擎输入；A83真实空箱G|U8逐行见下。真实720p–4K三比例/长译文、设备字段未齐；84–87新增敌人/选择屏/焦点/布局需自己的G。实体手柄保后续具设备者责任，不要求当前用户购买/不冒签。|
|V13 换幕/终局|#79已CLOSED，用户幕转换/双Boss认可；B3LaterAct/A10，A82新幕清状态与Boss hook|正式P/B完整标准跑局/A10实际击杀细分未齐；84–87尤其87补真实Ending→奖励→新幕及双Boss首/末场，88组合public内部WinRun与beta独立WinRun，不以换幕源码证明全内容兼容。|
|V14 兼容/许可|现WingedBoots仅Neow局部排除；兼容只读方法体已核；资源来源研究存在|GoldenCompass实际Tezcatara候选未过滤、Flight新局/恢复/大厅未明确拒绝。88后续实施最短清单见末；84–87各新资源NOTICE/来源/许可范围待交。最终同提交双版包/权限/Mod限制/具体第三方清单尚未签。|

## S六类：逐行保留责任

来源 `../AllMatters_prototype68/prototypes/coop-stage/CLOSEOUT-PROPOSAL.md` §四，用户已确认的决策收口不等于该历史表一直“正式未测”；最新B3/R93/R94及A82/A83覆盖必须向前合并。

|S/映射|已实现及证据|剩余/84–87输入|
|---|---|---|
|S1 真多人传输认证断线→V03|B3/R94实际ENet与用户Host+2Clients；A82/A83新真实ENet上述数量|分版正式三端细分；84–87新增状态/完整恢复的真实传播，不能用combat角色数代端数。|
|S2 原存档大厅完整阶段→V04|B3同源五故障/完整大厅；R94六阶段×四名单24及额外人工通过；A82Hunting、A83任务完整端点|各战斗票新增端点及分版故障归属。历史原型80观测/适配40匹配只证明来源，不能覆盖新怪物。|
|S3 Ancient共同继续→V05|B3完整原生奖励/R93人工确认，缺席补领/图保留/整体回退|88补P/B环境归属；84–87不得改变奖励边界，不以旧原型RedMask/金币代签。|
|S4 原生进入计步重访商店→V06–07|B3真实入口/库存/R93认可；A82root/steps/ENet滞后、服务与真实G|84–87事件内部不增步、战前重开不二扣；88归属正式失败/滞后流程。|
|S5 正式过场任务提示→V02+12|B3正式后台/输入/恢复，用户过场多人认可；A82新提示生命周期G|84–87选择屏与新战斗UI回焦；全部设备/分版字段集中补，非重做已验设计。|
|S6 追猎完整恢复面具逐敌→V08–10|A82生命周期完整，A83非战斗完整；W84–87仅局部|84–87每票完整战前/奖励点、逐敌、真实Ending/调度及布局仍未交；88只组合不可兜底实现。|

## H十三组：不以首片冒充整组

来源H旧CLOSEOUT的原13组。以下“旧”均保留原型N/用户通过及其限制，不是正式集成验收。

|H/映射|已有正式输入与旧证据|尚缺与责任|
|---|---|---|
|H1 全场景追加/布局→V10|旧每版1440工厂/1080视觉，缺Marker/越宽/相交负例、五虫210px人工通过；无本批完整布局交付|84五虫/墨宝等、85族母/异鱼等、86Mystic、87火炬；完整1/4人紧凑/宽屏/目标G及正式端。|
|H2 真菌兽/术士→V10|W84真菌部分N；旧生长/HP39与40/六随机/伤员/死亡Ending|84补整票孢子真正末敌Ending与恢复；86Mystic CAST→HEAL→BUFF及六种独立选择/目标完整N；未接入素材不算通过。|
|H3 第一二幕逐敌→V10|旧逐项原生TABLE/COMPANION等；W84局部Byrdonis|84密林暗港整表；85巢穴普通精英整表/固定层/隐藏状态/追加；各自完整恢复与布局。|
|H4 无厌/知识恶魔→V10|旧三轮逐玩家选择/展示不入堆/弹层释放；W85仅Ponder真实受罚Boss heal20双版各1|85补无厌完整动作、知识每玩家选择/入堆/攻击/意图、实际G弹层关闭恢复焦点及全恢复。|
|H5 第三幕/实验体→V10|旧喷火版本差异/领地/连枷/复生；W86仅Mecha开场固定5|86补首喷火/各招、领地/连枷新牌；87实验体二阶段Skittish10/Suck3、3+N×9/10、真实第二复生完成才第三阶段计时3/6/9；public/beta只启动一次。|
|H6 女王→V10|旧6意图×1–4人实际死亡/当次不行动/次次攻击10/激怒+2及用户流程|87尚无生产规则：防止死亡不触发、一次Rekindle覆盖、火炬Minion/HP50/attack10、存活+2不补死者、布局及恢复。|
|H7 沙漏→V10|旧实际12次出牌五状态/满手/Wither强化；W87仅每owner初12/Display/无Artifact真实History双版各1|87补混合owner11/12/13、触发顺序Wound/Burn/Void/Dazed/Wither、reset12/显示事件、溢出/生成升级链、实际战斗/恢复。|
|H8 HP固定值Fatal→V10|旧1–4HP一次/固定Artifact/普通Minion/Fatal/胜利；W86Artifact5且其他目标仍缩放|84–87分别交每敌HP一次/各固定值、真实命令历史与原生Ending，指定Minion不得扩大；W86AsyncLocal源收窄尚待后续。|
|H9 商店→V07|A82完整服务拒绝/原库存Used/ENet滞后；B3/R93库存已确认；旧内存实验/退出泄漏负例|88正式双端/分版实际重访归属；不再写成库存从未保存；84–87无新增责任。|
|H10 休息→V08|A82真实Heal加成附奖/额外次数/禁项无历史/无合法项引擎Proceed，版本出口区别|正式遗物禁Heal组合、映射/设备及双端完成归属；84–87不得以public Exit立刻完成推全员完成。|
|H11 空箱→V11|A83整票完整，真实direct pay穿透/Owner实际入库负例、Face/Spoils/空Tween/双端drain|public整房正式资源/真实机器与输入；未知第三方维持未支持，不要求84–87补通用取消。|
|H12 安全面具Boss→V08|A82当前root独立快照/最后安全/唯一面具/最终Boss奖励前清及恢复；A83非奖励完成|84事件子战同root/85–87战斗消费与完整奖励点，87双Boss/同名事件Boss不能误清；真实全场击杀M待联合。|
|H13 HUD→V08–09+12|A82真实topbar hover与地图Label生命周期已接权威领域；旧只是示例四态限制已由A82部分更新|84–87能力/事件/UI不得误报；88三比例/端点/长译文/真实设备及完整终局解除句，不能旧HUD原型替代全部生产UI。|

## U八组

来源 `../AllMatters_prototype71/prototypes/jigsaw-tutorial-input/REQUIREMENTS.md` §9，用户整份要求/B方案已确认。以下不把已确认设计当新问题，也不把模拟Gamepad当实体设备。

|U/映射|已有实现/来源|未验与新票责任|
|---|---|---|
|U1 首次六句/档案续读→V12|B3正式教程/本机档案与Presentation/Recovery；用户既有过场认可|分版真实退出续读/旧跑局不倒退的环境字段由88归属；84–87不改教程共识。|
|U2 不同阅读进度/帮助/共同阶段→V12|B3多人/正式输入，R94多人恢复认可；U旧用户要求已确认|88集中正式端阶段关闭/有效焦点归属；新知识选择屏/战斗UI85–87不得加阅读屏障。|
|U3 布局尺寸长译文非颜色→V12|B3真实G、用户部分720p/1080p反馈；A82新原生货架/休息G|三比例16:9/16:10/21:9、1280×720和3840×2160端点/长中文替代译文完整未齐；84–87新布局必须G覆盖1/4人紧凑宽屏。|
|U4 右键中键/错误双5秒/几何→V12|B3正式输入/Feedback；A82地图解除句实际5秒后空|88旧case逐条关联与受影响复验；84–87新窗口不吞输入/不穿透。不能仅测试callback代引擎输入。|
|U5 手柄遍历映射重绑视区焦点→V12|B3正式焦点/映射自动检查，U旧模拟标准Gamepad+B方案确认；A82Back/原生Cancel宿主边界|84–87新敌目标/选择屏必须回焦与可点G；88双版重绑/任意布局自动检查。实体手柄保持未来具设备验收者专项未验，不要求用户补设备。|
|U6 自动等待失败恢复→V02|B3真正Godot后台/恢复，R94相关阶段已人工认可；S旧Promise不替代正式实现|88核各输入与失败出口现有case；84–87若新增等待，仅对受影响任务/提示复验，不冒称旧Promise证明新战斗。|
|U7 节点脉动圈选分歧→V12|B3正式探索/地图与用户多人操作，首次进度/商店消费者N；A82root/提示增量|88归属普通/Boss/Ancient/已访及问号商店可走与图标，分歧不重抽；84–87不应修改拓扑来适配战斗。|
|U8 额度安全追猎BossHUD→V08–09|A82 N/1/0、root安全/全队面具/按时Boss静默/末Boss5秒句、JSON/完整点/ENet；用户am_steps完成根设0边界已确认|84–87整场能力/恢复/实际末Boss与双Boss，88联合实际端/完整终局。不能把“本房0仍安全”误用到完成根命令0的下一房。|

## 两遗物 / Flight：首轮审计实施清单（已由文首增量部分落实）

1. 从最终可用集成tip实施。`JigsawNeowRewards`在有效拼图session范围排WingedBoots+GoldenCompass，保原false；仅改IsAllowedAtNeow仍漏真实Tezcatara。对双版已确认的私有`get_OptionPool3`，在绑定有效session的Ancient owner下过滤本次新列表GoldenCompass，保持其余顺序及原生三次抽取、不先抽再重抽。普通模式四项/同seed同RNG不变；真实GenerateInitialOptionsWrapper与获取前边界red→green。
2. Flight检查实际state.Modifiers，在Enable任何创建/附大厅前明确拒绝组合；Restore确认拼图payload/marked后、validating早退前拒绝，普通Flight存档早退保持。无半session、不改原文件、不清标记、不偷偷降普通局。保存大厅canonicalization也必须测，不能只测Enable。
3. 普通Custom/Daily/preferences开关两值、GoodModifiers/日抽样、Flight.ShouldAllowFreeTravel保持；正常单人/真实ENet拼图回归。源API已在`issue88-compatibility-approach.md`定位，无需本轮重复全API。实际实施时按规范在该工作树核精准目标。

## 仍需交付的整体事实

- #88硬依赖#79/#81/#83/#84/#85/#86/#87；#79/#83已具相应旧/新证据，#81仍OPEN是正式分版矩阵/归属未齐，**不能等价成93/94未实现或人工未通过**。84–87未完不能签88最终验收，也不阻止本只读整理。
- 各84–87必须交自身完整战前/胜利点、逐敌/专用布局/调度、来源NOTICE与实际版本；不是88以几只代表怪补上。当前W85–87首片未提交，W84未提交，均不可计最终包已含。
- 从最终审查的同一实现提交生成两版包，附普通Markdown命令/版本/人数/资源/退出码/未验。旧`dist/batch-3-integration`不代表最新性能或本批实现，当前82/83包也不代表84–88。
- 新Mystic/TorchHead/Tender/Spore/Mask等逐来源核对；Balance固定来源与转换成功仅是输入，不自动签全部游戏美术再分发。既有安全要求保留，不擅自联系作者。
- 保留正式public完整PCK/真实游戏端/实体手柄各自缺口；共享betaPCK、独立DLL、1–4 combat roles、ENet本机进程分别标注。用户不重复已确认93/94原验收，只集中补新增追猎、分版/人数/设备/故障归属。

来源主文：`implementation-82-88-delivery-audit.md`、`issue88-compatibility-approach.md`、`batch82-88-boss-delivery.md`；集成`docs/issue{82,83}-{hunting,treasure}-implementation.md`及`issue82-review.md`/`issue83-review.md`（实际文件名见锚点）；交接固定`8efe31c`的`docs/research/v0.1.2-verification-handoff.md`本地`.local/research-verification-handoff.txt`；S/H/U上述原始文件。所有“通过”是既有记录/日志读取，不是本轮重新执行。
