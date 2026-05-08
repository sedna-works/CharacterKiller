# CharacterKiller 发展路线图

> 基于代码审查与 AI 时代产品演进方向整理。按优先级与实现阶段排列。

---

## 🔴 高优先级 — 稳定性与工程基础

### 1. LLM 输出容错（JSON 解析单点故障）
- **状态**: 🔲 待实现
- **描述**: `SkillsPipeline` 当前完全依赖 LLM 返回合法 JSON，解析失败直接抛异常并仅记录前 2000 字符。需建立多层容错机制。
- **动机**: 当前最大稳定性风险。LLM 偶尔会在 JSON 外包裹解释性文字，或产生语法错误（如 trailing comma）。
- **关键实现点**:
  - 优先使用 OpenAI / DeepSeek 的 `json_mode` 或 `response_format: {type: "json_object"}` 强制结构化输出
  - 若解析失败，自动提取 Markdown 代码块（```json ... ```）后重试解析
  - 引入二次修复调用：将原始响应 + 错误信息发送回 LLM，请求修正为合法 JSON
  - 解析失败时完整保存原始响应到 `output/errors/{角色名}_{timestamp}.txt`，便于调试

### 2. 实现 TiktokenEstimator（精确 Token 计数）
- **状态**: 🔲 待实现
- **描述**: 项目已引用 `Microsoft.ML.Tokenizers` 2.0.0，但只实现了 `CharBasedEstimator`。需基于该库实现 `TiktokenEstimator`。
- **动机**: 字符近似估算对大文本切片精度不足。中/英/日混合剧本的 token 比例差异大，精确计数可避免 chunk 超限或切片过碎。
- **关键实现点**:
  - 在 `CharacterKiller.Infrastructure/src/Tokenization/` 下新建 `TiktokenEstimator.cs`
  - 实现 `ITokenEstimator` 接口，使用 `Microsoft.ML.Tokenizers` 的 `Tokenizer` 类
  - 支持通过配置切换估算器（`"Estimator": "tiktoken"` / `"char"`）
  - `ServiceRegistrar` 中根据配置注册对应实现
  - 性能注意：对大文本（500MB 上限）需避免重复 tokenize，可缓存结果

### 3. 移除或激活僵尸字段
- **状态**: 🔲 待实现
- **描述**: `TaskConfig` / `JobConfig` 中的 `VndbCharacterId` 和 `SkillsTaskState` 中的 `ConversationHistory`、`IterationCount` 当前完全未被使用。
- **动机**: 避免配置误导用户，减少维护负担。
- **关键实现点**:
  - 方案 A（推荐）：实现 VNDB 数据拉取。在 `SummarizePipeline` 开始时，若配置了 `VndbCharacterId`，调用 VNDB API 获取角色基础信息（身高、生日、三围、声优等），作为额外 context 注入 summarize prompt
  - 方案 B：若短期内不实现，从配置模型和文档中移除这些字段，避免混淆
  - `ConversationHistory` 如为多轮迭代预留，应加 TODO 注释并关联下方"多轮迭代精炼"条目

---

## 🟡 中优先级 — 产品功能进化

### 4. 单元测试覆盖
- **状态**: 🔲 待实现
- **描述**: 当前项目零测试。对纯逻辑类优先补充单元测试。
- **动机**: 工程成熟度基础。`TextSlicer`、`TokenLimiter` 等类有明确边界条件和可预期行为，适合单元测试。
- **关键实现点**:
  - 新建 `CharacterKiller.Tests` 项目（xUnit）
  - 优先测试类（按 ROI 排序）：
    1. `TextSlicer` — 空文本、超长单句、段落边界、混合语言、Emoji、连续换行
    2. `TokenLimiter` — 二分查找截断正确性、边界 token 数、空字符串
    3. `RetryPolicy` — 退避延迟公式、异常传播、成功即停止
    4. `JsonCheckpointStore` — 断点恢复逻辑、文件缺失校验、并发写安全
  - LLM 相关类（`OpenAiCompatibleClient`、`SummarizePipeline`）引入 Moq 或手工 mock `ILlmClient`

