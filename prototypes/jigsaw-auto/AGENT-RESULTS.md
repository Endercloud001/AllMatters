# 自动实验与用户验收结果

搜索 tree-search-v2（增量几何/必要Boss邻块优先）；Python independent-verifier-v3（新房型规则）；事务原型 transaction-v1。Git提交提供源码追溯，不增加自定义hash或冻结接口。

最新独立验证器重新验收 9458 个成功记录，零不符。9,600次完整事务、400次旧见证构造、800次额外给定池事务分别保留完整gzip JSONL。失败均保留原池及指定原布局，清票回编辑；从未把UNKNOWN计为UNSAT。

## 完整事务耗时

每行100种子。计时包含输入复制、可用见证独立检查、Node往返、最终Python独立验收与替换；批量使用常驻Node进程。夹具载入/检索为一次性成本 25.49ms，进程创建调用为60.61ms；进程实际启动工作计入首条往返。scratch预检所给见证仅作统一记账，普通从零搜索不使用该见证。实时页面每次新建任务，其服务端/页面等待另记。

|池族|配置|方案|预算|有效|UNKNOWN|完整UNSAT|P50/P95 ms|去整体旋转布局数|返回原见证数|
|---|---|---|---|---|---|---|---|---|---|
|controlled|act1|witness|0|100|0|0|19.96 / 27.83|1|100|
|controlled|act1|extend|0|100|0|0|19.71 / 26.32|1|100|
|controlled|act1|scratch|0|0|100|0|10.48 / 13.85|0|0|
|controlled|act1|witness|100|100|0|0|19.44 / 25.37|1|100|
|controlled|act1|extend|100|100|0|0|43.53 / 54.82|100|0|
|controlled|act1|scratch|100|89|11|0|22.68 / 31.66|89|0|
|controlled|act1|witness|1000|100|0|0|19.69 / 25.74|1|100|
|controlled|act1|extend|1000|100|0|0|47.27 / 61.61|100|0|
|controlled|act1|scratch|1000|100|0|0|23.23 / 30.14|100|0|
|controlled|act1|witness|10000|100|0|0|20.00 / 26.32|1|100|
|controlled|act1|extend|10000|100|0|0|46.26 / 63.94|100|0|
|controlled|act1|scratch|10000|100|0|0|23.07 / 31.63|100|0|
|controlled|act2|witness|0|100|0|0|21.46 / 27.37|1|100|
|controlled|act2|extend|0|100|0|0|20.21 / 27.53|1|100|
|controlled|act2|scratch|0|0|100|0|10.91 / 14.18|0|0|
|controlled|act2|witness|100|100|0|0|20.57 / 25.18|1|100|
|controlled|act2|extend|100|100|0|0|44.26 / 57.38|100|0|
|controlled|act2|scratch|100|89|11|0|23.04 / 31.17|89|0|
|controlled|act2|witness|1000|100|0|0|20.82 / 26.74|1|100|
|controlled|act2|extend|1000|100|0|0|48.96 / 61.61|100|0|
|controlled|act2|scratch|1000|100|0|0|23.77 / 30.76|100|0|
|controlled|act2|witness|10000|100|0|0|21.42 / 26.32|1|100|
|controlled|act2|extend|10000|100|0|0|47.72 / 62.40|100|0|
|controlled|act2|scratch|10000|100|0|0|24.89 / 32.98|100|0|
|controlled|act3|witness|0|100|0|0|20.58 / 27.64|1|100|
|controlled|act3|extend|0|100|0|0|22.02 / 28.57|1|100|
|controlled|act3|scratch|0|0|100|0|11.20 / 14.64|0|0|
|controlled|act3|witness|100|100|0|0|21.09 / 27.53|1|100|
|controlled|act3|extend|100|100|0|0|45.29 / 59.66|100|0|
|controlled|act3|scratch|100|89|11|0|23.68 / 31.59|89|0|
|controlled|act3|witness|1000|100|0|0|21.22 / 27.43|1|100|
|controlled|act3|extend|1000|100|0|0|48.51 / 62.79|100|0|
|controlled|act3|scratch|1000|100|0|0|24.18 / 33.81|100|0|
|controlled|act3|witness|10000|100|0|0|21.09 / 27.18|1|100|
|controlled|act3|extend|10000|100|0|0|49.49 / 64.72|100|0|
|controlled|act3|scratch|10000|100|0|0|24.46 / 31.20|100|0|
|controlled|act3-double|witness|0|100|0|0|20.28 / 27.30|1|100|
|controlled|act3-double|extend|0|100|0|0|20.51 / 26.87|1|100|
|controlled|act3-double|scratch|0|0|100|0|10.85 / 15.42|0|0|
|controlled|act3-double|witness|100|100|0|0|19.46 / 26.19|1|100|
|controlled|act3-double|extend|100|100|0|0|45.38 / 55.72|100|0|
|controlled|act3-double|scratch|100|83|17|0|22.62 / 30.81|83|0|
|controlled|act3-double|witness|1000|100|0|0|21.03 / 26.17|1|100|
|controlled|act3-double|extend|1000|100|0|0|47.27 / 61.33|100|0|
|controlled|act3-double|scratch|1000|100|0|0|23.62 / 32.03|100|0|
|controlled|act3-double|witness|10000|100|0|0|20.50 / 26.49|1|100|
|controlled|act3-double|extend|10000|100|0|0|45.64 / 62.74|100|0|
|controlled|act3-double|scratch|10000|100|0|0|23.66 / 32.70|100|0|
|existing-demo|act1|witness|0|100|0|0|33.25 / 42.29|1|100|
|existing-demo|act1|extend|0|100|0|0|33.35 / 43.09|1|100|
|existing-demo|act1|scratch|0|0|100|0|15.49 / 21.89|0|0|
|existing-demo|act1|witness|100|100|0|0|34.64 / 42.88|1|100|
|existing-demo|act1|extend|100|100|0|0|49.71 / 62.12|100|0|
|existing-demo|act1|scratch|100|76|24|0|29.69 / 40.27|76|0|
|existing-demo|act1|witness|1000|100|0|0|34.96 / 43.10|1|100|
|existing-demo|act1|extend|1000|100|0|0|49.35 / 61.34|100|0|
|existing-demo|act1|scratch|1000|100|0|0|31.10 / 39.83|100|0|
|existing-demo|act1|witness|10000|100|0|0|33.62 / 41.84|1|100|
|existing-demo|act1|extend|10000|100|0|0|47.52 / 63.53|100|0|
|existing-demo|act1|scratch|10000|100|0|0|30.31 / 36.67|100|0|
|existing-demo|act2|witness|0|100|0|0|34.65 / 42.78|1|100|
|existing-demo|act2|extend|0|100|0|0|34.22 / 42.90|1|100|
|existing-demo|act2|scratch|0|0|100|0|15.71 / 21.30|0|0|
|existing-demo|act2|witness|100|100|0|0|34.55 / 43.76|1|100|
|existing-demo|act2|extend|100|100|0|0|51.17 / 65.32|100|0|
|existing-demo|act2|scratch|100|76|24|0|30.64 / 40.18|76|0|
|existing-demo|act2|witness|1000|100|0|0|35.88 / 43.12|1|100|
|existing-demo|act2|extend|1000|100|0|0|50.65 / 64.89|100|0|
|existing-demo|act2|scratch|1000|100|0|0|32.21 / 40.67|100|0|
|existing-demo|act2|witness|10000|100|0|0|34.23 / 42.11|1|100|
|existing-demo|act2|extend|10000|100|0|0|50.59 / 65.19|100|0|
|existing-demo|act2|scratch|10000|100|0|0|32.04 / 39.03|100|0|
|existing-demo|act3|witness|0|100|0|0|36.91 / 44.80|1|100|
|existing-demo|act3|extend|0|100|0|0|36.88 / 45.08|1|100|
|existing-demo|act3|scratch|0|0|100|0|16.40 / 21.67|0|0|
|existing-demo|act3|witness|100|100|0|0|35.20 / 44.79|1|100|
|existing-demo|act3|extend|100|100|0|0|50.10 / 64.57|100|0|
|existing-demo|act3|scratch|100|76|24|0|29.91 / 38.36|76|0|
|existing-demo|act3|witness|1000|100|0|0|35.02 / 45.61|1|100|
|existing-demo|act3|extend|1000|100|0|0|51.83 / 66.08|100|0|
|existing-demo|act3|scratch|1000|100|0|0|32.32 / 42.24|100|0|
|existing-demo|act3|witness|10000|100|0|0|35.85 / 43.78|1|100|
|existing-demo|act3|extend|10000|100|0|0|51.76 / 65.77|100|0|
|existing-demo|act3|scratch|10000|100|0|0|32.62 / 42.84|100|0|
|existing-demo|act3-double|witness|0|100|0|0|28.11 / 35.16|1|100|
|existing-demo|act3-double|extend|0|100|0|0|28.06 / 34.51|1|100|
|existing-demo|act3-double|scratch|0|0|100|0|13.45 / 17.64|0|0|
|existing-demo|act3-double|witness|100|100|0|0|27.85 / 33.55|1|100|
|existing-demo|act3-double|extend|100|100|0|0|49.80 / 62.97|100|0|
|existing-demo|act3-double|scratch|100|80|20|0|27.88 / 36.21|80|0|
|existing-demo|act3-double|witness|1000|100|0|0|27.45 / 35.54|1|100|
|existing-demo|act3-double|extend|1000|100|0|0|53.24 / 63.54|100|0|
|existing-demo|act3-double|scratch|1000|100|0|0|29.77 / 37.60|100|0|
|existing-demo|act3-double|witness|10000|100|0|0|27.76 / 34.95|1|100|
|existing-demo|act3-double|extend|10000|100|0|0|50.82 / 68.38|100|0|
|existing-demo|act3-double|scratch|10000|100|0|0|30.04 / 38.33|100|0|

