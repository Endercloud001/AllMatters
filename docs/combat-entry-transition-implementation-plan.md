# 节点过场最小复用实现计划

日期：2026-11-12。起点：`4c35375ca9e2f7a96c56046f86db6e2846424545`；集成工作树开始干净。用户已指定按《原版战斗入口过场及 AllMatters 复用调研》的最小方案实现，并要求分派 subagent。

## 文档适用性与范围

调研足以作为实现起点：双版本真实方法体、普通入口、原生外围过场、保存及多人确认顺序已查明。它不是可机械复制的代码；public 内建 FadeIn 的限定范围、全局过场的生命周期与 await 后重验须在实现中具体化，并通过真实入口测试。原调研位于主检出的 `docs/combat-entry-transition-research-2026-11-12.md`，相应双版本方法体在本树 `.local/combat-transition-research/`。

只补普通 Jigsaw 节点进入的原版房间过场，包括问号、商店等同一普通入口。保持玩法选点、完整保存、RNG、共同就绪和恢复语义。不接管原生地图旅行，不重复战斗横幅，不同时实施性能优化。原有 RetryEntry、读档、幕切换、事件子房间的原生过场不被全局拦截。

## 设计约束

在已有 `Enter`/`EnterCore` 生命周期协调一次本地过场：锁定输入，在完整 `ToSave+Copy` 前 await 原版 RoomFadeOut；await 后重验 session、pending attempt 与进入有效性；保留原保存与投影时点，只进入一次。成功时等待本地或全队原有确认，再完成揭幕；失败先恢复地图再揭幕。离开/切局后的旧操作不能清理新局的全局过场。过场自身有共享 Tween，不可用无条件 finally FadeIn 抢占其他过场。

beta 不自动淡入，由本地拥有本次过场的流程揭幕。public 原生 EnterMapPointInternal 内建 private FadeIn，只可在当前普通 Jigsaw 入口的精确作用域延后，其他调用保持原样。新增 Harmony patch 目标须先在 `.local/api/queries/` 留存真实双版本查询与签名。沿原版 FastMode 行为，不擅自改 Instant 偏好；不宣称遮罩能减少 GC 或保存开销。

文件职责：`src/Jigsaw/JigsawNativeSession.cs` 接入原有进入生命周期；必要时新增一个聚焦本功能的过场文件以集中原版适配/生命周期，不创建通用动画框架；`src/Tests/JigsawNativeTests.cs` 及现有正式 UI/原生宿主验证行为。实现者可按实际结构调整具体文件，但不得越过上述范围。

### 真实联机验证后的补充

新增淡出 await 后，三端测试重现“快端已进入原生 CombatStateSynchronizer.WaitForSync，慢端淡出失败从不进入”的挂起。仅原有房间就绪后的 EntryFailed 通知不能让原生等待完成。因此复用现有 entryDescriptor，追加仅当前 EntryRound/attempt 的过场就绪信号，所有本地淡出成功后才开始原生进入；原有房间共同确认仍保留。新枚举追加末尾，不改既有消息编号，不持久化第二份进入状态。

这是针对此失败的运行时同步：Git、版本、主键、事务、唯一约束、类型约束只能保护代码或局部数据，普通测试能够揭示挂起但不能在实际运行中协调不同端。没有新增通用 gate、冻结协议或额外存储。发送本地过场就绪后，本端不能再因纯视觉接管/暂停撤回原生同步参与；视觉所有权丢失仅意味着不能揭幕。所有端须升级本次包，不能混用旧普通/采集包。

## 执行与验证

- [x] 准备：运行 `tools/ReviewScope.ps1 -ReviewedFrom 4c35375ca9e2f7a96c56046f86db6e2846424545`；核对本树忽略目录未跟踪；按项目入口完成双版宿主开工检查，保留日志。复用既有依赖与资源，不启动正式游戏、不修改安装目录。
- [x] 第一条纵向切片：复用已约定的生产 Jigsaw 入口、Godot 入树 UI、原生 EnterMapCoord/就绪回调作为测试位置；先构造“过场完成前不开始保存/进入，完成后进入一次”的失败行为断言，再加入最小 beta/public 接入使其通过。使用真实节点与原版方法，必要宿主替代须记录，不能用孤立策略模拟整个产品流程。
- [x] 逐个扩展：慢端未确认不揭幕；public 不内部提前/重复淡入；失败恢复后揭幕并恢复输入；淡出期间会话/attempt 变化不继续进入；旧会话不操作新过场；与已存在全局过场不互相 Kill。每个新增测试先验证确实会在缺少相应行为时失败，再实现。保留异常、取消、保存恢复与可靠共同进入行为。
- [x] 双版专项与完整验证：按 `tools/Test.ps1 -Branch All -Filter <真实专项名> -SkipResourceExport` 迭代；最后运行 `tools/Check.ps1` 的双版完整检查、适用 xUnit/构建。失败先保留原日志并诊断。headless/TestMode 跳过真实动画，只能验证时序和原生路由；Normal/Fast/Instant、遮罩层级、战斗横幅与真实多人视觉由用户实机签收。
- [x] 暂存仅本轮文件，以 `git diff --cached 4c35375ca9e2f7a96c56046f86db6e2846424545 -- <本轮路径>` 做独立 Spec/Standards 审查；已解决首次两项发现、复验受影响专项并完成双版完整检查；两轴复审无剩余发现。本地提交由主会话随后执行，不 push/发布/关闭 issue。
- [x] 准备双版普通测试包的项目内独立目录 `dist/combat-entry-transition/`、构建步骤与用户验收指南，保留现有普通与诊断交付。最终五文件普通包在上述本地提交后重新构建，提交号及实际 DLL 版本补记该目录 `delivery.md`；无需更新诊断采集包。只提供用户手动安装，不写游戏 mods。

异步约束：空 `write_stdin` 使用 `yield_time_ms=300000`；外层 `functions.exec` yield 至少比最长嵌套等待多 30000 ms。日志保存项目内 `.local/`，只输出当前判断所需证据。默认不新增 hash、冻结 contract、baseline 或 gate；保留既有安全措施。
