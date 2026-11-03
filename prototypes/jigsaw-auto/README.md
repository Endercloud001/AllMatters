# 自动拼图决策原型

Agent自动部分的最新结论见 [AGENT-RESULTS.md](AGENT-RESULTS.md)。本票仍等待用户体验，不发布resolution。源码与实验资产保留在独立分支 `prototype/jigsaw-auto-budget`，不实现正式生成器。

## 直接运行实时原型

在工作树根目录执行（Python3.10+、Node24，无求解器或Python第三方依赖）：

```powershell
python -X utf8 prototypes/jigsaw-auto/serve.py
```

打开 http://127.0.0.1:8769/ 。Ctrl+C停止；状态只在内存中，只有worker恢复实验会写清楚标记的PROTOTYPE临时JSON。服务仅绑定本机回环地址，不启动游戏/Steam、不改安装或mods。

实时流程为：保留原图→锁定输入→搜索→独立Python验收→成功后替换。失败保留本幕原池、原图和通路并清票回编辑。页面显示任务启动、独立验收在内的服务端耗时，以及浏览器实际等待。独立验收需要本机进程，因此实时版用上述一条启动命令；`review.html`仍可双击离线回放旧记录，但不作为实时搜索/等待的替代证据。

请比较三个场景：

1. 正常同池装配：在受控池/独立给定池、四幕配置、三方案、四预算中选择；重复生成时只换种子，不换池。比较用块、分布、多样性和等待。
2. 失败保留编辑：整体旋转当前布局；取消“已有同池见证”，选择预算0后生成；也可注入异常或错误输出，观察原图保留。
3. 执行中恢复：独立给定池、第三幕双Boss、从零搜索、预算10000（新版任务较快，执行中才可中断）；生成中点击中断并重做。使用原输入/种子重做，不重新抽池或降低条件。

没有路线播放；满额度探索顺序仅保存在实验记录中。阶段、休息覆盖、Boss邻块、用块/节点数和房型分布均可查看。这里的编辑仅模拟整体旋转，不取代已验证的完整手动编辑原型。

## 重现自动证据

```powershell
python -X utf8 prototypes/jigsaw-auto/refresh-rooms.py
node prototypes/jigsaw-auto/geometry-equivalence.cjs
node prototypes/jigsaw-auto/reproduce.cjs
node prototypes/jigsaw-auto/pick-slow-case.cjs
python -X utf8 prototypes/jigsaw-auto/negative.py
python -X utf8 prototypes/jigsaw-auto/coverage.py
python -X utf8 prototypes/jigsaw-auto/pipeline-check.py
node prototypes/jigsaw-auto/worker-recovery.cjs
node prototypes/jigsaw-auto/recovery.cjs
python -X utf8 prototypes/jigsaw-auto/measure-pipeline.py
python -X utf8 prototypes/jigsaw-auto/stress-pools.py
python -X utf8 prototypes/jigsaw-auto/finish-report.py
python -X utf8 prototypes/jigsaw-auto/build-live.py
```

浏览器实测：保持serve.py运行，执行 `node prototypes/jigsaw-auto/live-browser-check.cjs`。可通过PLAYWRIGHT_MODULE/CHROME_PATH指定本机工具位置；它们只用于验收，不是运行原型的依赖。生成desktop/mobile/waiting截图及32项JSON记录。

`measure-pipeline.py`需要数分钟：两个固定池族×四配置×三方案×四预算×100种子，共9600次完整事务；另测400次旧见证构造。`stress-pools.py`准备100个额外房型分配作为既有输入，然后固定原池，四配置×两预算，共800事务。`finish-report.py`用最新独立验证器重验这些文件内的全部成功记录，再生成汇总。

完整原始记录为 pipeline-raw.jsonl.gz、bootstrap-timing.jsonl.gz、stress-pools.jsonl.gz。每条包含原池/状态、结果、探索顺序及房型、休息块覆盖、Boss邻块、扩展数、耗时拆分和Node RSS采样。失败状态同样保留，可检查没有换池/改房型。pipeline-summary.json、stress-summary.json及AGENT-RESULTS.md提供汇总。

旧版9,600记录和RESULTS.md保留作为历史比较；其旧witness时间0代表未测，不能作为最新完整等待结论。最新计时已经补入独立验收和旧见证构造的实际测量，不把毫秒测量变为玩法约束。

