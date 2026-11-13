# #82–#88 实施索引

日期：2026-11-12。本对话主控集成，开发起点 `f7e915c`，集成分支 `integration/issues-82-88-20261112`。

## 当前接续状态（2026-11-13）

当前源码集成 `1956c17`，目标 **public v0.107.1 / beta v0.113.0、RitsuLib 0.6.11**。本节覆盖下方历史记录中的旧目标、资源缺失、未提交状态和旧失败位置。旧发布包与旧版通过数不代签新目标。#82–#88 仍OPEN，#84–#88仍部分实现；当前提交完整Test/Check、同提交双版包及正式实机矩阵尚未交付。

Holes完成片 `1956c17` 补全已领／Proceed后全员真实 `NMapScreen.Open(false)` 认证→原生host SaveRun→第三轮全新三端 LoadRunLobby 自然 MapRoom。父事件完成与根完成保存分开：无退出／仅两端退出不提升，全员完成才投影保存副本，重复保存根只计一次且live投票根保留。expected按阶段及端点/profile归属核对完整Players／RNG／history／Page／Point／Map；固定host名单 `[1,3,2]` 覆盖保存副本顺序。实施树双版event-各4 PASS、宝箱共用恢复各1 PASS，四诊断均0，日志 Holes 树 `.local/executor-holes-completed/ordered-event-final-{public,public-beta}-host.log`、`treasure-final-{public,public-beta}-host.log`。独立双轴无阻塞，正常hooks差异无漂移。裸地图节点调用真实入口、显式native保存并隔离IO；正式输入／自动UI保存／部分领取恢复未证，详见[Holes完成片](issue85-hunting-implementation.md#holes-全员退出后的完成点保存2026-11-13)。

集成树同源码 `1956c17` 串行复验双版 `event-` 各4 PASS，Build／Host／Engine／Metadata全0；实际日志 `.local/executor-holes-completed/integrated-event-{public,public-beta}-host.log`。完成点目录 public `b6ef480e-09d6-40b9-9bd3-49e09c0e05ec`、beta `dc22b61a-bfbc-4f44-966f-5f206f7dfda2`，各三轮九份.err为空、第三轮三端均 MapRoom／opening=0／rewards=0。

`f9ccd3c` / `e088feb` 已集成 Holes 真实 ENet 三端战前保存及全新三端 LoadRunLobby 恢复，只有测试与既有 recovery 分派改动。混合票不执行、一致票等待第三 owner 的真实 Chosen 任务及最后 gold/event/run RNG 效果后才捕获，随后完整比较玩家、run/owner/monster RNG、敌人、父页、检查点、根房和 history；开场恰一次、旧锁定动作不能重放。原生首次入场会在 checkpoint 之后用 SyncPlayerDataMessage 合并远端 discovery，因此恢复 Players 预期取独立 pre-opening 完整采样，另对首次同步变化作完整比较；新大厅 roster 按既有 LoadListener 在入房前已完整核验的顺序比较，不排序或删除数组字段。两轮独立 Standards/Spec 均0剩余发现，正常hooks后差异无漂移。同 `e088feb` 在集成树运行 filter `Jigsaw recovery native load lobby event-`，两版各2 PASS（旧 event-combat-lag + 新 event-holes-lag），Build/Host/Engine/Metadata均0；日志集成树 `.local/executor-holes-integrated/event-pair-final-{public,public-beta}-host.log`。后续奖励切片见下段；正式UI/真实客户端仍待 #85 后续。

`31e849a` 已串行快进集成 Holes 三端胜利奖励保存→全新三端恢复测试，仅原两测试文件328+/16-，无生产补丁。合法点来自双版原生自动胜利结算、奖励生成之前的实际保存；保存回调前独立采样完整玩家/RNG，原进程生成后的完整奖励和状态作为 fresh 预期。新大厅只采用已独立验证的 roster；领取前独立预测本端首次药水发现记录，保留完整数组比较。真实领取核对金币、原牌组及奖励牌身份/升级、药水与重复调用拒绝，真实 Proceed 后仅允许父页完成和已证四个 Page 清理字段改变。实施树同源码双版 `event-` 组合各3 PASS（旧事件战、Holes战前、新奖励恢复），Build/Host/Engine/Metadata全0；日志 Holes 树 `.local/executor-holes-reward/event-final-{public,public-beta}-host.log`。独立 Standards/Spec 均0发现，正常hooks前后提交差异与已审内容一致。领取后完整跨端比较是一致性证据，不扩大为所有领取副作用的独立预言；该片当时未覆盖完成点第三轮 fresh，新增完成片见上方 Holes 完成片。正式UI/真实客户端、完整Check及最终包仍未交付。

集成树同 `31e849a` 串行复验相同 `event-` 组合，public / beta 各3 PASS，Build/Host/Engine/Metadata全0；最终日志 `.local/executor-holes-reward/integrated-event-{public,public-beta}-host.log`。奖励目录分别为public `5bda2fe6-1044-4435-8980-4c96945df0f1`、beta `2a588df1-0b24-44c0-9f1f-a54de92fe69a`，各三capture+三fresh、六.err为空。

`01b5096` 的 Wither 原生强化链四例已集成并有双版各4 PASS专项证据。Boss 原组六项本轮保持原 runner/资源/环境，预算内三个新进程：两次各6 PASS，第三次前五 PASS 后在第六项第一段 Phrog HP→Kill→Boss:44 抛原异常；本轮1/3同症状失败，不能估计稳定率或称已修。三次均0 warning、约1.2秒增量构建，旧警告差异不足以解释红绿。日志 Boss 树 `.local/executor-boss-warmed/reward-window-original-{1,2,3}-public-host.log`；原条件预算停止；随后一轮 mode/tracker/Notify 内存观测按最多三个新进程执行，三次均6 PASS、四诊断0，但未捕获带探针原错，不能称修复。首Phrog Kill窗口均8对 Notify prefix/finalizer，HP listener为当前tracker、_state为当前combat、owner匹配，mode按true→false→true；这些只描述成功样本。日志同目录 `mode-probe-{1,2,3}-public-host.log`，探针副本/diff已归档并精确撤回，没有新增HP detour、delegate包装或JIT变量。当前诊断见主仓 `.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`；本轮没有 Boss 生产改动。

`8869289` 已集成受罚 Holes 单 owner 完整检查点：真实共享 AM_FIGHT 战前回退、胜利奖励点重载与实际领取、Proceed/Resume 父事件终结；保留完整玩家、run/owner event/monster RNG、初始敌人及根房历史，旧父页动作实际不可重放。新增7例还覆盖真实连续两次回退、安全根本次扣至0、四种非法 parent/encounter/resume 组合及受控真实 pending-option 正常完成/同 entry 换 map 拒绝。后者先双版复现旧任务写入替换地图，再以地图引用检查最小修复。独立双轴及增量复审均无剩余发现，正常 hooks 仅排版。合入后同8869289每版 Holes14+Checkpoint8+EventCombat4，共52 PASS；六跑 Build/Host/Metadata均0，Holes每版各1条预期旧请求拒绝 Engine 诊断，其余四跑 Engine0。日志兼容树 `.local/executor-holes-integrated/holes-integrated-{holes,checkpoint,eventcombat}-{public,public-beta}-host.log`。其后的三端战前恢复见上段，三端奖励恢复新增证据见上段；单owner奖励测试显式调用 CompleteCurrentRoom 模拟宿主缺少地图UI的退出边界，不代签正式UI全链。

`c469b9a` 已集成 Waterfall 原生 Trigger 后清可见当前负面、固定新增 Artifact1，并保留原 Steam/AboutToBlow/Explode 链；真实遭遇是 Underdocks 的 `WaterfallGiantBoss`，不是 Elite。独立双轴0发现，双版各6项专项及同提交受影响组合 public19/beta18通过，专项 Build/Host/Engine/Metadata均0；初次致死自动 Trigger、整场恢复和正式布局仍未证。`070a0ae` 已集成 Hive 事件普通开场：匹配父事件及已接受受罚 Unknown 根的子战统一 Weak/Frail/Vulnerable各1，顶层精英仍Weak2；真实 AM_FIGHT 同步器、原生开场及下一完整回合、1–4combat角色/Artifact0–3和跨await失效/异常已验证，独立双轴0发现。合入后同070a0ae两版各 Hive10+ActTwo8+EventCombat4，共44项通过，六日志四诊断均0，位于兼容树 `.local/host-20261113/hive-integrated-{hive,acttwo,eventcombat}-{public,public-beta}-host.log`。两版自然衰减保留：public跳首tick，beta记录三条-1；不通过改生产统一。连续独立事件战、Holes战前/奖励恢复、正式UI及真实多端仍属#85后续，不能以既有Colosseum/Vegetation恢复测试代签。

匹配public资源及辅助依赖已备齐，位于主仓 `.local/public-resource-recovery-20261113/public107-content/`。辅助依赖来自本机匹配文件及官方Sentry5.0.0 NuGet net9.0，临时复用已有官方depot manifest校验；未混用不匹配beta Sentry、未新增仓库校验清单。`8a69527`将DLL/PCK/Spine绑定所选目标并打印来源，修正beta首房ArcaneScroll夹具，保留真实副作用及Player/整组RunRng回滚重放。原交接在工作区父目录 `AllMatters_docs/handoffs/`；各树docs实际为普通目录，不自动同步。

同提交 `df6a1f9` 已完成匹配资源双版全量：public509 / beta507 PASS，Build / Host / Metadata均0，EngineErrors分别25 / 26，不能称零错误；此全量尚未在后续 Waterfall/Hive 提交重跑。`df6a1f9`仅对ENet winner证据发布遇Win32共享/锁拒绝32/33作20ms等待、500ms有界处理；临时文件写一次、原子Move不覆盖、其他IO立即失败及原网络超时保留。真实自然red与锁注入红绿、双版各3文件边界和7个真实奖励/故障场景均已验证，独立双轴0发现、正常hooks范围无漂移。public日志 `public-full-df6a1f9-host.log`、beta日志 `beta-full-df6a1f9-host.log` 位于兼容树 `.local/host-20261113/`。当前宿主已健康，可继续未完切片，完整项目Check和最终包仍待。

诊断分类仍分别保留：public18条坏档/名单保护负例+1worker注入，其余视觉缺类1、managed action pop2、退出资源3；beta19条坏档/名单保护+1worker注入+1CursedTome原生多人场外致死保护，其余视觉缺类1、treasure EmptyTween的NTreasureRoom.EnableSkipAfterDelay→ActiveScreenContext.GetCurrentScreen NRE1、退出资源3。CursedTome保护阻止死亡并恢复1HP，不能写成已真实杀死。详细逐行记录在主仓 `.local/main-control-20261113-resume/{public,beta}-df6a1f9-diagnostics.md`。以下失败位置保留为历史进展，不覆盖本次全量结果。

本轮已审、正常hooks提交并集成的增量：`c81a63b`按版本选择真实商店场景，双版各1；`2f2f8d5`修public Phrog第四槽右侧内缩，测试/阈值不改，双版全组各7；`6d84776`实现Hive精英Weak2且普通三项各1保留，双版各2；`9f6a896`经`f4056c3`合入MultiClaw基伤9/A9+10，保留原生3+N/目标/Strength/Suck/第三阶段动作，并修无Jigsaw空acts的helper读取顺序，双版各4。以上均独立Standards/Spec无未解决发现，专项Build/Host/Engine/Metadata均0。1–4combat角色不代表正式客户端；beta Dread不属于当前Standard拼图入口，本片不扩展支持或改原生4+N。

public匹配资源全量先后在367项后商店场景、424项后Phrog几何、487项后TestSubject启动Task捕获处失败。`62c49de`只修测试观察：在真实caller原调用后观察同一Task，保留CTS/StartCombatInternal/RunSafely及全部两复生、九回合、3/6/9断言。public专用两Phrog前置有真实red/green，Boss专项public6/beta5，诊断0。独立probe已确认capture为空时原生coordinator实际round1/hand5，不能写成原生战斗未启动。更广beta混合UI/backend前置的不同订阅异常仍未解决。

随后public全量在496项PASS后Host1退出（Build0/Engine25/Metadata0，无最终通过汇总），新失败是ENet子进程stdout回调AppendAllText的文件占用IOException，历史句柄占用者未知。`c8f3bb1`改为双流立即持续读取、await终结后保存证据，原同步/奖励/非零退出断言保留；独立双轴审查0发现，双版各3个进程边界用例及7个真实ENet奖励/故障场景分别通过，专项诊断0。读句柄故障注入只证明原回调IO/输出丢失链，不冒称重现历史host崩溃。同提交public全量随后在504项PASS后Host1退出（Build0/Engine25/Metadata0，无最终通过汇总）：第6个奖励场景automatic failure的子进程3在 `JigsawNetworkTests.cs:211` 将 `3.winner.tmp` 重命名为 `3.winner` 时遇共享拒绝，Host继而等待all callbacks超时。旧输出回调IOException未再出现，完整子错误已保存；新的句柄占用者仍未知，不能认定与旧日志问题同源。

beta全量495 PASS是新增RunRng断言与本轮切片之前的检查点，Build0/Host0/Metadata0；新增断言及切片已有双版专项。该轮EngineErrors26已分类：20条恢复/损坏注入等负例、CursedTome联机致死1、TeaTrial视觉脚本1、treasure emptyTween NRE1、退出资源3，不能写零引擎错误。最新public失败日志为兼容树 `.local/host-20261113/public-full-c8f3bb1-host.log`；上一轮日志为同目录 `public-full-f4056c3-boss-observer-WIP-host.log`，专项及原始红绿也在该目录；上一轮为已审同内容WIP，正常hook后patch一致；最新一轮直接运行已提交c8f3bb1。主控审查/资源来源记录在主仓 `.local/main-control-20261113-resume/`。

## 兼容迁移历史与已集成证据

2026-11-13 主控接续：交接时本地 `08b2e89`，保留两个既有文档更新及暂存的 Boss 测试启动修复。该修复的独立 Standards / Spec 审查均无发现，尚未提交。新批次 `Test.ps1 -Branch All` 在 beta 编译阶段失败（4 项 API 错误，未进入行为测试），日志为集成树 `.local/tests/main-control-takeover-20261113.log`。本机 beta 已更新至 v0.113.0（commit `16ac0fe3`）；用户明确选择调整目标为 **public v0.107.1 / public-beta v0.113.0，先完成兼容迁移**。旧 v0.111.0 的通过数仅为历史证据，不代签新目标。

当前首批安排：`fix/beta-0113-compat-20261113` 独立工作树先迁移 API 与验证宿主；其实施者独占 native / build / formatter。其余代理只读整理后续切片。宿主就绪后优先分派 #85 Hive 精英开场 Weak2 与 #87 实验体 MultiClaw 9/10、3+N；两者分别独占 ActTwo 与 Boss 文件，HelloWorldTests 注册和集成由主控串行处理。每片须重新核对 v0.113.0 API、真实 red / green、双版验证及独立两轴审查；旧草稿不计通过。

2026-11-13 兼容检查点：`1d1dac04792006a262edd31c53139c93b85831de` 已快进合入集成分支，57 文件（602+/163-），含此前已审 Boss 启动修复。最小版本分派保留 public 原API，beta 切至 v0.113.0，完整 RitsuLib 0.6.11 作为独立开发依赖。真实商店迁至 MerchantSynchronizer，保留库存身份/价格/RNG、sender/receiver 限制及原生任务与 ordered stock 收敛前的保存阻塞。测试选择器沿真实 CardSelectorStack 临时绑定跑局、异常后恢复；地图新资源路径仅 beta 条件化。双轴审查及两次增量返修复审无未解决发现；曾发现并真实修复多端测试绕过同步入口，以及商店 FTUE 前过早完成根房的问题。

该提交受影响专项两版各23通过、0失败（Peck1、Light1、Grip1、fungus4、Insatiable1、Flight4、TestSubject3、native shop4、recovery shop4），每组构建/宿主退出0，详细记录在兼容树 `.local/compat-final-checks.json`。beta 另有真实三进程 ENet1、MerchantExit/FTUE1、selector scope1通过。主控合入后 TestSubject 双版各3复验通过，Build/Host/Engine/Metadata均0，日志集成树 `.local/main-control-20261113/integrated-boss-both.log`。人数为宿主角色或原生 ENet 进程，不代签正式多游戏端。

全量仍未通过：beta299项后首次失败于旧 `schema_version` 对象坏档注入。新版原生 MigrationManager 为保护 Steam 云存档，对有效 JSON 的跑局加载失败原地保留且拒绝回退，不能添加 Mod 泛型兜底；下一片仅适配故障夹具并保留安全负例。public先前全量262项后失败于旧资源路径/ENet错误洪泛，匹配0.107.1的完整PCK/原生资源仍缺；用户要求Agent自行补齐，正调查合法本地缓存及可用来源。没有完整 public UI、完整迁移、整批 Check 或正式包通过结论。原工作树/stash/备份保留，未推送、未安装或启动游戏。

需求以 [#73](https://github.com/Endercloud001/AllMatters/issues/73) 和各 issue 最新明确修订为准；实现导航见 [开发准备](issues-82-88-development-preparation-2026-11-12.md)，新增图片与返回怪物的来源见 [素材来源](issues-82-88-asset-sources.md)。本索引只记录分工、证据和未验范围，不另立规则或接口。

## 排程和文件归属

|波次|工单|实施安排|当前状态|
|---|---|---|---|
|1|#82|单一实施者独占领域状态、保存、恢复投影、面具、服务限制及控制台接入；主控审查后集成|已集成 46a2d5b，自动验证和双轴审查通过|
|2|#83、#84|宝箱已集成；#84 的共享完整点先行集成，逐敌与布局分别继续|#83 已集成90c7970；#84 共享切片d8fb141经31bb112合入，整票实施中|
|3|#85、#86、#87|独立怪物/能力/场景文件隔离推进；公共检查点已集成，注册逐次合入|三个已审查部分切片均已集成，整票仍实施中|
|4|#88|对照 V01–V14 / S六类 / H十三组 / U八组汇总正式证据、复测、包和兼容许可声明|独立兼容切片080104f已集成；整票仍待 #79/#81/#83–#87|

波次是避免冲突的实施安排，不增设 tracker 硬依赖。#81 本次仍 OPEN；[最新收尾记录](https://github.com/Endercloud001/AllMatters/issues/81#issuecomment-6814733380)确认实现及自动验证完成，#93/#94 对应人工测试已获用户确认，不能重新列为未验。其余分版实机矩阵、真实两人及故障分支、人数/设备/分辨率逐项归属仍需补齐；#88 最终汇总时分别承接，不把父票 OPEN 解释为已发现功能缺陷。

实施者使用从当前集成提交创建的独立 worktree。集成分支由主控独占；子代理只能提交本票文件。新增公共接缝先与主控沟通；共享文件后续实施者从已集成版本继续。依赖/API/工具可只读共用，本地测试输出、obj/bin/dist 各 worktree 独立。主控协调原生/Godot 测试串行执行，避免宿主全局端口或资源互扰。

沿用已经确认的测试边界：D 领域公开行为、N 双版真实命令/保存、G 正式节点树和输入、M 真实多端。每票红绿切片、双版验证后做 Standards/Spec 独立审查，修复再复验。未执行的真实游戏/设备场景按票列出，用户集中安装启动，不逐票要求安装。

## 验证起点

同一代码起点 `f7e915c` 刚完成 `Test.ps1 -Branch All`：beta 403 / public 405，测试构建、宿主及日志检查均退出0；详细诊断见开发准备 §9。隔离 worktree 初始化后再跑最小宿主检查；实现完成运行受影响双版矩阵及完整项目检查。原型和共享beta资源宿主不代签正式public运行。

## 实施与审查记录

|工单|实施提交|自动证据/包|独立审查|未验范围|
|---|---|---|---|---|
|#82|46a2d5b605fe53fd8314bc837234b47bc691ff36|Check All 退出0：beta418/public421、双版Build；最终beta显示专项1；集成后追猎双版各14及双版Build退出0|[双轴审查及返修闭环](issue82-review.md)，两轴通过|真实游戏/正式public资源/设备/完整终局等见票内记录|
|#83|90c7970c16ea160e85d0257c53d7cdaf76233c0e|Check2退出0：beta431/public433、双版Build；集成后宝箱双版各11及双版Build退出0|[双轴审查与补证闭环](issue83-review.md)，两轴通过|public正式整房/真实机器设备/未知直接支付边界等见实现记录|
|#84|共享d8fb141、暗港936f557及Matriarch281110c、密林2594715均已集成，当前merge2840635|共享点双版各8、Vegetation各13、真实三人在局延迟owner+全新三端加载各1；暗港最新各4，密林接最新基线组合各25。见[共享点](issue84-combat-checkpoint-seam.md)、[暗港](issue84-underdocks-implementation.md)、[密林](issue84-overgrowth-implementation.md)|共享/网络/暗港/Matriarch/密林及追加guard均独立两轴无阻塞；Phrog宿主P2已返修复审闭环，整票待审|暗港剩余规则、其他事件完整恢复、正式全布局与实际输入尚未交付；public56条已知共享资源诊断，不能代签正式资源|
|#85|4641f0f经463f692、Hive普通开场847cc845经bcd4060已集成（部分）|Ponder/Liquify/Knowledge攻击及三轮真实诅咒选择六例，加Hive普通开场三项1/真实History/其他Weak来源，共双版各7通过，Build/Host/错误统计0；[实施记录](issue85-hunting-implementation.md)|原六例与普通开场分别独立Spec/Standards无阻塞项，beta Description目标补证已复核|精英/事件开场/蟹、1–4owner选择/实际G/生命周期/布局/恢复未交付|
|#86|a5ba509经54ebd7e、Flail增量c8b5fdd经d2e7379已集成（部分）|MechaKnight开场固定Artifact5/精确来源重入、首招Flame及原生循环，SoulNexus初始Territorial1，以及Flail原生攻击后全部玩家Tangled1；最新统一双版各5通过，Build/Host/错误统计0，详见[实施记录](issue86-hunting-implementation.md)|四case与Flail增量分别独立Spec/Standards无阻塞项；Flail总伤害进一步细化逐玩家History为非阻塞建议|Mystic仅红测试草稿；完整荣耀普通/精英、布局/恢复未交付|
|#87|4a0c58b经90fe154已集成（部分）|Aeonglass初值与第12次五状态、TestSubject真实两复生/第二次完成后9回合及3/6/9限制，双版各4通过；1–4角色输入，非真实四端；集成后专项尚待运行|当前切片两轴无硬发现；测试异步清理建议已修、静态复审及双版各4运行通过|特定Wither升级链、实验体MultiClaw数值/增长、Queen、旧异步隔离、布局及整场恢复未交付|
|#88|080104f29997eddc5df85ca7b3869cc15d560dfc（兼容切片）|罗盘24种子与Flight新局/直接恢复/保存大厅拒绝双版各3；控制台发现双命令各1；Check All beta434/public436及双版Build退出0；[记录](issue88-compatibility-implementation.md)、[控制台](issues-82-88-console-reference.md)、[41项地图](issues-82-88-verification-map.md)|扩展七代码文件独立Standards/Spec均无可操作发现|完整组合验证、真实每日抽样等归属与最终同提交双版包尚未交付；全量宿主日志含故障注入/资源诊断，不能表述为正式游戏零错误|

2026-11-13 集成增量：#85/#86 两片合并后，`Jigsaw hunting act` 双版各10项通过，Build/Host/Engine/Metadata均0，日志 `.local/integration-slices/acts-{public,public-beta}.log`；#88每日抽样保留测试4aae096经f5d8dde已集成，修复字段序列化漏检后双版各1通过（64种子/实际modifier属性及RNG/非空Flight），日志 `.claude/worktrees/issue88-delivery/.local/issue88/daily-fields-final-{public,public-beta}.log`。共享检查点和兼容合并后各8/3双版通过。最终整批同提交检查/包仍待剩余实现。

2026-11-13 接续增量：#84 暗港独立工作树的 Gardener/Colony/Eel 已双版各3通过（含1–4角色、安全/受罚、同目标独立来源与Artifact顺序），并完成独立Spec/Standards审查，无硬发现；尚未提交。Colony 的20是硬壳，6是覆甲，不是HP固定20。Matriarch 下一测试已按真实Boss与A8生命阈值准备，尚未运行。主工作树 Phrog 真节点测试已复现约2.5秒原生死亡动画后第五槽缺失，并修复受罚场景槽位和旧战斗异步续体隔离；双版各4通过，覆盖1/4角色与1280×720/1680×720的敌方命中框、HP条和意图几何。public宿主有40条已知共享beta资源NHotkeyIcon诊断，不代表正式public资源验收；实际指针/焦点、Infested展示数值及完整文本布局仍待。Vantom实际Boss开场与未受罚对照测试草稿已准备，尚未运行。所有新增状态仍需后续整票集成、布局与恢复验证。

2026-11-13 后续集成：Flail `c8b5fdd`经`d2e7379`、暗港`936f557`经`551d85a`已正常合入，原有Compatibility/Flight/Checkpoint/Recovery/ActTwo/Three/Boss注册均保留；源文件除注册合并外无额外语义差异。上段“暗港尚未提交”和“Vantom尚未运行”为当时状态：Vantom现已双版各3通过，真实受罚独立Boss以`[Inklet,Vantom,Inklet]`开场，未受罚/非Jigsaw/其他Boss对照不追加；原生Slippery人数缩放保留。随后Phrog双版各4回归通过，public资源诊断边界不变。密林24文件已精确暂存进入独立双轴审查；Hive普通开场、Matriarch、Mystic及Queen按窗口继续，未运行草稿不计通过。集成后组合复验尚待执行。

2026-11-13 审查返修：密林Spec未发现当前切片实现错误；Standards发现Phrog宿主清理遗留已释放全局对象的P2。经真实beta反例、精确原NGame/NRun引用恢复修复后，Phrog双版各5通过并完成独立返修复审；beta Engine0，public56条均已知NHotkeyIcon共享资源诊断。同进程正常/断言异常退出、未挂载scope换全局实例及后续原生UI消费者已验证，不能扩写为任意场景切换。密林仍待安全消费最新集成并提交。Matriarch生命切片已真实红绿及双版各4通过，正在双轴审查，左随从尚未实现。Hive普通开场847cc845已合入bcd4060，源码与来源一致；精英开场/Mystic/Queen均仅测试草稿，未运行。

2026-11-13 密林整合完成：完整备份旧index/工作文件后，旧基90c7970正常merge最新集成54a6cc0，再以3way仅应用已审24文件；原七共享文件与Flight检查取新基，Hello保留全部注册。组合测试发现Vantom guard过早读取非Jigsaw无幕run.Act的真实失败，调序先检查有效会话，新增原生生成负例；独立两轴追加复审通过。最终双25通过，Build/Host/Metadata0、无异常/准备失败，betaEngine0/public56全部同NHotkeyIcon。2594715经2840635已合入，两树干净；备份与stash aee7502保留。Infested原生Single图标与当前说明不显示数量，旧“显示4应5”待办缺乏事实依据，已撤回假设，不新增tooltip；实际五虫生成已证。最终整票组合Check及同提交包尚待运行。

2026-11-13 整批检查回归：7d5070a已同步草稿PR104，但advisory推送钩子Check All两版均在旧Ember用例失败，Hive开场guard也过早读取非Jigsaw无幕run.Act。资源/脚本/空白/双版Build通过不代签测试。新增真实无幕Hook负例先同异常red，最小前置有效会话判断后ActTwo双版各8、Ember各1通过，独立两轴均0发现；完整Check仍待重跑。原失败、修复红绿日志完整归档于集成树`.local/integration-slices/hive-guard-regression-20261113/`，不能以旧专项通过覆盖这次全量失败。

2026-11-13 当前本地08b2e89（远端草稿仍3e45694）：Hive guard已提交复审修复；全量顺序继续暴露Phrog测试宿主热调用绕过晚装Notify补丁，以及将物理窗口误当画布的问题。两项分别最小化、真实red→green和独立双轴复审，修复只在测试宿主；Phrog最新双版各7通过，public96条同NHotkey诊断，Auto/Expand模式明确，不覆盖Keep。正确源码full Check beta492通过；public相同首次五卡setup超时重复两次，已转Boss子代理独立诊断，仍不能声明整批Check通过。完整故障、编码恢复、引擎日志分类与证据见[Phrog测试隔离](issue84-phrog-test-isolation.md)。

以下为早期预检来源索引（当时尚待 #82；当前以表格和接续增量为准）：本机 `.local/research/issue83-implementation-approach.md`（宝箱原生开箱、任务及空 Tween）及 `implementation-82-88-assets.md`（已核实的用户素材与 Balance 固定版本资源路线）。另派只读调查核实宝箱异步任务/完整保存点及 #84 的完整战斗表；这些记录描述预检阶段，不撤销上文已经集成的 #82/#83 和共享检查点。

只读预检补充：`issue83-checkpoint-seam.md` 记录空箱异步任务、半份任务保存及全员完成待投票的恢复风险；`issue84-implementation-approach.md` 逐行映射密林/暗港表，`issue84-event-checkpoint-seam.md` 细化 Colosseum 第二场等事件战前点；`issue85-implementation-approach.md` 映射巢穴表、知识恶魔原生选择及固定层人数反例。均在本机 `.local/research/`，仅为可实施输入，尚未运行对应功能验证。

`issue86-implementation-approach.md`、`issue87-implementation-approach.md` 已补齐荣耀普通/精英和三个 Boss 的逐行映射、固定层/计时/个人计数/原生版本差异、正式布局与各敌完整恢复用例；同为只读预检，不能计作实现或测试通过。

## 交付边界

不启动游戏/Steam、不复制到真实 mods、不改真实存档；产物只写开发输出。素材逐项核权，未知不分发，不擅联作者。用户手动安装和真实端/设备验收按第4/5/6人工批次集中安排。#88 不代表自动正式发布。主仓既有删除和未跟踪文件保持原状，历史 worktree 不清理；删除 worktree 仍遵守 AGENTS 的备份/报告/确认要求。
