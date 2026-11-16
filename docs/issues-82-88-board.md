# #82–#88 当前调度看板

更新：2026-11-16。集成树：`AllMatters/.claude/worktrees/hunting-20261112`；分支：`integration/issues-82-88-20261112`；当前源码：`3f20593`。仅集成主控维护。

## 当前工作：#88 兼容验证与统一交付

**第三批双版交付已完成。** 第三批通过项已记录，技术诊断暂停2，#88尚待新行为实机复测。

|目标|第三批完整验证 / 实际归档（本树`.local/tests/archives/`）|Build / Host|Engine / Metadata|
|---|---|---|---|
|public-beta 0.113.1|680通过；`run-20261116-165328286-a2a9c25d4cad4b72afd76bf39774fa21`|0 / 0|26 / 0|
|public 0.107.1|682通过；`run-20261116-175745206-5569e9f4fad4477ba22e36c791bf96da`|0 / 0|26 / 0|

同一提交3f20593完成两版全量及构建。原public进程中断后独立重跑完整public检查，通过后构建beta并更新两版包。控制台位于 `.local/issue88-manual3/`，详见[实施索引](issues-82-88-implementation.md#第三批反馈实施2026-11-16)。

## 人工已通过

第三批：五虫、暗港/荣耀普通站位均符合预期；指南其余操作序列通过，Mystic塞牌逻辑正确、呈现待改。第一批：追猎商店/休息/宝箱；暗港三精英与乐加维林族母/灵魂异鱼；密林普通站位、三精英/三Boss。第二批：暗港普通站位；帝皇蟹/无厌沙虫；荣耀三精英；女王/永世沙漏Boss逻辑。保留用户实际反馈范围，不扩签双版、多人或完整终局。

## 当前卡片（第二批已集成，第三批在制）

|卡片|状态 / 源码|已证与剩余|
|---|---|---|
|M88-2-flow|Integrated / `b3f0a49`|TestSubject/Knowledge合法预载guard、绑定效果、真实fight→新战斗→win与原Crab恢复双版strict；0.6.14窄核通过，旧黑屏跑局待实机。|
|M88-2-rules|Hive Integrated / `b3f0a49`|同entry确认后继续Weak/Frail/Vulnerable各1，双版pending确认及stale回归通过。|
|M88-2-glory|Integrated / `a464e66`|独立原生采样red→green，荣耀普通12+精英3双版strict；复用暗港算法，本体未改。|
|M88-2-torch|Integrated / `14c9351`；低优先级|生产工厂附绿焰，双版真实GL图已看；无原点残留，不改Queen规则/IntentPos，正式召唤外观待实机。|
|M88-3-marks|Integrated / `34cd003`|第二幕共享藏宝图、全队一次皮草，定稿/入房/召唤/保存恢复及原生素材。|
|M88-3-mode|Integrated / `6f8d463`|Standard+Dread准入，靴子/罗盘当幕共享+3不叠加，保留原生非拼图行为。|
|M88-3-presentation|Integrated / `34cd003`|Mystic塞牌提示及区别治疗的原动作、细看文案，清除临时诊断。|

第二批原始失败、通过清单与复测操作见[验收指南](batch-6-test-guide.md#第二批人工反馈2026-11-16)；独立审查及精确归档见[第二批实施](issues-82-88-implementation.md#第二批反馈修复2026-11-16)。

## 第一批修复保留

`226c4e9`：未绑定怪物预载guard、Boss特殊节点与旧图迁移、地图Opened/Closed配对、移除入场对话；暗港原生同数量中心、五虫地面0.8缩放、Waterfall爆发后清负面+Artifact3、光门文案。完整Check/双轴审查/hooks通过，原实机出口仍按[复测顺序](batch-6-test-guide.md#本轮修复包复测顺序)保留。

## 其他暂停诊断

|卡片|当前事实|恢复条件 / 证据|
|---|---|---|
|T-beta-art-binding / #88|历史ENet地图GCHandle fatal未定位，实验已撤，不签修复。|资源绑定/Dispose新证据；`.local/enet-art-binding-20261115/first-beta-fatal`，树`map-art-lifetime-20261115`。|
|P87-boss / #87|后台HP订阅间歇失败未定位，探针已撤。|新失败/可区分观测；`boss-warmed-callback-20261113`及主仓`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`。|

## 交付与验收边界

统一包在本树`dist/batch-6-integration/AllMatters-public/`与`AllMatters-public-beta/`，每目标仅五文件；DLL均`0.1.1+3f2059377a9281b0d0bbadff4658f0f429c1077d`，PCK/许可/manifest已核。第二批包保存在`.local/issue88-manual3/previous-delivery/`，更早包保留原处。

完整自动宿主使用RitsuLib0.6.11；第二批实机为Workshop0.6.14，并另做相关0.6.14窄核。Agent不启动游戏/Steam、不写真实mods或存档。第三批回报保留已通过的既有操作序列；新模式、步数与标记、施法提示和前冲仍待新包实机。BaseLib Continue清理异常及D3D12设备丢失未签修复。

真实多端/设备/终局责任见[验证地图](issues-82-88-verification-map.md)。#93/#94既有签收保留，#88未自动关闭；未推送或正式发布。调度遵循[批次流程](agents/batch-flow.md)：最多两片在制、唯一执行者、独立双轴、正常hooks。