“返回原见证”包括witness方案主动返回及扩展预算未增加用块时返回；不等同于异常发生次数。Python最终验收不通过的候选不发布。layout-summary.json另汇总用块/普通节点、Boss最短距离、房型与探索房型分布、休息覆盖、Boss邻块和P50/P95扩展数。重复布局按块/节点ID统计，另消除整体旋转，不据此声称均匀或视觉体验好。

## 研究装配器构造成本（新规则池）

旧研究演示assemble最多20次尝试，单独作为bootstrap计时，**不是服从本票扩展预算的正式生成器**；只适用于该独立给定测试池。生产候选仍须从造池时持有独立验收通过的见证，或在给定池预算内找到后才可发布。
- B=15 double=False: 100样本，100有效，完整构造及独立验收P50/P95 746.88/1182.38ms。
- B=14 double=False: 100样本，100有效，完整构造及独立验收P50/P95 743.29/1181.48ms。
- B=13 double=False: 100样本，100有效，完整构造及独立验收P50/P95 773.45/1268.07ms。
- B=13 double=True: 100样本，100有效，完整构造及独立验收P50/P95 396.62/1023.11ms。

## 额外给定池

100个固定种子先准备合法房型分配（配额/骨架/内部边不变），随后每池×四配置×预算0/1000从零搜索。原池在搜索开始后不再改动；这是100个额外输入池，不代表所有任意池。

