# 女王及精确火炬头场景探针（尚无用户结果）

仅当前女王战斗：聚合体死亡覆盖一次重燃、左侧两火炬头、下个敌方阶段直接攻击、激怒增存活火炬头力量且不补召。它没有完整追猎触发/计步、网络或检查点恢复设计。一次标记只属于当前内存Queen实例，不可据此声称读档不会重复。

## 产物与资源输入

运行 `./prototypes/hunting-boundaries/queen/Build.ps1`，得到：

- public v0.107.1：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters_prototype70/dist/public/AllMattersHuntingQueenProbe/`
- beta v0.111.0：`E:/Godot_v4.5.1-stable_mono_win64/sts2mod/AllMatters_prototype70/dist/public-beta/AllMattersHuntingQueenProbe/`

产物只有自编DLL和JSON，未复制任何美术、上游DLL或上游PCK。依赖项目既定RitsuLib0.6.7完整包。另需由你单独取得作者 [AFTP0.13.0原始PCK](https://github.com/Cany0udance/ActsFromThePast/releases/tag/0.13.0)，将原文件 `ActsFromThePast.pck` 放在此探针DLL旁；不要为此加载AFTP DLL。已有只读研究输入在本机 `.local/resource-search/ActsFromThePast.pck`，可由你手动使用；Agent未向安装目录或mods复制文件。也可由你在启动环境指定 `ALLMATTERS_HUNTING_RESOURCE_PACK` 为原包绝对路径。

探针只读挂载资源，replaceFiles=false；资源输入缺失时初始化明确失败，不以相似怪或省略召唤代替。美术再分发许可尚未建立，见RESOURCE-SEARCH.md；此验收指令不授予打包发布权。

## 实际Agent运行证据

双目标独立DLL已经在Godot宿主实际加载并执行初始化；每目标6种女王原当前意图×1/2/3/4人，验证真实聚合体死亡覆盖、只一次、原生敌方快照、下一阶段两次固定10攻击、随后激怒固定2及不补死亡槽。当前补验使用真实第三幕上下文，火炬头HP按QueenBoss/第三幕/人数只缩放一次。

原生Queen场景实际加入 `(1050,695)`、`(1250,695)` 两左Marker并声明槽位；实际独立DLL的火炬头CreateVisuals加载作者精确4.2.11骨架、144×240 Bounds及halberd_slam；原生中英名称可读。实验命令与结果见 `../evidence/queen-live-r3.log`，不是纯构建证明。宿主EndEnemyTurn仍被隔离，尚无真实游戏全回合/音乐/渲染/胜利或保存证明。

## 用户操作与观察

1. 由你手动安装匹配版本的完整探针目录及上述原始资源输入，启动新测试跑局；不覆盖正式AllMatters目录。
2. 在测试跑局打开原生控制台，可输入 `fight QUEEN_BOSS`。准确ID已经过真实ModelDb查找；Agent未执行跳房或启动游戏。若要核对正常第三幕缩放，请在第三幕测试上下文中进入女王；第一幕控制台跳入女王只用于布局，不冒充第三幕HP证据。
3. 在女王不同当前意图时正常击败聚合体，观察意图被重燃覆盖。被阻止的死亡不应重燃；实际重燃整场只一次。
4. 重燃应出现精确火炬头模型，两只均在女王左侧且带爪牙；当次敌方快照不行动，下个敌方阶段直接攻击，不再额外空过。左槽先行动，因此应先各攻击10，再由女王激怒增力量2，不能为先打12擅改顺序。
5. 击败一只火炬头再观察激怒：只给存活火炬头增力量、不补召。女王死亡后原生Minion处理及正常胜利/奖励应可继续，仍须实机确认完整流程。
6. 在常用分辨率、窗口/窄屏及宽屏检查身体/血条/意图和点击区域。当前坐标是实验输入，需记录与女王、聚合体尸体、玩家及手牌重叠的位置，不能以减少召唤解决。

反馈版本、人数、所在幕、分辨率、原意图/击败聚合体时机、重燃/行动/激怒/不补召、点击/遮挡及胜利结果。用户结果待回填。不要把本探针当前战斗的一次标记当作多人票的完整恢复验证。
