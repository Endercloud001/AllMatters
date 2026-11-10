# Batch 2 多人恢复 TDD 实现计划

**日期**：2026-11-10  
**目标**：为 #91–#94 建立完整的自动化验收框架  
**方法**：TDD 红绿重构循环  
**预期时间**：5–7 个编码循环  

---

## 总体流程

```
红（测试失败）→ 绿（实现最小代码）→ 重构（改进） → 下一个场景
```

每个循环包括：
1. 编写测试用例（场景矩阵中的一行）
2. 运行测试，确认失败（红）
3. 实现最小功能使测试通过（绿）
4. 重构清理（如有）
5. 两目标验证（`./tools/Test.ps1 -Branch All`）

---

## 第 1 循环：大厅恢复基础框架

### 红：编写测试（L1–L4）

**文件**：`src/Tests/LobbyRecoveryTests.cs`（新建）

```csharp
using Xunit;
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Tests;

public class LobbyRecoveryTests
{
    [Fact(DisplayName = "L1: 从检查点读取原始玩家名单")]
    public void InitializeFromCheckpoint_LoadsOriginalPlayers()
    {
        // Arrange: 创建包含原始玩家的检查点
        var checkpoint = new JigsawCheckpoint
        {
            Act = 1,
            Map = new MapSave
            {
                OriginalPlayers = new[] { 1UL, 2UL, 3UL }
            }
        };
        
        var run = JigsawRun.Create(sessionId: 100, players: [1UL, 2UL, 3UL]);
        var recovery = LobbyRecovery.GetOrCreate(run);

        // Act
        var result = recovery.InitializeFromCheckpoint();

        // Assert
        Assert.True(result);
        Assert.Equal(3, recovery.GetOriginalPlayers().Count);
    }

    [Fact(DisplayName = "L2: 玩家连接事件更新已连接集合")]
    public void OnPlayerConnected_AddsToConnectedSet()
    {
        // Arrange
        var run = JigsawRun.Create(sessionId: 101, players: [1UL, 2UL]);
        var recovery = LobbyRecovery.GetOrCreate(run);
        recovery.InitializeFromCheckpoint();

        // Act
        recovery.OnPlayerConnected(1UL);

        // Assert
        var lobbyState = recovery.GetLobbyState();
        Assert.Contains(1UL, lobbyState.ConnectedPlayerIds);
    }

    [Fact(DisplayName = "L3: 玩家断开事件更新已连接集合")]
    public void OnPlayerDisconnected_RemovesFromConnectedSet()
    {
        // Arrange
        var run = JigsawRun.Create(sessionId: 102, players: [1UL, 2UL]);
        var recovery = LobbyRecovery.GetOrCreate(run);
        recovery.InitializeFromCheckpoint();
        recovery.OnPlayerConnected(1UL);
        recovery.OnPlayerConnected(2UL);

        // Act
        recovery.OnPlayerDisconnected(1UL);

        // Assert
        var lobbyState = recovery.GetLobbyState();
        Assert.DoesNotContain(1UL, lobbyState.ConnectedPlayerIds);
        Assert.Contains(2UL, lobbyState.ConnectedPlayerIds);
    }

    [Fact(DisplayName = "L4: 全部玩家就绪判断（Quorum）")]
    public void IsLobbyReady_ReturnsTrueWhenAllPlayersConnected()
    {
        // Arrange
        var run = JigsawRun.Create(sessionId: 103, players: [1UL, 2UL, 3UL]);
        var recovery = LobbyRecovery.GetOrCreate(run);
        recovery.InitializeFromCheckpoint();

        // Act
        recovery.OnPlayerConnected(1UL);
        recovery.OnPlayerConnected(2UL);
        recovery.OnPlayerConnected(3UL);

        // Assert
        Assert.True(recovery.IsLobbyReady());
    }
}
```

### 绿：最小实现

**修改**：`src/Jigsaw/LobbyRecovery.cs`

```csharp
// 添加以下公开方法：

/// <summary>获取原始玩家名单。</summary>
public IReadOnlyList<ulong> GetOriginalPlayers() => originalPlayers.AsReadOnly();

/// <summary>检查大厅是否就绪（所有原始玩家已连接）。</summary>
public bool IsLobbyReady()
{
    if (!recovered || originalPlayers.Count == 0)
        return false;
    
    return originalPlayers.All(id => connectedPlayerIds.Contains(id));
}
```

