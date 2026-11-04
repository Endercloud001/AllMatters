# 真实战斗布局的最小自动实验接缝

2026-11-04，research 补查；仅源码/调用链证据，不把本笔记算运行通过。目标是补足[验证追猎逐敌机制、房间服务与战斗布局边界](https://github.com/Endercloud001/AllMatters/issues/70)第一组：真实模型资源与Bounds、原生定位、专用场景Marker容量及点击/意图几何。既有1440组遭遇工厂输入只能证明保留原敌和追加命令，不能替代本实验。具体新运行结果由 `PrototypeHuntingLayoutTests.cs` 和对应原始日志记录。

## 一手来源

本工作树 `.local/layout-next-api/{public,public-beta}/MegaCrit.Sts2.Core.<类型>.decompiled.cs` 为本轮新导出；P/B分别指实际public v0.107.1/59260271157f76a2896f0eab5bc6ea1245d8b314、public-beta v0.111.0/41cef1ea4657c524aa50e870df009e56337e8c32。实际DLL与其他笔记相同，只读，不执行游戏。行号为本地导出行号。

两版均导出 `Nodes.Rooms.NCombatRoom`、`Nodes.Combat.NCreature`、`Nodes.Combat.NCreatureVisuals`，两次命令退出0见 `.local/layout-next-{public,public-beta}.log`。追加成功导出 `Rooms.CombatRoom`、`Nodes.Combat.NCreatureStateDisplay`；public另有MonsterModel，beta复用主仓已核 `.local/api/queries/public-beta-v0.111.0/MegaCrit.Sts2.Core.Models.MonsterModel.decompiled.cs`。beta还导出NHealthBar、NPowerContainer。

一次误查 `Nodes.Rooms.ICombatRoomVisuals` 的导出退出70，原日志留 `.local/layout-next-extra.log`，不是API不存在：接口实际属于 `Rooms`。`CombatRoom : AbstractRoom, ICombatRoomVisuals`；真实 `new CombatRoom(combatState)` 构造只保存已有CombatState，可供raw NCombatRoom._visuals使用，**不需要自己实现替身接口或执行房间Enter**。来源 P CombatRoom:23、61；B:23、86。既有布局实验已据此使用真实CombatRoom实例。

导出复现方式（`$branch`分别public/public-beta）：

```powershell
./tools/ExportApiReference.ps1 -Branch $branch -IlspyPath E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters/.local/tools/ilspycmd.exe -Types MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom,MegaCrit.Sts2.Core.Nodes.Combat.NCreature,MegaCrit.Sts2.Core.Nodes.Combat.NCreatureVisuals -OutputDir .local/layout-next-api/$branch
```

## 原生布局算法：两种路径必须分开

`CreateEnemyNodes` 在Encounter.HasScene时实例化Encounter.CreateScene，加至_enemyContainer并把slots.Position减去(960,540)，之后逐敌AddCreature；有slots走 `PositionCreaturesWithSlots`，无slots走 `PositionEnemies`，最后RandomizeEnemyScalesAndHues。来源 P NCombatRoom:344–370；B:475–501。

- **专用槽位**：`PositionCreaturesWithSlots`只读取每个Entity.SlotName并执行EncounterSlots.GetNode<Marker2D>(slot).GlobalPosition。没有缺Marker的自动fallback。来源P:372–379，B:503–510。必须先记录原场景真实Marker及原敌位置，再记录追加位缺失。增加实验Marker可以继续测真实定位接线，但不能因此声明正式容量/遮挡已解决。
- **无专用槽位的自动排布**：`PositionEnemies(List<NCreature>, float scaling)`读取真实Visuals.Bounds.Size.X，目标局部宽度960/scaling，初始间距70、左偏移至少150；拥挤时把间距压至至少5，间距<30再交错Y，溢出时写原生warning。来源P:381–415，B:512–546。它不读取真实Viewport/玩家区域，不代表所有分辨率完整画面都能容纳。
- **专用场景的假设性fallback测量**：可以把同一真实怪物集合另送PositionEnemies测容量，但这不是原版为该专用场景实际选择的路径，日志须明确 `hypothetical fallback`，不能把该路径的交叉/越界直接叫作专用场景实际失败。

即使PositionEnemies使用真实Bounds宽度，Bounds.Position可能不以原点为中心；原生定位算法只按宽度排，实际点击框/可视框应从真实GlobalRect读取，不能以 `creatureX±width/2` 替代全部怪物的真实矩形。

## 最小真实模型/定位夹具

1. 依既有CoverageTests枚举每个非抽象原生Encounter、BattlewornDummy三个必需Setting，运行GenerateMonstersWithSlots，使用真实CombatState.CreateCreature。逐生态加入准确真菌兽/Seapunk/神秘术士，保留原敌和实际SlotName。首批可先1人后扩展1–4人；不能据1人外推全部人数。
2. Ritsu注册实际游戏程序集；挂载现有beta PCK、Mod PCK和作者AFTP准确资源PCK。每个实体实际 `entity.Monster.CreateVisuals()`，将返回视觉子树真实入树让 `NCreatureVisuals.Ready`运行，读取Bounds、IntentPosition、Spine数据与SceneFilePath。这是实际原物种资源，不准用固定宽度、相似怪或模拟视觉代入。
3. raw `new NCreature` 的Entity、Visuals私设属性可通过反射填入；该类没有SetVisuals方法。抑制NCreature.Ready/Enter/Exit避免整套战斗UI，但**保留视觉子树自身Ready**。将实体节点真实入树后，给raw NCombatRoom._visuals填 `new CombatRoom(combat)`，反射调用真实PositionEnemies。主算法只有拥挤warning分支才用_visuals.Encounter.Title，但夹具仍应填真实上下文，避免仅在容量超限时抛空引用掩盖原本测量。
4. 专用场景实际CreateScene、真实入树、施加原生(-960,-540)偏移，注入EncounterSlots，先只定位原敌；完整列表缺追加Marker的原生结果单独记负例。之后才加明确标为实验的Marker并再次走同一原生方法，所有敌人仍在，不选择正式布局方案。
5. 对无专用槽位实际布局、原始专用槽位和实验追加位分别记录：物种、SceneFilePath、Bounds.Position/Size/Scale、原生相机缩放、原生定位结果、真实GlobalRect、两两交集、局部目标宽度/越界、Marker名称/位置。保留失败和资源警告，不能只记录“遍历结束”。

来源：P NCreature.Create:242–251、Ready:254起；B:450–459、462起；P NCreatureVisuals.Ready:138起；B:251–278。视觉Ready从真实场景读取`%Visuals`、`%Bounds`、`%IntentPos`、`%CenterPos`，绑定真实Spine；数据缺失会warning并关闭Spine，不应忽略后仍声称资源完整。

**必须拒绝原生错误fallback资源。** MonsterModel.CreateVisuals捕获加载异常后会返回fallback场景，该场景也可能有正宽Bounds。仅 `Bounds.Size.X>0` 不足；记录并检查SceneFilePath不含fallback、准确资源路径以及预期Spine数据。来源P MonsterModel:237–254；B主仓同类型:278–295。自编Fungus/Mystic准确路径与Animator见PrototypeHuntingModels.cs；Torch见PrototypeHuntingTorch.cs；本Mod乐加维林为Ritsu自己的TryCreateCreatureVisuals。当前普通原生CreateVisuals本身不依赖AfterAddedToRoom，但未执行各原敌的钩子、阶段和动态Animator只证明初始资源几何，不能保证变身后Bounds或全部动作期间布局。

## 扩展到真实UpdateBounds的最小字段

原生 `NCreature.UpdateBounds(Node boundsContainer)` 可直接在上述真实视觉夹具上运行，精确选择Node重载，避免误选string重载。它按真实`%Bounds` Size/GlobalPosition与Visuals.Scale/`_tempScale`计算Hitbox与reticle，再按真实IntentPos设置IntentContainer。`_tempScale`原生构造初值1；反射观察或显式输入1均须记录。来源P:385–399；B:594–607、构造字段389。

可注入的最小字段：

- Hitbox私设属性→真实Godot Control；IntentContainer私设属性→真实Control。
- `_selectionReticle`→实际字段类型NSelectionReticle原生实例；`_stateDisplay`→实际NCreatureStateDisplay实例。
- 只在 `NCreatureStateDisplay.SetCreatureBounds` 叶方法隔离，保留完整原生UpdateBounds几何方法；reticle/stateDisplay不必跑Ready，避免不存在子节点和真实UI事件订阅。记录raw widget的树归属，测量只用于几何。

为什么隔离state-display叶：两版SetCreatureBounds会调用NHealthBar.UpdateLayoutForCreatureBounds、设置nameplate、NPowerContainer.SetCreatureBounds后RefreshValues（P StateDisplay179–186，B266–273）。beta HealthBar330–339需要_creature、HpBarContainer、_blockContainer，还defer到SetHpBarContainerSizeWithOffsetsImmediately365读取foreground/middleground等；PowerContainer96–103会UpdatePositions。raw状态条不是最小可运行依赖，盲调可能在deferred帧再失败。构造一套假HP控件也不能回答真实HP显示问题。

这个实验可声称**真实原生UpdateBounds把准确物种Bounds转换为Hitbox/reticle/意图锚点几何**；不能声称鼠标已实际命中、真实Reticle绘制/HealthBar/Power宽度已验、意图图标文字无遮挡或手柄导航正常。真实意图容器为默认Size时，其矩形更不能当成最终NIntent内容范围。

## 剩余确切依赖与边界

| 尚缺边界 | 原生依赖/来源 | 如何继续而不提前选架构 |
| --- | --- | --- |
| 完整NCreature.Ready | 真实health/stateDisplay、reticle、hitbox、intent子树；SetCreature、Animator/skin、UpdatePhobiaMode；B462–533/P254起 | 先实际节点树beta小场景，不以raw夹具冒充完整Ready；逐叶处理宿主外围，原模型/Animator/意图绘制保留 |
| 真实意图内容尺寸 | NCreature.UpdateIntent创建NIntent并UpdateIntent、依赖实际NextMove/目标；B613起，P410起 | 对专项五虫/女王及拥挤场景准备原生状态机与真实NIntent节点，记录图标/数字实际矩形；不能只看IntentPos |
| NCombatRoom.AddCreature完整链 | NCreature.Create→加敌我container→slot定位→UpdateCreatureNavigation；B722–747 | 需要真实Ui.Hand.CardHolderContainer、焦点路径等；先布局方法子链能独立运行，完整房间界面再补 |
| 已补：原生重复物种缩放/颜色 | RandomizeEnemyScalesAndHues在定位之后按真实MonsterMaxHpBeforeModification、基础HP范围和Bounds、Chaotic RNG调用SetScaleAndHue；B865–902 | 初始Bounds布局不包括这个后续缩放；下一批使用真实HP与原生方法，记录缩放后Hitbox及交集，不能拿默认scale=1批准最终容量 |
| 五虫、女王与指定随从 | 现有FiveWormProbe只新增位置Marker/Slots；QueenProbe左两TorchMarker为实验位置；既有运行未全面接入布局容器 | 将真实召唤结果形成的原生实体集合送上述视觉/定位链，保持真实动作顺序，不因重叠取消追加或自行改玩法 |
| 四人玩家区域/最终分辨率 | PositionPlayersAndPets及NCombatRoom.SetupCamera/OnResize、SceneContainer缩放/偏移，玩家/手牌全局UI | 当前敌方局部宽960/scaling不能作为完整画布边界，后续实际容器/玩家布局自动实验与用户物理输入分开 |
| public原版资源 | 当前两程序集共用beta PCK；PUBLIC-RESOURCE-SEARCH.md已核网上树版本冲突 | 本批API方法可双版实际运行，但不能叫public原版场景验证；不把beta可自动做的部分转用户 |

本研究没有发现必须先改玩法才可继续的限制，不发resolution，不减少追加、不确定正式Marker策略、不选集成架构。

后续实测：layout-native-r5双版各1080输入（1–4人）已运行原生cosmetic缩放及UpdateBounds；前文“下一批”缩放缺口已补。原生方法断言通过仍含P配beta资源缺脚本/节点诊断，未批准完整资源和完整Ready。结果及准确边界见BATCH-RESULTS.md顶部。
