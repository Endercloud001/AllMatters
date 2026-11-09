# Retrospective: Batch 1 开发（T04 探索 / T05 商店 / T06 换幕）

**日期**: 2026-11-09
**分析会话**: 主会话 89486eb6 及其子代理会话（Bug#1 商店修复 / Bug#2 Boss 换幕修复）；第二轮补充用户在 Batch 1 中观察到的流程问题（#78/#79 并行冲突、测试宿主阻塞、交付顺序、文档语言）
**PR**: #102 — Batch 1: T04探索、T05商店、T06换幕实现

---

## 🔴 严重：#78 与 #79 并行实施共用同一工作目录，造成代码混合与生产事故

**现象**: #78（T05 商店）与 #79（T06 换幕）的实施代理同时在主 checkout 工作，未各自创建独立 worktree。两方的未提交改动互相覆盖、交错写入同一批文件（尤其 `src/Jigsaw/JigsawNativeSession.cs`），产生严重的合并错误与文件冲突，最终需要用户批准一次事后修复。

**根因**:
- 派发并行实施时没有强制隔离；`start_session` 的 `use_worktree` 在普通 checkout 下默认共享当前目录。
- 两张票都触及 `JigsawNativeSession.cs` 这一 native 集成热点文件，共享目录下必然冲突。
- 没有「并行票必须隔离」的书面约定，派发方凭默认值行事。

**改进**:
1. **并行实施一律独立 worktree**：派发两个及以上实施代理时，每个都设 `use_worktree: true`，并显式给出 `source_branch`（见下一条）与各自的 `branch_name`。
2. **热点文件串行化**：若多张票都要改 `JigsawNativeSession.cs` 等集成热点，先排定合并顺序；后合并者在前者合入后 rebase/merge 基线，再开始改该文件。
3. **集成分支由主会话独占**：子代理只在自己的分支提交，合入 `integration/batch-N` 由主会话逐个执行并在每次合入后构建。
4. 将上述三条写入 `docs/development.md` 新增「并行实施与 worktree」小节，`AGENTS.md` 加一行指针：「**并行实施**多张工单前读取 `docs/development.md` 的『并行实施与 worktree』。」

---

## 🔴 严重：测试基础设施阻塞 issue 实施

**现象**: 实施期间自动化测试宿主报错 `Attempting to register type AllMatters.Relics.BlueCandle... not associated with any mod!`，测试结果从未输出。TDD 的 red/green 信号完全缺失，T04/T05/T06 的正确性只能靠用户实机测试兜底，两个关键 bug（商店重置、Act 3 首 Boss 直接进结局）因此一直漏到人工验收阶段。

**证据**: `not associated with any mod` 11 次命中；每轮修复都无法用自动化确认。

**根因**:
- `HeadlessTestSetup` 的 mod 注册时序在引入 `BlueCandle` 后与 RitsuLib 注册不再对齐，属于预存问题，但一直被当作「已知提示」放过。
- 开工前没有「测试宿主可用」的前置检查，坏掉的宿主到实施中途才暴露。

**改进**:
1. **开工前冒烟**：每个批次开工前先跑一次 `tools/Test.ps1` 并用 `tools/ReadTestLog.ps1` 确认「完整通过数」非空。宿主 red 时先修宿主（或单独开阻塞票并在批次中标为 `blocked by`），再开始实施。
2. **把宿主修复立为独立工单**：修正 BlueCandle 注册时序，验收条件为 public / public-beta 两目标测试均输出完整通过数。
3. **区分提示与阻断**：`ReadTestLog.ps1` 已单独计数「缺少 Mod 关联」提示；当宿主退出且通过数为空时应视为阻断（非零退出），而非提示。

---

## 🔴 严重：子代理从错误的分支起始

**现象**: Bug#2 修复子代理以 `main` 作为 worktree 起点，而非 `integration/batch-1`。

