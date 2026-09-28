# 依维希 — Slay the Spire 2 角色 Mod

当前版本：**0.3.0 美术接入预览**，目标为本机游戏 **v0.107.1 / BaseLib 3.4.7**。美术包包含82张卡图、选角展示、面部头像、三形态母版与镰刀遗物图。**战斗角色动作仍为占位；尚未完成游戏内开局、战斗、存档与联机验证。**

## 当前内容

- 依维希角色：88 初始生命，11 张初始牌组，初始遗物“月眼之镰”。
- **82 种已接入卡牌**：B01–B06、C01–C28、U01–U28、R01–R16、T01–T03、A01；其中72张属于常规奖励池。
- 初始／魔法使的魔力、半龙的怒气；形态费用限制、生命代价、距离、振奋、冰冻与冻结。
- 营火永久进阶、打击转换为龙咬、六种镰刀铭刻的选择与效果、局内进度保存字段。
- 17种能力、吟唱与完整效果封存，实付X与单卡成长快照，法库释放及控制牌回收。
- 欧罗巴斯「古老牙齿」可升华镰柄蓄能为二相一身，接入临时换形、1:1资源转换、精确生命比例和终焉上限联动。
- 独立规则层和自动化检查，以及锁定源文件版本/校验和的美术导入与PCK验证工具。

已将原策划完整卡池接入真实游戏模型。美术版选角头像已与铁甲战士区分；三形态源图带灰底，用于角色展示和形态选择。原生引擎中的UI、战斗组合、保存/继续及联机需要按清单实测。

详细边界见 [实现状态](docs/implementation-status.md)，策划来源见 [需求摘录](docs/reference/design-summary.md)。

测试命令、实测结果与覆盖范围见 [验证记录](docs/verification.md)。

## 构建

需要 Python 3、.NET 9 SDK、自己安装的游戏和 BaseLib；美术包还需要 Godot 4.5.1 和已确认的源图。本仓库不包含游戏、依赖 DLL 或原始美术大图。

复制 `Local.props.example` 为 `Local.props`，按机器设置游戏根目录 `Sts2Path`、程序集目录 `Sts2DataDir`（可选）和 BaseLib 文件夹 `BaseLibPath`。默认配置对应本机 WSL 可访问的 Windows 游戏安装。

```bash
python3 tools/stage_art.py --source /home/ika/temp/slay_mod --check
python3 tools/stage_art.py --source /home/ika/temp/slay_mod
python3 tools/build.py --art --godot /path/to/godot --package
```

该命令运行检查、针对真实游戏程序集编译、导出并验证资源，生成 `dist/Ivich-0.3.0.zip`。构建不会自动安装或修改游戏目录。若 `dotnet` 不在 PATH，脚本会检查 `~/.dotnet/dotnet`，也可设置 `IVICH_DOTNET`。不带 `--art` 的 `--package` 只生成使用占位资源的 `-code.zip`。

只运行无需游戏的规则检查：

```bash
python3 tools/build.py --rules-only
```

## 安装开发包

1. 确保游戏可加载 BaseLib 3.4.7。
2. 退出游戏，将发布 ZIP 中的 `Ivich` 文件夹放到游戏的 `mods` 文件夹中；更新旧版时一同替换 `Ivich.dll`、`Ivich.pck` 和 `Ivich.json`。
3. 确认这三个文件直接位于 `mods/Ivich/`，没有多套一层文件夹。
4. 启用依维希及 BaseLib，重启后选择白发少女头像。角色多时可横向滚动；信息为依维希、88生命、月眼之镰。

美术 ZIP 的清单为 `has_pck=true`，必须同时安装PCK；仓库源清单保留 `false` 以支持纯代码构建。打包时只修改ZIP内的清单，并校验资源包与当前资源工程一致。安装后按 [游戏验证清单](docs/playtest-checklist.md) 确认显示和玩法。

## 美术后续接入

按照 [美术接入说明](docs/art-integration.md) 更新明确的资源映射。卡图以B01等策划编号定位，升级版复用原图；未找到资源时自动使用占位。透明三形态战斗角色、动作、状态图标与音效仍需制作和接入。

## 代码目录

| 目录 | 用途 |
| --- | --- |
| `src/Ivich.Core` | 不依赖游戏的确定性规则及法术队列 |
| `src/Ivich.Mod` | BaseLib 模型、真实游戏命令、钩子、营火与奖励 |
| `content/cards.json` | 全82张策划目录与来源信息 |
| `content/runtime-enabled.json` | 当前82张启用清单 |
| `tests` | 规则、数据、打包及真实程序集模型检查 |
| `tools` | 构建、打包、策划抽取及美术导入 |

API依据和固定版本见 [依赖记录](docs/reference/dependencies.md)。