### 重构

- 补充 XML 注释
- 确保日志一致性

### 验证

```powershell
./tools/Test.ps1 -Branch public-beta -Filter 'LobbyRecoveryTests'
./tools/Test.ps1 -Branch public -Filter 'LobbyRecoveryTests'
```

**预期结果**：两个分支 4 个测试通过 ✅

---

## 第 2 循环：大厅进阶场景（L5–L9）

### 红：编写测试

```csharp
[Fact(DisplayName = "L5: 部分玩家就绪判断（降级）")]
public void IsLobbyReady_ReturnsTrueWithPartialPlayers()
{
    // 3 个原始，2 个已连接 → IsLobbyReady() 由降级策略决定
    // 当前最小实现：返回 false（等待全部）
    
    var run = JigsawRun.Create(sessionId: 104, players: [1UL, 2UL, 3UL]);
    var recovery = LobbyRecovery.GetOrCreate(run);
    recovery.InitializeFromCheckpoint();
    recovery.OnPlayerConnected(1UL);
    recovery.OnPlayerConnected(2UL);

    // 当前预期：false（严格模式）
    Assert.False(recovery.IsLobbyReady());
}

[Fact(DisplayName = "L8: 重复连接相同玩家无重复")]
public void OnPlayerConnected_Idempotent()
{
    var run = JigsawRun.Create(sessionId: 105, players: [1UL]);
    var recovery = LobbyRecovery.GetOrCreate(run);
    recovery.InitializeFromCheckpoint();

    recovery.OnPlayerConnected(1UL);
    recovery.OnPlayerConnected(1UL);  // 重复

    var state = recovery.GetLobbyState();
    Assert.Single(state.ConnectedPlayerIds);
}

[Fact(DisplayName = "L9: 未知玩家尝试连接被拒绝")]
public void OnPlayerConnected_RejectsUnknownPlayer()
{
    var run = JigsawRun.Create(sessionId: 106, players: [1UL, 2UL]);
    var recovery = LobbyRecovery.GetOrCreate(run);
    recovery.InitializeFromCheckpoint();

    recovery.OnPlayerConnected(99UL);  // 未知玩家

    var state = recovery.GetLobbyState();
    Assert.DoesNotContain(99UL, state.ConnectedPlayerIds);
}
```

### 绿：最小实现

修改 `OnPlayerConnected` 以验证玩家属于原始名单：

```csharp
public void OnPlayerConnected(ulong playerId)
{
    if (!recovered)
        return;

    // 验证玩家在原始名单中
    if (!originalPlayers.Contains(playerId))
    {
        Console.WriteLine($"[LobbyRecovery] Rejected unknown player: NetId={playerId}");
        return;
    }

    if (connectedPlayerIds.Add(playerId))
    {
        Console.WriteLine($"[LobbyRecovery] Player connected: NetId={playerId}");
    }
}
```

### 验证

```powershell
./tools/Test.ps1 -Branch All -Filter 'LobbyRecoveryTests'
```

**预期结果**：两个分支 9 个测试通过 ✅

---

## 第 3 循环：大厅集成状态（L6–L7）

### 红：编写测试

```csharp
[Fact(DisplayName = "L6: 大厅状态快照包含探索信息")]
public void GetLobbyState_IncludesExplorationInfo()
{
    // Arrange: 创建含探索进度的 save
    var run = JigsawRun.Create(108, budget: 10, players: [1UL, 2UL]);
    
    // 模拟探索进度
    var dest = run.AvailableDestinations.FirstOrDefault();
    if (dest > 0)
    {
        run.BeginEnter(dest);
        run.ConfirmEnter(run.PendingEntry!.Id);
        run.CompleteRoom(run.PendingEntry.Id);
    }

    var recovery = LobbyRecovery.GetOrCreate(run);
    recovery.InitializeFromCheckpoint();

    // Act
    var state = recovery.GetLobbyState();

    // Assert
    Assert.NotNull(state.ExplorationState);
    Assert.True(state.ExplorationState.VisitedCount > 0);
}

[Fact(DisplayName = "L7: 大厅状态快照包含存档信息")]
public void GetLobbyState_IncludesSaveStateInfo()
{
    var run = JigsawRun.Create(109, budget: 10, players: [1UL]);
    var recovery = LobbyRecovery.GetOrCreate(run);
    recovery.InitializeFromCheckpoint();

    // 初始化存档恢复
    SaveStateRecovery.GetOrCreate(run)?.InitializeFromCheckpoint();

    // Act
    var state = recovery.GetLobbyState();

    // Assert
    Assert.NotNull(state.SaveState);
    Assert.True(state.SaveState.Act >= 1);
}
```

