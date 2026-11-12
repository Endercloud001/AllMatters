# #82–#88 验证覆盖地图：V14 / S6 / H13 / U8

2026-11-12。下表保留首轮审计时的证据及缺口；正在变化的实现以文首增量记录和各票日志为准。首轮审计仅只读调查，后续 #88 已实施独立兼容切片；均未启动游戏、安装或改真实存档。编号沿来源；S1–S6、H1–H13、U1–U8在本文表示**六/十三/八个场景行**，不要与交接文档中 S1模型、S2注册、S3保存等证据级别混淆。D=领域行为，N=真实原生DLL/命令/保存，G=真实Godot树/输入，M=正式游戏真实端。ENet本机独立进程单列，不能自动代签M。

## 后续实施增量（均未作为整票交付）

- 集成分支已含审查通过的 #82/#83、#88 兼容切片 `080104f` 和 #84 共享完整点 `d8fb141`（merge `31bb112`）。#84–#87 逐敌工作树尚未提交或合入；独立审查状态逐项列明，局部通过不能当作最终包事实。
- #84：主工作树的真菌、Byrdonis、Effigy、完整战前/胜利点、Colosseum第二场、Kin、BeastCry、五虫、DenseVegetation治疗后开战恢复、真实场景named Marker均已有局部双版通过。Kin真实同move重入、Byrdonis其他Regen来源反例已修到精确来源。Named Marker存在仍不证明整房hitbox、意图、血条、焦点或非零死亡动画。公共恢复正在提取无新怪物依赖的独立审查测试。
- #84 共享完整点：独立干净切片双版各8、关联Vegetation各13、真实三人在局延迟owner与全新三端加载各1通过，Build/Host及错误统计0；旧异步加载污染、胜利重复休整、缺失RNG默认接受均经实际red→green及独立复审。已集成，范围及证据见 [共享点记录](issue84-combat-checkpoint-seam.md)。旧主树未自动等价为已消费此修复。
- #84 暗港分工作树：Gardener双版各1例通过（内部1–4人数×安全/受罚），四原槽/首招/单次History9与同目标其他Skittish来源保原生缩放均通过；日志 `.claude/worktrees/issue84-underdocks/.local/issue84-underdocks/gardener-green-{public-beta,public}.log`。其余暗港规则待实现。
- #85：Ponder/Liquify/Knowledge A8/A9攻击双版各3例通过；Liquify真实沙坑5、原目标两牌堆各4、StatusIntent8，攻击意图与DamageCmd一致。日志 `issue85-hunting/.local/issue85/knowledge-attacks-green-{public-beta,public}.log`；普通/精英开场、蟹、逐玩家选择屏及全恢复仍待交。
- #86：Mecha开场及真实异步重入双版各2例通过；已移除AsyncLocal，缩放例外限确切mutable Artifact的一次调用，同目标其他来源正常缩放。日志 `issue86-hunting/.local/issue86/mecha-reentrant-green2-all.log`；整票其余规则仍未交。
- #87：Aeonglass 初值与真实混合 owner 第11/12/13次出牌双版各2例通过；五状态顺序、每owner重置12与显示事件、空手及9张手牌的原生溢出已证。Wither 特定升级链、实验体/女王及全恢复仍待交。
- #88：黄金罗盘真实候选排除与24种子普通前后对照、Flight 新局拒绝/普通模式保留/直接 Enable 无半session、普通 Flight 保存大厅通过与拼图 Flight 明确拒绝且不改原文件，兼容 filter 双版各3例通过；扩展七文件已通过独立 Standards/Spec 源码审查。控制台实际发现两命令及顺序重置双版各1例通过。全量 Check 已通过（beta434/public436，两版 Build/Host0；日志含故障注入及宿主资源诊断，详见兼容记录）；真实每日抽样、损坏备份恢复与正常多人激活的具体运行归属仍需核对。详见 [兼容记录](issue88-compatibility-implementation.md) 与 [命令参考](issues-82-88-console-reference.md)。

增量中的Build/Host均退出0、EngineErrors/ModMetadataErrors均0，除明确尚未运行的用例。DLL目标/共享beta PCK/真实端限制与下表一致。日志位于相应worktree，本地路径不是可重分发测试资产。

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

集成目前为 `integration/issues-82-88-20261112` / `.claude/worktrees/hunting-20261112` HEAD `2f95862`，**只含82/83**；main仍 `f7e915c`。远端PR104按控制器当前状态只到 `f754aa2`，本轮不联网改写该状态，也不把本地83或W84–87说成已推送。集成 `docs/issues-82-88-implementation.md` 中85–87“无生产/首red待跑”是早于首片的时间性文字，以W85–87新日志范围更新，不能反向扩为整票完成。

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
