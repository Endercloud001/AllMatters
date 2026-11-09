# Issue tracker: GitHub

仓库：`Endercloud001/AllMatters`。使用 `gh`，在其他工作目录操作时显式指定 `-R Endercloud001/AllMatters`。

- 读取：`gh issue view <number> --comments`。
- 列出：`gh issue list --json number,title,labels`。
- 发布任务：`gh issue create --title <title> --body-file <file>`。
- 应用标签：`gh issue edit <number> --add-label <label>`。
- 关闭：`gh issue close <number>`。

多行正文写入临时文件后传入 `--body-file`。技能要求发布到 issue tracker 时才创建 issue；初始化本身不自动创建任务或留言。

CLI 出现 TLS/连接超时后，元数据读取与 PR/issue 操作优先使用已连接的 GitHub 工具；先筛选结构化字段再输出。PR 状态只需 number/state/merged/mergeable/head_sha/base_sha，完整 diff 留在工具结果中按文件读取，不直接打印整个 fetch_pr 结果。

读取大型父规格时，将完整结果留在工具存储或本机 `.local/`，只摘录本票涉及的决定、内容章节和验收条件。已有本地规格可定位章节，需求冲突以 GitHub 和用户最终答复为准。只询问后续可开发编号时，获取相关工单的编号、状态和阻塞项，再核实其他前置状态；无需输出各票完整正文。通用文件/日志查询方式见 `docs/development.md` 的“游戏日志与交接”。

Git 传输可用 `tools/GitRemote.ps1 -Operation fetch`（或 push、ls-remote），按现有配置使用代理；没有 Git/环境代理时读取系统代理，只作用于当前命令。该工具不设置全局代理，不输出代理凭据。

**PRs as a request surface: no.**

## 语言与文风

面向人工查阅的内容（PR 标题与描述、issue 正文与评论、测试文档、复盘与交接文档）**以中文为主**，或中英双语。

- **保留英文**的情形：专有名词（Harmony patch、worktree）、API 名（`MerchantInventory`）、文件路径（`src/Jigsaw/`）、代码标识符、命令行、Git 术语
- **用中文**的内容：需求描述、流程说明、操作步骤、验收条件、反馈与总结

提交信息（commit message）与代码注释沿用仓库现有风格。

## 分批人工验收的实施票

按用户 2026-11-05 确认执行：本票实现、适用自动验证、审查和提交已交付后可以关闭；人工未验项目继续留在所属批次记录中，关闭不代表用户验收通过。收尾评论同时记录提交/包是否已推送、实际验证与未验范围。批次统一包就绪再请求安装测试；只有用户实际反馈才更新人工通过状态。该约定适用于实施票，不改变研究/HITL 决策票的解决条件，也不自动改写父规格。
