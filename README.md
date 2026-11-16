# AllMatters · 事无巨细

《杀戮尖塔 2》模组，由 Endercloud001 开发。扩展二代事件，带回部分一代卡牌、遗物与遭遇，并在 v0.1.2 加入可单人或联机游玩的拼图模式。

## v0.1.2 拼图更新

在 AllMatters 模组设置中打开「局内默认启用拼图模式」，再开始新局。拼图适用于标准模式和支持该模式的 public-beta 恐惧模式。每日挑战、极速和自定义沿用各自的原版地图流程。

- **拼出自己的路线。** 每幕开始时可摆放、旋转并连接地图碎片，再选「由先古之民审视」。也可选「令先古之民决定」，让游戏生成地图。联机时可以一起编辑，全员同意后才开始审视或生成。
- **探索与续局。** 从已探索区域前往相邻的新房间，也可以沿已探索路线折返商店。联机探索沿用按玩家票决定目的地的规则。保存后通过原生跑局大厅继续，地图和房间状态随检查点恢复。
- **建筑师追猎。** 每幕有有限的步数。耗尽后仍能探索，后续适用房间进入追猎。密林、暗港、巢穴和荣耀会改变怪物阵容、行动或首领机制。追猎期间商店停办交易，休息处保留可用的治疗，普通宝箱奖励暂停。击败当幕最后一场首领后移除建筑师面具。
- **地图卡牌与遗物。** 羽翼之靴或黄金罗盘在拾取当幕为全队增加 3 步，同幕只触发一次。第二幕可用藏宝图标记宝箱，抵达后按每位玩家持有的张数结算。皮草大衣标记普通或精英战斗，标记战中出现的敌人当前生命降至 1 点。
- **地图绘画。** 左下角可画笔标注、擦除或清空。当前幕笔迹可随存档恢复，右下角「分享」可导出包含涂鸦的地图全景。

## 事件、遗物与卡牌

二代事件新增交易、选牌、离开和战斗分支。例如垃圾堆可带回一代卡牌与遗物，圆桌茶会可挑战第一勇士。牧师、诅咒书本、心灵绽放、大图书馆和竞技场等一代事件亦回归，部分规则针对二代重做。public-beta 的酷刑博物馆可取得展示遗物并获得一张愧疚，或扣除 10 点当前生命离开。模组提供简体中文和英文。[事件清单](docs/v0.1-scope.md#一代事件回归与调整)

## 下载与安装

v0.1.2 的开发验证目标为 **public 0.107.1** 和 **public-beta 0.113.1**，使用 [RitsuLib 0.6.11 完整 variant-pack](https://github.com/BAKAOLC/STS2-RitsuLib/releases/download/v0.6.11/STS2-RitsuLib.0.6.11.variant-pack.zip)。联机玩家应使用相同的游戏分支与兼容模组组合。[v0.1.2 实现与验收记录](docs/issues-82-88-implementation.md)

GitHub Releases 当前提供 **v0.1.1 预览版**，其下载包和依赖对应以下版本。v0.1.2 可按下文从源码构建。当前构建的模组清单仍显示 v0.1.1。

| 游戏分支 | v0.1.1 预览安装包 |
| --- | --- |
| public / 0.107.1 | [AllMatters-v0.1.1-public.zip](https://github.com/Endercloud001/AllMatters/releases/download/v0.1.1-preview.1/AllMatters-v0.1.1-public.zip) |
| public-beta / 0.111.0 | [AllMatters-v0.1.1-public-beta.zip](https://github.com/Endercloud001/AllMatters/releases/download/v0.1.1-preview.1/AllMatters-v0.1.1-public-beta.zip) |

1. 退出游戏，解压对应游戏分支的 AllMatters 安装包。预览版依赖 [RitsuLib 0.6.7 完整 variant-pack](https://github.com/BAKAOLC/STS2-RitsuLib/releases/download/v0.6.7/STS2-RitsuLib.0.6.7.variant-pack.zip)。
2. 在 Steam 的游戏「管理 → 浏览本地文件」打开安装目录。放入解压后的模组，确保清单位于 `mods/AllMatters/AllMatters.json`。
3. 把解压后的完整 RitsuLib 安装到 `mods/STS2-RitsuLib/`，保留原有目录结构及 `assets.zip`。
4. 启动游戏，在模组列表中启用 RitsuLib 与 AllMatters。

## 从源码构建

准备 .NET 9 SDK、Godot 4.5.1 mono、对应分支的游戏文件和完整 RitsuLib 包。首次配置时复制 `Paths.example.props` 为 `Paths.local.props`，再按开发指南填写本机路径。

```powershell
./tools/Build.ps1 -Branch public
./tools/Build.ps1 -Branch public-beta
```

构建输出位于 `dist/<branch>/AllMatters/`。运行 `./tools/Check.ps1 -Branch All` 可执行完整双版自动检查。[开发指南](docs/development.md) · [第三方来源](THIRD_PARTY_NOTICES.md)
