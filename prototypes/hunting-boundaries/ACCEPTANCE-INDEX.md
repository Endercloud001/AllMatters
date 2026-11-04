# 用户实机入口（人工清单已确认通过）

2026-11-04 收口更新：人工清单已由用户明确确认通过，public界面按用户声明验收；新增双版1–4人原生开战/两次实际复生/第三阶段9回合串联实验。当前结论、13组逐项答案与适配限制以 [CLOSEOUT.md](CLOSEOUT.md) 为准。以下旧批次的“待验/未收到/未解决”描述保留为历史，不覆盖本次结论。

2026-11-04后续用户实机已回填部分观察：商店禁用、五虫及意图、锻造禁用、空箱、女王两火炬头符合预期；旧五虫血条重叠，新210px间距已获用户本次窗口实机通过。精确范围及截图见[用户验收记录](USER-ACCEPTANCE-2026-11-04.md)，以下“尚无用户结果”为历史批次状态。整票未解决。

本轮已准备六个独立实验包，均只在开发输出目录。每个包都不是完整追猎功能；不要以这些强制场景输入验证预算、网络或完整保存恢复。Agent未安装、未启动游戏/Steam、未触碰真实存档。

当前本机输出根为 `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters_prototype70_continue/dist/`。选择与游戏实际版本匹配的 `public-beta/`（v0.111.0）或 `public/`（v0.107.1），里面每个探针目录均有同名DLL和JSON。用户自行安装完整探针目录，保留完整RitsuLib0.6.7依赖（[作者发布](https://github.com/BAKAOLC/STS2-RitsuLib/releases)），不要覆盖正式AllMatters。实验后由用户禁用/移除探针。

|独立目录|用户只需观察|具体步骤及自动证据边界|
|---|---|---|
|AllMattersHuntingShopProbe|商店商品隐藏、删牌已使用外观；鼠标/键盘/手柄不触发服务；重开保持、返回/离店可用，普通事件服务正常|[商店步骤](shop/ACCEPTANCE.md)；本轮新增双版独立DLL实际运行；注入处理器不是物理输入，退出泄漏仍在|
|AllMattersHuntingPresentationProbe|Ctrl+F8循环四态；地图/商店/休息/战斗遮挡、焦点和可读性|[HUD步骤](presentation/ACCEPTANCE.md)；示例数据，不代表领域状态已集成|
|AllMattersHuntingFiveWormProbe|原生异蛙死亡后的五虫可见/意图/点击、奇偶首次行动与正常胜利|[五虫步骤](five-worm/ACCEPTANCE.md)；只适配异蛙，不因显示失败减少虫数|
|AllMattersHuntingQueenProbe|聚合体死亡后一次重燃、左两火炬头、下阶段攻10/激怒+2、不补召|[女王步骤](queen/ACCEPTANCE.md)；额外只读输入是作者精确PCK，不加载作者DLL，不代表素材获发布授权|
|AllMattersHuntingTreasureProbe|原生开箱动画、空文案/烟/淡出、无奖励且能继续；物理输入与真实客户端显示|[宝箱步骤](treasure/ACCEPTANCE.md)；实际加载独立DLL，beta原场景自动运行；public场景资源缺口和直接发遗物兼容未解决|
|AllMattersHuntingRestProbe|原图标/灰显、禁止项三类物理输入都不执行、允许休息及原有额外次数|[休息步骤](rest/ACCEPTANCE.md)；多人原生完成聚合仍归相关票|

较早各步骤中的 `AllMatters_prototype70/dist/...` 为历史路径；本表的 `_continue/dist/...` 是本轮重新构建产物。女王需要用户单独提供作者原始 `ActsFromThePast.pck`，本机已存在的研究输入可读自 `E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters_prototype70/.local/resource-search/ActsFromThePast.pck`，或按女王步骤使用环境变量；Agent未向mods复制资源。

以上六包及上一轮单列清单已由用户确认实机通过。public界面是用户声明验收；本机未取得public原版PCK，Agent自动public场景证据仍有限。

自动实验证据与限制见CLOSEOUT.md；直接发奖调用方限制已回答并交接，不要求用户代做。

人工验收状态：本表六包及上一轮知识选择/战斗/真实客户端清单，现均按用户最新确认标为通过，不再重复请求；详细证据级别见USER-ACCEPTANCE-2026-11-04.md。public界面为用户声明验收。
