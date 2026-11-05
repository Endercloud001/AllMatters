# T02 自动拼图与恢复交接

实施范围为 [#75](https://github.com/Endercloud001/AllMatters/issues/75)，权威为 [#73](https://github.com/Endercloud001/AllMatters/issues/73) 及明确修订。起始提交 `8d4fef0cadd19bdc16f1e6246bd7a92af7e5b220`；当前分支保留 #74 实现及复盘提交，不合并、不发布、不修改父规格。领取前原生 blocked-by 仅 #74，已关闭且前置提交已包含；#75 无评论、无指派，随后按 tracker 指派 Endercloud001。

## 实现及边界

造池时将六个休息处分布到六个不同碎块，保持原配额、原内部通路、Boss 替换前约束和十四块身份。单／双 Boss 六块构造见证在造池时独立验收并随池保存；自动期间不修复、重抽或更换池。手动不增加自动条件。

自动授权保存原图和边、完整池、见证、独立随机状态、实际候选次序、算法版本、扩展预算及阶段。`attachment-search-v1` 使用保存的次序做有限端点附着回溯，预算按尝试次数计算；当前默认 2000 是可调整实现参数，不是原型预算或时间承诺。搜索不穷尽所有附加跨边，未找到只能报 UNKNOWN。预算耗尽可返回同池合格见证；所有成功仍经独立验收。满额度顺序先走 Boss 最短普通前缀，再由完成区域扩展到 B−1 个不同普通节点，最后进 Boss；测试通过探索公开接口逐步执行。

后台 C# 计算与正式 Godot 界面分开；尝试复用当前角色原生 CharacterSelectTransitionPath，黑屏中央显示既定字幕。成功才一次换图并进入实际 Ancient；无确认图步骤。失败保留原图、池和边、清票、撤去遮罩，使用原生错误弹窗及指定恢复句。原生 Modal 打开期间拼图输入让位，关闭后恢复焦点。保存失败仍沿已有退出保护处理，不报告已经保存。

授权恢复从原生保存容器重做原操作，成功图直接恢复。每次等待之后检查会话、地图和操作身份，含最终保存完成至进入 Ancient 的窗口。领域断线不会撤销已授权任务；真实多人入口和传输仍由 #80 接入，当前正式入口延续 #74 的单人限制，不能据领域测试宣称真实断线联机已验。

## 自动证据

原始日志及开工修改清单在忽略目录 `.local/implementation-75/`。开工既有修改包括 AGENTS/GLOSSARY/tracker、旧范围文档、Relics/NativeCounterTests 和工具文件，全部排除本票提交；自动检查运行于保留这些修改的当前工作区，开发输出不是第 1 批正式集成包。

|范围|入口与证据|
|---|---|
|六块见证|`Test.ps1 -Branch All -Filter 'Jigsaw automatic original pool' -SkipResourceExport`；单／双 Boss × 3 seed × 13/14/15 额度。首次旧行为断言失败见 witness-red.log；最终测试包含完整独立验收|
|预算、坏候选、恢复|领域用例覆盖无见证预算 0/1 UNKNOWN、同池预算 0 保底、非法旋转/重复边、失败保图清票、授权序列化、旧操作拒绝、成功图恢复、断线不断授权及满额度可执行顺序|
|真实原生结果|`Test.ps1 -Branch All -Filter 'native automatic' -SkipResourceExport`；实际 Ancient/保存入口，真实 DLL/Godot，记录计算等待期间帧数和耗时；不使用模拟房间结果|
|正式输入和后台|生产 screen 入真实节点树，Input.ParseInputEvent 激活实际 session.Automatic；受控 worker 等待期间观察帧和输入隔离，注入异常检查原生弹窗与恢复句；另覆盖 worker 迟到及最终保存迟到的成功/失败|
|跨进程|两个独立 Test.ps1 调用分别过滤 `authorized process producer`、`authorized process consumer`，通过 ALLMATTERS_75_RECOVERY 指定隔离测试文件；真正原生容器来自 MockGodotFileIo，第二进程经原生解析、FromSerializable 和 LoadIntoLatestMapCoord 恢复。public-beta PID 39956→51208，public PID 19544→30540；四条命令均退出 0|
|完整检查|`./tools/Check.ps1` 最终退出 0，9 项全 PASS，双版各 257 项行为测试通过及双版开发包构建；日志 delivery-check.log。受影响迭代复用未改资源的 `-SkipResourceExport`|

目标分别为 public v0.107.1（59260271…）和 public-beta v0.111.0（41cef1ea…），Godot 4.5.1 mono、RitsuLib 0.6.7。两版签名分别查询：原生过场、CharacterSelectTransitionPath、NErrorPopup、NModalContainer、保存宿主 API。宿主共同使用 beta PCK；public 官方资源、正式游戏加载及真实设备仍未验。测试的 MockGodotFileIo 不访问真实存档；跨进程交换文件仅是生成的测试容器，不是生产 Mod 侧保存路径。

最终全量样本：beta 713ms／13 帧，public DLL 宿主 950ms／12 帧，均生成 14 块；单个共享 beta 资源宿主样本，不是延迟、内存或正式体验保证。公开版完整资源仍缺，原生角色过场视觉本次未在正式游戏运行。

ReadTestLog 核对两版 BuildExit/HostExit 均 0、各 257 项；beta EngineErrors=6、ModMetadataErrors=252，public EngineErrors=5、ModMetadataErrors=0。诊断保留在原始日志：本票有意注入 worker 异常，既有负例多人死亡、缺 NCreatureVisuals 宿主脚本、退出 Dummy 渲染资源诊断及无正式 Mod 元数据关联提示仍存在；不把退出 0 解释为无诊断或正式加载通过。成功候选最终写失败也已注入并验证：保图不进入奖励，再次继续先保存再进实际 Ancient。

## Standards

按 implement/code-review 对上述起点执行暂存双轴只读审查，排除既有修改。Standards 发现准备／过场异常恢复缺失及候选规范化后提交原数据两项，已修复；复审又指出最终保存等待后的旧会话窗口，与 Spec 同一问题，已修复并以双版延迟保存回归验证。最终源码复审无遗留项。

## Spec

Spec 首轮要求补可执行探索顺序、逐等待点会话隔离、正式节点输入／后台／错误证据；复审补最终保存窗口，均已实现并复验。审查者实际读取本票九个代码／测试路径的暂存 diff，不能把实现摘要当审查通过；完整检查由实施方执行。人工未验责任不因审查通过改变。

## 来源及许可

构造坐标、约束和实验输入参考 [#64 最新修订](https://github.com/Endercloud001/AllMatters/issues/64#issuecomment-6609408502)、[#69 收口及资产](https://github.com/Endercloud001/AllMatters/issues/69#issuecomment-6609856333)；正式文字参考 [U 完整要求](https://github.com/Endercloud001/AllMatters/blob/bad6e14/prototypes/jigsaw-tutorial-input/REQUIREMENTS.md)，恢复职责参考 #73 的 I/S/V 来源。C# 生产实现重新编写，不合入 Node/Python/browser 实验脚手架，不引入求解器或第三方素材。过场路径由已安装游戏提供，不随 Mod 复制分发。仓库既有许可和安全边界保持不变。

人工步骤及包就绪状态以 [第 1 批记录](jigsaw-batch-1.md) 为准。
