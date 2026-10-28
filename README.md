# AllMatters

《杀戮尖塔 2》模组 · **v0.1.1** · 作者：Endercloud001

为大部分二代事件加入更多选项，并带回了部分一代事件、遗物和卡牌

## 主要内容

- **二代事件扩展**：增加新的交易、选牌、离开与战斗分支，并调整部分原有选项。例如垃圾堆带回一代卡牌与遗物，圆桌茶会可以挑战第一勇士，人形洞穴之地可能遭遇倏忽魔。逐事件规则、数值与关联内容见 [v0.1.1 更新范围](docs/v0.1.1-scope.md)。
- **一代事件回归**：带回牧师、诅咒书本、心灵绽放、大图书馆、竞技场等 11 个事件，以及相关遗物和卡牌；部分效果按二代环境重新设计。事件清单与具体规则见 [v0.1 范围](docs/v0.1-scope.md#一代事件回归与调整)。

模组提供简体中文和英文文本。详细效果以本模组范围文档为准，与一代原版或参考模组可能不同。

## 安装与更新

需要游戏本体，以及独立安装的 **RitsuLib 0.6.7 完整 variant-pack**。依赖来源见 [参考与依赖记录](docs/references.md)。

1. 退出游戏，选择与游戏分支对应的 AllMatters 包；自行构建时，输出为 `dist/<branch>/AllMatters/`。
2. 将整个 `AllMatters` 目录复制到游戏的 `mods/` 目录。更新时替换旧版对应文件，并保留 DLL、JSON、PCK、许可证及来源声明。
3. 将完整 RitsuLib 包安装到 `mods/STS2-RitsuLib/`，保留 `compat`、`shared`、`assets.zip` 等内容，不要仅复制单个 DLL。
4. 通过 Steam 启动游戏，在模组列表中启用 RitsuLib 与 AllMatters。

开发与验证目标为 **public 0.107.1** 和 **public-beta 0.111.0**，不代表其他游戏版本已验证兼容。已有自动化与部分 public-beta 实机验收记录；完整 public 游戏环境、真实联机、保存恢复和回放仍有待验证项。

## 从源码构建

准备 .NET 9 SDK、Godot 4.5.1 mono、目标分支的游戏程序集和完整 RitsuLib 包。在仓库根目录首次配置本机路径后构建：

```powershell
Copy-Item Paths.example.props Paths.local.props
# 编辑 Paths.local.props，填写游戏、Godot 和 RitsuLib 路径
./tools/Build.ps1 -Branch public-beta
# 使用 public 对应的游戏程序集构建另一目标
./tools/Build.ps1 -Branch public
```

已有 `Paths.local.props` 时直接编辑，避免覆盖本机配置。构建产物位于 `dist/<branch>/AllMatters/`，由用户手动安装。

运行 `./tools/Check.ps1` 可执行脚本检查、双目标行为测试和开发包构建。完整环境配置、单项测试命令及实机验收流程见 [开发指南](docs/development.md)。
