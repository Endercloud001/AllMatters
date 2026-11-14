# #84 Overgrowth 已实现切片与审查范围

2026-11-13。本记录仅覆盖当前已实现且有自动化证据的 Overgrowth 部分，不能用于关闭整张 #84。没有正式游戏安装、实机或真实联网多人验收。

## 已实现与证据

| 切片 | 已执行验证 | 边界 |
| --- | --- | --- |
| 普通受罚战斗 FungusBeast | 双版本原生入场唯一追加；HP28、正常敌人；Spore2、最后死亡脆弱结算、Artifact/阻止死亡；Bite/Grow 随机和强制分支，1–4 人 | 不是所有事件来源及完整 UI 验收 |
| Byrdonis | 固定 Regen6、保留 Territorial；独立后续 Regen 来源保留原生人数缩放，双版本 | 原生 debug 人数不代表真实联机 |
| Effigy | Awake 起手、Slow 保留、真实 Wake Strength10 后接 Slash，双版本 | 未逐项实机 |
| Kin | 原生 orb 单次2，Artifact、stack、目标子集及同来源重入反例，双版本 | 保留原生命令链 |
| Ceremonial Beast | 第二阶段真实 Cry，保留 Ringing/原目标，新增 debuff 仅所有活玩家，双版本 | 不代表整场 Boss 演出验收 |
| Phrog | 原生非零约2.5秒死亡、五个实际 NCreature、public 等待后生成/beta隐藏后揭示、旧 run 替换隔离；1/4人×1280/1680实际敌人 hitbox/HP/intent，双版本 | 指针/焦点、血条文字互遮仍待做；四人宿主左侧玩家越界来源待查。Infested为Single能力，当前资源文案不显示召唤数量，不另增数字tooltip |
| Vantom | 原生开场顺序 Inklet/Vantom/Inklet，两默认非 Minion 墨宝、Jab、原生人数缩放 Slippery；未受罚、非拼图、其他受罚 Boss 对照，1–4人双版本 | 事件子战斗实际反例、SecondBoss、HP精确抽样、Jab伤害/后续、保存及实际UI待验证 |
| Colosseum/DenseVegetation 生态追加 | 第二战独立战前点、胜利不回滚；真实治疗后 Fight 恢复；有 scene 的额外敌人 marker，双版本 | 依赖已独立审查的 d8fb141 共享 checkpoint；不是其他事件全部接入 |

以上对应既有日志 `.local/tests/issue84-{fungus-four,byrdonis-green,byrdonis-source-green,effigy-green,kin-source-green,beast-green,slots-green,vegetation-green,event-second-green,event-victory-green,checkpoint-green,victory}-{public,public-beta}.log`，相关最终日志构建/宿主均0、引擎/元数据均0；旧日志不是当前全量重跑。

最新独立验证：`Test.ps1 -Branch All -Filter Vantom -SkipResourceExport` 两版各3项，Build/Host/Engine/Metadata均0；随后 `-Filter Phrog` 两版各4项，Build/Host均0，beta Engine0/public Engine40（共享 beta PCK 的 NHotkeyIcon 类不匹配），无异常/准备失败/元数据错误。准确日志位于 `.local/issue84-phrog/vantom-final-{public,public-beta}-host.log`、`phrog-after-vantom-{public,public-beta}-host.log`。不宣称正式 public 资源验证通过。

## 生产与资源

本生态切片生产范围为 `JigsawHuntingCombat.cs`、`JigsawHuntingActOne.cs`、`JigsawHuntingPhrog.cs`、`FungusBeast.cs`、`SporeCloudPower.cs`，以及对应双语 monsters/powers。Fungus 使用原生创建/人数 HP 缩放链；Vantom 专属生成要求 Overgrowth、独立唯一房间、无父事件、同一遭遇、受罚 Jigsaw Boss/SecondBoss，不更改原生 Inklet 行为。Phrog 布局仅专属受罚遭遇：w1+45x、w2+15x、第五第二排、仅 beta w4-24x。

