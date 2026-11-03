# Agent 自动部分收口记录

2026-11-03；只在prototype/coop-stage-recovery工作树继续，未修改主工作区、游戏安装、Steam或真实存档。此文件更新第一批EXPERIMENT.md中的历史待办；不是整票resolution或双版支持承诺。

## 本轮实际结果

|实验|版本/人数|实际结果与证据|可证明范围|
|---|---|---|---|
|第一批回归|Node24.11.1，1/3人|19项通过，results.json|给定接受顺序的符号规则|
|第二批边界|Node24.11.1，1/3人|20项通过，batch2-results.json|旧编辑/重复投票、恢复会话隔离、放弃后新进入身份、固定抽取/确认、完整奖励快照、共同继续、面具/战斗占位字段|
|受控异步|Node24.11.1，1人|4项通过，controlled-task-results.json|Promise等待、取消/超时后晚成功失效、恢复后旧任务失效/重启成功；非真实生成器|
|本机TCP|Node24.11.1，3独立连接|8次操作通过，loopback-results.json|真实OS回环TCP分帧、观察接受顺序、广播副本一致；单进程，非Steam/原生服务/WAN|
|浏览器|Chrome156.0.8078.80，模拟1/3人|12个引导按钮序列通过，0脚本错误，自由拿取/恢复通过，800宽无横向溢出；browser-results.json及两张PNG|模型页实际可运行；桌面截图已人工查看，不代表用户体验认可|
|实际RitsuLib保存/编解码|0.6.7，compat0.107.1与0.111.0|两版编译0错误0警告，运行exit0；native-probe/evidence|实际槽位Set→BuildPayload→临时磁盘→ImportPayloadIntoRun→Get相等；实际消息JSON序列化往返|
|实际原生API元数据|public v0.107.1/59260271…、beta v0.111.0/41cef1ea…|两版消息注册/发送接口存在；AfterAllRestSitesCompleted public=False、beta=True，见各run.log|接口存在和双版差异，未调用实际传输/休息|
|独立宿主消息注册|public兼容DLL|首次注册时Fatal 0xC0000005，initial-public-registration-crash.log|注册牵动RitsuLib生命周期→原生Logger→Godot.OS，独立.NET宿主无法直接使用；不能推出游戏内不支持|

第一批时序源码探针结果仍有效，但没有升级为游戏运行证据。实际保存探针使用未初始化RunState作为附件容器，反射调用内部payload入口；不运行原生RunSaveManager、房间或大厅。它证明实际扩展载荷可往返，不证明公开集成入口、Harmony保存补丁、原生磁盘格式注入、跑局恢复或奖励副作用。

消息注册调用能编译；运行遇到Godot宿主边界，不能把跳过该调用后的成功日志写成“注册通过”。代码保留PROTOTYPE_TRY_REGISTRATION=1复现开关，默认只运行安全的保存/codec实验。首次shell未单独保留进程退出码，原始Fatal及堆栈完整保存；不伪造一个测得的退出码。未尝试加载Godot原生引擎来绕过项目不启动游戏的约束。

## 重现

在独立工作树根目录运行：

```powershell
node prototypes/coop-stage/experiment.cjs
node prototypes/coop-stage/batch2.cjs
node prototypes/coop-stage/controlled-task.cjs
node prototypes/coop-stage/loopback.cjs
node prototypes/coop-stage/browser-check.cjs
& prototypes/coop-stage/native-probe/Run.ps1
```

Run.ps1使用本机同级AllMatters依赖、现有beta路径；其他机器需修改路径，未下载或复制游戏DLL。浏览器脚本可用PROTOTYPE_PLAYWRIGHT/PROTOTYPE_CHROME指定本机工具。所有保存文件使用带PROTOTYPE/wipe-me名称的系统临时目录；日志记录确切位置。双击prototype.html无需依赖。

第二批奖励实验：先保留选择前完整snapshot，改变甲金币/遗物/牌组，再从选择前checkpoint恢复；证明所选边界可避免模型混档，不证明原生奖励已回滚。写失败实验在替换完整文件前显式throw，保留旧完整文件；不声称验证了断电或文件系统原子性。

## 实验修正与范围

新增请求身份和编辑/会话世代仅用于制造重复及迟到实验，不是冻结协议或最终架构。它们是普通主键与版本计数，没有新增hash/contract/baseline/gate。恢复增加世代，放弃进入后新进入使用独立递增身份，避免“未成功进入则重用同id”的错误。自由操作不强制携带实验元数据；本页不是可部署协议，不能将其当作认证/防作弊实现。

“完成”是第一批全队完成快捷动作，第二批另外逐人“必要流程完成”验证未全员不得发面具。任务取消/超时仅为实验注入失败，不新增玩家按钮或决定正式超时玩法。没有发现需要改变已确认规则的冲突，因此无需grilling；宿主限制未改写成玩法。

## Agent部分与后续集成的边界

本次承诺补做的独立模型、受控异步、浏览器QA、实际双版依赖编译和载荷往返已执行并留证。原生注册运行的失败已复现归因到宿主，继续证明需具备Godot/框架生命周期的集成宿主；无此宿主时不能包装成通过。

以下保留为正式实现后的验收要求，当前没有可运行Mod承载，全部**未验证**：

- 原生三人消息注册/传输/断线、身份认证及旧操作隔离，正式UI运行中加入拒绝。
- 原版多人存档大厅正常退出、执行中授权恢复、保存失败/异常退出完整点；RitsuLib公开集成及原生保存补丁的全链效果。
- 原生Ancient部分奖励整体回退、全部完成后不重发、断线名单与在局人数分离、public休息完成聚合。
- 真实统一进入确认、某客户端加载失败、重复请求、同店货架/价格与事件连续战斗。
- 原版角色选择动画复用、字幕/黑屏、任务终止及错误弹窗的真实显示和保存恢复。
- 追猎实际遗物唯一性与末Boss清理；原生追加、开场能力、实验体完整回合/阶段、沙漏个人计数的完整检查点。逐敌/服务专项仍归对应验证票。

集成票可据此排除“仅坐标足够标识每次进入”“单靠退出按钮会保存所有新增阶段”“public可直接复用beta休息完成API”“载荷JSON通过就等于大厅恢复通过”等假设。现有证据允许继续设计适配，不能替它选择最终确认端、消息库、存储分工或承诺双版支持。整票继续开放；没有伪造用户反馈，没有最终resolution。
