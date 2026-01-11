# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目概述

这是一个基于 Unity 的飞机大战游戏，灵感来自微信飞机大战。项目使用 Unity 2020.3.48f1c1 构建，并包含 Mirror 网络库以支持潜在的多人游戏功能。

## 构建和运行

### 打开项目
- 使用 Unity 2020.3.48f1c1 或兼容版本打开项目
- 主场景位于 `Assets/Scenes/SampleScene.unity`
- 确保配置了 .NET 4.x 运行时（Project Settings > Player > Other Settings）

### 运行游戏
- 在 Unity 编辑器中打开 `Assets/Scenes/SampleScene.unity`
- 点击 Unity 编辑器中的 Play 按钮运行游戏
- 游戏将从 StartGamePanel UI 开始

## 架构设计

### 核心框架模式

项目使用**基于管理器的单例架构**和事件驱动系统：

1. **入口点**：`Main.cs` (Assets/Scripts/Main.cs)
   - 挂载在场景中的 GameObject 上
   - 在 Start 时调用 `GameMgr.instance.Main()`
   - 每帧调用 `GameMgr.instance.Update()`

2. **管理器单例**：所有管理器都遵循单例模式，通过 `instance` 属性访问
   - `GameMgr`：游戏状态和生命周期管理中心
   - `PanelMgr`：UI 界面的显示/隐藏管理
   - `ConfigMgr`：XML 配置文件的加载和解析
   - `ResourceMgr`：Unity Resources 文件夹资源加载和缓存
   - `EventDispatcher`：事件订阅和分发系统

3. **事件系统**：`EventDispatcher.cs` (Assets/Scripts/Framework/EventDispatcher.cs)
   - 订阅事件：`EventDispatcher.instance.Regist(eventName, handler)`
   - 取消订阅：`EventDispatcher.instance.UnRegist(eventName, handler)`
   - 触发事件：`EventDispatcher.instance.DispatchEvent(eventName, params)`
   - 事件名称定义在 `EventDef.cs` 中

### 游戏状态管理

游戏状态由 `GameMgr` 管理 (Assets/Scripts/Framework/GameMgr.cs)：
- `Ready`：初始状态，显示开始菜单
- `Playing`：游戏进行中
- `Pause`：游戏暂停
- `End`：游戏结束状态

状态转换：
- `StartGame()`：Ready → Playing
- `PauseGame()`：Playing → Pause
- `ContinueGame()`：Pause → Playing
- `GameOver()`：Playing → End
- `RestartGame()`：End → Playing（清除所有对象）
- `BackToHomePanel()`：任意状态 → Ready（清除所有对象）

### UI 界面系统

界面继承自 `BasePanel` (Assets/Scripts/Framework/PanelMgr.cs)：
- 界面在 Canvas 下动态创建
- 每个界面从 `Assets/Resources/Panels/` 加载预制体
- 重写 `SetUi(PrefabSlot slot)` 来绑定 UI 元素
- 重写 `OnShow()` 和 `OnHide()` 处理生命周期钩子

界面生命周期：
1. `ShowPanel<T>()` 在需要时创建界面 GameObject
2. 使用 `m_panelResPath` 从 Resources 加载预制体
3. 调用 `SetUi()` 从 PrefabSlot 绑定 UI 元素
4. 调用 `OnShow()` 进行自定义初始化
5. `HidePanel<T>()` 销毁界面 GameObject 并调用 `OnHide()`

### 飞机系统

飞机层次结构 (Assets/Scripts/Aircraft/)：
- `BaseAircraft`：基类，包含碰撞、血量和爆炸逻辑
- `PlayerAircraft`：玩家控制的飞机，处理输入
- `EnemyAircraft`：AI 控制的敌机
- `AircraftFactory`：使用工厂模式创建飞机实例

飞机通过 `AircraftFactory.CreateAircraft(AircraftType)` 创建：
1. 从 Resources 加载预制体
2. 在工厂根 GameObject 下实例化
3. 返回配置好的飞机实例

### 敌机生成系统

`EnemyGenerator` (Assets/Scripts/Aircraft/EnemyGenerator.cs)：
- 基于关卡配置使用加权随机选择
- 按 `LevelConfig.EnemySpawnTime` 定义的间隔生成敌机
- 敌机类型、速度和血量在 `GameConfig.xml` 中配置
- `WeightedRandom` 类处理基于概率的敌机选择

### 子弹系统

两个独立的子弹系统：
- **玩家子弹**：`PlayerBulletGenerator` 管理玩家发射的子弹
- **敌机子弹**：`EnemyBulletGenerator` 管理敌机发射的子弹
- 子弹使用对象池模式进行复用以提高性能

### 配置系统

游戏配置基于 XML (Assets/Resources/Config/GameConfig.xml)：
- `GameConfig`：根配置，包含关卡数组
- `LevelConfig`：每个关卡的设置（ID、分数阈值、敌机生成时间、敌机配置）
- `EnemyConfig`：敌机类型设置（索引、速度范围、血量、权重）

配置通过 `ConfigMgr.instance.Load()` 加载，使用 `XmlSerializer` 反序列化。

### 资源加载

所有游戏资源从 `Assets/Resources/` 文件夹加载：
- `Resources/Panels/`：UI 界面预制体
- `Resources/Player/`：玩家飞机预制体
- `Resources/Enemy/`：敌机预制体
- `Resources/Bullet/`：子弹预制体
- `Resources/Config/`：XML 配置文件

`ResourceMgr` 在字典中缓存已加载的资源以避免重复加载。

## Mirror 网络库

项目包含 Mirror 网络库 (Assets/Mirror/)，但目前尚未集成到游戏逻辑中。Mirror 是一个 MMO 级别的网络库，可用于多人游戏功能。

Mirror 文档：https://mirror-networking.gitbook.io/docs/

## 关键设计模式

1. **单例模式**：所有管理器使用延迟初始化的单例
2. **工厂模式**：`AircraftFactory` 用于创建飞机实例
3. **观察者模式**：`EventDispatcher` 用于解耦的事件通信
4. **对象池模式**：子弹生成器复用子弹实例
5. **模板方法模式**：`BasePanel` 和 `BaseAircraft` 定义生命周期钩子

## 重要说明

- 游戏使用 Unity 的 2D 物理系统（Collider2D、Rigidbody2D）
- 动画通过 Unity 的 Animator 组件处理
- UI 使用 Unity 的 Canvas 系统构建（RectTransform）
- 代码库包含中文注释，README.md 中有博客文章链接
- 游戏进度基于分数，自动进行关卡转换