Fungus 五原资源来自 MT-SUPER-POWER/STS2-Balance-MOD `d9eff9e067b07689e722c5283e7c3e7c451961ca` 的 `Assets/ActsFromPast/ActsFromThePast/monsters/fungi_beast/`。PNG/骨架逐字节相同，场景/atlas/tres 改本项目路径并去外来脚本；既有 Balance/ActsFromThePast/Mega Crit 归属边界在 NOTICE。Spore PNG 与用户授权原件逐字节相同，代码 GPL 不重新许可用户 artwork。两份新 PNG import 是实际新增资源的元数据，不是既有 import 的行尾改动；不包含缓存 `.godot`。

## 集成与剩余工作

工作树起点 90c7970 较集成分支旧。七个共享 seam 文件与 d8fb141 逐文件规范化行尾比较完全一致：Colosseum、NativeDenseVegetation、JigsawCheckpoint、JigsawNativeSession、JigsawEventCombatCheckpoint、JigsawCombatCheckpointTests、JigsawRecoveryNetworkTests。本轮不再次暂存这些文件；合并必须保留 d8fb141 的修复与 HelloWorld 中独立 checkpoint 注册，不用旧基覆盖。Flight hook 等基线后续变化由主控集成时处理，当前没有尝试撤销。

当前生态暂存范围只注册 HuntingCombat 测试；工作版本 HelloWorld 的共享 checkpoint 注册保留在索引外。依赖整合 d8fb141 后再做最终构建与全量检查。事件生态测试保留四个已执行用例，不重复纳入独立 seam 测试。

整票尚缺其他 act/全部事件来源、上表未覆盖项、必要真实 UI 输入/焦点、正式双资源包和联机验收、最终完整 Check、双轴独立审查。分项测试通过不代表整张工单已完成。

## 独立审查与 Phrog 宿主清理返修

2026-11-13，Spec 独立读取原24文件暂存差异、规格及双版目标方法，当前生态切片未发现可确认的规格实现错误或越界，整票条件仍按上表列为部分/待交。Standards 同样实际读取差异及源码，发现一项 P2：NodeHost覆盖全局屏幕震动/快捷键管理器后释放自身对象，却没有恢复原引用，后续UI可能访问已释放对象。

返修先经真实 beta 场景清理反例确认失败，再捕获精确原 NGame/NRun 及三个原引用。清理先完成原生Room退出（它仍需活的本scope快捷键依赖），finally恢复原owner引用，然后释放本scope依赖并解除测试patch，不向替换后的全局实例写回。新增用例覆盖正常清理、受控断言抛出、全局实例替换以及清理后原生屏幕震动与快捷键实际消费。

最终 `Test.ps1 -Branch All -Filter Phrog -SkipResourceExport` 双版各5通过，Build/Host/Metadata均0，无异常或准备失败；beta Engine0，public Engine56全部已知共享beta PCK的NHotkeyIcon类不匹配，不代表正式资源通过。日志 `.local/issue84-phrog/cleanup-phrog-{public,public-beta}-host.log` 与 `cleanup-phrog-green-dual.log`，真实红及精确返修说明见 `cleanup-working-review.md`。尚待独立返修复审，不把原四项旧结果当当前五项验证。

独立Standards返修复审已实际读取当前index、精确修复差异、双版原生退出消费者及红绿日志，确认原P2闭环、无新增阻塞项。实例替换用例覆盖未挂载Room的scope，不扩写为任意已挂载场景切换生命周期验收。原Spec结论无当前切片实现错误，仍保留全部partial范围；本次审查者未运行测试。

## 接入集成分支与组合回归

