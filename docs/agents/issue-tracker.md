# Issue tracker: GitHub

仓库：`Endercloud001/AllMatters`。使用 `gh`，在其他工作目录操作时显式指定 `-R Endercloud001/AllMatters`。

- 读取：`gh issue view <number> --comments`。
- 列出：`gh issue list --json number,title,labels`。
- 发布任务：`gh issue create --title <title> --body-file <file>`。
- 应用标签：`gh issue edit <number> --add-label <label>`。
- 关闭：`gh issue close <number>`。

多行正文写入临时文件后传入 `--body-file`。技能要求发布到 issue tracker 时才创建 issue；初始化本身不自动创建任务或留言。

**PRs as a request surface: no.**
