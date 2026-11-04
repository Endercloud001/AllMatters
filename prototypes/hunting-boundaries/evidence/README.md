# 命令、原始日志和证据边界

`runs.json`列出每次运行的实际脚本退出状态、目标通过行、首条相关诊断及完整日志位置。脚本外层catch会记录失败，不用外层PowerShell进程退出0掩盖内部失败。记录不会自动把实验组或整票判定为完成。

主要新增可直接读取日志：

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

资源PCK、原始游戏素材和依赖DLL不在日志归档内。重复的框架初始化输出压缩留档；最新combined-r6完整原始行号与runtime-cases.json保持一致。完整规则始终引用追猎resolution，局部适配与显式驱动范围见BATCH-RESULTS.md。
