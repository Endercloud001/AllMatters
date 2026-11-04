# 精确资源搜索：神秘术士与火炬头

2026-11-04。用户明确没有已授权 Spine 编辑器，希望寻找已适配资源。本轮使用 research，读取作者一手 release 和对应源码；不以相似怪物替代，不安装 Mod、不执行参考程序集或构建脚本。只下载公开 PCK 到忽略的 `.local/` 作本机读取。

## 找到的资源与准确身份

找到 [Cany0udance/ActsFromThePast 0.13.0 release](https://github.com/Cany0udance/ActsFromThePast/releases/tag/0.13.0) 中的独立 `ActsFromThePast.pck`：资产 ID `444876462`，API 声明和下载文件实际大小均 `109837280` 字节；release 发布于 `2026-06-11T16:50:38Z`。本轮先查类型、来源、大小，再下载；未下载 DLL/ZIP，也未运行上游代码。

对应 tag 经 `gh api repos/Cany0udance/ActsFromThePast/git/ref/tags/0.13.0` 读得 commit `2ec5d3f79f663897b82dee181395b8da4c62a9b3`。该固定提交的原生移植模型分别为 [Mystic.cs](https://github.com/Cany0udance/ActsFromThePast/blob/2ec5d3f79f663897b82dee181395b8da4c62a9b3/ActsFromThePast/Acts/TheCity/Enemies/Mystic.cs) 和 [TorchHead.cs](https://github.com/Cany0udance/ActsFromThePast/blob/2ec5d3f79f663897b82dee181395b8da4c62a9b3/ActsFromThePast/Acts/TheCity/Enemies/TorchHead.cs)，两者 `VisualsPath` 直接指向下表。结合此前本机 STS1 Healer/神秘术士与 TorchHead/火炬头身份探针，确定这是所需精确物种的移植资源，不是通用法师/相似怪物。

| 对象 | PCK 内真实路径（加 `res://` 使用） | 只读证据 |
| --- | --- | --- |
| 神秘术士 | `ActsFromThePast/monsters/mystic/mystic.tscn` | 场景名 Mystic；仅原生 `res://src/Core/Nodes/Combat/NCreatureVisuals.cs` 脚本及骨架数据依赖 |
| 神秘术士骨架 | `ActsFromThePast/monsters/mystic/mystic.spskel`、`mystic.spatlas`、`mystic_skel_data.tres` | 二进制骨架 `42901` 字节，文件头明确为 `4.2.11` |
| 火炬头 | `ActsFromThePast/monsters/torch_head/torch_head.tscn` | 场景名 TorchHead；同样只依赖原生 NCreatureVisuals 和骨架数据 |
| 火炬头骨架 | `ActsFromThePast/monsters/torch_head/torch_head.spskel`、`torch_head.spatlas`、`torch_head_skel_data.tres` | 二进制骨架 `2495` 字节，文件头明确为 `4.2.11` |
| 纹理 | 上述两个目录 `.png.import` 指向包内 `.godot/imported/mystic.png-0452525adcdcb13214829430a2f6a99a.ctex` 与 `torch_head.png-4c293d4ac4f1bd918281288f2d9a38d3.ctex` | 导出 PCK 已含导入后的纹理；只取 tscn/spskel 会漏依赖 |

场景实际参数：Mystic Bounds 宽230、高250，CenterPos `(0,-125)`，IntentPos `(0,-300)`；TorchHead Bounds 宽144、高240，CenterPos `(0,-120)`，IntentPos `(8,-290)`。两者 Visuals 均为 SpineSprite，位置 `(0,-20)`、缩放 `(1.1,1.1)`。这些只是场景数据，不是战斗摆放、遮挡或容量验证结果。

## 本机可复测资产与命令

忽略目录绝对路径：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters_prototype70/.local/resource-search/ActsFromThePast.pck`。`git check-ignore .local/resource-search/ActsFromThePast.pck` 退出0确认不会加入 Git；仅索引结果在同目录 `index.json`，不分发素材。

在工作树根目录可重新下载（现有文件不要覆盖，选择新空目录）：

```powershell
gh api repos/Cany0udance/ActsFromThePast/releases/tags/0.13.0 --jq '{html_url,tag_name,assets:[.assets[]|select(.name=="ActsFromThePast.pck")|{id,name,size,browser_download_url}]}'
gh release download 0.13.0 -R Cany0udance/ActsFromThePast -p ActsFromThePast.pck -D .local/resource-search
```

本轮上述读取/下载均退出0。只读解析的 PCK header 为 GDPC、格式3、Godot版本字段4.5.1、file base112、directory offset109704528，目录数1251。目录项只读取未加密文件，读取匹配项 offset+file base 的字节，不提取或执行内容。复测索引的最小 Python：

```python
import struct
f = open('.local/resource-search/ActsFromThePast.pck', 'rb')
f.seek(32)
directory = struct.unpack('<Q', f.read(8))[0]
f.seek(directory)
count = struct.unpack('<I', f.read(4))[0]
for _ in range(count):
    size = struct.unpack('<I', f.read(4))[0]
    name = f.read(size).rstrip(b'\0').decode()
    offset, length = struct.unpack('<QQ', f.read(16))
    f.read(16)  # PCK 原生目录校验字段；不新增自定义 hash
    flags = struct.unpack('<I', f.read(4))[0]
    if '/monsters/mystic/' in name or '/monsters/torch_head/' in name:
        print(name, offset, length, flags)
```

## 当前证据等级及可加载补验入口

本子研究实际执行：release/源码/许可索引读取、公开 PCK 下载、PCK header/目录/场景/骨架头只读检查。均退出0。**尚未由本子研究执行 Godot 加载**；4.2.11 头不能代替 native skeleton 成功构建。

可由主实验宿主挂载该 PCK（已有 beta PCK 与 native Spine extension 之后，建议 `replaceFiles:false`），直接加载两 `.tscn` 或 `.tres` 并实例化 `SpineSprite`，检查 `GetSkeleton()` 与 `GetData()` 非空、动画存在、原生 Bounds/IntentPos，记录错误日志。无须加载 AFTP DLL 或 BaseLib 即可先验证此资源链，因为这两场景未引用 AFTP 自定义脚本。是否存在隐式兼容问题仍需真实运行判断。

主实验此前已报告：原始 STS1 3.4.02 的 atlas/JSON 外层资源非 null，但 `nativeSkeletonLoaded=False`；现有 Lagavulin4.2.11为 true。两目标宿主都用 beta PCK/native Spine，public DLL+beta资源成功不能证明完整 public 安装资源支持。本次新包应沿同样分层记录，不能把“非null”冒充骨架已加载。

## 读取、转换、发布分别记录

| 问题 | 本轮结论 | 仍需什么 |
| --- | --- | --- |
| 可以取得并读取吗 | 是：作者公开 release 实际下载成功，准确物种文件存在且可读 | 不需要为算法实验安装上游 Mod |
| 是已转换数据吗 | 包内两骨架均实际标记4.2.11，已有Godot资源场景/图集/导入纹理组合 | 仍需 native runtime 构建骨架/动画运行；不声称已验证转换无损 |
| 还需要自购转换器才可开展本机加载实验吗 | 现有公开已转换 PCK 提供了无需现场转换的候选输入 | 不因此获得素材发布权，也不替换游戏runtime |
| 可以发布到 AllMatters 吗 | 未建立：GitHub仓库 API `license:null`；0.13.0完整树按 license 名称检索无结果 | 应取得作者对其移植/转换贡献的许可及原美术权利方条件；代码许可与美术许可分开。未联系作者，未对外发消息 |
| 可以拿它证明完整追猎机制吗 | 不能；上游原AI与追猎方案有差异 | 仅复用本机资源输入，机制仍由本票实际行为实验回答 |

上游 [README](https://github.com/Cany0udance/ActsFromThePast/blob/2ec5d3f79f663897b82dee181395b8da4c62a9b3/README.md)自称STS1幕移植且有占位/未完成部分；这不是资产授权声明。无明确 LICENSE 不能从公开可下载推导可再发布；本笔记没有将其称为授权资源，也未把原有 Balance 资源声明延伸到此包。

本轮优先路径已经找到精确已转换候选，故未扩大到相似怪物库或无来源网盘。剩余路线是：真实 Godot 加载/动画检查 → 用户实机布局/显示确认 → 发布前明确授权；任何一项缺失均单独保持未验证，不取消已确认的追加规则。

## 主实验追加的真实运行证据

作者包还包含准确的真菌兽资源 `res://ActsFromThePast/monsters/fungi_beast/`；[固定tag的FungiBeast模型](https://github.com/Cany0udance/ActsFromThePast/blob/2ec5d3f79f663897b82dee181395b8da4c62a9b3/ActsFromThePast/Acts/Exordium/Enemies/FungiBeast.cs)直接引用该路径。只复用视觉输入，不复制上游AI；其初手/随机规则不能替代本票resolution。

在原型分支Godot 4.5.1宿主，实际挂载该PCK（replaceFiles=false、无上游DLL），三种本票原创规则模型各自的原生CreateVisuals均解析到准确场景；SpineSkeletonDataResource实际loaded=true、版本4.2.11，动画逐个实际SetAnimation并推进Godot帧。神秘术士Attack/Hit/Idle、火炬头halberd_slam/idle、真菌兽Attack/Hit/Idle全部存在且播放；额外验证模型Animator的Attack触发接到确切动画，避免沿用默认小写idle_loop/attack而失配。

| 物种 | 实际Bounds宽×高 | 实际IntentPos | 骨架缩放 |
| --- | --- | --- | --- |
| 真菌兽 | 260×170 | (0,-220) | 1.1×1.1 |
| 神秘术士 | 230×250 | (0,-300) | 1.1×1.1 |
| 火炬头 | 144×240 | (8,-290) | 1.1×1.1 |

命令 `./tools/Test.ps1 -Branch All -Filter PrototypeHuntingResource -SkipResourceExport`，`evidence/resources-r9.log`及退出文件为0；随后 `evidence/combined-r5.log`双目标各37项通过，包括模型Animator补验。这仅证明双目标DLL+共享beta native资源环境下可加载/播放，仍不证明public原生资源独立安装、真实战斗布局、完整AI动画切换或美术可再分发。PCK保留忽略目录，Git只提交来源、探针和日志。原始3.4.02加载失败、已转换4.2.11实际成功与发布许可未建立三个结果分别保留。
