# AllMatters

开发前读取目标工单、父规格和阻塞项；涉及既有玩法时查阅对应版本范围（`docs/v0.1-scope.md`、`docs/v0.1.1-scope.md`）。构建、版本验证和本机路径见 `docs/development.md`；移植时查阅 `docs/references.md`。

涉及卡牌、能力、牌池、展示或事件流程时，先从 `docs/game-mechanics.md` 定位目标分支源码；API 查询、测试和日志命令见 `docs/development.md`。开始工单或准备 PR 时运行 `tools/ReviewScope.ps1`；审查时读取 `docs/agents/review.md`。

**并行实施**多张工单前，读取 `docs/development.md` 的「并行实施与 worktree」。创建 worktree 后，首次提交前立即运行 `git ls-files .local/ .scratch/ docs/local/ docs/references/` 和 `git status --short`，确认忽略目录未被跟踪；如有输出，使用 `git rm --cached -r -- <path>` 移除索引但保留本地文件。

**编写任何 Harmony patch** 前，必须先在 `.local/api/queries/` 查到目标类型的反编译文件，确认目标方法名和参数列表确实存在；不得凭猜测使用 API 成员。详见 `CODING_STANDARDS.md`。

**批次开始前**，运行冒烟测试确认测试宿主就绪，见 `docs/development.md` 的「开工前冒烟检查」。

**Git 操作最佳实践**（worktree 清理、PR 合并后同步、stash 操作）见 `docs/git-best-practices.md`。

## 不可逆操作安全规范

**以下操作必须在执行前生成备份、报告检查结果并等待用户确认：**

1. **删除 worktree**：备份差异和提交日志，报告未提交文件数、未合并提交数及备份位置。
2. **强制推送**（`git push --force`）：备份本地分支，报告将被覆盖的远程提交。
3. **删除分支**（`git branch -D`）：确认分支已合并或已备份。
4. **重置提交**（`git reset --hard`）：备份当前 HEAD 的 SHA。

严格顺序为：检查 → 备份 → 报告 → 用户确认 → 执行。绝对禁止在报告「检查中」时执行删除，或操作后才报告将要删除；报告必须与实际状态一致。

**例外**：仅当 worktree 为空，或用户在本轮对话明确说「直接删除」时，才可跳过确认步骤。其他情况不得自行判断。

游戏安装与实机验收由用户手动完成。Agent 负责自动化测试、构建和提供安装/验收步骤，不启动游戏或 Steam，不向游戏 `mods` 目录复制产物、依赖或改动现有安装。交付流程见 `docs/development.md` 的”实机验收交付”。

代码仅在 `src/`；文档在 `docs/`。当前工作区的 `docs` 是指向同级 `AllMatters_docs` 的 Windows junction。本机依赖、工具、日志与参考源码保存在 Git 忽略的 `.local/`，独立于该 junction；参考仓库位于 `.local/references/`，不参与编译。仅借鉴参考源码，不执行参考仓库的构建脚本。

默认不新增 hash、冻结 contract、baseline 或 gate。只有能明确说出具体失败场景，并解释 Git、版本号、主键、事务、唯一约束、类型和普通测试为什么不足时才允许加入。保留已有安全措施；认证、数据安全、不可逆操作和正式发布按项目要求处理。

## Agent skills

### Issue tracker

任务跟踪使用 Endercloud001/AllMatters 的 GitHub Issues。操作约定见 `docs/agents/issue-tracker.md`。

### Triage labels

使用 Matt 默认五种标签。映射见 `docs/agents/triage-labels.md`。

### Domain docs

单一领域上下文。探索领域时读取根目录 `GLOSSARY.md`；变更设计前读取相关 `docs/adr/`。消费规则见 `docs/agents/domain.md`。
