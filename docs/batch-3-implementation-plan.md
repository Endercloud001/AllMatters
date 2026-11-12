# 批次 3：多人拼图恢复实施

起点：`ef4f25da713dcc1b84a88a87be35eb2f7497a1f4`。范围为 #81、#91–#94、#96，权威为 GitHub #73、各票及 #92 共享访问记录修订。2026-11-11 开始前已读取先行调研交接；其只调研边界是上一轮历史，不覆盖本轮实施授权。

主检出保留既有未跟踪 `docs/RETROSPECTIVE-2026-11-11-batch2-issue81.md`，不纳入本轮审查。既有 `issue-91-94` worktree 有未提交更改，不复用或修改。集成分支 `integration/batch-3-20261111` 独占，子任务由独立 worktree 提交后顺序集成。

## 实施顺序与边界

1. 宿主准备：修复真实 Mod 注册和累计超时。保留诊断阈值与防挂保护；双版宿主健康后开始玩法实施。
2. #96：复用现有生成器，补正预算搜索、七种形状与四向连接的行为证据，不另写生成器。
3. #91：修复原生 FromSerializable → SetUpSavedMultiplayer 时序，保留完整检查点验证，使用加载大厅实际在局名单；原存档名单及缺席角色不删除。
4. #92：沿同一恢复地图接正式展示，覆盖共享访问/完成/位置/步数与追猎，禁止测试自己复制 DTO 代签生产恢复。
5. #93：沿原生 SerializableRun 验证完整库存、部分/完整 Ancient 奖励和同来源故障恢复，不新增库存或奖励账本。
6. #94/#81：复用 ENet 子进程宿主接真实加载大厅，覆盖完整、缺席、迟到、恢复中断和 20+ 有实际行为断言的排列；串行修改集成热点。不用 stub Completed 代签恢复成功。
7. 最终双版完整检查、staged Standards+Spec 审查、修复及受影响复验、提交，准备同一提交的批次统一包和人工清单。

## 已约定的验证入口

沿规格及项目现有 seam：生产生成器公开操作；真实 RunState.FromSerializable / LoadRunLobby / SetUpSavedMultiplayer；原生 SaveManager 隔离 IO；原生奖励命令；正式 JigsawScreen 入树和引擎输入；真实 ENet Host/Clients。局部方法调用只证明局部行为。新增 API/patch 先检查双版反编译签名；准备失败不算 TDD red。

使用 `tools/Test.ps1`、`tools/ReadTestLog.ps1`、双版测试项目构建和最终 `tools/Check.ps1`；仅代码变动时复用 `-SkipResourceExport`。大日志留 `.local/batch3/`，记录退出码和证据范围。完整 public 安装、真实 Steam 多端与实体设备仍待用户；共享 beta 资源宿主不代签 public 正式运行。

## 交付状态规则

自动实现、自动验证、GitHub 状态、统一包和用户验收分别记录。未经用户三端反馈不标 #94 人工通过。Agent 不启动游戏/Steam、不复制到 mods、不推送/关闭 Issue。全部产出在 `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/` 内。保留既有安全措施，不新增 hash、冻结 contract、baseline 或 gate。
