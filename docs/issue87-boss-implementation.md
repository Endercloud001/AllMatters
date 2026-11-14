# #87 Boss 实施记录：沙漏与实验体阶段切片

2026-11-13。起点 `f754aa24dee2cc1f660b6153c834c1a571aadd94`，分支 `feat/issue87-hunting`。本记录只覆盖已审查、已验证的切片，**不代表 #87 完成**。

## 已实施行为

永世沙漏仅在荣耀原生受罚根 Boss 房启用特殊规则：每位目标持独立初值/显示 12 的计数，不授予开场人工制品；原生实际出牌到第 12 次后按顺序生成伤口、灼伤、虚无、晕眩、凋萎，并重置至 12。仍使用原生命令，保留归属、等待、显示事件及满手溢出。

实验体保留真实死亡、适应能力及原生复生任务。第一次复生的确切疼痛戳刺授予调用改成胆小 10、吮吸 3；胆小的多人缩放例外仅作用于本次确切可变能力实例，其他来源保留原生规则。第二次复生原任务成功完成后才启用战斗实例上的共享玩家方回合序号；第 3/6/9 个完整玩家方回合给怪物自身施加细看，该回合末移除。细看阻止额外抽牌，保留原生起手抽牌和直接生成入手。

新增细看 SVG 为本次独立绘制矢量图，不复制 Doormaker 素材；配有中英文文案。未接入的火炬资源不包含在此切片提交中。

## 验证与审查

- 真实失败：原生沙漏第 12 次出牌仍只给凋萎；原生实验体首复生仍给疼痛戳刺；实际两死、第二复生暂停后进入第三阶段，第 3 个完整玩家方回合仍可额外抽牌。之后分别最小修复。
- 最终命令：`pwsh -NoProfile -File ./tools/Test.ps1 -Branch All -Filter 'Jigsaw hunting Boss' -SkipResourceExport`。清理修正后双版各 **4 项通过**，Build/Host 均退出 0，EngineErrors/ModMetadataErrors 均 0，无异常、准备失败或诊断。
- 日志位于此工作树 `.local/issue87/cleanup-review-green-{public-beta,public}.log`，整体命令记录 `cleanup-review-green-all.log`。早期失败日志保留，准确分类见 `.local/issue87/review-scope-current.md`，不将带 green 字样的失败尝试计入通过。
- Standards、Spec 均完成独立实际源码审查；Spec 无此切片内错误，Standards 无硬规范违规。其异步清理建议已修复：断言失败也释放复生等待、取消战斗并等待旧协调任务结束后才恢复测试 patch；上述最终复验覆盖该修正。

测试覆盖 1–4 个原生战斗角色、混合归属 11/12/13 次出牌、空手/9 张手牌、真实死亡与复生、暂停真实第二复生 `SetMaxHp`、九个真实玩家方回合、每位角色起手 5/额外抽牌/生成入手。合法单人跑局与协调器只提交本地玩家一次结束回合；战斗角色数量不是多个真实客户端。测试用提高的玩家 HP 保证存活，未替换核心计时或死亡 Ending。

版本为 public v0.107.1、public-beta v0.111.0 真实 DLL；public 宿主共用 beta PCK，不能证明匹配 public 正式资源或真实游戏端。未启动游戏/Steam，未安装产物，未修改真实存档。本次是测试宿主编译与运行，尚非最终同提交双版发布包。

## 仍需完成

实验体爪击 9/10 与 3+N 增长/意图、半个玩家方回合内复生完成的专项、沙漏凋萎强化链、女王六意图/阻止死亡/整场一次重燃、火炬/激怒/Minion/原生阶段调度、完整战前与胜利奖励点恢复、旧任务隔离及正式布局/目标/焦点仍待实施验证。真实多端 Boss 计时、匹配 public 资源和设备验收另记，不能由本切片代签。

共享检查点接缝由主控集成分支提供；本切片未重复实现或提前宣称三 Boss 整场恢复已交付。

## Queen 原生重燃与独立火炬（2026-11-14）

源码 `8ef5ff7`，基 `7b08841`，合入 `8e8b480`。当前 public v0.107.1 / beta v0.113.0、RitsuLib0.6.11；本节覆盖上方Queen尚未实施的历史状态，不关闭 #87。受罚顶层Glory QueenBoss保留原Queen/聚合体创建，聚合体真实死亡后把6个受支持Standard意图替换为一次REKINDLE，执行前不能Roll跳过；当前拼图入口不支持beta Dread。

