# 原生主文件／backup 同来源补验

2026-11-05；一次性原型，保留在 `prototype/coop-stage-recovery`。来源：[备份恢复补验要求](https://github.com/Endercloud001/AllMatters/issues/68#issuecomment-6627394062)、[最新集成决定](https://github.com/Endercloud001/AllMatters/issues/67#issuecomment-6629243377)。本记录不修改该决定，不是正式保存 Adapter 或整票 resolution。

## 问题及实际链路

问题：原生实际选中的主文件或 backup，能否与 Mod 阶段、预算、本房受罚标记来自同一次保存？写失败会不会把未保存状态变成最新成功点？

探针：[PrototypeCheckpointTests.cs](../../src/Tests/PrototypeCheckpointTests.cs)。沿既有 `HelloWorldTests` 的 Godot／RitsuLib 初始化顺序，真实调用：

`HeadlessTestSetup → RunManager.SetUpTest → RunSavedData.Set → RunManager.ToSave → RunSaveManager.SaveRun(SerializableRun,bool) → LoadRunSave / LoadMultiplayerRunSave → MigrationManager 原生加载 → RunState.FromSerializable → 原生玩家与 RunSavedData.TryGet`

未替换原生序列化、加载选择或 RitsuLib 正常保存／加载补丁。每组使用新的原生 `MockGodotFileIo`，全部保存、改写、删除和重命名只在其内存字典中；`profile68` 是字典内的路径，不访问对应磁盘档案。使用真实 RitsuLib 0.6.7 两个 Runtime。单人／多人参数只决定原生保存路径，宿主网络是 `NetSingleplayerGameService`，不是多人服务或大厅。

源记录来自原生 `MigrationManager.LoadWithAggressiveRecovery<SerializableRun>` 成功结果的对象身份、`filePath` 与传入的完整 JSON；通过 scoped Harmony postfix 观察，不改变返回值。主文件加载失败后由原生决定是否尝试 backup。**不按文件存在、金币值、独立 JSON 反序列化或自选 fallback 推断来源。**

适配组在原生 `LoadRunSave / LoadMultiplayerRunSave` 返回后、`FromSerializable` 前，用返回的同一 `SerializableRun` 对象查源记录，再调用真实 RitsuLib 内部 `AttachDocumentFromJson` 覆盖其文档。反射与私有解析 seam 仅用于本原型；正式 Adapter 要隐藏版本差异并在所有恢复入口应用，不将此文件直接并入生产。普通对象身份已足够，无自定义 hash、独立侧文件、contract 或 baseline。

## 输入与矩阵

先保存 A：111 金币、无 RedMask、`editing / budget 7 / penalized false`；再保存 B：222 金币、1 个原生 RedMask、`exploring / budget 0 / penalized true`。主文件=B，backup=A。B 在 `ToSave` 后另把活跃 Mod 槽位改为 `unsaved-memory / 99 / false`，检查写出的仍是捕获的 B。

写失败候选 C：333 金币、`unsaved-C / 3 / false`，开启真实 `MockGodotFileIo.ShouldFailWrites`；候选捕获不代表保存成功。原型调用者仅在 `await SaveRun` 成功后才推进最新点标记。

每版 2 个保存路径 × 2 种写入模式（同步、异步）× 2 种恢复行为（现状、最小适配）× 5 个场景 = 40 条；双版共 **80 条实际观测**。这是 80 个探针组合，不是 80 个原生多人会话。测试框架每目标计为 1 项矩阵入口；不能报告成 80 项独立测试或 80 项现状通过。

|场景|原生实际结果|原 RitsuLib 0.6.7 结果（每版各4条）|最小适配结果（每版各4条）|
|---|---|---|---|
|正常主文件|B，主文件成功|金币222、RedMask1；Mod B 同源；未误用之后修改的内存值|仍为 B，同源|
|主文件缺失，backup有效|A，backup成功|原生 A；无本 Mod 槽位，不能当作默认编辑状态已正确恢复|原生与 Mod 均 A|
|主文件原生加载失败，backup有效|A；主文件 `Unrecoverable`，原生将其重命名后选择 backup|原生 A；主路径已不存在，Mod 文档缺失|原生与 Mod 均 A|
|同上，但保存损坏主文件的重命名失败|A；主文件仍存在，原生仍选择 backup|**金币111、RedMask0 是 A；Mod `exploring/0/true` 是 B，实际混档反例**|从成功原生结果绑定 backup A，覆盖误附文档，双方均 A|
|写入 C 失败|抛 `InvalidOperationException: Simulated write failure`；主 B 与 backup A 字节均不变|`Saved` 未触发，调用者最新点仍 B，恢复 B；C 不成为成功点|相同|

原行为合计 40 条：正常／写失败 16 条一致，三种 backup 场景 **24 条缺失或混档反例**。适配组 **40/40 同来源匹配**。命令成功表示如实复现并验证预期，不把原行为反例改成通过。

失败主文件仍是有效 JSON，只将 `schema_version` 改为对象，保留 B 的完整 Ritsu 文档；由原生方法拒绝，而非探针跳过。额外重命名失败使用 `RenameFileAction` 抛受控 IOException，不模拟真实权限故障或断电。原生不会必然保持主文件不变：本次两版都实际尝试保留损坏文件；这修正了早先“加载不修改原文件”的泛化描述。来源要求仍是跟随**实际选中的对象与内容**。

## 版本、命令、原始证据

|目标|实际游戏程序集 ProductVersion|原生保存 schema|结果|
|---|---|---|---|
|public v0.107.1|`0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`|16|40条，命令退出0|
|public-beta v0.111.0|`0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`|20|40条，命令退出0|

两版路径分隔符有差异；本次观察到的选源、缺槽／混档、失败写入语义一致，不据此推导版本可互换存档。Godot 4.5.1 mono headless，.NET 9。public 使用实际 public DLL 和既有测试宿主资源，不证明匹配 public PCK 的正式 UI 或完整安装运行。

工作树根目录复现：

```powershell
./prototypes/coop-stage/Run-Checkpoint.ps1
# 实际顺序执行：
./tools/Test.ps1 -Branch public-beta -Filter PrototypeCheckpoint -SkipResourceExport
./tools/Test.ps1 -Branch public -Filter PrototypeCheckpoint -SkipResourceExport
# 只核对已归档证据，不重新运行实验：
node prototypes/coop-stage/Summarize-Checkpoint.cjs
```

已有成功导出的资源包且本轮未改资源，因此明确复用；新机器依 `docs/development.md` 准备宿主，不向游戏 mods 复制任何文件。

- [命令与退出码](checkpoint-evidence/commands.json)：两目标退出0。
- [80条字段、源路径、预期对照及IO调用](checkpoint-evidence/observations.json)。每条保留是否适配、人数路径、写模式、原生源、金币／遗物、Mod字段、失败详情及最新标记。
- 原始完整 [public日志](checkpoint-evidence/public.log) 与 [beta日志](checkpoint-evidence/public-beta.log)：包含构建、版本、Ritsu初始化、原生失败／fallback、每条结果。存在宿主已有的未关联Mod模型诊断与两个既有 nullable 编译警告；不宣称零 ERROR 或零警告。
- [探索过程与失败记录](CHECKPOINT-ATTEMPTS.md)：包括不能拦截 UnsafeAccessor helper、不同中间入口及其限制；这些均不作为最终加载证据。
- 用户确认后发现并修正探针清理时对全局LocalContext.NetId的污染；[最终完整检查日志](checkpoint-evidence/final-check.log)实际退出0，双版各219项测试、资源、双版构建及脚本检查全部PASS。原生矩阵结论不变；完整检查不证明正式拼图功能已实现。

## 能证明与不能证明

已证明真实公开保存／加载流水线可携带同一捕获点；实际未适配库有 backup 缺文档／混档；按原生成功结果绑定来源的最小适配在本矩阵解决；`ShouldFailWrites` 可观测失败，调用者无需把尝试保存误标为成功。

未证明真实文件系统断电原子性、磁盘满／杀进程、云存档、并发写排队／编辑合并／正常退出等待、多人大厅分发和 canonicalization、完整奖励／商店／战斗内容检查点；保存后读 Mod 槽位不证明上述正式对象已完整捕获。原型的 `latest` 是等待保存完成的最小调用者，不是已存在的正式检查点管理器。

规格必须要求：恢复输出以原生实际选中来源为准；新拼图存档缺少必要 Mod 文档应报告不可恢复或使用经验证的整体策略，不能默默创建默认布局；此处不发明回退玩法。存档修复／迁移后还需核对语义一致性，不能只凭“同文件”承诺完整。正式版本 Adapter 对私有 seam 的接入失效属于后续实现阻塞，必须保留已测反例回归；不能降格为通过或自动转用独立 Mod 文件。