|配置|预算|样本|有效|UNKNOWN|UNSAT|原池/失败布局受损|
|---|---|---|---|---|---|---|
|act1|0|100|0|100|0|0|
|act1|1000|100|100|0|0|0|
|act2|0|100|0|100|0|0|
|act2|1000|100|100|0|0|0|
|act3|0|100|0|100|0|0|
|act3|1000|100|100|0|0|0|
|act3-double|0|100|0|100|0|0|
|act3-double|1000|100|100|0|0|0|

## 当前规则及用户反馈

- 三项均在Boss替换前检查：每块≤1休息处；不许四节点房型相同；固定内部通路不能直接连接两个精英。仅骨架共边但无内部通路不因此禁止。手动与自动共用整池约束。
- 6个休息处分属6块；受控六块见证已独立验证。旧三休息块/四块见证及旧固定池不合格，legacy-room-inputs.json仅作拒绝对照；新给定测试池在输入准备时创建，运行时从不修复或重抽给定池。
- 用户对第2、3组（失败保留、等待/恢复）的通过反馈保留；修改后的地图布局和房型分布仍待用户观察。

## 验证与边界

- negative-results.json 101项；coverage-results.json 49项。后者明确区分完整布局、几何、拓扑、自动条件和探索阶段；阶段负例只证明该验收阶段，不能替代完整合法性。
- pipeline-check-results.json 60项事务检查；worker-recovery-results.json 两项真实worker中断后JSON重做；recovery-results.json 12项状态出口模拟。原生多人存档由相应验证票承担。
- live-browser-results.json 32项：24组配置/方案、预算零、异常、错误输出、实际活动进程中断恢复、请求失败、非法种子、旧给定池及旧页面缺失原池。编辑布局逐字段保持、输入控件锁定、实际等待幕、手机/桌面无溢出，无JS错误；不是用户体验认可。
- 已用Godot检查双版各216测试与构建通过（先前日志godot-check.log.gz），未启动游戏/Steam或改安装。本轮只修改一次性Python/JS/HTML，不新增正式生成器。
- 搜索只枚举碎块接入树，未完整穷尽附加跨边组合。找到且独立通过的解可靠；失败只能UNKNOWN。完整全域UNSAT为0，无最优性、均匀性、任意池必有解保证；无需为本票引入正式求解器。
- 内存为Node进程RSS采样，各组最大采样范围 61.2–67.9 MiB；不是Python+Node总峰值或单次搜索增量。原始记录保留采样值。
- 构造保证来自已验证受控见证；额外给定池的成功率与布局多样性仅有实验支持。全部自动成功含异常回退，都经过独立验收。现有扩展实验偏好继续增加可放碎块直至池用满或预算结束，这只是比较样本的搜索策略，不增加最少块数玩法规则。
- 不把任何毫秒值提升为玩法限制。布局、房型、多样性或等待是否可接受仍由用户体验决定；若不接受，回到grilling，不擅改房型配额、额度或最短Boss距离。

- 增量几何与全量几何48组对照的scene、status、扩展数一致。必要Boss邻块优先仅改变候选次序，不新增规则；最终独立Python仍验收全部成功结果。

用户已于2026-11-04确认新版布局更为合理并予以通过，允许收尾；第2/3组此前已通过。验收原文及边界见HITL-ACCEPTANCE.md。本票通过resolution交接，不关闭其他验证票、不将原型扩展为正式生成器。

当前规则的reproducibility.json记录72组重复运行，scene/status/expanded全部一致；时间与RSS不要求逐值相同。
