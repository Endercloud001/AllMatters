# #82–#88 验证覆盖地图：V14 / S6 / H13 / U8

更新：2026-11-14，目标public0.107.1 / beta0.113.0。本文保留V14/S6/H13/U8逐行责任与尚欠证据；实际提交、命令与日志见[实施索引](issues-82-88-implementation.md)及逐票记录，在制状态只看[唯一看板](issues-82-88-board.md)。整票均不从局部PASS推定完成。

D=领域行为，N=真实原生DLL/命令/保存，G=真实Godot节点树及引擎输入，M=正式游戏真实端。ENet本机独立进程证明实际传输，combat角色数证明人数输入；两者均不自动等于多台机器或正式游戏端。下文S/H/U表示场景行，区别于旧交接里的模型/注册/保存证据分层。

## 规范与历史依据

需求覆盖顺序为[#73](https://github.com/Endercloud001/AllMatters/issues/73)及明确修订→其纳入的详细来源→原型收口的实际证明范围。整场回战前要求覆盖旧“续打战中阶段”；原型人工通过保留，但不能代签生产集成。

|依据|固定来源/本地恢复方式|
|---|---|
|H逐敌/限步/面具/服务|[#63完整表](https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226)；旧[v0.1.2追猎笔记](v0.1.2-battle-enhancement-in-hunting.md)仅作搜索辅助|
|完整检查点/兼容|[#67 resolution](https://github.com/Endercloud001/AllMatters/issues/67#issuecomment-6629243377)；`git show 5d4b2c4:docs/research/v0.1.2-integration-decision.md`|
|追猎原型收口|[CLOSEOUT](https://github.com/Endercloud001/AllMatters/blob/0ca54c4/prototypes/hunting-boundaries/CLOSEOUT.md)；`git show 0ca54c4:prototypes/hunting-boundaries/INTEGRATION-INPUTS.md`|
|U教程/正式输入/布局|[REQUIREMENTS](https://github.com/Endercloud001/AllMatters/blob/bad6e14/prototypes/jigsaw-tutorial-input/REQUIREMENTS.md)；按固定提交读，不依赖旧同级工作目录|
|V/S/H/U完整最小输入|[验证交接§§4–7](https://github.com/Endercloud001/AllMatters/blob/8efe31c3d5fc99a2425b84b063d6abb13b7814ae/docs/research/v0.1.2-verification-handoff.md)；`git show 8efe31c3:docs/research/v0.1.2-verification-handoff.md`|
|细看历史/Mystic/TorchHead原算法|[#66研究入口](https://github.com/Endercloud001/AllMatters/issues/66#issuecomment-6600755901)；`git show 864b5d7:docs/research/v0.1.2-doormaker-history.md`及同提交`v0.1.2-sts1-mystic-torchhead.md`|
|既有人工验收|[批次3最新签收](batch-3-test-guide.md#最新状态2026-11-12)：#93/#94用户确认保留；不补写未报告的版本/人数/设备，也不重新标成未通过|

## V01–V14

|V/主题|已有证据与入口|剩余/责任|
|---|---|---|
|V01 几何共享编辑|批次3领域/G树/输入及用户多人操作；[批次3指南](batch-3-test-guide.md)|#88归属分版三端竞争/尺寸；#84–#87新增战斗UI保持编辑/焦点边界|
|V02 自动授权/生成/恢复|批次3真实后台任务/大厅恢复；#94人工确认|#88各幕/预算/七形与生成中退出的分版字段；新增任务验证失效/旧请求隔离|
|V03 正式网络|批次3真实ENet和用户Host+2Clients；#82/#83/Holes新增实际多进程|#88正式三游戏端Send/Broadcast/竞争/断线/Host退出归属；#84–#87新增状态传播不得用角色数代签|
|V04 同源完整点/大厅|#93/#94已验；#82完整状态、#83任务边界及多敌fresh/三端Holes，见逐票记录|适用新增端点、五故障/3存档2在局的分版环境归属；各战斗票不得拼接半份玩家/怪物状态|
|V05 Ancient整体回退|批次3原生奖励/缺席补领及#93人工；兼容迁移保留真实同步器|#88分版两人先后领取与大厅环境；不新增个人半点恢复|
|V06 可靠进入/首次进度|批次3原生入口/票/历史；#82 am_steps合法/非法/ENet，完成根设0边界用户确认|#84–#87事件子战/恢复不二扣；#88两/三端失败出口归属|
|V07 商店|批次3真实库存/动态价及#93人工；#82拒绝服务/删牌sender/货架/滞后端|#88正式双端滞后、重访及恢复分版归属；不能重新声称库存从未保存|
|V08 根房安全/完成/服务|#82权威root快照/全队面具/Heal/遗物及双run自然离开；#83非奖励任务完成|适用正式游戏双端/问号实际房型；战斗票消费当前根快照，不从面具或显示0反推|
|V09 战斗完整恢复|密林首普通、Phrog、Vantom、Matriarch、Waterfall、SoulFysh、Seapunk、Hive首普通、Knowledge、Mystic及三Boss fresh已逐片集成|每票仍核适用敌人/事件/多owner/双Boss完整端点；多数fresh是单owner同进程新大厅，不能代签正式端/新进程|
|V10 逐敌/调度/布局|[密林](issue84-overgrowth-implementation.md)、[暗港](issue84-underdocks-implementation.md)、[巢穴](issue85-hunting-implementation.md)、[荣耀](issue86-hunting-implementation.md)、[Boss](issue87-boss-implementation.md)已多片集成|Matriarch布局在制、Aeonglass正式个人UI待；其他适用布局/输入/死亡缺席/调度按票收口，P87间歇故障未修|
|V11 空箱|#83双版正式整房真实鼠标/Proceed/自然保存恢复已集成，实际ENet等待/奖励拒绝保留|真实双游戏端/设备；未知直接支付、未返回Task后台维持未支持范围，不推成通用取消|
|V12 UI/教程/输入|批次3、#82 HUD/服务、#83整房及多敌完整HP/意图/目标/焦点G证据|U八行逐项：三比例/720p–4K/长译文/重绑/实体设备/实际端归属；模拟输入不代签实体手柄|
|V13 换幕/终局|既有#79幕转换/双Boss用户认可，#82新幕/最终Boss清理；三Boss fresh含自然胜利终局|#88分版标准/A10实际击杀、双Boss首末/WinRun差异；原型或换幕源码不代签全内容|
|V14 兼容/许可|#88 GoldenCompass实际Tezcatara过滤、WingedBoots排除、Flight新局/恢复/大厅拒绝和64种子每日字段/RNG已集成|最终同提交双版包/NOTICE核对、权限及Mod限制/具体第三方范围；见[兼容记录](issue88-compatibility-implementation.md)及[素材来源](issues-82-88-asset-sources.md)|

## S六类

S来源为原coop-stage收口的六类场景；保留#93/#94签收，不用旧原型观测数覆盖新生产行为。

|S/映射|已有证据|剩余/责任|
|---|---|---|
|S1 真多人传输认证断线→V03|批次3 ENet/用户Host+2Clients；#82/#83/Holes真实多进程|#88正式端分版归属；各战斗票新增状态/掉线生命周期单独验证|
|S2 原存档大厅完整阶段→V04|#94六阶段×四名单24及额外人工；#82追猎/#83任务/Holes三轮新大厅|新增端点/故障分版归属；不能用旧原型80观测/40适配代签新怪物|
|S3 Ancient共同继续→V05|批次3原生奖励/副作用/缺席补领，#93人工|#88 public/beta实际环境；战斗增强保持既有奖励边界|
|S4 原生进入计步重访商店→V06–07|批次3入口/库存人工；#82root/steps/ENet滞后/服务G|事件内部不增步、fresh不二扣；#88正式失败/滞后流程归属|
|S5 正式过场任务提示→V02+12|批次3过场/后台/输入/恢复用户确认；#82新提示生命周期|Knowledge新增多owner屏等待/回焦在制；设备/分版集中补，不重做已验设计|
|S6 追猎完整恢复面具逐敌→V08–10|#82生命周期、#83非战斗；#84–#87已集成多片规则/fresh/布局|每票适用剩余敌人/事件/真实Ending/调度/布局；#88组合不代补玩法实现|

## H十三组

H来源为追猎原型原13组；这里只列正式集成及剩余责任，原型工厂数量和人工通过不写成生产通过数。

|H/映射|已集成范围|剩余/责任|
|---|---|---|
|H1 全场景追加/布局→V10|Phrog/Vantom/SoulFysh/Seapunk14场/Glory15场/Queen等正式完整HP意图/鼠标/焦点；蟹原场景保持|#84 Matriarch在制；其他适用场景与真实端逐票补。只有真实加怪/遮挡red才改布局，效果片保持原场景|
|H2 真菌兽/术士→V10|#84真菌生长/孢子/自然Ending、#86Mystic56HP/六随机/HEAL12/BUFF2/资源/fresh|其他适用人数/独立owner/正式端；不照搬上游AI，不以素材存在当行为通过|
|H3 第一二幕逐敌→V10|#84密林暗港多敌规则/固定值/自然死亡/fresh；#85Hive普通/精英/事件及蟹Artifact3|其他完整恢复/事件/布局与来源边界仍归#84/#85，不能以代表怪签整表|
|H4 无厌/知识恶魔→V10|#85三轮单owner选择/效果/展示不入堆、Tender真实屏和Knowledge fresh|双owner首轮在制；死亡/缺席/断线/其他轮多人、正式端与剩余输入|
|H5 第三幕/实验体→V10|#86Mecha/SoulNexus/Flail原生动作；#87TestSubject复生完成后计时/3、6、9/Skittish10/Suck3/MultiClaw/fresh|原调度及适用多人/布局/真实端；public/beta动作差异保留，不能改一版对齐另一版|
|H6 女王→V10|真实死亡一次重燃、六意图/TorchHead50HP/10伤/Minion、出生不攻/次轮攻击/存活+2、fresh/布局|真实端/其他适用意图和设备；来源阻止死亡不得触发，已死火炬不复补|
|H7 沙漏→V10|初值/触发/reset12、每owner、五状态与溢出/Wither升级、完整fresh|#87原NPower图标/真实hover/个人计数UI Ready；适用多人/正式端，模型Amount6不等于个人显示12|
|H8 HP固定值Fatal→V10|各票原生人数缩放/固定Artifact/Minion来源与真实History、自然死亡已逐片验证|逐敌适用组合及真实Ending归属；不把1–4角色扩写成四真实端|
|H9 商店→V07|#82服务拒绝/Used保留/ENet滞后；批次3库存人工认可|#88正式双端/分版重访归属；库存既有证据不重复标未验|
|H10 休息→V08|#82Heal加成/附奖/禁项无历史/遗物禁项、双run投票后自然离开|真实双UI/设备及适用分版归属；public本端Exit不等于全员完成|
|H11 空箱→V11|#83非奖励来源/direct-pay负例/Face/Spoils/空Tween/双版正式整房/等待清理|正式双游戏端/设备；不新增通用第三方取消支持|
|H12 安全面具Boss→V08|#82当前root快照/最后安全/全队面具/最后Boss奖励前清；事件同root及多敌fresh来源|适用事件/双Boss首末/同名事件Boss/真实多端全场；不是凭怪物名清面具|
|H13 HUD→V08–09+12|#82正式顶栏/地图生命周期、N/1/0与末Boss解除句已接权威状态|#88三比例/长文案/设备/实际终局归属；新能力/事件/UI不误报|

## U八组

来源为上方固定REQUIREMENTS的§9；用户整份要求/B方案确认保留，模拟Gamepad与实体手柄分开记录。

|U/映射|已有实现/证据|剩余/责任|
|---|---|---|
|U1 首次六句/档案续读→V12|批次3正式教程/本地档案/Presentation/Recovery及用户过场认可|#88分版退出续读/旧跑局不倒退环境字段|
|U2 阅读进度/帮助/共同阶段→V12|批次3多人正式输入及#94恢复人工确认|#88正式端阶段关闭/焦点；新增选择屏不引入阅读屏障|
|U3 布局尺寸/长译文/非颜色→V12|批次3G树/部分720p–1080p用户反馈，#82服务G及新增多敌1/4角色两宽布局|16:9/16:10/21:9、720p–4K端点/长中文替代译文及剩余敌人；#84–#87各自补G|
|U4 右中键/错误双5秒/几何→V12|批次3正式输入/Feedback；#82解除句5秒生命周期|#88逐case关联及受影响复验；新增窗口不吞/穿透输入，直接callback不代派发|
|U5 手柄遍历/重绑/视区焦点→V12|批次3正式焦点/映射自动检查、B方案确认；新增目标完整左右焦点环|#88分版重绑/任意布局；知识新屏退出回焦；实体手柄由后续具设备者验收，不要求用户补设备|
|U6 自动等待/失败恢复→V02|批次3真正Godot后台/恢复，#94相关人工确认|#88核现有失败出口；新增任务仅对受影响链补证，旧Promise不代签|
|U7 节点脉动/圈选/分歧→V12|批次3探索/地图/首次进度/商店及用户多人操作，#82root提示|#88普通/Boss/Ancient/已访/问号图标与可走归属；不为战斗改拓扑|
|U8 额度安全/追猎BossHUD→V08–09|#82N/1/0、root/全队面具/按时Boss静默/末Boss5秒句/JSON/ENet；am_steps完成根设0已确认|#84–#87剩余能力/恢复/实际末Boss与双Boss；#88联合实际端终局。本房0仍安全不覆盖完成根命令0的下一房|

## 整体交付边界

#88明示依赖为#79/#81/#83–#87；已有签收按实际范围保留，OPEN不等于实现失败。最终同审查提交的完整Check、双版包、NOTICE/依赖声明及人工矩阵仍待；旧包和较早全量不能代签当前源码。各票完整行为/恢复/布局由本票交付，#88只汇总组合。来源与分发核对见[素材来源](issues-82-88-asset-sources.md)，具体交付命令见[开发入口](development.md#实机验收交付)。
