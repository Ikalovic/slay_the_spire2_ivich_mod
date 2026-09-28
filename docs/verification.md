# 验证记录

## 0.3.0 美术接入（2026-09-28）

执行：

```sh
python3 tools/stage_art.py --source /home/ika/temp/slay_mod --check
python3 tools/stage_art.py --source /home/ika/temp/slay_mod
python3 tools/build_art.py --godot /tmp/ivich-tools/Godot_v4.5.1-stable_linux.x86_64
python3 tools/build.py
python3 tools/package_release.py --art
```

| 检查 | 结果与范围 |
| --- | --- |
| Python | 25/25：原目录/打包检查及源图覆盖、固定SHA256、禁止废案和源目录写入、PCK缺失/过期/替换拒绝 |
| 规则与异步法术 | 53/53、9/9 |
| 真实程序集 | 15/15；Release编译0警告0错误；19个Harmony目标安装及清理成功，包含角色头像按钮 |
| Godot导入 | 168个PNG目标；原图按字节复制，导入器生成不同尺寸的显示纹理 |
| 隔离PCK加载 | 337项资源加载通过；场景成功实例化；包内508个文件均属于Ivich或Godot必要缓存/元数据 |
| 渲染预览 | 从PCK加载并渲染选角背景、头像、镰刀、代表卡图及两形态预览；已检查完整构图与显示比例 |
| 原生场景尺寸核查 | 从本机游戏PCK只读提取的角色选择、遗物、卡牌场景确认IgnoreSize/FitWidth，不按原图尺寸撑大控件 |

合计102项自动检查，另有337项实际资源加载检查。日志为 `artifacts/verification-0.3.0.log`、`artifacts/art-build-0.3.0.log`；资源证明在 `artifacts/art/Ivich.pck.validation.json`，记录Godot版本、PCK/源工程SHA256、每个加载资源的类型与尺寸。Godot编辑器下载经过官方SHA-512校验，实测版本为 `4.5.1.stable.official.f62fdbde1`。

PCK中的B03小图为170×256、大图666×1000；图集画布分别为350×266、1325×1007，均严格25:19并保留整张纵图。头像图集420×420。两张形态选择图集为1650×1254。纹理以原生控件大小绘制，不按这些像素尺寸铺满界面。

预览输出在 `artifacts/art-preview/selection.png`、`cards.png`。它们是独立Godot资源预览，文字布局用于检查构图，不是原生游戏截图。预览驱动报告不支持切换V-Sync，但两张截图均成功保存，未出现资源加载错误。

导出验证脚本首次运行暴露GDScript节点类型推断错误，改为明确Node类型后重跑成功；完整红绿日志保留。独立预览脚本首次赋值顺序导致尺寸被纹理最小值撑大，调整为先设置IgnoreSize后重新渲染并检查；另行核实了游戏真实场景的模式。

0.2.0用户游戏日志已确认Ivich初始化与选中 `CHARACTER.IVICH-IVICH_CHARACTER`。本轮未替换用户正在使用的游戏安装，也未宣称0.3.0已经通过游戏内交互、战斗、存档或联机验证。三形态战斗动画依然使用占位；带灰底人物母版只用于展示。

---

## 0.2.0 原始构建记录（历史）

执行命令：

```bash
python3 tools/build.py --package
```

| 检查 | 结果 | 覆盖范围 |
| --- | --- | --- |
| Python测试 | 10/10通过 | 全82张目录与源稿一致、82个实现类、72张奖励候选、初始组、各形态商店能力卡、X费、发布白名单与校验和 |
| Ivich.Core.Tests | 53/53通过 | 资源隔离、原子费用、精确生命比例、形态/上限/终焉联动、伤害/冻结、营火成长、快照、限次及溢疗取整 |
| Ivich.Spells.Tests | 9/9通过 | 异步顺序、吟唱延迟、起始阶段新记录、显式加速、分离释放批次、清场取消与跨战版本隔离 |
| Release构建 | 0警告/0错误 | 真实游戏v0.107.1与已安装BaseLib3.4.7程序集 |
| Ivich.Mod.Smoke | 15/15通过 | 全卡注册/克隆/升级/费用、先古入口、控制牌强制消耗/复制、成长重置和原生保存往返、公式预览、双侧奖励、能力叠加 |
| Harmony绑定 | 18个实际补丁目标安装成功并清理 | 验证真实签名和补丁绑定；包含在程序集检查中 |

共87项自动检查通过；Harmony清理另行确认。完整输出保存在本机 artifacts/verification-0.2.0.log。

产物为 dist/Ivich-0.2.0.zip。Ivich.dll已包含Core源码，不要求另装Core；包内没有游戏、Godot或BaseLib程序集。SHA256SUMS记录每个发布文件的校验和。

## 测试方法与限制

程序集检查使用安装目录的实际DLL和游戏公开的ModelDb.Inject，没有伪造游戏API。R05使用真实ToSerializable/FromSerializable往返；测试通过原生InjectTypeIntoCache补齐BaseLib在正式加载时执行的SavedProperty类型注册。此检查证明字段能序列化，不代表完整游戏的保存/继续流程已实测。

无Godot引擎的额外完整Player构造探测在原生依赖访问期间退出139，未纳入通过项；因此不在该环境中强行模拟战斗。纯规则测试验证确定性算法，真实程序集检查验证模型/签名/序列化契约，两者均不能取代实际游戏运行。

未执行：完整ModManager/BaseLib启动、开局战斗、效果链和UI交互、存档继续、原生特殊阶段/复活、多人同步、美术/PCK导出。具体操作见[游戏验证清单](playtest-checklist.md)。

本轮独立审查还修复了：控制牌覆盖原生强制消耗/复制结果、早于本Mod回合开始钩子新建的吟唱被提前推进、100终焉双回调重复请求死亡，以及绝对上限重设被误算为平坦增量。
