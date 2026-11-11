# 第三轮：进入战斗切换停顿调查

调查起点：`decb472a80e7448bf7fbb1b510f9db107ca6983f`。用户确认多人选点、进入房间和商店正常；本调查不重新判这些功能失败。实际游戏为 beta 0.111.0，RitsuLib 0.6.10。全部测量在独立项目内 worktree `round3-combat-perf`，未启动正式游戏或 Steam、未改安装。

## 结论及证据限制

已区分出同步工作、原生异步加载和多人共同就绪等待；目前没有足够的帧时间证据将用户看到的短暂卡顿唯一归因给某一项。不能把过场总耗时当成主线程停帧，也不能因 headless 进入正常就宣称 GPU 实机场景无问题。

发现三个值得实机定位的点：原生每房预加载结束后显式 `GC.Collect()`；拼图 `Refresh` 每次重建隐藏碎块按钮及投票头像容器，可能增加分配和垃圾回收；首次战斗场景创建/材质着色器上传不在 headless 的覆盖范围。本轮只交调查与方案，不禁用原生 GC、不删完整保存、不放宽多人共同就绪，不加入常驻诊断。

## 用户日志

来源：集成工作树 `.local/round3-feedback-20261111/user-godot.log`。日志中共 6 次 `Combat Room` 预加载完成，耗时分别为 102、91、96、120、122、130 ms（第 1977、2357、4993、5631、6392、7484 行），最小 91、中位 111、最大 130 ms。它们是原生加载会话墙钟耗时，不是最大帧时间；各次遭遇和缓存内容不同，不能标为严格冷/暖对照。第 7943 行 Common 705 ms 属于其他场景，不混入战斗样本。

双版原生源码：`PreloadManager.LoadRoomAssets` 先计算缓存集合、await 加载完成，再调用 `GC.Collect()`；`LoadAssetSets` 中有 `await Task.Yield()`。beta `CombatRoom.StartCombat` 的预加载之后才创建 `NCombatRoom` 并设置战斗。显式 GC 是停帧候选，但日志无 GC 区间/堆数据，尚未证明它占据用户所见停顿。

源码证据位于本 worktree `.local/api/queries/perf-{beta,public}/MegaCrit.Sts2.Core.Assets.PreloadManager.decompiled.cs` 和同目录 `CombatRoom` 文件。public 加载也有同一显式 GC 路径。

## 可运行测量

复用既有真实 ENet Host + 两个 Client 测试，不新增测试框架：

```powershell
# 在本独立 worktree，将项目内保留的临时仪表补丁应用后运行；不要应用到游戏安装。
git apply .local/round3/temporary-instrumentation.patch
pwsh -NoProfile -File tools/Test.ps1 -Branch All -Filter 'Jigsaw recovery native load lobby combat-exit all' -SkipResourceExport
# 测完按该补丁反向还原。
git apply -R .local/round3/temporary-instrumentation.patch
```

实际将 TEMP/TMP 指向本树 `.local/round3/temp`；三轮采样命令均双版各 1 项通过，0 失败，日志 `combat-entry-measure{,2,3}.log`。第二轮因仪表脚本编码失败而仅重复首轮粒度，不把它冒充新增同步段测量。最终统计只采用第三轮 `combat-entry-measure3.log` 指向的两个运行目录，不混入准备失败。

第三轮 beta 目录为 `.local/batch3/recovery/combat-exit-all-36ebb59e-0d59-4592-960b-6c944a3b4dcf/`，public 为 `combat-exit-all-ff52fe94-2bb0-4532-96af-f498dfd7ad97/`；每目录保留 `host.log`、`2.log`、`3.log`。计算脚本及结果在 `.local/round3/{summarize.py,timing-summary.json,ui-summary.json}`。

