# 参考源码

下载日期：2026-10-20。所有路径相对仓库根目录。源码仅供阅读，不是已安装的运行时 Mod；未执行这些仓库的项目脚本。

| 来源 | 本地目录 | 用途 | 下载方式与声明 |
| --- | --- | --- | --- |
| [STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD) | .local/references/STS2-Balance-MOD | 事件、遗物、战斗与资源的主要移植来源 | main 浅克隆；本地 LICENSE 为 GPLv3 |
| [ActsFromThePast](https://github.com/Cany0udance/ActsFromThePast) | .local/references/ActsFromThePast | STS1 原版事件、怪物与移植结构对照 | GitHub 默认分支源码归档；未发现仓库根 LICENSE，使用具体素材或代码前核对来源声明 |
| [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2) | .local/references/ModTemplate-StS2 | 游戏加载入口、manifest、程序集与 Godot 项目结构参考 | master 浅克隆；未发现根 LICENSE，不直接复制模板实现 |
| [STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib) | .local/references/STS2-RitsuLib | 上游模型自动注册和 API 适配依赖研究 | GitHub 默认分支源码归档；根 LICENSE 为 MIT |
| [sts2-ref-assemblies](https://github.com/BSchneppe/sts2-ref-assemblies) | .local/api/0.107.1/ 与 .local/api/0.111.0-beta-41cef1ea/ | 两版 API 签名与编译参考 | 从对应 NuGet 版本下载，只有引用，不含方法实现 |
| [sts2-mod-dev](https://github.com/iryuko/sts2-mod-dev/tree/main/references/game-dlls) | .local/api/public-github-0.107.1/ | public 实际方法体与编译参考 | 只下载 sts2、0Harmony、GodotSharp 和版本元数据；构建提交号与 v0.107.1 元数据一致；不提交或分发 |

两份归档因 git 网络连接失败而使用 GitHub API zipball 下载；没有 .git。另两份可用 `git -C <目录> log -1` 查看版本，或 `git -C <目录> pull --ff-only` 更新。参考代码不提交本仓库；换机器可按上表重新下载到 .local/references/。不递归拉取上游的其他子模块，避免下载无关内容。

Balance MOD 的自定义 Relics 目录不包括原生 RedMask；事件直接引用游戏 RedMask。不要因为目录缺少对应文件就再次注册一个同名遗物。

牧师实现已移植 Balance 的事件逻辑、中英文文本和 cleric.png（上游指南说明为重制图）。整体采用 GPL-3.0-only，许可证见根目录 LICENSE，文件来源与修改见 THIRD_PARTY_NOTICES.md。未复制 ActsFromThePast 或游戏自带素材；RitsuLib 作为独立 MIT 依赖安装，不打包其程序集。

## 读取入口

- 主来源 `docs/events.md`：需求展示。
- `Sts2BalanceModCode/Events/`：事件实际行为，含手册之外的事件，按范围筛选。
- `Sts2BalanceModCode/Abstract/`、`Relics/`、`Cards/`、`Powers/`、`Encounters/`、`Monsters/`、`Patches/Events/`：追踪实际依赖。
- `Sts2BalanceMod/localization/`、`images/` 和相应资源：逐项查找，移植时使用 AllMatters 路径。
- RitsuLib 文档和源码：先检查能否支持两套目标游戏 API，再确定包版本与注册方式。

## 官方版本来源

- [Steam 分支元数据查询](https://api.steamcmd.net/v1/info/2868840)，供参考；游戏本地 release_info.json 与 appmanifest 是实际安装版本的依据。
- [公开版 v0.107.1 官方公告](https://steamstore-a.akamaihd.net/news/externalpost/steam_community_announcements/1835871199305790)，上游文档保存在 docs/official-news/2026-06-19-major-update-2-v0-107-1/original.md。

默认公开分支已补齐 GitHub 来源的实际程序集和双版本引用包；来源核对、API 差异及编译证据见 双分支 API 调查（本地记录：`docs/research/dual-branch-api.md`，不随仓库分发）。仍未准备完整 public 游戏环境，也未进行游戏内兼容验证。

当前本机资料已迁至仓库根目录的 `.local/`，旧 `docs/local/` 与 `docs/references/` 仅保留兼容入口。后续下载和工具配置使用新路径，使资料不随文档 junction 的切换而失联。