2026-11-13，旧工作树完整备份到 `.local/issue84-integration-20261113/`：24文件索引二进制完整patch、31个变更路径的工作字节、所有索引blob、原始index、两个未跟踪共享文件、工作差异和本地提交/stash记录均保留并核对。新增文件暂时移到备份后，工作树恢复干净旧基90c7970，再通过正常merge提交0c94ef2消费集成54a6cc0（含Matriarch281110c）。未reset、rebase、删除工作树或丢弃旧stash。

仅重新应用已审24文件索引patch；唯一冲突为Hello注册链，保留集成分支的Compatibility/Flight/Checkpoint/Underdocks/Recovery/ActTwo/ActThree/Boss注册，另加本片HuntingCombat入口。七个共享seam直接取新基，逐文件无差异；NativeSession的两条Flight检查、Tender素材说明与Scrutiny双语能力均保留，没有复制旧工作版本覆盖它们。

首次fresh资源导出后的组合回归在beta复现新边界：无Jigsaw会话、无幕数据的原生Vantom生成被新作用域过早读取Act而抛异常。增加真实原生生成回归后先确认同一失败，再将条件调为先确认有效Jigsaw会话与受罚根、后读Act，未移除任何限定条件。失败日志为`ecology-red-public-beta.log`及`guard-red-public-beta.log`，不归为共享资源诊断。

最终命令 `Test.ps1 -Branch All -Filter JigsawHunting -SkipResourceExport` 使用本轮已重新导出的资源，两版各25项通过：ActOne15（含Phrog5与新增无幕反例）、Combat6、EventCombat4。Build/Host/Metadata均0，无异常或准备失败；beta Engine0；public Engine56，经逐条分组全部为共享beta PCK的同一NHotkeyIcon类不匹配。准确日志在备份目录的`ecology-green-{public,public-beta}.log`及`ecology-green-all.log`。未运行最终整批Check，也不代表匹配public资源、真实设备或多端验收。guard增量另行独立复审后才纳入提交。

guard追加独立Spec/Standards复审均已完成：两名审查者实际读取working相对index的精确两个文件差异、相关源码/原生方法、真实负例及红绿日志，均无可行动发现；未自行运行测试。原先所有会话、人数、追猎根、幕、唯一房间、父事件及遭遇身份条件均保留，仅调整短路顺序。此返修与原24文件切片一并提交，不据此扩张整票完成范围。

Phrog 在集成全量顺序下的事件回调热身与画布隔离返修、真实红绿证据、双轴审查及最终验证状态另见 [Phrog 原生 UI 测试隔离诊断](issue84-phrog-test-isolation.md)。该返修仅修改测试宿主，画布验证范围明确为默认/Auto 的 Expand 模型。

## 密林首普通完整战前与胜利奖励 fresh（2026-11-14）

源码`c7dbd18`经`2c44d4b`集成，仅新增ForestRecovery测试及Hello注册两文件640+，生产与共享checkpoint未改。目标public0.107.1/beta0.113.0、RitsuLib0.6.11，匹配资源；首个真实受罚Overgrowth普通`session.Enter`沿固定seed自然选遭遇，没有挑选或替换Encounter。独立采样before-roll与原开场，完整比较玩家保存字段、五堆有序`Card.ToSerializable`、Energy/Block/powers、run/owner/monster RNG、Act/map/history、敌人slot/HP/powers/intent及菌兽私有`consecutiveBites`。

真实BITE6、独立克隆RNG预测40%分支（必要时第二次BITE强制GROW）、GROW获得Strength3，再BITE9；source实际保存时counter1/Strength3。新LoadRunLobby ready沿原SetupSavedMultiplayer/GenerateMap/LoadIntoLatestMapCoord重建新run/session/room，完整原开场恢复，counter0/Strength0、唯一菌兽HP28只缩放一次、Spore2且无Minion；再次进入同根不扣第二次步数。真实移牌、损血、金币和RNG扰动使回退比较能检出错误，不只比较未变化的空状态。

