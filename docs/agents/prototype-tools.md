# 原型检查与状态报告

这些工具用于项目内 wayfinder 执行，不是正式生成器、网络协议或存档格式。参数和记录格式可随项目改进；规则正文仍以来源票为准。

## 轻量验证

```powershell
./tools/CheckPrototype.ps1 -Path prototypes/example
./tools/CheckPrototype.ps1 -Path ../AllMatters_prototype69/prototypes/jigsaw-auto -SyntaxOnly
```

检查 Python AST、Node 的 js/cjs/mjs 语法及 PowerShell 语法，跳过依赖、缓存和构建目录。运行时按脚本扩展名校验，避免把cjs交给Python。没有语义 smoke 时明确报告“只检查语法”，不把它当作领域验证通过。完整输出留在 `.local/prototype-checks/`，失败保留退出码和日志位置。

在各原型目录放置可选 `prototype.json`，列出轻量语义检查。入口使用参数数组，不使用shell命令字符串：

```json
{
  "smoke": [
    {"runtime": "python", "script": "negative.py", "args": []},
    {"runtime": "node", "script": "reproduce.cjs", "args": []}
  ]
}
```

runtime支持python、node、pwsh；脚本相对该配置目录，工作目录也是该目录。选择少量关键负例、一次完整事务及失败恢复。完整种子矩阵、浏览器长测和游戏构建在README中给出显式命令，不自动混入smoke。

HTML中的内联脚本与实际渲染通过浏览器smoke验证，文件扩展名语法检查不覆盖它们。已有归档分支可像上例一样显式调用主工作树工具，无需为接入流程改写其历史实验数据。

`tools/Check.ps1` 在工作树有 `prototypes/` 时调用此入口，且保留原语法、workflow、资源、双版测试与构建。提示型pre-push的非阻断行为不变。工具自身的回归验证为 `tools/Tests/PrototypeWorkflow.Tests.ps1`，由现有Check入口运行；机器缺少Python/Node时仅运行并明确标注PowerShell工具回归，不给纯游戏构建新增这两个依赖。实际原型包含Python/JS时仍须具备相应运行时。

## 单一证据记录生成状态

新原型可在自己的实验JSON中加入requirements；已有原型适配自己的状态来源即可，不创建两份用户批准状态。

```json
{
  "question": "验证本票提出的问题",
  "issue": "来源票URL",
  "requirements": [
    {"id":"A1","description":"完整事务与必要负例","owner":"agent","status":"todo","evidence":[],"next":"补齐失败恢复"},
    {"id":"U1","description":"用户体验","owner":"user","status":"todo","evidence":[],"next":"自动工作完成后邀请体验"}
  ]
}
```

status支持todo、blocked、passed、not_applicable；通过项需有证据，阻塞或不适用需说明原因。用户项的证据是实际答复或来源，而不是测试日志。只由实际参与的用户反馈改变用户状态。

```powershell
./tools/WriteWayfinderStatus.ps1 -Path prototypes/example/experiment.json -Output prototypes/example/STATUS.md
./tools/WriteWayfinderStatus.ps1 -Path prototypes/example/experiment.json -Output prototypes/example/STATUS.md -Check
```

工具不修改源记录。Agent仍有未完成项时不会生成“等待用户体验”或“可resolution”；即使用户已通过其他场景也一样。全部条目满足只表示可以人工核对本票收口，不自动操作GitHub。

每个用户场景分别记录，便于规则变更时只重开受影响的项。Agent项被用户决定阻塞时仍属未完成，在reason/next中明确当前问题并等待所需答案，继续其他独立工作。

## 环境和大文件读取

```powershell
./tools/InspectWorktree.ps1 | ConvertTo-Json -Depth 5
./tools/ReadTextSlice.ps1 -Path prototypes/example/demo.html -Pattern 'function generate' -Count 3 -MaxChars 240
```

环境检查只读取当前工作树配置、工具位置、主工作树配置位置和附近Godot候选；不修改用户路径。文本片段包含真实路径、行列和是否截断，完整内容仍留原文件。大JSON按字段读取，不通过提高输出预算打印内嵌媒体。