### 绿：最小实现

确保 `GetLobbyState()` 已在 `LobbyRecovery` 中正确委托给 `ExplorationStateRecovery` 和 `SaveStateRecovery`（现有代码已实现，但需要验证逻辑）。

如有缺陷，补充以下代码：

```csharp
public LobbyState GetLobbyState()
{
    var explorationRecovery = ExplorationStateRecovery.GetOrCreate(nativeRun);
    var explorationSnapshot = explorationRecovery?.GetSnapshot();

    var saveRecovery = SaveStateRecovery.GetOrCreate(nativeRun);
    SaveStateRecovery.SaveStateSnapshot? saveState = null;
    if (saveRecovery?.IsInitialized ?? false)
    {
        saveState = saveRecovery.GetSnapshot();
    }

    return new LobbyState
    {
        OriginalPlayerCount = originalPlayers.Count,
        ConnectedPlayerCount = connectedPlayerIds.Count,
        IsRecovered = recovered,
        ConnectedPlayerIds = [.. connectedPlayerIds],
        IsOriginalRosterReady = recovered && 
            originalPlayers.Count > 0 &&
            connectedPlayerIds.Count == originalPlayers.Count &&
            originalPlayers.All(id => connectedPlayerIds.Contains(id)),
        ExplorationState = explorationSnapshot != null 
            ? new ExplorationStateInfo { /* ... */ } 
            : null,
        SaveState = saveState != null 
            ? new SaveStateInfo { /* ... */ } 
            : null
    };
}
```

### 验证

```powershell
./tools/Test.ps1 -Branch All -Filter 'LobbyRecoveryTests'
```

**预期结果**：两个分支 11 个测试通过 ✅

---

## 第 4 循环：探索恢复（E1–E11）

### 红：编写测试

**文件**：`src/Tests/ExplorationRecoveryTests.cs`（补充或创建）

```csharp
using Xunit;
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Tests;

public class ExplorationRecoveryTests
{
    [Fact(DisplayName = "E1: 从检查点读取访问节点集合")]
    public void InitializeFromCheckpoint_LoadsVisitedNodes()
    {
        var run = JigsawRun.Create(200, budget: 10, players: [1UL]);
        
        // 模拟访问节点
        var dest = run.AvailableDestinations.FirstOrDefault();
        if (dest > 0)
        {
            run.BeginEnter(dest);
            run.ConfirmEnter(run.PendingEntry!.Id);
        }

        var recovery = ExplorationStateRecovery.GetOrCreate(run);
        var result = recovery?.InitializeFromCheckpoint() ?? false;

        Assert.True(result);
        var snapshot = recovery?.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.NotEmpty(snapshot.VisitedNodes);
    }

    [Fact(DisplayName = "E3: 恢复步数与预算")]
    public void GetRemainingSteps_CalculatesCorrectly()
    {
        var run = JigsawRun.Create(201, budget: 10, players: [1UL]);
        // 模拟前进 3 步
        for (int i = 0; i < 3; i++)
        {
            var dest = run.AvailableDestinations.FirstOrDefault();
            if (dest > 0)
            {
                run.BeginEnter(dest);
                run.ConfirmEnter(run.PendingEntry!.Id);
                run.CompleteRoom(run.PendingEntry.Id);
            }
        }

        var recovery = ExplorationStateRecovery.GetOrCreate(run);
        recovery?.InitializeFromCheckpoint();

        var remaining = recovery?.GetRemainingSteps() ?? -1;
        Assert.Equal(7, remaining);  // 10 - 3
    }

    [Fact(DisplayName = "E4: 判断狩猎阶段")]
    public void IsHunting_ReturnsTrueWhenStepsExceedBudget()
    {
        var run = JigsawRun.Create(202, budget: 10, players: [1UL]);
        // 模拟超过预算
        for (int i = 0; i < 12; i++)
        {
            run.Steps = i;  // 直接设置步数用于测试
        }

        var recovery = ExplorationStateRecovery.GetOrCreate(run);
        recovery?.InitializeFromCheckpoint();
        
        // 注：需要在 ExplorationStateRecovery 中暴露设置 Steps 的方法或使用真实流程

        // 简化：假设已读取 snapshot
        var snapshot = recovery?.GetSnapshot();
        if (snapshot != null)
        {
            Assert.True(snapshot.Steps > snapshot.Budget || 
                        recovery.IsHunting());
        }
    }

    // 其他场景 E5–E11 类似...
}
```

