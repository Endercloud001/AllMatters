# #82–#88 当前调度看板

更新：2026-11-16。集成树：`AllMatters/.claude/worktrees/hunting-20261112`；分支：`integration/issues-82-88-20261112`；当前源码：`14c9351`。仅集成主控维护。

## 当前工作：#88 兼容验证与统一交付

**第二批修复已集成，完整Check与统一交付包核对完成，待用户实机复测。** 在制0/2，Ready0，技术诊断暂停3；Mystic回血未签修复，#88未整票完成。

|目标|当前完整验证 / 实际归档（本树`.local/tests/archives/`）|Build / Host|Engine / Metadata|
|---|---|---|---|
|public-beta 0.113.1|669通过；`run-20261116-105950642-199a7d0e4d1141f391fa1993568d0876`|0 / 0|26 / 0|
|public 0.107.1|671通过；`run-20261116-113834407-29d4c94cc5914de58159cef3809cf1cb`|0 / 0|26 / 0|

本次控制台：`.local/issue88-manual2/full-check-14c9351.log`，完整Check退出0，双版Release及全部步骤PASS；26条仍为既有负例/宿主诊断。第一批226c4e9的661/663与历史归档见[实施索引](issues-82-88-implementation.md#第一批人工反馈修复2026-11-16)。

## 人工已通过

第一批：追猎商店/休息/宝箱；暗港三精英与乐加维林族母/灵魂异鱼；密林普通站位、三精英/三Boss。第二批：暗港普通站位；帝皇蟹/无厌沙虫；荣耀三精英；女王/永世沙漏Boss逻辑。保留用户实际反馈范围，不扩签双版、多人或完整终局。

## 第二批卡片

|卡片|状态 / 源码|已证与剩余|
|---|---|---|
|M88-2-flow|Integrated / `b3f0a49`|TestSubject/Knowledge合法预载guard、绑定效果、真实fight→新战斗→win与原Crab恢复双版strict；0.6.14窄核通过，旧黑屏跑局待实机。|
|M88-2-rules|Hive Integrated / `b3f0a49`|同entry确认后继续Weak/Frail/Vulnerable各1，双版pending确认及stale回归通过。|
|M88-2-glory|Integrated / `a464e66`|独立原生采样red→green，荣耀普通12+精英3双版strict；复用暗港算法，本体未改。|
|M88-2-torch|Integrated / `14c9351`；低优先级|生产工厂附绿焰，双版真实GL图已看；无原点残留，不改Queen规则/IntentPos，正式召唤外观待实机。|
|M88-2-mystic|Paused / `00dfbd6`|用户确认敌HP上升；三层0.6.14未复现，只合动作快照；收到实机MysticAction日志再恢复，未签修复。|

第二批原始失败、通过清单与复测操作见[验收指南](batch-6-test-guide.md#第二批人工反馈2026-11-16)；独立审查及精确归档见[第二批实施](issues-82-88-implementation.md#第二批反馈修复2026-11-16)。

## 第一批修复保留

`226c4e9`：未绑定怪物预载guard、Boss特殊节点与旧图迁移、地图Opened/Closed配对、移除入场对话；暗港原生同数量中心、五虫地面0.8缩放、Waterfall爆发后清负面+Artifact3、光门文案。完整Check/双轴审查/hooks通过，原实机出口仍按[复测顺序](batch-6-test-guide.md#本轮修复包复测顺序)保留。

## 其他暂停诊断

|卡片|当前事实|恢复条件 / 证据|
|---|---|---|
|T-beta-art-binding / #88|历史ENet地图GCHandle fatal未定位，实验已撤，不签修复。|资源绑定/Dispose新证据；`.local/enet-art-binding-20261115/first-beta-fatal`，树`map-art-lifetime-20261115`。|
|P87-boss / #87|后台HP订阅间歇失败未定位，探针已撤。|新失败/可区分观测；`boss-warmed-callback-20261113`及主仓`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`。|

## 交付与验收边界

统一包在本树`dist/batch-6-integration/AllMatters-public/`与`AllMatters-public-beta/`，每目标仅五文件；DLL均`0.1.1+14c935188d43aa7cb0082b7e6235e001bf27372b`，PCK/许可/manifest已核。前批包保存在`.local/issue88-manual2/previous-delivery/`，更早包保留原处。

完整自动宿主使用RitsuLib0.6.11；第二批实机为Workshop0.6.14，并另做相关0.6.14窄核。Agent不启动游戏/Steam、不写真实mods或存档。真实连续精英/事件、旧卡局、奖励后跨幕/暗滤镜与新外观仍由用户复测；BaseLib Continue清理异常及D3D12设备丢失未签修复。

真实多端/设备/终局责任见[验证地图](issues-82-88-verification-map.md)。#93/#94既有签收保留，#88未自动关闭；未推送或正式发布。调度遵循[批次流程](agents/batch-flow.md)：最多两片在制、唯一执行者、独立双轴、正常hooks。
