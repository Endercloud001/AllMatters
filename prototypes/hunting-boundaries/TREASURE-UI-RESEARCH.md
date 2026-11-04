# 空宝箱原生界面：后续最小实验输入

2026-11-04，research 定向补查；供[验证追猎逐敌机制、房间服务与战斗布局边界](https://github.com/Endercloud001/AllMatters/issues/70)使用。只读真实程序集并导出方法体，**本笔记没有运行证据**，不修改玩法、不选择正式取消奖励架构。既有真实行为证据仍以 BATCH-RESULTS.md 和原始日志为准。

## 来源与复现

本工作树 `.local/research-treasure/{public,public-beta}/` 新导出五个类型：`Nodes.Rooms.NTreasureRoom`、`Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicCollection`、`Nodes.TreasureRooms.NTreasureButton`、`Multiplayer.Game.TreasureRoomRelicSynchronizer`、`Rooms.TreasureRoom`，前缀均为 `MegaCrit.Sts2.Core.`，文件后缀 `.decompiled.cs`。下面 P/B 行号均属于本次导出，不是官方源码行号。游戏代码只读，不随原型分发。

- P：public v0.107.1，ProductVersion `0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`；实际 DLL 为主工作树 `.local/api/public-github-0.107.1/sts2.dll`。
- B：public-beta v0.111.0，ProductVersion `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`；实际 DLL 为本机 beta 安装 `data_sts2_windows_x86_64/sts2.dll`，只读。
- 两次导出命令退出均为0，来源头与文件清单在 `.local/research-treasure-public.log`、`.local/research-treasure-public-beta.log`。没有执行上游构建脚本或游戏。
- 既有上下文：[服务入口调查](https://github.com/Endercloud001/AllMatters/blob/research/hunting-entrypoints/docs/research/v0.1.2-hunting-services.md)及完整[追猎规则](https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226)。它们不是此次新运行证据。

复现导出，在本工作树执行（`$branch` 分别为 `public`、`public-beta`）：

```powershell
./tools/ExportApiReference.ps1 -Branch $branch -IlspyPath E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/.local/tools/ilspycmd.exe -Types MegaCrit.Sts2.Core.Nodes.Rooms.NTreasureRoom,MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic.NTreasureRoomRelicCollection,MegaCrit.Sts2.Core.Nodes.TreasureRooms.NTreasureButton,MegaCrit.Sts2.Core.Multiplayer.Game.TreasureRoomRelicSynchronizer,MegaCrit.Sts2.Core.Rooms.TreasureRoom -OutputDir .local/research-treasure/$branch
```

## 最小真实调用链与容易误测的完成通知

`TreasureRoom.EnterInternal` 在 preload/Create 后调用 `Hook.AfterRoomEntered`，再 `BeginRelicPicking`；真实 Face 消耗等非奖励行为应沿这条链保留。`TreasureRoom.DoNormalRewards` 调 OneOffSynchronizer，`DoExtraRewardsIfNeeded` 逐玩家生成额外 RewardsSet 并 Offer，**只等待本地玩家任务**。因此后者返回不能证明所有玩家必要流程结束。来源：B `Rooms.TreasureRoom`:37、74、84；P 同名方法。

当 ShouldGenerateTreasure 对所有玩家返回 false 时，`BeginRelicPicking` 创建空列表和玩家票，再调用 `EndRelicVoting` 把 CurrentRelics 置 null；这里**没有调用 RelicsAwarded**。来源：B `TreasureRoomRelicSynchronizer`:89–130、309–313；P 同名方法。

真正空箱界面调用顺序为：

1. `NTreasureRoom.OpenChest`：banner 进入、继续按钮禁用、宝箱动画启动、普通/额外奖励方法。
2. Collection.`InitializeRelics`：根据空 CurrentRelics 设置 `_isEmptyChest`，隐藏全部遗物holder，调用 `SpawnEmptyChestVfx`，显示本地化空箱文案/烟雾/真实 tween。
3. Collection.`AnimIn`：创建真实 Tween，空箱分支在 **1秒** 后回调 `CompleteWithNoRelics`，后者发送空 results 的 RelicsAwarded。
4. Collection.`OnRelicsAwarded→AnimateRelicAwards`：设置 RelicPickingBegan 的 TaskCompletionSource；空 results 不 Obtain 遗物。多人空结果分支仍等待 Cmd.Wait(0.7f)，再完成 RelicPickingFinished。
5. `OpenChest` 等待 began/finished 后恢复 Proceed、banner 退出、地图可旅行、集合退出和宝箱恢复显示。

来源：B `NTreasureRoom`:291–329；P:245–282；B Collection:238–254、317–331、372–385、427–517；P Collection:159–175、290–305、344–434；B Synchronizer:332–336，P 同名方法。

**实验不得预先手调 CompleteWithNoRelics 来让界面完成。** 那只能证明通知 API；还可能与真实 tween 回调重复触发，Collection 使用 SetResult 而非 TrySetResult。若要验证真实动画到完成链，应等待真实 SceneTree 帧，并观察单次原生回调、两个原生任务完成、Proceed 恢复和奖励不变。只读代码不能代替上述运行。

## 宿主依赖及版本分叉

| 边界 | public | public-beta | 下一实验必须明确 |
| --- | --- | --- | --- |
| 宝箱 UI | NTreasureRoom 自己持有 NButton、`%ChestVisual`、MegaSprite，Ready 内配置骨架/skin；OpenChest 把动画 timeScale 从0改1 | NTreasureButton 在 Ready 绑定 `%ChestVisual`；Setup(act) 配置骨架；OpenChest 调其 AnimOut/AnimIn | 不能把 beta 宝箱节点树当作 public 原版场景。应优先真实 beta 场景自动实验；public 原资源未提供时只做有界方法/手建依赖实验并明确层级 |
| Collection动画签名 | AnimIn(Node chestVisual)、AnimOut(Node chestVisual)，控制宝箱 modulate | AnimIn()/AnimOut()，宝箱由 NTreasureButton 控制 | 调用原生对应版本方法，不加通用假动画来冒充原版 |
| 场景/单例 | ActiveScreenContext、RunManager同步器、NMapScreen、NRun.ScreenStateTracker；原生 holder/hand/vote子树 | 另有 Ready/ExitTree 的 NGame.Set/ClearScreenShakeTarget | 如果局部替代 UI 外围单例，记录每一项；不要替代待测 OpenChest/Collection tween/完成通知 |
| 工厂 | NTreasureRoom.Create 在 TestMode.IsOn 时返回 null | 同左 | 仅在同步场景工厂范围临时关闭 TestMode，随后立即恢复；不要让该更改扩散到奖励或保存路径 |

来源：P NTreasureRoom:133–200、245–282；B:193–245、291–329；B NTreasureButton:76–124；P/B Collection上述动画成员。Collection.Ready 必须先绑定 holder/hand/label 并订阅同步器事件，然后 Initialize(run) 设置 hands 和 run；顺序来源 B:192–232，P同名方法。每次实验使用全新 Collection，完成源不可复用；按 ExitTree 正常解除订阅，等待 tween/异步任务结束后再释放。

## FTUE 与真实存档安全边界

两版 `NTreasureRoom.RelicFtueCheck` 都会读取 `SaveManager.Instance.SeenFtue("obtain_relic_ftue")`；未见过时会等待1秒、添加 NRelicRewardFtue 到模态栈，再调用 `SaveManager.MarkFtueAsComplete`。来源 P NTreasureRoom:294–304；B:341–351。真实 SaveManager.MarkFtueAsComplete 会委托 `_progressSaveManager.MarkFtueAsComplete`，来源主仓 `.local/api/public-github-0.107.1/implementation/MegaCrit.Sts2.Core.Saves.SaveManager.decompiled.cs`:679–682、`.local/api/queries/public-beta-v0.111.0/MegaCrit.Sts2.Core.Saves.SaveManager.decompiled.cs`:834–837。

因此 Godot 自动实验必须在调用之前隔离这条进度写入路径；可在 NTreasureRoom 的 RelicFtueCheck 整个入口返回 CompletedTask，并记录这只是排除教程/保存副作用的宿主替代。不要执行真实 MarkFtueAsComplete 后才检查是否误写，也不要用 SetFtuesEnabled 修改真实偏好。该隔离不能称为原生 FTUE 验收。最后的 ProceedFromTerminalRewardsScreen/地图旅行不属于本次开箱完成证明，避免点击继续触发正式跑局保存/进度流程。

## 直接发奖兼容性仍须有界

来源已明确金币正常宝箱发放不经过 RewardsSet：OneOffSynchronizer.DoTreasureRoomRewards 在 ShouldGenerateTreasure 后直接 GainGold，再 TryHandleSpoilsMap。后者任务完成另有 GainGold(600)→CompleteQuest→RemoveFromDeck。来源主仓 `.local/api/hunting-services/public/MegaCrit.Sts2.Core.Multiplayer.Game.OneOffSynchronizer.decompiled.cs`:112–149，`.local/api/queries/public-beta-v0.111.0/MegaCrit.Sts2.Core.Multiplayer.Game.OneOffSynchronizer.decompiled.cs`:127–168；`.local/api/hunting-services/beta/MegaCrit.Sts2.Core.Models.Cards.SpoilsMap.decompiled.cs`:118–124。奖励清单清空不自动覆盖这些命令。

原型 `PrototypeHuntingDirectRewardRelic.AfterModifyingRewards` 是**受控自编反例**，不是找到的真实第三方 Mod：它 await Obtain 后读取 Owner，或要求 returned 实际在 Owner.Relics 中才继续非奖励计数。来源本工作树 `src/Tests/PrototypeHuntingTreasureTests.cs`:218起。裸取消返回对象缺 Owner 会中断该调用者；只补 Owner 的上下文载体不能满足实际入库断言。具体运行结果见既有 treasure-direct/return 日志，本文不重算或扩大。

这个限制不能通过跳过全部钩子、撤销非奖励计数或宣称任意第三方兼容来隐藏。目前没有据此证实必须改玩法。后续应分别记录：原生/Spoils/本Mod奖励钩子已测路径；明确具体来源的第三方直接命令；调用者继续使用返回值的实际前提。若某一真实调用者只能在违背已确认规则后继续，应带准确反例回用户讨论，不能凭抽象“任意Mod”问题先删规则或确定正式协议。

## 下一项可自动执行实验

可先缩小为 Collection 原生子链实验，而不伪称完整 OpenChest：向新原生 Collection 注入必要原生节点字段/测试run，实际调用 InitializeRelics→AnimIn，让真实 tween 通知原同步器，再由真实 OnRelicsAwarded/AnimateRelicAwards 完成两个源。原生 `NHandImageCollection` 的 `_hands` 构造即为空列表，GetHand 只查列表、BeforeRelicsAwarded 只遍历冻结，故未入树空实例可以自然承担空results子链的 hand 依赖；无需替代这些待测方法。来源为补导出 P/B同目录 `...NHandImageCollection.decompiled.cs`，P:68、190–193、224–230；B:122、244–247、281–287。导出退出0见 `.local/research-hands-{public,public-beta}.log`。

**不要为这个空手夹具在多人输入下调用 hands.Initialize(run)**：它会订阅 InputSynchronizer 并创建每个hand、访问 NGame.CursorManager；raw hands 不入树就不执行带 CursorManager 的 ExitTree。来源 P:74–110、205–221；B:128–164、259–275。这是一种明确裁掉真实peer指针显示/完整节点树的宿主输入，不能将结果升级为完整多人UI或原生网络。Collection.Ready 的字段/事件订阅若人工注入，同样须写明，不能宣称完整节点树已通过。

beta OpenChest 外围叶依赖已另导出四类型至 B同目录，命令退出0见 `.local/research-openchest-leaves.log`：

- `NCommonBanner.AnimateIn/AnimateOut`（122/131）只创建Tween，不依赖NGame；原生banner入树但Ready字段手设可保留实际动画。Ready会查`%Label`及订阅根viewport大小变化，手设夹具需标明。
- `NProceedButton.Ready/ExitTree`（256/269）依赖NGame事件，`OnEnable/OnDisable`（280/307）需要viewport、image、outline和ShaderMaterial；父类NButton.OnEnable/OnDisable（219/225）还注册/注销热键。`UpdateText`（371）只需原生MegaLabel和本地化，维护IsSkip。隔离Ready/ExitTree与热键叶不等于完整物理按钮通过。
- `NMapScreen.Instance`（601）沿NRun.GlobalUi取MapScreen；`SetTravelEnabled`（1273）根据run调用ShouldProceedToNextMapPoint钩子并刷新map points，不能给未初始化run的raw map盲调。原生AnimOut仍访问NRun.ScreenStateTracker；若只测开箱子链，可用局部外层UI夹具或明确的调用点替代，而不能将其记为真实地图旅行/完整屏幕状态通过。

先在 beta PCK 的真实 `rooms/treasure_room.tscn` 节点树中运行一次有界空箱：全新测试跑局与真实 TreasureRoom/同步器、入树前填 room/run；保留 Face/异步非奖励钩子；沿已有抑制实验分开取消普通奖励、保留Spoils任务；隔离FTUE进度调用和地图/屏幕外围，**不手动 CompleteWithNoRelics**。通过反射 await 真实 OpenChest 并由真实帧推进，记录1–4人下空文案/holder、动画 timeScale/可见性、延迟回调次数、began/finished、Proceed启用、金钱/遗物/任务前后、全部宿主替代和退出诊断。只做本地 native APIs，不称原生网络通过。public 原资源不具备时保留资源缺口，不让用户代做这个本可自动的 beta 实验。
