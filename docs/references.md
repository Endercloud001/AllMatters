# 参考源码

以下为来源与重新获取入口，**不是当前树已安装内容清单**。历史采集日期为2026-10-20；读取前核本机目录与实际revision，移植前核具体文件许可。参考源码仅供阅读，不执行上游构建/部署脚本，不提交游戏方法体或上游整包。

|来源|本机资料建议位置|用途及来源边界|
|---|---|---|
|[STS2-Balance-MOD](https://github.com/MT-SUPER-POWER/STS2-Balance-MOD)|`.local/references/STS2-Balance-MOD`|事件/遗物/战斗/资源移植来源；GPLv3。已使用素材的固定 revision 和路径见[第三方素材来源与许可](../THIRD_PARTY_NOTICES.md)|
|[ActsFromThePast](https://github.com/Cany0udance/ActsFromThePast)|`.local/references/ActsFromThePast`|STS1事件/怪物与转换结构对照；历史未发现根LICENSE，逐文件核来源，不从可加载推出可分发|
|[ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2)|`.local/references/ModTemplate-StS2`|加载入口/manifest/Godot结构参考；历史未发现根LICENSE，不直接复制模板实现|
|[STS2-RitsuLib](https://github.com/BAKAOLC/STS2-RitsuLib)|`.local/references/STS2-RitsuLib`|模型注册/API适配研究，MIT；运行依赖独立安装，当前目标及完整variant-pack见[开发入口](development.md#当前开发目标2026-11-13)|
|[sts2-ref-assemblies](https://github.com/BSchneppe/sts2-ref-assemblies)|`.local/api/`下分版本保存|NuGet引用包只有签名、没有方法实现；旧beta0.111材料不代表当前beta0.113|
|[sts2-mod-dev game-dlls](https://github.com/iryuko/sts2-mod-dev/tree/main/references/game-dlls)|历史`.local/api/public-github-0.107.1/`|public实际程序集及元数据曾用于比对，不提交或分发。当前宿主使用匹配目标的完整资源，不能凭旧DLL目录当安装环境|

按上游链接将资料取到目标树`.local/`；带`.git`的副本用`git -C <目录> log -1`核revision，归档副本没有提交历史，应记录来源版本。不要自动更新已固定的素材来源，也不递归获取无关子模块。

## 读取与移植

玩法从目标Issue和适用范围文档读取。Balance源码以`Sts2BalanceModCode/Events/`为事件入口，沿`Relics/Cards/Powers/Encounters/Monsters/Patches/Events`追消费者，再逐项核对本地化和资源路径，不假定另有本仓库`docs/events.md`。

Balance自定义Relics目录不包含原生RedMask；事件引用游戏模型，不能因上游缺同名自定义文件而重复注册。牧师等已移植内容的实际来源、修改和分发声明在根目录`THIRD_PARTY_NOTICES.md`；原游戏美术权属不由本项目GPL重新许可。

Harmony目标先核**当前两版实际方法体**，工具和输出目录统一见[API查询](development.md#api-查询)。参考签名可辅助定位，不能代替所选版本的方法实现或正式资源/游戏验收。

## 版本与本机目录

实际版本以所选目标本地`release_info.json`、程序集元数据及安装manifest为准；[Steam分支元数据查询](https://api.steamcmd.net/v1/info/2868840)仅作外部参考。当前目标、匹配资源与本机路径见[开发环境](development.md)，不再引用未跟踪的旧公告或调查路径作为必需入口。

各树`docs`可能是普通目录或junction，访问前核实际属性；`.local/`依赖、API、工具和日志与文档独立。新clone不承诺存在旧`docs/local/`、`docs/references/`或同级研究目录；历史材料从Git固定来源恢复，不自动改目录结构。
