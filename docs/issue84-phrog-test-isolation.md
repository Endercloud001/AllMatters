# #84 Phrog 原生 UI 测试隔离诊断

2026-11-13。本记录描述集成分支全量运行才暴露的两个测试宿主问题。生产实现、原生死亡动画、敌人节点、血条和意图断言均保留；不是整张 #84 的完成声明，也不替代正式游戏、匹配 public 资源或真实联机验收。

## 原始失败与定位

原始全量 beta 在前置 418 项之后进入 Phrog 原生 UI 用例，抛出 `Backend should not be subscribing to CombatStateChanged!`。调用链经过 `CombatManager.AddCreature`、`CreatureCmd.Add`、`Infested.AfterDeath` 与追加第五只敌人的逻辑。原始宿主和控制台日志先行保留，没有用后来的结果覆盖。

将前置列表分半不能稳定重现，说明只有用例集合还不足以解释失败。连续执行 20 次真实 UI 死亡用例通过；连续执行 100 次原有 backend `PhrogWorms`，再执行一次真实 UI 非零死亡用例，两次重现同一异常。临时诊断表明原有 Notify 前缀仍在，但热身后的 `CreaturesChanged` 路径绕过该前缀；在 `AddCreature` 上加 patch 也没有拦住已内联的调用，在 Add 的调用处拦截则使失败移动至 Remove。最终选择事件委托边界，不依赖这些已移除的实验 patch。

修复只作用于 `NodeHost`：捕获实际 manager、combat 与原生 `StateTracker.OnCreaturesChanged` 委托，在原调用顺序的位置放入作用域包装。包装仍调用原生回调；仅在 combat、NGame、NRun 与原生 run 均属于本 scope 时，同步暂时关闭 TestMode，并在 finally 恢复。释放宿主时恢复精确原委托，保留其他订阅及顺序。没有把整次死亡过程切成非测试模式，也没有永久关闭 TestMode。既有原 owner 的 shake/hotkey/container 清理修复保留。

普通注册用例 `Phrog native UI survives backend creature callback warmup and cleanup` 执行实际 100 次 backend 战斗，再执行原生非零死亡及独立清理/UI 消费用例。禁用委托修复时，该回归实际失败；启用后通过。清理用例同时检查正常释放和受控断言抛出后的完整委托列表及顺序。

## 第二个独立问题：画布坐标与物理窗口

事件边界修复后的首次完整 Check 在两版都进一步到达几何断言，然后失败：五个实际 hitbox 未全部位于 viewport。最小组合是既有 responsive map 用例，随后原 Phrog compact/wide 几何用例。这个失败没有归因于前一个异常或资源日志。

原宿主未启用 stretch，将 1280×720 同时当成 UI 画布和物理窗口。单独运行时从 64 像素窗口实际 resize 至 1280，会触发原生调整；前置 map 已留下同尺寸时，再设置 1280 不产生实际尺寸变化，暴露不同 Ready/resize 路径。直接发射 SizeChanged 信号可以使实验通过，但该方案被丢弃，没有进入最终源码。

原生 `NCombatRoom.AddCreature` 创建并挂载真实节点、分配 slot、更新导航，但不执行布局 reflow；调整发生于 Ready 或窗口尺寸变化。在失真的固定 1280 画布下，原生安全四只也可越界，因此没有据此修改生产位置。读取实际 beta PCK 的 `project.binary`，得到画布 1920×1080、stretch `canvas_items`、aspect `expand`；原生四只和受罚五只在此模型下均通过。

当前 `NativeDisplayScope` 在真实房间 Mount/Ready 之前设置 1920×1080 内容画布、CanvasItems、Expand 与 1280×720 物理窗口，并捕获、恢复四个原始窗口属性。几何用例仅将物理窗口切换为 1280×720 和 1680×720，使用 `GetVisibleRect()` 取得画布边界，再以 `GetStretchTransform()` 检查映射后的实际窗口边界。原有节点可见、hitbox 正尺寸、敌人两两分离、玩家分离、实际 HP 与意图显示断言全部保留；没有手动移动 marker、改变敌人 scale 或模拟尺寸信号。

这里准确覆盖的是项目默认/Auto 的 Expand 模型。双版 `NGame.ApplyDisplaySettings` 的显式 SixteenByNine 分支还会设置 Keep；当前结果不能扩写为 Keep 或全部显示模式验收。此前关于意图可能立即可见的假说也不成立：原生 `RefreshIntents` 会将 alpha 归零。真实几何原因是宿主的 1280 画布模型失真，最终仍等待实际意图 fade。

