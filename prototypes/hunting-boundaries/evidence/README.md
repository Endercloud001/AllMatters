# 命令、原始日志和证据边界

`runs.json`列出每次运行的实际脚本退出状态、目标通过行、首条相关诊断及完整日志位置。脚本外层catch会记录失败，不用外层PowerShell进程退出0掩盖内部失败。记录不会自动把实验组或整票判定为完成。

主要新增可直接读取日志：

最新选择屏批次：`combined-r12.log`退出0，双目标各51项、4536行观察；`full-check-r6.log`双目标各267项且整体检查通过，退出0。其中public完整选择屏为明确资源反例（`UI_CHOICE_SCREEN_NEGATIVE`、requirementSatisfied=false），实际Godot缺脚本/子节点Ready错误与退出资源泄漏均保留；观察断言通过不代表该要求通过。beta完整选择节点树仅在隔离输入宿主下运行，不证明物理输入、OverlayStack或游戏外观。runs.json共131次；r1–r6选择屏诊断原文在ZIP。

2026-11-04后续批次：`combined-r11.log`退出0，双目标各50项、4512行结构化观察；`full-check-r5.log`退出0，双目标各266项且整体检查通过。`rest-entry-r3.log`是实际独立休息点DLL与原生选项/按钮/房间过滤，`native-turn-r2.log`是1–4人×9玩家回合穿插原生敌方开始/行动/结束。仅作为各自边界的运行证据；具体输入和未验证范围见BATCH-RESULTS.md。runs.json现索引123次运行，包含新批次的错误过滤器、错误DLL路径等失败中间记录；原文完整保留ZIP。

最新汇总为 `combined-r10.log` 和 `one-command-r1.log`（退出0，双目标各47项原型实验）；`full-check-r4.log` 双目标各263项并通过完整检查，退出0。仍有Godot退出时各一个Material/Shader资源泄漏诊断，不能据此声称无诊断。`runtime-cases.json` 的4290行观察来自combined-r10，原始行号保持不变。

新增关键原始证据：`native-ending-negative-public*.log`（退出1，最后死亡易伤0反例）、`native-ending-r3.log`（暂缓结束后2，含阻止死亡及Artifact）、`combat-end-r5.log`（真实原生结束/清理/奖励前面具边界）、`five-worm-live-r4.log`、`queen-live-r3.log`（实际独立DLL及准确资源）、`treasure-direct-r2.log`（异步直接奖励绕过及返回值兼容性反例）。测量命令退出0不代表其中的规则缺口已经解决。

历史状态更正：full-check-r2虽双目标各260项通过，整体因文件末尾空行而失败。早期包装脚本未立即保存LASTEXITCODE，曾误记0；已改为1并保留日志。修正后r3及最新r4完整通过。完整检查日志的原始尾随空格保存在ZIP，展开可读副本仅去除尾随空白。

| 日志 | 命令 | 实际状态及范围 |
| --- | --- | --- |
| combined-r6.log | `./tools/Test.ps1 -Branch All -Filter PrototypeHunting -SkipResourceExport` | 退出0，双目标各39项；每目标含1440组原生遭遇添加/开场输入。不是完整实机/网络/恢复通过。 |
| direct-removal-r1.log | `./tools/Test.ps1 -Branch All -Filter 'direct removal' -SkipResourceExport` | 退出1：真实直接删牌绕过购买包装器，钱1000→925、牌10→9、次数1。修复探针后发送/接收拒绝结果在combined-r6。 |
| resources-r9.log | `./tools/Test.ps1 -Branch All -Filter PrototypeHuntingResource -SkipResourceExport` | 退出0：原始3.4.02骨架不加载；准确三物种作者转换资源4.2.11实际加载/播放。 |
| treasure-r7.log | `./tools/Test.ps1 -Branch All -Filter PrototypeHuntingTreasure -SkipResourceExport` | 退出0：真实房间进入/普通及异步额外奖励/Offer/空投票通知/退出，非奖励效果保留。 |
| ui-r6.log | `./tools/Test.ps1 -Branch All -Filter PrototypeHuntingUi -SkipResourceExport` | 退出0：原生商店/休息节点和劫难/柔嫩展示；最新中英补验见combined-r6。 |
| hud-r4.log | `./tools/Test.ps1 -Branch All -Filter PrototypeHuntingHud -SkipResourceExport` | 退出0：实际Godot子视口四态、焦点，以及独立DLL注册入口；仍待游戏实机。 |
| presentation-build-r3.log | `./prototypes/hunting-boundaries/presentation/Build.ps1` | 退出0，public/beta独立HUD显示包构建与整理。构建本身不当作行为证据。 |

其余新增中间日志和退出文件完整保存在 `intermediate-runs.zip`，包含失败、宿主配置调整、旧版泄漏报告和被后续实验替代的结果；文件名与runs.json一致。原始文本另留本机忽略目录 `.local/hunting70-intermediate/`。可在任意新空目录用 `Expand-Archive -LiteralPath '<本目录>/intermediate-runs.zip' -DestinationPath '<新空目录>'` 阅读。没有删除历史失败，也没有归档/修改原先已跟踪的首批日志。

资源PCK、原始游戏素材和依赖DLL不在日志归档内。后续中间日志本机原文另留`.local/hunting70-intermediate2/`；runs.json保留全部113次运行索引。完整规则始终引用追猎resolution，局部适配与显式驱动范围见BATCH-RESULTS.md。
