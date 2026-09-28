# 美术接入

代码开发版使用游戏已有的角色场景、卡图与状态图标。完整角色动画、选择立绘、音效尚未接入；本次未改动 `/home/ika/temp/slay_mod/art` 中的生成任务或图片。

卡图以策划编号定位，升级版复用对应图片：

| 用途 | 游戏资源路径 | 模板给出的尺寸 |
| --- | --- | --- |
| 常规卡图缩略图 | `res://Ivich/images/card_portraits/b01.png` | 250×190 |
| 常规大卡图 | `res://Ivich/images/card_portraits/big/b01.png` | 1000×760 或 500×380 |
| 全幅卡图缩略图 | 同上 | 250×350 |
| 全幅大卡图 | 同上 | 606×852 |

尺寸来自 BaseLib 角色模板说明，不代表已经在本机游戏内检查裁切。人物母版不直接作为战斗场景；三形态的轻Q版动作仍需按游戏场景结构制作。

1. 从已确认图片制作符合尺寸的 PNG；保留原始大图。
2. 复制 `assets/approved.example.json` 并填写明确的图片路径。脚本不会自动挑“最新”版本。
3. 执行 `python3 tools/import_art.py 你的映射.json --check` 检查，再去掉 `--check` 将副本放入忽略 Git 的 `assets/imported`。
4. 使用匹配游戏版本的 MegaDot/Godot 导入 `assets/imported/project.godot`，按 `Resources` 预设导出 `Ivich.pck`。需要先导入资源再导出，不能仅把 PNG 文件塞进 ZIP。
5. 对资源包进行游戏预览后，将发布清单 `has_pck` 改为 `true`，与同名 DLL 一起发布。

卡牌运行时在资源路径存在时加载自有图像，否则保留占位。`tools/package_release.py` 专用于当前纯代码包，会拒绝尚未核验的 PCK 发布配置。人物场景和其余 UI 素材应在资源可用后单独接入 `IvichCharacter` 及状态/资源模型。

当前没有导出过 PCK；上述导出步骤需要在美术交付阶段验证。
