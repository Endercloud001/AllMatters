# Issue #81 实施路线图：多人探索与原生大厅恢复

**版本**：v0.1.2（拼图更新）  
**优先级**：Batch 2（#76 完成后启动）  
**工作区**：issue-81-lobby-recovery worktree  
**关键依赖**：#76（存档恢复基础）  

---

## 工单汇总与关键需求

### #81 母工单：多人探索与原生大厅恢复
- **范围**：多人游戏中探索状态同步、存档降级、大厅恢复流程
- **关键产物**：LobbyRecovery 系统、多人状态管理框架
- **测试宿主**：HeadlessCombatHost（双分支）

### #91 子工单：多人游戏大厅原生适配
**当前状态**：未开始  
**关键需求**：
- 将 Ritsu 多人大厅与原生 RunLobby 适配
- 实现大厅连接监听与玩家状态追踪
- 初始化拼图编辑和探索阶段的原生大厅回调

**关键数据结构**：
```csharp
// src/Jigsaw/JigsawNativeSession.cs（已存在）
- OriginalPlayers: ulong[] - 初始玩家列表
- ActivePlayers: ulong[] - 当前连接玩家
- Players: HashSet<ulong> - 内部玩家追踪
```

**交付物**：
- [ ] JigsawNativeSession 完整实现（currently ~1200 LOC）
- [ ] 大厅事件处理与 Harmony patch
- [ ] 多人连接状态同步

---

### #92 子工单：多人探索状态同步
**当前状态**：未开始  
**前置**：#91 完成  
**关键需求**：
- 同步已访问节点列表（visited）
- 同步已完成房间列表（completed）
- 同步当前节点状态（CurrentNode）
- 实现步数计数器网络同步

**关键数据结构**：
```csharp
// src/Jigsaw/JigsawTypes.cs
public sealed record JigsawSave(
    JigsawPhase Phase,           // Editing, Inspecting, Generating, Ancient, Exploring
    int Budget,                  // 15/14/13 per act
    ulong[] Players,             // Active player IDs
    int Steps,                   // Current step count
    int FirstProgress,           // First player to enter?
    int[] Visited,               // Visited node IDs
    int[] Completed,             // Completed node IDs
    int? CurrentNode,            // Current exploration node
    JigsawEnterAttempt? PendingEntry,  // Pending room entry
);

// Additional per-player state needed:
public Dictionary<ulong, ExplorationState> PlayerStates {
    int? CurrentNode;
    bool PendingChoice;
    JigsawPhase LocalPhase;
}
```

**交付物**：
- [ ] 多人 RPC 消息定义（访问、完成、移动、步数）
- [ ] 状态同步测试（单人保存 → 多人加载验证）
- [ ] 网络调度与冲突处理

---

### #93 子工单：存档降级与多人兼容
**当前状态**：未开始  
**前置**：#92 完成  
**关键需求**：
- 实现多人存档到单人的降级（duplicate player state）
- 保留单人兼容的主要状态
- 处理多人特定字段的默认值赋予
- 禁用不兼容的多人功能（房间独立选择等）

**关键数据结构**：
```csharp
// Downgrade mapping:
JigsawSave (Multiplayer)  →  SerializableRun (Single-player)
├── Players[0]            →  Primary owner
├── Visited[]             →  Merged union or first player
├── Completed[]           →  Merged union or first player
├── CurrentNode           →  First player's node or Ancient
├── ShopStates            →  Player[0]'s inventory per node
└── Phase                 →  Convert to native sequence
```

**交付物**：
- [ ] 降级逻辑实现（compat/0.107.1 + compat/0.111.0）
- [ ] 降级验证测试
- [ ] 单人加载多人存档流程文档

---

### #94 子工单：多人完成与大厅恢复
**当前状态**：未开始  
**前置**：#93 完成  
**关键需求**：
- 实现玩家断线重连时的大厅恢复流程
- 验证所有玩家已连接后进入原生会话
- 管理多人会话生命周期（创建 → 同步 → 验收 → 结束）
- 处理超时与异常断线情况

**关键数据结构**：
```csharp
// src/Jigsaw/LobbyRecovery.cs (to be created)
public sealed class LobbyRecovery {
    private JigsawSave checkpoint;
    private Dictionary<ulong, PlayerConnectionState> playerStates;
    private ManualResetEvent allPlayersReady;
    
    // Expected methods:
    public static LobbyRecovery FromCheckpoint(JigsawSave save);
    public void SyncConnectionState(ulong playerId, bool connected);
    public bool IsReadyToEnter { get; }
    public JigsawSave GetRestoredSave();
}
```

**交付物**：
- [ ] LobbyRecovery 完整实现
- [ ] 连接监听与重连流程
- [ ] 超时与失败恢复处理
- [ ] 集成测试（模拟断线重连）

---

## 关键数据结构与状态机

### 探索阶段状态机

