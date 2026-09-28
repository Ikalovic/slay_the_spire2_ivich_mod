# 实际程序集烟雾测试

先编译模组，再运行：

```sh
dotnet build src/Ivich.Mod -c Release
dotnet run --project tests/Ivich.Mod.Smoke -c Release
```

也可在 `--` 后指定要检查的 `Ivich.dll`。测试工程读取根目录 `Local.props` 中的 `Sts2Path`、`Sts2DataDir` 与 `BaseLibPath`，与模组工程使用相同的默认路径；这些属性也可通过 MSBuild 的 `-p:` 参数设置。

测试使用安装目录中的实际 `sts2.dll`、`BaseLib.dll`、`GodotSharp.dll` 与 `0Harmony.dll`，不会复制、伪造或替代游戏 API。通过游戏公开的 `ModelDb.Inject`（供测试与模组使用）构造模组模型，检查 BaseLib 牌池注册、初始牌组、隐藏选择牌、升级、典型卡牌数值、专属资源费用，以及实际 Harmony 补丁安装与清理。

这是托管层烟雾测试。它不启动游戏、不调用美术资源加载、不执行战斗，也不声称验证 Godot 场景、输入、多人同步或完整存档/继续流程。完整 `ModelDb.Init` 依赖游戏的 ModManager 启动流程，因此这里使用原版明确提供的注入入口。BaseLib 的全套启动补丁未安装，相关出牌/复制钩子仍需游戏内验证。


0.2.0补充检查：全82张卡，原生先古升华入口，控制牌复制/强制消耗，混合升级能力叠加，C19/U27成长与克隆重置，R05真实序列化往返，以及14类公式卡的空目标预览。SavedProperty注册使用原版SavedPropertiesTypeCache.InjectTypeIntoCache，对应BaseLib正式LatePostInit的注册步骤。

未载入Godot时，完整Player构造会访问引擎依赖；一次独立探测因此退出139，未列入测试通过项。当前检查不使用未初始化的假Player绕过这一限制。
