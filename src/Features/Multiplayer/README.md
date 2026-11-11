# 多人恢复入口

恢复沿原生保存、加载大厅与跑局生命周期执行：`RunSaveManager.LoadAndCanonicalizeMultiplayerRunSave` → `LoadRunLobby` 原生载荷 → `RunState.FromSerializable` → `RunManager.SetUpSavedMultiplayer` → `LoadIntoLatestMapCoord`。

完整原生库存由 `SerializableRun` 保存，拼图文档通过 `JigsawCheckpoint` 和同来源扩展保留。原存档角色与本次大厅在局成员分开；正式探索界面读取恢复后的 `JigsawNativeSession.Map`。

旧 `RecoveryStrategies`/`MultiplayerRecoveryOrchestrator` 仅返回阶段标志，从未接入生产，已由原生加载链路及 `JigsawRecoveryNetworkTests` 的实际 ENet 多端恢复测试替代。没有第二套存档文件或恢复编排状态。

自动宿主、共享 beta 资源、正式 public 安装和用户真实三端验收分别记录，详见 `docs/batch-3-test-guide.md`。
