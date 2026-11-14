# #82–#88 当前调度看板

更新：2026-11-14。唯一维护者：集成主控；集成树`AllMatters/.claude/worktrees/hunting-20261112`，分支`integration/issues-82-88-20261112`，源码基点=`5d127b0`。目标为public0.107.1 / beta0.113.0 / RitsuLib0.6.11。源码切片集成不代表整票关闭；本板不推断GitHub实时状态。

**玩法在制2/2（交接冻结）、Ready1、技术诊断暂停1。** 用户另行授权依次实施复盘的六项开发加速改进；不恢复下列玩法片，两片源码与暂存区按原记录保留。

## 复盘改进实施（2026-11-14）

六项已按依赖顺序由六名实施者完成，独立树、双轴审查、正常 hooks、串行快进集成；当前无改进片在制，收口执行者为主控。这组环境改进不签收 #82–#88。

|顺序|改进与结束条件|状态/证据|
|---|---|---|
|1|共享原生宿主生命周期，Vantom/Crab 两个真实消费者保留原 oracle 并通过相关双版验证|Integrated：`ce9f61d`；实施树 `retro-host-20261114/.local/retro-host/summary.txt`，双版各冒烟1/Vantom8/Crab4，Build/Host/Engine/Metadata全0；独立双轴0发现，正常 hooks，审查patch与提交一致|
|2|固定串行执行角色，一次委托包含故障分类、归档和可继续验证范围|Integrated：`205b8ac`；独立双轴通过（去重P3已复核），正常 hooks；实施树 `retro-executor-20261114/.local/retro-executor-review/` 保存补丁/检查|
|3|扩展现有 Test 自动归档、结构化摘要、可选严格诊断，并验证失败传播|Integrated：`b837a61`；实施树 `retro-test-tools-20261114/.local/workflow-last-copy-fix.log`，fixture整套通过，真实严格冒烟双版各1且四诊断0；双轴通过、P2修复已复核、正常 hooks；新接口见开发入口|
|4|派发明确未验条目、复用证据及效果/布局边界|Integrated：`68cdf15`；独立双轴0发现，正常 hooks，实施树 `retro-value-20261114/.local/reviews/retro-value-20261114/` 留证|
|5|新任务精简上下文与身份，首屏位置/角色/改动/下一步/权限|Integrated：`fafbbb5`；两文档净减133行，独立双轴通过（字段去重已复核），正常 hooks；实施树 `retro-context-20261114/.local/retro-context/` 留证|
|6|导航、文档、输出去重；核实 docs-only formatter 原因后做最小修复|Integrated：`7b00eb5`；实施树 `retro-economy-20261114/.local/workflow-economy.log` 整套38 PASS，真实默认 hooks 文档/工具双跳过、C#仍执行并回暂存；独立双轴0发现，正常提交，审查patch与提交一致|

