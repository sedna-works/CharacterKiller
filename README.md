# Character Killer

从 Galgame / 视觉小说剧本文本中提炼角色信息，并生成可用于角色扮演（Roleplay）的 skill 文件夹结构。

参考项目：[GalgameCharacterSkills](https://github.com/JodieRuth/GalgameCharacterSkills)

---

## 功能

- **Summarize**：读取剧本文本，按段落切片后逐段调用 LLM 归纳目标角色信息，输出为 Markdown 摘要。
- **Skills**：基于摘要文件，调用 LLM 生成角色资料包。支持两种输出模式：
  - `roleplay`（默认）：生成 AI 角色扮演 skill 文件夹（7 个文件）。
  - `template`：生成去剧情化、可复用的小说人物模板 skill 文件夹（7 个文件，内容与 roleplay 同源但去除剧情绑定）。
- **流式输出**：LLM 生成内容实时打印到终端，可直观看到处理进度。
- **断点续传**：每个任务独立支持 Checkpoint，意外中断后可从中恢复，避免重复调用 LLM。
- **多文件输入**：支持单文件或多个剧本文本按顺序合并后整体分析。
- **批量任务**：通过配置文件中的 `Jobs` 列表一次性处理多个角色。
- **摘要自动压缩**：当 summary 过长时自动分片压缩，压缩结果缓存到磁盘避免重复消耗 Token。

---

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（已验证 `10.0.202`）

---

## 构建

### 使用脚本（推荐）

**Windows（PowerShell）**：
```powershell
# 框架依赖，单文件（约 2 MB）
.\Tools\build.ps1

# 自包含 + 指定运行时（约 74 MB，无需安装 .NET 运行时）
.\Tools\build.ps1 -Runtime win-x64 -SelfContained
```

**Linux / macOS（Bash）**：
```bash
# 框架依赖，单文件
./Tools/build.sh

# 自包含 + 指定运行时
./Tools/build.sh -r osx-arm64 --self-contained
```

产物输出到项目根目录的 `publish/`（或 `publish/{RID}/`）。

### 手动构建

```bash
dotnet build
dotnet publish CharacterKiller.CLI -c Release -o ./publish
```

---

## 配置

CLI 默认读取 `appsettings.json`，可通过 `-c` 或 `--config` 指定其他路径。**配置文件为必需项**，若未指定且默认文件不存在，程序将报错退出。

### 最小配置示例

```json
{
  "Llm": {
    "Provider": "openai",
    "BaseUrl": "https://api.openai.com/v1",
    "Model": "gpt-4o-mini",
    "ApiKey": "your-api-key"
  },
  "Task": {
    "InputFile": "script.txt",
    "CharacterName": "角色名",
    "OutputDirectory": "output",
    "OutputMode": "roleplay",
    "SkillsMaxContextChars": 150000
  }
}
```

### 完整配置结构

```json
{
  "Llm": {
    "Provider": "openai",
    "BaseUrl": "https://api.openai.com/v1",
    "Model": "gpt-4o-mini",
    "ApiKey": "your-api-key",
    "MaxRetries": 3,
    "TimeoutSeconds": 120,
    "MaxConcurrency": 1
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

### 配置项说明

- `Llm.MaxConcurrency`：同时向 LLM API 发出的最大并发 HTTP 请求数，默认 `1`。
- `Task.SkillsMaxContextChars`：Skills 阶段允许的最大摘要字符数。若 summary 超过此值，会先分片调用 LLM 压缩提炼，结果缓存到 `summaries/compressed_{角色名}.md`。设为 `0` 禁用压缩。

### 配置优先级

1. 命令行参数（仅覆盖 `Task` 的少量字段）
2. 环境变量（前缀 `GCS_`，如 `GCS_LLM__MODEL`）
3. `GCS_APIKEY`（单独覆盖 `Llm.ApiKey`）
4. JSON 配置文件

> **提示**：环境变量中使用双下划线 `__` 表示配置层级，例如 `GCS_LLM__BASEURL` 对应 `Llm.BaseUrl`。

---

## 使用方式

### 默认流程（summarize + skills）

```bash
# 角色扮演模式（默认）
CharacterKiller.CLI -c appsettings.json -i script.txt -n 角色名

# 人物模板模式
CharacterKiller.CLI -c appsettings.json -i script.txt -n 角色名 -m template
```

### 仅执行 summarize

```bash
CharacterKiller.CLI summarize -c appsettings.json -i script.txt -n 角色名
```

### 仅执行 skills（基于已有摘要）

```bash
CharacterKiller.CLI skills -c appsettings.json -n 角色名
```

### 批量任务

在 `appsettings.json` 中配置 `Jobs` 列表：

```json
{
  "Jobs": [
    {
      "InputFiles": ["脚本A.txt", "脚本B.txt"],
      "CharacterName": "角色A",
      "OutputDirectory": "output/角色A",
      "OutputMode": "roleplay"
    },
    {
      "InputFiles": ["脚本C.txt"],
      "CharacterName": "角色B",
      "OutputDirectory": "output/角色B",
      "OutputMode": "template"
    }
  ]
}
```

当 `Jobs` 非空时，程序会优先执行批量任务，忽略单任务 `Task` 配置。

---

## 输出说明

### Summarize 阶段

生成角色摘要 Markdown：

```
output/summaries/{角色名}.md
```

### Skills 阶段（`OutputMode: roleplay`）

生成 AI 角色扮演 skill 文件夹：

```
output/roleplay/{角色名}-roleplay-main/
├── SKILL.md
├── soul.md
├── limit.md
└── resource/
    ├── behavior_guide.md
    ├── speech_patterns.md
    ├── relationship_dynamics.md
    └── key_life_events.md

output/roleplay/{角色名}-roleplay-code/
└── （同上，但排除 limit.md）
```

### Skills 阶段（`OutputMode: template`）

生成去剧情化的可复用角色模板 skill 文件夹，**结构与 roleplay 完全一致**：

```
output/templates/{角色名}-template-main/
├── SKILL.md          # 模板使用说明与跨世界观适配指南
├── soul.md           # 角色内核（去剧情化）
├── limit.md          # 限制与边界
└── resource/
    ├── behavior_guide.md
    ├── speech_patterns.md
    ├── relationship_dynamics.md
    └── key_life_events.md   # 抽象化经历（非具体剧情）

output/templates/{角色名}-template-code/
└── （同上，但排除 limit.md）
```

Template 模式的核心处理：
- **隐去具体剧情**：不保留原作的具体事件、具体人名、具体地点。
- **背景模糊化**：例如"远坂家的大小姐"→"神秘传承贵族世家的继承人"。
- **保留原型特征**：性格、行为、语言、关系动态等本质特征完整保留，以 archetype（原型）形式呈现。

### Checkpoint

断点续传数据保存在任务输出目录下的 `checkpoints/` 子目录中。若已完成的切片结果文件缺失，恢复时会自动移回待处理队列重新执行。

---

## 命令行参数速查

| 参数 | 说明 |
|------|------|
| `-c, --config <path>` | 指定配置文件路径（默认 `appsettings.json`） |
| `-i, --input <path>` | 指定输入剧本文件（覆盖 `Task.InputFile`） |
| `-n, --character <name>` | 指定角色名（覆盖 `Task.CharacterName`） |
| `-m, --mode <mode>` | 输出模式：`roleplay` 或 `template`（覆盖 `Task.OutputMode`） |

### 并发配置

通过 `Execution` 配置项开启并行执行，可显著缩短批量任务或多切片的处理时间：

```json
{
  "Execution": {
    "MaxChunkConcurrency": 5,
    "MaxJobConcurrency": 3
  }
}
```

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| `MaxChunkConcurrency` | Summarize 阶段单个角色的最大切片并发数。设为 1 表示顺序处理（默认）。 | `1` |
| `MaxJobConcurrency` | 批量任务（Jobs）的最大并行角色数。设为 1 表示顺序处理（默认）。 | `1` |

**并发安全说明**：
- 每个切片是独立的 LLM 调用，彼此之间**没有上下文依赖**，并行不会影响归纳质量。
- `ILlmClient` 内部已根据 `Llm.MaxConcurrency` 限制同时发出的 HTTP 请求数，避免触发 LLM Provider 的 Rate Limit。
- 建议根据你的 API 账户并发限额调整（如 OpenAI 免费档通常限制 2-3 并发）。

---

## 注意事项

- **API Key**：通过配置文件或 `GCS_APIKEY` 环境变量传入，切勿硬编码到源码中。
- **LLM JSON 输出**：Skills 阶段依赖 LLM 返回合法 JSON。若解析失败，程序会抛出异常并记录原始响应的前 2000 个字符以便排查。
- **Token 估算**：默认使用字符近似估算器（`CharBasedEstimator`）。如需更精确的 `cl100k_base` Tiktoken 估算，可自行在 `ServiceRegistrar` 中替换实现（项目已引用 `Microsoft.ML.Tokenizers`）。
- **流式输出**：LLM 生成内容会实时打印到终端。在首 token 到达前会显示旋转进度条，若长时间无响应请检查网络或 API 状态。
- **文件大小限制**：`LocalFileReader` 拒绝读取超过 500 MB 的输入文件。
- **文件路径**：输出文件名会对角色名进行清理（移除非法字符），但输入文件路径未做深度校验，请确保传入可信路径。

---

## 技术栈

- C# 13 / .NET 10
- `System.CommandLine`（CLI 框架）
- `Microsoft.Extensions.*`（DI、配置、日志）
- `Microsoft.ML.Tokenizers`（Token 化，可选启用）

---

## 项目结构

```
CharacterKiller/
├── CharacterKiller.Core/          # 领域模型、接口、核心服务
│   └── src/Resilience/
│       └── RetryPolicy.cs         # 通用重试策略（指数退避）
├── CharacterKiller.Application/   # 业务流水线（Pipeline）与 Prompt 构造
│   └── src/Prompts/
│       ├── SummarizePromptBuilder.cs
│       ├── RoleplayPromptBuilder.cs
│       └── TemplatePromptBuilder.cs
├── CharacterKiller.Infrastructure/# LLM 客户端、Checkpoint、文件读取
├── CharacterKiller.CLI/           # 可执行入口、命令行解析
├── Tools/
│   ├── build.ps1                  # Windows 构建脚本
│   └── build.sh                   # Linux / macOS 构建脚本
└── README.md
```
