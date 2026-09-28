# 编译依据

检查日期：2026-09-28。

| 依赖 | 本次实际使用 |
| --- | --- |
| Slay the Spire 2 | v0.107.1，commit `59260271` |
| BaseLib | 本机创意工坊安装 3.4.7 |
| .NET SDK | 9.0.318，目标 net9.0 |
| GodotSharp | 使用游戏随附程序集，4.5.1 系列 |

参考实现来自维护者源代码：

- [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2/tree/55ca2c606e6c78dd39689a5cf979b243a49652e7)：角色与卡池结构。
- [BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2/tree/c070755d11ec062f6c97c4dda1a205d975ed066a)：模型自动注册、自定义资源、代码本地化、营火扩展。
- [环境安装说明](https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup)：C# 开发工具与资源打包约定。

实际命令和虚方法签名以已安装的 `sts2.dll`、`BaseLib.dll` 和 `sts2.xml` 核验。临时研究与反编译文件位于仓库外，不作为发布内容。发布包不含游戏、BaseLib 或 Godot 的程序集与原始资源；用户从自己的游戏和 BaseLib 安装加载它们。

当前美术占位由 `PlaceholderCharacterModel` 引用游戏已经拥有的资源，不重新分发这些资源。代码本地化直接提供中文文本，因此纯代码开发包不要求空 PCK 文件。切换到自有 PNG/场景时，需要先完成 Godot 导入与 PCK 导出，再更新清单的 `has_pck`。


0.2.0额外核验：原版欧罗巴斯OptionPool3中的ArchaicTooth、BaseLib的ITranscendenceCard/GetTranscendenceTransformedCard；原版CardModel的复制/重放/返回牌堆、SavedProperty序列化，CreatureCmd的生命/死亡与PlayerCmd.EndTurn。先古升华源选择B03，属于本Mod明确记录的实现选择。
