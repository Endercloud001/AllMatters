# Git 操作最佳实践

## stash 操作

避免直接 `git stash drop`：该命令默认删除最新 stash (`stash@{0}`)，易于误删。

**推荐流程**：

1. 列出所有 stash，查看索引和描述：
   ```powershell
   git stash list
   ```

2. 指定索引删除（替代 drop 默认行为）：
   ```powershell
   git stash drop stash@{N}  # N 为具体索引
   ```

3. 或先应用后自动删除（需要恢复内容时）：
   ```powershell
   git stash pop stash@{N}
   ```

4. 清空全部 stash（谨慎使用）：
   ```powershell
   git stash clear  # 需确认意图，无法恢复
   ```

## Worktree 初始化安全清单

创建 worktree 后，首次提交前立即执行以下检查：

```powershell
git ls-files .local/ .scratch/ docs/local/ docs/references/
# 预期输出为空；如有输出，说明忽略目录已被跟踪
git status --short
# 预期不包含上述目录
```

如发现已跟踪路径，先从索引移除但保留本地文件：

```powershell
git rm --cached -r -- <path>
```

随后重新运行上述两项检查，确认干净后再继续开发。提交时，`tools/hooks/pre-commit` 会阻止这些目录进入暂存区。

## Worktree 清理安全规范（强制执行）

**清理任何 worktree 前，必须严格按「检查 → 备份 → 报告 → 用户确认 → 执行」顺序操作，不得跳过或颠倒。**

### 1. 检查与备份阶段

```powershell
git worktree list
cd <worktree-path>
git status
git log --oneline <branch> ^origin/main
git diff > <backup-path>/diff.patch
git log --oneline --graph > <backup-path>/commits.txt
```

记录目标路径、分支、未提交文件数、未合并提交数量及 SHA。必须生成差异快照和提交日志备份。

### 2. 报告阶段

向用户报告以下内容：

- 路径和分支；
- 未提交文件数量；
- 未合并提交数量及 SHA；
- 备份位置；
- 将删除 worktree 目录，以及必要时删除分支的不可逆影响。

### 3. 用户确认阶段

必须等待用户明确回复「确认清理」「删除」或「继续」后才能执行。用户回复「取消」「等等」或「备份」时立即中止；无回复时不得自行判断。

### 4. 执行与验证阶段

确认差异快照和提交日志备份存在后，执行 `git worktree remove --force <path>`，必要时再删除分支，最后用 `git worktree list` 验证结果。可使用 `tools/SafeRemoveWorktree.ps1` 自动完成检查、备份、报告和确认流程。

### 5. 例外情况

仅当 worktree 为空、已合并且已推送远程，或用户在本轮对话明确说「直接删除」/「清理所有 worktree」时，才可跳过确认。其他情况必须报告并等待确认。

## worktree 清理

参见 `docs/development.md` 的「并行实施与 worktree」→「worktree 清理」。

## PR 合并后同步

参见 `docs/development.md` 的「PR 合并后清理」。

标准序列（PR 已在 GitHub 上 squash-merge）：

```powershell
rtk git fetch origin
rtk git checkout main
rtk git reset --hard origin/main
rtk git branch -d integration/batch-N
rtk git branch -d <feature-branch>
```

**为什么需要 `reset --hard`**：远程 PR 被 squash 合并后，本地 main 可能有多个游离提交未同步。`reset --hard` 可靠地将本地 main 对齐远程状态。

不要跳过 fetch — 本地缓存可能过期。
