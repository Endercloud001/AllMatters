# Agent自动实验结果：等待用户体验

搜索 tree-search-v1；Python independent-verifier-v2；事务原型 transaction-v1。Git提交提供源码追溯，不增加自定义hash或冻结接口。

最新独立验证器重新验收 8974 个成功记录，零不符。9,600次完整事务、400次旧见证构造、800次额外给定池事务分别保留完整gzip JSONL。失败均保留原池及指定原布局，清票回编辑；从未把UNKNOWN计为UNSAT。

## 完整事务耗时

每行100种子。计时包含输入复制、可用见证独立检查、Node往返、最终Python独立验收与替换；批量使用常驻Node进程。夹具载入/检索为一次性成本 235.68ms，进程创建调用为28.00ms；进程实际启动工作计入首条往返。scratch预检所给见证仅作统一记账，普通从零搜索不使用该见证。实时页面每次新建任务，其服务端/页面等待另记。

|池族|配置|方案|预算|有效|UNKNOWN|完整UNSAT|P50/P95 ms|返回原见证数|
|---|---|---|---|---|---|---|---|---|
|controlled|act1|witness|0|100|0|0|3.00 / 4.40|100|
|controlled|act1|extend|0|100|0|0|3.04 / 4.17|100|
|controlled|act1|scratch|0|0|100|0|1.59 / 2.34|0|
|controlled|act1|witness|100|100|0|0|2.86 / 4.32|100|
|controlled|act1|extend|100|100|0|0|27.19 / 42.76|0|
|controlled|act1|scratch|100|59|41|0|10.27 / 27.24|0|
|controlled|act1|witness|1000|100|0|0|3.07 / 4.07|100|
|controlled|act1|extend|1000|100|0|0|51.86 / 85.85|0|
|controlled|act1|scratch|1000|87|13|0|12.37 / 571.49|0|
|controlled|act1|witness|10000|100|0|0|3.03 / 4.35|100|
|controlled|act1|extend|10000|100|0|0|52.10 / 83.48|0|
|controlled|act1|scratch|10000|95|5|0|11.08 / 1726.15|0|
|controlled|act2|witness|0|100|0|0|3.21 / 4.55|100|
|controlled|act2|extend|0|100|0|0|2.97 / 4.31|100|
|controlled|act2|scratch|0|0|100|0|1.71 / 2.35|0|
|controlled|act2|witness|100|100|0|0|3.10 / 4.07|100|
|controlled|act2|extend|100|100|0|0|28.84 / 43.84|0|
|controlled|act2|scratch|100|59|41|0|10.71 / 28.28|0|
|controlled|act2|witness|1000|100|0|0|3.10 / 4.42|100|
|controlled|act2|extend|1000|100|0|0|53.38 / 91.72|0|
|controlled|act2|scratch|1000|87|13|0|10.87 / 583.78|0|
|controlled|act2|witness|10000|100|0|0|3.09 / 4.76|100|
|controlled|act2|extend|10000|100|0|0|54.14 / 82.73|0|
|controlled|act2|scratch|10000|95|5|0|10.62 / 1556.02|0|
|controlled|act3|witness|0|100|0|0|3.03 / 4.31|100|
|controlled|act3|extend|0|100|0|0|2.86 / 4.28|100|
|controlled|act3|scratch|0|0|100|0|1.61 / 2.34|0|
|controlled|act3|witness|100|100|0|0|2.91 / 3.83|100|
|controlled|act3|extend|100|100|0|0|27.87 / 43.86|0|
|controlled|act3|scratch|100|59|41|0|9.96 / 30.98|0|
|controlled|act3|witness|1000|100|0|0|2.99 / 4.10|100|
|controlled|act3|extend|1000|100|0|0|54.77 / 77.48|0|
|controlled|act3|scratch|1000|87|13|0|10.22 / 537.90|0|
|controlled|act3|witness|10000|100|0|0|2.98 / 4.24|100|
|controlled|act3|extend|10000|100|0|0|52.57 / 78.97|0|
|controlled|act3|scratch|10000|95|5|0|10.66 / 1608.70|0|
|controlled|act3-double|witness|0|100|0|0|2.87 / 4.03|100|
|controlled|act3-double|extend|0|100|0|0|2.93 / 3.72|100|
|controlled|act3-double|scratch|0|0|100|0|1.55 / 2.26|0|
|controlled|act3-double|witness|100|100|0|0|2.87 / 3.84|100|
|controlled|act3-double|extend|100|100|0|0|27.32 / 42.68|0|
|controlled|act3-double|scratch|100|50|50|0|12.76 / 29.15|0|
|controlled|act3-double|witness|1000|100|0|0|3.03 / 4.14|100|
|controlled|act3-double|extend|1000|100|0|0|54.35 / 78.68|0|
|controlled|act3-double|scratch|1000|74|26|0|22.09 / 555.87|0|
|controlled|act3-double|witness|10000|100|0|0|2.97 / 3.91|100|
|controlled|act3-double|extend|10000|100|0|0|53.69 / 81.67|0|
|controlled|act3-double|scratch|10000|84|16|0|21.98 / 6246.97|0|
|existing-demo|act1|witness|0|100|0|0|6.02 / 7.52|100|
|existing-demo|act1|extend|0|100|0|0|5.86 / 7.35|100|
|existing-demo|act1|scratch|0|0|100|0|2.84 / 3.84|0|
|existing-demo|act1|witness|100|100|0|0|5.89 / 7.80|100|
|existing-demo|act1|extend|100|100|0|0|38.12 / 53.21|0|
|existing-demo|act1|scratch|100|62|38|0|11.05 / 29.72|0|
|existing-demo|act1|witness|1000|100|0|0|6.10 / 7.97|100|
|existing-demo|act1|extend|1000|100|0|0|37.05 / 63.72|0|
|existing-demo|act1|scratch|1000|85|15|0|11.63 / 579.63|0|
|existing-demo|act1|witness|10000|100|0|0|5.89 / 7.59|100|
|existing-demo|act1|extend|10000|100|0|0|38.25 / 63.08|0|
|existing-demo|act1|scratch|10000|90|10|0|12.28 / 6048.50|0|
|existing-demo|act2|witness|0|100|0|0|5.96 / 7.69|100|
|existing-demo|act2|extend|0|100|0|0|5.75 / 7.00|100|
|existing-demo|act2|scratch|0|0|100|0|2.56 / 3.33|0|
|existing-demo|act2|witness|100|100|0|0|5.67 / 7.67|100|
|existing-demo|act2|extend|100|100|0|0|34.77 / 52.24|0|
|existing-demo|act2|scratch|100|62|38|0|11.54 / 30.27|0|
|existing-demo|act2|witness|1000|100|0|0|5.77 / 7.50|100|
|existing-demo|act2|extend|1000|100|0|0|36.01 / 61.83|0|
|existing-demo|act2|scratch|1000|85|15|0|11.39 / 574.05|0|
|existing-demo|act2|witness|10000|100|0|0|5.93 / 7.92|100|
|existing-demo|act2|extend|10000|100|0|0|35.66 / 63.07|0|
|existing-demo|act2|scratch|10000|90|10|0|12.27 / 5841.56|0|
|existing-demo|act3|witness|0|100|0|0|5.95 / 8.34|100|
|existing-demo|act3|extend|0|100|0|0|5.89 / 7.96|100|
|existing-demo|act3|scratch|0|0|100|0|2.79 / 4.17|0|
|existing-demo|act3|witness|100|100|0|0|5.89 / 7.67|100|
|existing-demo|act3|extend|100|100|0|0|35.66 / 52.56|0|
|existing-demo|act3|scratch|100|62|38|0|10.97 / 27.99|0|
|existing-demo|act3|witness|1000|100|0|0|6.11 / 8.02|100|
|existing-demo|act3|extend|1000|100|0|0|38.89 / 62.45|0|
|existing-demo|act3|scratch|1000|85|15|0|12.32 / 580.83|0|
|existing-demo|act3|witness|10000|100|0|0|5.98 / 7.51|100|
|existing-demo|act3|extend|10000|100|0|0|38.32 / 60.41|0|
|existing-demo|act3|scratch|10000|90|10|0|13.00 / 5834.00|0|
|existing-demo|act3-double|witness|0|100|0|0|7.84 / 10.46|100|
|existing-demo|act3-double|extend|0|100|0|0|7.61 / 10.62|100|
|existing-demo|act3-double|scratch|0|0|100|0|3.38 / 5.19|0|
|existing-demo|act3-double|witness|100|100|0|0|7.61 / 10.19|100|
|existing-demo|act3-double|extend|100|100|0|0|30.31 / 50.18|0|
|existing-demo|act3-double|scratch|100|47|53|0|16.08 / 32.80|0|
|existing-demo|act3-double|witness|1000|100|0|0|7.56 / 10.44|100|
|existing-demo|act3-double|extend|1000|100|0|0|29.76 / 53.08|0|
|existing-demo|act3-double|scratch|1000|72|28|0|41.04 / 644.78|0|
|existing-demo|act3-double|witness|10000|100|0|0|7.75 / 10.26|100|
|existing-demo|act3-double|extend|10000|100|0|0|30.87 / 52.95|0|
|existing-demo|act3-double|scratch|10000|80|20|0|39.00 / 6779.58|0|

