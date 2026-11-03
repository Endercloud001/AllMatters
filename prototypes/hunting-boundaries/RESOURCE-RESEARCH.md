# 追猎资源：读取、转换与发布的分层证据

2026-11-04；本票原型研究资产，不是正式资源接入。规则源为[完整追猎 resolution](https://github.com/Endercloud001/AllMatters/issues/63#issuecomment-6584771226)，历史输入为[补充研究结论](https://github.com/Endercloud001/AllMatters/issues/66#issuecomment-6600755901)。没有提取、转换或提交游戏素材，没有启动游戏、Steam 或参考构建脚本。

## 本机重新执行的证据

在工作区根目录执行：

```powershell
python AllMatters_research66/docs/research/experiments/hunting-history/inspect_sts1.py 'E:/SteamLibrary/steamapps/common/SlayTheSpire/desktop-1.0.jar'
```

退出 0。只读 ZIP/JSON 探针的实际输出：Healer=神秘术士，TorchHead=火炬头；二者 skeleton.spine 都是 `3.4.02`。Healer 动画 `Attack/Hit/Idle`，TorchHead 动画 `halberd_slam/idle`；资源分别在 JAR 的 `images/monsters/theCity/healer/` 和 `images/monsters/theCity/torchHead/`。版本常量 `[V2.3.4] (12-18-2022)`。这只证明数据确实存在且可读，不证明 Godot 可加载。

读取 `AllMatters/src/Resources/AllMatters/monsters/lagavulin/lagavulin.spskel` 前 75 字节，退出 0，文件头含 `4.2.11`。对应 `lagavulin_skel_data.tres` 以 `SpineSkeletonDataResource` 组合 `.spskel` 与 `.spatlas`。这是现有可复用资产的导出版本证据，不能独自证明游戏 native runtime 的精确版本。

本机工具检查：PATH 的 `Spine`/`Spine.com` 未找到；`C:/Program Files`、`C:/Program Files (x86)`、`C:/Users/Endercloud/AppData/Local`、`E:/` 一级目录及 `.local/references` 的限定检索未发现 Spine 编辑器/转换器。PATH 查询无命中命令退出 1，后续目录查询退出 0。未全盘扫描，不能据此断言机器绝无编辑器或没有许可证。

## 已有 Godot 链路与转换路径

本仓一手入口：`src/Tests/TestRunner.cs:33–57` 挂载本机 beta PCK，并通过 `.local/spine.gdextension` 加载安装内 `libspine_godot.windows.template_release.x86_64.dll`；不复制 native DLL。`src/Tests/MindBloomTests.cs` 的 `Presentation` 实际创建乐加维林视觉，检查骨架六动画与两套 Bounds，再检查 encounter Marker。可复用其加载/断言方式，不能将既有乐加维林成功等同神秘术士成功。

`tools/ExportResources.ps1:13–16` 明确 stock Godot 不转换游戏自定义 Spine 资源，只由 PCKPacker 原样装包。运行普通资源导出不会把 3.4.02 自动升级成游戏可读骨架。

[官方版本文档](https://esotericsoftware.com/spine-versioning#Synchronizing-versions)要求导出与 runtime 的 major/minor 一致；[同页 File storage](https://esotericsoftware.com/spine-versioning#File-storage)说明旧导出先在导出它的同版本编辑器导入、保存工程，再由新编辑器打开重导。可执行候选流程为：

1. 查明游戏实际 runtime 支持的版本，并核实本机可用的已授权 Spine 编辑器。
2. 在忽略的本票 scratch 目录只读提取所需原始 JSON/atlas/PNG，保留 JAR 不变；用 3.4.02 编辑器导入 JSON 保存 `.spine` 工程，再用目标版本导出。若 3.4.02 无法取得，记录实际工具失败，不能靠改版本字符串跳过格式迁移。
3. 依据[官方 Import Data](https://esotericsoftware.com/spine-import#Data)恢复骨架；按图集还原图像依赖。缺 nonessential 数据可能丢信息；须逐动画核对骨骼、网格、事件及图集坐标。不能保证一次导入无损。
4. 按游戏现有 `.spskel/.spatlas/.tres/.tscn` 接线，在已有测试宿主实际 Load/CreateVisuals，输出动画清单、Bounds、Marker、资源错误和退出状态；再安排用户确认真实渲染/意图遮挡。官方通用 `.skel/.spine-json` 文档不是本游戏扩展名和加载器兼容的运行证据。

[官方 spine-godot 文档](https://esotericsoftware.com/spine-godot#Exporting-for-Godot)说明骨架、atlas、PNG 是组合依赖，并区分 GDExtension 与自定义引擎模块。现有游戏 `MegaSpine` 封装已提供接缝，不应为本最小原型另装一套 runtime 或修改游戏 native 库。官方改 `.json` 为 `.spine-json` 的脚本只解决扩展名导入冲突，不是 3.4→4.2 数据转换器。

以上是候选步骤，未运行编辑器导入或转换，尚无“可转换”结论。未知编辑器路径/授权、原工程缺失、native runtime 精确版本；不改变追猎追加规则，也不拿其他怪物图像替代。

## 权限记录（非授权结论）

| 对象 | 可读取证据 | 转换权限/能力 | 发布权限 |
| --- | --- | --- | --- |
| STS1 Healer/TorchHead 原始骨架图集 | 本机合法安装 JAR，探针实际只读成功 | 工具路径和版本尚缺；本机持有游戏不自动构成转换授权 | 未取得明确再分发授权，不能借本仓 GPL 声明重许可 |
| 现有乐加维林等资源 | 本仓已有素材及来源声明，已有加载宿主 | 已有格式可供接线参考；不证明其他素材适配 | 保留 `THIRD_PARTY_NOTICES.md` 的既有来源限制；不扩大其声明范围 |
| AFTP 素材 | [固定源码 94155c1](https://github.com/Cany0udance/ActsFromThePast/tree/94155c101cbed62e5b485ede29f2a872163e69b2)研究已记公开树缺对应资产；release PCK 未在本次获取 | 未验证 | 无明确独立授权，不从公开源码可见推断素材可发布 |
| 恢复 Doormaker 图标 | [作者仓库 45b56f3](https://github.com/BAKAOLC/STS2-Act3GuaranteeDoormaker/tree/45b56f3c006a90a66801dd95f75130992e381bdb)历史研究有索引 | 不需要整 Boss；无需为过滤算法复制图标 | 未明确 LICENSE，不能当已授权素材 |
| Spine 编辑器/runtime | 官方文档可读 | 需核实适用许可及可用工具 | [官方 runtime 许可](https://esotericsoftware.com/spine-runtimes-license)、[编辑器许可](https://esotericsoftware.com/spine-editor-license)分别适用；不把 native runtime 权利当成游戏美术权利 |

许可信息依据 2026-11-04 阅读的权利方官方条款，仅识别需核实的权限，不作法律判断。本原型沿用游戏已安装 runtime，不打包新 runtime。`THIRD_PARTY_NOTICES.md:58–59,85–87` 对已有移植也明确不是完整权属审计，不应将这些先例当成新增素材已获许可。

## 下一批 Agent 可运行项与实机边界

- 在当前宿主先运行已有乐加维林真实资源加载，输出当前模型 Bounds 和新 Marker 是否存在；它回答可复用资源问题，不回答新怪转换。
- 可只读检查 native DLL 的版本字符串并查询已注册 Spine 类/方法，避免猜 runtime 版本；查明版本后才设置转换目标。
- 若找到已授权转换器，在本票 scratch 完成上述逐级导入，保留命令、版本、退出码、完整错误日志，成功后才运行新资源加载探针。若找不到，保持资源条目未验证，继续不依赖素材的真实战斗 API 实验。
- 最终真实位置、意图/点击遮挡、分辨率与动画视觉由用户实机确认。资源存在、加载成功、转换成功、可发布和战斗逻辑通过必须分别记录。

研究来源入口另见 `AllMatters_research66/docs/research/v0.1.2-sts1-mystic-torchhead.md` 与 `v0.1.2-doormaker-history.md`；本笔记只补本票资源边界，不替代其历史方法体证据。
