# #84 共享完整战斗检查点

2026-11-12。实现分支 `feat/combat-checkpoint-seam`，起点 `2f95862408c7141a0c86269b955626981af284f8`。本切片先提供可复用恢复能力，不代表 #84 逐敌增强、全事件或正式布局完成。

## 行为与范围

普通战斗恢复同一次完整战前状态，实际胜利后保留原生 prefinished 奖励点。事件子战当前支持 Colosseum 与 DenseVegetation：在所有 owner 的原生选项任务完成后捕获完整玩家、全局 RNG、各 owner 事件 RNG、父事件及本次子战描述；重载重建该子战，不回退整个事件，也不拼接旧战中实体或计数。

Colosseum 第二场保留第一场已发生的损失、奖励和父事件进度。DenseVegetation 保留先前休整结果，开战后关闭旧休整回调；真实胜利奖励点重建父事件时同样不重新休整。恢复不再次扣探索额度。

等待加载父房与同步器任务之后，检查原 session、原生 run、map/root、父房间和同步器归属；已被替换的异步恢复不得修改新跑局。持久化 owner RNG 使用双版实际字段并要求必填，缺失时沿现有读取机制拒绝，不能默认为 RNG 0。

## 自动验证

本机证据位于该工作树 `.local/combat-checkpoint/`。

|验证|双版结果|日志|
|---|---|---|
|普通完整战前与真实胜利奖励点、Colosseum 两场有限恢复、Dense 休整后战前与真实胜利、旧异步加载替换跑局、删除必填 RNG 字段|各 8 项通过，Build/Host 0、EngineErrors/ModMetadataErrors 0|`final-{public-beta,public}.log`|
|既有 Native vegetation 关联回归|各 13 项通过，Build/Host 0、EngineErrors/ModMetadataErrors 0|`native-vegetation-{public-beta,public}.log`|
|真实 ENet 三人在局，最后 owner 任务延迟与全新三端加载|各 1 网络用例通过，Build/Host 0、EngineErrors/ModMetadataErrors 0|`network-{public-beta,public}.log`|

网络用例实际使用 Host1、Client2、Client3。原生同步器持有三个真实 owner Task，其中 owner3 的真实 Fight 返回任务延迟；等待期间没有子房、战前描述、历史追加或额外计步。释放后验证最后 owner 的金币和 RNG 副作用进入完整点，再从实际保存文件启动全新三进程，逐端核对完整玩家、全局/各 owner RNG、地图、历史、父子房间及开场敌人。不是多角色单人 transport，也不是手填 pending 集合。

网络场景强制准备受罚根房，不另证明探索进入流程；该网络用例未推进战中回滚或胜利后的旧休整回调。后两者的单人原生测试与网络结果分别保留，不扩大证据范围。

## 审查及边界

独立 Standards/Spec 复审已确认三项实际失败的修复：旧 Load 续体污染替换跑局、Dense 胜利读档重复休整、缺失 public RNG 字段默认接受。Spec 指出的多人同步证据缺口由上述真实三端延迟及全新三端加载补证，独立源码补审未发现该网络补证范围的阻断问题。Standards 的测试 BFS 重复为非阻塞建议，未新增通用框架。

该切片尚未覆盖其他事件战斗、每只新怪的完整恢复、非零死亡动画、全部正式布局及输入；这些仍由对应 #84–#87 实现承担。public v0.107.1 与 public-beta v0.111.0 使用真实 DLL，public 宿主共用 beta PCK，不能代签匹配 public 正式资源或真实游戏端；未启动游戏/Steam、安装产物或改真实存档。
