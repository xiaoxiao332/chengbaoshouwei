# FortressFrontier 项目协作规范

## 当前基线

- 产品：`《城垒争锋》v0.4`（英文标识 `RampartRivals`），静态配置 `Schema v14`；旧文档文件名保留，根命名空间不随产品名改动。
- 发布主体：个人开发者 `岳周洋`；厂商中文名 `孤独的飞鱼的工作室`，英文名 `feiyu`。
- Android 正式 Application ID：`com.feiyu.rampartrivals`；Unity Company/Product：`孤独的飞鱼的工作室` / `城垒争锋`。
- Android 正式签名：外置 PKCS12 keystore `C:/Users/Administrator/AppData/Local/RampartRivals/Signing/rampartrivals-release.keystore`，alias `rampartrivals-release`，证书 SHA-256 `2A:BE:DF:E1:27:10:AD:0F:08:9B:5D:4A:4B:85:AB:FA:E3:2A:FC:CC:22:D0:AC:8F:1E:75:E1:42:F6:B2:17:16`。密码只存 Windows Credential Manager，不写入仓库、文档或日志。
- Unity 重启后由 `Assets/Game/Editor/AndroidSigningCredentialLoader.cs` 从 Windows Credential Manager 的既定 Generic Credential 恢复签名密码；Android 构建前置检查在凭据缺失时必须直接失败，禁止回退到未签名或其他证书。
- Unity：`6000.3.11f1`；URP 2D；Input System；uGUI；Addressables 2.9.1，本地内容。
- 平台：当前 Unity Editor Build Target 已切换为 `Android`，横屏优先，后续 Steam；Editor 验证不能替代 Android Player/真机验证。
- 根命名空间和程序集前缀：`FortressFrontier`。

## TapTap / TapADN 发布约束

- 仅接入结算页玩家主动触发的激励视频；不接横幅、插屏、开屏或强制广告，不因广告影响核心单机流程。
- TapADN Dirichlet Mediation Unity SDK 固定为 `5.1.2.3`；导入包 MD5 `73E997C3410705F74F4366AE27236E14`，SHA-256 `E19D3CDC0C3D7C07E25E830AA9F49D7B00286ACEA10900E525E95F8AA8E6D4B4`。
- 广告只在玩家明确同意隐私政策并点击观看后初始化；未配置 Media ID、Media Key、奖励广告位 ID、隐私政策 URL 中任一项时入口必须隐藏。
- 激励奖励为本局“完成奖励 + 胜利奖励”经模式倍率后的 50% 金币，四舍五入采用整数规则；不包含首通奖励；按 `MatchId` 持久化且最多领取一次。
- Android Manifest 仅保留广告联网所需的 `INTERNET` 与 `ACCESS_NETWORK_STATE`；不得为广告引入定位、电话、蓝牙或 Wi-Fi 状态权限。
- 项目没有自有联网服务、登录或排行榜；广告域名由官方 SDK 管理，不在 Application ID 中使用或虚构域名。上架仍需准备可公开访问的隐私政策 URL，并单独确认 TapTap 防沉迷材料要求。

## 新对话必读

1. `AGENTS.md`：硬约束。
2. `Docs/README.md`：文档地图和当前产品摘要。
3. `Docs/开发进度.md`：真实实现、最后验证证据、未验证改动和下一步。
4. 按任务读取专项文档；文档不能代替 `Assets/`、Prefab、配置资产和测试证据。

## 架构硬约束

- `GlobalManager` 仅是 Boot 场景持久组合根；`UIManager` 仅管理面板生命周期、层级和 UI 状态。
- 业务系统继承 `GameSystemBase`，按需实现 Tick、暂停、存档和场景生命周期接口。
- 禁止万能 Manager、平行单例、静态可变全局状态、公共 Service Locator、名称/Tag/场景名查找式跨系统 API。
- 依赖由 Bootstrap/Scene Installer 显式构造注入；不通过 Script Execution Order 修复初始化。
- `FortressFrontier.Core` 不引用 Unity；Runtime、Presentation、Infrastructure 只向 Core/Runtime 方向依赖；Bootstrap 是唯一组合层；禁止循环程序集依赖。

## UI、资源与数据

