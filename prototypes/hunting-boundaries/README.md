# 追猎边界：一次性原型（未解决）

只回答[验证追猎逐敌机制、房间服务与战斗布局边界](https://github.com/Endercloud001/AllMatters/issues/70)，不是正式追猎实现。分支 `prototype/hunting-boundaries`；无安装、无真实存档、无游戏/Steam启动。

## 输入与覆盖

完整规则直接使用[追猎 resolution](https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226)，历史修订消费[补充 resolution](https://github.com/Endercloud001/AllMatters/issues/66#issuecomment-6600755901)。`matrix.json` 为13组要求索引；`cases.json` 将票据每组子句展开为目标版本×1/2/3/4人工作清单。清单是待完成工作，不能因相邻探针通过自动改为passed。细分实际运行证据见 `observations.json`，尚缺的具体输入/动作须在对应批次补齐。

复用研究分支 `research/hunting-entrypoints` 的四份逐敌/服务笔记及历史探针。Godot下RitsuLib实际注册已在多人验证票通过，早期独立.NET崩溃不作为排除依据。完整检查点/原生联网仍由多人验证票承担，本票没有重测或宣布通过。

## 运行

工作树根：`./tools/Test.ps1 -Branch All -Filter PrototypeHunting`。后续仅C#改动可加 `-SkipResourceExport`。配置使用本工作树忽略的 `Paths.local.props`，全部为已有绝对开发依赖路径。两目标必须顺序运行。

探针入口 `src/Tests/PrototypeHuntingTests.cs`，只接入一次性Godot测试宿主，没有正式Mod补丁。

## 已运行范围与限制

- 原生CreateCreature一次/两次缩放对照；实际Artifact应用与局部减层；Minion分类及Fatal许可。尚未实际胜利或触发Feed/击杀遗物。不是新增三怪固定HP模型验证。
- 族母原生睡眠后SetMaxAndCurrentHp减半；动态负力量/敏捷清除保留Plating。不是瀑布隐藏状态/完整爆发验证。
- 全部原生Encounter类型的专用场景PackedScene载入、Marker位置与模型Slots审计；未实例化完整战斗UI、未加入第五Marker、未量视觉宽度或输入遮挡。两目标程序集均挂载同一beta PCK，public原版资源仍无独立证据。
- 虫位1–4原生出生空过实际执行后检查起手；第5位原生状态机失败负例，以及局部补第五分支后的意图。未执行第五虫攻击伤害或死亡召唤五虫链。
- 资源读取/转换/发布分开见 `RESOURCE-RESEARCH.md`；没有把相似怪代入，也没有分发STS1素材。

## 后续依赖批次

1. 完成五虫实际召唤/攻击与专用位置适配；双墨宝、族母爪牙、异鱼随从，女王火炬头追加与快照行动。先解决真实资源加载与最小领域模型，不能以场景审计取代追加。
2. 真菌兽/神秘术士原创最小模型及随机、死亡边界；全部逐幕逐敌表、展示选择、实验体完整回合3/6/9、沙漏逐玩家计数和满手。
3. 服务执行端及显示端原型：全部商品/删牌副作用不变、休息附加次数与双版完成、同步/异步宝箱及SpoilsMap非奖励任务链。
4. 最后一步房间、连续事件战斗、商店实际离开、最终Boss奖励前移除领域输入；向多人验证交接字段而不假称恢复完成。
5. 自动API/行为工作完成后，再提供可安装最小验收资产，由用户启动游戏确认HUD四态、焦点/分辨率/遮挡和真实战斗布局。当前没有用户验收结果，不把未做的自动工作转给用户。

全部13组仍开放；不发布resolution、不关闭票、不更新地图Decisions so far、不选择集成架构。
