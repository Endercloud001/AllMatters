### **可并行开发的 Issue 批次**

#### **Batch 1（立即启动，无依赖阻塞）**：

- `#77` T04 探索、房间完成与进度  
- `#78` T05 商店重访与库存持久化  
- `#79` T06 换幕、双Boss与终局  
- `#95` Map engine 和路径寻找（v0.1.2 基础）

**特点**：四个 issues 完全独立，互不依赖，可并行 4 个 Agent 推进。

#### **Batch 2（#76 完成后启动）**：

- `#81` T08 多人探索与大厅恢复（包含 sub-issues #91-94）  
- `#82-88` Hunt & Battle 系列（T09-T15，7 个 issues）  
- `#96-98` Jigsaw 生成、UI、惩罚系统（依赖 #95 完成）

**特点**：#81 和 #82-88 可并行；#96-98 序列依赖 #95。

#### **Batch 3（后续）**：

- `#99` 多人地图导航（v0.1.2，不依赖 #95-98）

------

### **实施提示词（可复制-粘贴）**

#### **方案 A：逐个 Batch 用 `/implement-spec` 并行编排**

```
/implement-spec

## AllMatters Batch 1: 基础探索与地图引擎

**Tickets**（4 个独立 issues）：

- #77：T04 探索、房间完成与进度
- #78：T05 商店重访与库存持久化
- #79：T06 换幕、双Boss与终局
- #95：Map engine 和路径寻找

**约定**：
- 各 ticket 不共享代码（独立模块）
- 并行 4 个 Agent（或分波）
- 每个 Agent 遵循 /tdd → 构建 → 验收 → 代码审查（双轴）
- Blocker：都依赖 guardrails（已在 main）和 #76 未完成时不涉及存档恢复

**集成**：汇总到 `integration/batch-1` 分支，单次代码审查后合并 main。

**交付清单**：
- [ ] 构建通过 `dotnet build`
- [ ] Pre-commit hook 自动格式化通过
- [ ] CI 绿色（~30s）
- [ ] 代码审查（spec + standards）通过
```

#### **方案 B：分工—— Batch 1 逐个 ticket 用 `/implement`**

若想更细控制每个 ticket，逐个启动 session：

```
/implement

## Issue #77: T04 探索、房间完成与进度

**需求**（从 GitHub Issue 提取）：
- 房间完成状态追踪（DataModel 层）
- 进度条 UI 展示与更新
- 单人存档 → 加载时进度恢复
- 多人时进度同步（RPC/网络消息）

**参考**：
- `docs/v0.1.1-scope.md` § T04
- `docs/game-mechanics.md` § 探索与进度
- `src/Features/Exploration/` 现有代码

**验收标准**：
- 新增单元测试覆盖进度计算
- 手动验收：单人模式探索完成，进度保存 ✓；多人模式两名玩家探索进度同步 ✓
- 构建 + CI 绿色

**不涉及**：存档恢复（#76 完成后的 #81 负责）
```

重复 4 次（#77、#78、#79、#95），每次 `/implement` 新建独立 session。

------

### 