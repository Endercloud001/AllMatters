# #80 开发进度交接：继续验证、审查并提交

交接日期：2026-11-08（Asia/Shanghai）。先看同目录 [项目基本介绍](AllMatters-project-overview-2026-11-08.md)，再使用本文件接力。本次用户明确要求两份交接文档，前一实现回合被有意中断；**现在不要将仍未完成的收尾写成已经交付。**

## 当前最重要的事实

- 仓库：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters`；分支 `main`。
- 当前 HEAD / 本轮起点仍是 **`a43f61e6f66b0935ce3dac5b7088d184f338e535`**，提交名 `feat(jigsaw): support native multiplayer editing (#80)`。
- 本轮补缺口改动 **尚未提交**。有部分暂存，也有同文件的后续未暂存修改；不能直接提交现有 index。
- 所有子 Agent 已完成并交回源码/构建修改权；交接核查时无活跃实现者。最后一个 `pending_reward80` 已明确报告无构建在运行。新会话仍应检查是否有被中断遗留的命令再启动构建。
- 当前未执行本轮最终全量 Check、完整两轴审查、最终干净源码打包和提交。旧的 301 项全量通过记录属于 `a43f61e` 阶段，**不能代签本轮完整工作树**。
- #80 尚不能仅凭这一交接标记完成；#76 不关闭，#81 不启动整票。没有启动游戏/Steam、安装 mods、修改真实存档或发布 Release。

## 用户授权、规格与边界

用户最后的开发目标是：使用适当技能继续实现 #80，补齐审查发现的明确缺口，之后完成适用检查、两轴审查和当前分支提交。没有要求推送或正式发布。

权威与已有决定直接读原件，不在此复制规格：

- [父规格 #73](https://github.com/Endercloud001/AllMatters/issues/73)
- [#80](https://github.com/Endercloud001/AllMatters/issues/80)、[#76](https://github.com/Endercloud001/AllMatters/issues/76)、[#81](https://github.com/Endercloud001/AllMatters/issues/81)
- 用户已确认的职责决定：[评论 6706936501](https://github.com/Endercloud001/AllMatters/issues/80#issuecomment-6706936501)
- 仓库 `docs/jigsaw-multiplayer-boundary.md`、`docs/jigsaw-lobby-checkpoints.md`

简要边界：#76 保存基础由 #80 接入在局名单、个人 Ancient 完成及必要安全点后可以独立收口；双端部分领奖后的完整原生大厅恢复统一交给 #81 集成验收。**这不是现在关闭 #76 的授权。** 不重建“76 等 81、81 等 77、77 等 76”的收口循环，不擅自修改父规格或原生依赖。最后读取时 #80 仅被已关闭 #74 阻塞；接手时重新核实远端关系。

已确认的单人保存/退出/继续、跨幕素材、第三幕双 Boss 步数位置、字体和计步人工通过保持有效，未受影响时不重复索要人工验收。

## 工作树与 index：优先保护

本轮开工快照位于：

- `.local/issue80-followup/start.txt`
- `.local/issue80-followup/pre-existing.txt`
- `.local/issue80-followup/pre-existing.patch`

这些记录用于识别原有无关修改。特别是 `AGENTS.md`、`GLOSSARY.md`、tracker/范围文档、遗物计数器/呈现、`NativeCounterTests.cs`、Check/路径工具，以及若干未跟踪研究/工具文件，不要纳入 #80 提交或开发包。直接对照快照与 `git status`，不要依赖口头文件名单。

目前 index 只有激活切片的五个路径：

1. `src/Jigsaw/JigsawCheckpoint.cs`
2. `src/Jigsaw/JigsawNewRunMode.cs`
3. `src/Jigsaw/JigsawPreferences.cs`
4. `src/Tests/JigsawActivationTests.cs`
5. `src/Tests/JigsawNativeTests.cs`

其中两个测试文件还有后续未暂存修改（`AM` / `MM`）；其余协议、原生会话、重置、领域和测试改动也未暂存。新增 `docs/jigsaw-multiplayer-followup.md` 是本轮交付说明草稿，尚未最终更新/暂存。

## 已实现到哪里：按原件导航

目前已补上主要生产接入缺口，并取得双目标专项证据。以下仅列接手导航，细节看源码、diff 和各交接原件，不把它当作最终审查结论。

| 已完成切片 | 阅读位置 / 最新证据入口 |
|---|---|
| 正式多人开局共享 Host 模式选择，经原生 BeginRun 进入 SetUpNewMultiplayer；客户端相反偏好不改变结果 | 新 `JigsawNewRunMode.cs`、`JigsawActivationTests.cs`；`activation-affected-{beta,public}.log` |
| 原生请求序号、编辑修订、旧 session 拒绝；明确建删线意图及快速输入的端点 framing | `JigsawEditorAction.cs`、`JigsawRun.cs`；`protocol-*`、`stale-*`、`endpoint-final-green.log` |
| Host 重置经同一接受顺序和广播，保留原池，清全员临时态；异步结果不提前报已应用 | `JigsawResetCommand.cs`、动作适配器；`reset-network-actual-green.log`、`reset-final-green.log` |
| 三进程实际原生选项与奖励；部分完成时保持整个原生与全部 Ritsu 扩展同点 | `JigsawNativeSession.cs`、`JigsawNetworkTests.cs`；`ancient-whole-checkpoint-green.log`、`ancient-singleplayer-regression.log` |
| 正式开局连续进入编辑、共识、真实 Ancient、保存和共同继续 | `.local/issue80-followup/official-flow-handoff.md`；`integrated-final-green.log` |
| Host 独占自动 worker、原生队列发布结果/失败，阶段工作延后执行 | `automatic-authority-final-{green,failure-green,singleplayer}.log`；`automatic-authority-processes/` |
| 持有者断线由 Host 排序，保其他票并启动新授权；决策人数以权威 Map 集合为准 | `disconnect-final-green.log`；运行场景见下面环境变量 |
| Host socket 退出即失效，但保存查询仍保护奖励前点；取消等待 worker，旧结果不能提交 | `hostexit-{final,worker-final,normal-final}-green.log`；`hostexit-*-children/` |
| 3 原队员 / 2 在局真实奖励、缺席者未被发奖、晚到真实原队员完成事实 | `.local/issue80-followup/absent-handoff.txt`、`absent-native.log`、`absent-normal-regression.log` |
| 晚到奖励回调能在逻辑 Exploring 后释放安全点；持点期间不能提前进入新房 | `lagged-*`、`late-entry-{red,green}.log` |
| **最后完成**：缺席者已开始的原生领奖任务挂起/抛错时，整体回退点不会过早释放 | **`.local/issue80-followup/pending-reward-handoff.txt`**；`pending-reward-{red,green,fault,native-regression}.log` |

全部日志路径在仓库 `.local/issue80-followup/` 下。最后切片复用了原生 `EventOption.WasChosen` 和待选项任务事实，未另建奖励账本；异步等待在原生队列/事件回调之外，并重新检查 session、map、synchronizer 和事件身份。原生等待函数吞异常不能被当成安全完成，已选但未完成的原角色事件仍保留整个回退点。

**过期资料警告：** `.local/issue80-followup/root-progress.md` 记录了中途状态，其 ACTIVE Agent、待办 Host 退出、缺席奖励和 pending-owner 部分已经过时；`absent-handoff.txt` 中“pending owner 尚未完成”也已由最新 `pending-reward-handoff.txt` 解决。不要按旧待办重做。`docs/jigsaw-multiplayer-boundary.md` 的“生产缺口”是早期起点说明，需要改为历史记录并链接最终交付说明。

## 必须知道的运行时陷阱

- 原生 `RunManager.Launch()` 是正式启动职责：设置 LocalContext 身份并释放网络消息缓冲。曾因测试监听器漏调用而出现 Host 自己执行、客户端不执行；最终宿主按两版真实 NGame.StartRun 调用 Launch，不能用手动关缓冲或禁认证替代。
- 原生动作 ID 已提供跨玩家接受顺序，不另造第二套 Host 排序。动作载荷的序号/修订用于拒绝重放和旧编辑阶段，不是新 hash/contract/gate。
- Ritsu 的 ToSave 后处理会附当前文档。仅保存 `BeforeAncient` 对象指针会混入部分新扩展；当前深复制和后置恢复完整文档是必要修正，不能在“简化”时删除。
- live `Current` 与关闭后仍用于保存的 `SaveSession` 不可合并：退出后要拒绝新编辑/旧 worker，同时在清理前继续保护晚到保存。
- 旧测试直接调用 AncientFinished 不能证明领奖。当前三进程正常链路已改为原生 `ChooseLocalOption`、实际选项消息和效果。

原生 API 细节看 `.local/issue80-followup/ancient-api.md` 及其引用的实际双 DLL 导出。不要依据最新版参考库源码猜本机 Ritsu 0.6.7 API。

## 下一 Agent 的收尾顺序

1. 读取 AGENTS、开发指南及本交接，核对 HEAD、index、工作树、无遗留运行进程。先保留当前全部改动；不要 reset、clean 或覆盖 index。
2. 对照 #80 做一次最终需求矩阵。已知还值得补的专项：最后票与 Reset 两种顺序、Generating/Ancient 拒绝 Reset、连续 Reset、客户端伪造 FinalizeAutomatic 拒绝、在途重置被关闭取消。部分已有守卫和相关测试，但缺专门断言；先检查现有 Cases，避免重复或为测试而测试。快速端点与延迟建删的部分证明仍是原生本地/领域层级，应明确哪些经过三进程。
3. 只修可明确复现的问题，沿已确认原生/Godot seam TDD；不要再停在介绍或准备阶段，不重新访谈已确认职责。
4. 更新交付说明草稿和历史边界说明，记录最终自动/未验范围、批次与包状态。
5. **最终完整检查和双目标构建尚未做。** 建议以 a43f61e 加本轮精确任务差异建立隔离源码验证/打包，避免把工作树中的无关遗物改动编入包。现有 Paths.local.props 可转为验证目录的绝对依赖路径；不改游戏安装。测试/构建不能并发共享输出。
6. 只暂存 #80 文件/代码块。按 implement 调用 code-review：`mode: staged`，固定起点 a43f61e，全任务明确路径，两个只读 Agent 分别 Standards / Spec，使用完全相同的 `git diff --cached <起点> -- <路径>`。审查者可读源码/Git/已有日志，不能修改、构建、测试；不凭实现摘要批准。
7. 修复发现，重新暂存并复审更新后的实际 diff，运行受影响检查。完成前核对原有 tracked diff 与开工补丁一致，不额外新增 hash。
8. **提交当前 main**，只提交本票；无自动 push/Release。提供提交、验证版本/次数/日志、包目录、批次状态和剩余人工场景。不要关闭 #76 或推进 #81。

仅激活五路径做过中途两轴审查且无遗留；**整个当前续作尚未最终两轴通过**。不要复用该局部结论替代步骤 6。

## 专项运行提示

通用入口在仓库根目录：

```powershell
./tools/Test.ps1 -Branch All -Filter 'Jigsaw native ENet editor broadcast' -SkipResourceExport
```

以下是已存在的 opt-in 场景，不是另一份规格。一次设置一个场景，在独立 PowerShell 调用中设置/清理变量，避免污染后面的正常测试：

- `JIGSAW_AUTOMATIC_FAILURE=1`：Host worker 失败。
- `JIGSAW_DISCONNECT_CONSENSUS=1`：持有者离开、剩余一致票继续授权。
- `JIGSAW_ABSENT_ANCIENT=1`：原队员缺席，实际在局玩家领奖。
- `JIGSAW_PENDING_ANCIENT=1`：缺席者已开始的原生任务挂起；再加 `JIGSAW_PENDING_ANCIENT_FAULT=1` 测失败。

Host 提前退出的确切变量和值从 `JigsawNetworkTests.cs` 或 `hostexit-*.log` 读取，不猜名称。父进程、Host、两客户端都要保留退出状态及子日志。

空 write_stdin 轮询至少 180000ms，优先 300000ms；外层 exec yield 至少比最长嵌套等待多 30000ms。此前某些 green 文件名创建早于新断言真正插入，尤其 `endpoint-native-green.log` / `reset-network-green.log`，不能据文件名宣称新增场景通过；优先使用上表最新证据。

## 证据界限及人工交付

三个独立无界面 Godot 进程使用真实 ENet/原生大厅/动作/选项消息，是实际原生自动网络证据；不是三台设备上的正式游戏验收。监听器替代完整角色选择/NGame/NRun 场景；public 共享 beta 资源。

奖励用原生 DebugOption 固定 public Precise Scissors / beta Pomander；选牌 selector 取当前合法候选，绕过 PlayerChoiceSynchronizer。可证明真实选项任务、遗物/卡牌效果及回退点，不能称自然选项分布、设备选牌同步均已验证。

保存使用隔离 MockGodotFileIo 和实际 RunSaveManager；现有 SaveManager 前缀将 ToSave(null) 交隔离管理器，绕过外层 Host/ShouldSave 和 preFinishedRoom 转交。不能把这些分支写成测试已经覆盖。

#80/#81 为第 3 批多人拼图及探索。统一批次包仍未就绪，不要求用户现在安装或重复单人验收。真实每版三端体验、完整 public 资源、实体手柄和 #81 探索/大厅仍按各自范围保留未验；人工通过只按实际反馈登记。

## Suggested skills

- **`implement` + `tdd`**：继续补最后必要断言/修复，沿既有 seam，不重做规划。路径 `C:/Users/Endercloud/.agents/skills/{implement,tdd}/SKILL.md`。
- **`code-review`**：最终必须执行的两轴 staged 审查，固定起点和排除范围见上文。
- **`diagnosing-bugs`**：仅在出现新的宿主/异步/原生间歇性失败时，先收紧可复现反馈命令。
- **`superpowers-zh:verification-before-completion`**：最终完成/通过断言使用新鲜检查输出；不把旧 301 项结果外推到当前树。
- **`superpowers-zh:subagent-driven-development`**：若继续分片实现，精简新上下文、明确唯一源码/构建修改权，防止并发覆盖测试输出。
- **`grill-with-docs`**：仅需改变职责/依赖/规则时使用；已确认范围不再问权限。
- **`handoff`**：若再次移交，更新交接文档并明确当前 index/未提交状态。

不需要启动 wayfinder、重新 to-spec/to-tickets 或将本轮扩展成 #81 整票。目标是把已有大量实现可靠收尾，而不是再交付一轮仅文档/准备状态。
