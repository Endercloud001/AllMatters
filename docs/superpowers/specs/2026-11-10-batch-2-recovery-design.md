# Batch 2 多人会话恢复设计规格

**日期**：2026-11-10  
**范围**：#91–#94 多人恢复框架批次化收尾  
**前置**：#91 LobbyRecovery、#92 ExplorationStateRecovery、#93 SaveStateRecovery、#94 框架集成  
**交付**：自动化测试 + 规范文档 + 验收矩阵  

---

## 1. 概述与现状

### 1.1 已交付基础代码

| 工单 | 模块 | 交付物 | 状态 |
|------|------|--------|------|
| #91 | LobbyRecovery | 大厅同步基础架构、玩家名单恢复、连接/断开追踪 | 已合并 main |
| #92 | ExplorationStateRecovery | 探索状态恢复、节点访问/完成追踪、步数/预算保存 | 已合并 main |
| #93 | SaveStateRecovery | 存档恢复、玩家库存追踪、30s 重连窗口 | 已合并 main |
| #94 | MultiplayerRecoveryOrchestrator | 恢复管道框架、阶段转移、错误处理 | 已合并 main |

### 1.2 验收边界

**自动化验证范围**（Agent 负责）：
- 单目标单进程单玩家重连流程
- 存档格式/数据完整性
- 大厅/探索/存档恢复单元逻辑
- 框架阶段转移与错误恢复

**手动/实机验收范围**（用户负责）：
- 真实 3 进程多客户端同步
- 网络分割与部分重连
- 并发加入竞态
- UI 映射与用户可见行为

**不在本批次范围**：
- 完整联网/服务端实现
- 回放/观战模式
- Mod 间交互

---

## 2. 自动化验证场景矩阵（20+ 场景）

### 2.1 大厅恢复（Lobby）

| ID | 场景 | 输入 | 期望结果 | 自动化 | 备注 |
|----|----|------|--------|------|------|
| L1 | 从检查点读取原始玩家名单 | 包含 OriginalPlayers 的检查点 | LobbyRecovery.originalPlayers 非空 | ✅ | 单元测试 |
| L2 | 玩家连接事件更新已连接集合 | OnPlayerConnected(P1) | P1 在 connectedPlayerIds 中 | ✅ | 单元测试 |
| L3 | 玩家断开事件更新已连接集合 | OnPlayerDisconnected(P1) | P1 不在 connectedPlayerIds 中 | ✅ | 单元测试 |
| L4 | 全部玩家就绪判断（Quorum） | 3 个原始玩家，3 个已连接 | IsLobbyReady() == true | ✅ | 单元测试 |
| L5 | 部分玩家就绪判断（降级） | 3 个原始玩家，2 个已连接 | IsLobbyReady() 按策略判断 | ✅ | 单元测试 |
| L6 | 大厅状态快照包含探索信息 | 调用 GetLobbyState() | LobbyState.ExplorationState 非空 | ✅ | 集成测试 |
| L7 | 大厅状态快照包含存档信息 | 调用 GetLobbyState() | LobbyState.SaveState 非空 | ✅ | 集成测试 |
| L8 | 重复连接相同玩家无重复 | OnPlayerConnected(P1) × 2 | connectedPlayerIds.Count == 1 | ✅ | 边界测试 |
| L9 | 未知玩家尝试连接被拒绝 | OnPlayerConnected(Unknown) | 日志记录拒绝，不添加到名单 | ✅ | 单元测试 |

### 2.2 探索恢复（Exploration）

