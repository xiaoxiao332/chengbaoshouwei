# Unity 项目上下文

## 环境

- 项目：FortressFrontier / 《城垒争锋》v0.4 / Schema v14。
- Unity：6000.3.11f1。
- 渲染：URP 2D。
- 输入：Unity Input System。
- UI：uGUI。
- 资源：Addressables 2.9.1，本地内容。
- 当前 Unity Editor Build Target：Android（2026-08-29 已确认）。
- 目标：Android 横屏优先，后续 Steam。
- Android PlayerSettings：横屏；ARM64；IL2CPP；Min SDK 26；Target SDK 由 Unity 自动选择。

## 主要目录

- `Assets/Game/Core`：纯 C# 核心标识和系统接口。
- `Assets/Game/Runtime`：玩法、配置快照和场景运行时。
- `Assets/Game/Presentation`：UI、ViewModel 和世界表现。
- `Assets/Game/Infrastructure`：Addressables、存档和平台服务。
- `Assets/Game/Bootstrap`：Boot/Selection/Gameplay 组合。
- `Assets/Game/Editor`：内容 Authoring、校验和 MCP 入口。
- `Assets/Game/Content`：Catalog、Prefab 和根配置。
- `Assets/Game/Tests`：EditMode、PlayMode 和 Explicit 平衡测试。
- `Assets/Game/Art/Formal/PNG`：正式运行 PNG。

## 场景

- `Assets/Game/Scenes/Boot.unity`
- `Assets/Game/Scenes/Selection.unity`
- `Assets/Game/Scenes/Gameplay.unity`

流程：`Boot → Selection → Gameplay → ResultPanel → Selection`。

## 关键资产

- 根配置：`Assets/Game/Content/Config/GameContentConfig.asset`
- Gameplay UI：`Assets/Game/Content/Prefabs/UI/Gameplay.prefab`
- 援军组合视图：`Assets/Game/Content/Prefabs/UI/ReinforcementCardVisual.prefab`
- Schema v14 奖励图标：`Assets/Game/Art/Formal/PNG/SchemaV14/`

## 程序集边界

- Core 不引用 Unity。
- Runtime 依赖 Core。
- Presentation/Infrastructure 依赖 Core/Runtime。
- Bootstrap 是唯一组合所有运行时程序集的位置。
- Editor 和 Tests 不进入 Player 运行链。

## Unity 自动化

优先使用 Unity MCP：

- 刷新、编译、Console。
- Prefab、ScriptableObject、Importer 和 Addressables 修改/回读。
- EditMode/PlayMode 测试。
- Editor 代码执行和内容校验。

相关菜单：

- `Fortress Frontier/Content/Apply Schema v14 Config Only`
- `Fortress Frontier/Schema v14/Import Reward Sprites`
- `Fortress Frontier/Schema v14/Reconcile Reward Choice Visuals`
- `Fortress Frontier/Content/Validate Config`
- `Fortress Frontier/Content/Validate Project Content`
- `Fortress Frontier/Addressables/Build Local Content`

菜单调度若只返回“Attempted”但资产未变化，应通过 Unity Editor 域调用同一 Authoring 入口并立即回读；不能手改 YAML。

## 测试注意事项

- 当前 Unity MCP 的整程序集 EditMode 可能实际启动 36 个 Explicit 正式平衡子批；常规完整回归必须按非平衡夹具运行，并仅单独加入 `QuickRegression_SixCombinationsTenSeeds_ProducesCadenceReport`。
- 正式矩阵为 6 个地图/模式组合×6 批×50 局。禁止一次运行 300 或 1800 局。
- PlayMode 若停在 InitTestScene 且测试总数/当前测试为空，应清理孤立作业；确认无资产写入后再重启 Unity/MCP。
- 测试结果、Console、Prefab 回读和构建证据必须写入 `Docs/开发进度.md`。

## 当前自动化验证状态

- Android 目标完整非 Explicit EditMode：按夹具精确过滤，130/130 passed、0 failed；未把 Explicit 正式矩阵计入本轮结果。
- Android 目标完整 PlayMode：14/14 passed。
- Schema/Expected 均为 14；内容与项目校验问题数均为 0。
- Android Addressables Local Content 已成功重建；Local-Core、Local-Scenes、Local-UI 产物和 Catalog 均存在，重复地址为 0；`Gameplay.prefab` 递归依赖包含建筑升级图标。
- 项目固定以 1920×1080 生成奖励、Selection 和建筑升级截图，并已按真实像素尺寸人工回读；不维护其他分辨率的验收分支。
- 奖励视觉 Authoring 已通过两次执行资产哈希不变的幂等验证。

## 当前限制

- 当前活动平台为 Android，Editor 脚本、测试和 Addressables 已验证；SDK、NDK、OpenJDK 的完整 Player 工具链仍未通过 APK/AAB 构建确认。
- 尚无 Android APK/AAB 构建、安装启动或真机验证证据；正式 Android 包名和私有签名仍未配置。
- 当前临时 Android Application ID `com.DefaultCompany.` 保持不变，由项目负责人正式构建前处理。
- Editor 测试不能证明 Player、Android 性能、触控或真机稳定性。
- 项目可能没有 Git 元数据；状态复核依靠具体文件、Unity 回读和测试证据。

## 本次平台事实来源

- `Library/EditorUserBuildSettings.asset`：活动 Build Target/Group 为 Android（只读核对，生成目录不纳入修改）。
- `ProjectSettings/ProjectSettings.asset`：横屏、ARM64、Android IL2CPP、Min SDK 26、Target SDK 自动选择。
- `ProjectSettings/ProjectVersion.txt`：Unity 6000.3.11f1。

最后核对：2026-08-29；当前目录无 Git 元数据，无法记录 commit。
