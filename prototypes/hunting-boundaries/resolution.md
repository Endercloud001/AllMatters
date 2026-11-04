## Resolution

**结论：** 双版原生API/行为实验及用户确认的人工清单，已经足以回答本票提出的可行性与适配边界问题。原生入口可承载完整追猎规则，但不能用通用加HP/力量、清空奖励列表或补单一Marker代替逐敌、异步房间完成与专用布局适配。逐项答案、具体版本/人数/输入/预期/实际/退出码/日志索引、实验层级及已知反例见[13组收口记录](https://github.com/Endercloud001/AllMatters/blob/0c829c8b/prototypes/hunting-boundaries/CLOSEOUT.md)、[覆盖矩阵](https://github.com/Endercloud001/AllMatters/blob/0c829c8b/prototypes/hunting-boundaries/matrix.json)、[运行记录](https://github.com/Endercloud001/AllMatters/blob/0c829c8b/prototypes/hunting-boundaries/runtime-cases.json)。

**新增关键实证：** [连续阶段日志](https://github.com/Endercloud001/AllMatters/blob/0c829c8b/prototypes/hunting-boundaries/evidence/stage-continuous-r6.log)在public v0.107.1/59260271与public-beta v0.111.0/41cef1ea下，以1/2/3/4人输入实际执行TestSubject原生开战、两次死亡拦截及复生，再运行第三阶段九个玩家方回合。3/6/9只限制额外抽牌，原生起手和回合末清理保留。readiness协调由宿主显式驱动/局部隔离，不证明原生多人消息、检查点、保存恢复或正式集成。完整检查[退出0](https://github.com/Endercloud001/AllMatters/blob/0c829c8/prototypes/hunting-boundaries/evidence/full-check-closeout-r1.log)：beta273项、public272项。

用户确认上一轮列出的六包、选择屏、完整女王/战斗布局、服务、HUD及真实多人客户端人工清单通过；public界面按用户明确声明记为用户验收，不写成Agent取得准确public原生PCK或完整场景运行。记录见[用户验收](https://github.com/Endercloud001/AllMatters/blob/0c829c8/prototypes/hunting-boundaries/USER-ACCEPTANCE-2026-11-04.md)。

**适配限制：** 专用遭遇缺Marker、局部越宽/相交不得导致删怪；public与beta回合调度边界不同；商店内存保留不等于保存重访；public休息Exit不代表全员结束。宝箱直接GainGold/Obtain可绕过列表清空；底层取消不一定满足调用方对Owner或真实入库的后续假设。已证明规则入口与这类第三方调用方不具备通用兼容，必须按具体来源适配并验证。该结论不删玩法规则，也不指定正式集成架构。

交接资产在独立分支[`prototype/hunting-boundaries-continuation`](https://github.com/Endercloud001/AllMatters/tree/prototype/hunting-boundaries-continuation)，完整运行包与原始失败/成功日志保留在`prototypes/hunting-boundaries/`。历史aggregate ZIP早于五虫210px修订；最新单独间距包/Build入口见验收索引。未安装mods、未启动游戏/Steam、未触碰真实存档。

下游请将这些结论作为实现/集成输入；多人保存、网络、恢复及实际集成仍由各自票承担。本 resolution 回答的是本票问题与实验边界，不代表正式追猎功能实现或所有第三方Mod兼容。