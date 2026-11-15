# #82–#88 当前调度看板

更新：2026-11-16。集成树：`AllMatters/.claude/worktrees/hunting-20261112`；分支：`integration/issues-82-88-20261112`；修复源码：`226c4e9`。本板仅由集成主控维护，整票状态不从切片通过推定。

## 当前工作：#88 兼容验证与统一交付

**自动在制0/2，Ready0，技术诊断暂停2；第一批修复包已就绪，等待复测。** 完整`Check.ps1 -Branch All`退出0，脚本、空白、资源、两版宿主及构建全部PASS；正常hooks及独立双轴审查完成，检查/构建进程已结束。

|目标|通过数|Build / Host|Engine / Metadata|实际归档（本树 `.local/tests/archives/`）|
|---|---:|---|---|---|
|public-beta 0.113.1|661|0 / 0|26 / 0|`run-20261116-030906107-4209ff762559452b8bc709b5c604e663`|
|public 0.107.1|663|0 / 0|26 / 0|`run-20261116-034551760-6cfa4d088f874547b9a3047209b70ec7`|

完整控制台：`.local/issue88-manual-feedback/full-check-226c4e9.log`。26条为既有负例/宿主诊断，不称零错误；分类和历史从[实施索引](issues-82-88-implementation.md)进入。

## 第一批人工反馈

已通过且保留：追猎商店/休息/宝箱；暗港三精英及乐加维林族母/灵魂异鱼；密林三精英/三Boss及普通站位。用户未逐项注明版本，不扩签双版或完整终局。

原始失败、修改要求及操作清单见[第一批反馈](batch-6-test-guide.md#第一批人工反馈2026-11-15)与[修复包复测顺序](batch-6-test-guide.md#本轮修复包复测顺序)；已通过项目不要求逐票重装。

## 最近集成：第一批反馈修复

|卡片 / 归属|状态 / 实施树|行为与已证范围|
|---|---|---|
|M88-flow / #88|Integrated；`issue88-flow-20261115`|三怪未绑定Creature预载guard、Boss特殊点及旧档迁移、入场对话隐藏、地图/奖励Overlay信号配对。预载、实际SavedMaps加载、真实ESC等窄双版及完整Check通过。|
|M88-combat / #84/#85/#88|Integrated；`issue88-combat-20261115`|暗港自动队列对齐原生同数量采样，五虫落地0.8缩放，Waterfall爆发动作后清全部负面并固定Artifact3；双版整组/恢复及完整Check通过。|
|M88-copy / #88|Integrated；主控|光门改为“随机升级2张牌”，效果不变；双版既有升级行为各1及完整Check通过。|

各片red/green、审查增量与完整证据见[修复记录](issues-82-88-implementation.md#第一批人工反馈修复2026-11-16)。Queen右Torch等此前切片历史仍在实施索引，不复制旧在制卡。

## 暂停的技术诊断

|卡片 / issue|当前事实|恢复条件与证据|
|---|---|---|
|T-beta-art-binding / #88|历史ENet地图绘制GCHandle fatal未定位；强持有实验已撤回，当前完整Check未复现，不签修复。|出现资源绑定重建/实际Dispose新证据再恢复；`.local/enet-art-binding-20261115/first-beta-fatal`，实验树`map-art-lifetime-20261115`。|
|P87-boss / #87|历史后台HP订阅偶发失败未定位，探针已撤回；后续通过不改签根因修复。|有区分假说的新失败/观测再恢复；保留`boss-warmed-callback-20261113`及主仓`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`。|

## 交付与人工验收边界

统一包：本树`dist/batch-6-integration/AllMatters-public/`与`AllMatters-public-beta/`，每目标仅五文件。DLL产品版本均`0.1.1+226c4e9879c50165c24913e31bde8f0f95154b98`，PCK/NOTICE/依赖说明已核；旧包备份在`.local/issue88-manual-feedback/previous-delivery/`。

自动宿主使用完整RitsuLib0.6.11；最新实机日志实际加载Workshop0.6.13并覆盖本地0.6.7，两个组合分开记证。Agent不启动游戏/Steam、不写真实mods或存档。

待实机：连续精英/事件入场及fight、旧卡局恢复、Boss奖励继续后跨幕、地图暗滤镜和改后队形/文字。已修诊断链不等于所有黑屏/闪退消失；日志另有BaseLib Continue清理异常与D3D12设备丢失，均未签修复。

批次4为#82/#83、批次5为#84–#87、批次6为#88；真实多端/设备/终局等责任仍见[验证地图](issues-82-88-verification-map.md)。#93/#94既有用户签收保留，#88未因本包自动关闭，未推送或正式发布。

调度继续遵循[批次流程](agents/batch-flow.md)：最多两片在制、唯一执行者、独立双轴、正常hooks，每次重跑有变化/新证据或既有交付要求。
