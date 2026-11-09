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

## worktree 清理

参见 `docs/development.md` 的「并行实施与 worktree」→「worktree 清理」。

标准步骤：
1. 归档相关子代理会话
2. 等待 ~5 秒确保文件句柄释放
3. `git worktree remove --force <path>`

Windows 上若删除失败，检查目录是否被 IDE、编辑器或后台进程持有。

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