第6项同时修正本机激活：Husky从`core.hooksPath`读取配置；仅集成树与`retro-economy-20261114`使用worktree级`.husky`，common原绝对路径及13棵缺hook旧树均未改，`extensions.worktreeConfig`原已开启。恢复记录在实施树`.local/hooks-config-before.json`；新树准备见[开发入口](development.md#并行实施与-worktree)。持久C#日志证明执行/回暂存；实际格式化前后文本仅在当时工具断言，不补造采样日志。

收口复核：原Knowledge源码仍暂存672+、无源码未暂存差异；Matriarch仍暂存949+、测试另4+/2−、生产文件未跟踪，HEAD各为`f1eb1dd`/`eb4837c`。本轮未重跑原玩法片或最终交付Check；宿主相关双版证据复用第1项，工具最终fixture复用第6项，不把开发环境改进冒称玩法/人工验收完成。

## 活跃与就绪

|卡片/issue|行为|状态|owner / worktree|依赖与热点|下一动作/证据|开始时间|
|---|---|---|---|---|---|---|
|C85-knowledge-owner / #85|两真实owner首轮错峰选择、等待/归属/屏幕释放及原下一招|Verify/beta绿，交接冻结|`glory_layout` / `knowledge-owner-20261114`，基`f1eb1dd`|新KnowledgeMultiOwnerTests/Hello；生产和公共宿主只读|最新entry9-beta 1PASS四诊断0、child错误流空；暂存672+未提交，public及相关组合/双轴/hooks/集成待；退出问题及下一命令见上方当前交接和#85实施记录|2026-11-14 拉取|
|C84-matriarch-layout / #84|追加睡眠Lagavulin睡/醒完整显示与原生输入|Verify/多人oracle返修未完，交接冻结|`matriarch_fresh_prepare` / `matriarch-layout-20261114`，基`eb4837c`|新MatriarchLayout生产85行未跟踪；测试/Hello暂存949+，测试另4+/2-未暂存|真实几何red后专用整树平移，awake3-beta1四诊断0；全组beta两例PASS后roles4固定Plating断言失败，public未跑；Plating公式已改未测，Metallicize公式待修；下一最小roles4，见上方当前交接/#84记录|2026-11-14 拉取|
|C87-aeonglass-ui / #87|正式本地个人counter图标/数字/真实hover随12次出牌与五状态溢出更新重置12|Ready|待分配，未建树|新AeonglassUiTests/Hello；默认tests-only，无新增怪物不改原布局|brief `.local/batch-scope-audit/aeonglass-ui-next-slice.md`；双版NPower/Container真实消费链已核，本地四模型仅显示本地Target；先实际PCK tooltip，不推断原Amount6已适配，双版各2，复用模型/fresh证据|2026-11-14 准备|

### 2026-11-14 15:49 主控停止交接（当前入口）

接手先核实际Git状态，再读目标卡及逐票在制记录；不依赖旧会话agent继续工作。最近检查没有本批次Godot/dotnet/ilspy/Test.ps1进程或待回收执行session。日志在**各实施树**的`.local/executor-knowledge-owner/`和`.local/executor-matriarch-layout/`，不是集成树同名目录。

- **Knowledge先收尾**：跑public同例，再双版ActTwo8/Tender1、独立双轴/正常hooks/串行集成；准确分支、命令、退出修复和未证范围见[Knowledge在制记录](issue85-hunting-implementation.md#knowledge-双-owner-错峰选择在制交接2026-11-14)。已有entry9-beta证据不因交接重跑。
- **Matriarch继续精确oracle返修**：先核Plating未暂存差异并修Metallicize，再最小roles4；命令、双版API和完整矩阵见[Matriarch在制记录](issue84-underdocks-implementation.md#matriarch-正式睡醒布局在制交接2026-11-14)。最终审查纳入未跟踪生产文件，不放宽几何或输入oracle。
- **Aeonglass保持Ready**：brief`.local/batch-scope-audit/aeonglass-ui-next-slice.md`，尚未建树。先使一个在制片流出，再测真实本地图标/个人12计数/tooltip；只加效果、不增怪物，默认保留原布局。

帝皇蟹只加Artifact3，不加怪物；旧移动方案已撤回归档。原贴图/坐标/目标/可见HP与共有交叠的tests-only对照已集成，双版各9 PASS。各实施树`.import`导出脏项和主仓旧删除/未跟踪内容均保留。

## 暂停与等待

|卡片/issue|当前事实|恢复条件/证据|
|---|---|---|
|P87-boss / #87|原六项2绿1同症状红；随后3次probe绿未捕获原错，probe已撤回，未修复；保留`boss-warmed-callback-20261113`树|有能区分现有假说的新观测/实验或新失败后限定预算重启。诊断在**主仓**`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`；历史开始时间未计量，不盲跑原组|
|W88-delivery / #88|当前源码完整Check、同提交双版包/许可和集中人工矩阵待|适用剩余行为及矩阵明确后按[开发入口](development.md#实机验收交付)执行；[最近全量基点](issues-82-88-implementation.md#完整检查与已知诊断)不能代签当前HEAD|

## 最近集成

|切片|实施→合入|集成证据|
|---|---|---|
|Waterfall完整fresh|`2fb2b0a`→`eb4837c`；文档`5d127b0`|双版各8 PASS、四诊断0；[暗港记录](issue84-underdocks-implementation.md#waterfall-复生爆炸态与非空奖励完整-fresh2026-11-14)|
|帝皇蟹原场景保持|`4e4bf4c`→`f1eb1dd`；文档`aab249c`|双版各9 PASS、四诊断0；[巢穴记录](issue85-hunting-implementation.md#帝皇蟹仅效果强化与原场景保持2026-11-14)|

其余集成范围与证据从[实施索引](issues-82-88-implementation.md)到达逐票记录，不在看板复制全历史。

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

## 更新方式

派发、返修、审查、暂停和集成时只改对应卡，沿[批次流程](agents/batch-flow.md)控制最多两片WIP、唯一工具链执行者、独立双轴和串行集成。Ready拉取顺序按验收价值/依赖/热点决定；卡片数不换算完成百分比。真实人工验收仍归各issue，集中安排，不从自动化PASS推断人工通过。