| ID | 场景 | 输入 | 期望结果 | 自动化 | 备注 |
|----|----|------|--------|------|------|
| E1 | 从检查点读取访问节点集合 | 包含 Visited 的检查点 | VisitedNodes 包含所有已访问节点 | ✅ | 单元测试 |
| E2 | 从检查点读取完成节点集合 | 包含 Completed 的检查点 | CompletedNodes 包含所有已完成节点 | ✅ | 单元测试 |
| E3 | 恢复步数与预算 | 检查点：Steps=5, Budget=10 | GetRemainingSteps() == 5 | ✅ | 单元测试 |
| E4 | 判断狩猎阶段（Steps > Budget） | Steps=12, Budget=10 | IsHunting() == true | ✅ | 单元测试 |
| E5 | 判断非狩猎阶段（Steps ≤ Budget） | Steps=5, Budget=10 | IsHunting() == false | ✅ | 单元测试 |
| E6 | 当前位置保存与恢复 | CurrentNode=42 | GetSnapshot().CurrentNode == 42 | ✅ | 单元测试 |
| E7 | 阶段信息（Phase）保存与恢复 | Phase=Act1 | GetSnapshot().Phase == Act1 | ✅ | 单元测试 |
| E8 | 查询已访问节点状态 | IsNodeVisited(5) 在已访问集合中 | 返回 true | ✅ | 单元测试 |
| E9 | 查询未访问节点状态 | IsNodeVisited(99) 不在已访问集合中 | 返回 false | ✅ | 单元测试 |
| E10 | 查询已完成节点状态 | IsNodeCompleted(3) 在已完成集合中 | 返回 true | ✅ | 单元测试 |
| E11 | 未初始化时返回空/0 | InitializeFromCheckpoint() 未调用 | IsInitialized == false, GetSnapshot() == null | ✅ | 单元测试 |

### 2.3 存档恢复（SaveState）

| ID | 场景 | 输入 | 期望结果 | 自动化 | 备注 |
|----|----|------|--------|------|------|
| S1 | 从检查点读取 Act 进度 | 检查点：Act=2 | GetSnapshot().Act == 2 | ✅ | 单元测试 |
| S2 | 从检查点读取原始玩家名单 | 检查点：OriginalPlayers=[P1, P2, P3] | 玩家状态初始化为 3 | ✅ | 单元测试 |
| S3 | 玩家连接后心跳更新 | OnPlayerConnected(P1) | P1.LastHeartbeat 更新为当前时间 | ✅ | 单元测试 |
| S4 | 玩家状态更新（库存/资源） | UpdatePlayerState(P1, 100, 50, 100, 10, 2) | P1.Gold=100, Health=50, CardsCount=10, RelicsCount=2 | ✅ | 单元测试 |
| S5 | 30s 重连窗口超时判定 | OnPlayerDisconnected(P1) 后等待 > 30s | IsPlayerTimeout(P1) == true | ✅ | 时间模拟测试 |
| S6 | 玩家在窗口内重连成功 | OnPlayerDisconnected(P1) 后 15s 内重连 | P1.IsConnected == true | ✅ | 单元测试 |
| S7 | 批量玩家状态查询 | 3 个玩家的状态 | GetAllPlayerStates() 返回 3 项 | ✅ | 单元测试 |
| S8 | 已连接玩家数统计 | 3 个原始玩家中 2 个已连接 | GetConnectedPlayerCount() == 2 | ✅ | 单元测试 |

### 2.4 框架阶段转移（Orchestrator）

| ID | 场景 | 输入 | 期望结果 | 自动化 | 备注 |
|----|----|------|--------|------|------|
| O1 | 初始化恢复上下文 | InitializeRecovery(sid, [P1, P2], [P1]) | RecoveryContext 创建，Phase=Connection | ✅ | 单元测试 |
| O2 | 执行恢复管道（成功） | ExecuteRecoveryAsync() 所有策略通过 | RecoveryOutcome.Success == true | ✅ | 集成测试 |
| O3 | 策略失败时停止管道 | 一个策略返回失败 | 管道停止，返回错误信息 | ✅ | 集成测试 |
| O4 | 错误可重试标志传播 | 策略设置 CanRetry=true | RecoveryOutcome.CanRetry == true | ✅ | 单元测试 |
| O5 | 会话状态追踪 | 多个会话同时恢复 | GetActiveSessions() 返回所有会话 | ✅ | 集成测试 |
| O6 | 会话清理 | CompleteSession(sid) | 会话从 activeSessions 移除 | ✅ | 单元测试 |
| O7 | 异常处理与日志 | 恢复中抛出异常 | 捕获异常，返回 Failed 状态 | ✅ | 集成测试 |