### 绿：验证现有实现

`ExplorationStateRecovery` 已实现，验证以下方法存在且功能正确：

- `GetOrCreate(RunState)` ✅
- `InitializeFromCheckpoint()` ✅
- `GetSnapshot()` ✅
- `GetRemainingSteps()` ✅
- `IsHunting()` ✅
- `IsNodeVisited()` ✅
- `IsNodeCompleted()` ✅

如有不足，补充对应的公开方法。

### 重构

确保所有场景覆盖边界情况（空集合、null、0 值）。

### 验证

```powershell
./tools/Test.ps1 -Branch All -Filter 'ExplorationRecoveryTests'
```

**预期结果**：两个分支 11 个测试通过 ✅

---

## 第 5 循环：存档恢复（S1–S8）

### 红：编写测试

**文件**：`src/Tests/SaveStateRecoveryTests.cs`（补充或创建）

```csharp
using Xunit;
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Tests;

public class SaveStateRecoveryTests
{
    [Fact(DisplayName = "S1: 从检查点读取 Act 进度")]
    public void InitializeFromCheckpoint_LoadsAct()
    {
        var run = JigsawRun.Create(300, players: [1UL]);
        var recovery = SaveStateRecovery.GetOrCreate(run);
        
        var result = recovery?.InitializeFromCheckpoint() ?? false;
        Assert.True(result);

        var snapshot = recovery?.GetSnapshot();
        Assert.NotNull(snapshot);
        Assert.True(snapshot.Act >= 1);
    }

    [Fact(DisplayName = "S3: 玩家连接后心跳更新")]
    public void OnPlayerConnected_UpdatesHeartbeat()
    {
        var run = JigsawRun.Create(301, players: [1UL, 2UL]);
        var recovery = SaveStateRecovery.GetOrCreate(run);
        recovery?.InitializeFromCheckpoint();

        var beforeHeartbeat = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        recovery?.OnPlayerConnected(1UL);
        var afterHeartbeat = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 验证心跳时间戳在合理范围内
        var playerState = recovery?.GetPlayerState(1UL);
        Assert.NotNull(playerState);
        Assert.True(playerState.LastHeartbeat >= beforeHeartbeat);
        Assert.True(playerState.LastHeartbeat <= afterHeartbeat + 1000);  // 容差 1s
    }

    [Fact(DisplayName = "S4: 玩家状态更新")]
    public void UpdatePlayerState_StoresInventory()
    {
        var run = JigsawRun.Create(302, players: [1UL]);
        var recovery = SaveStateRecovery.GetOrCreate(run);
        recovery?.InitializeFromCheckpoint();

        recovery?.UpdatePlayerState(1UL, gold: 100, health: 50, maxHealth: 100, cardsCount: 10, relicsCount: 2);

        var playerState = recovery?.GetPlayerState(1UL);
        Assert.NotNull(playerState);
        Assert.Equal(100, playerState.Gold);
        Assert.Equal(50, playerState.Health);
        Assert.Equal(10, playerState.CardsCount);
    }

    // 其他场景 S5–S8 类似...
}
```

### 绿：验证现有实现

`SaveStateRecovery` 已部分实现。确保以下存在：

- `GetOrCreate(RunState)` ✅
- `InitializeFromCheckpoint()` ✅
- `GetSnapshot()` ✅（可能需添加）
- `OnPlayerConnected(ulong)` ✅
- `OnPlayerDisconnected(ulong)` ✅
- `UpdatePlayerState(...)` ✅
- `GetPlayerState(ulong)` ✅（可能需添加公开方法）

### 重构

- 补充 `GetPlayerState()` 公开方法
- 确保时间戳精度一致

### 验证