## 规则与证据对应

|要求|手动|自动含保底/恢复|最新证据|
|---|---|---|---|
|全局一格留白含对角、无边、异块|必须|必须|geometry_stage、7个边界及完整布局|
|固定旋转/骨架/内部边、端口、交叉/重合/穿点、跨块两条上限|必须|必须|完整旋转/镜像、pool检查和geometry阶段针对性用例|
|Boss终端、普通区不穿Boss、限步|必须|必须|topology阶段及完整布局对照|
|B−1个不同非Boss节点后第B步进单/第一Boss|不加强|必须|每次成功重新构造并逐步模拟顺序，各幕阈值上下界、重访/未放节点/第二Boss负例|
|原池全部休息块整块纳入|不加强|必须|原池集合差检查、多休息节点块及只取节点负例|
|Boss块直连先古时另接两个不同直接邻块|不加强|必须|一邻块/同块两线/重复线/间接/未声明线/非直连免触发及完整布局正例|
|预算零/耗尽、异常、错误输出均不得回退旧见证|按手动原规则|必须|60项完整事务及真实worker/页面进程中断重做|

negative-results.json为101项完整布局/探索对照；coverage-results.json为49项针对性阶段对照。新增记录标注验收阶段，**阶段负例只证明该阶段**，不伪称已满足其余几何/拓扑前提；完整布局正反对照和全量成功重验另外执行。生成器未导入Python验证器；Python不导入旧JS几何或研究Python图算法。

## 保证与已知边界

- 两个新版输入池均将6个休息处分布在6块。受控池持有已验证的六块构造见证，独立给定池持有该输入的一份已验证见证。后者是特定输入证据，不是任意池的构造证明。
- 100个额外池仅改变输入准备阶段的房型分配，保持配额、骨架和内部边；它们进一步检验无见证时的有限搜索/失败恢复，不代表所有任意池。
- 搜索是确定性有界接入树回溯（v2：必要Boss邻块优先、增量几何；48组全量/增量对照结果及扩展数一致），不完整枚举所有附加跨边。找到且独立验证通过的解可用；未找到只报UNKNOWN。完整全域穷尽UNSAT为0，不宣称最优、均匀、多样性必好或任意池必有解。本票不需要因此实现正式求解器。
- 当前池族固定Boss为I1末点/末两点，不覆盖所有合法Boss骨架。扩展样本偏向继续增加可放块，直到用满或预算结束；这是实验搜索策略，不新增最少块数或其他玩法条件。
- 旧演示bootstrap最多20次尝试，已单独计时，但不是服从确定性扩展预算的正式构造器。运行时候选不依赖它无限重抽；正常实时流程使用随池持有、独立验收过的见证。
- 内存是Node进程RSS采样，不是Python+Node总峰值或单次增量。批量计时是常驻进程；页面每次新建进程，二者分别记录。运行环境未隔离CPU负载，数值不作延迟承诺。
- 已验证真实Node worker/进程中断和临时JSON重做；不证明Godot后台任务、原生过场、真实多人保存或网络。后者按对应验证票边界处理。
- Godot资源、public-beta/public各216项测试、双版本构建已通过，日志godot-check.log.gz；本轮仅修改一次性原型，不改正式src。最终提交的推送钩子结果另外记录。

只有布局、房型、多样性和等待经用户体验后，本票才有资格发布resolution；若体验不接受，回到grilling，不擅改最短路线下限、用块数或配额。当前不关闭本票或其他验证票，不更新父地图Decisions so far。

## 用户体验后的房型规则修订

替换Boss前，每块至多1个休息处；不能4节点同类；不能由固定内部通路直接连接两个精英。骨架共边但无通路不在第三项限制内。整池手动/自动共用，原配额保持。规则来源为用户明确确认；旧池和旧见证保留在legacy-room-inputs.json作拒绝对照，绝不在运行时修复或换池。新池只在显式refresh-rooms.py输入准备阶段创建。

第2、3组体验已由用户通过；本轮自动回归仍会运行，新布局待观察。不会因为重跑自动实验而撤销既有通过反馈。历史统计和离线页已标注旧规则，不作为当前成功/保底。

统计以顶层独立验收返回的metrics为准；layout中研究示例附带的normal/distance/metrics是UI缓存，不作为验收或汇总输入，页面显示时用独立统计覆盖。
