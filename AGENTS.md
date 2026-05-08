# CharacterKiller

本项目用于从文本中提炼角色信息生成相关角色的 SKILL。

参考项目：[Github: GalgameCharacterSkills](https://github.com/JodieRuth/GalgameCharacterSkills)

---

## 技术栈

- **语言**：C# 13 / .NET 10
- **项目格式**：SDK-style (`.csproj`)，解决方案文件为 `.slnx`（XML 格式）
- **CLI 框架**：`System.CommandLine` 3.0.0-preview.3.26207.106
- **DI / 配置 / 日志**：`Microsoft.Extensions.*` 10.0.7
- **Token 化**：`Microsoft.ML.Tokenizers` 2.0.0（已引用，但默认使用字符近似估算器）

---

## 项目结构

采用分层架构，共 4 个项目：

| 项目                             | 职责                                                        | 依赖                             |
| -------------------------------- | ----------------------------------------------------------- | -------------------------------- |
| `CharacterKiller.Core`           | 领域模型、接口、核心服务（文本切片、Token 截断）            | 无                               |
| `CharacterKiller.Application`    | 业务流水线（Pipeline）与 Prompt 构造                        | `Core`                           |
| `CharacterKiller.Infrastructure` | 具体实现：LLM 客户端、Checkpoint 存储、文件读取、Token 估算 | `Core`                           |
| `CharacterKiller.CLI`            | 可执行入口、命令行解析、依赖注册、配置绑定                  | `Application` + `Infrastructure` |

### 目录约定

- 源码位于每个项目的 `src/` 目录下，按功能分子目录（如 `Pipelines/`, `Prompts/`, `Models/`, `Interfaces/` 等）。
- **命名空间不要求与文件夹结构一致**（`.editorconfig` 中关闭了 `IDE0130`）。

---

## 构建与运行

### 环境要求

- .NET 10 SDK（当前已安装 `10.0.202`）

### 常用命令

```bash
# 构建整个解决方案
dotnet build

# 运行 CLI（默认执行 summarize + skills）
dotnet run --project CharacterKiller.CLI

# 指定配置文件与参数
dotnet run --project CharacterKiller.CLI -- -c appsettings.json -i script.txt -n 角色名

# 仅执行 summarize
dotnet run --project CharacterKiller.CLI -- summarize -c appsettings.json -i script.txt -n 角色名

# 仅执行 skills（基于已有的 summary）
dotnet run --project CharacterKiller.CLI -- skills -c appsettings.json -n 角色名

# 使用 template 模式生成小说人物模板
dotnet run --project CharacterKiller.CLI -- -c appsettings.json -i script.txt -n 角色名 -m template
```

### 发布

```bash
dotnet publish CharacterKiller.CLI -c Release -o ./publish
```

---

## 配置

CLI 默认读取 `appsettings.json`，可通过 `-c` 或 `--config` 指定其他路径。

配置结构对应 `CharacterKiller.CLI.Configuration.CliConfig`：

```json
{
  "Llm": {
    "Provider": "openai",
    "BaseUrl": "https://api.openai.com/v1",
    "Model": "gpt-4o-mini",
    "ApiKey": "your-api-key",
    "MaxRetries": 3,
    "TimeoutSeconds": 120
  },
  "Task": {
    "InputFile": "script.txt",
    "InputFiles": [],
    "CharacterName": "",
    "VndbCharacterId": null,
    "OutputDirectory": "output",
    "OutputMode": "roleplay",
    "SkillsMaxContextChars": 150000
  },
  "Jobs": [
    {
      "InputFiles": ["file1.txt", "file2.txt"],
      "CharacterName": "角色A",
      "OutputDirectory": "output/角色A",
      "OutputMode": "template",
      "SkillsMaxContextChars": 150000
    }
  ],
  "Slicing": {
    "ChunkSizeTokens": 50000,
    "OverlapTokens": 500
  },
  "Execution": {
    "MaxChunkConcurrency": 1,
    "MaxJobConcurrency": 1
  },
  "Checkpoint": {
    "Enabled": true,
    "Directory": "checkpoints"
  }
}
```

- `Task`：单任务配置（命令行模式）。
  - `InputFile`：单文件输入路径。当 `InputFiles` 为空时使用。
  - `InputFiles`：多文件输入列表，按顺序拼接后整体分析。若此列表非空，优先使用它，忽略 `InputFile`。
  - `CharacterName`：目标角色名称。
  - `VndbCharacterId`：VNDB 角色 ID（可选）。
  - `OutputDirectory`：任务输出目录，默认 `output`。
  - `OutputMode`：输出模式。`roleplay`（默认）生成 AI 角色扮演 skill 文件夹；`template` 生成去剧情化、可复用的小说人物模板。
  - `SkillsMaxContextChars`：Skills 阶段输入 summary 的最大字符数（默认 `150000`）。当 summary 长度超过此值时，会先分片调用 LLM 压缩提炼，再生成 Skills。设为 `0` 表示禁用压缩（使用原始完整 summary，可能触发超时）。
- `Jobs`：批量任务列表。若 `Jobs` 非空，则优先执行批量任务，忽略 `Task`。每个 Job 的字段与 `Task` 相同，但不包含 `InputFile`（统一使用 `InputFiles` 列表）。
- `Execution`：执行并发度配置。
  - `MaxChunkConcurrency`：Summarize 阶段单个角色的最大切片并发数。默认 `1`（顺序执行）。
  - `MaxJobConcurrency`：批量任务（Jobs）的最大并行角色数。默认 `1`（顺序执行）。

### 环境变量

- 配置支持环境变量覆盖，前缀为 `GCS_`（如 `GCS_LLM__MODEL`）。
- `GCS_APIKEY` 可直接覆盖 `Llm.ApiKey`。

---

## 核心流程

### 1. Summarize（文本切片归纳）

`SummarizePipeline` 负责：

1. 读取剧本文本（支持单文件/多文件合并）。
2. 使用 `TextSlicer` 按段落切分为多个 chunk（默认 50000 tokens/块，重叠 500 tokens）。
3. 为每个 chunk 调用 LLM，逐段归纳目标角色信息。
4. 汇总所有片段结果，输出为 Markdown 文件（`output/summaries/{角色名}.md`）。

### 2. Skills（生成角色技能包）

`SkillsPipeline` 负责：

1. 读取上述 summary 文件。
2. 根据 `OutputMode` 决定生成策略：
   - `roleplay`（默认）：生成 AI 角色扮演 skill 包。
   - `template`：生成去剧情化的小说人物模板。
3. 解析 JSON 并写入对应文件夹结构。

**Roleplay 模式输出**（`output/roleplay/`）：
- `{角色名}-roleplay-main/`：完整文件
- `{角色名}-roleplay-code/`：排除 `limit.md`
- 文件：`SKILL.md`、`soul.md`、`limit.md`、`resource/behavior_guide.md`、`resource/speech_patterns.md`、`resource/relationship_dynamics.md`、`resource/key_life_events.md`

**Template 模式输出**（`output/templates/`）：
- `{角色名}/`
- 文件：`README.md`、`profile.md`、`personality.md`、`background.md`、`behavior.md`、`speech.md`、`relationships.md`
- 特点：隐去具体剧情、模糊化背景（如将具体家族替换为"贵族世家"）、保留角色原型特征。

### Checkpoint（断点续传）

- 每个任务（summarize / skills）都有独立的 Checkpoint 机制，基于 JSON 文件持久化。
- Checkpoint 保存在任务输出目录下的 `checkpoints/` 子目录中。
- 支持恢复时校验：若已完成的切片结果文件缺失，会自动移回待处理队列重新执行。
- 大内容（切片结果）与元数据分离存储，避免 checkpoint JSON 膨胀。

---

## 代码风格指南

所有 `.editorconfig` 内容一致，关键规则如下：

- **缩进**：空格，4 个字符。
- **编码**：UTF-8，行尾 `CRLF`。
- **using 排序**：`System` 开头，按字母排序。
- **var 使用**：内置类型和类型显而易见时建议用 `var`，其他情况建议显式类型。
- **花括号**：建议始终使用（`csharp_prefer_braces = true:suggestion`）。
- **私有字段**：camelCase，前缀加 `_`。
- **命名空间**：不要求与文件夹结构匹配；允许冗余的 `using`（相关警告已关闭）。

---

## 测试

**当前项目没有测试项目。** 若需添加测试，建议：

- 使用 `xUnit` 或 `NUnit`。
- 对 `TextSlicer`、`TokenLimiter`、`PromptBuilder` 等纯逻辑类优先补充单元测试。
- 对 `OpenAiCompatibleClient`、`JsonCheckpointStore` 等可引入接口 mock（已预留 `ILlmClient`、`ICheckpointStore` 等接口）。

---

## 安全与注意事项

- **API Key**：通过配置文件或 `GCS_APIKEY` 环境变量传入，避免硬编码。
- **Checkpoint 数据**：可能包含原始剧本文本摘要，注意输出目录权限。
- **文件路径**：输出文件名会对角色名进行清理（移除非法字符），但输入文件路径未做深度校验。
- **LLM 返回内容**：Skills 阶段依赖 LLM 返回合法 JSON，解析失败会抛出异常并记录原始响应前 2000 字符。
- **重试策略**：LLM 请求失败时默认按指数退避重试（最多 `MaxRetries` 次）。

---

## 扩展提示

- **新增 LLM Provider**：目前所有 provider（OpenAI、Kimi、DeepSeek、Ollama）均复用 `OpenAiCompatibleClient`。若需接入非 OpenAI 兼容接口，在 `LlmClientFactory` 中新增分支即可。
- **更换 Token 估算器**：默认使用 `CharBasedEstimator`（字符近似）。可在 `ServiceRegistrar` 中替换为 `TiktokenEstimator`（需自行实现，Infrastructure 已引用 `Microsoft.ML.Tokenizers`）。
- **新增 OutputMode**：在 `TaskConfig`/`JobConfig` 中设置 `OutputMode` 即可切换生成策略。`SkillsPipeline` 内部通过 `taskConfig.OutputMode` 分支选择 `RoleplayPromptBuilder` 或 `TemplatePromptBuilder`，并写入不同的输出目录结构。新增模式时，只需：1) 新增 PromptBuilder 实现；2) 在 `SkillsPipeline` 中添加分支；3) 在 `TaskConfig`/`JobConfig` 中扩展枚举/字符串值。
- **并行执行**：通过 `ExecutionConfig`（`MaxChunkConcurrency`、`MaxJobConcurrency`）和 `LlmConfig.MaxConcurrency` 三层控制并发。`SummarizePipeline` 内部使用 `Parallel.ForEachAsync` 实现切片级并行，`Program.cs` 使用 `Parallel.ForEachAsync` 实现 Job 级并行，`OpenAiCompatibleClient` 使用 `SemaphoreSlim` 限制总 HTTP 并发数。三层独立配置，可灵活组合。
- **新增 Pipeline**：参考 `SummarizePipeline` / `SkillsPipeline`，利用 `ICheckpointStore` 的泛型接口实现新任务的断点续传。
