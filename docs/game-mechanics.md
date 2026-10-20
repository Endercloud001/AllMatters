# 游戏机制阅读地图

遇到卡牌、能力、牌池、显示或事件流程问题时，先按此表定位目标分支的类型与成员，再读取相关方法体。玩法取舍以 [v0.1 范围](v0.1-scope.md) 为准；本页提供机制与证据入口。

## 证据范围

本页记录 2026-10-20 核查的 public 0.107.1 与 public-beta 0.111.0。换版本后查询当前配置的真实 DLL，保留来源与版本输出。

- **方法体**用于判断执行顺序、默认行为及副作用；引用程序集只证明签名存在。
- **运行测试**只证明该场景结果，不据此推断未读过的内部实现或真实联机/回放已通过。
- public 实现导出位于 `local/api/public-github-0.107.1/implementation/`；beta 实现导出位于 `local/api/installed-beta/`。这些文件不提交；缺少时按完整类型名重新导出。

## 按问题找入口

| 问题 | 先查的类型与成员 | 本轮已确认的事实及范围 |
| --- | --- | --- |
| 卡牌有角色外观，却在该角色图鉴或奖励池找不到 | `CardModel.Pool`、`VisualCardPool`、`Rarity`；`NCardLibrary.UpdateFilter`；对应 `CardPoolModel` | beta 你好世界实际属于 `EventCardPool`，外观池为 `DefectCardPool`。图鉴故障机器人筛选使用实际 Pool；搜索文字还检查发现记录。不要由卡框推断实际奖励池。 |
| 能力牌打出后为何不能从弃牌堆找回 | `CardModel.OnPlayWrapper`、`GetResultPileTypeForCardPlay`；`CardPileCmd.RemoveFromCombat` | public/beta 原版能力牌结果为 `PileType.None`，包装流程将其从战斗牌堆移除，不是普通弃牌或消耗。具体复制/重放效果另查调用者。 |
| 卡牌升级状态为何没有传到能力 | `Cards.HelloWorld.OnPlay`、`OnUpgrade`；`PowerCmd.Apply` 的 `cardSource` 参数 | 两分支原版你好世界均只施加一层同类型能力，升级仅附加固有；来源参数的存在不代表能力会保存或读取升级状态。 |
| 相同能力是否合并，层数如何计算 | `PowerModel.StackType`、`InstanceType`；`PowerCmd.FindExistingInstanceForStacking`、`ModifyAmount` | public 方法体按 InstanceType 决定实例匹配；None 按模型 ID 匹配。两分支测试覆盖了本 Mod 两类能力各自累计，beta 内部匹配算法仍需读取目标方法体后再下结论。 |
| 回合开始时该用哪一个层数 | `PowerModel.AmountOnTurnStart`；原能力 `BeforeHandDraw`；`CombatManager`、`Hook.BeforeHandDraw` 的调用链 | 两分支原版 HelloWorldPower 读取回合开始快照，不直接用当前 Amount 生成。单测设置快照并不等于执行了完整回合调度。 |
| 随机生成是否改了牌池或 RNG | `CardPoolModel.GetUnlockedCards`；`CardFactory.GetDistinctForCombat`、`FilterForCombat`；调用者的 RNG | 两版你好世界都使用所属玩家角色池、解锁/多人限制、Common 筛选和 CombatCardGeneration RNG。public 工厂还过滤战斗生成资格，并返回包含实例创建的惰性序列；枚举结果可能产生副作用。小牌池行为查实际工厂/扩展方法，不由 Distinct 名称推断。 |
| 该升级哪个对象，满手牌时如何处理 | `CombatState.CreateCard`；`CardCmd.Upgrade`；`CardPileCmd.AddGeneratedCardsToCombat`、`Add` | public 源码及两版测试确认本调整升级生成实例后交给原版命令，模板未改变；满手牌由原命令处理溢出。直接改模板、手工塞入牌堆会绕过原流程。 |
| 单卡查看升级为何与牌组列表颜色不同 | `CardModel.GetDescriptionForPile`、`GetDescriptionForUpgradePreview`；`IfUpgradedVar`；`ShowIfUpgradedFormatter` | beta 格式化器在 UpgradePreview 中给整个选中分支加绿色标签；应把实际变化的词语放在需要高亮的范围内。两分支中英文回归测试已覆盖你好世界的普通、升级和预览输出。 |
| 新模型需要怎样注册 | `ModManager` 加载程序集；`ReflectionHelper.GetSubtypesInMods`；`ModelDb.AllAbstractModelSubtypes`、`Init`、`InitIds` | public 方法体包含 Mod 子类型发现；beta 实机已加载本 Mod 两类能力。你好世界调整使用原生发现即可；事件注册及 Godot 脚本注册仍按具体功能查证，不据此默认添加框架依赖。 |
| 事件战斗后如何恢复原事件 | `EventRoom`、`CombatRoom`、`EventModel`、`RunManager`、事件同步器、回放写入器 | 两分支存在实际内部流程差异。直接阅读 [连续事件战斗调查](research/dual-branch-api.md#连续事件战斗的具体风险)，不要将相同方法名视为相同时序。 |

## 读取顺序

1. 从当前需求找一条完整用户行为：模型创建/升级 → 打出 → 命令 → 能力 → 钩子 → 生成与入牌。
2. 查询目标分支的类型全名与公开/受保护签名。需要顺序或副作用证据时，再导出该类型的方法体。
3. 同时阅读游戏调用者与被调用成员，核对归属玩家、可变性、回合快照、RNG 和命令副作用。
4. 复用现有 Godot 测试宿主验证一个最小例子；全局生命周期替代、资源来源和未执行的真实游戏步骤写入验证记录。
5. 代码可回答的事实由 Agent 查证；只将玩法取舍交给用户。验收收尾时约定可观察接口、测试场景、版本及审查起点，后续实现沿用。

本轮具体测试宿主、限制与实机结果见 [你好世界验证记录](testing/hello-world.md)；分支差异总览见 [双分支调查](research/dual-branch-api.md)，框架接入候选见 [RitsuLib 调查](research/ritsulib-compatibility.md)。历史调查的“本轮未验证”只描述该调查当时的状态，后续功能结果以对应验证记录为准。
