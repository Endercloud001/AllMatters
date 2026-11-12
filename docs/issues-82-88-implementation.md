# #82–#88 实施索引

日期：2026-11-12。本对话主控集成，开发起点 `f7e915c`，集成分支 `integration/issues-82-88-20261112`。

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

以下为早期预检来源索引（当时尚待 #82；当前以表格和接续增量为准）：本机 `.local/research/issue83-implementation-approach.md`（宝箱原生开箱、任务及空 Tween）及 `implementation-82-88-assets.md`（已核实的用户素材与 Balance 固定版本资源路线）。另派只读调查核实宝箱异步任务/完整保存点及 #84 的完整战斗表；这些记录描述预检阶段，不撤销上文已经集成的 #82/#83 和共享检查点。

只读预检补充：`issue83-checkpoint-seam.md` 记录空箱异步任务、半份任务保存及全员完成待投票的恢复风险；`issue84-implementation-approach.md` 逐行映射密林/暗港表，`issue84-event-checkpoint-seam.md` 细化 Colosseum 第二场等事件战前点；`issue85-implementation-approach.md` 映射巢穴表、知识恶魔原生选择及固定层人数反例。均在本机 `.local/research/`，仅为可实施输入，尚未运行对应功能验证。

`issue86-implementation-approach.md`、`issue87-implementation-approach.md` 已补齐荣耀普通/精英和三个 Boss 的逐行映射、固定层/计时/个人计数/原生版本差异、正式布局与各敌完整恢复用例；同为只读预检，不能计作实现或测试通过。

## 交付边界

不启动游戏/Steam、不复制到真实 mods、不改真实存档；产物只写开发输出。素材逐项核权，未知不分发，不擅联作者。用户手动安装和真实端/设备验收按第4/5/6人工批次集中安排。#88 不代表自动正式发布。主仓既有删除和未跟踪文件保持原状，历史 worktree 不清理；删除 worktree 仍遵守 AGENTS 的备份/报告/确认要求。