“返回原见证”包括witness方案主动返回及扩展预算未增加用块时返回；不等同于异常发生次数。Python最终验收不通过的候选不发布。

## 旧见证构造成本（补测）

旧研究演示assemble最多20次尝试，单独作为bootstrap计时，**不是服从本票扩展预算的正式生成器**；只适用于该既有演示池。生产候选仍须从造池时持有独立验收通过的见证，或在给定池预算内找到后才可发布。
- B=15 double=False: 100样本，100有效，完整构造及独立验收P50/P95 225.24/326.69ms。
- B=14 double=False: 100样本，100有效，完整构造及独立验收P50/P95 223.61/331.67ms。
- B=13 double=False: 100样本，100有效，完整构造及独立验收P50/P95 218.30/315.12ms。
- B=13 double=True: 100样本，100有效，完整构造及独立验收P50/P95 214.82/328.84ms。

## 额外给定池

100个固定种子先准备合法房型分配（配额/骨架/内部边不变），随后每池×四配置×预算0/1000从零搜索。原池在搜索开始后不再改动；这是100个额外输入池，不代表所有任意池。

|配置|预算|样本|有效|UNKNOWN|UNSAT|原池/失败布局受损|
|---|---|---|---|---|---|---|
|act1|0|100|0|100|0|0|
|act1|1000|100|86|14|0|0|
|act2|0|100|0|100|0|0|
|act2|1000|100|86|14|0|0|
|act3|0|100|0|100|0|0|
|act3|1000|100|86|14|0|0|
|act3-double|0|100|0|100|0|0|
|act3-double|1000|100|75|25|0|0|