普通注册回归 `Phrog geometry retains native canvas after a responsive map` 先运行实际前置 map 用例，再运行 1–4 人原生开场（UI 检查 1/4 人）的 Phrog 几何测试。临时精确列表、重复次数环境变量、诊断输出、四只对照探针和候选 patch 都已从源码移除。

## 验证记录与证据位置

全部原始证据保存在 `.local/integration-slices/phrog-suite-order-20261113/`，不纳入版本控制。重点文件：

| 范围 | 证据 |
| --- | --- |
| 原始全量异常 | `original-hook-console.log`、`original-public-beta-last.log`、`repro-full-beta-host.log` |
| backend 热身与回归红 | `backend100-host.log`、`backend100-debug-host.log`、`regression-red-host.log` |
| 事件修复后完整 Check 的真实几何失败 | `check-final-console.log`、`check-first-event-boundary-{public-beta,public}-host.log` |
| 精确前置组合与原生画布对照 | `min-geometry-6-1-host.log`、`geometry-debug-{pair,single}-host.log`、`geometry-nativecontent-safe4-host.log`、`geometry-nativecontent-five-console.log` |
| 当前 Phrog 定向验证 | `phrog-final-nativecanvas-console.log`、`phrog-nativecanvas-{public-beta,public}-host.log` |

当前定向命令为 `tools/Test.ps1 -Branch All -Filter Phrog -SkipResourceExport`，双版各 7 项通过，Build/Host/Metadata 均为 0，无异常或准备失败。beta 引擎错误为 0；public 引擎错误 96 条，逐类均为共享 beta PCK 的既有 NHotkeyIcon 类不匹配日志，不能声明正式 public 资源通过。

诊断期间一次临时选择器恢复错误曾把 `HelloWorldTests.cs` 中五处中文从 UTF-8 改写成 CP936 字节。损坏字节已保留为 `HelloWorldTests.pre-byte-restore.bin`，源码已通过 `git show HEAD:src/Tests/HelloWorldTests.cs` 的原始字节恢复，逐字节与 HEAD 一致；没有更改中文断言。恢复前已经编译的完整 Check 不作为最终验收，即使后来源码已恢复也不追认该二进制。

恢复前编译的 Check 退出 1：beta 完成 492 项、Host 0；public 在 Phrog 全部通过后，于 `JigsawHuntingBossTests.TestSubjectTurns` 首次五卡准备条件超时、Host 1。资源、脚本及两版 Build 通过。准确原日志为 `check-pre-byte-restore-{public-beta,public}-host.log`、`check-pre-byte-restore-transcript.log` 与 `check-nativecanvas-console.log`；public 日志没有 coordinator、pauses 或 events 的运行时快照，不能从 stack 推断内部状态。该失败保留为独立线索，未修改 Boss 源码。

事件修复与后续 canvas 增量分别经过独立 Standards、Spec 双轴审查，均无可行动发现。审查结果不替代最终 fresh Check。

最终以正确源码重新执行 `tools/Check.ps1 -Branch All`，退出 1。PowerShell syntax、Workflow scripts、Git whitespace、Placeholder scan、fresh Resources 及两版 Build 全部通过；beta 完成 492 项，Build/Host/Metadata 为 0，无 FirstException 或准备失败；public Build/Metadata 为 0，Host 1，再次于同一 Boss `TestSubjectTurns` 首次五卡准备条件超时。两版原始日志均明确列出 Phrog 七项 PASS，因此两个 Phrog 隔离修复的完整顺序回归闭环，但不能宣称整批 Check 通过。未修改 Boss 实现或测试，此失败交由后续独立诊断处理。

`ReadTestLog.ps1` 统计 beta EngineErrors 24，FirstDiagnostic 为 `Player 1 has been killed outside of combat in multiplayer`；按完整错误顶行分类：18 条存档损坏/类型/名单验证日志、1 条上述死亡诊断、1 条 NCreatureVisuals 类不匹配、1 条故障注入 worker failure，以及 3 条退出时 RID/资源残留。public EngineErrors 126，FirstDiagnostic 为 NCreatureVisuals 类不匹配；分类为 101 条 NHotkeyIcon 类不匹配、18 条存档验证日志、2 条 RitsuLibManagedGameAction 出队异常、1 条 NCreatureVisuals 类不匹配、1 条故障注入 worker failure 和 3 条退出残留。不能把所有错误归为 NHotkeyIcon，也不据此声明其他日志无害。

准确最终证据是 `check-restored-source-console.log`、`check-restored-source-transcript.log` 与 `check-restored-source-{public-beta,public}-host.log`。完整原始日志均保留；源码复核确认 HelloWorld 仍逐字节等于 HEAD。Phrog 交付差异仅两个测试文件和本文/实现记录链接，没有生产改动、临时选择器或导入元数据噪声。检查进程已退出，原生执行窗口已释放。