原生命令杀死其他敌人，再令菌兽最后死亡，孢子实际增加Vulnerable2并进入Ending；原CheckWinCondition/Player.AfterCombatEnd清理能力与Block、原BurningBlood回血6后自然prefinished保存。source原奖励生成非空金币/卡/药后skip，第二新大厅从胜利点重建，无新敌人或opening；保存前boundary与生成后完整玩家/RNG/map/history及奖励内容分阶段取expected。原同步领取实际改变金币、卡身份/升级与药水，独立预测本端profile发现；重复领取拒绝且完整状态不变，原Proceed只完成一次，不手调Complete或末尾补Save。完整SerializableProgress经JSON往返与原FromSerializable隔离，finally恢复原profile引用并核完整原内容不变。

独立Standards/Spec均0发现，实际审查完整640+及原生消费者；正常hook后patch与已审内容一致。实施树`forest-fresh-20261114/.local/executor-forest-fresh/`中最小`entry1-beta-host.log`与`final-public-host.log`各1 PASS，`related-final-{public,public-beta}-host.log`各6 PASS。集成`2c44d4b`执行`Test.ps1 -Branch All -Filter 'Jigsaw Forest fresh' -SkipResourceExport`，双版各1 PASS，日志集成树`.local/executor-20261114/forest-fresh-integrated-{public,public-beta}-host.log`；以上Build/Host/Engine/Metadata全0，无RID/ObjectDB诊断。证据仅单owner同进程两次新大厅，不代签其他#84敌人的完整恢复、正式布局、真实多端或新进程。

## Phrog 五虫完整显示与原生输入（2026-11-14）

源码`3961924`经`22a2cc3`合入，限定`JigsawHuntingPhrogLayout`及原`JigsawHuntingPhrogNodeTests`两文件。原Phrog约2.5秒非零死亡、public等待及beta隐藏/揭示链保持；下排四个marker X为915/1200/1485/1770，第五X1485、Y相对第一槽上移270。原Phrog marker、四下排Y、原生死亡/行动/生成模型均未改，不扩到其他遭遇或非受罚来源。

beta首真实几何red为六对显示/目标相交，含HP互压63.65logical宽、下排意图与第五血条互压；`first-native2-beta-host.log`保存原证据。扩展完整HP标签/描边oracle后，public又在真实导航后捕获第三虫意图与第五虫HP交叠72×5.77logical（`complete-hp2-public-host.log`）；第五槽再上移30，按该观测留约24logical间距，不放宽断言。`upper-row-public-host.log`实际1 PASS、四诊断0覆盖修复后的1/4角色两宽。

正式矩阵检查1280×720和1680×720真实viewport/画布变换；HP为实际current/max、完整文字/最小内容及描边包络，意图为自然首个非stun招式的实际sprite、数值内容/溢出/outline。每只敌显示彼此及与其他敌人/所有玩家hitbox分离；真实鼠标motion/press/release必须返回同一虫实例，左右`InputEventAction`走包括全部玩家的完整原生焦点环，导航后再量显示重叠。前后原生玩家序列化、跑局/怪物RNG、history、HP/招式/能力不变；不据此声称所有玩家显示包络或所有后续意图均已穷举。

新增输入scope补真实挂树NGame、原Target/Controller/arrow、screen tracker、TopBar焦点代理与真实energy factory；保原Activate/Enable/导航/输入，隔离游戏启动/音频生命周期。public热键读取真实隐藏DevConsole可见性依赖，保存并恢复原引用；不是patch热键消音。旧`input-tree2`虽1 PASS但Engine860、旧`final-phrog-public`虽7 PASS但Engine128均保留为失败夹具证据，不当最终green。新增scope的异常退出按源码审查核原引用/TestMode/InputMap/Harmony清理；旧NodeHost.Cleanup不冒称单独覆盖了新scope所有异常路径。