## 验证与边界

- negative-results.json 57项；coverage-results.json 57项。后者明确区分完整布局、几何、拓扑、自动条件和探索阶段；阶段负例只证明该验收阶段，不能替代完整合法性。
- pipeline-check-results.json 50项事务检查；worker-recovery-results.json 两项真实worker中断后JSON重做；recovery-results.json 12项状态出口模拟。原生多人存档由相应验证票承担。
- live-browser-results.json 30项：24组配置/方案、预算零、异常、错误输出、实际活动进程中断恢复、请求失败及非法种子。编辑布局逐字段保持、输入控件锁定、实际等待幕、手机/桌面无溢出，无JS错误；不是用户体验认可。
- 已用Godot检查双版各216测试与构建通过（先前日志godot-check.log.gz），未启动游戏/Steam或改安装。本轮只修改一次性Python/JS/HTML，不新增正式生成器。
- 搜索只枚举碎块接入树，未完整穷尽附加跨边组合。找到且独立通过的解可靠；失败只能UNKNOWN。完整全域UNSAT为0，无最优性、均匀性、任意池必有解保证；无需为本票引入正式求解器。
- 内存为Node进程RSS采样，各组最大采样范围 79.3–118.3 MiB；不是Python+Node总峰值或单次搜索增量。原始记录保留采样值。
- 构造保证来自已验证受控见证；额外给定池的成功率与布局多样性仅有实验支持。全部自动成功含异常回退，都经过独立验收。现有扩展实验偏好继续增加可放碎块直至池用满或预算结束，这只是比较样本的搜索策略，不增加最少块数玩法规则。
- 不把任何毫秒值提升为玩法限制。布局、房型、多样性或等待是否可接受仍由用户体验决定；若不接受，回到grilling，不擅改房型配额、额度或最短Boss距离。

本票继续开放；不发布resolution、不解除集成票依赖、不关闭其他验证票。下一步仅需用户体验与反馈，以及由反馈触发的必要修订。
