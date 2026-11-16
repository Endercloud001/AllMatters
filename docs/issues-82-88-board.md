# #82–#88 当前调度看板

更新：2026-11-17。集成树：`AllMatters/.claude/worktrees/hunting-20261112`；分支：`integration/issues-82-88-20261112`；当前源码：`6dbb686`。仅集成主控维护。

## 当前工作：#88 兼容验证与统一交付

**用户确认 #82–#88 的多端、多人等测试项目和各票验收条件均通过。** 第六轮、绘画工具栏位置及画笔落点已有实机确认。绘画、保存和地图 UI 双版各26项受影响回归严格通过。

## 人工已通过

第四批：标准与恐惧模式准入、羽翼之靴、黄金罗盘、皮草大衣、“细看”文案及第二幕标记宝箱的藏宝图金币和移卡通过。第四批退出和地图故障单独跟踪。第三批：五虫、暗港/荣耀普通站位、指南其余操作序列和Mystic塞牌逻辑通过。首两批已通过场景见[验收指南](batch-6-test-guide.md#第一批人工反馈2026-11-15)。

## 当前卡片

|卡片|状态 / 源码|已证与剩余|
|---|---|---|
|M88-2-flow|Integrated / `b3f0a49`|TestSubject/Knowledge合法预载guard、绑定效果、真实fight→新战斗→win与原Crab恢复双版strict；0.6.14窄核通过，旧黑屏跑局待实机。|
|M88-2-rules|Hive Integrated / `b3f0a49`|同entry确认后继续Weak/Frail/Vulnerable各1，双版pending确认及stale回归通过。|
|M88-2-glory|Integrated / `a464e66`|独立原生采样red→green，荣耀普通12+精英3双版strict；复用暗港算法，本体未改。|
|M88-2-torch|Integrated / `14c9351`；低优先级|生产工厂附绿焰，双版真实GL图已看；无原点残留，不改Queen规则/IntentPos，正式召唤外观待实机。|
|M88-3-marks|Integrated / `34cd003`|第二幕共享藏宝图、全队一次皮草，定稿/入房/召唤/保存恢复及原生素材。|
|M88-3-mode|Integrated / `6f8d463`|Standard+Dread准入，靴子/罗盘当幕共享+3不叠加，保留原生非拼图行为。|
|M88-3-presentation|Integrated / `34cd003`|Mystic塞牌提示及区别治疗的原动作、细看文案，清除临时诊断。|
|M88-4-chest|集成 / `f25de54`|双版实际跳过→地图→重载及待选遗物时退出各2 strict。|
|M88-4-mystic|集成 / 同上|挥杖与原版夜魇特效，保留弃牌预览。双版行为各8通过。|
|M73-4-museum|集成 / 同上|仅 beta，两个选项宿主测试通过。[规格评论](https://github.com/Endercloud001/AllMatters/issues/73#issuecomment-6914847082)|
|M88-6-ui / Peck|实机通过 / `f4ea117`|用户确认上述问题均修复。|
|地图绘画|集成 / `fbc47fc`|复用原生画笔、擦除、清空及保存；public宿主导出含笔迹全景PNG。|

第二批原始失败、通过清单与复测操作见[验收指南](batch-6-test-guide.md#第二批人工反馈2026-11-16)；独立审查及精确归档见[第二批实施](issues-82-88-implementation.md#第二批反馈修复2026-11-16)。

## 第一批修复保留

`226c4e9`：未绑定怪物预载guard、Boss特殊节点与旧图迁移、地图Opened/Closed配对、移除入场对话；暗港原生同数量中心、五虫地面0.8缩放、Waterfall爆发后清负面+Artifact3、光门文案。完整Check/双轴审查/hooks通过，原实机出口仍按[复测顺序](batch-6-test-guide.md#本轮修复包复测顺序)保留。

## 其他暂停诊断

|卡片|当前事实|恢复条件 / 证据|
|---|---|---|
|T-beta-art-binding / #88|历史ENet地图GCHandle fatal未定位，实验已撤，不签修复。|资源绑定/Dispose新证据；`.local/enet-art-binding-20261115/first-beta-fatal`，树`map-art-lifetime-20261115`。|
|P87-boss / #87|后台HP订阅间歇失败未定位，探针已撤。|新失败/可区分观测；`boss-warmed-callback-20261113`及主仓`.local/main-control-20261113-resume/boss-warmed-callback-current-diagnosis.md`。|

## 交付与验收边界

统一包在本树`dist/batch-6-integration/AllMatters-public/`与`AllMatters-public-beta/`，每目标五文件；DLL均`0.1.1+6dbb6861e411c0ebc80e6944b886ef449e460351`，逐文件核对通过。上一轮包备份在`.local/issue88-drawing-offset/previous-delivery/`。

第五轮用户确认上一轮问题均通过，第四轮宝箱存退/放弃/地图继续、Mystic呈现和博物馆回报据此更新。保留反馈原有版本/人数范围。面具新尺寸及红字待新包实机查看。

用户最终验收见[验收指南](batch-6-test-guide.md#最终人工验收2026-11-17)，#93/#94既有签收保留。此前分场景证据及历史诊断见[验证地图](issues-82-88-verification-map.md)。发布包仍以 GitHub Releases 为准。调度遵循[批次流程](agents/batch-flow.md)：最多两片在制、唯一执行者、独立双轴、正常hooks。
