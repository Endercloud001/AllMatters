# AllMatters

《杀戮尖塔 2》Mod，作者 Endercloud001。已实现「你好世界」升级调整，并通过 public-beta v0.111.0 的用户牌效及高亮实机验收；v0.1 事件仍在开发，public 完整环境、真实联机与回放验证待完成。

v0.1 计划从 [STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD) 移植 11 个一代回归事件（排除对对碰）、关联遗物和必要依赖，并让升级后的「你好世界」在每回合开始生成一张升级过的随机普通牌。

- [范围与依赖清单](docs/v0.1-scope.md)
- [环境、构建与双分支验证](docs/development.md)
- [参考源码与许可记录](docs/references.md)
- [实施顺序](docs/implementation-plan.md)
- [双分支 API 比较与编译验证](docs/research/dual-branch-api.md)
- [你好世界设计与验收要求](docs/hello-world-design.md)
- [你好世界测试方法与验证边界](docs/testing/hello-world.md)

牧师已实现治疗、净化与离开，并接入 RitsuLib、事件图片和中英文 PCK。实际游戏验收由用户通过 Steam 手动进行，步骤及自动化证据见 [牧师验证记录](docs/testing/cleric.md)。

快速开始（PowerShell）：复制 `Paths.example.props` 为 `Paths.local.props`，填入游戏 DLL、Godot mono 和完整 RitsuLib 0.6.7 variant-pack 目录，然后运行 `./tools/Build.ps1`。输出在 `dist/public-beta/AllMatters/`。`-Branch public` 使用另一套公开版 DLL。安装时复制整个 AllMatters 输出目录到游戏 `mods/`，并将完整 RitsuLib 包另装为 `mods/STS2-RitsuLib/`，保留其 compat、shared 及 assets.zip 等文件。

本地文档实际保存在相邻 `AllMatters_docs`，通过 `docs` junction 作为普通文档纳入 Git。其他机器正常 clone 后使用仓库内的 `docs/` 即可。游戏 DLL、参考源码和本机配置不会提交。
