# 蒙面强盗 #11：实现与验证记录

日期：2026-10-21。父规格 #2；直接阻塞 #3 已在 GitHub 关闭。沿用事件表、Q1–Q20 和已确认的事件/选项行为测试入口，不重新决定玩法。实现前审查起点 `ca13c00435b7ed158b693df43a0709492e09186e`，当前分支 `main`。本票代码已实现；用户已确认 beta 功能、怪物意图和战后奖励正常。新版对白排版及真实主客机、保存恢复、回放和完整 public 的逐项验收仍待完成，不能据此关闭全部验收项。

## 行为与来源

- Act 2、累计层数至少 23、全队无人持有原生红面具；共享投票，每份事件实例只扣其 Owner 的全部金币。
- 付费后保留 Balance 的 Pointy、Romeo、Romeo 三段对话和两次继续，最后离开；开战复用原生 Combat layout 及一次终结战斗，不返回已替换的事件页。
- 完整 Pointy/Romeo/Bear、原生命值/多人缩放、行动状态机、意图和 Spine 场景。Bear 首回合 -2 敏捷，之后突进/重击；Pointy 连刺；Romeo 嘲讽、痛苦挥砍和交叉斩。上游 Romeo 实际施加 3 层虚弱，保留源码行为，不按注释误改为易伤。
- 每人一份显式 25–35 金币和原生红面具，保留正常战斗牌奖励、药水概率和后续原生奖励修改钩子。仅对本遭遇移除与显式金币重复的默认金币，不删除其他奖励。
- 中英文对话、名称、招式、反应、遭遇标题和怪物素材均参考/移植 Balance。同版本源码与素材归属见根目录 `THIRD_PARTY_NOTICES.md`；不执行上游脚本，不分发游戏原生库。

## 两目标入口与资源

目标 DLL：public 0.107.1，ProductVersion `0.1.0+59260271157f76a2896f0eab5bc6ea1245d8b314`；beta 0.111.0，ProductVersion `0.1.0+41cef1ea4657c524aa50e870df009e56337e8c32`；Godot 4.5.1 mono、net9.0、RitsuLib 0.6.7 对应 compat 模块。

- public 的 `EventModel.EnterCombatWithoutExitingEvent` 仅本地 Owner 建房间，复用 `GenerateInternalCombatState` 的战斗状态，因此本地实例必须为所有参战玩家准备显式奖励。
- beta 的 `EventCombatSynchronizer` 收集每人的 ready/rewards 后建一次房间，每个事件实例只提交 Owner 奖励。直接沿用原生单战斗退出与回放接入，不注入 beta 私有同步字段。
- beta 图鉴使用 `ModelDb.EventEncounters`；public 没有该入口，局部适配 `NBestiary.CreateEntries` 添加独立事件分组，未把遭遇注入普通地图池。public 分组的真实界面仍未实测。
- Balance 怪物场景根为 Node2D，以 RitsuLib 现有工厂转换为 `NCreatureVisuals`，保留 Bounds、意图、对话和 Spine Body 槽位。
- 普通 Godot 编辑器没有 Spine 类型，常规 `--export-pack` 无法解析这些场景；尝试在编辑器加载游戏 release Spine 扩展还导致编辑器崩溃。最终采用原生 `PCKPacker`：正常导入纹理，将 AllMatters 资源、原生纹理导入文件及未经编辑器转换的骨骼/图集/场景打包。测试进程单独加载本地游戏 Spine 扩展。游戏原生库和本机路径描述不进入发布包。

## 自动化证据

实际执行：

```powershell
./tools/Test.ps1 -Filter 'Masked Bandits'
./tools/Test.ps1 -Branch All -Filter 'Masked Bandits'
./tools/Test.ps1 -Branch All
./tools/Build.ps1 -Branch public-beta
./tools/Build.ps1 -Branch public
```

定向两分支各 5 项通过，全量两分支各 75 项通过，含你好世界及全部既有事件回归。新增测试经历缺少注册和重复默认金币的失败，再补实现。中途构建检查也覆盖两版真实 DLL。

五项观察：

1. Act 1/2/3、累计 22/23 层、队友单独持面具的准入。
2. 两人不同金币按各自 Owner 扣一次，旧选项不能重复结算；三段对话页按序结束。
3. 本地身份分别为玩家 1/2：原生 public/beta 入口只产生一个正确父事件的完整三人战斗房间；每人一个红面具、一份 25–35 金币、保留卡牌奖励；真实原生命令领取后检查金币与遗物。合法的战后 pre-finished 房间序列化/恢复保留父事件和两人显式奖励。
4. 从真实怪物开局状态机滚出熊抱及 Debuff 意图，执行后两人各 -2 敏捷；下一行动为突进。
5. 从生成 PCK 创建原生怪物 Visuals 并绑定有 Idle 动画的 Spine 骨骼、加载三人布局槽位，检查两语言事件/怪物名称；beta 检查事件图鉴枚举。

