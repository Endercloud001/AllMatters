# #82–#88 实施索引

目标：public0.107.1 / public-beta0.113.1 / RitsuLib0.6.11。集成树`hunting-20261112`、分支`integration/issues-82-88-20261112`，源码基点=`5d127b0`。本页只导航已集成成果和验证基点；在制源码、未提交状态、Ready顺序及下一命令统一见[唯一看板](issues-82-88-board.md)。

需求以[#73](https://github.com/Endercloud001/AllMatters/issues/73)及各票明确修订为准；规格固定来源见[验证地图](issues-82-88-verification-map.md#规范与历史依据)，API/工具方法见[开发入口](development.md#api-查询)，来源与分发边界见[素材来源](issues-82-88-asset-sources.md)。覆盖矩阵集中在[验证地图](issues-82-88-verification-map.md)，不在此重复逐次执行日志。

## 已集成范围与证据入口

下表均为部分自动化交付；每条来源记录保留准确源码提交、命令、版本、人数/端数、审查及未验范围，不能把一票中的代表case写成整票通过。

|Issue|已集成成果|权威实施/证据记录|
|---|---|---|
|#82 限步/面具/服务|`46a2d5b`领域/保存/恢复投影、HUD、控制台和服务限制；`09fa14f`→`76e49d2`真实休息遗物及双run自然离开|[实施记录](issue82-hunting-implementation.md)、[双轴审查](issue82-review.md)；休息遗物及原native rest集成双版各2，四诊断0|
|#83 空宝箱|`90c7970`空奖励/Face/Spoils及任务等待；`fb58274`→`2915a6a`双版正式整房鼠标/Proceed与自然保存恢复|[实施记录](issue83-treasure-implementation.md)、[双轴审查](issue83-review.md)；最新集成双版各11，四诊断0；未知直接支付/未返回Task后台不宣称通用拦截|
|#84 密林|原生逐敌规则、Phrog五虫及Vantom普通墨宝、正式布局/鼠标/全焦点环、密林首普通及Phrog/Vantom战前与胜利奖励fresh|[密林记录](issue84-overgrowth-implementation.md)；Phrog最新组集成双版各8，Vantom最新组各13，四诊断0|
|#84 暗港|共享完整检查点`d8fb141`→`31bb112`；Gardener/Colony/Eel、Matriarch生命/睡眠随从/fresh、Waterfall复生/fresh、SoulFysh随从/fresh/布局、Seapunk14场开场/fresh/布局|[共享接缝](issue84-combat-checkpoint-seam.md)、[暗港记录](issue84-underdocks-implementation.md)；最新Waterfall`2fb2b0a`→`eb4837c`集成双版各8，四诊断0。Matriarch布局`6655b2c`→`10b05df`已集成，双版各5四诊断0|
|#85 巢穴/知识/蟹/Holes|ActTwo规则、Hive精英与事件开场/Hive首普通fresh；Knowledge三轮单owner规则、Tender真实屏与单ownerfresh；帝皇蟹固定Artifact3与原场景保持；Holes单owner及三端战前/奖励/完成点恢复|[巢穴记录](issue85-hunting-implementation.md)；蟹最新集成双版各9，Holes完成点`1956c17`双版event各4。Knowledge首轮双owner片及`2c45c7c`→`845d00e`自然调度及`22dd5cc`→`673ce47`三轮双owner已集成；蟹`859096a`→`f2906d4`完整fresh已集成，专项双版各1四诊断0|
|#86 荣耀普通/精英|Mecha/SoulNexus/Flail原生规则；Mystic正式模型/资源/三动作/fresh；`a050e7b`→`21a563f`12普通/3精英正式布局及输入|[荣耀记录](issue86-hunting-implementation.md)；布局集成双版各15、相关Forest/Mystic各3，四诊断0|
|#87 荣耀Boss|Aeonglass个人12计数/五状态/Wither及fresh；TestSubject复生/计时/Skittish/Suck/MultiClaw/fresh；Queen一次重燃/TorchHead/激怒/fresh/布局|[Boss记录](issue87-boss-implementation.md)；Queen布局后集成各9、Aeonglass/TestSubject fresh各1，双版四诊断0。间歇P87故障未修，个人正式UI及`adfc29f`→`251147c`原场景、`8e57b7d`→`8d3c602`三阶段布局已集成，专项双版各2四诊断0|
|#88 兼容/交付|`080104f`GoldenCompass/WingedBoots与Flight边界；控制台注册/执行；`4aae096`→`f5d8dde`64种子每日modifier字段/RNG验证|[兼容记录](issue88-compatibility-implementation.md)、[控制台](issues-82-88-console-reference.md)；U3长译文按钮`335653f`→`fcc8831`集成双版Presentation各20；当前候选完整Check/同提交包/集中人工矩阵收口中|

## 完整检查与已知诊断

最终候选源码`41b21300cf7a30ef2d2aa89f6cc232a416de8e81`完整`Check.ps1 -Branch All`退出0：脚本、空白、资源、两版宿主及两版构建全部通过；beta0.113.1通过654项、public0.107.1通过656项，Build/Host均0、Engine均26、Metadata均0。实际归档分别为`.local/tests/archives/run-20261115-172333350-347d3f22e4544ec18a012a60f814c3ca/`和`run-20261115-180125874-702ef8e3ecd840c681d478cff9b29529/`，控制台`.local/implement-resume-20261114/full-check-20261115-41b2130.log`。模型缓存、Hive、Knowledge与Queen右火炬修复点均已越过完整原序。

beta26条诊断标题与4972518逐条一致，分类见下；public为18条保存/名单保护负例、1条worker注入、1条失效事件任务拒绝、2条原生managed action队列诊断、1条headless视觉脚本缺类、3条退出资源诊断。完整通过不代表零诊断，也不改签暂停的GCHandle/P87已修。

同源码统一包已生成到`dist/batch-6-integration/AllMatters-public/`及`AllMatters-public-beta/`，每版恰五文件，DLL产品版本均含完整41b2130提交。最终PCK原生枚举409条、导入113条全部有源引用，已从包内读取Queen新marker1320/695；实物/来源核对见[素材来源](issues-82-88-asset-sources.md)，用户集中安装与剩余人工项见[验收指南](batch-6-test-guide.md)。未推送、安装或关闭工单。

前候选`4972518`完整Check退出1：静态/脚本/资源与双版构建通过，beta0.113.1的Build0/Host0/Passed654/Engine26/Metadata0；public0.107.1的Build0/Host1/Passed=null/Engine25/Metadata0，已通过原序Hive与Knowledge三轮，随后Queen四角色720p布局的右Torch血条与Queen目标框出现逻辑2.700×2.182、物理约1.8×1.45交集。原档分别`.local/tests/archives/run-20261115-155544343-fe2baffe8fba441ab726e4c144b97dc2/`与`run-20261115-163417811-4dc7c2d635004954bdae0accdb3b4f8f/`，控制台`.local/implement-resume-20261114/full-check-20261115-4972518.log`；布局按真实red另片最小修复，原断言不放宽。

beta26条诊断已核为21条保存/名单负例、worker注入及失效事件任务拒绝，另1条原生多人场外致死保护、1条headless视觉脚本缺类和3条退出资源诊断；不能称零错误。本轮未复现既往GCHandle致命错误，也不把它改签已修。

Queen几何red已由`2b0725d`→`41b2130`最小修复，仅右Torch marker X1330→1320，测试未改；双版Queen原组各9 strict通过，独立双轴无发现、正常hooks、已审补丁一致。实测间隙与原档见[Queen右火炬分离记录](issue87-boss-implementation.md#queen-右火炬血条与女王目标分离2026-11-15)；当前41b2130完整Check已通过，实测654/656来自新资源实际执行。

前候选`c4bc995`的完整Check也退出1：静态/脚本/资源与双版构建通过；beta0.113.1在ENet进入转换用例发生地图绘制GCHandle致命错误，public0.107.1已通过原序Hive fresh，但在Knowledge原生双owner准备屏障失败。两目标Build0/Host1/Passed=null，Engine分别5/25、Metadata0。控制台`.local/implement-resume-20261114/full-check-20261115-c4bc995.log`；beta原档`.local/tests/archives/run-20261115-120357097-b91d1314bf6e47c6a2500d79a20a2fa0/`、public原档`run-20261115-120600967-b7d41baa2d1647758037cb6508f3d3c0/`。该候选当时进入两项窄诊断，后续修复与暂停事实见下文。

beta角色2栈为`ResourceLoader.Load`→`JigsawNativeArt.DrawPath`→正式`_Draw`，Host随后等其头像超时；首fatal已复制到`.local/enet-art-binding-20261115/first-beta-fatal`。public的Until超时诊断在MapRoom读Hand又抛异常，遮住真实等待；父已写`host.entry-ready`、子未写，不能把失败端武断归父。该次失败时统一包尚未就绪；当前交付状态见页首。

Knowledge后续窄复现与0.6.11方法体确认：父Sidecar仍观察旧断线服务，旧features使父越过能力检查，子尚未协商，父实际超时于双方准备屏障。`c52af37`→`4972518`只修改测试夹具：SetUpTest后公有Observe实际network，清理只释放本例owned epoch；保留20秒、真实握手与三轮断言，诊断房外读取不再遮原异常。实施树`knowledge-handshake-20261115/.local/tests/archives/knowledge-handshake-diagnostic-20261115-123518036-4bf72692bbe34a3caf33d8230bbf5256`为真实red，`knowledge-handshake-fixed-20261115-123758450-1b75dde896b947689cc2ea42d9f74687`及`knowledge-handshake-fixed-beta-20261115-124054448-ead8d00638aa4111b448fac9b7481b4d`分别public/beta各3 strict绿。独立双轴无发现、正常hooks、已审补丁一致；本片不签正式游戏跨跑局入口。

绘制强持有实验已归档撤回：1131实际AtlasManager已有强缓存，不能称缺少GC根；实验绿不签根因，GodotSharp版本一致也不证明没有绑定竞态。原件在`map-art-lifetime-20261115/.local/map-art-lifetime-experiment/`，当前暂停等待实际绑定重建或Dispose证据。4972518中beta完整通过，绘制致命错误未复现但仍未定位，不签根因修复。

更早完整`Check.ps1 -Branch All`在`fcc8831`退出1：脚本、空白、资源与双版构建通过，但beta0.113.0在RestRelic真实子端握手失败，public0.107.1在Hive fresh的启动Task计数断言失败。控制台归档`.local/implement-resume-20261114/full-check-20261115-fcc8831.log`；原测试档分别为`.local/tests/archives/run-20261115-064511158-8eb09c30e9534b08afe81e89da4a0d35/`及`run-20261115-070007780-632c2093b76848b593dfcef61dbee504/`。该候选当时因这两个失败未交付，不能用构建成功替代宿主通过。

beta握手已定位为Beggar保存测试重复调用原生非幂等的模型缓存初始化，使宿主与新子端Hash不同；`fb9e6bc`→`030eb34`删除重复Init并保留保存恢复原断言，新增已初始化Hash不变检查。新目标beta0.113.1先取得真实断言red，修复后双版库存各1、真实RestRelic各2 strict通过；证据位于实施树`model-cache-fixture-20261115/.local/tests/archives/cache-fixture-{red,green,rest-regression}-*`。独立双轴无发现，正常hooks，提交补丁与已审补丁一致。该片不代签独立Hive问题或完整Check。

Hive一次原序prefix复现实际`expected=1 actual=0`、opening完成/liveRoom=CombatRoom，确认为原Task漏观测而非重复启动证据。`654e25c`→`c4bc995`只修改Hive测试，复用Waterfall已用的`CombatRoom.StartCombat.MoveNext`单次原调用前后观测；public保留原Task，beta读真实`_turnLoopTask`，原count及恢复断言保留，public仅成功`Task.CompletedTask`缓存允许共享而run/room/combat保持独立。实施树`hive-startup-diagnosis-20261115/.local/tests/archives/`下`hive-startup-prefix-20261115-113549838-cd510441e18e4a6bb6f88da84f851c50`为red，`hive-caller-verified-20261115-115637505-8f3865b80bc446e0977dea383faf6bef`及`hive-neighbor-waterfall-20261115-115721389-322147de42a34537ba356a87d896bc2b`为Hive/Waterfall双版各1 strict绿。临时runner已撤回，独立双轴无发现、正常hooks、已审补丁一致；集成c4bc995当次public原序已通过Hive，beta因更早ENet失败尚未到此；当前41b2130双版完整通过见页首。

2026-11-15用户确认beta更新为0.113.1（d09d48e5），public仍为0.107.1；独立完整运行时快照与RitsuLib0.6.11实际加载冒烟已通过。本文及逐票记录此前的beta0.113.0证据保留历史版本归属，不代签0.113.1完整检查。

较早双版全量基点为**历史提交`df6a1f9`**：public509 / beta507 PASS，Build/Host/Metadata均0，EngineErrors分别25/26，不能称零错误或为后续切片签完整Check。

- 原始日志在兼容树`.local/host-20261113/{public,beta}-full-df6a1f9-host.log`。诊断分类在**主仓**`.local/main-control-20261113-resume/{public,beta}-df6a1f9-diagnostics.md`。
- public含18条坏档/名单保护负例＋1worker注入，其余视觉缺类1、managed action pop2、退出资源3；beta含19条保护负例＋1worker注入＋1原生CursedTome多人场外致死保护，其余视觉缺类1、treasure空Tween NRE1、退出资源3。CursedTome保护恢复1HP，不是实际击杀成功。
- 当前局部宿主已能运行匹配资源；P87原六项间歇故障单独暂停，事实与恢复条件见[看板](issues-82-88-board.md#暂停的技术诊断)。未捕获原错的probe绿不构成修复。

最早开工基点`f7e915c`的beta0.111.0/public0.107.1冒烟为403/405 PASS、Engine22/23；仅保留主仓`.local/research/batch82-88/{public-beta-smoke,public-smoke}.log`历史锚点，不作为当前目标证据。

较早完整检查的逐次失败和过期派发安排已从入口移除；需要追溯时查Git历史及原日志，避免旧“未提交/缺public资源/下一片”覆盖现状。

## 环境、审查与交付边界

`1d1dac0`完成beta0.113.0兼容及RitsuLib0.6.11接入；`8a69527`将DLL/PCK/Spine绑定所选目标，public匹配资源位于主仓`.local/public-resource-recovery-20261113/public107-content/`。beta0.111.0旧通过数只作历史，不能代签当前目标；工具/API/构建方法只在[开发入口](development.md)维护。

#81仍需按具体分版/设备/故障项核对归属；其[#93/#94收尾确认](https://github.com/Endercloud001/AllMatters/issues/81#issuecomment-6814733380)已认可实现、自动验证及对应人工项，不能因父票OPEN将它们重新标未通过。其他适用真实端/设备验收仍逐项保留。

最终从复审后的同一源码提交执行项目完整Check、双版构建与许可检查，再按[实机验收交付](development.md#实机验收交付)集中提供人工批次。Agent不启动游戏/Steam、不写真实mods或存档；本地切片不授权推送、评论、关票或正式发布。原树、分支、stash及本机证据不因文档精简而删除。

## 第一批人工反馈修复（2026-11-16）

人工通过及全部原始失败项保存在[验收指南](batch-6-test-guide.md#第一批人工反馈2026-11-15)。通过项保留原反馈范围；最新游戏日志为 beta 0.113.1 / Workshop RitsuLib 0.6.13，自动宿主使用完整0.6.11，两者不混签。

本轮源码提交 `226c4e9`，正常pre-commit formatter及回暂存hooks通过，提交后与两实施树已审暂存逐文件一致。完整Check已退出0，PS语法/工作流/空白/占位检查、资源及双版全量宿主/构建全部PASS。beta661（Build/Host0、Engine26、Metadata0），归档 `run-20261116-030906107-4209ff762559452b8bc709b5c604e663`；public663（同为0/0、26/0），归档 `run-20261116-034551760-6cfa4d088f874547b9a3047209b70ec7`。完整控制台 `.local/issue88-manual-feedback/full-check-226c4e9.log`；26条仍按既有负例/宿主诊断分类，不称零错误。

本轮行为改动：未绑定Creature时保留原生怪物预载入口（Effigy/Wriggler/TheInsatiable）；Boss投影保留原生特殊节点，旧保存地图在通知原生控件前迁移（public与beta分别接入口）；地图显示/隐藏配对原生Opened/Closed，移除奖励暗幕残留；Exploring隐藏多余对话，保留入场失败的恢复按钮。光门中英文仅改文案，原升级选择规则不变。

战斗改动：暗港自动布局对齐原版相同数量队列中心，专用场景及密林原站位保留；Phrog五虫沿地面单排并缩放到0.8，真实HP/意图/鼠标与手柄可用；Waterfall在AboutToBlowMove完成后去除全部Debuff（含负力量/隐藏负面）并固定获得Artifact3，首次致死时不提前发放。

窄验证证据在同级实施树 `.local/tests/archives/`：combat树 `issue88-combat-full-slice-1-20261116-000840831-7d44c0448c494ae7b472a0fdabc15ba7` 为双版Waterfall各8，`issue88-combat-full-slice-2-20261116-000855237-6d4d8c040ebf4a10801dcda5348fe964` 为真实恢复/领奖各1；Phrog几何双版各1见 `issue88-combat-corrected-1-20261116-000605246-c7fee472c47b410d953202d064b2b073`。暗港独立oracle直接采样重排前原生同数量真实节点中心，删除公式副本并作双向偏差断言，双版各14 strict、Engine/Metadata均0，见combat树 `issue88-native-layout-independent-20261116-025531858-37a4de85c71049d4be7d8de5fc9eea7b`。

flow树 `issue88-legacy-compatible-green-20261116-000502050-d5333ca6938a4df981ced4fc6959ad58` 覆盖实际SavedMapsToLoad→GenerateMap双版各1；`issue88-flow-strict-fixed-20261116-025343277-ab0a86b2d990459491a723a21c0cfff5` 为补充怪物预载双版各2；`issue88-flow-strict-fixed-20261116-025358200-f2f714b4c6ad47b68ec8369c9b67bfd2` 为奖励Overlay/地图热键/真实Escape双版各1且Engine/Metadata为0。光门既有行为回归双版各1见集成树 `issue88-light-copy-20261115-234637855-414c4354978649dfa2f704ccfcba6b13`。

独立Standards/Spec审查覆盖两个实施树暂存差异与增量；两版新增Harmony目标已核实，暗港测试原单向/公式副本缺口已修。测试名称从“preserves intents”收窄为实际验证的“preload succeeds before creature binding”。本轮误启动的重复基线主动停止，归档 `issue88-manual-smoke-20261115-232243994-c501b03311a2454089c190db40d36e44` 不计通过。

未签收：真实游戏连续精英、事件入场、fight、Boss奖励按钮后跨幕及旧档恢复后领奖出口，均须对修复包实机复测；自动投影/预载/Overlay验证不冒签完整游戏流程。另日志 `godot2026-11-15T22.53.36.log:785` 的Continue异常落在BaseLib.CustomMessageWrapper.Unregister→RunManager.CleanUp，`godot2026-11-15T19.58.20.log:30310` 为D3D12设备丢失；现有栈没有证明本Mod重复清理或事件逻辑致错，两项未宣称修复。

统一包已按五文件白名单更新到本树 `dist/batch-6-integration/AllMatters-public/` 与 `AllMatters-public-beta/`；两版DLL ProductVersion均为 `0.1.1+226c4e9879c50165c24913e31bde8f0f95154b98`，各5文件，PCK各85,203,232字节且与此前已检查资源包字节完全一致，原409资源/113导入及Queen右Torch1320/695证据继续适用。LICENSE/THIRD_PARTY_NOTICES与仓库原件一致，依赖manifest仍为STS2-RitsuLib。旧统一包5文件保留在 `.local/issue88-manual-feedback/previous-delivery/`。Agent没有安装、启动游戏/Steam、写mods或真实存档；按[修复包复测顺序](batch-6-test-guide.md#本轮修复包复测顺序)完成用户实机确认。

## 第二批反馈修复（2026-11-16）

`b3f0a49`已集成TestSubject/Knowledge攻击getter正式预载guard、Hive同entry确认后继续三负面，并补真实帝皇蟹fight→Knowledge开局→win回归；正常hooks与两轴审查通过，集成树双版Hive各2、console各1 strict及双版构建通过。flow树合法mutable TestSubject用例曾恢复旧guard再次red，避免误用原生不支持的canonical入口；实际旧存档恢复仍由用户复测，未证存档格式损坏。

flow树 `.local/tests/archives/` 的 `manual2-flow-complete-1-20261116-054343830-8447dd16c9f34dee9d496fe0d201e16d`双版预载/绑定7、`manual2-flow-complete-2-20261116-054359922-3921289026f24b69ba0491f26a76eb46`双版Boss7、`manual2-flow-complete-3-20261116-054430815-4b55f04125d4459f8cba29760696751c`双版完整console1、`manual2-flow-complete-4-20261116-054448323-5f0c6ead9599483cbb2ee6f2b659fd83`双版原CrabFresh1均strict通过。RitsuLib0.6.14独立snapshot额外验证beta预载7、console1、TestSubjectFresh1，见同树 `manual2-flow-ritsu0614-{1,2,3}` 归档；源安装只读，未更改游戏依赖。

Hive修复前pending确认red见rules树 `manual2-rules-red-1-20261116-053414857-e0a946c46db34b4086be451fd818c476`；修复后双版各2及原stale边界各2见 `manual2-hive-green-1-20261116-053927297-71d6d13d7b6341a29a1a43765bc858af` / `manual2-hive-green-2-20261116-053944441-2dba66925bd64b4ab0994bf26a77c0c8`。

Mystic用户已明确“敌人HP确实上升”，问题仍在诊断。源安装DLL已核为226c4e9；实机库为Workshop0.6.14。受伤敌人自然动作和0.6.14全组8例均通过；正式无节点协调器四轮也未复现（rules树 `manual2-mystic-coordinator0614-20261116-055054892-bf24a5e46229490a8e3e5d78f8d8e6f6`）。这不是修复证明；真节点用例依次补齐NCard池、纯表现ScreenShake与墙钟等待夹具后继续，不把准备失败算用户Bug。

`a464e66`将荣耀普通自动布局转发已验暗港算法（暗港本体仅private→internal），用户TurretOperatorWeak场景12先独立原生中心断言red；修复后双版各1、完整荣耀普通12+精英3双版各15 strict通过（glory树归档`manual2-glory-layout-all-20261116-060830212-0548e517b54e46cfaac50a1488348cd5`）。集成窄核与双版构建亦通过，正常hooks/双轴独立审查完成。

Mystic正式节点+协调器+真实牌视觉链在0.6.14最终strict通过，归档rules树`manual2-mystic-ui-wallclock0614-20261116-062140580-a8cf528951f141daa425468f21628a30`：屏幕缓存Status/Heal/Buff/Status与实际动作吻合，两次CAST只加Burn/Slimed各1、敌HP不变，HEAL回12。准备阶段的NCard池/ScreenShake/300帧等待失败已分开归类；大诊断补丁归档集成树`.local/issue88-manual2/mystic-ui-diagnostic.patch`，不合入正式全量宿主。

用户确认实机敌人HP上升仍未解释，不能按未复现签修。`00dfbd6`只添加`[AllMatters][MysticAction]`动作begin/end、实际NextMove、敌HP/玩家弃牌、CAST生成牌与HEAL目标前后HP快照，不含玩家个人标识、不增加RNG或await，日志错误不干扰原动作异常；两轴增量审查通过，主控双版各8及beta/Ritsu0.6.14各8 strict行为回归通过（`manual2-mystic-trace-regression-20261116-102129865-7537888ee6be4fc6a7ea0c270a5bc117` / `manual2-mystic-trace0614-20261116-102156313-8183024b4bcd4665a7d6ee449d5950bb`）。恢复诊断条件为修复包实机复现后的上述动作日志，暂停期间不重复碰绿扩测。

`14c9351`恢复TorchHead生产视觉的独立绿色粒子，跟随原生fireslot全局坐标，不改Queen规则或IntentPos；双版匹配扩展已实测骨骼移动增量，不叠乘全局变换。原始生产工厂无emitter断言red；双版节点窄核各2 strict见torch树`manual2-torch-native-corrected-20261116-104550528-af1e80cd0a684eeba1831410dc24694c`。首次真实GL截图仅小绿点并在原点残留，未按绿色像素初断言签收；取消预热、先定位后启动并调整密度/尺寸后，真实OpenGL双版各1通过，图为torch树`.local/torch-flame-beta-v2.png`（1938绿像素、extent301,176..360,249）与`torch-flame-public.png`（1578、302,173..360,249），均由主控及Spec独立看图确认头笼内升焰、左上无残留。生产死亡停止发射有源码依据，独立截图不代签真实Queen召唤/缩放/遮挡；生命周期测试的重入只断言Visible，不扩大为死亡行为已验。正常hooks与独立双轴审查通过，未启动正式游戏或Steam；GL导入器改写的实施树`.import`未集成。

最终同一源码`14c935188d43aa7cb0082b7e6235e001bf27372b`的`Check.ps1 -Branch All`已退出0，资源、脚本、空白、双版完整宿主及Release构建均PASS，进程已结束。beta669（Build/Host0，Engine26，Metadata0）归档`run-20261116-105950642-199a7d0e4d1141f391fa1993568d0876`；public671（同为0/0、26/0）归档`run-20261116-113834407-29d4c94cc5914de58159cef3809cf1cb`，均在集成树`.local/tests/archives/`。完整控制台`.local/issue88-manual2/full-check-14c9351.log`。beta诊断逐条与前批相同，public仅故意坏档负例的隔离文件时间戳不同，归一后无差异；不称零诊断。完整执行涵盖新增Torch各2、真实Crab→Knowledge控制台、Hive三负面、荣耀15与原暗港14、Queen/实验体布局及恢复消费者；没有因窄核绿灯省略既有完整交付要求。

统一交付两目标目录各5文件，DLL ProductVersion均`0.1.1+14c935188d43aa7cb0082b7e6235e001bf27372b`（public1,077,760字节，beta1,074,176字节），PCK各85,203,232字节并与前批已核资源逐字节一致；LICENSE/THIRD_PARTY_NOTICES与仓库原件一致，manifest仍声明STS2-RitsuLib。替换前的226c4e9五文件分别备份在`.local/issue88-manual2/previous-delivery/AllMatters-{public,public-beta}/`，未动更早备份和用户handoff文档。完整宿主依赖0.6.11，0.6.14仅按上文窄证据记载。实机安装、旧黑屏跑局恢复、连续精英/事件、跨幕、暗滤镜及新外观仍由用户按指南复测；Mystic回血待带动作快照的新日志，不因本包重新签为通过。

## 第三批反馈实施（2026-11-16）

已按用户确认规则实施，见[第三批计划](plans/2026-11-16-issue88-manual3.md)及[人工反馈](batch-6-test-guide.md#第三批人工反馈与确认规则2026-11-16)。

`6f8d463` 启用目标beta标准/恐惧，保留每日、极速和自定义原生流程。靴/罗盘回到先古候选，首次拾取当幕全队上限与余额同时加3，保留已激活追猎，恢复保留当幕已加标记。独立双轴审查发现命令可设 int.MaxValue，首次拾取的余额加法已封顶并核双版边界。模式树 `.local/tests/archives/manual3-mode-*` 归档包含行为red、双版4例、实际ENet启动矩阵、先古候选与恢复。

`34cd003` 第二幕合法手拼或自动采纳时产生共享藏宝图标箱；无箱原生可见移卡，晚取卡不补标；标箱按各owner抵达时牌数走原生每张600，追猎只跳过原普通收入，保留整份进房前恢复点。皮草每幕全队一次按版本取7/8，开场和召唤只设当前HP。两版标记树 `.local/tests/archives/manual3-final-*` 严格验证7个新增用例和11个旧宝箱用例。图标从原生场景属性读取，避免实例化原生节点造成测试宿主脚本装载诊断；集成双版真实纹理断言见 `manual3-merged-icon-state-20261116-150721561-b9e982785024453a831b1a356695e895`。正式地图上图标观感与无箱提示画面待实机。

同提交 Mystic CAST 使用原STS1普攻的前冲表现（治疗抬杖），向原生预览提交实际进入弃牌堆的卡；用户已确认旧塞牌正确，清除临时诊断。两版Mystic各8 strict含真实弃牌结果/预览对象/RNG验证，见集成归档 `manual3-mystic-20261116-150315222-842219b86375412bb53becfd4690d24d`。细看两条中文描述改为指定文案。前冲和卡牌提示画面仍交实机核对。

联机宝箱旧夹具原在第一幕入房后硬写原生任务坐标，与第三批规格冲突。第一次全量已主动停止；`d223dda` 把夹具迁为第二幕进房前有正式逻辑标箱的保存，保留真 ENet 两端等待、卡牌移除及重载检查，断言只给持牌者600且不支付追猎的普通金币。双版严格各1通过，见 `manual3-merged-treasure-enet-20261116-151027074-f3b0ff2d74a34120a2cbc76079a3c918`。最终完整检查按 `d223dda` 重启，控制台 `.local/issue88-manual3/full-check-d223dda.log`。

候选d223dda的beta完整680通过，public在deferred Ancient旧测试夹具失败。新候选恢复先古池后抽到原生万花筒，旧夹具仅支持牌组选卡，并把真实奖励消耗RNG误列为继续流程错误。夹具现支持原生奖励选卡并在奖励完成后采样RNG，继续后核对其保持；地图步骤/历史与已完成玩家DTO及下一次原生重载断言均保留。双版该用例strict各1通过，归档 `manual3-deferred-reward-rng-20261116-160026598-9f023b74c62049ada4f9803854ff439d`。失败完整归档 public `run-20261116-155127269-43651927a3e04693bfc973056b464e14`，beta `run-20261116-151215712-d1f76fc0a9ee4eb8b76c1e7ca9384638`，不得将此轮记为完整PASS。

候选dbd52fc完整beta680再次通过，public已过deferred Ancient后在FirstRoomFlow旧夹具强选PRECISE_SCISSORS失败。日志实际可选FISHING_ROD/ARCANE_SCROLL/SILKEN_TRESS，现统一选择两版都有的ARCANE_SCROLL，验证实际新增一张原生Rare、原卡保留，完整DTO/RNG回滚与重放断言保留。双版实际奖励回调strict各1通过（manual3-actual-reward-scroll-20261116-165103956-167174dbdab64c1482a731b986efc8dc）。网络deferred的强选是已显式ForceReward注入，核后保留。此轮完整失败归档不能代签最终通过。

最终源码 `3f2059377a9281b0d0bbadff4658f0f429c1077d` 完成两版全量。原 `full-check-3f20593.log` 归档 beta 680通过（`run-20261116-165328286-a2a9c25d4cad4b72afd76bf39774fa21`），public在帝皇蟹布局期间进程中断、无完成摘要。以相同提交重跑 `Check.ps1 -Branch public`，全部步骤PASS，public 682通过（`run-20261116-175745206-5569e9f4fad4477ba22e36c791bf96da`）。随后以相同提交及新导出PCK执行beta构建，成功完成。两版测试构建/宿主0/0、Engine26、Metadata0；诊断与前批相同类别，public两条中文输出仅控制台编码不同。恢复检查、beta构建和交付核对日志分别为 `public-resume-3f20593.log`、`beta-build-3f20593.log`、`package-result.log`，均在 `.local/issue88-manual3/`。构建使用项目默认Debug配置。

第三批统一目录每目标五文件。两版DLL产品版本皆 `0.1.1+3f2059377a9281b0d0bbadff4658f0f429c1077d`，public DLL 1,091,584字节、beta DLL 1,088,512字节，两版PCK均85,203,520字节；已逐文件核对构建输出与交付副本、manifest依赖及许可证原件。第二批包已备份到 `.local/issue88-manual3/previous-delivery/`。实机按指南确认新模式、当幕步数、地图标记和战斗反馈。

## 第四批反馈实施（2026-11-16）

用户实机通过的模式准入、三个遗物、“细看”及藏宝图金币和移卡已记入[看板](issues-82-88-board.md#人工已通过)。开箱后曾触发等待弹框，返回地图无法继续。暂停存退、继续、放弃与重载各有复测步骤。

原生单人跳过遗物只标记跳过并立即推进，开箱任务仍等待遗物完成事件。拼图在推进与退出中都等待该任务，导致真实 Skip 输入后终端推进卡住。beta 宿主旧行为在 `.local/issue88-manual4/marked-native.log` 复现超时。修复在已确认跳过后复用原生空奖励完成通知，房间推进继续等待开箱自身，退出等待已启动的源奖励和原生遗物分配。不同幕的同编号房间独立记录，放弃跑局时跳过额外的退出前保存。原版跳过按钮计时跟随场景销毁，避免退出后尝试启用已销毁的按钮。

神秘术士施法复用治疗的挥杖动作，以及原版夜魇使用的雾幕和手影节点，仍按玩家生成状态牌、加入弃牌堆并播出原生预览。酷刑博物馆仅 beta 生效，复用原版展示遗物和结尾，拿走加一张可悬停查看的红字愧疚，逃出扣当前生命10点。规格已补记于 [#73 评论](https://github.com/Endercloud001/AllMatters/issues/73#issuecomment-6914847082)。

独立规格与标准审查覆盖暂存差异和增量。宝箱真实输入两版各2，严格诊断为0，见 `manual4-marked-final-20261116-193711579-8dca48bc89404c898f527c9afaa9dddd`。用例覆盖金币和移卡已结算时经过原生暂停退出入口，以及实际 Skip → 拼图地图 → 同进程新大厅恢复和唯一结算。退出用例替换原生主菜单场景切换，由实机继续核验完整存退和放弃。Mystic 双版各8，见 `manual4-mystic-20261116-192841360-004d33cc9f714c01938fe7fd81599599`，画面留待实机。博物馆 beta 两例见实施树 `manual4-museum-20261116-192135702-369a87283e3e4beb8cd4430ef8eb596e`。候选 `f25de547bcd5444a4221cccb87c7f081302c11c7` 通过正常提交钩子。

同一源码 `f25de547bcd5444a4221cccb87c7f081302c11c7` 的 `Check.ps1 -Branch All` 已完整退出通过，包含资源、脚本、双版本完整宿主和构建。public-beta 684通过，归档 `run-20261116-193945663-8f8d39877202420b804ad00fb30d227c`；public 684通过，归档 `run-20261116-201901712-de64bd66dcd24aae94d78d48d620da61`，都在本树 `.local/tests/archives/`。两版 Build/Host 各0/0，Engine26、Metadata0。完整控制台及交付核对在 `.local/issue88-manual4/full-check-f25de54.log` 和 `package-result.log`，完成标记为 `delivery-complete.txt`。

双版本统一包各五文件，DLL ProductVersion 均为 `0.1.1+f25de547bcd5444a4221cccb87c7f081302c11c7`。public DLL 1,095,168字节，beta DLL 1,096,192字节，PCK各85,203,520字节。交付脚本逐文件核对构建副本、manifest依赖和许可证，第三批包保存于 `.local/issue88-manual4/previous-delivery/`。新的夜魇呈现和完整存退/放弃操作仍按第四批复测指南进行实机验收。

## 第五轮反馈与issue补记（2026-11-16）

用户已确认第四轮问题全部通过。追加建筑师面具栏图标裁掉原图多余透明留白，使用Godot AtlasTexture区域(128,32,1104,1136)，原始图保留用于大图。中文首句使用指定“建筑师的目光盯上了你。”并标红，英文对应首句同步标红。

双版既有商店/面具用例各1通过，新增断言验证真实Icon加载为AtlasTexture及轮廓一致，归档manual5-mask-20261116-213245403-b84b1ca347c94d1c88f0bfec06ffc553。此轮展示小改动沿用第四批684完整验证作为基线，受影响窄核和双版构建另记。

已按用户授权发布以下评论，逐票保留本地源码、真实宿主与人工反馈的证据范围。

- [#73 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/73#issuecomment-6917999528)
- [#81 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/81#issuecomment-6918021041)
- [#82 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/82#issuecomment-6918021580)
- [#83 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/83#issuecomment-6918022070)
- [#84 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/84#issuecomment-6918022521)
- [#85 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/85#issuecomment-6918023083)
- [#86 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/86#issuecomment-6918023625)
- [#87 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/87#issuecomment-6918024148)
- [#88 实现或规格补记](https://github.com/Endercloud001/AllMatters/issues/88#issuecomment-6918024658)

最终面具提交448e8f7cb3b176ca8fe591bdaeb093eda05da0f9通过正常hooks和独立只读审查。最终双版受影响用例各1 strict通过，Engine/Metadata各0/0，归档manual5-mask-final-20261116-214158318-77c3d3d2cb4a4d598a3cf642dad05e3e。两版构建通过，统一五文件包更新且逐文件核对，DLL产品版本为0.1.1+该完整SHA；第四批包保存在.local/issue88-manual5/previous-delivery/。展示微调未重跑684全量，沿第四批全量加本轮受影响窄核与构建记录。

## 第六轮反馈修复（2026-11-16）

源码f4ea117c86e140372e9a13a627ec23ce0e1c1493修复四项反馈。探索中的普通“还可前行N步”句不再显示，最后一步/额度耗尽和操作反馈移至屏幕左下；原生顶栏步数与悬停仍可用。地图_Input在原生Capstone及可见Overlay打开时交还输入，避免查看本人或队友牌组时触发地图。beta Peck动态变量实际为HitCount，public为Repeat，现按版本插入描述，伤害与去格挡实际行为沿既有实现。

重复已投票节点调用原生FlavorSynchronizer.SendMapPing，沿原消息广播和200ms节流；两版原生PingMapCoord接收转到拼图逻辑节点，以原生NMapPingVfx、玩家MapDrawingColor、头像闪烁及map_ping.mp3呈现。原生参数顺序分别为public(coord,player)、beta(player,coord)，Harmony按名称绑定。锚点随平移缩放更新，特效生命周期由原生播放结束释放。

两轴独立只读审查通过。双版地图UI各22 strict通过，归档manual6-ui-merged-20261116-234852677-93e486b6c5c04b868e51829a49f828be，包含Capstone打开时真实引擎卡牌按钮点击不投地图票、左下提示位置及实际原生波纹颜色/中心/释放。Peck双语普通/升级描述及攻击结算各2 strict通过，归档manual6-peck-final-20261116-234028892-cf63339b7b9044edb4bceacfc0d7c171。两版Engine/Metadata均0/0。UI夹具使用前景按钮代表卡牌，实际完整牌组界面和真实多人音效/广播按第六轮指南复验。原生卡牌未升级时调用升级预览API仍显示其当前3次，测试按实际动态变量状态断言，升级卡为4次。

双版默认Debug构建通过，日志.local/issue88-manual6/build-{beta,public}.log。统一交付五文件已逐项核对，版本0.1.1+f4ea117c86e140372e9a13a627ec23ce0e1c1493，第五轮包保存于.local/issue88-manual6/previous-delivery/。

## 地图绘画恢复（2026-11-17）

第六轮问题经用户实测通过。拼图探索复用唯一一层原生 NMapDrawings 与其画笔、擦除、清空、网络和原生序列化。一键清空后同帧存档也会移除待销毁笔迹。战前检查点回退保留当前幕新笔迹，恢复层按原生归一化坐标换算，保存并退出时补写当前绘画。beta 使用原版分享，public 0.107.1 使用 Godot 导出全景 PNG。

源码 fbc47fc8f1f2000d54032bfd12168f33b13b43fd 两版绘画1、保存3、地图UI22均严格通过。归档 drawing-final-tools-20261117-010002324-419570e46db54fa0aaf16efc1f6f8615、drawing-exit-20261117-005905206-3dd9bbe785c349a29b9d5a882d9831ee、drawing-presentation-20261117-010018053-f095e724a7554b119e0de06f7adf3e2b。public OpenGL 宿主导出含笔迹的1280×720全景图，证据在 .local/issue88-drawing/public-panorama.png。双版构建和五文件统一交付核对通过。beta平台服务写入及真实多人绘画按复测指南验收。

### 绘画工具栏左侧裁切

实机截图中画笔及悬停说明越过左缘。拼图地图现在按三个原生按钮和悬停提示锚点的实际边界定位于左下。回归在 1280×720 模拟画笔伸出工具组左侧 96 像素，断言三个按钮完整进入地图视口。双版各1严格通过，归档 drawing-toolbar-20261117-011021578-68c2c63afc73413ab2acb70082d0310f。双版构建与五文件校验通过，源码 969d245，旧包备份在 .local/issue88-drawing-toolbar/previous-delivery/。