重燃行动才通过原生CreateCreature/Add召两只独立TorchHead：基础HP50，人数缩放一次，Minion1，唯一10伤自循环。原coordinator在1–4战斗角色下出生当轮不攻、下轮直攻；原Queen Enrage自身+2保留，只追加本Queen拥有且存活火炬各2，不补死火炬。真实Feed杀Minion无Fatal、主敌对照有+3MaxHP；女王真死后原生Minion清理并IsEnding。预防死亡、外来聚合体、重复通知、非受罚/事件/非Jigsaw/其他幕/非Boss/无acts/关闭场景均排除；await后重查run/session/map/room/combat/Queen身份，失败原样传播。

真实首个red为死亡后仍PUPPET_STRINGS_MOVE。扩测又发现实际预加载GetAssetPaths→Queen.AssetPaths→GetIntents会创建临时状态机，旧Generate后绑定导致同Queen CWT重复key；改在MonsterModel.SetUpForCombat后仅绑定实际machine，回归覆盖死亡前、排队后、执行后三次资源枚举。final3两版7PASS仍有10 CanvasItem RID warning，不能签零泄漏；两处CreateScene明确Free后final4双版各7PASS、四诊断0且该RID警告消失，其他资源WARN仍存在。实施证据 `queen-rekindle-20261114/.local/executor-queen/{red-public,final2-public,final3-public,final3-public-beta,final4-public,final4-public-beta}-host.log`，首次red资源导入EditorSettings错误按准备诊断单列。

双轴独立实际diff/API审查无阻塞；测试名已收窄Standard。正常hook仅10处单行if的return换行，Standards增量确认无语义变化，最终14文件948+。集成命令 `Test.ps1 -Branch All -Filter 'Jigsaw Queen'` 默认导出新资源，两版各7 PASS；BuildExit/HostExit/EngineErrors/ModMetadataErrors全0，无CanvasItem RID/ObjectDB泄漏。原始日志 `.local/executor-20261114/queen-integrated-{public,public-beta}-host.log`。固定素材源与既有归属写入NOTICE，未安装或启动正式游戏。

正式scene保留原聚合体(1207,709)/女王(1606,695)，追加火炬(1150,695)/(1330,695)；scene/factory/Spine idle及halberd_slam/双语已证，不能由slot坐标签完整1/4人×紧凑/宽屏意图/血条/目标/焦点。完整战前与奖励fresh恢复已拉独立后续片，真实远端与暂停Boss间歇故障仍未修复，三次probe绿不作修复证据。

## Queen 完整战前与终局胜利点 fresh（2026-11-14）

源码 `5ac1e5d`，合入 `09d91c2`，两文件525+且生产/共享checkpoint零改动。原生GenerateRooms后用已核SetBossEncounter明确本例Queen，再session.Enter取得生产RetainRoomCheckpoint；两次新大厅不再次设Boss。原开场独立采样完整玩家保存字段、run/owner/monster RNG、原生Acts/history/coords、完整拼图map和敌人slot/HP/powers/intent。审查P2指出Player.ToSerializable没有战斗牌堆，已补逐owner五堆有序card.ToSerializable、当前Energy/Block/powers；真实手牌移弃牌、损血/金币/各RNG改变及一只火炬死亡/激怒后实际保存，再以原expected完整比较，未从恢复对象回填。

第一次LoadRunLobby.AddLocalHostPlayer/SetReady自然SetUpSavedMultiplayer/GenerateMap/LoadIntoLatestMapCoord，取得新run/session/room/Queen/聚合体身份；原始完整开场相同，0火炬、聚合体活、步数/预算/NextEntry/history不重复。再次真实死亡/重燃两只新Torch证明整场once重置，旧聚合体迟到通知不增召。原生Queen真死/Minion清理→CheckWinCondition→实际prefinished保存；第二新大厅恢复终局胜利点，opening/startup增量0、无敌人火炬，原room/map/history/所有RNG按对应阶段精确相同。

两版原生最终Glory Boss默认奖励为空，非Holes的非空pending语义；原/恢复各真实Offer一次，严格验最终Queen身份、ExtraRewards0、Rewards.Count0、synchronizer completed=true和精确空内容，不造ExtraRewards。未验证非空领取UI。首次失败仅ActFloor在setup派生前为0的oracle阶段错误，当前保留setup0断言及实际进房后原楼层比较；第二次失败为误搬普通战斗非空奖励假设，均由两版原生方法体修正，非生产修复。

独立Standards最终硬规范0；Spec P2已修复增量通过。正常hook提交patch与已审一致。实施日志 `queen-fresh-recovery-20261114/.local/executor-queen-fresh/piles-final-{public,public-beta}-host.log`；集成日志 `.local/executor-20261114/queen-fresh-integrated-{public,public-beta}-host.log`；两处命令均 `Test.ps1 -Branch All -Filter 'Jigsaw Queen fresh' -SkipResourceExport`，两版各1 PASS、Build/Host/Engine/Metadata全0，无CanvasItem RID/ObjectDB泄漏。证据为单owner、同进程两次fresh run/session/lobby；新进程、真实远端、布局/设备仍未代签，#87继续partial。