```
┌─────────────────────────────────────────────────────────────┐
│                   JigsawPhase Flow                           │
└─────────────────────────────────────────────────────────────┘

Editing ──→ Inspecting ──→ Generating ──→ Ancient ──→ Exploring
   ↓
 (players drag & place pieces)
 
Ancient
 ↓
 (all players depart ancient→first room)
 
Exploring
 ├─→ (all players enter same room)
 ├─→ (steps -= 1)
 ├─→ (room completed for all)
 └─→ (continue until Boss or step overflow)
```

### 多人状态结构

```csharp
// Shared state (replicated across all players)
JigsawSave {
    Phase: JigsawPhase,
    Visited: int[] (shared),
    Completed: int[] (shared),
    Steps: int (shared countdown),
    CurrentNode: int? (shared, must agree before entry),
    PendingEntry: JigsawEnterAttempt? (queued room entry),
}

// Per-player state (local only until sync)
PlayerExplorationState {
    PlayerId: ulong,
    SelectedNode: int? (local choice before sync),
    IsReady: bool,
    LastSyncTime: DateTime,
}
```

### 降级规则

| 多人属性 | 单人映射 | 行为 |
|---------|--------|------|
| Players[] | 主玩家 (Players[0]) | 保留 ID，其他丢弃 |
| Visited[] | Merged 或 Players[0] 的值 | 选择保守值（更多探索） |
| CurrentNode | Players[0] 的当前节点 | 保留第一玩家状态 |
| ShopStates | Players[0] 的库存快照 | 丢弃其他玩家购买记录 |
| Phase | Convert to Native flow | Editing→Ancient→Exploring |

---

## 依赖关系图

```
Issue #76 (存档恢复基础)
     │
     ├─→ #81 (多人探索 & 大厅)
           │
           ├─→ #91 (大厅原生适配)
           │     │
           │     └─→ #92 (多人状态同步)
           │           │
           │           └─→ #93 (存档降级)
           │                 │
           │                 └─→ #94 (大厅恢复)
           │
     └─→ #82-88 (Hunt & Battle 系列)
           │
           └─→ #96-98 (Jigsaw 生成、UI、惩罚)
                    ↑ 依赖 #95 (Map engine)
```

**并行项**：#81 与 #82-88 完全独立，可同时推进。

---

## 测试策略

### 单元测试清单

**JigsawRun 状态验证** (已有框架)
- [ ] Multi-player 编辑阶段状态追踪
- [ ] 步数计数器同步
- [ ] 已访问/完成节点集合操作
- [ ] 玩家加入/离开时状态变化

**多人状态同步**
- [ ] RPC 消息序列化/反序列化
- [ ] 状态冲突检测（不同玩家不同步）
- [ ] 网络延迟下的状态一致性

**存档降级**
- [ ] 多人 JigsawSave → 单人 SerializableRun 转换
- [ ] 字段映射完整性检查
- [ ] 兼容版本混合测试（public 0.107.1 vs public-beta 0.111.0）

**大厅恢复**
- [ ] 连接状态变化追踪
- [ ] 超时处理与重试逻辑
- [ ] 不完全连接下的拒绝行为

### 集成测试清单

**无界面测试（HeadlessCombatHost）**

```powershell
# Setup
./tools/Test.ps1 -Filter "multiplayer lobby recovery"

# Test sequences
1. Create 2-player run → Jigsaw editing → Save checkpoint
2. Simulate player disconnect → Check LobbyRecovery.IsReady = false
3. Reconnect player → Check IsReady = true
4. Enter ancient → Verify state synced across players
5. Player 1 explores → Player 2 still in ancient → Step doesn't decrement
6. Both players move to same room → Step decrements
7. Save state → Load as single-player → Verify downgrade
```

**双分支验证**

```powershell
./tools/Test.ps1 -Branch All -Filter "multiplayer"
# Ensures compatibility with public 0.107.1 and public-beta 0.111.0
```

### 手动验收步骤

1. **多人编辑阶段**
   - [ ] 2 名玩家进入大厅 → 拼图编辑 UI 出现
   - [ ] 玩家 1 拖拽拼图块 → 玩家 2 实时看到
   - [ ] 玩家 1 旋转块 → 玩家 2 看到旋转
   - [ ] 两名玩家都点击"开始探索" → 进入古代文明节点

2. **多人探索同步**
   - [ ] 两名玩家出发古代文明 → 步数显示 15（第一幕）
   - [ ] 玩家 1 进入房间 A → 等待玩家 2
   - [ ] 玩家 2 进入房间 A → 步数变成 14，房间标记完成
   - [ ] 重复 3 次不同房间 → 步数准确倒计时

3. **大厅恢复**
   - [ ] 多人游戏中途，玩家 1 掉线
   - [ ] 游戏暂停，显示"等待玩家 1 重新连接"
   - [ ] 玩家 1 重新连接 → 大厅恢复，游戏继续
   - [ ] 继续探索 → 状态一致

4. **存档降级**
   - [ ] 多人游戏进行到房间 5（步数 10）
   - [ ] 保存多人存档
   - [ ] 以单人模式加载该存档
   - [ ] 确认步数、已访问/完成节点保留
   - [ ] 商店库存为玩家 1 的状态

---

