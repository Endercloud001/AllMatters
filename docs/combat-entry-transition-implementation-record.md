# 普通节点进入过场实施记录

日期：2026-11-12。实施起点 `4c35375ca9e2f7a96c56046f86db6e2846424545`，集成分支 `integration/batch-3-20261111`。开工已有变更仅本轮实施计划；未改主 checkout、其他 worktree、正式游戏安装或采集包。

## 实现

普通 `EnterCore` 在完整 `ToSave+Copy` 前复用 `NTransition.RoomFadeOut`。完整检查点、投影、RNG 与原生 `EnterMapCoord` 的相对顺序保持；确认原生房间及共同决定后才揭幕。失败先恢复拼图地图，再按拥有权揭幕。屏幕暂时禁用处理（包括子 GUI），原有 `enteringTask` 防重入；await 后重验会话和 attempt，恢复输入尊重暂停。

两版共用聚焦的 `JigsawEntryTransition`。它只在普通入口期间持有当前原生 transition；已有 `InTransition` 时拒绝抢占。四个共享 tween 的原生 Fade/RoomFade 入口只观察同实例上的外部接管，不更改外部参数、Task 或取消；外部接管会取消未完成淡出等待，旧操作不揭幕。会话关闭也取消等待。

public 的 `EnterMapPointInternal` 异步状态机由真实方法的 `AsyncStateMachineAttribute` 定位，只把末尾唯一的 private `FadeIn(bool)` 调用换为局部桥接。当前普通 native invocation 的 AsyncLocal scope 延后该调用；其他调用点及 scope 外调用执行原生方法。scope 在 native invocation 返回后失效，继承执行上下文不能延续抑制；本次视觉拥有权丢失也不能让旧 native call 揭开其他过场。匹配不兼容时保留原 IL，报告并停用本功能过场适配。

真实 ENet 测试发现：快端已进入原生 `CombatStateSynchronizer.WaitForSync`，另一端淡出失败而从未进入时，快端会一直等原生同步。新增仅绑定当前 round/attempt 的 `EntryFadeReady`，追加现有 enum 末尾，复用可靠 entry descriptor。所有端淡出成功才共同开始 native entry；仍需原有 native-ready 共同确认。发送本地 fade-ready 后，本端承担 native 参与责任，之后视觉接管只阻止自有揭幕；不因仅视觉变化退出原生同步。淡出失败沿原有 `EntryFailed` 和完整点可靠重试。此状态不持久化，不是通用动画/屏障框架；运行时协调的具体必要性见实施计划。

## 验证证据

开工双版原集合均 392 通过，BuildExit/HostExit 均为 0。beta `41cef1e`，public `5926027`；日志 `.local/combat-entry-transition/start-public-beta.log`、`start-public.log`。既有 EngineErrors 分别 20/21，ModMetadataErrors 均 0，无首个异常或准备失败；包括测试故意失败路径、public 复用 beta 资源的 `NCreatureVisuals` 提示和退出资源提示，不能解释为实机视觉通过。

按纵向切片记录真实行为 red：

- `red-1.log`：引擎鼠标选点后已进 MerchantRoom、Visited 由 1 增至 2，但 RoomFadeOut 调用为 0；随后双版 `green-1.log` 通过。
- `red-2.log`：public 真正末尾 callsite 调用了 private FadeIn；`green-2.log` 在普通 invocation 阴性、scope 外 private 方法阳性断言均通过。TestMode 自然跳过该 wrapper 内的视觉，故通过观察 private wrapper 是否实际执行验证路由。
- `red-3.log`：外部同一 NTransition.RoomFadeIn 接管后旧进入仍等待；`green-3.log` 验证取消传播、不进入、不旧揭幕。
- `red-4.log` 对应的 ENet 子日志重现淡出失败使快端等不到共同退出；改用本 attempt 的 fade-ready 协调后 `green-4-recovery.log` 两版真实三进程慢端、失败和整点重试通过。最早一次探针错误地等待同步前的 RoomEntered 属于测试准备问题，未作为行为 red。
- `red-5.log` 及其 host.err：public 已承诺进入后的视觉接管，旧 native private FadeIn 再次执行；修复 invocation scope 生命周期后 `focused-final.log` 中真实三进程 public 接管专项通过。