## Queen 正式布局与原生输入（2026-11-14）

源码`372c488`经`f55305f`集成，三文件510+/1−。真实正式NCombatRoom先创建原聚合体/Queen，再原CreatureCmd.Kill→REKINDLE/PerformMove召两只独立Torch。1/4个combat角色、1280×720/1680×720两种物理viewport，1920×1080 CanvasItems Expand逻辑画布（宽屏2520×1080）。真实Visuals/Hitbox、HP hitbox、intent sprite及非空数字label的逻辑/物理几何都在画布内；跨实体显示相互分离且不压其他enemy/player hitbox。两Torch原ATTACK文本实际parsed为10，content22×28、原label62×23、outline12，包络覆盖原frame、不裁剪内容溢出及实际transform缩放的描边，没有缩标签/改动画换绿。

首次beta准备失败在原Kill→StartDeathAnim→SfxCmd.PlayDeath直调空audio owner；只隔离该精确音频边界，真实死亡/通知/召唤链保留。entry2虽1PASS仍缺HP互交oracle；主控从日志发现两Torch HP重叠，补真实readability-red-beta后得到70.19995×23.400024逻辑交集（物理46.8×15.6），遂仅将左Torch marker(1150,695)改为(1050,695)。原右Torch(1330,695)、amalgam(1207,709)、Queen(1606,695)保留；新两血条实际间距19.8logical。数字增量首次Control.GlobalScale编译失败没有native执行，随后使用CanvasItem.GetGlobalTransform基向量长度；仅有效value2-final作最终数字证据。

每只敌人真实viewport MouseMotion进入精确Hitbox/IsFocused，原NTargetManager.StartTargeting和真实press/release后SelectionFinished返回同一node；控制器经真实action检测入模式，仅一次初始GrabFocus后原生左右action走含全部players的整房X序双向环并逐步核GuiGetFocusOwner/IsFocused。测试名收窄为mouse selection and controller navigation，不冒称控制器确认选中。真实TargetingArrow原场景、原Controller Label/hover/owner lifecycle和action清理保留；仅隔离NGame启动/Steam/audio及空地图生命周期，不patch hover/target/nav。每例finally恢复窗口/全局/输入/patch并free节点，未携Phrog死亡冻结。

双轴独立初审及数字37+/4−增量均无硬问题；最终实施树`queen-native-layout-20261114/.local/executor-layout/value2-final-{public,public-beta}-host.log`双版各1 PASS四诊断0无RID。正常hook成功，提交patch与已审patch完全一致。集成`f55305f`默认重新导出资源后执行`Test.ps1 -Branch All -Filter 'Jigsaw Queen'`，双版各9（旧7+fresh1+布局1）PASS、Build/Host/Engine/Metadata全0，无RID/ObjectDB；日志集成树`.local/executor-20261114/queen-layout-integrated-{public,public-beta}-host.log`。

本片是单run owner下1/4 combat角色、模拟鼠标/控制器action证据；不代签所有Queen意图状态、实际设备、新进程/真实远端或整票。Boss原组六项间歇故障仍暂停未修复；三次probe绿仍不是修复。空槽转#86全部12普通/3精英正式场景四组合布局，避免只继续堆Queen局部用例。

## Aeonglass 个人计数与终局完整 fresh（2026-11-14）

测试源码`fff3232`经`8481af1`集成，生产未改。真实acts=[Overgrowth,Hive,Glory]、CurrentActIndex/Checkpoint.Act=2，准备期一次原生SetBossEncounter(AeonglassBoss)后实际session.Enter；开场唯一512基础HP原Aeonglass、无Artifact、每owner一个Target明确的WitheringPresence，Amount保原6而Display/CardsLeft均12，History独立核仅一次。实际13次生成Defend至Discard再AutoPlay，旧counter11与五状态并存；真Ebb留下Block33/EyeLasers，再伤血/金币/Hand→Exhaust与run/owner/monster RNG扰动后实际SaveRun。

新LoadRunLobby生成新run/session/room/怪与presence身份，完整原开场五堆顺序/Energy/Block/powers、敌HP/意图/WitherUpgradeCount、个人counter/display、原生玩家/RNG/acts/map/history/根房一致，重复Enter不再增强或扣步。真实致死→原生CheckWinCondition、BurningBlood回血6及胜利保存，第二新大厅敌人0且startup/opening不增加，最终Glory原生空奖励completed且精确内容保持；不造ExtraRewards，不声称非空领奖。Profile深克隆及原引用/全内容恢复与全局清理保留。

