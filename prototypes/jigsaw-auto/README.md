# 自动拼图决策原型（一次性代码，票保持开放）

本原型回答构造见证、有界扩展和给定池从零搜索在当前条件下的可用范围；不实现正式生成器。来源为研究分支 f6e7a94 的最新坐标证明及本机研究地图页面；手动交互原型已检查但不复制其编辑状态机。实验分支 `prototype/jigsaw-auto-budget`，独立工作树 `AllMatters_prototype69`，不触碰原工作区修改。

## 运行

在此工作树根目录，Node 24.11.1、Python 3.10.8，无第三方求解器或游戏依赖：

```powershell
node prototypes/jigsaw-auto/extract.cjs
python -X utf8 prototypes/jigsaw-auto/controlled.py
python -X utf8 prototypes/jigsaw-auto/negative.py
node prototypes/jigsaw-auto/search.cjs
node prototypes/jigsaw-auto/run-controlled.cjs
python -X utf8 prototypes/jigsaw-auto/report.py
python -X utf8 prototypes/jigsaw-auto/report.py --controlled
node prototypes/jigsaw-auto/recovery.cjs
python -X utf8 prototypes/jigsaw-auto/build-review.py
```

`review.html` 双击离线打开。保留原池卡、原版美术、缩放、休息处高亮，不恢复路线播放。比较页是少量独立通过样本的记录回放，同池按钮切换五种种子结果；等待按钮回放记录耗时，不代表原生过场或实时生成。原 `prototype.html` 是保留的研究演示，可实时重新装配，但其旧装配器没有本票的扩展预算语义，不作为新预算实验入口。

## 规则与证据

|条件|手动|自动含保底/恢复|证据|
|---|---|---|---|
|全局实际占地留白含对角|必须|必须|独立Python几何与7边界|
|整数网格、固定碎块/内部边、端口/通路、两条上限|必须|必须|原池重建、独立逐格检查|
|Boss终端、普通区域不穿Boss、限步|必须|必须|独立图检查|
|B−1个不同非Boss节点后第B步第一Boss|不加强|必须|独立顺序构造并逐步模拟|
|全部含休息处碎块整块使用|不加强|必须|原池集合与实际用块集合|
|Boss块直连先古则另有两个不同直接邻块|不加强|必须|显式跨边按块去重|
|失败保留原图/池/边并清票|手动另有撤回语义|必须|内存/JSON出口模拟|

不增加最短Boss距离下限、最少块数或房型配额新条件。原池先分配房型再替换Boss；当前原型明确只覆盖Boss在I1末端（单Boss）或末两点（双Boss）的池族，不覆盖所有可合法选Boss的骨架。

## 实验设计与数据

两个池族分别运行：受控池将6休息处分配在3个必用块；既有池保持原研究页面全部房型，6休息处分布5块。都不是“所有任意池”的随机样本。每池族每配置每方案每预算固定种子1..100；四配置为单Boss幕1/B15、幕2/B14、幕3/B13及双Boss幕3/B13。方案3种、预算0/100/1000/10000候选扩展，每池族4,800条记录，总9,600条。种子改变候选顺序，不重抽给定池。

`raw.jsonl`/`controlled-raw.jsonl` 是生成器原始输出；`checked.jsonl`/`controlled-checked.jsonl` 增加Python独立验收、探索顺序及房型记录；`summary.json`/`controlled-summary.json` 汇总每组样本、SAT/UNKNOWN/UNSAT、P50/P95、用块、距离、重复布局及保底数。重复布局同时统计精确坐标和消除整体旋转后的数量，不宣称均匀随机。内存是采样时整个Node进程RSS，不是单次搜索增量或真正峰值。两族实验本次并发运行，耗时受CPU竞争影响，不作延迟承诺。

研究旧装配器生成既有池见证的耗时未计入原始witness记录（ms=0是未测占位，**不得解读为瞬时生成**）；extend记录也不含这项预处理。受控见证来自显式构造，witness计时仅包含复制与整体旋转。比较应分别说明已持有见证和需要先求见证的成本。

候选扩展计数包含几何拒绝；候选顺序由自带xorshift、稳定数组顺序决定。搜索直接复用研究几何作剪枝，但其成功标记仅是候选；只有独立Python通过才计有效解。验证器不导入JS或研究Python图算法。原始结果、计数与布局可复现；墙钟和RSS不可逐值复现。`reproducibility.json`记录36组重复运行的scene/status/expanded一致性。

## 保证与边界

- 受控池初始见证经过独立验证，构造保证仅对该受控族成立。用给定池时绝不改房型或重抽池。
- 从零搜索当前枚举连通接入树，**未完整枚举附加跨边组合**，因此只支持找到解或UNKNOWN；没有任何完整全域UNSAT证书。不能据此给任意池判无解。该限制也是待补的关键实验，不能将此版本当作已经完成的完整回溯比较。
- 预算内有已验证见证才允许成功回退。没有合格候选按自动失败保留原图/池/边，不退回旧单块或旧四块构造。JS原型在线部分尚未接入Python拒绝结果的运行时回传，交付回放仅选独立通过样本。
- 47项正负对照与12项恢复出口模拟，不等于本票完整负例清单。商店重访充数、双Boss误计、跨Boss区域、重复边冒充邻块等仍须补充针对性拒绝阶段证据；不能用其他检查提前拒绝来声称覆盖对应条件。
- 恢复模拟不接原生网络、实际多人保存、Godot过场；异常案例是失败出口注入，不是实际线程执行中断恢复。
- 用户尚未参与本原型体验。完整负例、搜索完备边界及体验仍是开放工作；当前不能发布resolution或关闭本票。

机器：Windows / AMD Ryzen 9 7945HX；Node v24.11.1、Python3.10.8。实验不执行第三方构建、不启动游戏/Steam、不复制mods、不新增hash/contract/baseline/gate。

补充计时边界：P50/P95是Node候选搜索时间，不含离线Python独立验收，亦非完整交互等待。浏览器72项选择切换无JS错误，桌面与390px手机无横向溢出；截图及browser-results.json仅证明页面运行情况，不替代用户体验。