每端记录了两个不同进入：节点 1 为恢复待进入战斗、节点 2 为战后选定的下一房，不能将二者当同一战斗的冷/暖重复。两版 × 三端 × 两次进入共 12 段。这个宿主保留原生逻辑房/奖励和 ENet 协议，但 `TestMode` 会跳过原生资源预加载，原生音频、完整场景和 GPU 渲染不等于实机；因此以下数字只用于识别 Mod 同步段与异步等待，不与用户 91–130 ms 预加载相加。

| 区段 | 第三轮观测范围 | 含义 |
| --- | --- | --- |
| 进入前检查/建立完整保存点 | 初次已有恢复点的区间 1.313–2.613 ms；下一房建立副本 9.096–11.952 ms | `??=` 复用已有点时不执行复制；下一房同步 `ToSave` + JSON 独立副本，保留其恢复安全作用 |
| 调用原生 EnterMapCoord 到返回 Task | 1.949–17.830 ms | 原生调用最初同步段，仍含测试三进程调度干扰 |
| 返回 Task 后到原生进入完成 | 34.999–685.070 ms | await 墙钟，包含网络与原生流程，不能称为同长停帧 |
| 本地原生就绪后等共享决定 | 45.078–1569.114 ms | 可靠共同进入等待；不是同步 busy wait，不应为提速跳过 |

另在同一轮正式挂载的拼图界面中量了 116 次 `Refresh` 调用，地图均为 25 节点：最小 2.509 ms、中位 4.255 ms、最大 35.760 ms。计时仅包围同步 Refresh，不含 `_Draw`、布局完成、渲染线程和 GPU；样本来自三个并发进程，不是独立实机分布，不能外推百分位或实机大地图上界。用户截图地图明显大于 25 节点，需另作实机测量。

`JigsawScreen.Refresh` 确实每次移除/排队释放旧 poolButtons，再重建未放置碎块按钮，即使探索期 pool 已隐藏；也重建投票头像容器。原生 ResourceLoader 本身会缓存已加载资源，不能仅看到 `Load` 被调用就断言每次都磁盘读取。Boss 视觉有 `bossVisuals` 缓存，不是每帧重建所有 Boss。

## 解决方案及验证顺序

1. 先在开发用的真实 beta 独立测试存档做一次 CPU/帧/GC/资源加载捕获，保留相同场景尺寸和已安装 Mod 集合，分别记录首次进入与同遭遇资源已热后的进入。把最大帧与原生预加载开始/结束、GC、`NCombatRoom` 创建、拼图 Refresh 对齐，再判断是等待过场还是单帧尖峰。当前 Agent 按项目边界不启动游戏；这一步需用户或能运行开发游戏的环境执行。无需增加常驻日志或新增验收 gate。
2. 若尖峰落在拼图 Refresh，优先小改为探索期不重建隐藏碎块列表、票未变时不重建头像。需保持编辑/探索切换、投票头像移动、关闭/重开和不同分辨率行为；以现有正式 UI 输入与保存恢复测试保护，再比较相同场景的分配量/帧时间。该方案局部且较低风险，但本轮测量尚不足以声称它解决用户卡顿，未预先实施。
3. 若尖峰在原生预加载结束后的 GC 或首次战斗场景/着色器创建，向原版或相关 Mod 提交带帧数据的最小复现。可以评估提前预热确定会用到的静态资源，但不得提前创建房间、生成怪物/奖励或消耗 RNG。不能简单 patch 掉全局 GC、后台调用 Godot 场景 API，或一次预载所有遭遇增加内存压力。
4. 若主要是多人等待，保持现有共同就绪机制；可考虑在不暴露旧房场景的前提下用原版过场/加载反馈改善等待感。它改善视觉连续性，不等于减少帧阻塞；须分别验证两端时序、失败重试和不重复扣步。

所有临时 `[PERF-round3]` / `[PERF-round3-ui]` 源码已还原，交付只新增此调查文档；ignored 补丁及日志保留项目内供复现。不新增冻结 contract、hash、baseline 或 gate。
