# FortressFrontier 文档入口

本目录只描述 `《城垒争锋》v0.4 / Schema v14` 当前目标与当前状态，不保存旧 Schema 迁移史或阶段流水账。部分专项文档沿用旧文件名，内容中的正式产品名以本页和 `AGENTS.md` 为准。

## 快速理解项目

《城垒争锋》是 Android 横屏优先的 2D 单机城墙攻防游戏，当前 Unity Editor Build Target 已切换为 `Android`。玩家在九格建筑区发展经济、激活营地、逐兵训练并自动推进；双方通过真实采集、加工和公开奖励获得资源，摧毁对方城墙获胜。流程为：

`Boot → Selection → Gameplay → ResultPanel（Gameplay 内暂停）→ Selection`

Schema v14 的当前核心变化是：每图 `3+6+3` 资源节点、等墙距安全点、六波中场激活，以及固定“建筑/建筑/资源/援军”四选一奖励和三档稀有度。

## 阅读顺序

1. `../AGENTS.md`：工程和验证硬约束。
2. `开发进度.md`：当前实现、证据和下一步；新对话必须先读。
3. 根据任务只读一到两份专项文档：

| 任务 | 文档 |
|---|---|
| 产品闭环、规则、数值边界 | `游戏策划案_堡垒前线.md` |
| 地图模式、阶段、AI、敌方经济 | `阶段效果与敌方经济设计.md` |
| 程序集、系统生命周期、确定性算法 | `技术架构.md` |
| Catalog、稳定 ID、快照、事务 | `统一内容配置与系统协作.md` |
| UI、场景比例、Sprite、Safe Area | `美术规范_UI与场景布局.md` |
| 操作流程和交付验收 | `完整游戏闭环与素材替换.md` |
| Unity 版本、包、目录、工具 | `AI/UnityProjectContext.md` |
| 长期视觉目标 | `AI/视觉概念方向_堡垒前线.md` 与 `ConceptArt/` |

## 证据规则

- 设计文档定义目标，不证明功能已实现。
- `开发进度.md` 只按当前 Unity 编译、测试、MCP 回读、截图和构建结果更新。
- Android Build Target 下的 Editor 测试、视觉截图和 Addressables 已有当前证据；它们不自动证明 APK/AAB、签名或真机运行通过。
- Editor 结果不等同 Player、Android 真机、性能或真人体验结果。
- `ValidationScreenshots/` 只保存当前 Schema v14 的最终验收图；未通过截图不保留。
