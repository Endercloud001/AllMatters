# public 原生资源补查：未找到可核 v0.107.1 完整资源

2026-11-04；用户暂无本机 public PCK 备份，并授权按需从互联网调查/拉取。本次只查 GitHub 原始树、版本文本及两个小场景，不下载大型压缩包，不执行上游代码，不修改安装或mods。结果是**找到可参考的提取资源树，但版本不一致，尚不能解除 public 原版界面的证据缺口**。

## 已核来源

| 来源 | 具体证据 | 结论与限制 |
| --- | --- | --- |
| [iryuko/sts2-mod-dev 固定树](https://github.com/iryuko/sts2-mod-dev/tree/b2f708182195af964de62cf55141614a189e9cd2) | recursive Git树完整（truncated=false）；`references/pck-extract/sts2-main` 下6803条，blob size合计308,804,157字节（约309MB，不是可直接下载PCK大小） | 提取目录确有宝箱/选择屏、字体/本地化/导入元数据，但没有完整 sts2.pck。不能把该树当 public v0.107.1 完整资源 |
| [同树 API 版本记录](https://github.com/iryuko/sts2-mod-dev/blob/b2f708182195af964de62cf55141614a189e9cd2/references/api-notes/app/release_info.json) | v0.107.1 / 59260271；与本机下载的public DLL版本对应 | 只认证本处API记录，不认证仓库其他资源目录 |
| [提取目录自身版本记录](https://github.com/iryuko/sts2-mod-dev/blob/b2f708182195af964de62cf55141614a189e9cd2/references/pck-extract/sts2-main/release_info.json) | v0.98.3 / cb602cef，2026-03-09；该文件历史查询仅找到2026-03-15导入提交 dd25d7944ace92242d42d3b58652cbaf013225d1 | 与API版本不同；不能确定该目录所有文件是否同一版本，只能说“混合/未核版本的提取资源”，也不能肯定全树都精确v0.98.3 |
| [提取的二选一场景](https://github.com/iryuko/sts2-mod-dev/blob/b2f708182195af964de62cf55141614a189e9cd2/references/pck-extract/sts2-main/scenes/screens/card_selection/choose_a_card_selection_screen.tscn) | 3618字节；含 InspectPrompt/ControllerIcon TextureRect（本地下载行63），依赖 choice_selection_skip_button.tscn、combat_piles_container.tscn、peek_button.tscn、字体及placeholder图等 | 可能供后续兼容资源实验参考；没有加载/运行该场景，尚不知其他依赖是否与public API匹配 |
| [提取的宝箱场景](https://github.com/iryuko/sts2-mod-dev/blob/b2f708182195af964de62cf55141614a189e9cd2/references/pck-extract/sts2-main/scenes/rooms/treasure_room.tscn) | 9662字节；引用 NTreasureButton.cs（行4），ChestVisual 在 Chest 子节点（行116） | 与实际public v0.107.1 NTreasureRoom.Ready期待根部 `%ChestVisual` + NButton/MegaSprite 的实现不能直接等同；不能因有旧ControllerIcon就认定整个场景为匹配public |
| [BSchneppe/sts2-ref-assemblies v0.107.1](https://github.com/BSchneppe/sts2-ref-assemblies/releases/tag/v0.107.1) | 该release资产列表为空；main固定树4e7f56aead1b2467b8dbc94905801bb0c73c078c完整，无PCK或tscn | 编译引用来源，不能补界面资源 |
| [Motoki0705/slay_the_spire_2_mods 版本记录](https://github.com/Motoki0705/slay_the_spire_2_mods/blob/90919693974dcb1461a9cd28feb3444f41b8aaa0/docs/research/motion/evidence/release_info.json) | GitHub code搜索v0.107.1的另一命中，版本记录同为59260271；完整树没有PCK、treasure_room.tscn、choose_a_card_selection_screen.tscn | 有版本研究材料，不是所需资源下载源 |

iryuko现有发布资产主要为其角色Mod，ZIP约408–464MB，另一个card-workbench约73MB；未见标为原版public资源包，未下载。不能用其Mod版本号认证其中未知资产版本。

## 查询及本地保留

使用已认证 `gh api`，只读：

```powershell
gh api 'repos/iryuko/sts2-mod-dev/git/trees/main?recursive=1'
gh api 'repos/iryuko/sts2-mod-dev/commits?path=references/pck-extract/sts2-main/release_info.json&per_page=20'
gh api repos/iryuko/sts2-mod-dev/releases
gh api 'repos/BSchneppe/sts2-ref-assemblies/git/trees/main?recursive=1'
gh api repos/BSchneppe/sts2-ref-assemblies/releases/tags/v0.107.1
gh search code '"v0.107.1" filename:release_info.json' --limit 15 --json repository,path,url
gh search code 'filename:release_info.json "59260271"' --limit 15 --json repository,path,url
gh search code 'filename:treasure_room.tscn "NButton.cs"' --limit 15 --json repository,path,url
```

这些API/搜索查询退出0。repo搜索`sts2 assets`、`sts2 decompiled`返回空；code搜索只提供当前可见命中，不证明互联网不存在匹配资源。

完整原始树JSON留在本工作树 `.local/public-resource-{iryuko,ref,motoki}-tree.json`；四个小文本在 `.local/public-resource-search/`（两个release_info及两个tscn），没有把专有资源加进分发内容。原始小文本没有执行或加载。需引用本地行号时对应文件名是原路径用下划线替换 `/`。

## 后续可消费的边界

当前public仍可验证真实API与方法级子链。若之后利用上述未核版本的ControllerIcon/选择屏构造兼容资源，必须称作**跨版本兼容资源实验**并记录来源/差异；即使能运行，也不能改写成public v0.107.1原版资源/完整游戏验收通过。完整public原版PCK尚无已核下载来源；用户实机验收前不能据此承诺双版本显示支持。无需为此修改追猎规则或要求用户承担可在beta自动跑的实验。