**证据**: 子代理提交 `d59f077` 包含来自 `main` 上 #80 多人编辑的 6 个文件（`Alpha.cs`、`Beta.cs` 等空检查改动），这些文件与 `integration/batch-1` 无关。需要手动识别并只 cherry-port `JigsawFirstBossReturnsToMap` patch 和 `using` 导入，其余丢弃。

**根因**: 子代理的 prompt 里未明确指定 `source_branch: integration/batch-1`；子代理工具调用中 `start_session` 默认使用仓库默认分支。

**改进**: 派发子代理修复 feature branch 上的 bug 时，prompt 必须写明起点分支：
```
source_branch: integration/batch-1
branch_name: fix/shop-revisit
```
或在 brief 里明确：「你的 worktree 必须从 `integration/batch-1` 切出，不从 `main`。」

---

## 🔴 严重：第一次修复使用了不存在的 API

**现象**: Bug#1（商店物品重置）的第一轮修复使用了 `ShopRoom`、`shopCards`、`shopRelics`，均不存在于 STS2 API，导致修复完全失效。

**证据**: grep 命中 `ShopRoom` 55 次 / `shopCards` 28 次，全部来自失败修复的代码与错误日志。

**根因**: 子代理没有先查询 `.local/api/queries/` 的反编译 API，凭猜测命名写了不存在的成员。正确 API（`MerchantRoom` / `MerchantInventory`）在 `.local/api/queries/public-beta-v0.111.0/` 中有完整记录。

**改进**:

1. **AGENTS.md 导航指针**：针对 Native 层 patch 写法，增加"先查 API"的显式提示。
2. **编码标准**：在 `CODING_STANDARDS.md` 加规则：「任何 Harmony patch 目标类型，必须在 `.local/api/queries/` 查到对应反编译文件后方可使用。patch 必须引用实际存在的方法名和参数列表。」
3. **Reviewer 检查**：reviewer 应检查每个 `HarmonyPatch` 的目标类型和方法名是否在 API 查询记录中存在。

---

## 🔴 严重：pre-commit hook 产生格外格式化改动，污染工作区

**现象**: `git commit` 后，`dotnet format` 通过 Husky pre-commit 对 `JigsawNativeSession.cs` 产生额外格式化改动，导致工作区 dirty，`git checkout main` 被拒绝。

**证据**: `pre-commit hook` grep 命中 62 次。需要 `git checkout -- src/Jigsaw/JigsawNativeSession.cs` 强行丢弃。

**根因**: `.husky/task-runner.json` 配置的 `dotnet format` 在 commit 后改动了文件却**不自动 stage 这些格式化改动**。开发者提交的代码未预先用 `dotnet format` 对齐，hook 修复后遗留了未暂存的修改。

**现有配置**（`.husky/task-runner.json`）：
```json
{ "command": "dotnet", "args": ["format", "AllMatters.sln"] }
```

**改进**: task-runner 应在 format 后自动 `git add` 受影响文件，或 **改为 pre-commit 时 format + stage，而不是只 format**。参考 Husky.NET 的 `git-add` 配置：

```json
{
  "tasks": [
    {
      "name": "pre-commit",
      "command": "dotnet",
      "args": ["format", "AllMatters.sln"],
      "include": ["**/*.cs"]
    },
    {
      "name": "stage-formatted",
      "command": "git",
      "args": ["add", "--update"],
      "include": ["**/*.cs"]
    }
  ]
}
```

替代方案：提交前显式运行 `dotnet format` 然后 `git add`，让 hook 成为幂等验证，而非首次修复。

---

## 🟡 重要：本地 main 与远程 main 不同步导致 git 操作失败

**现象**: `gh pr merge` 失败（PR 已远程合并），`git pull --ff-only` 失败（本地有 7 个游离提交），最终需要 `git reset --hard origin/main`。

**证据**: `diverged` 7 次、`ff-only` 9 次命中。