新增专项覆盖正式 ShowMap/CreateScreen 入树、引擎鼠标/按键、原生 EnterMapCoord/RoomEntered、真实三进程共同就绪与失败重试、已有原生过场不被抢占、失效 attempt、旧会话隔离、失败后引擎输入重试。测试只替代原生动画完成 Task；慢房间用原生 EnterInternal Task 后置等待，保留真实同步和房间工作。失败后的 retry 经正式按钮引擎确认。失败注入和取消会产生预期错误日志。

自动化不验证 Normal/Fast/Instant 的可见扫屏、真实材质/层级、战斗横幅剩余播放时间或实际联网视觉。游戏启动、安装与用户签收按 [验收指南](combat-entry-transition-test-guide.md) 手动执行。

## 当前状态

首轮完整 `final-check.log` 实际失败，不能作为通过证据：两版既有 `Jigsaw native map previews Ancient and unfinished rooms return safely` 用例读取已释放屏幕的 ProcessMode 抛出 ObjectDisposedException（`final-public-beta.log:3594`、`final-public.log:3667`）。新增读取先检查 Godot 实例有效性后，`review-fixed-disposed-screen.log` 双版原用例通过。首轮脚本、资源、扫描、两版构建通过；随后 xUnit 两版各 27 通过。保留旧日志，新完整轮使用 `final2-*`。

首次 Standards 审查发现首个测试未实际观察保存，已加入真实 `RunManager.ToSave` postfix 计数：输入前归零，淡出挂起时必须为 0，真实入房后必须大于 0。首次 Spec 审查发现可靠 EntryFailed 可先于某端创建 fade decision，随后新建未完成 Task 会挂起；真实三进程 `red-review-earlyfailure.log` 与项目内 temp 子证据重现，修复为已知失败不再发 ready/创建后决策尊重已有失败。`review-fixed-focused.log` 双版全部新增过场专项通过，包含 `Jigsaw entry transition native ENet early failure reaches endpoints still fading`、原有慢端及整点恢复；不把编译准备失败算 red。

19 份先前位于系统 Temp 的本轮 ENet 证据已复制至项目 `.local/combat-entry-transition/evidence/`，`evidence-map.txt` 记录原路径与项目路径。后续命令进程内将 TEMP/TMP 指向项目 `.local/combat-entry-transition/temp/` 并还原环境，不删除原临时数据。

最终 `tools/Check.ps1 -Branch All` 退出 0（`final2-check.log`）：脚本语法、脚本工作流、空白、占位扫描、资源、两版完整宿主及两版普通构建全部通过。beta **401**、public **403** 通过，BuildExit/HostExit 都为 0，EngineErrors 仍分别 20/21，ModMetadataErrors 均 0，FirstException/PreparationFailure 均空。两版实际游戏程序集版本仍为上列 `41cef1e`/`5926027`。对应原始宿主日志 `final2-public-beta.log`、`final2-public.log`；同源码后的 xUnit 两版各 **27** 通过（`final2-xunit-<branch>.log`）。

Spec/Standards 首次审查各一项发现（上列 P1 早失败决策与 P2 保存观察），均已修复；两名原 reviewer 独立读取更新 diff 与专项证据复审，均无剩余发现。资源导出造成的 106 份 `.import` 换行漂移先确认无语义 diff 后已还原，忽略目录未跟踪；不将其混入提交。

实际依赖配置为完整 RitsuLib **0.6.10**（开发文档的 0.6.7 为旧环境描述）。提交由主会话在上述复审和完整验证后执行；最终独立普通包须在该提交后重新构建，实际提交号、DLL ProductVersion 与五文件目录见 `dist/combat-entry-transition/delivery.md`。准备目录与指南已完成，不把提交前构建的版本号称为最终交付。普通包须全队升级同一消息集，旧普通包及旧采集包不能与本次多人混用。

## 用户实机验收（2026-11-12）

用户确认已按验收指南完成实际测试，单人和多人的过场验收完全通过，各种表现均符合预期。本次签收对应交付实现提交 `10410c4e632c3e450d335252783fd55a59fdaae0`；用户未另行提供具体游戏分支、人数或逐项录像，故不补写这些细节。上述确认完成本功能的人工过场验收，不扩展为性能根因已修复或 #81/#94 其他组合已签收。

用户同时授权必要的 Git 提交、同步和推送。本轮仅补记验收，生产代码与已验收五文件包保持原实现版本；同步远程后推送 `integration/batch-3-20261111`，保留工作树，主分支集成及正式发布另行处理。