独立Standards/Spec实读595+/0两文件及相关双版消费者均无阻塞；beta缺失AeonglassBoss/CardCmd已实际导出窄核。双版编译0错误，默认资源导出后实施`.local/executor-aeonglass-fresh/first-{beta,public}-host.log`各1 PASS；正常hooks成功，提交patch与已审暂存完全一致。集成树`.local/integration-slices/aeonglass-fresh-integrated-{beta,public}-host.log`双版各1 PASS，Build/Host/Engine/Metadata均0。单owner同进程两次新大厅，不代签真实多端/新进程、正式设备输入或#87整票；暂停Boss原组六项诊断未被本片重跑或签修。

## TestSubject 第三阶段与细看完整 fresh（2026-11-14）

测试源码`d0ca778`经`cc0bc71`集成，生产未改。合法Act2实际session.Enter后，原生Kill/SetReadyToEndTurn经原协调器两次RESPAWN进入第三形态，再真实推进至FullTurns3/Scrutiny1并验证额外抽牌被拒；不改MaxHp、不写phase/clock。旧复生计数2、真实死亡ExtraFields.TestSubjectKills增量2、五堆/HP/Block/金币与三RNG扰动后实际SaveRun。新LoadRunLobby完整重建原第一形态及初始能力/意图、五堆顺序/Energy/powers、run/owner/monster RNG、完整RunExtraFields/acts/map/history/根房；旧clock不存在、respawns0、重复Enter无重增强/扣步。

新战斗再次原生两复生，新clock仅1且额外抽牌实际接受，随后真实最终Kill→原CheckWinCondition、BurningBlood回血及胜利保存。第二新大厅无敌人/无startup或opening增加，原最终Glory空奖励completed且内容一致，不造非空奖励；profile深克隆及原引用/全内容恢复。独立Standards无硬问题（BeforeBoss准备重复为非阻塞维护建议，未扩大本片重构），Spec无阻塞；正常hooks退出0且提交patch与已审暂存完全一致。

实施树`.local/executor-testsubject-fresh/first-{beta,public}-host.log`双版各1 PASS；集成树`.local/integration-slices/testsubject-fresh-integrated-{beta,public}-host.log`双版各1 PASS，Build/Host/Engine/Metadata均0；首轮默认导出匹配资源，集成资源未变复用。单owner同进程两新大厅，不代签原九回合人数矩阵以外的新覆盖、真实多端/设备或整票；暂停的暖Phrog原组间歇故障未被重跑或签修。空槽拉取#82真实MiniatureTent剩余操作及Shovel原生附加项验证。

## Aeonglass 个人计数正式图标与中英悬浮说明（2026-11-14）

8401f1a经b5088fd合入，五文件422+/2-。真实产品red为图标12但正式hover仍每6次仅Wither，safe同版本同文；未改Amount6、原生出牌/五状态命令或布局。新JigsawHuntingAeonglassHoverTips仅精确mutable/visible/hunted Glory Aeonglass的原PowerModel.HoverTips首项替换说明，保留额外原Wither预览，eng/zhs本地化提供12规则、当前剩余及五状态牌名。

新增单人eng、四combat角色九手牌溢出zhs两例，真实原生NPower/texture/label和13次出牌，12/11/1/reset12与1280/1680宽度下held/rehover实际文字、完整可见几何、纯观察前后玩家/五堆/怪物/RNG不变。四模型仅本地Target图标可见；非本地出牌不减本地图标。public接受精确NPower根或该实例直接Icon.Pass子节点真实鼠标路由。卡池寿命涵盖房间及延迟清理，语言finally恢复。

实施最终严格归档在aeonglass-ui-20261114/.local/tests/archives/aeonglass-ui-languages-final-20261114-180304321-ea4c3572673d4fd6895341cd5299a1ca，双版各2 PASS，Build/Host/Engine/Metadata全0。编译Title误用、public路由过窄、延迟回池等准备失败保留，不冒称产品red；Spec新增简中P2已用原四角色例双版闭环。独立双轴0剩余，正常hooks成功，已审patch与提交逐字节一致，.import未暂存保留。集成专项进行中。原Boss偶现故障、完整原场景焦点/布局、真实远端/实体设备与整票未签。

集成b5088fd双版各2严格通过，Build/Host/Engine/Metadata全0；原始日志及summary位于.local/tests/archives/aeonglass-ui-integrated-20261114-181023625-989c9ed033204a339c3a4b8645b49c77。集成运行重新导出含新中英键的PCK，不复用旧文案包。
