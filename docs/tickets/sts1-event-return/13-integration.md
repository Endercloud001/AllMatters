# T13：11 事件集成验收与双分支开发包交付

GitHub：[#15](https://github.com/Endercloud001/AllMatters/issues/15)；已发布并标记 `ready-for-agent`，执行状态以 GitHub 为准。

## Parent

[一代事件回归父规格 #2](https://github.com/Endercloud001/AllMatters/issues/2)

## What to build

将已交付事件组合后验证无跨事件回归，交付安装说明和双分支开发包及真实验收记录，为后续正式发布提供清晰证据。

本票按父规格 A01–A32 验收。

## Acceptance criteria

- [ ] 核对 11 个事件与所有选项、必要遗物/牌/能力均存在；A01–A32 已有对应验证结果和明确边界，不能用规格存在代替通过。
- [ ] 完整 public 与 beta 分别实际加载/玩法验证，检查中英文、PCK、怪物资源、模型身份、依赖选择及安装说明与产物一致。
- [ ] 组合验证强盗/墓穴红面具关系、书本/泉水永久诅咒边界、瓶子不进入转盘/商店/常规池、绽放印记与治疗、瓶装跨战斗与恢复。
- [ ] 完成代表性个人/共享选择与三类事件战斗的真实双端联机、可保存节点恢复和回放重播；覆盖竞技场中场和重复领取风险，保留版本及复现证据。
- [ ] 回归你好世界现有已确认功能，运行相关两分支自动化和构建；排除资源、语言、模型或依赖引入的回归。
- [ ] 开发包只含允许分发的模组及资源，来源、许可、归属齐全，不包含游戏 DLL/JAR/反编译输出；未经明确授权不正式发布或上传发行包。
- [ ] 本票承担组合验证与开发交付，不接收内容票尚未做完的本地测试/核心行为；发现问题回到对应内容修复并仅重测受影响场景。
- [ ] 遵循父规格及范围事件表；未修改行为继承 Balance，不重新访谈 Q1–Q20，不引入表外功能、额外 hash/冻结 contract/baseline/gate。
- [ ] 除环境/集成票的专门边界外，本票包含相应中英文、图片/场景/PCK、模型行为、真实目标双分支构建及 beta 实际可玩验证；普通测试只针对具体失败风险，公开行为入口优先。
- [ ] 本票引入的代码/素材先核对许可与归属；记录实际命令、版本、结果和宿主替代边界。真实联机/回放/完整 public 未执行时如实列出，不能以无界面通过替代实机证据。

## Blocked by

- [#4: feat: 老乞丐与神圣泉水：付费删牌和诅咒清除](https://github.com/Endercloud001/AllMatters/issues/4)
- [#5: feat: 增益研究者：J.A.X.、变化升级与突变之力](https://github.com/Endercloud001/AllMatters/issues/5)
- [#6: feat: 诅咒书本：独立阅读、三选一及三本书效果](https://github.com/Endercloud001/AllMatters/issues/6)
- [#7: feat: 变化大转盘：六结果、稀有度分布与空池处理](https://github.com/Endercloud001/AllMatters/issues/7)
- [#8: feat: 红面具大人之墓：固定献金及持面具分支](https://github.com/Endercloud001/AllMatters/issues/8)
- [#10: feat: 大图书馆：打扫、三种瓶装绑定及跨战斗恢复](https://github.com/Endercloud001/AllMatters/issues/10)
- [#11: feat: 蒙面强盗：交钱或三人帮事件战斗](https://github.com/Endercloud001/AllMatters/issues/11)
- [#12: feat: 心灵绽放：双乐加维林与三类非战斗选择](https://github.com/Endercloud001/AllMatters/issues/12)
- [#13: feat: 竞技场：首战零战利品、中场及随机精英第二战](https://github.com/Endercloud001/AllMatters/issues/13)
- [#14: chore: public 完整环境：安装并实机验证牧师](https://github.com/Endercloud001/AllMatters/issues/14)