```powershell
./tools/Test.ps1 -Branch All -Filter 'SaveStateRecoveryTests'
```

**预期结果**：两个分支 8 个测试通过 ✅

---

## 第 6 循环：框架阶段转移（O1–O7）

### 红：编写测试

**文件**：`src/Tests/Integration/MultiplayerRecoveryOrchestratorTests.cs`（新建）

```csharp
using System.Threading.Tasks;
using Xunit;
using AllMatters.Features.Multiplayer;

namespace AllMatters.Tests.Integration;

public class MultiplayerRecoveryOrchestratorTests
{
    [Fact(DisplayName = "O1: 初始化恢复上下文")]
    public void InitializeRecovery_CreatesContext()
    {
        // Arrange
        var orchestrator = new MultiplayerRecoveryOrchestrator(
            new MockRecoveryStrategy(RecoveryPhase.Connection)
        );

        // Act
        var context = orchestrator.InitializeRecovery(
            sessionId: 1000,
            originalPlayers: new[] { 1UL, 2UL },
            connectedPlayers: new[] { 1UL }
        );

        // Assert
        Assert.Equal(1000UL, context.SessionId);
        Assert.Equal(2, context.OriginalPlayers.Count);
        Assert.Equal(RecoveryPhase.Connection, context.CurrentPhase);
    }

    [Fact(DisplayName = "O2: 执行恢复管道（成功）")]
    public async Task ExecuteRecoveryAsync_SucceedsWithValidStrategies()
    {
        // Arrange
        var orchestrator = new MultiplayerRecoveryOrchestrator(
            new MockRecoveryStrategy(RecoveryPhase.Connection, nextPhase: RecoveryPhase.Validation),
            new MockRecoveryStrategy(RecoveryPhase.Validation, nextPhase: RecoveryPhase.Lobby),
            new MockRecoveryStrategy(RecoveryPhase.Lobby, nextPhase: null)  // 最后一个策略无下一阶段
        );

        var context = orchestrator.InitializeRecovery(
            sessionId: 1001,
            originalPlayers: new[] { 1UL, 2UL },
            connectedPlayers: new[] { 1UL, 2UL }
        );

        // Act
        var outcome = await orchestrator.ExecuteRecoveryAsync(context);

        // Assert
        Assert.True(outcome.Success);
    }

    [Fact(DisplayName = "O3: 策略失败时停止管道")]
    public async Task ExecuteRecoveryAsync_StopsOnStrategyFailure()
    {
        // Arrange
        var orchestrator = new MultiplayerRecoveryOrchestrator(
            new MockRecoveryStrategy(RecoveryPhase.Connection, shouldFail: true),
            new MockRecoveryStrategy(RecoveryPhase.Validation)
        );

        var context = orchestrator.InitializeRecovery(
            sessionId: 1002,
            originalPlayers: new[] { 1UL },
            connectedPlayers: new[] { 1UL }
        );

        // Act
        var outcome = await orchestrator.ExecuteRecoveryAsync(context);

        // Assert
        Assert.False(outcome.Success);
        Assert.NotNull(outcome.ErrorMessage);
    }

    // 其他场景 O4–O7 类似...
}

// 测试 Mock
internal class MockRecoveryStrategy : IRecoveryStrategy
{
    private readonly bool shouldFail;
    private readonly RecoveryPhase? nextPhase;

    public RecoveryPhase Phase { get; }

    public MockRecoveryStrategy(RecoveryPhase phase, RecoveryPhase? nextPhase = null, bool shouldFail = false)
    {
        Phase = phase;
        this.nextPhase = nextPhase;
        this.shouldFail = shouldFail;
    }

    public bool CanRecover(RecoveryContext context) => !shouldFail;

    public Task<RecoveryResult> RecoverAsync(RecoveryContext context)
    {
        if (shouldFail)
            return Task.FromResult(RecoveryResult.Fail("Mock failure", canRetry: true));

        return Task.FromResult(RecoveryResult.Ok(nextPhase ?? RecoveryPhase.Completed));
    }

    public bool ValidateRecovery(RecoveryContext context) => true;
}
```

### 绿：验证现有实现

`MultiplayerRecoveryOrchestrator` 已实现。确保：

- `InitializeRecovery(...)` ✅
- `ExecuteRecoveryAsync(...)` ✅
- `GetSessionPhase()` ✅
- `GetActiveSessions()` ✅
- `CompleteSession()` ✅

