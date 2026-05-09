# CharacterKiller 架构文档

> 本文档描述 CharacterKiller 的代码架构、核心类关系与数据流程。
> 所有图示使用 [Mermaid](https://mermaid.js.org/) 语法。

---

## 1. 架构概览

CharacterKiller 采用**分层架构**，共 4 个项目，依赖关系严格向内：

| 项目             | 职责                                           | 依赖                             |
| ---------------- | ---------------------------------------------- | -------------------------------- |
| `Core`           | 领域模型、接口、纯逻辑服务                     | 无                               |
| `Application`    | 业务流水线（Pipeline）与 Prompt 构造           | `Core`                           |
| `Infrastructure` | 具体技术实现：LLM 客户端、文件存储、Token 估算 | `Core`                           |
| `CLI`            | 可执行入口、命令行解析、依赖注册、配置绑定     | `Application` + `Infrastructure` |

---

## 2. 项目依赖图

```mermaid
graph TB
    subgraph CLI["CharacterKiller.CLI"]
        Program["Program.cs<br/>命令行解析 & 主流程编排"]
        CliConfig["CliConfig<br/>配置根节点"]
        ServiceRegistrar["ServiceRegistrar<br/>DI 注册"]
    end

    subgraph Application["CharacterKiller.Application"]
        SummarizePipeline["SummarizePipeline<br/>文本切片归纳流水线"]
        SkillsPipeline["SkillsPipeline<br/>角色技能包生成流水线"]
        SummarizePromptBuilder["SummarizePromptBuilder<br/>Summarize Prompt 构造"]
        RoleplayPromptBuilder["RoleplayPromptBuilder<br/>Roleplay Prompt 构造"]
        TemplatePromptBuilder["TemplatePromptBuilder<br/>Template Prompt 构造"]
    end

    subgraph Infrastructure["CharacterKiller.Infrastructure"]
        OpenAiCompatibleClient["OpenAiCompatibleClient<br/>OpenAI 兼容 LLM 客户端"]
        LlmClientFactory["LlmClientFactory<br/>LLM 客户端工厂"]
        JsonCheckpointStore["JsonCheckpointStore<br/>JSON 断点持久化"]
        NullCheckpointStore["NullCheckpointStore<br/>空实现（禁用断点）"]
        LocalFileReader["LocalFileReader<br/>本地文件读取"]
        CharBasedEstimator["CharBasedEstimator<br/>字符近似 Token 估算"]
    end

    subgraph Core["CharacterKiller.Core"]
        subgraph Interfaces["Interfaces"]
            ILlmClient["ILlmClient"]
            ICheckpointStore["ICheckpointStore&lt;TState&gt;"]
            ITokenEstimator["ITokenEstimator"]
            IFileReader["IFileReader"]
    end

        subgraph Models["Models"]
            CheckpointState["CheckpointState&lt;TTaskState&gt;"]
            CheckpointMetadata["CheckpointMetadata"]
            ProgressState["ProgressState"]
            TaskConfig["TaskConfig"]
            TextChunk["TextChunk"]
            SummarizeTaskState["SummarizeTaskState"]
            SkillsTaskState["SkillsTaskState"]
        end

        subgraph Services["Services & Resilience"]
            TextSlicer["TextSlicer<br/>文本切片"]
            TokenLimiter["TokenLimiter<br/>Token 截断"]
            AtomicFileWriter["AtomicFileWriter<br/>原子文件写入"]
            RetryPolicy["RetryPolicy<br/>指数退避重试"]
        end
    end

    CLI --> Application
    CLI --> Infrastructure
    Application --> Core
    Infrastructure --> Core

    Program --> CliConfig
    Program --> ServiceRegistrar
    ServiceRegistrar --> SummarizePipeline
    ServiceRegistrar --> SkillsPipeline
    ServiceRegistrar --> OpenAiCompatibleClient
    ServiceRegistrar --> JsonCheckpointStore
    ServiceRegistrar --> LocalFileReader
    ServiceRegistrar --> CharBasedEstimator

    SummarizePipeline --> ILlmClient
    SummarizePipeline --> ICheckpointStore
    SummarizePipeline --> ITokenEstimator
    SummarizePipeline --> IFileReader
    SummarizePipeline --> TextSlicer
    SummarizePipeline --> SummarizePromptBuilder
    SummarizePipeline --> AtomicFileWriter

    SkillsPipeline --> ILlmClient
    SkillsPipeline --> ICheckpointStore
    SkillsPipeline --> RoleplayPromptBuilder
    SkillsPipeline --> TemplatePromptBuilder
    SkillsPipeline --> AtomicFileWriter

    OpenAiCompatibleClient -.implements.-> ILlmClient
    JsonCheckpointStore -.implements.-> ICheckpointStore
    NullCheckpointStore -.implements.-> ICheckpointStore
    LocalFileReader -.implements.-> IFileReader
    CharBasedEstimator -.implements.-> ITokenEstimator

    TextSlicer --> ITokenEstimator
    TextSlicer --> TextChunk
    TokenLimiter --> ITokenEstimator
```

---

## 3. 核心类图

### 3.1 接口层

```mermaid
classDiagram
    class ILlmClient {
        <<interface>>
        +CompleteAsync(systemPrompt, userPrompt, ct) Task~string~
    }

    class ICheckpointStore {
        <<interface>>
        +LoadAsync~TState~(checkpointId, ct) Task~CheckpointState~TState~~?
        +SaveAsync~TState~(state, ct) Task
        +DeleteAsync(checkpointId, ct) Task
        +ListAsync(ct) Task~IReadOnlyList~CheckpointSummary~~
        +SaveSliceResultAsync(checkpointId, index, content, ct) Task
        +LoadSliceResultAsync(checkpointId, index, ct) Task~string?~
        +SliceResultExistsAsync(checkpointId, index, ct) Task~bool~
        +WithBaseDir(baseDir) ICheckpointStore
    }

    class ITokenEstimator {
        <<interface>>
        +Estimate(text) int
    }

    class IFileReader {
        <<interface>>
        +ReadAsync(path, ct) Task~string~
        +Exists(path) bool
    }
```

### 3.2 模型层

```mermaid
classDiagram
    class CheckpointStatus {
        <<enum>>
        Running
        Completed
        Failed
    }

    class CheckpointMetadata {
        +string CheckpointId
        +string TaskType
        +CheckpointStatus Status
        +DateTime CreatedAt
        +DateTime UpdatedAt
        +Dictionary~string,object~ InputParams
    }

    class ProgressState {
        +int CurrentStep
        +int TotalSteps
        +string CurrentPhase
        +List~int~ CompletedItems
        +List~int~ FailedItems
        +List~int~ PendingItems
    }

    class CheckpointState~TTaskState~ {
        +CheckpointMetadata Metadata
        +ProgressState Progress
        +TTaskState TaskState
    }

    class SummarizeTaskState {
        +List~TextChunk~ Chunks
        +Dictionary~int,string~ SliceOutputFiles
        +string? FinalOutputPath
    }

    class SkillsTaskState {
        +string SummaryFilePath
        +List~LlmMessage~ ConversationHistory
        +int IterationCount
        +string? FinalOutputPath
    }

    class TextChunk {
        +int Index
        +string Content
        +int EstimatedTokens
    }

    class TaskConfig {
        +string InputFile
        +List~string~ InputFiles
        +string CharacterName
        +string? VndbCharacterId
        +string OutputDirectory
        +string OutputMode
        +int SkillsMaxContextChars
    }

    class LlmMessage {
        +string Role
        +string Content
    }

    CheckpointMetadata --> CheckpointStatus
    CheckpointState --> CheckpointMetadata
    CheckpointState --> ProgressState
    SummarizeTaskState --> TextChunk
    SkillsTaskState --> LlmMessage
```

### 3.3 服务与流水线层

```mermaid
classDiagram
    class AtomicFileWriter {
        <<static>>
        +WriteAllTextAsync(path, content, ct) Task
    }

    class RetryPolicy {
        <<static>>
        +ExecuteAsync~T~(action, maxRetries, canRetry, logger, ct) Task~T~
        +ExecuteAsync(action, maxRetries, canRetry, logger, ct) Task
    }

    class TextSlicer {
        -ITokenEstimator _estimator
        +TextSlicer(estimator)
        +Slice(text, chunkSizeTokens, overlapTokens) List~TextChunk~
        -FlushChunk(...)
        -ExtractOverlap(paragraphs, overlapTokens) string
        -SplitBySentences(paragraph, chunkSizeTokens) List~string~
    }

    class TokenLimiter {
        -ITokenEstimator _estimator
        +TokenLimiter(estimator)
        +TruncateToTokens(text, maxTokens) string
    }

    class SummarizePipeline {
        -ILlmClient _llmClient
        -ITokenEstimator _estimator
        -ICheckpointStore _checkpointStore
        -IFileReader _fileReader
        -ILogger~SummarizePipeline~ _logger
        -ExecutionConfig _executionConfig
        -CheckpointConfig _checkpointConfig
        +RunAsync(taskConfig, slicingConfig, ct) Task
        -ProcessChunkAsync(index, chunks, systemPrompt, checkpoint, checkpointId, store, ct) Task
        -ValidateCheckpointAsync(checkpoint, checkpointId, store, ct) Task
        -RenderSummaryMarkdown(characterName, segments, inputFiles) string
        -Sanitize(name) string
    }

    class SkillsPipeline {
        -ILlmClient _llmClient
        -ICheckpointStore _checkpointStore
        -ILogger~SkillsPipeline~ _logger
        -CheckpointConfig _checkpointConfig
        +RunAsync(taskConfig, ct) Task
        -ParseSkillPack(response) Dictionary~string,string~
        -WriteSkillPackAsync(mainDir, codeDir, files, logPrefix, ct) Task
        -CompressSummaryAsync(characterName, summaryText, maxChunkChars, ct) Task~string~
        -Sanitize(name) string
    }

    class SummarizePromptBuilder {
        <<static>>
        +BuildSystemPrompt() string
        +BuildUserPrompt(characterName, chunkContent) string
    }

    class RoleplayPromptBuilder {
        <<static>>
        +BuildSystemPrompt(characterName) string
        +BuildUserPrompt(characterName, summaryText) string
    }

    class TemplatePromptBuilder {
        <<static>>
        +BuildSystemPrompt(characterName) string
        +BuildUserPrompt(characterName, summaryText) string
    }

    class OpenAiCompatibleClient {
        -LlmConfig _config
        -ILogger~OpenAiCompatibleClient~ _logger
        -HttpClient _httpClient
        -SemaphoreSlim _semaphore
        +OpenAiCompatibleClient(config, logger)
        +CompleteAsync(systemPrompt, userPrompt, ct) Task~string~
        -CompleteInternalAsync(systemPrompt, userPrompt, ct) Task~string~
        -ReadStreamAsync(response, logger, ct) Task~string~
        -RunSpinnerAsync(ct) Task~string~
        +Dispose()
    }

    class JsonCheckpointStore {
        -string _baseDir
        -SemaphoreSlim _writeLock
        +JsonCheckpointStore(baseDir)
        +WithBaseDir(baseDir) ICheckpointStore
        +LoadAsync~TState~(checkpointId, ct) Task~CheckpointState~TState~~?
        +SaveAsync~TState~(state, ct) Task
        +DeleteAsync(checkpointId, ct) Task
        +ListAsync(ct) Task~IReadOnlyList~CheckpointSummary~~
        +SaveSliceResultAsync(checkpointId, index, content, ct) Task
        +LoadSliceResultAsync(checkpointId, index, ct) Task~string?~
        +SliceResultExistsAsync(checkpointId, index, ct) Task~bool~
        -GetCheckpointPath(checkpointId) string
        -GetTempDir(checkpointId) string
        -GetSlicePath(checkpointId, index) string
    }

    class LocalFileReader {
        +ReadAsync(path, ct) Task~string~
        +Exists(path) bool
    }

    class CharBasedEstimator {
        +Estimate(text) int
    }

    TextSlicer --> ITokenEstimator
    TokenLimiter --> ITokenEstimator
    SummarizePipeline --> ILlmClient
    SummarizePipeline --> ITokenEstimator
    SummarizePipeline --> ICheckpointStore
    SummarizePipeline --> IFileReader
    SummarizePipeline --> TextSlicer
    SummarizePipeline --> AtomicFileWriter
    SkillsPipeline --> ILlmClient
    SkillsPipeline --> ICheckpointStore
    SkillsPipeline --> AtomicFileWriter
    OpenAiCompatibleClient --> RetryPolicy
    JsonCheckpointStore --> AtomicFileWriter
```

### 3.4 CLI 层

```mermaid
classDiagram
    class CliConfig {
        +LlmConfig Llm
        +TaskConfig Task
        +List~JobConfig~ Jobs
        +SlicingConfig Slicing
        +CheckpointConfig Checkpoint
        +ExecutionConfig Execution
    }

    class ServiceRegistrar {
        <<static>>
        +Register(services, config)
    }

    class Program {
        <<top-level statements>>
        +Main(args)
        -BuildConfigAndServices(parseResult, configOption) (CliConfig, IServiceProvider)
        -MapJobToTask(job) TaskConfig
        -ValidateJobConfig(job, index)
        -ApplyTaskOverrides(config, parseResult, inputOption, characterOption, modeOption)
        -ValidateTaskConfig(task, requireInput)
        -RunJobsAsync(jobs, maxConcurrency, executeAsync, ct) Task
    }

    Program --> CliConfig
    Program --> ServiceRegistrar
    Program --> SummarizePipeline
    Program --> SkillsPipeline
```

---

## 4. 数据流程图

### 4.1 SummarizePipeline 流程

从原始剧本文本到角色 Summary Markdown 的完整流程：

```mermaid
flowchart TD
    Start(["开始 Summarize"]) --> ReadConfig["读取 TaskConfig<br/>InputFiles / CharacterName"]
    ReadConfig --> CheckInput{"InputFiles<br/>是否为空？"}
    CheckInput -->|是| UseSingle["使用 InputFile<br/>（单文件兼容）"]
    CheckInput -->|否| UseMulti["使用 InputFiles<br/>（多文件列表）"]
    UseSingle --> MergeFiles["合并所有文件内容"]
    UseMulti --> MergeFiles

    MergeFiles --> CreateCheckpoint["创建/加载 Checkpoint<br/>summarize_{角色名}"]
    CreateCheckpoint --> CheckResume{"Checkpoint<br/>状态？"}
    CheckResume -->|Completed| Skip["跳过，直接返回"]
    CheckResume -->|Running/Failed| Validate["ValidateCheckpointAsync<br/>校验已完成的切片文件是否存在"]

    Validate --> Slice["TextSlicer.Slice<br/>按段落/句子切分"]
    Slice --> BuildPrompt["SummarizePromptBuilder<br/>构造 System + User Prompt"]

    BuildPrompt --> ConcurrencyCheck{"MaxChunkConcurrency<br/>> 1？"}
    ConcurrencyCheck -->|是| Parallel["Parallel.ForEachAsync<br/>并行处理 Pending Chunks"]
    ConcurrencyCheck -->|否| Sequential["foreach 顺序处理<br/>Pending Chunks"]

    Parallel --> LLM1["LLM 调用<br/>逐段归纳角色信息"]
    Sequential --> LLM1

    LLM1 --> SaveSlice["SaveSliceResultAsync<br/>结果存入 checkpoints/temp/"]
    SaveSlice --> SaveCheckpoint1["SaveAsync<br/>更新 Checkpoint 进度"]

    SaveCheckpoint1 --> AllDone{"所有 Chunks<br/>已完成？"}
    AllDone -->|否| Parallel
    AllDone -->|是| Aggregate["汇总所有切片结果<br/>RenderSummaryMarkdown"]

    Aggregate --> AtomicWrite["AtomicFileWriter<br/>写入 summaries/{角色名}.md"]
    AtomicWrite --> MarkComplete["Checkpoint.Status = Completed"]
    MarkComplete --> End(["Summarize 完成"])

    Skip --> End
```

### 4.2 SkillsPipeline 流程

从 Summary 到角色 Skill 文件夹的完整流程：

```mermaid
flowchart TD
    Start(["开始 Skills"]) --> ReadSummary["读取 summaries/{角色名}.md"]
    ReadSummary --> CheckLength{"Summary 长度 ><br/>SkillsMaxContextChars？"}

    CheckLength -->|超过阈值| CheckCache{"compressed_{角色名}.md<br/>是否存在？"}
    CheckCache -->|存在| ReadCompressed["读取压缩缓存"]
    CheckCache -->|不存在| Compress["CompressSummaryAsync<br/>分片调用 LLM 压缩提炼"]
    Compress --> CacheResult["AtomicFileWriter<br/>写入压缩缓存"]
    CacheResult --> UseCompressed["使用压缩后的 Summary"]
    ReadCompressed --> UseCompressed

    CheckLength -->|未超过| UseOriginal["使用原始 Summary"]
    UseCompressed --> CreateCheckpoint["创建/加载 Checkpoint<br/>skills_{角色名}_{mode}"]
    UseOriginal --> CreateCheckpoint

    CreateCheckpoint --> CheckResume{"Checkpoint<br/>状态？"}
    CheckResume -->|Completed| Skip["跳过，直接返回"]
    CheckResume -->|Running/Failed| SelectMode{"OutputMode？"}

    SelectMode -->|roleplay| RoleplayPrompt["RoleplayPromptBuilder<br/>构造 Prompt"]
    SelectMode -->|template| TemplatePrompt["TemplatePromptBuilder<br/>构造 Prompt"]

    RoleplayPrompt --> LLM["LLM 调用<br/>流式生成 JSON"]
    TemplatePrompt --> LLM

    LLM --> Parse["ParseSkillPack<br/>提取 JSON to Dictionary"]
    Parse -->|解析失败| LogError["记录错误日志<br/>抛出异常"]
    LogError --> EndFail(["Skills 失败"])

    Parse -->|解析成功| WriteMain["写入 *-main/<br/>完整文件（含 limit.md）"]
    WriteMain --> WriteCode["写入 *-code/<br/>排除 limit.md"]
    WriteCode --> MarkComplete["Checkpoint.Status = Completed<br/>记录 FinalOutputPath"]
    MarkComplete --> End(["Skills 完成"])

    Skip --> End
```

---

## 5. Checkpoint 断点续传机制

### 5.1 存储结构

```mermaid
flowchart LR
    subgraph CheckpointDir["checkpoints/"]
        JSON["summarize_苍崎青子.json<br/>元数据 + 进度 + TaskState"]
        JSON2["skills_苍崎青子_roleplay.json<br/>元数据 + 进度 + TaskState"]

        subgraph Temp["temp/"]
            subgraph ID1["summarize_苍崎青子/"]
                S1["slice_0.md"]
                S2["slice_1.md"]
                S3["slice_2.md"]
        end
        end
    end

    JSON -.引用.-> S1
    JSON -.引用.-> S2
    JSON -.引用.-> S3
```

### 5.2 断点恢复时序

```mermaid
sequenceDiagram
    actor User
    participant Program as CLI Program
    participant Pipeline as SummarizePipeline
    participant Store as JsonCheckpointStore
    participant LLM as OpenAiCompatibleClient
    participant Disk as 文件系统

    User->>Program: dotnet run summarize -n 苍崎青子
    Program->>Pipeline: RunAsync(taskConfig, slicingConfig)

    Pipeline->>Store: LoadAsync(SummarizeTaskState)
    Store->>Disk: 读取 {checkpointId}.json
    Disk-->>Store: JSON 内容
    Store-->>Pipeline: CheckpointState of SummarizeTaskState

    alt Checkpoint 不存在
        Pipeline->>Store: 创建新 Checkpoint (Status = Running)
        Pipeline->>Store: SaveAsync
    else Checkpoint 存在且 Status = Running or Failed
        Pipeline->>Pipeline: ValidateCheckpointAsync
        loop 遍历 CompletedItems
            Pipeline->>Store: SliceResultExistsAsync(index)
            Store->>Disk: 检查 slice file
            Disk-->>Store: exists or not
            alt 文件缺失
                Pipeline->>Pipeline: 移回 PendingItems
            end
        end
    else Checkpoint 存在且 Status = Completed
        Pipeline-->>Program: 直接返回，跳过执行
    end

    loop 处理每个 Pending Chunk
        Pipeline->>LLM: CompleteAsync(systemPrompt, userPrompt)
        LLM-->>Pipeline: 归纳结果
        Pipeline->>Store: SaveSliceResultAsync(index, content)
        Store->>Disk: 原子写入 slice file
        Pipeline->>Store: SaveAsync(checkpoint)
        Store->>Disk: 原子写入 {checkpointId}.json
    end

    Pipeline->>Pipeline: 汇总所有切片 to Markdown
    Pipeline->>Disk: 原子写入 summaries/苍崎青子.md
    Pipeline->>Store: SaveAsync (Status = Completed)
    Pipeline-->>Program: 完成
```

---

## 6. 并发控制模型

CharacterKiller 在三个层面独立控制并发：

```mermaid
flowchart TD
    subgraph L1["① Job 级并发"]
        direction TB
        Jobs["Jobs 列表<br/>多个角色任务"]
        ParallelJobs["Parallel.ForEachAsync<br/>MaxJobConcurrency"]
        Jobs --> ParallelJobs
    end

    subgraph L2["② Chunk 级并发"]
        direction TB
        Chunks["Text Chunks<br/>单个角色的文本切片"]
        ParallelChunks["Parallel.ForEachAsync<br/>MaxChunkConcurrency"]
        Chunks --> ParallelChunks
    end

    subgraph L3["③ HTTP 级并发"]
        direction TB
        Requests["HTTP 请求<br/>所有 LLM API 调用"]
        Semaphore["SemaphoreSlim<br/>LlmConfig.MaxConcurrency"]
        Requests --> Semaphore
    end

    L1 -->|每个 Job 内部触发| L2
    L2 -->|每个 Chunk 调用 LLM 触发| L3
```

| 层级  | 控制点                       | 配置项                                | 默认值 | 说明                  |
| ----- | ---------------------------- | ------------------------------------- | ------ | --------------------- |
| Job   | `Program.RunJobsAsync`       | `ExecutionConfig.MaxJobConcurrency`   | `1`    | 批量任务间并行        |
| Chunk | `SummarizePipeline.RunAsync` | `ExecutionConfig.MaxChunkConcurrency` | `1`    | 单个角色的切片间并行  |
| HTTP  | `OpenAiCompatibleClient`     | `LlmConfig.MaxConcurrency`            | `1`    | 全局 LLM API 并发上限 |

三层独立配置，可灵活组合。例如：`MaxJobConcurrency=2`、`MaxChunkConcurrency=4`、`MaxConcurrency=8` 表示同时处理 2 个角色，每个角色 4 个切片并行，总共最多 8 个 HTTP 请求。

---

## 7. 关键设计决策

### 7.1 为什么 `ICheckpointStore` 使用泛型？

```csharp
Task<CheckpointState<TState>?> LoadAsync<TState>(...) where TState : class, new();
```

不同 Pipeline 需要保存不同的任务状态：
- `SummarizePipeline` 需要 `SummarizeTaskState`（切片列表、切片结果文件映射）
- `SkillsPipeline` 需要 `SkillsTaskState`（Summary 路径、对话历史、迭代次数）

泛型设计使得新增 Pipeline 时无需修改 `ICheckpointStore` 接口。

### 7.2 为什么切片结果与元数据分离存储？

| 存储位置                               | 内容                                   | 原因                             |
| -------------------------------------- | -------------------------------------- | -------------------------------- |
| `{checkpointId}.json`                  | 元数据 + `ProgressState` + `TaskState` | 体积小，频繁读写                 |
| `temp/{checkpointId}/slice_{index}.md` | 切片归纳结果（大文本）                 | 避免 JSON 膨胀，独立管理生命周期 |

### 7.3 为什么强制 SSE 流式输出？

`OpenAiCompatibleClient` 中 `Stream = true` 是硬编码的：
- **用户体验**：长文本生成时实时看到输出，避免"黑屏等待"
- **首 token 感知**：`RunSpinnerAsync` 在首 token 到达前显示旋转进度条
- **超时检测**：流式连接可以更早发现网络/服务异常

### 7.4 原子写入的实现

```csharp
// AtomicFileWriter.cs
var tempPath = path + ".tmp" + Guid.NewGuid();
await File.WriteAllTextAsync(tempPath, content, ct);
File.Move(tempPath, path, overwrite: true);
```

所有关键文件（Checkpoint JSON、Summary Markdown、Skill 文件）均通过原子写入，避免程序崩溃时留下半写文件。