**根因**: 在 feature branch 上工作时，远程 `main` 被 PR squash-merge 推进，但本地 `main` 从未 fetch。PR 合并后直接切 main 时发现分叉。

**改进**: PR 合并后的标准操作序列：
```bash
rtk git fetch origin
rtk git checkout main
rtk git reset --hard origin/main
rtk git branch -d integration/batch-1
```
将此序列写入 `docs/development.md` 的「PR 合并后清理」小节，避免每次重新推导。

---

## 🟡 重要：worktree 目录无法删除（子代理进程未关闭）

**现象**: `git worktree remove --force` 和 `rm -rf` 均返回 "Permission denied" / "Device or resource busy"。

**证据**: `worktree remove` 26 次 / `Permission denied` 8 次。

**根因**: 两个子代理 Claude Code 会话仍持有 worktree 目录的文件句柄。在 Windows 上，打开的文件不能被其他进程删除。

**改进**: 清理 worktree 时，先确认子代理会话已归档（通过 CCD session `archive_session`），再执行删除。操作顺序：
1. `mcp__ccd_session_mgmt__archive_session` 归档子代理会话
2. 等待约 5 秒确保进程退出
3. `git worktree remove --force <path>` 或 `rm -rf`

将此注意事项加入 `docs/development.md` 的「worktree 清理」说明。

---

## 🟡 重要：`.pdb` 文件误打包到 dist

**现象**: 构建产物中将 `.pdb` 调试符号文件复制到 `dist/batch-1-integration/AllMatters-public-beta/`，但正式 mod 包不需要 `.pdb`。

**证据**: `pdb` 41 次命中，用户提问「.pdb 文件是什么？是否应该复制到 mods 文件夹？」

**根因**: 构建脚本未明确过滤 `.pdb`，构建代理将输出目录所有文件一并复制。

**改进**: `docs/development.md` 的「构建」节明确写出 mod 包必须包含且仅包含：
```
AllMatters.dll
AllMatters.pck
AllMatters.json
LICENSE
THIRD_PARTY_NOTICES.md
```
不包含 `.pdb`、`.xml`（XML doc）或其他调试产物。构建脚本加白名单复制而非全量复制。

---

## 🟡 重要：自动测试后未主动交付测试 mod 包与测试文档

**现象**: 自动化测试与构建完成后，Agent 没有主动产出可安装的测试 mod 包，也没有写该轮测试文档；两者都是用户手动提出后才执行。

**根因**: `docs/development.md` 的「实机验收交付」描述了交付内容，但没有把它写成批次收尾的**必做步骤**，也没有约定产物位置与文档命名，Agent 把它当成可选项。

**改进**: 在「实机验收交付」明确批次完成的交付物（缺一即未完成）：
- 测试 mod 包：`dist/batch-N-integration/AllMatters-<branch>/`，仅含白名单文件（见 `.pdb` 一条）。
- 测试文档：`docs/batch-N-test-guide.md`，列出安装步骤、依赖（RitsuLib 完整包）、逐项操作与预期结果；修复轮追加「修复后验证项」小节。
- 交付消息：给出包的绝对路径、实测版本、文档路径。

---

## 🟡 重要：交付顺序错误——应在审查修正之后再打包

**现象**: 测试包与测试文档的生成时机没有约定，存在审查前就给用户打包的情况；审查后代码又改动，用户手上的包与最终代码不一致，测试结论失效、需重新打包。

**改进**: 批次收尾固定顺序：

```
实施 → 自动化测试 + 两目标构建 → 代码审查 → Agent 自行修正审查发现 → 复测/复构建
    → 打包测试 mod → 写测试文档 → 交付用户实机验收
```

用户实机发现 bug 后的修复轮同样走「修复 → 测试 → 审查 → 修正 → 重新打包 → 追加测试文档」。打包与文档必须对应审查通过后的同一提交；交付消息写明该提交 SHA。此顺序写入 `docs/development.md`「实机验收交付」。

