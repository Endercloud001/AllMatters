# #83 空宝箱、任务完成与保存实施记录

实施起点：`46a2d5b605fe53fd8314bc837234b47bc691ff36`（已含 #82）；已快进至集成分支 `f754aa2`。状态：完整 Check 和双轴增量审查通过，待主控提交集成。本记录不代表人工验收或允许发布。

测试沿用 #73 已确认的 D/N/G/M 接缝：权威根房状态、真实原生开箱/任务/保存入口、正式空箱节点动画与输入、真实双端消息。public v0.107.1 与 public-beta v0.111.0 分别记结果；public DLL 使用 beta PCK 的宿主不能代签 public 正式资源。

## 红绿证据

|切片|红阶段|绿阶段|
|---|---|---|
|受罚箱 Face 与 Spoils|`smoke.log`：public 真实 Enter→DoNormalRewards 后任务未完成，原生 ShouldGenerateTreasure=false 早退|`spoils-green.log`：双版本各 1 项；Face 消耗、金币零、真实牌/地图任务移除、历史一次|
|无 Face、普通安全箱|`native-rewards-red.log`：受罚箱仍产共享遗物|`native-rewards-green.log`：双版本各 2 项；受罚无金币/遗物，安全箱金币 42–52 且有遗物，安全 Face 行为不变|
|异步附奖|`extras-red.log`：生成的奖励仍在列表；`extra-completion-red.log`：生成挂起仍早完房间|`extras-green.log`、`extra-completion-green.log`：真实同步/异步 hook、非奖励工作一次、同一完成请求恢复|
|完整 before-open 保存|`checkpoint-red.log`：真实移牌 hook 挂起，历史已写、牌仍存在|`checkpoint-green2.log`：双版本真实 IO 保存/恢复为牌 1、完成任务 0、pending-entry 1、已走步数 0|
|已开始任务不能早完|`completion-red.log`：收到完成时真实 Spoils Task 挂起|`completion-green.log`：双版本同一完成请求在真实任务结束后恢复|
|来源尚未开始|`source-not-started-red.log`：DoNormalRewards 之前的完成意图立即成功|`source-not-started-green.log`：双版本等待真正原生来源到达并完成|
|旧任务 drain|`recovery-drain-red.log`：恢复提前替换 RunManager|`recovery-drain-green.log`，后加强于 `all-targets.log`：真实附奖 hook fault 与牌移除挂起共存，首个 fault 不释放 drain，旧 hook 恢复时全局 run 仍是旧 run，全部结束才安装 restored run|
|完成但未投票的保存|`completed-red.log`：保存退回开箱前，重开箱|`completed-green.log`：双版本真实 IO 保存完成根，current/pending 为空、completed 含原根、计步不重复|
|真实双端|单端竞态先红后绿；网络 fixture 初次故障是 sequence 太低及误读单人路径，不能列产品红例|`network-third.log`：双版本各 1 项真实 ENet Host+Client；3 原始角色/2 在场；完成 intent 先到，native ChestOpened 后到；两端实际 Spoils 挂起/释放，同一 managed intent 继续，双端各自保存 multiplayer 完成点，缺席角色不补工作|
|真实空 Tween|早期宿主准备错误不列行为红例|`tween-completed.log`、`all-targets.log`：beta 真实完整 OpenChest 与 Proceed；public 真实 collection 动画/通知。宿主日志清理后以最终单例日志为准|

初始化时 beta 原始全量 419 项通过；public 编入首个新增测试后在该目标行为断言失败，不能列为 public 全量通过。没有启动游戏/Steam、没有写真实 mods 或存档。

日志位于本票 worktree 的 `.local/issue83/`，不提交生成日志。早期 `all-targets.log` 的宿主错误已修复；最终 `review-slices-final.log` 两个 DLL 各 11 项通过，无 Godot ERROR、无 RID/ObjectDB 泄漏警告。`network-third.log` 两个 DLL 各 1 项真实双进程通过。

首轮完整 `final-check.log` 在两个版本的 #83 首例前遇到全量顺序特有的宿主失败：#82 已向 Godot 注册原生脚本，#83 再次注册导致 MegaLabel 路径重复。两个版本 Build 均成功（0 警告/0 错误），不能把这一轮称作完整通过。最小修复为复用现有 `JigsawHuntingNativeTests.RegisterScripts`，不再维持第二个注册状态；随后运行 `final-check2.log` 验证全量实际次序。

最终命令 `./tools/Check.ps1 -Branch All` 退出 0，见 `final-check2.log`：PowerShell、workflow、whitespace、placeholder、fresh resources、双版本测试与双版本 Build 共 9 类检查全部 PASS。public-beta v0.111.0（product `41cef1e…`）431 项通过，public v0.107.1（product `5926027…`）433 项通过；包括各自 11 项票内切片与真实 ENet treasure-hunting absent。双版本 Build 均 0 警告/0 错误，开发包位于本 worktree `dist/public-beta/AllMatters` 和 `dist/public/AllMatters`，未安装或发布。全量含现有明确注入故障场景的日志输出，不能称整个日志没有 ERROR；票内最终专项无宿主 ERROR。

## 实现与完整点

`JigsawHuntingTreasure.cs` 拦截具体来源：Hook.ShouldGenerateTreasure 的共享遗物、OneOffSynchronizer 的普通金币、SpoilsMap 的金币，以及当前宝箱 Room 的标准 RewardsSet 最终列表。普通金币仍调用真正 TryHandleSpoilsMap；Spoils 仍执行原生 CompleteQuest 和 RemoveFromDeck，包括任务历史、移牌 hook 和地图任务移除。Face 沿用原生进入 hook。列表清理等待同步/异步生成 Task 结束，保留非奖励工作。

