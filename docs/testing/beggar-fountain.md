# 老乞丐与神圣泉水：验证及手动验收

对应 [GitHub #4](https://github.com/Endercloud001/AllMatters/issues/4)、父规格 #2 的 A01、A02、A11、A29。阻塞项 #3 已关闭并通过 PR #16 合入。玩法沿用已确认事件表与 Q1–Q20，不增加幕数门槛或泉水副作用。

## 实现及来源

老乞丐要求全员至少 75 金币；没有可移除牌只禁用给钱，不阻止入池。付款后保留 Balance 的显露牧师页面、切换图片及净化选项；随后原生选牌必须选一张，不可取消。泉水要求全员都有可移除诅咒，只清除选择者的全部可移除诅咒。两个事件均为个人选择。

代码、中英文及两张未修改图片来自本机已核对的 Balance GPLv3 源码，参考提交 `d9eff9e067b07689e722c5283e7c3e7c451961ca`；素材使用其指南声明的重绘成品，不复制旧原图。归属与修改清单已加入根目录 THIRD_PARTY_NOTICES.md。牧师揭示图复用 #3 已归属素材。注册、默认原生布局、语言载入与 PCK 沿用 #3，不另造场景或持久化系统。

## 自动化入口

`src/Tests/BeggarFountainTests.cs` 复用已有 Godot 宿主，使用真实注册模型 → BeginEvent → EventOption.Chosen → 原生金币、选牌和删牌命令，观察金币、生命、牌组和事件状态。七项覆盖：

- 老乞丐准入逐人检查 74/75；空牌组不提高准入。
- 给钱要求完整费用及合法目标；禁用点击和离开均不改变资源。
- 付款后尚未删牌，必须完成净化；重复旧选项不重扣、不重删；队友资源和待选事件不变。
- 泉水逐人检查可移除诅咒；非诅咒及 Eternal 诅咒均不能满足准入。
- 混合普通牌、多个普通诅咒（含同名重复）、原生永久诅咒及另加 Eternal 的原生诅咒，喝水只清除全部可移除诅咒；不改变生命、金币或队友；离开不删牌。
- 实际导出 PCK 的两张图片、中英文标题与选项、75 金币动态文案，以及付款切换牧师纹理；牧师图片列入预加载路径。
- 原生玩家 JSON 格式往返恢复事件前及效果完成后的资源；真实异步选牌回答保持挂起时确认已经扣费、尚未删牌，恢复入口快照仍保留未完成的选择。

死灵诅咒由书本 #6 交付，本票不提前新增该生产模型。两版 CardModel.IsRemovable 均以 Eternal 排除牌；上游死灵诅咒定义包含 Eternal。本票使用合法原生诅咒实例加 Eternal 验证这条边界，真实 AllMatters 死灵诅咒组合留待 #6/#15，不能写为已验证。

## 保存节点与证据限制

已读取两版真实方法体：RunManager.EnterMapPointInternal 在进入节点、执行事件之前调用 SaveManager.SaveRun(null)；普通 EventRoom.OnEventStateChanged 不保存（只有先古例外）。EventRoom.ToSerializable 只保存事件 ID 与 pre-finished 状态，EventModel.BeginEvent 不支持普通事件的 pre-finished 恢复。两版 NPauseMenu 的保存退出经 NGame.ReturnToMainMenu 调用 RunManager.CleanUp（先关闭 ShouldSave）再 LoadMainMenu；后者只等待已存在的 CurrentRunSaveTask，不新增 SaveRun。因此不会在付款页或选牌 UI 写入事件进度。

因此原生读档会回到事件前的玩家资源与初始选择，付款与删牌一起回退；泉水清除同理。进入下一地图节点才保存结算后的金币与牌组。本票不新增任意中间页存档。自动化往返的是原生 SerializablePlayer JSON，使用原生 ModelIdSerializationCache.Init 准备保存元数据；没有新增校验系统、hash 或 gate。

该测试不执行真实保存退出、磁盘存档、完整地图调度、多人掉线重连或回放。实机应分别在付款揭示页、选牌时（若 UI 允许退出）、结算后未进下一节点、进入下一节点后退出重进，按上述原生语义核对；不能声称付款页被独立持久化。

## 环境与命令

2026-10-20：public 0.107.1（`59260271`）、public-beta 0.111.0（`41cef1ea`）、RitsuLib 0.6.7、Godot 4.5.1 mono、.NET SDK 9.0.319。public 使用其真实 DLL，测试资源仍来自本机 beta 游戏 PCK。

```powershell
./tools/Test.ps1 -Filter 'Old Beggar'
./tools/Test.ps1 -Filter 'Divine Fountain'
./tools/Test.ps1 -Filter 'Beggar Fountain'
./tools/Test.ps1 -Branch public -Filter 'Beggar'
./tools/Test.ps1 -Branch public -Filter 'Fountain'
./tools/Check.ps1
```

各项红绿记录保存在 `.local/checks/issue4-*.log`，包括注册缺失、选项缺失、揭示图片未预加载时的实际失败。保存格式首次执行暴露宿主尚未初始化原生保存元数据；beta 需先 AssemblyInfo.Init，再初始化 ModelIdSerializationCache，public 直接初始化原生缓存。补齐后两目标恢复测试均通过，没有替换序列化、选牌或资源命令。

最终 `./tools/Check.ps1` 退出码 0：public-beta/public 各 27/27（既有你好世界 15 项、牧师 5 项、新增 7 项），两目标测试编译及开发包构建均为 0 警告、0 错误。PowerShell 语法及 Git 空白检查通过。完整输出在 `.local/checks/issue4-final-check.log`，同轮 transcript 为 `.local/checks/last-run.log`；public 单项检查另见 `issue4-public-beggar.log`、`issue4-public-fountain.log`。

宿主保留既有 CombatManager.IsInProgress/IsEnding 及空手通知替代，沿用 HelloWorldTests 的初始化顺序；本票未修改这些替代。测试选牌由原生 ICardSelector 入口回答；揭示图测试挂载简单 Control/TextureRect，并未实例化完整事件房间。因此不证明实际 UI 布局、不可取消按钮、完整房间调度、网络或回放生命周期已通过。

## 用户安装与 Steam 验收

Agent 不启动游戏/Steam，不复制到游戏 mods，不改现有安装或存档。开发包由用户自行更新：

1. 退出游戏，将 `dist/public-beta/AllMatters/` 整个目录复制到游戏 `mods/AllMatters/`，保留 DLL、manifest、PCK、LICENSE、来源声明。完整 public 环境只使用 `dist/public/AllMatters/`，不要混用 DLL。
2. 独立依赖为 `.local/dependencies/RitsuLib-0.6.7/` 完整 variant-pack，来源为 [RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)。安装到 `mods/STS2-RitsuLib/`，保留 compat、shared 等全部目录；不能只装单个 DLL。已安装正确完整版本时无需重复替换。
3. 通过 Steam 启动 beta，在可丢弃的测试局分别输入 `event ALL_MATTERS_EVENT_OLD_BEGGAR`、`event ALL_MATTERS_EVENT_THE_DIVINE_FOUNTAIN`。调试入口绕过自然准入，另检查正常事件池与全员条件。
4. 老乞丐：有至少 75 金币和合法牌时给钱，应扣 75、显示牧师图及净化页；净化必须删一张合法牌后结束。另查 74 金币、只有永久牌时禁用；离开不改变资源。
5. 泉水：混合普通诅咒、永久诅咒和普通牌时喝水，应清除全部可移除诅咒，保留其余牌，生命与金币不变。另查离开。死灵诅咒在 #6 有真实模型后补组合验收。
6. 中英文各检查标题、初始/揭示/结束页面和禁用提示，确认图片、选牌、完成后地图与下一节点可用。按上一节覆盖可保存节点。记录游戏版本、选项前后资源、截图或日志。

## 用户实机反馈与开发验收（2026-10-20）

用户明确反馈：“经我实测，老乞丐与神圣泉水事件完全符合预期，可以判定为开发通过。”结合此前两目标各 27/27 自动化、双目标构建与独立审查，#4 按本票范围判定开发通过。

随反馈提供的四张截图已保留，能够直接证明的范围为：

- [老乞丐禁用选项](images/beggar-fountain-beggar-locked.png)：中文标题、正文、需要 75 金币和一张可移除牌的锁定提示、离开选项。
- [老乞丐可用选项](images/beggar-fountain-beggar-options.png)：持有 129 金币时显示给金币及离开，费用与删牌说明正确。
- [神圣泉水初始页](images/beggar-fountain-fountain-options.png)：游戏标注 v0.111.0，AllMatters 中文标题、图片、喝水和离开选项，以及仅清除可移除诅咒的说明。
- [神圣泉水清除效果](images/beggar-fountain-fountain-removal.png)：喝水后显示结束页及诅咒“笨拙”的移除动画。

用户的整体玩法确认按原意记录，不扩写为英文显示、不可取消按钮、自然入池、每种永久诅咒或各保存节点均已逐项实测。真实磁盘保存退出、联机、回放、完整 public 加载，以及真实死灵诅咒组合仍无单独验证记录；保留为后续环境/集成验收范围，不反向阻塞本票开发通过。Agent 本轮只记录反馈，未启动游戏、修改安装或重跑业务测试。

## 审查与提交范围

本轮起点为当前 main 的 `f5046d163f04aa029c3444e6c2484c2384eeea39`。开始已刷新 origin 并运行 ReviewScope，起点没有额外未合入提交。工作区原有范围/规格/环境脚本等未提交改动保留，仅审查并提交 #4 的生产代码、资源、测试、来源声明及本记录。

按 code-review 技能由两个独立 reviewer 审查 `git diff --cached f5046d163f04aa029c3444e6c2484c2384eeea39`，覆盖 13 个本票文件；提交前审查暂存范围，避免将用户未提交准备工作混入。Standards：0 项发现；Spec：0 项生产代码缺陷。Spec 另核对两版退出至菜单的保存调用链，已补入上文。实现提交为 `c016408`；后续用户实机确认见上一节，磁盘读档、真实联机/回放与完整 public 的证据边界继续保留。