### 5. Prompt 模板外置化
- **状态**: 🔲 待实现
- **描述**: 当前 `SummarizePromptBuilder`、`RoleplayPromptBuilder`、`TemplatePromptBuilder` 的 prompt 全部是硬编码 C# 字符串。
- **动机**: 不同 LLM 模型需要不同 prompt 策略；用户需要自定义输出风格；社区可共享 prompt 模板。
- **关键实现点**:
  - 抽象 `IPromptBuilder` 接口：`string BuildSummarizePrompt(...)`, `string BuildSkillsPrompt(...)`
  - 将当前硬编码 prompt 提取为默认模板文件（`Templates/default/summarize.md`、`roleplay.md`、`template.md`）
  - 支持配置指定模板目录：`"TemplatesDirectory": "./my_templates"`
  - 模板引擎使用简单字符串替换（如 `{{characterName}}`、`{{chunkText}}`），无需引入重型模板库
  - 默认模板嵌入为资源文件，无外置模板时自动回退

### 6. 多角色并行提取
- **状态**: 🔲 待实现
- **描述**: 当前一次任务只处理一个角色。Galgame 通常有多个可攻略角色，用户需多次运行。
- **动机**: 大幅提升批量处理效率。Summarize 阶段只需读一次文本，可提取所有角色信息。
- **关键实现点**:
  - 新增配置字段：`"CharacterNames": ["角色A", "角色B"]`（与现有 `"CharacterName"` 兼容）
  - 或：在 Summarize 阶段让 LLM 自动识别所有出场角色，输出角色列表供用户选择
  - `SummarizePipeline` 输出按角色分离的 summary 片段（或一个总 summary 中包含所有角色标签）
  - `SkillsPipeline` 对多个角色并行执行（复用现有的 `MaxJobConcurrency` 机制）
  - 输出目录：`output/roleplay/角色A-main/`、`output/roleplay/角色B-main/`
  - 额外价值：生成角色关系矩阵 `output/relationship_matrix.md`

### 7. 多轮迭代精炼（Iterative Refinement）
- **状态**: 🔲 待实现
- **描述**: 利用 `SkillsTaskState.ConversationHistory` 和 `IterationCount` 的设计意图，实现质量自检循环。
- **动机**: 单次 LLM 调用难以保证角色设定的一致性（尤其是 template 模式的去剧情化处理）。多轮迭代可显著提升输出质量。
- **关键实现点**:
  - 第一轮：LLM 生成初稿 JSON
  - 第二轮：将初稿 + 原始 summary 发送给 LLM，要求以"质检员"身份检查矛盾、遗漏、OOC（out of character）问题
  - 第三轮：根据质检反馈修正，输出最终版
  - 配置项：`"SkillsIterations": 1`（默认，兼容当前行为），设为 2/3 时启用迭代
  - 迭代间的对话历史存入 `SkillsTaskState.ConversationHistory`，支持断点续传（迭代级 checkpoint）

### 8. 输入格式扩展
- **状态**: 🔲 待实现
- **描述**: 当前 `LocalFileReader` 只读取纯文本并强制按 UTF-8 解码。
- **动机**: 支持更多剧本文本来源，降低用户预处理成本。
- **关键实现点**:
  - 自动编码检测：使用 `Ude` 库或 .NET 的 `System.Text.Encoding` 试探解码（Shift-JIS、GBK、Big5、UTF-8）
  - JSON 格式剧本：部分解包工具输出结构化 JSON（字段如 `Character`、`Text`、`Scene`），需实现 `IFileReader` 新实现或预处理转换器
  - Markdown / 带标记剧本：若文本中已有 `【角色名】` 或 `**场景标题**` 等标记，可在 summarize prompt 中提示 LLM 利用这些结构

---

## 🟢 低优先级 / 长期 — AI 时代方向

### 9. 生态对接：SillyTavern / RisuAI / Claude Artifacts
- **状态**: 🔲 待研究
- **描述**: 当前输出为 skill 文件夹（Markdown 文件）。主流角色扮演平台有各自的角色卡格式。
- **动机**: 降低用户从"提取角色"到"实际使用"的摩擦，扩大用户群。
- **关键实现点**:
  - **SillyTavern**: 输出 PNG 格式角色卡（嵌入 JSON metadata，base64 编码）。结构包含 `name`、`description`、`first_mes`、`mes_example`、`personality`、`scenario` 等字段。需研究其 PNG 编码头格式（tEXt chunk: `chara`）。
  - **RisuAI**: 支持 JSON 角色定义，可直接映射已提取的字段。
  - **Claude Artifacts / Custom Instructions**: 输出可直接粘贴的 system prompt 格式。
  - 新增 `"OutputFormat": "skill" | "sillytavern" | "risu" | "claude"` 配置项。

