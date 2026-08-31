# Schema v14 最终验证截图

本目录只保存当前 Schema v14 最终通过的 GameView 验收图，不保留旧 Schema、失败截图或过程截图。

当前最终通过截图：

- `SchemaV14/reward-choice-1920x1080.png`
- `SchemaV14/selection-card-page2-1920x1080.png`
- `SchemaV14/building-upgrade-1920x1080.png`

三张文件由完整 PlayMode 的真实 Boot→Selection→Gameplay 流程生成；测试固定 GameView 为 1920×1080，并读取 PNG 头校验真实像素尺寸。项目不生成或维护其他分辨率的验收图。

截图必须来自真实 Boot→Selection→Gameplay 流程的 Tick 600 四选一，并同时满足：

- 标题为“四选一”。
- 四个槽位完整，建筑/建筑/资源/援军顺序正确。
- 不显示 CardId、ResourceId 或路径。
- 稀有度、图标、Choice3 兵种组合和数量可读。
- Pop 层无世界穿透，Safe Area、裁切和触控区无问题。

当前截图已通过人工回读和对应 PlayMode。Editor 截图不等同 Android 真机证据。

Selection 第二页截图还必须满足：食物/木材/石矿/铁矿营地、盾卫营地、研究院、箭塔和箭雨的 Sprite 与卡牌名称逐项一致，翻页后不保留第一页 Sprite。

建筑升级截图取自二升三的权威升级过程中，必须同时显示橙金色升级条和右上角暖金尖角图标；建筑菜单、九宫格和触控区不得互相遮挡。当前截图 SHA-256：

- 1920×1080：`FBB61B7FE5139989CAB53152AB1B6BA5308FC14D97CC500A070A2BBC44603DE9`