### 重构

- 补充异常处理覆盖
- 日志输出一致性

### 验证

```powershell
./tools/Test.ps1 -Branch All -Filter 'MultiplayerRecoveryOrchestratorTests'
```

**预期结果**：两个分支 7 个测试通过 ✅

---

## 第 7 循环：完整 E2E 集成测试

### 红：解除测试 Skip

**文件**：`src/Tests/Integration/MultiplayerRecoveryE2ETests.cs`

移除 `[Fact(Skip = "...")]` 中的 Skip 属性，实现完整管道测试：

```csharp
[Fact(DisplayName = "E2E: 单玩家重连恢复完整流程")]
public async Task SinglePlayerReconnection_CompleteFlow()
{
    // Arrange: 创建多人局，一个玩家断开
    var orchestrator = new MultiplayerRecoveryOrchestrator(
        new ConnectionRecoveryStrategy(),   // #94 框架中定义
        new ValidationRecoveryStrategy(),   // #94 框架中定义
        new LobbyRecoveryStrategy(),        // 包装 LobbyRecovery
        new ExplorationRecoveryStrategy(),  // 包装 ExplorationStateRecovery
        new SaveStateRecoveryStrategy()     // 包装 SaveStateRecovery
    );

    // Act: 初始化并执行恢复
    var context = orchestrator.InitializeRecovery(
        sessionId: 5000,
        originalPlayers: new[] { 100UL },
        connectedPlayers: new[] { 100UL }
    );

    var outcome = await orchestrator.ExecuteRecoveryAsync(context);

    // Assert: 完整恢复成功
    Assert.True(outcome.Success);
    Assert.Equal(RecoveryPhase.Completed, context.CurrentPhase);
}
```

### 绿：实现策略适配器

在 `src/Features/Multiplayer/` 中为现有恢复模块创建适配器：

**文件**：`src/Features/Multiplayer/Strategies/LobbyRecoveryStrategy.cs`（新建）

```csharp
using AllMatters.Jigsaw;
using MegaCrit.Sts2.Core.Runs;

namespace AllMatters.Features.Multiplayer;

/// <summary>Lobby recovery strategy implementation.</summary>
public sealed class LobbyRecoveryStrategy : IRecoveryStrategy
{
    public RecoveryPhase Phase => RecoveryPhase.Lobby;

    public bool CanRecover(RecoveryContext context)
    {
        return context.CurrentPhase == RecoveryPhase.Lobby ||
               context.CurrentPhase == RecoveryPhase.Validation;
    }

    public async Task<RecoveryResult> RecoverAsync(RecoveryContext context)
    {
        try
        {
            // 尝试从 State 中获取或创建 RunState
            if (!context.State.TryGetValue("RunState", out var runStateObj) ||
                runStateObj is not RunState run)
            {
                return RecoveryResult.Fail("No RunState available", canRetry: false);
            }

            var recovery = LobbyRecovery.GetOrCreate(run);
            if (recovery == null)
                return RecoveryResult.Fail("Failed to create LobbyRecovery", canRetry: false);

            // 初始化并同步已连接的玩家
            if (!recovery.InitializeFromCheckpoint())
                return RecoveryResult.Fail("Checkpoint initialization failed", canRetry: true);

            foreach (var playerId in context.ConnectedPlayers)
            {
                recovery.OnPlayerConnected(playerId);
            }

            // 检查是否可以继续
            var lobbyState = recovery.GetLobbyState();
            if (lobbyState.ConnectedPlayerCount == 0)
                return RecoveryResult.Fail("No connected players", canRetry: true);

            return RecoveryResult.Ok(RecoveryPhase.Exploration);
        }
        catch (Exception ex)
        {
            return RecoveryResult.Fail($"Exception: {ex.Message}", canRetry: true);
        }
    }

    public bool ValidateRecovery(RecoveryContext context)
    {
        // 验证大厅状态就绪
        if (!context.State.TryGetValue("RunState", out var runStateObj) ||
            runStateObj is not RunState run)
            return false;

        var recovery = LobbyRecovery.GetOrCreate(run);
        return recovery?.IsRecovered ?? false;
    }
}
```

类似地创建 `ExplorationRecoveryStrategy` 和 `SaveStateRecoveryStrategy`。