日志保留在 `.local/masked-bandits-{red,fight-red2,targeted2,full,package-beta,package-public}.log`，大日志不提交。宿主仍会输出已知模型程序集未关联正式 mod 的日志；它使用手工测试注册，不能证明正式 ModManager 加载/网络排序。最终完整运行未见 Godot 原生 ERROR/WARNING 或资源泄漏诊断；保留上述测试注册与缓存提示的证据边界。

## 保存、实机、联机与回放边界

测试替代 `RunManager.EnterRoomWithoutExitingCurrentRoom` 的 UI/调度，捕获原生入口已经构造的房间；没有运行完整房间栈、出入房间、真实胜败、网络投票或回放。原有宿主还替代全局战斗状态与空手通知。public 无界面测试使用 public DLL + beta 资源/Spine 原生库，不能证明完整 public 环境兼容。

两版真实 `CombatRoom.ToSerializable` 都禁止非 pre-finished 且有 ParentEventId 的事件战斗序列化，因此测试只在战后标记完成的合法节点恢复，不承诺任意战斗帧存档。付费中间页面通常按原生事件入口快照重启，不新增中间页保存体系。本票未以模型序列化结果证明实际读档后不重复领奖；入口、付费后、战后领奖前后游戏实际支持的保存节点还须用户检查。

**首次交付时未执行（后续人工反馈见文末）**：beta 正式加载、完整单人三人帮实战、主客机交钱/开战/领奖、断线与真实保存恢复、真实战斗回放、完整 public 加载及 public 图鉴视觉。未启动游戏或 Steam，未复制任何产物到游戏 mods，未改存档、未关闭 issue。

## 用户安装及验收

开发包位于仓库 `dist/public-beta/AllMatters/` 和 `dist/public/AllMatters/`，包含 DLL、manifest、PCK、LICENSE、THIRD_PARTY_NOTICES。更新时由用户退出游戏后备份现有包，再将目标分支的完整 AllMatters 目录放入游戏 mods；不要混用两目标 DLL。

独立依赖为 `.local/dependencies/RitsuLib-0.6.7/` 的完整 variant-pack，安装为 `mods/STS2-RitsuLib/`，保留 shared、compat、根 DLL、manifest 和包内资源结构，不只复制 compat DLL。通过 Steam 启动对应版本。

1. 在可丢弃测试局用 `event ALL_MATTERS_EVENT_MASKED_BANDITS` 定位事件。此命令绕过自然准入；另在第二幕 23 层起自然未知房间验证入池和红面具排除。
2. 中英文分别看三人材质、人物名称、初始选择和付费三段气泡；交钱后各自为零且不返款、不重复扣，最终可离开。
3. 另开测试局选开战；检查 Pointy 连刺意图、Romeo 嘲讽及攻击/虚弱、Bear 首回合熊抱 -2 敏捷及后续攻击/格挡。胜后各得一红面具、一份 25–35 金币、正常卡牌/药水规则，不额外叠加默认金币；完成后正常前进。
4. 两进程主客机分别完成相同分支，记录各自初始/最终金币、敏捷、遗物和奖励，确认一次建房/一次领奖；检查图鉴在事件分组。
5. 按实际可用保存节点在事件进入、付费、胜利领奖前后退出续局，检查金币/遗物和可继续状态；录制并重新播放开战回放，检查动作和结果一致。每项分别记录实际结果，不用“整体可玩”代替全部逐项通过。

## 代码审查

按 `code-review` 技能分别运行 Standards 和 Spec 两个独立审查，范围仅本票代码/资源/工具改动，不含开始前已有的文档、csproj 和工具工作。Standards 发现并修复 1 项 P2：将本地 beta Spine 原生库依赖从生产导出移至测试宿主，避免阻塞只配置 public 的生产构建；同步更新开发说明。Spec 未发现确认的实现缺陷，保留 1 项实机验收证据缺口。修复后重新执行受影响的定向双分支测试及双目标打包。

## 人工验收反馈与文案修订（2026-10-21）

用户反馈：“功能性部分均无问题，战斗时怪物意图无错误，战斗后的奖励也符合预期。”附件为 beta 0.111.0 中文界面：事件三人场景及战后 26 金币、红面具和卡牌奖励。这确认了用户实际测试的功能、意图和奖励；不扩写为已逐项验证真实联机、保存恢复、回放或完整 public。

本轮按反馈替换中文入场文案，强调红面具并保留金币金色，移除中文标签两侧和笑声中的多余空格。付费对白保留三段内容及说话人顺序，拆为 14 个短句气泡；每次“继续”显示下一句，最后一句阅读完后再结束，避免整段挤入同一气泡。英文同步拆分；不改变费用、准入、怪物、战斗或奖励规则。此前“仅三页”测试先因第 3 句不可继续失败，再更新实现。

新对白的实际气泡排版仍需用户安装新包后复看；旧版功能验收不自动代表新版视觉已通过。安装及测试入口沿用上文。

修订验证：`./tools/Test.ps1 -Branch All` 两目标各 75/75；Standards 与 Spec 独立审查均 0 项发现。双目标开发包重新构建，日志为 `.local/masked-bandits-dialogue-{full,build-beta,build-public}.log`。仅提交本轮事件、对应文本、行为回归测试和本验证记录，原有未提交改动保留。
