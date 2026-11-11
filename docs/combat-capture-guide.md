# 临时战斗切换性能采集

这是单独的诊断包，供复现第三轮反馈中的进战斗短暂停顿。普通包不包含诊断采集代码。采集包不改变选点、随机、奖励、存档或共同就绪规则，不禁用原生 GC。

## 用户操作

1. 退出游戏，保留正在使用的普通 AllMatters 五文件包和测试存档。
2. 从本集成工作树的 `dist/combat-capture/AllMatters-public-beta/` 或 `AllMatters-public/` 选匹配游戏分支的五文件诊断包，手动替换游戏 `mods/AllMatters/`。仍需完整 RitsuLib 0.6.10；不更换依赖，不同时加载两份 AllMatters。
3. 正常启动游戏，在测试存档中进入约 3–5 个战斗节点，尽量包含首次进入和后续进入。看到卡顿时记下大概是哪次，不必完整通关，也无需打开终端或调试器。多人可以先只在反馈卡顿的本机安装对应诊断包；它不新增网络消息，各端仍须相同游戏分支与玩法版本。
4. 采集文件自动写到本集成工作树内 `.local/combat-captures/`，完整路径为 `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/.claude/worktrees/batch3-20261111/.local/combat-captures/`。该诊断包绑定构建时的项目绝对路径，不适合拷到另一台电脑后沿用输出位置。复现后在地图或战斗界面停留至少 5 秒，让采集窗口结束并写盘，再退出。
5. 告诉 Agent“已复现”，Agent 读取上述本地文件分析。无需手动上传普通游戏存档或提供 Steam ID。
6. 完成采集后退出游戏，换回保留的普通包，即停止采集。保留项目内数据以便分析。

Agent 不替用户启动游戏/Steam，不写游戏安装目录。

## 能判断什么

数据用于把可见帧间隔尖峰与加载、地图刷新和 GC 次数变化放到同一时间轴，并区分同步执行与 await 总等待。GC 计数变化表示该区间发生过收集，不等于已经测到精确暂停时长；不直接测量 GPU、驱动或着色器编译。最终归因仍需结合实际数据，不能仅凭长异步等待宣称掉帧原因。

为覆盖问号房实际开战，所有房间进入都会生成记录，通过最终房型区分战斗。窗口含进入前最多 2 秒的近期帧记录、进入完成后约 3 秒，单窗口最多 60 秒；中途退出可能来不及写出最后一个窗口，请按上述步骤等候。

采集会产生额外测量开销，因此结果用于定位候选，不作为无工具情况下绝对帧耗时的保证。只在窗口内保留有限数据，统一写盘，不逐帧同步写文件；不记录玩家名称、Steam ID、牌组或完整存档。

## 代理构建入口

```powershell
pwsh -NoProfile -File tools/BuildCombatCapture.ps1 -Branch public-beta -SkipResourceExport
pwsh -NoProfile -File tools/BuildCombatCapture.ps1 -Branch public -SkipResourceExport
pwsh -NoProfile -File tools/ReadCombatCapture.ps1 -Path .local/combat-captures | ConvertTo-Json -Depth 8
```

资源改动或尚无资源包时省略 `-SkipResourceExport`。专用配置 `PerfCapture` 与普通 Debug 构建输出分离；最终提交与包版本记录在诊断包父目录的 `delivery.md`。本轮所有产出均在项目内，不覆盖 `dist/batch-3-integration/` 的普通包。

## 准备与验证记录

本轮起点 `5cfd0ae9425fbe2e618caee2d918dd07af4c1e50`，集成工作树开始干净，既有主 checkout 的未跟踪复盘排除。采集代码独立 worktree 实施，全部输出项目内；没有启动实际游戏或安装工具包。

双版采集专项已实际执行原生 `EnterMapCoord → CombatRoom → ProcessFrame → 自动结束写盘`，并验证原任务故障/取消仍传播、错误输出目录不影响房间。TestMode 跳过真实资源预加载与完整渲染，故该专项验证采集路由和文件，不代签实机预加载/GC/GPU 结论。普通双版专项验证程序集不含采集类型。证据在 `.local/combat-capture-preparation/capture-integrated.log` 及独立树 `combat-capture/.local/capture/{test3,normal}.log`；解析摘要另检查中位数、最大间隔及长帧计数。

独立 Spec 与 Standards 审查已完成；测试名按诊断/普通构建分别对应真实行为，原命名发现复审解除。项目全套验证及最终构建结果记录在诊断包 `delivery.md`，不是用户实机捕获已完成。
