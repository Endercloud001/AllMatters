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