## 关键文件与代码段

### 现存代码入口

| 文件 | 关键类/方法 | 用途 |
|-----|----------|------|
| `src/Jigsaw/JigsawNativeSession.cs` | `JigsawNativeSession.AttachLobby()` | 原生大厅接入点 |
| `src/Jigsaw/JigsawRun.cs` | `JigsawRun.SelectedEndpoints`, `Visited`, `Completed` | 多人状态管理 |
| `src/Jigsaw/JigsawTypes.cs` | `JigsawSave`, `JigsawPhase` | 状态序列化与生命周期 |
| `src/Jigsaw/JigsawCheckpoint.cs` | Checkpoint 保存逻辑 | 存档快照 |
| `src/Tests/HeadlessCombatHost.cs` | `HeadlessTestSetup` | 测试宿主框架 |

### 待创建文件

| 文件 | 类 | 职责 |
|-----|---|------|
| `src/Jigsaw/LobbyRecovery.cs` | `LobbyRecovery` | 多人大厅恢复管理器 |
| `src/Jigsaw/MultiplayerStateSync.cs` | `MultiplayerStateSync` | 跨客户端状态同步 | 
| `src/Jigsaw/ExplorationDowngrade.cs` | `ExplorationDowngrade` | 多人→单人存档转换 |
| `src/Tests/MultiplayerJigsawTests.cs` | 测试类 | 多人状态与同步测试 |
| `src/Tests/LobbyRecoveryTests.cs` | 测试类 | 大厅恢复集成测试 |

### 关键 Harmony Patches 清单

```csharp
// #91: 大厅初始化
[HarmonyPatch(typeof(RunManager), "InitializeMultiplayerLobby")]
internal static class JigsawLobbyInit

// #92: 房间完成通知
[HarmonyPatch(typeof(RoomManager), "CompleteRoom")]
internal static class JigsawRoomCompletion

// #93: 存档保存钩子
[HarmonyPatch(typeof(SaveManager), "SerializeRun")]
internal static class JigsawSaveDowngrade

// #94: 大厅连接状态变化
[HarmonyPatch(typeof(RunLobby), "HandlePlayerConnection")]
internal static class JigsawLobbyConnection
```

---

## 构建与验证命令

### 构建检查
```powershell
# 编译两个目标
./tools/Build.ps1 -Branch public-beta
./tools/Build.ps1 -Branch public -GameDataDir 'D:/STS2-public/data_sts2_windows_x86_64'

# 运行完整检查
./tools/Check.ps1
```

### 冒烟测试
```powershell
# 开工前健康检查
./tools/Test.ps1 -Branch All

# 检查通过标志
./tools/ReadTestLog.ps1 -Path .local/tests/public-beta-last.log
./tools/ReadTestLog.ps1 -Path .local/tests/public-last.log
```

### 子工单测试
```powershell
# #91 大厅适配
./tools/Test.ps1 -Filter "native session"

# #92 多人同步
./tools/Test.ps1 -Filter "multiplayer sync"

# #93 降级
./tools/Test.ps1 -Filter "exploration downgrade"

# #94 恢复
./tools/Test.ps1 -Filter "lobby recovery"
```

---

## 验收标准（DoD）

### 每个子工单的通用 DoD

- [ ] 代码编译通过（两分支）
- [ ] 单元测试 100% 通过
- [ ] 集成测试验证
- [ ] 代码审查通过（Spec + Standards 双轴）
- [ ] 提交信息标准格式
- [ ] 无新的 compiler warnings

### 母工单 #81 总体 DoD

- [ ] 四个子工单都完成（#91-94）
- [ ] 多人游戏从大厅到探索完整流程工作
- [ ] 大厅恢复在网络中断场景下可靠
- [ ] 存档降级保持单人兼容
- [ ] 两分支（0.107.1 + 0.111.0）都验证通过
- [ ] 手动验收清单全部通过
- [ ] 交付包就绪（DLL + manifest + 文档）

---

## 已知风险与缓解

| 风险 | 影响 | 缓解策略 |
|-----|------|--------|
| 网络延迟导致状态不一致 | 玩家看到不同房间 | 实现三向握手（Ready → Confirmed → Proceed） |
| 断线后大厅僵死 | 游戏无法继续 | 设置超时与自动回滚到检查点 |
| 多人→单人存档丢失数据 | 数据损坏或不可恢复 | 保存原始多人存档，降级前备份 |
| 两版本 API 差异 | public 编译/运行时失败 | 双分支测试覆盖关键路径 |
| 商店库存状态不一致 | 玩家获得重复或失去物品 | 使用 ShopStates 快照，每个玩家+节点独立库存 |

---

## 参考文档

- `docs/v0.1.2-scope.md` - 拼图更新总体设计
- `docs/game-mechanics.md` - API 入口与方法体查证流程
- `docs/development.md` - 构建、测试、发布流程
- `docs/git-best-practices.md` - Git worktree 与 PR 管理
- `src/Jigsaw/JigsawTypes.cs` - 数据结构定义
- `.local/api/` - 两版本 DLL 参考实现