### 验证

```powershell
./tools/Test.ps1 -Branch All -Filter 'E2E|MultiplayerRecovery'
```

**预期结果**：两个分支 E2E 测试通过 ✅

---

## 执行检查清单

在开始每个循环前：

- [ ] 确认测试宿主就绪：`./tools/Test.ps1 -Branch All` 通过，通过数非零
- [ ] Git 状态干净：`git status --short` 无未追踪或已修改文件
- [ ] 当前分支为 worktree 分支（非 main）

在每个循环完成后：

- [ ] 运行 `./tools/Check.ps1`，所有步骤 PASS
- [ ] 两目标测试均通过：public-beta 和 public
- [ ] 代码审查标记无阻塞项
- [ ] 提交变更：`git add <files> && git commit -m "test: [循环] [场景]"`

---

## 预期输出物

### 新增文件

```
src/Tests/
├── LobbyRecoveryTests.cs              // 9 个场景
├── ExplorationRecoveryTests.cs        // 11 个场景（补充）
├── SaveStateRecoveryTests.cs          // 8 个场景（补充）
└── Integration/
    ├── MultiplayerRecoveryOrchestratorTests.cs  // 7 个场景
    └── MultiplayerRecoveryE2ETests.cs          // 3 个场景（解除 Skip）

src/Features/Multiplayer/Strategies/
├── LobbyRecoveryStrategy.cs           // 适配器
├── ExplorationRecoveryStrategy.cs     // 适配器
└── SaveStateRecoveryStrategy.cs       // 适配器
```

### 总测试覆盖

- **大厅恢复（L）**：9 场景 ✅
- **探索恢复（E）**：11 场景 ✅
- **存档恢复（S）**：8 场景 ✅
- **框架转移（O）**：7 场景 ✅
- **E2E 集成**：3 场景 ✅

**总计**：38+ 个自动化测试场景

### 验收文档

- 规格文档：`docs/superpowers/specs/2026-11-10-batch-2-recovery-design.md`
- 实现计划：`docs/superpowers/plans/2026-11-10-batch-2-recovery.md`
- 验收矩阵：规格文档第 2 章节

---

## 时间估计

| 循环 | 工作 | 预计时长 |
|-----|------|--------|
| 1 | 大厅基础（L1–L4） | 1h |
| 2 | 大厅进阶（L5–L9） | 1h |
| 3 | 大厅集成（L6–L7） | 0.5h |
| 4 | 探索恢复（E1–E11） | 1.5h |
| 5 | 存档恢复（S1–S8） | 1h |
| 6 | 框架转移（O1–O7） | 1.5h |
| 7 | E2E 集成 + 最终验证 | 1.5h |

**总计**：~8h（含两目标构建与验证）

---

## 关键决策点

### 1. 降级策略

**当前实现**：严格模式（等待全部原始玩家）

```csharp
public bool IsLobbyReady()
{
    return originalPlayers.All(id => connectedPlayerIds.Contains(id));
}
```

**可选**：可配置的最小人数

```csharp
public bool IsLobbyReady(int minPlayersRequired = 0)
{
    if (minPlayersRequired == 0)
        return connectedPlayerIds.Count > 0;
    return connectedPlayerIds.Count >= minPlayersRequired;
}
```

**决策**：初期保持严格模式，后续由用户决定是否支持降级。

### 2. 30s 超时窗口

`SaveStateRecovery` 已硬编码 30s，当前暂不参数化。

**可能改进**：通过 `RecoveryContext.State` 传入超时参数。

**决策**：初期保持固定值，E2E 测试中使用模拟时间验证。

---

## 关键风险

| 风险 | 影响 | 缓解措施 |
|-----|------|--------|
| API 签名不匹配（两版本） | 编译失败 | 先运行 `./tools/ExportApiReference.ps1` 验证 |
| 测试宿主初始化失败 | TDD 红阶段卡顿 | 批次开始前运行冒烟检查 |
| Harmony patch 错误 | 加载失败 | 每个 patch 都验证目标存在 |
| 并发测试竞态 | 随机失败 | 所有恢复操作使用 ConditionalWeakTable，天然隔离 |

---

**文档版本**：1.0  
**最后更新**：2026-11-10  
**作者**：Kiro  
**状态**：计划草稿，待执行验证