---

## 🟢 轻微：PR、issue 与交付文档语言不符合用户阅读习惯

**现象**: Agent 撰写的 PR 描述、issue 内容及部分交付给人工查阅的文档以英文为主，不便用户审阅。

**改进**: 新增写作约定（放入 `docs/agents/issue-tracker.md`，`AGENTS.md` 的 Issue tracker 指针已覆盖）：
- 面向人工查阅的内容——PR 标题与描述、issue 正文与评论、测试文档、复盘与交接文档——**以中文为主**，或中英双语。
- 专有名词、代码标识符、API 名、文件路径、命令保留英文原文（如 `MerchantInventory`、Harmony patch、worktree）。
- 提交信息与代码注释沿用仓库现有风格。

---

## 🟢 轻微：`git stash drop` 删错了 stash

**现象**: 执行 `git stash drop stash@{0}` 时，当前 stash 列表顶部是旧的 `fix/issue80-critical-cleanup` stash，而不是预期的失败商店修复 stash；后者已被推至 `stash@{1}`。

**证据**: `stash drop` 7 次命中。

**改进**: 删除 stash 前先 `git stash list` 确认目标内容，用 `git stash show stash@{N}` 验证后再 `git stash drop stash@{N}`。

---

## 🟢 轻微：Permission 分类器偶发 Stage 2 拦截

**现象**: 某些 bash 命令（如 `wc -l` 读取 `.jsonl`）被 Stage 2 分类器误判为高风险而拦截，需绕道。

**证据**: `Stage 2 classifier error` 3 次命中。

**现状**: 已知 Auto 模式偶发 transient 拦截。重试通常成功；不需要特殊改进，知悉即可。

---

## 📋 改进行动清单

| 优先级 | 改进项 | 目标文件 | 估计成本 |
|--------|--------|----------|----------|
| 🔴 | 并行实施一律独立 worktree，热点文件串行合入 | `docs/development.md` + `AGENTS.md` 指针 | 20 min |
| 🔴 | 修复测试宿主 BlueCandle 注册时序；开工前冒烟 | 新工单 + `tools/ReadTestLog.ps1` | 1–2 h |
| 🔴 | 子代理 prompt 必须指定 `source_branch` | 实践规范（见本文） | 0 |
| 🔴 | Harmony patch 目标必须先查 API 查询文件 | `CODING_STANDARDS.md` | 15 min |
| 🔴 | Husky pre-commit：format 后自动 stage | `.husky/task-runner.json` | 30 min |
| 🟡 | PR 合并后清理步骤文档化 | `docs/development.md` | 15 min |
| 🟡 | Worktree 删除前归档子代理会话 | `docs/development.md` | 10 min |
| 🟡 | dist 包白名单（不含 `.pdb`） | `docs/development.md` | 10 min |
| 🟡 | 批次收尾必交：测试 mod 包 + 测试文档 | `docs/development.md`「实机验收交付」 | 15 min |
| 🟡 | 固定顺序：审查 → 修正 → 再打包/写文档 | `docs/development.md`「实机验收交付」 | 10 min |
| 🟢 | 人工查阅内容以中文为主，专有名词保留英文 | `docs/agents/issue-tracker.md` | 10 min |
| 🟢 | stash drop 前先 stash show 验证 | 实践规范 | 0 |

---

## 已有防护（无需重复）

- Husky pre-commit：`dotnet format`（已存在，需修 stage 行为）
- `.local/api/queries/`：反编译 API 查询库（已存在，需强制使用）
- `tools/ReviewScope.ps1`：PR diff 范围确认（已存在）
- `docs/agents/review.md`：reviewer 规则（已存在，上轮复盘已加强指针）

---

*本文档由 `/retro` 技能生成，依据会话 89486eb6 及其子会话的 JSONL 日志分析。*
