# AGENTS.md

## 项目简介（给 Codex）
- 这是一个 Unity 项目，当前核心内容在：
  - `Assets/Scenes/SampleScene.unity`
  - `Assets/Scripts/`
  - `Assets/Dialogue/`
- Unity 版本：`6000.3.6f1`

## 必须遵守的协作规则
1. 用户编程基础扎实，但对 Unity 编辑器不熟。
2. 任何涉及 Unity 编辑器的操作，都要提供详细的“点哪里、改什么”的步骤。
3. 需要改 Unity Scene 时，**不要直接修改 `.unity` 场景文件**；改为输出可执行的 Unity 手动操作指南，让用户在编辑器里完成。

## 工作方式
- 可以直接修改：`Assets/Scripts/*.cs`、`Assets/Dialogue/*` 等文本资源。
- 需要 Scene 调整时：
  - 先说明目标和影响范围。
  - 再给出逐步点击路径（窗口/层级/Inspector 字段名/目标值）。
  - 最后给出验证步骤（Play 后应看到什么结果）。

## Unity 手动步骤格式（建议）
每次需要用户在 Unity 中操作时，按这个结构输出：
1. 打开位置：例如 `Assets/Scenes/SampleScene.unity`（双击打开）
2. 层级定位：在 `Hierarchy` 中选中具体对象（写清对象名和层级路径）
3. Inspector 修改：写清组件名、字段名、要填的值
4. 关联资源：需要拖拽时，写清“从 Project 面板哪个资源拖到哪个槽位”
5. 运行验证：点击 `Play` 后预期看到的现象

## 沟通要求
- 避免只说“去 Inspector 里改一下”。
- 必须给可复现、可逐项勾选的步骤。
- 如果存在多个可行方案，默认给“最简单、最不容易点错”的方案。
