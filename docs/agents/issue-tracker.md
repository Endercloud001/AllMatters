# Issue tracker: GitHub

仓库：`Endercloud001/AllMatters`。使用 `gh`，在其他工作目录操作时显式指定 `-R Endercloud001/AllMatters`。

- 读取：`gh issue view <number> --comments`。
- 列出：`gh issue list --json number,title,labels`。
- 发布任务：`gh issue create --title <title> --body-file <file>`。
- 应用标签：`gh issue edit <number> --add-label <label>`。
- 关闭：`gh issue close <number>`。

多行正文写入临时文件后传入 `--body-file`。技能要求发布到 issue tracker 时才创建 issue；初始化本身不自动创建任务或留言。

CLI 出现 TLS/连接超时后，元数据读取与 PR/issue 操作优先使用已连接的 GitHub 工具；先筛选结构化字段再输出。PR 状态只需 number/state/merged/mergeable/head_sha/base_sha，完整 diff 留在工具结果中按文件读取，不直接打印整个 fetch_pr 结果。

Git 传输可用 `tools/GitRemote.ps1 -Operation fetch`（或 push、ls-remote），按现有配置使用代理；没有 Git/环境代理时读取系统代理，只作用于当前命令。该工具不设置全局代理，不输出代理凭据。

**PRs as a request surface: no.**
