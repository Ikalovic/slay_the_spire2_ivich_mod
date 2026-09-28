# 美术接入

## 0.3.0 资源范围

来源为用户确认可用的 `/home/ika/temp/slay_mod/art/masters`。源目录不修改；`assets/selected-art.json` 明确记录每张图片的相对路径和 SHA256，不会在构建时自动选择“最新”版本。

| 用途 | 资源 | 显示方式 |
| --- | --- | --- |
| 82张卡图 | 81张1254×1254；B03为1024×1536 | Godot按比例导入，AtlasTexture留白适配25:19卡窗，保留完整构图 |
| 选角展示 | 初始形态母版v005 | 深蓝背景右侧展示完整母版，左侧保留原生角色说明 |
| 选角/局内头像 | 初始母版面部区域 | AtlasTexture只改变显示区域，不裁切原文件 |
| 形态选择牌 | 魔法使v001、半龙v003 | 完整母版作为选择牌卡图 |
| 初始遗物 | 透明Q版镰刀 | 替换燃烧之血占位图 |

三张角色母版均带灰底，不是透明战斗素材。当前保留游戏已有战斗、营火、商店角色动画及未交付的状态图标/音效。不会把参考图中的棋盘格当作透明通道，也不导入带标注图、废案或审图拼图。

## 构建美术包

使用与本机游戏基础版本一致的 [Godot 4.5.1](https://github.com/godotengine/godot/releases/tag/4.5.1-stable) 导入普通纹理和场景；这些资源不依赖MegaDot专属节点或Godot C#脚本。

```sh
python3 tools/stage_art.py --source /home/ika/temp/slay_mod --check
python3 tools/stage_art.py --source /home/ika/temp/slay_mod
python3 tools/build.py --art --godot /path/to/godot --package
```

`assets/imported` 是被Git忽略的生成工程。手写头像资源和UI场景保存在 `assets/runtime/Ivich`，暂存时一起复制。修改映射、源模板或生成工具后重新暂存，不在生成目录做长期修复。

导入分别限制卡图大纹理与缩略纹理的尺寸，使用无损导入。B03按真实纵向比例添加左右留白，不拉伸、不强行裁成横图。头像贴图保留原尺寸，以保证图集区域坐标准确。

构建工具先导入，再导出 `artifacts/art/Ivich.pck`，最后在隔离Godot工程中挂载PCK并逐项加载纹理、图集和场景。验证证明绑定PCK哈希、资源清单与源工程指纹；缺少证明、资源变化或PCK变化均拒绝发布。

美术ZIP包含 `Ivich.dll`、`Ivich.pck`、`Ivich.json`，清单为 `has_pck=true`。纯代码包文件名带 `-code`，清单为 `false`。它们均不包含游戏或BaseLib程序集。

## 运行时路径

| 用途 | 路径 |
| --- | --- |
| 卡牌缩略图 | `res://Ivich/images/card_portraits/b01.tres` |
| 卡牌大图 | `res://Ivich/images/card_portraits/big/b01.tres` |
| 头像 | `res://Ivich/images/ui/avatar.tres` |
| 选角背景 | `res://Ivich/scenes/character_select.tscn` |
| 局内头像场景 | `res://Ivich/scenes/character_icon.tscn` |
| 形态选择 | `res://Ivich/images/card_portraits/forms/mage.tres`、`dragon.tres` |

原生选角纹理属性要求CompressedTexture2D；先提供真实导入PNG，再在Ivich按钮初始化完成后设置面部AtlasTexture。局内头像、卡图和场景接口可直接接受Texture2D。补丁仅作用于Ivich按钮。

原有 `tools/import_art.py` 仍用于明确准备好游戏尺寸的PNG；C#同时兼容这些路径。完整源图流程优先使用新的图集路径。

## 验证边界

Godot导出、资源加载与独立场景预览不能替代游戏内卡窗、角色按钮交互、不同分辨率和联机测试。请按 [游戏验证清单](playtest-checklist.md) 复核，实际执行记录见 [验证记录](verification.md)。