`JigsawNativeExploration` 保留会话内原始 Task 引用和来源到达通知。完成意图必须等该玩家的原生来源在本端开始，再等待任务，并重新收集直到集合稳定；空集合不代表完成。实际 NTreasureRoom.OpenChest Task 也受观察，本地空 Tween 未完不能离开。

进入确认时保留已有完整 before-roll 存档对象。真实任务挂起或失败时，RunManager.ToSave 返回整个旧点，玩家、牌组、任务历史、RNG、拼图 pending-entry 一起回退。低优先级 postfix 再复制整点，避免 RitsuLib 给旧玩家快照附当前地图文档。全员完成且真实任务成功但未投下一房时，只在保存副本中完成根房，现场地图与投票不变；重载回探索地图，不重开箱。逻辑仅作用于保留了整点的受罚箱；临时替换 slot 前必须存在原 slot，并在 finally 还原。

## 退出、断线与故障

原生 Spoils/移牌/附奖 hook 没有取消令牌。关闭 Mod 等待者不能终止原 Task，因此暂停 CloseToMenu、原生 ReturnToMainMenu、单人 LoadCheckpoint 和联机 entry reload 都须等原 Task 实际结束后再换全局 run。断线后的 SaveSession 仍可定位旧 run；Close 立即停止接受新宝箱来源，停止后的开箱/生成入口不启动新工作。

退出等待前，启用保存时显式 SaveRun，再用 WaitForSave 确认写入结果；未完箱由 ToSave 返回旧完整点。失败且仍在局时恢复接受来源，暂停菜单显示现有失败提示并留局。已经 fault 但无挂起的 Task 也算未结算。Drain 的 WhenAll 收齐全部 sibling，不因第一个 fault 提前清理；日志记录 entry 和 exception，fault 不算成功完成。

等待提示解释会保留会话直到原任务结束。第三方 hook 若永久不返回，不能安全强行取消并开新局，只能继续等待；已成功写入的旧整点保留在磁盘。结束进程后重载可回旧点，不能声称任务已取消。

## 支持来源与边界

目标覆盖原生普通宝箱金币、共享遗物、Spoils Map、现有 Face 消耗，以及标准 RewardsSet 列表中的同步/异步附奖，保留这些来源的非奖励工作。

不全局取消 GainGold/Obtain，不返回有 Owner 但未实际入库的假遗物，也不先发奖再回收。未知第三方直接命令支付、自建无 Room 奖励集、未返回到 hook Task 的后台发奖不在已支持范围；不能将最终清列表描述为通用禁止一切发奖。

public v0.107.1 与 beta v0.111.0 分开编译运行。public DLL 借用 beta PCK，整房 Spine/ChestSkin 不兼容；public 只验证真实 collection 空箱 Tween、延迟 callback 与原生通知，不能签正式 public 整场 OpenChest/Proceed。beta 执行真实整房链。没有手动调用 CompleteWithNoRelics 来伪造动画完成。

新 Harmony 目标 NGame.ReturnToMainMenu 已分别查询真实方法体：`.local/api/queries/public-v0.107.1/MegaCrit.Sts2.Core.Nodes.NGame.decompiled.cs:797` 和 `public-beta-v0.111.0/...:1075`，均为无参数 Task，顺序为 FadeOut→Preload→RunManager.CleanUp→LoadMainMenu。原生断线链由 RunManager.LocalPlayerDisconnected→ReturnToMainMenuWithError→NGame.ReturnToMainMenuAfterRun→ReturnToMainMenu 到达此入口。public 导出版本证据见 `ngame-public-api.log`。

审查补充切片包括真实 AfterModifyingRewards 内直接 GainGold 7/Obtain Lantern 的穿透反例（列表清空、金币 +7、返回遗物 Owner 与实际入库一致）、已完成点实际 SetUpSaved→LoadIntoLatestMapCoord 回 MapRoom，以及实际 Pause CloseToMenu/已关闭 session 的 ReturnToMainMenu 生产 prefix。菜单 fixture 仅替换 drain 之后的原生 fade/preload/场景所有权尾部，保留真实 CleanUp 为可观察效果；不能将它描述为跑过整个正式菜单资源。

## 第 4 批人工步骤

由主控统一提供集成包和安装步骤，本票不单独安装或启动。

1. 正式 public/beta 各进受罚箱：无 Face 时无金币/共享遗物/标准附奖；有 Face 时消耗；Spoils 完成并移牌而无金币。对照安全箱和普通模式正常奖励。
2. 两端分别开箱，空箱动画后能继续；远端未完成时本端不能代签。实机确认键鼠、映射和焦点；有设备者补实体手柄。
3. 任务/附奖未结束时保存退出，应显示等待；任务结束后退出。重载得到完整开箱前点或完整完成点，无历史已完成而牌未移除的拼接状态。
4. 两端完成但未投下一房时保存重载，回探索地图；原箱不重开，Face/Spoils/hook 不重复，步数不重复扣除。
5. 一端在任务未结束时断线，保留旧点，旧任务结束前不装新 run。永久挂起按上述限制处理，不当作已取消。
6. 第三方按实际 Room/Task 路径逐项确认；直接支付和未返回 Task 的后台奖励单列为未支持，不能由原生场景结果推断。
