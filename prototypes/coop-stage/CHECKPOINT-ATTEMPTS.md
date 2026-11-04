# 补验探索过程与失败来源

以下为2026-11-05本会话实际命令结果的整理，不是伪造的原始日志。

1. 初次 ILSpy 类型使用 `MegaCrit.Sts2.Core.Saves.MigrationManager`，退出70；按实际命名空间 `MegaCrit.Sts2.Core.Saves.Migrations.MigrationManager` 查询后退出0。反编译只留本机 `.local/issue68-continuation/api/`，不分发游戏方法体。
2. 最初尝试直接为 RitsuLib `RunSavedDataPatchHelpers.AttachDocumentFromCurrentFile` 安装 Harmony prefix。Godot smoke退出1，MonoMod 在生成 wrapper 时失败：`Unexpected null ... call MegaCrit.Sts2.Core.Saves.ISaveStore& modreq(System.Runtime.InteropServices.InAttribute) ...RunSavedDataRunSaveManagerAccess::SaveStore(...)`。换 postfix、查询已有补丁 owner 未解决。没有保留这些尝试为正式适配，没有移除项目安全措施或替换库；最终源码不修改该 helper。
3. 改用原生 MigrationManager 观察后，版本日志尝试读取 `Assembly.Location`，宿主内存加载程序集使其为空，`FileVersionInfo.GetVersionInfo` 抛 `ArgumentException: The path is empty`，smoke退出1。版本来源改用工具在启动前输出的真实 DLL ProductVersion，探针仅输出程序集标识。
4. 随后 smoke退出0及首轮80组合命令退出0，使用直接 `MigrationManager.LoadSave` 加手动复现 Ritsu 主路径绑定，损坏主文件用空串。该轮只作环境诊断，**不作为本次要求的公开加载流水线证据**。当时smoke原始日志见 [early-smoke.log](checkpoint-evidence/early-smoke.log)，其日期、版本与状态以文件内容为准。
5. 最终改回真实 `RunSaveManager.LoadRunSave / LoadMultiplayerRunSave`，不手动复现现状 helper；输入改为有效 JSON 中原生不可接受的 schema 类型，仍保留完整主文件 Mod B。适配仅在真实加载返回后按原生成功结果的对象身份重新附文档。最终80组合双版退出0；加强现状反例断言、归档 IO 调用与完整性检查后再次运行最终源，退出0。最终命令、日志及80条观测见 CHECKPOINT-RESULTS.md。

早期smoke使用同一路径覆盖日志，故第2、3项的原始单独文件未保留；异常文本与退出码来自本会话工具输出，不能将当前成功smoke文件冒充早期失败原日志。关键的原行为失败反例及适配后证据均在最终原始日志和 observations.json 中完整保留，不依赖这些环境诊断。

中途 commentary 曾表示将绕开公开加载入口；最终已撤销该实验捷径。判断以当前源与最终日志为准，不将先前建议写成已完成结论。