- 面板继承 `UIPanelBase`。根节点全屏拉伸；运行时只设置父级、归零位置/offset、恢复单位缩放，不重建子布局。
- UI 层固定：`Bg=0`、`Window=100`、`Pop=200`、`Over=300`；交互内容位于 Safe Area。
- 业务程序集禁止直接调用 `Addressables.*` 或 `Resources.Load`；Addressables API 只在 Infrastructure。
- 业务只传稳定强类型 ID、`ResourceKey`、`SceneKey`；禁止保存物理路径、Handle、Unity Object 或场景实例到运行状态/存档。
- ScriptableObject 只保存静态配置；本局状态为纯 C# Runtime State；永久状态为 Save DTO。
- 对象池实现必须 `internal`，业务代码只持有并释放实例租约。

## C# 与序列化

- 类型/成员 PascalCase，字段 `_camelCase`；Unity 引用使用 `[SerializeField] private`。
- 初始化幂等；事件订阅/取消成对；异步使用 `Task`、`CancellationToken` 并处理异常、取消和卸载。
- 修改序列化字段名使用 `FormerlySerializedAs` 并检查所有引用资产。
- 确定性逻辑使用稳定 ID 排序、独立随机流和整数/定点计算，不依赖 Dictionary 顺序或跨平台浮点指数。

## 配置与运行时边界

- 唯一根配置：`Assets/Game/Content/Config/GameContentConfig.asset`。
- SchemaVersion 必须精确等于 `ContentConstants.ExpectedSchemaVersion`；已发布稳定 ID 不复用、不改名。
- 创建对局时冻结不可变 Match 快照；运行中不热切换编辑器资产。
- UI 只消费 ViewModel/Snapshot 并发送 Command；不能读取业务 ScriptableObject、直接改库存/手牌/单位或计算胜负。
- AI 只能提交正式业务命令；目标系统必须再次原子校验。敌方收入只来自公开库存、真实采集、加工和奖励，所有收支进入账本。
- 敌方城墙后不生成建筑 GameObject；设施为纯数据状态。

## 当前 Schema v14 核心事实

- 每图资源节点固定为玩家安全区 3、中场候选 6、敌方安全区 3；敌我安全节点到各自城墙等距。
- 安全节点初始容量 100，耗尽 180 秒后以容量 30 原位刷新；中场按 60/60/120/180/240/300 秒无放回激活，耗尽 45 秒后原位刷新。
- 奖励首次 60 秒，后续按热度冷却 60/55/50/45/45 秒；固定四槽：建筑、不同建筑、加工资源、援军部署。
- 稀有度为 Common/Rare/Epic，只影响显式资源数量和既有援军模板，不修改单位/建筑基础属性。
- 资源奖励直接入库；建筑/援军受六张道具手牌限制；援军仅在合法部署事务成功后消耗。

## 修改与验证

- 不编辑 `Library/`、`Temp/`、`Logs/` 等生成目录。
- 脚本编辑用补丁；Prefab、场景、ScriptableObject、Importer、Addressables 和 ProjectSettings 通过 Unity MCP/Editor API 修改并回读，不手改序列化 YAML。
- 工作区可能有用户改动；保留无关改动，不使用破坏性 Git 命令。
- 每次脚本改动后等待 Unity 编译并检查 Console；纯 C# 优先 EditMode，MonoBehaviour/UI/场景/生命周期使用 PlayMode。
- 当前验证默认保持 `Android` Build Target；未经任务明确要求不得为图省事切回 Standalone。切换平台本身只证明 Editor 目标状态，不证明 SDK/NDK/OpenJDK、Player 构建或真机运行通过。
- `Docs/开发进度.md` 必须分开记录“最后一次已验证基线”和“当前未验证改动”。未实际执行的编译、测试、截图、构建不得标记通过。
- 正式平衡矩阵必须分成 36 个 50 局 Explicit 子批；禁止单作业运行 300 或 1800 局。
- 高风险资产修改后必须检查 Prefab/资产回读、内容校验、Console 和相关测试。

## 文档职责

- 产品行为：`Docs/游戏策划案_堡垒前线.md`。
- 地图模式、阶段、AI、敌方经济：`Docs/阶段效果与敌方经济设计.md`。
- 程序集、生命周期、运行时算法：`Docs/技术架构.md`。
- Catalog、稳定 ID、快照、事务：`Docs/统一内容配置与系统协作.md`。
- UI、布局、素材、动画：`Docs/美术规范_UI与场景布局.md`。
- 跨系统操作与验收：`Docs/完整游戏闭环与素材替换.md`。
- 实现和证据：`Docs/开发进度.md`。

冲突顺序：本文件硬约束 → 主策划产品行为 → 专项架构/配置/美术 → 开发进度事实。发现冲突先同步文档，再改代码或资产。
