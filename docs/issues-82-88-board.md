# #82–#88 当前调度看板

更新：2026-11-15。集成树：`AllMatters/.claude/worktrees/hunting-20261112`；分支：`integration/issues-82-88-20261112`；当前候选：`41b2130`。本板仅由集成主控维护，整票状态不从切片通过推定。

## 当前工作：#88 兼容验证与统一交付

**自动实施在制0/2，Ready0，技术诊断暂停2；统一包已就绪，人工待验。** #87 Queen右火炬布局修复已集成；当前候选完整 `Check.ps1 -Branch All` 已退出0，脚本、空白、资源、两版宿主及两版构建均通过。主控持唯一native/API/build/formatter/hooks执行权，当前检查与构建进程均已结束。

|目标|通过数|Build / Host|Engine / Metadata|实际归档（本树 `.local/tests/archives/`）|
|---|---:|---|---|---|
|public-beta 0.113.1|654|0 / 0|26 / 0|`run-20261115-172333350-347d3f22e4544ec18a012a60f814c3ca`|
|public 0.107.1|656|0 / 0|26 / 0|`run-20261115-180125874-702ef8e3ecd840c681d478cff9b29529`|

完整控制台：`.local/implement-resume-20261114/full-check-20261115-41b2130.log`。Engine诊断包含负例、注入及宿主诊断，不称零错误；分类见[实施索引](issues-82-88-implementation.md#完整检查与已知诊断)。旧beta0.113.0日志保留历史归属。

## 当前卡片与下一动作

|卡片|状态 / owner|下一动作与结束条件|
|---|---|---|
|W88-delivery / #88|Ready for manual / 主控|41b2130双版五文件包已生成，最终PCK/NOTICE/依赖说明已核；按[验收指南](batch-6-test-guide.md)集中安装验收。产物在本树`dist/batch-6-integration/AllMatters-public/`与`AllMatters-public-beta/`。|

依赖锁定完整RitsuLib 0.6.11 variant-pack；隔离宿主使用该版本，游戏安装manifest仍为0.6.7，最终加载版本需安装时核对。Agent不启动游戏/Steam、不写真实mods或存档。

## 最近集成：本轮检查消除的缺口

|归属|修复 / 提交|已证范围|
|---|---|---|
|#87 Queen布局|右Torch marker X1330→1320；`2b0725d`→`41b2130`|原public四角色720p血条遮挡有真实red；双版Queen各9 strict通过，新候选完整Check通过。[几何与原档](issue87-boss-implementation.md#queen-右火炬血条与女王目标分离2026-11-15)|
|#85 Knowledge宿主|每例绑定真实Sidecar网络、按owned epoch清理；`c52af37`→`4972518`|原序旧features残留已窄复现；双版三例strict及当前完整原序通过。仅签headless适配，不签正式跨跑局入口。|
|#85 Hive宿主|在真实原调用者观察唯一startup Task；`654e25c`→`c4bc995`|原漏观测actual0已修；双版Hive/Waterfall专项及当前完整原序通过。|
|批次共享宿主|保存恢复测试不重复初始化原生模型缓存；`fb9e6bc`→`030eb34`|库存恢复、真实RestRelic握手专项及当前完整原序通过。|

上述切片均完成独立双轴审查、正常hooks及已审补丁一致核对。此前Knowledge三轮、蟹fresh、Matriarch/Aeonglass/TestSubject布局和长译文等已集成记录统一从[实施索引](issues-82-88-implementation.md)进入，不再保留过期派发卡。

## 暂停的技术诊断

|卡片 / issue|当前事实|恢复条件与证据|
|---|---|---|
|T-beta-art-binding / #88|曾在ENet地图绘制发生GCHandle致命错误；强持有实验已撤回，`map-art-lifetime-20261115`树干净。当前完整Check未复现，未签修复。|取得同一资源对象绑定重建或实际Dispose的新证据再恢复，不能重复缓存实验碰绿。首fatal：本树`.local/enet-art-binding-20261115/first-beta-fatal`；实验补丁在实施树`.local/map-art-lifetime-experiment/experiment.patch`。|
|P87-boss / #87|历史宿主后台HP订阅间歇失败未定位；探针已撤回，不把后续通过改签根因修复。|有可区分假说的新观测或新失败再限定预算恢复。保留`boss-warmed-callback-20261113`；主仓诊断`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`。|

## 交付与人工验收边界

- 批次4：#82/#83；批次5：#84–#87；批次6：#88。每个目标使用一个统一包，不逐票安装。
- #82–#87的逐票覆盖和剩余责任见[验证地图](issues-82-88-verification-map.md)；集中操作见[验收指南](batch-6-test-guide.md)。真实多端、设备/像素、适用断线死亡及实际终局仍按各票记录；#93/#94既有用户签收保留。
- 当前通过不代表整票关闭、正式游戏验收或已推送。用户现可按集中指南安装，分别保留public/beta实测结果。

调度继续遵循[批次流程](agents/batch-flow.md)：最多两个在制片、唯一执行者、独立双轴、正常hooks；每次重跑须有源码/资源/环境变化、新假说或既有集成交付要求。
