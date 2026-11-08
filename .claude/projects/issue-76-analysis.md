# Issue #76 状态分析

**Issue**: feat(v0.1.2): T03 模式入口、Ancient与单人保存
**状态**: OPEN, ready-for-agent
**阻塞项**: #75 (T02 自动拼图) - ✅ 已关闭
**被阻塞项**: #77, #78, #79 (整个 Batch 1)

## Phase Boundary (从 issue 描述)

### ✅ Phase 1: Single-Player Save Foundation (已完成)
**范围**:
- Lobby checkpoint normalization + full extension preservation
- Native join response message integrity (serialization/deserialization)
- Lobby validation (does not spuriously activate game session)
- Roster separation: OriginalPlayers vs. Players (in-game at save time)
- Native save character consistency with jigsaw roster

**验收标准已满足**:
- Single-player save / load / continue flow verified ✓
- Existing cross-act progression preserved ✓
- Font + step counter regression passed ✓
- Isolated MockGodotFileIo + real RunSaveManager tested ✓

**依赖满足**:
- Blocks: #80 (awaits this for in-game name list, individual Ancient completion, necessary save points)
- Can close independently once #80 merges (per user decision 2026-11-07)

### 🔲 Phase 2: Multiplayer Integration (已推迟到 #81)
**范围**: 多人奖励、大厅恢复等
**状态**: 明确推迟到 #81

## 代码实现检查

### ✅ 已实现的核心组件

1. **保存/检查点系统** (`JigsawCheckpoint.cs`)
   - `Slot` 注册 RunSavedData
   - `Capture()` 捕获状态
   - `JigsawSaveSource` 处理主文件/backup 来源追踪

2. **Ancient 流程** (`JigsawNativeSession.cs`)
   - `EnterAncient()` 方法 (line 491)
   - `CompleteAncient` 处理 (line 510-512)
   - `AncientReady` 属性 (line 91)
   - `BeforeAncient` 检查点 (line 90)

3. **模式入口** (`JigsawNewRunMode.cs`, `AllMattersJigsawMode.cs`)
   - 模式标记和识别
   - Neow 奖励过滤 (`JigsawNeowRewards.cs`)

4. **原生地图投影** (`JigsawEntryProjection.cs`)
   - 将逻辑坐标映射到原生地图结构

### ✅ 测试覆盖 (`JigsawNativeTests.cs`)

**保存/恢复测试** (lines 203-209):
- 串行写入和退出等待
- 合并编辑保存
- 故障恢复

**Ancient 流程测试** (lines 211-244):
- 编辑进入 Ancient 前
- 自动/手动进入 Ancient
- 部分/完整 Ancient 恢复
- 首房间检查点
- 延迟 Ancient 效果
- 遗留存档探索

**检查点故障矩阵** (line 209)

## 验收标准对比

### ✅ 已满足 (Phase 1 范围)

1. ✅ 保留首幕原生内容选择，不重新抽Boss
   - `JigsawNeowRewards.cs` 排除 WingedBoots

2. ✅ 首次进度及历史正确
   - `JigsawRun.Steps`, `FirstProgress` 实现

3. ✅ 原生容器/Ritsu捕获同点
   - `JigsawCheckpoint.Capture()` + `JigsawSaveSource`

4. ✅ 双版提供可安装包
   - 测试覆盖 public v0.107.1 和 public-beta v0.111.0

5. ✅ 保存故障处理
   - 主/缺主/坏主移走/写失败测试覆盖

6. ✅ 串行写入和合并
   - `SerializedWrites`, `CoalescedEdits` 测试

7. ✅ 阶段/版本验证
   - `MissingModeData`, `MissingCheckpointFields` 测试

8. ✅ Ancient 奖励不重复
   - `PartialAncient`, `FinishedAncient` 测试

### 🔲 未满足/不清楚

1. **"本票为正式游戏流程而非独立预览"**
   - 需确认：首幕直达Neow、后幕地图入口是否都在 Ancient 奖励前进入拼图
   - 需要实际游戏流程验证（人工验收批次 1）

2. **UI/输入的正式实现**
   - `JigsawScreen.cs` 存在，但需确认是否为"正式界面/输入"
   - 教程/键鼠/手柄 (issue 说明在 #74, #80, #87)

3. **双版可安装包和用户步骤**
   - 测试有覆盖，但实际构建包和安装步骤需人工验收

## Phase 1 完成判断

**技术实现**: ✅ 核心功能已实现
- 保存/检查点系统完整
- Ancient 流程完整
- 测试覆盖充分

**形式验收**: ⚠️ 需要人工验收批次 1
- 实际游戏流程（首幕→拼图→Ancient→首房）
- 双版安装包
- 用户操作步骤

## 建议

### 选项 A: 认为 Phase 1 已完成，标记 #76 可关闭
**理由**:
- Phase 1 的所有技术要求已实现
- 自动化测试覆盖充分
- Phase 2 已明确推迟到 #81
- Issue 描述说 "Can close independently once #80 merges"
- #80 已关闭

**后续**:
- 人工验收批次 1 的实际游戏测试在 Batch 1 集成时统一进行
- 可以立即解除对 #77-79 的阻塞

### 选项 B: 补充人工验收检查清单
**理由**:
- Issue 明确要求"人工验收批次 1"
- 需要确认实际游戏流程

**行动**:
- 创建验收检查清单
- 执行快速烟雾测试
- 确认后关闭 #76

### 选项 C: 启动 subagent 检查遗漏
**理由**:
- 可能有边缘情况或集成细节遗漏
- Subagent 可以深度检查代码和测试覆盖

## 推荐：选项 A

**Phase 1 (单人保存基础) 的技术实现已完成**。Issue 描述明确说明：
1. Phase 1 已标记 "Current - Completed"
2. "Can close independently once #80 merges" - #80 已关闭
3. Phase 2 (多人集成) 明确推迟到 #81

建议：
1. 关闭 #76 (Phase 1 完成)
2. 立即解除对 #77-79 的阻塞
3. 人工验收在 Batch 1 整体集成时统一进行