---

## 3. 双版本验证命令

### 3.1 测试命令（开发机）

```powershell
# 冒烟检查：确认测试宿主就绪
./tools/Test.ps1 -Branch All

# 单目标（public-beta）恢复相关测试
./tools/Test.ps1 -Branch public-beta -Filter 'Recovery|Multiplayer'

# 单目标（public）恢复相关测试
./tools/Test.ps1 -Branch public -Filter 'Recovery|Multiplayer'

# 完整检查（自动化 + 代码审查）
./tools/Check.ps1

# 详细读取测试日志（诊断失败）
./tools/ReadTestLog.ps1 -Path .local/tests/public-beta-last.log
./tools/ReadTestLog.ps1 -Path .local/tests/public-last.log
```

### 3.2 API 查询（确认方法签名）

```powershell
# 查询 JigsawCheckpoint 类型
./tools/ExportApiReference.ps1 -Find JigsawCheckpoint

# 查询 RunState.FromSerializable 签名
./tools/ExportApiReference.ps1 -Branch public -Types MegaCrit.Sts2.Core.Runs.RunState -Signatures -Member FromSerializable
./tools/ExportApiReference.ps1 -Branch public-beta -Types MegaCrit.Sts2.Core.Runs.RunState -Signatures -Member FromSerializable

# 查询检查点相关成员
./tools/ExportApiReference.ps1 -Types AllMatters.Jigsaw.JigsawCheckpoint -Signatures
```

---

## 4. 不关闭 Issue 的条件

每张工单在以下情况下保持打开，不因自动化测试通过而关闭：

| Issue | 未完成验收项 | 原因 |
|-------|------------|------|
| #91（LobbyRecovery） | 真实 3 进程多客户端同步、并发加入竞态、UI 映射 | 单进程单元测试无法验证分布式行为 |
| #92（ExplorationStateRecovery） | 多玩家异步进入语义、迟到玩家、多端 UI 一致性 | 需要真实网络端点 |
| #93（SaveStateRecovery） | 库存/奖励副作用、缺席玩家隔离、损坏存档降级 | 需要完整游戏对象模拟 |
| #94（Orchestrator） | 真实多人端点、错误恢复、重试策略、超时处理 | 需要完整集成验证 |

**关闭条件**：用户在实机验收后确认手动验收项通过，或这些项被明确排除为后续工作。

---

## 5. 自动化边界说明

### 5.1 测试宿主限制

- **无原生 UI**：无法验证 UI 状态映射、按钮可点击性、动画帧率
- **无网络**：无法验证分布式竞态、消息序列、超时重试
- **单进程**：无法验证进程间通信、资源竞争、内存隔离
- **模拟时间**：可用 `DateTimeOffset.UtcNow` 模拟超时，但不验证真实延迟

### 5.2 代码可验证的事实

- 数据结构完整性（成员存在、初始化逻辑）
- 单元逻辑正确性（状态转移、错误分支）
- 接口契约（参数/返回类型匹配）
- 验收范围内的最小玩法流程

### 5.3 代码不能验证的事实

- 真实网络行为（丢包、乱序、延迟）
- 并发竞态条件（多个客户端同时连接）
- 原生 API 行为（游戏内对象生命周期、资源加载）
- 用户体验（UI 响应性、引导文案清晰性）

---

## 6. 存档兼容性与降级

### 6.1 检查点格式版本

```csharp
// src/Jigsaw/JigsawCheckpoint.cs
public sealed class JigsawCheckpoint
{
    public required int FormatVersion { get; init; }  // 当前: 2
    public required int Act { get; init; }
    public required MapSave Map { get; init; }
    // ...
}
```

**版本历史**：
- v1（原始格式）: Act、Visited、Completed、Steps、Budget
- v2（当前）: + Phase、FirstProgress、OriginalPlayers

**兼容性**：
- 读取 v1 时，Phase/FirstProgress 使用默认值
- 读取未来版本时，忽略未知字段

### 6.2 降级策略（受损/缺失存档）

