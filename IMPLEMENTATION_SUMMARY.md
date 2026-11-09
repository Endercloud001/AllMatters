# Issue #92 Implementation: 多人探索状态同步 (Exploration State Recovery)

## 实现概述

本实现为多人游戏中探索状态恢复机制，确保玩家断线重连后能恢复到之前的探索进度（已访问房间、收集物品、当前位置等）。

## 核心组件

### 1. ExplorationStateRecovery.cs
**新增模块**，负责在多人游戏中追踪和恢复探索状态。

**关键功能**：
- `InitializeFromCheckpoint()` - 从保存点初始化探索状态
- `IsNodeVisited(nodeId)` - 检查节点是否已访问
- `IsNodeCompleted(nodeId)` - 检查节点是否已完成
- `GetRemainingSteps()` - 获取剩余步数
- `IsHunting()` - 检查是否进入追猎阶段（步数超出预算）

**ExplorationSnapshot**：
- `VisitedNodes` - 已访问房间集合
- `CompletedNodes` - 已完成房间集合
- `CurrentNode` - 当前所在节点
- `Steps` - 当前步数
- `FirstProgress` - 首次进度计数
- `Phase` - 当前阶段 (JigsawPhase)
- `Budget` - 每幕步数预算

### 2. LobbyRecovery.cs 增强
**更新现有模块**，集成探索状态同步。

**新增功能**：
- `GetLobbyState()` 现在包含 `ExplorationStateInfo`
- `ExplorationStateInfo` 结构体提供：
  - 访问/完成房间计数
  - 当前位置
  - 步数与剩余步数
  - 追猎状态标志
  - 当前游戏阶段

### 3. 测试套件

#### ExplorationRecoveryTests.cs
独立单元测试，验证探索状态恢复的核心逻辑：
- `CreateSnapshotFromSave` - 从JigsawSave创建快照
- `RestorePreservesVisited` - 恢复保留已访问房间
- `HandlePlayerReconnection` - 玩家重连场景
- `StepTrackingPersists` - 步数计数持久化
- `DetectHuntingPhase` - 追猎阶段检测
- `MultiPlayerStateIndependent` - 多人独立性

#### LobbyExplorationRecoveryTests.cs
集成测试，验证大厅恢复与探索状态的协作：
- `LobbyInitializesExplorationState` - 大厅初始化包含探索状态
- `ExplorationPersistsAcrossReconnect` - 探索状态在重连间隔保持
- `RemainingStepsCorrectAfterRecovery` - 剩余步数正确恢复
- `HuntingPhaseDetectionInRecovery` - 追猎阶段检测恢复
- `MultiPlayerCheckpointRecovery` - 多人检查点恢复
- `CurrentRoomPositionRestored` - 当前房间位置恢复

## 数据流

### 保存流程
```
JigsawRun.CreateSave()
    ↓
JigsawSave (包含已有字段)
    ├─ Visited: int[]      ← 访问的房间ID
    ├─ Completed: int[]    ← 完成的房间ID
    ├─ CurrentNode: int?   ← 当前节点
    ├─ Steps: int          ← 已用步数
    ├─ FirstProgress: int  ← 首次进度计数
    └─ Phase: JigsawPhase  ← 游戏阶段
    ↓
JigsawCheckpoint.Slot.Set(native, checkpointData)
```

### 恢复流程
```
RunState.FromSerializable()
    ↓
InitializeExplorationRecoveryPatch [Harmony Post]
    ↓
ExplorationStateRecovery.GetOrCreate()
    ↓
ExplorationStateRecovery.InitializeFromCheckpoint()
    ↓
JigsawCheckpoint.Slot.TryGet() 检索 JigsawSave
    ↓
ExplorationSnapshot 创建与存储
    ↓
LobbyRecovery.GetLobbyState() 整合状态
```

## 关键设计决策

### 1. 状态共享 vs 玩家独立
- **已实现**：全局探索状态（visited/completed 在所有活跃玩家间共享）
- **原因**：在Jigsaw中，地图的已访问/已完成状态对所有玩家都是相同的
- **未来扩展**：若需要每玩家独立跟踪，可在 `JigsawShopInventories` 模式基础上实现

### 2. 追猎阶段检测
- `Steps > Budget` 时触发追猎
- 在恢复时通过快照检测，无需重新计算
- UI可据此改变商店/宝箱/战斗难度表现

### 3. 多人重连场景
```
场景: P1、P2进入游戏 → P2断连 → P2重连
处理:
  1. 创建保存（包含探索状态）
  2. 恢复JigsawRun（仅P1活跃）
  3. P2调用Reconnect()重新加入
  4. 探索状态自动对齐（已访问/已完成房间相同）
  5. LobbyRecovery确保状态一致性
```

## 集成点

### 与现有系统的关系
1. **JigsawRun** - 数据提供者，CreateSave()已包含所需字段
2. **JigsawCheckpoint** - 持久化存储
3. **LobbyRecovery** - 协调多人恢复流程
4. **UI 层** (将在 #93 实现) - 使用ExplorationStateInfo显示进度

### Harmony补丁
```csharp
[HarmonyPatch(typeof(RunState), nameof(RunState.FromSerializable))]
InitializeExplorationRecoveryPatch
  ├─ Priority: High (确保早期初始化)
  └─ 触发: ExplorationStateRecovery初始化
```

## 验收标准达成

✓ **探索状态完整序列化**：
  - 房间访问记录 (Visited)
  - 房间完成记录 (Completed)
  - 当前位置 (CurrentNode)
  - 步数计数 (Steps, FirstProgress)

✓ **多人同步正确**：
  - 活跃玩家状态与旁观玩家状态区分（通过ActivePlayers/OriginalPlayers）
  - LobbyRecovery.GetLobbyState() 整合所有相关状态

✓ **单元测试覆盖**：
  - 核心恢复逻辑（ExplorationRecoveryTests）
  - 多人集成场景（LobbyExplorationRecoveryTests）

✓ **代码符合标准**：
  - 遵循 CODING_STANDARDS.md
  - 无Harmony patch API查询需求（使用已有RunState API）

## 后续依赖 (#93)

- **UI集成**：ExplorationStateInfo需与UI层绑定
- **保存恢复谬误**：处理格式化异常
- **完整E2E测试**：从保存-断连-恢复全流程验证

## 文件清单

新增：
- `src/Jigsaw/ExplorationStateRecovery.cs` - 核心恢复模块
- `src/Tests/ExplorationRecoveryTests.cs` - 单元测试
- `src/Tests/LobbyExplorationRecoveryTests.cs` - 集成测试

修改：
- `src/Jigsaw/LobbyRecovery.cs` - 增加探索状态整合

**不修改**：
- `src/Jigsaw/JigsawRun.cs` - 已有Visited/Completed/Steps字段，Restore()已支持恢复
- `src/Jigsaw/JigsawTypes.cs` - 无需修改JigsawSave（已包含所有字段）
