# #82–#88 新增素材来源与声明

2026-11-12。此表记录当前集成与开发工作树中新增素材的来源及声明工作；资源准备不等于模型、行为、布局或正式游戏验收通过。最终包应以实际合入文件及随包 `THIRD_PARTY_NOTICES.md` 为准。

## 用户提供的三张 PNG

使用依据为 #63 resolution comment 6584771226 及本批开发授权；代码的 GPL 许可不重新许可这些图片。不将其标为 Balance 提供，不推断其他项目的使用权。

|素材|原始归档名|当前用途与状态|声明安排|
|---|---|---|---|
|Architect Mask|`architect-mask.png`|#82 已集成，遗物图及各视图共用原图|现有 `Architect Mask (#82)` 条目已注明用户提供及图片权利边界，保留。|
|Spore Cloud|`spore-cloud.png`|#84 开发树能力图，原图供两尺寸|现有工作树条目已记来源；合入时明确代码 GPL 不重新许可图片。|
|Tender|`tender.png`|#85 已准备原图，尚未接入生产展示牌|接入时加独立用户素材条目，记录实际用途和处理；当前不宣称存在裁切或尺寸转换。|

## 返回怪物资源

Fungus Beast、Mystic、Torch Head 的来源为 `MT-SUPER-POWER/STS2-Balance-MOD`，revision `d9eff9e067b07689e722c5283e7c3e7c451961ca`，各路径位于 `Assets/ActsFromPast/ActsFromThePast/monsters/`。

|怪物|上游子目录|当前实际处理|当前状态|
|---|---|---|---|
|Fungus Beast|`fungi_beast/`|保留 PNG/骨架；图集仅适配资源路径，场景移除上游脚本，使用项目视觉工厂|#84 开发树已引用，已有来源声明；未整票合入。|
|Mystic|`mystic/`|资源路径与场景依赖适配；不将上游脚本作为本项目行为实现|#86 已准备，尚未接入生产；接入时补准确 NOTICE。|
|Torch Head|`torch_head/`|同类资源路径与场景适配|#87 已准备，尚未接入生产；接入时补准确 NOTICE。|

来源仓库的 LICENSE 与 README 声明 GPLv3；记录其分发声明并沿用项目已有返回怪物的来源写法，保留 Balance、ActsFromThePast、Mega Crit 及原作者归属。该记录不表示原游戏美术被重新许可，也不改变项目 LICENSE。仅复用上述已调查资源，不据此引入其他完整 Boss 实现或未明确来源的图标。

## 随包核对

`tools/Build.ps1` 复制 DLL、manifest、PCK、LICENSE 及 THIRD_PARTY_NOTICES。资源位于项目目录但尚未被 C# 引用，并不能证明它不会进入导出的 PCK；最终导出后核对实际内容及对应 NOTICE，不把准备状态当成交付状态。

最终声明应按实际合入素材更新，分别列用户图片和返回怪物来源。public DLL 共用 beta PCK 的自动测试不能代签匹配 public 正式资源或真实游戏端验收。

来源调查与逐文件读取证据保存在主仓本机 `.local/research/issues-82-88-license-current-audit.md`，以及 `implementation-82-88-assets.md`。本页未新增运行时校验或资源 hash。
