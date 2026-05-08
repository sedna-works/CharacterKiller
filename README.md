# Character Killer

从 Galgame / 视觉小说剧本文本中提炼角色信息，并生成可用于角色扮演（Roleplay）的 skill 文件夹结构。

参考项目：[GalgameCharacterSkills](https://github.com/JodieRuth/GalgameCharacterSkills)

---

## 功能

- **Summarize**：读取剧本文本，按段落切片后逐段调用 LLM 归纳目标角色信息，输出为 Markdown 摘要。
- **Skills**：基于摘要文件，调用 LLM 生成 7 个角色扮演技能文件，打包为标准 skill 文件夹结构。
- **断点续传**：每个任务独立支持 Checkpoint，意外中断后可从中恢复，避免重复调用 LLM。
- **多文件输入**：支持单文件或多个剧本文本按顺序合并后整体分析。
- **批量任务**：通过配置文件中的 `Jobs` 列表一次性处理多个角色。

---

## 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)（已验证 `10.0.202`）

---

## 构建

### 使用脚本（推荐）

```powershell
# 框架依赖，单文件（约 2 MB）
.\Tools\build.ps1

# 自包含 + 指定运行时（约 74 MB，无需安装 .NET 运行时）
.\Tools\build.ps1 -Runtime win-x64 -SelfContained
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
    "OutputMode": "roleplay"
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
    "TimeoutSeconds": 120
  },
  "Task": {
    "InputFile": "script.txt",
    "InputFiles": [],
    "CharacterName": "",
    "VndbCharacterId": null,
    "OutputDirectory": "output",
    "OutputMode": "roleplay"
  },
  "Jobs": [
    {
      "InputFiles": ["file1.txt", "file2.txt"],
      "CharacterName": "角色A",
      "OutputDirectory": "output/角色A",
      "OutputMode": "template"
    }
  ],
  "Slicing": {
    "ChunkSizeTokens": 50000,
    "OverlapTokens": 500
  },
  "Checkpoint": {
    "Enabled": true,
    "Directory": "checkpoints"
  }
}
```

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
      "OutputDirectory": "output/角色A"
    },
    {
      "InputFiles": ["脚本C.txt"],
      "CharacterName": "角色B",
      "OutputDirectory": "output/角色B"
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
output/skills/{角色名}-skill-main/
├── SKILL.md
├── soul.md
├── limit.md
└── resource/
    ├── behavior_guide.md
    ├── speech_patterns.md
    ├── relationship_dynamics.md
    └── key_life_events.md

output/skills/{角色名}-skill-code/
└── （同上，但排除 limit.md）
```

### Template 阶段（`OutputMode: template`）

生成可复用的小说人物模板：

```
output/templates/{角色名}/
├── README.md          # 模板使用说明与适配建议
├── profile.md         # 人物档案（外貌、气质、穿着风格）
├── personality.md     # 性格内核（价值观、驱动力、成长弧线）
├── background.md      # 模糊化背景（家庭/社会阶层抽象描述）
├── behavior.md        # 行为模式（习惯、反应、决策风格）
├── speech.md          # 语言特征（用词、语气、口头禅）
└── relationships.md   # 关系原型（互动模式，不绑定具体角色）
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

---

## 注意事项

- **API Key**：通过配置文件或 `GCS_APIKEY` 环境变量传入，切勿硬编码到源码中。
- **LLM JSON 输出**：Skills 阶段依赖 LLM 返回合法 JSON。若解析失败，程序会抛出异常并记录原始响应的前 2000 个字符以便排查。
- **Token 估算**：默认使用字符近似估算器（`CharBasedEstimator`）。如需更精确的 `cl100k_base` Tiktoken 估算，可自行在 `ServiceRegistrar` 中替换实现（项目已引用 `Microsoft.ML.Tokenizers`）。
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
├── CharacterKiller.Application/   # 业务流水线（Pipeline）与 Prompt 构造
├── CharacterKiller.Infrastructure/# LLM 客户端、Checkpoint、文件读取
├── CharacterKiller.CLI/           # 可执行入口、命令行解析
├── Tools/
│   └── build.ps1                  # 一键构建脚本
└── README.md
```