### 10. 动态角色引擎（超越静态 Skill 包）
- **状态**: 🔲 待研究
- **描述**: 从静态 Markdown 文件进化为可动态演化的角色状态。
- **动机**: AI 角色扮演的核心痛点是长期记忆和一致性。静态 skill 包无法表达"角色随经历成长"。
- **关键实现点**:
  - 从剧本提取 **时间线 + 事件因果图**（事件 A 导致角色性格转变 B），输出为有向图（如 JSON Graph 或 Mermaid）
  - 生成 **记忆优先级权重**：标记核心记忆（如"父母双亡"）和日常记忆（如"今天吃了什么"）
  - 输出 **RAG 友好格式**：按场景/事件切分，附带元数据（时间、地点、情绪、参与角色），可导入向量数据库
  - 设计角色状态机（情绪状态、关系状态），可作为后端服务 API 供聊天机器人调用

### 11. 多模态角色提取
- **状态**: 🔲 待研究
- **描述**: 当前只处理文本剧本。AI 时代视觉/语音理解已成熟。
- **动机**: 完整的角色塑造需要视觉形象、语音特征、行为姿态等多维度信息。
- **关键实现点**:
  - **视觉**：输入游戏 CG / 立绘，使用多模态 LLM（GPT-4V、Qwen-VL）提取视觉特征描述（服装细节、表情习惯、标志性姿态）
  - **语音**：输入角色语音样本（如广播剧、语音片段），提取语气特征（语速、口头禅、情感基调）
  - 输出包含 **视觉描述** 的 skill 包字段，可用于 Stable Diffusion / DALL-E 的角色一致性生成（如 LoRA 训练提示词）

### 12. Agentic Workflow 自动化
- **状态**: 🔲 待研究
- **描述**: 从"手动运行 CLI"进化为持续运转的自动化 Agent。
- **动机**: 降低重复操作，实现"监控-处理-验证-输出"全自动化。
- **关键实现点**:
  - 文件监控模式：`dotnet run --project CharacterKiller.CLI -- watch -d ./scripts/`，新文件放入后自动触发对应 pipeline
  - VNDB 深度联动：输入 VNDB 作品 ID，自动拉取所有角色列表 + 元数据，批量生成 skill
  - 后处理验证：生成后自动跑几轮对话测试（LLM 扮演该角色，自检还原度），输出质量评分
  - 考虑引入轻量级调度框架或基于 `IHostedService` 的后台模式

### 13. 角色宇宙与跨作品融合
- **状态**: 🔲 待研究
- **描述**: 管理和融合多个角色的高级功能。
- **动机**: 为创作者提供角色原型管理、跨作品灵感迁移等创新工具。
- **关键实现点**:
  - **角色数据库**：本地 SQLite / JSON 存储所有提取过的角色，支持搜索（按性格标签、关系、作品）
  - **角色对比**：选择两个角色，生成性格相似度/差异度分析
  - **跨作品融合**：提取角色 A（作品 X）和角色 B（作品 Y）的核心特征，生成融合角色设定
  - **关系网络可视化**：基于多角色的 relationship_dynamics，生成 Mermaid / Graphviz 关系图
  - **剧情模拟器**：基于角色设定，让 LLM 自主推演"如果角色 A 遇到场景 B 会如何反应"，用于验证角色一致性

---

## 🛠️ 工程债务清理

### 14. Checkpoint 清理机制
- **状态**: 🔲 待实现
- **描述**: Summarize 完成后，`checkpoints/temp/{id}/` 下的临时切片文件永久保留。
- **动机**: 长期运行会累积大量临时文件。
- **关键实现点**:
  - Summarize 成功完成后自动删除对应 checkpoint 的临时目录
  - 或提供 CLI 子命令 `clean`：清理已完成/过期的 checkpoint 数据

### 15. 统一 `ProcessChunkAsync` 逻辑
- **状态**: 🔲 待实现
- **描述**: `SummarizePipeline` 的并发分支和顺序分支从不同的数据源读取 `characterName`（`taskConfig.CharacterName` vs `checkpoint.Metadata.InputParams.CharacterName`）。
- **动机**: 逻辑不一致，可能导致并发和顺序执行行为差异。

### 16. `Program.cs` 重复代码提取
- **状态**: 🔲 待实现
- **描述**: `rootCommand.SetAction` 与 `runCommand.SetAction` 代码几乎完全重复。
- **动机**: DRY 原则。提取为共享方法 `RunPipelineAsync`。

---

## 贡献指南

- 建议从高优先级条目开始，逐层推进。
- 每个条目实现时应更新此文档状态（`🔲` → `🔄` → `✅`）。
- 大功能建议先开设计讨论（如"多模态角色提取"涉及架构变更），再动手编码。
