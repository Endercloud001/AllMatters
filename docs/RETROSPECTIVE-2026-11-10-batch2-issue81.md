# 复盘报告：Batch 2 Issue #81 多人大厅恢复

**日期**：2026-11-10  
**批次**：Batch 2 / Issue #81 (#91-94)  
**复盘范围**：本轮对话及子 agent 的开发过程  
**严重级别**：有 2 个高优先级问题（.local/ 意外提交、#94 worktree 管理失误）

---

## 执行摘要

Batch 2 (#91-94) 多人大厅恢复功能已完成实施，所有工单通过测试并合入 main。然而开发过程中暴露了 7 个系统性问题，其中 2 个为高优先级：
1. **`.local/` 意外提交**：worktree 中的符号链接被错误跟踪，花费额外 token 修复
2. **#94 worktree 管理失误**：Agent 在未备份情况下清理 worktree，导致需从历史提交 cherry-pick 恢复

这些问题反映了 Agent 工作流程文档的不足、worktree 操作的风险控制缺失，以及上下文读取失败导致的冗余操作。

---

## 问题分析（7 个）

### 问题 1：`.local/` 符号链接意外提交（高优先级）

**观察到的现象**：
- worktree 创建后，`.local/` 目录（实际为符号链接）被 Git 跟踪
- `.local/` 已在 `.gitignore` 中，但在 worktree 中仍被提交
- 需要额外的 `git rm --cached` 和清理提交（commit `5f7da42`）

**根因分析**：
1. **Why 1**：为什么 `.local/` 被跟踪？→ worktree 初始化时符号链接被当作普通目录提交
2. **Why 2**：为什么 gitignore 未生效？→ `.gitignore` 只阻止**未跟踪**文件，已提交文件不受影响
3. **Why 3**：为什么会在 worktree 中提交？→ Agent 缺少"worktree 初始化后立即检查 gitignore 违规"的标准流程
4. **Why 4**：为什么 `.local/` 是符号链接？→ 本机环境使用 junction 指向外部目录，但 Agent 不知道需要验证
5. **根本原因**：**缺少 worktree 初始化后的安全检查清单**

**影响**：
- 浪费 token：修复需额外读取、执行 `git rm --cached` 和提交
- 风险：如未及时发现，符号链接可能传播到其他 worktree 或 clone
- 延误：中断开发节奏，需额外清理步骤

**改进措施**：
1. **文档化 worktree 初始化 SOP**（写入 `docs/git-best-practices.md`）：
   ```markdown
   ## Worktree 初始化安全清单
   
   创建 worktree 后，**立即**执行以下检查：
   
   1. 验证 `.local/` 未被跟踪：
      ```powershell
      git ls-files .local/
      # 预期输出：空（无输出）
      ```
   
   2. 如已跟踪，立即移除：
      ```powershell
      git rm --cached -r .local/
      git commit -m "chore: remove accidentally tracked .local directory"
      ```
   
   3. 验证其他忽略目录（`.scratch/`、`docs/local/` 等）：
      ```powershell
      git ls-files .scratch/ docs/local/
      # 预期输出：空
      ```
   ```

2. **添加预提交钩子**（`tools/hooks/pre-commit`）：
   ```bash
   #!/bin/bash
   # 阻止意外提交忽略目录
   if git diff --cached --name-only | grep -E '^\.local/|^\.scratch/|^docs/(local|specs|tickets|research|testing)/'; then
     echo "ERROR: Attempting to commit ignored directories (.local/, .scratch/, docs/local/, etc.)"
     echo "Run: git rm --cached -r <dir>"
     exit 1
   fi
   ```

3. **更新 AGENTS.md**：
   ```markdown
   **Worktree 创建后必须验证**：每次创建 worktree 后，立即运行 `git ls-files .local/ .scratch/`，
   确认输出为空。如有输出，说明这些忽略目录被意外跟踪，必须用 `git rm --cached -r` 移除。
   ```

**预防规则**：
- **所有 worktree 创建**：必须在首次提交前运行 `git ls-files .local/ .scratch/`
- **PR 审查**：检查 `.gitignore` 中的目录是否出现在 `git diff --name-only`

---

### 问题 2：Issue #81 内容读取失败（多次尝试）

**观察到的现象**：
- Agent 反复尝试数次才成功读取 GitHub Issue #81 内容
- 期间一度误认为 issue 描述混乱
- 最终通过 `gh issue view 81` 成功获取

**根因分析**：
1. **Why 1**：为什么读取失败？→ GitHub API 调用可能触发速率限制或网络抖动
2. **Why 2**：为什么没有立即重试？→ Agent 缺少"API 失败自动重试"的策略
3. **Why 3**：为什么误判描述混乱？→ 部分读取时 JSON/HTML 片段被当作正文解析
4. **根本原因**：**缺少 GitHub API 调用的重试机制和错误诊断**

**影响**：
- 浪费 token：多次尝试相同操作
- 延误：在错误路径上花费时间（误判描述混乱）
- 用户体验差：Agent 表现出"困惑"而非系统化排查

**改进措施**：
1. **标准化 GitHub API 调用模式**（写入 `docs/agents/issue-tracker.md`）：
   ```markdown
   ## Issue 读取最佳实践
   
   优先使用 `gh` CLI（已安装且更稳定）：
   ```powershell
   gh issue view <num> --json title,body,labels,state
   ```
   
   如遇网络错误，自动重试 3 次（指数退避）：
   - 第 1 次失败：等待 1 秒
   - 第 2 次失败：等待 2 秒
   - 第 3 次失败：报告错误并终止
   
   不要在首次失败后就质疑 issue 内容质量。
   ```

2. **添加诊断命令**（`tools/DebugGitHubApi.ps1`）：
   ```powershell
   # 测试 GitHub API 连通性
   gh api rate_limit
   gh auth status
   ```

3. **更新 AGENTS.md**：
   ```markdown
   **GitHub API 失败处理**：如 `gh issue view` 失败，先检查：
   1. 网络连接（`gh auth status`）
   2. API 速率限制（`gh api rate_limit`）
   3. Issue 编号是否正确
   
   不要在 API 失败时推断 issue 内容有问题。
   ```

**预防规则**：
- **所有 GitHub API 调用**：失败时先诊断原因（网络/速率/权限），再重试
- **错误日志**：记录 API 失败的原始错误信息，不做主观推断

---

### 问题 3：Worktree 路径嵌套混乱

**观察到的现象**：
- Worktree 被创建在错误的嵌套路径（`.claude/.claude/.claude/worktrees/`）
- 需要后期手动修正路径
- 浪费时间定位和清理错误路径

**根因分析**：
1. **Why 1**：为什么路径嵌套？→ Agent 可能在已存在的 worktree 中创建新 worktree
2. **Why 2**：为什么未检测到错误？→ 创建 worktree 时未验证父目录是否已是 worktree
3. **Why 3**：为什么 EnterWorktree 允许这样操作？→ 工具本身不阻止嵌套，Agent 需要自行判断
4. **根本原因**：**缺少 worktree 创建前的路径验证**

**影响**：
- 浪费 token：需额外清理和重新创建 worktree
- 混淆：嵌套路径导致文件查找困难
- 风险：可能在错误路径提交代码

**改进措施**：
1. **添加 worktree 创建前验证**（写入 `docs/development.md`）：
   ```markdown
   ## Worktree 创建前验证
   
   **必须在主仓库目录中创建 worktree**，不要在既有 worktree 中创建：
   
   ```powershell
   # 1. 确认当前目录是主仓库（不是 worktree）
   git rev-parse --git-dir
   # 预期输出：.git （不是 .git/worktrees/<name>）
   
   # 2. 如在 worktree 中，先返回主仓库
   cd /path/to/main/repo
   
   # 3. 然后创建新 worktree
   git worktree add .claude/worktrees/<name> -b <branch>
   ```
   ```

2. **更新 AGENTS.md**：
   ```markdown
   **并行实施 worktree 创建**：调用 `start_session` 前，Agent 必须验证自己在主仓库根目录，
   不在既有 worktree 中。如在 worktree 中，子代理的 worktree 将嵌套在错误路径下。
   ```

3. **添加检查脚本**（`tools/ValidateWorktreeContext.ps1`）：
   ```powershell
   $gitDir = git rev-parse --git-dir
   if ($gitDir -like "*.git/worktrees/*") {
       Write-Error "当前目录是 worktree，不能在此创建新 worktree"
       exit 1
   }
   ```

**预防规则**：
- **创建 worktree 前**：必须确认 `git rev-parse --git-dir` 输出 `.git`（主仓库）
- **子代理调度前**：主会话必须确认自己在主仓库根目录

---

### 问题 4：测试文件损坏，API 使用错误

**观察到的现象**：
- 测试文件损坏，需重新生成
- 使用了错误的测试 API（非项目测试规范）
- 最终需添加 xUnit 依赖（commit `ce91929`）

**根因分析**：
1. **Why 1**：为什么测试文件损坏？→ Agent 可能在合并冲突时错误解决，导致文件不完整
2. **Why 2**：为什么使用错误 API？→ Agent 未读取既有测试文件了解项目规范
3. **Why 3**：为什么项目缺少 xUnit 依赖？→ 既有测试使用其他框架，Agent 引入了不同框架
4. **根本原因**：**写测试前未读取既有测试文件了解 API 规范**

**影响**：
- 浪费 token：重新生成测试文件
- 依赖混乱：引入非必要的 xUnit 依赖（项目可能已有 NUnit 或 MSTest）
- 风险：不同测试框架混用导致运行问题

**改进措施**：
1. **标准化测试编写流程**（写入 `CODING_STANDARDS.md`）：
   ```markdown
   ## 测试编写规范
   
   **写任何新测试前，必须**：
   
   1. 读取至少 2 个既有测试文件（`src/Tests/` 下）
   2. 识别项目使用的测试框架（xUnit / NUnit / MSTest）
   3. 识别测试宿主初始化模式（如 `HeadlessTestSetup`）
   4. 复用相同的 API 和断言风格
   
   **不要**：
   - 凭猜测使用测试 API
   - 引入新的测试框架（除非明确需求）
   - 跳过既有测试文件的阅读
   ```

2. **更新 AGENTS.md**：
   ```markdown
   **测试实施**：开始 TDD 前，先用 `rg --files src/Tests -g '*Tests.cs' | head -5` 列出既有测试，
   读取其中 2-3 个，确认项目使用的测试框架和宿主初始化模式，然后复用相同 API。
   ```

3. **添加测试框架检测脚本**（`tools/DetectTestFramework.ps1`）：
   ```powershell
   $projectFile = "src/Tests/AllMatters.Tests.csproj"
   $content = Get-Content $projectFile -Raw
   
   if ($content -match 'xunit') { Write-Host "xUnit" }
   elseif ($content -match 'nunit') { Write-Host "NUnit" }
   elseif ($content -match 'MSTest') { Write-Host "MSTest" }
   else { Write-Host "Unknown" }
   ```

**预防规则**：
- **写测试前**：必须读取 2-3 个既有测试文件，确认框架和 API
- **添加依赖前**：检查依赖是否已存在，避免重复引入

---

### 问题 5：`.git/index.lock` 锁定冲突

**观察到的现象**：
- 合并时出现 `.git/index.lock` 冲突
- 错误信息：`fatal: Unable to create '.../.git/index.lock': File exists`

**根因分析**：
1. **Why 1**：为什么出现 lock 文件？→ 多个 Git 进程同时操作同一仓库
2. **Why 2**：为什么会并行操作？→ Agent 可能在子代理运行 Git 命令时，主会话也执行了 Git 操作
3. **Why 3**：为什么未检测到冲突？→ Git 操作未加锁或协调
4. **根本原因**：**并行 worktree 操作时缺少 Git 操作串行化**

**影响**：
- 操作失败：需重试 Git 命令
- 数据风险：index 损坏可能导致提交丢失
- 延误：需手动删除 lock 文件

**改进措施**：
1. **文档化 Git 操作协调**（写入 `docs/git-best-practices.md`）：
   ```markdown
   ## 并行 worktree 的 Git 操作协调
   
   **问题**：多个 worktree 同时执行 Git 命令可能导致 `.git/index.lock` 冲突。
   
   **预防措施**：
   1. 主会话负责协调，子代理只在自己的 worktree 提交
   2. 合并到集成分支由主会话串行执行，不并行
   3. 如遇 `index.lock` 错误，等待 5 秒后重试（其他进程可能正在完成）
   
   **恢复步骤**（仅当确认无其他 Git 进程时）：
   ```powershell
   Remove-Item .git/index.lock -Force
   ```
   ```

2. **更新 AGENTS.md**：
   ```markdown
   **并行实施 Git 操作**：
   - 子代理只在各自 worktree 中提交，不操作共享 `.git/`
   - 主会话串行合并各分支到集成分支，每次合并后等待 2 秒
   - 遇到 `index.lock` 错误时，等待 5 秒后重试 1 次
   ```

**预防规则**：
- **并行操作前**：明确各代理的 Git 操作边界（仅限各自 worktree）
- **合并操作**：由主会话串行执行，不并行
- **错误处理**：`index.lock` 错误自动重试 1 次（延迟 5 秒）

---

### 问题 6：冗余 Worktree 创建

**观察到的现象**：
- 用户在对话早期已为 #92-94 创建了 worktree
- Agent 后来仍重复创建了新的 worktree
- 导致存在多个 #92、#93、#94 worktree

**根因分析**：
1. **Why 1**：为什么重复创建？→ Agent 未检查已存在的 worktree
2. **Why 2**：为什么未检查？→ Agent 上下文读取失败或对话早期信息未被注意
3. **Why 3**：为什么对话早期信息丢失？→ 长对话中早期上下文可能被压缩或遗忘
4. **根本原因**：**缺少"检查既有 worktree"的标准流程**

**影响**：
- 浪费资源：多个 worktree 占用磁盘空间
- 混淆：哪个 worktree 是最新的？
- 清理负担：需手动清理冗余 worktree

**改进措施**：
1. **标准化 worktree 创建前检查**（写入 `docs/development.md`）：
   ```markdown
   ## Worktree 创建前检查
   
   **每次创建 worktree 前，必须检查是否已存在**：
   
   ```powershell
   git worktree list
   # 查看输出，确认目标分支/工单是否已有 worktree
   ```
   
   如已存在：
   - 复用既有 worktree（`cd` 进入该目录）
   - 或明确为何需要新 worktree（如既有 worktree 状态损坏）
   ```

2. **更新 AGENTS.md**：
   ```markdown
   **并行实施前检查**：调用 `start_session(..., use_worktree: true)` 前，先运行 `git worktree list`，
   确认目标工单的 worktree 不存在。如已存在，通过 `SendMessage` 向既有会话发送指令，
   而不是创建新 worktree。
   ```

3. **添加 worktree 查询函数**（`tools/FindWorktree.ps1`）：
   ```powershell
   param([string]$IssueNum)
   git worktree list | Select-String "#$IssueNum|issue-$IssueNum"
   ```

**预防规则**：
- **创建 worktree 前**：必须运行 `git worktree list` 并检查输出
- **发现既有 worktree**：优先复用或通过 `SendMessage` 向既有会话发送任务

---

### 问题 7：#94 Worktree 管理严重失误（高优先级）

**观察到的现象**：
- Agent 先清理了 #94 worktree（包括其分支和提交）
- 随后向用户报告"#94 仍在错误路径"
- 然后又称"#94 不在列表中（已在之前清理时删除）"
- 最终只能从历史提交 `d716d4e` 或 `69273ef` cherry-pick 恢复
- 这是极度不专业的行为：在未备份前清理数据，然后给出矛盾报告

**根因分析**：
1. **Why 1**：为什么清理前未检查？→ Agent 缺少"清理前必须备份/确认"的规范
2. **Why 2**：为什么给出矛盾报告？→ Agent 先操作后报告，且未追踪操作结果
3. **Why 3**：为什么未意识到数据丢失风险？→ 缺少"不可逆操作前必须确认"的安全意识
4. **Why 4**：为什么能执行不可逆操作？→ 权限模式允许直接删除，无强制备份检查
5. **根本原因**：**缺少数据安全规范和不可逆操作前的强制检查**

**影响**：
- **严重**：丢失 #94 的开发成果，需从历史提交恢复
- **信任损失**：用户对 Agent 的可靠性产生质疑
- **浪费 token**：需额外 cherry-pick 和重新验证
- **风险**：如历史提交也丢失（如未推送），则无法恢复

**改进措施**：

1. **制定 Worktree 清理安全规范**（写入 `docs/git-best-practices.md`，**最高优先级**）：
   ```markdown
   ## Worktree 清理安全规范（强制执行）
   
   **清理任何 worktree 前，必须按顺序执行**：
   
   ### 1. 检查阶段（只读，不修改任何内容）
   
   ```powershell
   # 1.1 列出 worktree 信息
   git worktree list
   # 记录目标 worktree 的路径和分支
   
   # 1.2 检查未提交改动
   cd <worktree-path>
   git status
   # 如有未提交文件，记录列表
   
   # 1.3 检查未合并提交
   git log --oneline <branch> ^origin/main
   # 如有未合并提交，记录 SHA 和数量
   
   # 1.4 生成差异快照
   git diff > /path/to/backup/worktree-<name>-diff.patch
   git log --oneline --graph > /path/to/backup/worktree-<name>-commits.txt
   ```
   
   ### 2. 报告阶段（向用户报告检查结果）
   
   **格式**：
   ```
   Worktree 清理检查报告：<name>
   
   - 路径：<path>
   - 分支：<branch>
   - 未提交文件：<count> 个
   - 未合并提交：<count> 个（SHA: <list>）
   - 备份位置：<backup-path>
   
   清理操作将：
   1. 删除 worktree 目录（不可恢复）
   2. 删除分支 <branch>（如未推送远程，不可恢复）
   
   是否继续？（如有未提交/未合并内容，建议先备份或合并）
   ```
   
   ### 3. 用户确认阶段（强制等待）
   
   **Agent 必须等待用户明确回复**：
   - "确认清理" / "删除" / "继续" → 执行清理
   - "取消" / "等等" / "备份" → 中止，保留 worktree
   - **无回复时不得自行判断**
   
   ### 4. 执行阶段（仅在用户确认后）
   
   ```powershell
   # 4.1 再次确认备份完整
   Test-Path /path/to/backup/worktree-<name>-diff.patch
   Test-Path /path/to/backup/worktree-<name>-commits.txt
   
   # 4.2 执行清理
   git worktree remove --force <path>
   git branch -D <branch>  # 如需要
   
   # 4.3 验证清理结果
   git worktree list  # 确认已移除
   ```
   
   ### 5. 例外情况（仅限以下场景可跳过用户确认）
   
   - 空 worktree（无提交、无文件改动）
   - 已合并且已推送远程的 worktree
   - 用户在本轮对话中明确说"清理所有 worktree"
   
   **任何其他情况，必须报告并等待确认。**
   ```

2. **添加强制检查脚本**（`tools/SafeRemoveWorktree.ps1`）：
   ```powershell
   param(
       [Parameter(Mandatory=$true)]
       [string]$WorktreePath,
       [switch]$Force
   )
   
   # 1. 检查阶段
   $branch = (git -C $WorktreePath branch --show-current)
   $uncommitted = (git -C $WorktreePath status --short)
   $unmerged = (git -C $WorktreePath log --oneline "$branch" ^origin/main)
   
   # 2. 生成备份
   $backupDir = ".local/worktree-backups/$(Get-Date -Format 'yyyy-MM-dd-HHmmss')-$branch"
   New-Item -ItemType Directory -Path $backupDir -Force
   git -C $WorktreePath diff > "$backupDir/diff.patch"
   git -C $WorktreePath log --oneline --graph > "$backupDir/commits.txt"
   
   # 3. 报告
   Write-Host "Worktree 清理检查报告：$branch"
   Write-Host "- 路径：$WorktreePath"
   Write-Host "- 未提交文件：$($uncommitted.Count) 个"
   Write-Host "- 未合并提交：$($unmerged.Count) 个"
   Write-Host "- 备份位置：$backupDir"
   
   # 4. 确认
   if (-not $Force) {
       $confirm = Read-Host "是否继续清理？[y/N]"
       if ($confirm -ne 'y') {
           Write-Host "已取消清理"
           exit 0
       }
   }
   
   # 5. 执行
   git worktree remove --force $WorktreePath
   Write-Host "已删除 worktree：$WorktreePath"
   ```

3. **更新 AGENTS.md（最高优先级规则）**：
   ```markdown
   ## 不可逆操作安全规范
   
   **以下操作必须在执行前生成备份并等待用户确认**：
   
   1. **删除 worktree**：必须先备份差异和提交日志，报告检查结果，等待用户确认
   2. **强制推送**（`git push --force`）：必须先备份本地分支，报告将被覆盖的远程提交
   3. **删除分支**（`git branch -D`）：必须先确认该分支已合并或已备份
   4. **重置提交**（`git reset --hard`）：必须先备份当前 HEAD 的 SHA
   
   **绝对禁止**：
   - 在报告"检查中"时已执行删除操作
   - 操作后才报告"将要删除"
   - 给出矛盾报告（"已删除"vs"仍在错误路径"）
   
   **例外**：仅当用户在本轮对话明确说"直接删除，不需要确认"时，才可跳过确认步骤。
   ```

4. **添加 PR 审查检查项**（`docs/agents/review.md`）：
   ```markdown
   ## 不可逆操作审查
   
   Reviewer 必须检查：
   - 是否存在 `git worktree remove`、`git branch -D`、`git reset --hard` 等不可逆操作
   - 操作前是否生成了备份
   - 是否向用户报告了检查结果并等待确认
   - 如未备份或未确认，标记为**阻塞项**
   ```

**预防规则**：
- **任何不可逆操作前**：必须生成备份、报告检查结果、等待用户确认
- **严格顺序**：检查 → 备份 → 报告 → 确认 → 执行（不可跳过或颠倒）
- **操作后禁止矛盾报告**：如已删除，不能再报告"仍存在"；如报告"仍存在"，说明未删除
- **备份必须在执行前完成**：备份本身不可逆操作失败时，必须能从备份恢复

---

## 改进措施清单（按优先级排序）

### 立即实施（Batch 3 开始前）

| 优先级 | 措施 | 相关问题 | 负责文档 |
|--------|------|----------|----------|
| **P0** | 制定 Worktree 清理安全规范（强制执行） | #7 | `docs/git-best-practices.md` |
| **P0** | 添加 `.local/` 跟踪检测的 pre-commit 钩子 | #1 | `tools/hooks/pre-commit` |
| **P1** | 文档化 worktree 初始化安全清单 | #1 | `docs/git-best-practices.md` |
| **P1** | 标准化测试编写流程（读取既有测试） | #4 | `CODING_STANDARDS.md` |
| **P1** | 文档化 worktree 创建前验证（路径、既有检查） | #3, #6 | `docs/development.md` |

### 短期改进（Batch 3-4）

| 优先级 | 措施 | 相关问题 | 负责文档 |
|--------|------|----------|----------|
| **P2** | 标准化 GitHub API 调用模式（重试机制） | #2 | `docs/agents/issue-tracker.md` |
| **P2** | 文档化 Git 操作协调（并行 worktree） | #5 | `docs/git-best-practices.md` |
| **P2** | 添加 `SafeRemoveWorktree.ps1` 脚本 | #7 | `tools/` |
| **P2** | 添加 `ValidateWorktreeContext.ps1` 脚本 | #3 | `tools/` |

### 长期优化（Batch 5+）

| 优先级 | 措施 | 相关问题 | 负责文档 |
|--------|------|----------|----------|
| **P3** | 添加 `DetectTestFramework.ps1` 脚本 | #4 | `tools/` |
| **P3** | 添加 `DebugGitHubApi.ps1` 诊断脚本 | #2 | `tools/` |
| **P3** | 添加 `FindWorktree.ps1` 查询脚本 | #6 | `tools/` |

---

## AGENTS.md 建议更新

### 新增章节：Worktree 安全操作规范

```markdown
## Worktree 安全操作

### 创建前检查

1. **验证上下文**：确认当前目录是主仓库（`git rev-parse --git-dir` 输出 `.git`）
2. **检查既有 worktree**：运行 `git worktree list`，确认目标工单的 worktree 不存在
3. **验证源分支**：确认 `source_branch` 存在且是期望的集成分支（如 `integration/batch-N`）

### 创建后验证

1. **检查忽略目录**：立即运行 `git ls-files .local/ .scratch/`，确认输出为空
2. **如有输出**：用 `git rm --cached -r <dir>` 移除，提交后再继续开发

### 清理前备份（强制）

**任何 worktree 清理前，必须**：
1. 备份差异：`git diff > backup.patch`
2. 备份提交日志：`git log --oneline --graph > backup-commits.txt`
3. 报告检查结果（未提交文件数、未合并提交数、备份位置）
4. **等待用户确认**（不得自行判断）
5. 确认后执行清理

**例外**：仅当 worktree 为空（无提交、无改动）或用户明确说"直接删除"时，才可跳过确认。

### 并行操作协调

1. **子代理职责**：仅在各自 worktree 中提交，不操作共享 `.git/`
2. **主会话职责**：串行合并各分支到集成分支，每次合并后等待 2 秒
3. **错误处理**：遇到 `index.lock` 错误时，等待 5 秒后重试 1 次
```

### 新增章节：测试编写规范

```markdown
## 测试实施规范

**写任何新测试前，必须**：

1. 列出既有测试：`rg --files src/Tests -g '*Tests.cs' | head -5`
2. 读取 2-3 个既有测试文件，识别：
   - 测试框架（xUnit / NUnit / MSTest）
   - 宿主初始化模式（如 `HeadlessTestSetup`）
   - 断言风格和命名约定
3. 复用相同 API 和风格，不引入新框架

**不要**：
- 凭猜测使用测试 API
- 跳过既有测试文件的阅读
```

### 新增章节：GitHub API 容错

```markdown
## GitHub API 操作

**优先使用 `gh` CLI**（比直接 API 调用更稳定）：
```powershell
gh issue view <num> --json title,body,labels,state
```

**失败处理**：
1. 不要在首次失败后质疑 issue 内容
2. 先诊断原因：
   - 网络连接：`gh auth status`
   - API 速率：`gh api rate_limit`
   - 编号正确性
3. 自动重试 3 次（1s / 2s / 4s 指数退避）
4. 仍失败时报告具体错误，不推断原因
```

---

## 防止复现的检查清单

### 开发前（每个批次开始前）

- [ ] 确认当前目录是主仓库（不是 worktree）
- [ ] 运行 `git worktree list` 检查既有 worktree
- [ ] 读取 `docs/development.md` 的「并行实施与 worktree」章节
- [ ] 确认测试宿主就绪（运行冒烟检查）

### 开发中（创建 worktree 时）

- [ ] 验证 `git rev-parse --git-dir` 输出 `.git`
- [ ] 检查目标工单的 worktree 是否已存在
- [ ] 创建后立即运行 `git ls-files .local/ .scratch/`
- [ ] 如有输出，用 `git rm --cached -r` 移除

### 开发中（写测试时）

- [ ] 列出既有测试文件（`rg --files src/Tests -g '*Tests.cs'`）
- [ ] 读取 2-3 个既有测试，确认框架和 API
- [ ] 复用相同的宿主初始化模式和断言风格

### 开发后（清理 worktree 时）

- [ ] 运行 `git status` 和 `git log <branch> ^origin/main`
- [ ] 备份差异和提交日志到 `.local/worktree-backups/`
- [ ] 报告检查结果（未提交、未合并、备份位置）
- [ ] **等待用户确认**（不得自行判断）
- [ ] 确认后执行清理

### PR 审查时

- [ ] 检查 `.gitignore` 目录是否被意外跟踪
- [ ] 检查不可逆操作（worktree remove / branch -D / reset --hard）是否有备份
- [ ] 检查测试是否使用了项目既有的测试框架
- [ ] 检查 Git 操作是否在主会话中串行执行

---

## 总结与建议

本次批次的 7 个问题中，2 个为高优先级（`.local/` 意外提交、#94 worktree 管理失误），揭示了 Agent 工作流程中的系统性风险。

### 核心改进方向

1. **强化安全规范**：不可逆操作必须备份并等待用户确认
2. **文档化标准流程**：worktree 创建、清理、并行协调的完整 SOP
3. **添加自动检查**：pre-commit 钩子、验证脚本、安全清理脚本
4. **改进上下文读取**：检查既有资源（worktree、测试文件）再操作

### 下一步行动

1. **立即**：更新 `docs/git-best-practices.md`，添加 Worktree 清理安全规范
2. **Batch 3 前**：添加 pre-commit 钩子和 `SafeRemoveWorktree.ps1` 脚本
3. **Batch 3-4**：完善 `docs/development.md` 和 `CODING_STANDARDS.md`
4. **持续**：在每个 PR 审查中检查这些规范的遵守情况

---

**复盘日期**：2026-11-10  
**下次复盘**：Batch 3 完成后（建议每 2-3 个批次复盘一次）