独立Standards初审/增量无硬标准阻塞；Spec发现HP文字包络漏检及public输入缺console，均返修实读关闭，随后新几何red也按生产一行改动复审。正常hooks退出0，提交451+/27−与已审patch逐字一致；实施树`.local/executor-phrog-layout/upper-row-related-{beta,public}-host.log`双版各7 PASS，Build/Host/Engine/Metadata全0，涵盖真实死亡、原行动、backend warmup、旧room隔离、响应式地图前置及清理。集成`22a2cc3`再执行同组，集成树`.local/integration-slices/phrog-layout-integrated-{beta,public}-host.log`双版各7 PASS，四诊断全0。本片不代签暂停的#87 Boss原组六项故障、完整fresh、真实设备/游戏多端或#84整票。

## Vantom 与墨宝完整显示及原生输入（2026-11-14）

源码 `d9e72b2` 经 `307e829` 合入，新增专用 `JigsawHuntingVantomLayout`、八个独立布局 case 和 Hello 注册三文件819行。Vantom 原场没有 scene/slot；仅精确受罚 Boss/SecondBoss、原 `[Inklet,Vantom,Inklet]` 三个无槽节点生效。测原命中框/HP 容器/标签包络后，以24 logical间距仅水平移动完整 NCreature 子树，再调用原 `UpdateCreatureNavigation`；不改变模型、比例、纵向或其他遭遇。

首轮 beta 真几何 red 为四角色紧凑宽左右墨宝血条各交 Boss 血条约9.3/21.3 logical，日志实施树 `.local/executor-vantom-layout/native-generation-beta-host.log`（Build0/Host1、无PASS、Engine/Metadata0）；专用布局后原最小例 `spacing-beta-host.log` 为1 PASS、四诊断0。原生按生成插入序抽HP且CombatId参与怪物RNG，前插墨宝会自然改变Boss样本，因此撤回 safe/hunted HP/招式/RNG跨名单精确全等的假oracle，保留原生成与合法HP、原Slippery多人缩放及同一场状态快照。

矩阵为Boss/SecondBoss × 1/4角色 × 1280/1680×720八独立case，另合跑原Vantom四例。包含完整HP文字、最小内容及outline，原首招意图图标/数值及描边；safe实际走同原生焦点阶段，逐实体对记录旧玩家显示/显示及显示/hitbox交叠面积，hunted不得增加且新增墨宝不继承豁免。真实鼠标必须返回原三个不同实例，左右方向输入走包含玩家的完整原导航环。Initialize后、resize前采Creature/Monster/NextMove对象身份、HP/powers/monster RNG、玩家HP及完整Run RNG，resize后和输入后各核不变；完整RNG启用IncludeFields以包含beta counter/state0–3。初始布局hook发生在该动态快照之前，其不改模型由专用生产仅写Node.Position的源码审查支持，不冒称动态前后证明。

旧 `full-related-{beta,public}-host.log` 虽各12 PASS但Engine各16，均来自SecondBoss原Act未选择Boss而绘图报错，不作为最终绿。测试改为GenerateRooms后通过原Act公开API设第一/第二Boss、原MarkRoomVisited并核PullNextEncounter/HasSecondBoss/访问计数；`secondboss-fixture-beta-host.log` 最小1 PASS四诊断0。Standards发现默认序列化丢RNG字段，Spec发现晚快照/玩家pair漏检，均补齐后独立实读index增量关闭；初始生产及真实API范围继承已审结果，未用reviewer摘要代替源码。

正常hooks退出0，提交patch与已审三文件逐字一致；实施树 `.local/executor-vantom-layout/final-rng-{beta,public}-host.log` 双版各12 PASS，Build/Host/Engine/Metadata全0。集成源码 `307e829` 执行相同双版相关组，集成树 `.local/integration-slices/vantom-layout-integrated-{public-beta,public}-host.log` 双版各12 PASS，四诊断全0。完整fresh、全部后续招式、真实游戏多端及实体设备仍归#84后续，不签整票或暂停#87原组六项诊断。