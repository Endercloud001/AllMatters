# #80 多人闭环续作

2026-11-08。起点 `a43f61e6f66b0935ce3dac5b7088d184f338e535`。本轮补齐该提交的接入缺口；最终检查、审查及提交尚待完成，不以本记录代签整票完成。

## 已取得的自动证据

测试均使用实际 public 0.107.1 / public-beta 0.111.0 DLL，逐目标运行。日志保存在忽略目录 `.local/issue80-followup/`。

| 行为 | 证据 |
|---|---|
| Host 模式选择经原生大厅开局传到各端，客户端相反设置不改变结果 | `activation-affected-{beta,public}.log`；Host 开启、关闭均有三进程对照 |
| 正式启动回调连续进入编辑、投票、实际 Ancient 与共同继续 | `integrated-final-green.log`；原生 BeginRun 消息、SetUpNewMultiplayer、Launch、Godot 输入及原生队列 |
| 明确建删线意图、同一投票请求重发不撤票 | `protocol-domain-green.log`、`protocol-final-network-green.log`；领域延迟意图与实际三进程重发分别留证 |
| 旧阶段、待处理端点、旧 session 提交及快速连续端点 | `stale-*.log`、`endpoint-final-green.log`；各项证据层级以测试入口为准 |
| Host 重置走共享顺序，非 Host 拒绝，原池保留 | `reset-network-actual-green.log`、`reset-final-green.log` |
| 原生选项消息执行真实卡牌/遗物效果；部分队员完成仍保存整个奖励前点 | `ancient-whole-checkpoint-green.log`；比对原生数据及全部 Ritsu 扩展 |
| 已共同继续后晚到的本地实际奖励回调仍释放安全点 | `lagged-green.log`；原生本地滞后回归，不称为三进程延迟证明 |
| 仅 Host 执行自动 worker，结果和失败经原生队列发布 | `automatic-authority-final-{green,failure-green}.log`；客户端 worker 若被调用即抛错，另有 Host 抛错场景 |
| 单人原有自动流程及真实奖励回退/重放 | `automatic-authority-final-singleplayer.log`、`ancient-singleplayer-regression.log` |

早期 `endpoint-native-green.log`、`reset-network-green.log` 未实际插入对应新断言，不作为该新增场景证据。编译、初始化、名单或监听器就绪错误均属于准备失败；有效行为 red 单独保留，不能以文件名推断结果。

## 宿主边界

- 网络使用三个独立无界面 Godot 进程、原生 ENet、原生大厅消息和 Ritsu 托管动作；保留握手、兼容性及能力检查。这高于消息编解码或模拟角色人数，但不是三台设备上的正式游戏验收。
- 自动宿主以监听器替代角色选择界面和 NGame/NRun 场景加载；调用原生 `Launch()` 补齐身份和消息解缓冲职责。没有启动游戏或 Steam。
- 奖励测试使用原生 DebugOption 固定 public 的 Precise Scissors / beta 的 Pomander，执行真实选项与效果。选牌由对当前合法候选取首项的测试 selector 提供，绕过 PlayerChoiceSynchronizer；不宣称验证自然种子的选项分布或真实设备选牌同步。
- 保存只使用隔离 MockGodotFileIo 和实际 RunSaveManager。现有 SaveManager 拦截将原生 ToSave 送入隔离管理器，绕过外层 Host/ShouldSave 判定及 preFinishedRoom 转交；不能据此代签这些未经过的外层分支。
- public 自动宿主共享 beta 资源，不代签完整 public 正式资源、完整 NGame 渲染或实体手柄。

## 职责及批次

沿用 [已确认边界](jigsaw-multiplayer-boundary.md)：#76 提供保存基础；#80 接入在局名单、个人 Ancient 完成及必要安全点；双端部分领奖后的完整原生大厅恢复由 #81 集成验收。本轮不启动 #81、不关闭 #76，也不修改父规格或原生依赖。

#80/#81 属第 3 批“多人拼图及探索”。整批统一包尚未就绪，当前无需用户安装，不重复已通过且未受影响的单人保存、跨幕素材、字体及计步验收。后续整批仍需每版真实三端编辑/争用/断线/Host 退出/实际 Ancient，以及 #81 的探索和大厅恢复场景；人工通过只按用户实际反馈登记。