| 情景 | 策略 | 返回值 |
|-----|------|--------|
| 检查点缺失 | 跳过该玩家恢复 | RecoveryResult.Fail("No checkpoint") |
| Map.Visited 缺失 | 使用空集合 | IsInitialized=true, VisitedNodes.Count=0 |
| OriginalPlayers 缺失 | 使用当前已连接玩家 | 仅恢复已连接的玩家状态 |
| Phase 缺失（v1） | 默认 Phase.Act1 | 探索阶段设为默认值 |

---

## 7. 测试场景分类

### 7.1 单元测试（`src/Tests/*.cs`）

- 各恢复模块的独立逻辑
- 不依赖原生游戏 API、不涉及网络
- 快速反馈循环（< 1s）

### 7.2 集成测试（`src/Tests/Integration/*.cs`）

- 多个恢复模块组合（Orchestrator + 子策略）
- 涉及 JigsawRun、JigsawCheckpoint 等游戏对象
- 中等反馈循环（< 10s）

### 7.3 E2E 测试（`src/Tests/Integration/MultiplayerRecoveryE2ETests.cs`）

- 完整恢复管道（Connection → Completed）
- 仅设计了框架，实现内容在完整集成后补充
- 当前状态：所有测试标记为 Skip

---

## 8. 双版本适配

### 8.1 API 查询记录

**public-beta 0.111.0 版本**（本机 Steam 安装）：
- `RunState.FromSerializable(ISerializable)` ✅
- `JigsawCheckpoint` slot 访问 ✅
- `IReadOnlyCollection<T>` 支持 ✅

**public 0.107.1 版本**（GitHub 下载的 DLL）：
- `RunState.FromSerializable(ISerializable)` ✅
- `JigsawCheckpoint` slot 访问 ✅
- `IReadOnlyCollection<T>` 支持 ✅

### 8.2 条件编译

无需条件编译。两版本 API 兼容，恢复逻辑版本无关。

---

## 9. 验收记录模板

**工单完成后**，Agent 在工单评论记录以下信息：

```markdown
## 自动化验收完成（日期）

### 测试覆盖
- 场景矩阵：覆盖 X/Y 个场景 ✅
- 两目标通过率：public-beta X%, public Y% ✅
- 新增测试文件：[文件列表]

### 验证命令
```powershell
./tools/Test.ps1 -Branch All -Filter 'Recovery|Multiplayer'
./tools/Check.ps1
```

### 代码审查通过 ✅
- 所有 Harmony patch 已验证
- 无阻塞项

### 手动验收清单（用户）
- [ ] 3 人联机重连流程
- [ ] 部分离线降级
- [ ] UI 映射正确
- [ ] 错误恢复可重试

**注**：自动化验收通过不代表手动验收完成。
```

---

## 附录：相关文件清单

```
src/
├── Jigsaw/
│   ├── LobbyRecovery.cs             // #91 完成
│   ├── ExplorationStateRecovery.cs  // #92 完成
│   ├── SaveStateRecovery.cs         // #93 完成
│   └── JigsawCheckpoint.cs          // 检查点存储
├── Features/Multiplayer/
│   ├── IRecoveryStrategy.cs         // 恢复策略接口
│   ├── MultiplayerRecoveryOrchestrator.cs  // #94 完成
│   └── README.md                    // 集成文档
└── Tests/
    ├── LobbyExplorationRecoveryTests.cs
    ├── SaveStateRecoveryTests.cs
    ├── ExplorationRecoveryTests.cs
    └── Integration/
        └── MultiplayerRecoveryE2ETests.cs

docs/
├── game-mechanics.md        // 玩法机制索引
├── development.md           // 开发环境配置
└── superpowers/
    ├── specs/
    │   └── 2026-11-10-batch-2-recovery-design.md  // 本文档
    └── plans/
        └── 2026-11-10-batch-2-recovery.md  // 实现计划
```

---

**文档版本**：1.0  
**最后更新**：2026-11-10  
**作者**：Kiro  
**审核状态**：规格草稿完成，待代码实现验证
