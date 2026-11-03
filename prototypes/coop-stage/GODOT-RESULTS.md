# Godot宿主补验

2026-11-03，用户提供Godot目录并授权继续尝试构建。仅在独立prototype工作树配置忽略文件Paths.local.props；未改主工作区、Steam、游戏mods或真实存档。

实际可执行文件：`E:/Godot_v4.5.1-stable_mono_win64/Godot_v4.5.1-stable_mono_win64_console.exe`。使用原仓库headless编辑器导入/打包、测试项目和构建脚本，未启动正式游戏。RitsuLib0.6.7及.NET9运行时引用同级主仓库已有开发依赖；游戏API public0.107.1/59260271…与beta0.111.0/41cef1ea…，不是宣称当前Steam最新。

## 实际执行

1. `& tools/Check.ps1`：退出0，语法/流程/空白/资源导出/双版测试/双版构建全部PASS。既有216项测试各版通过。完整日志本机`.local/check-with-godot.log`；共享摘要`godot-evidence/check-summary.txt`保留原始行号。此轮运行在新增探针接入前。
2. `& tools/Test.ps1 -Branch All -Filter PrototypeCoop -SkipResourceExport`：退出0，双版各2项新探针通过。完整日志本机`.local/prototype-godot-tests.log`；共享摘要`godot-evidence/probe-summary.txt`保留原始行号。

新增一次性测试`src/Tests/PrototypeCoopTests.cs`，只在此prototype分支，接入原有HelloWorldTests初始化之后。没有改生命周期替代或正式Mod逻辑，没有绕过RitsuLib初始化。复用`HeadlessTestSetup`创建初始化跑局，实际保存槽位Set→内部BuildPayload→临时文件→ImportPayloadIntoRun→Get相等。仍不是RunSaveManager/原版大厅/奖励副作用恢复。

实际消息描述符注册、重复注册返回同标识、订阅及Dispose、JSON codec均通过。**先前纯.NET独立宿主Fatal0xC0000005保留为历史失败；本次已用Godot宿主解除该运行阻塞。** 不能再将消息注册描述为“只有编译通过/无法运行”。未调用Send/Broadcast或模拟原生联机，因此不能将其写为原生网络传输通过。

全量现有测试日志中含模型未关联Mod的诊断等输出，测试和脚本实际退出成功；不声称日志零ERROR。资源导入产生的仅换行状态变化经Git正规化确认无内容差异，未提交资源变更。

开发输出在独立工作树`dist/public/AllMatters`及`dist/public-beta/AllMatters`，是既有v0.1.1功能构建，不包含拼图正式实现，不能用于验收本票多人玩法。未向安装目录复制产物。Paths.local.props不提交，跨机器按docs/development.md设置对应绝对路径。

## 对本票结论的影响

GodotPath缺失、资源导出与双版构建阻塞已解除；RitsuLib注册API在既有Godot测试宿主下已实际可运行。可据此将它保留为集成候选，而不是因旧宿主失败排除。尚不能决定最终消息/存档架构。

原生真实多人传输、正式过场、原版多人大厅、奖励整体恢复和追猎原生战斗完整状态仍未运行。AGENT-RESULTS.md的独立宿主失败判断以本补验修订；其余未验证边界不变，整票继续开放。
