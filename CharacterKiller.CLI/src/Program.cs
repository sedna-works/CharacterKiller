using System.CommandLine;
using CharacterKiller.CLI.Configuration;
using CharacterKiller.CLI.DependencyInjection;
using CharacterKiller.Application.Pipelines;
using CharacterKiller.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var configOption = new Option<string>("--config", "-c")
{
    Description = "配置文件路径"
};

var rootCommand = new RootCommand("CharacterKiller - Galgame 角色信息提炼工具")
{
    configOption
};

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var (config, provider) = BuildConfigAndServices(parseResult, configOption);
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("CharacterKiller");

    if (config.Jobs.Count > 0)
    {
        logger.LogInformation("检测到批量任务，共 {Count} 个角色，并发度={Concurrency}", config.Jobs.Count, config.Execution.MaxJobConcurrency);
        var summarizePipeline = provider.GetRequiredService<SummarizePipeline>();
        var skillsPipeline = provider.GetRequiredService<SkillsPipeline>();

        await RunJobsAsync(
            config.Jobs.Select(MapJobToTask),
            config.Execution.MaxJobConcurrency,
            async (taskConfig, ct) =>
            {
                logger.LogInformation("批量 Run：角色={Character}，文件数={FileCount}", taskConfig.CharacterName, taskConfig.InputFiles.Count);
                await summarizePipeline.RunAsync(taskConfig, config.Slicing, ct);
                await skillsPipeline.RunAsync(taskConfig, ct);
            },
            cancellationToken);
    }
    else
    {
        ValidateTaskConfig(config.Task, requireInput: true);
        logger.LogInformation("执行 Run：角色={Character}，剧本={Input}，模式={Mode}", config.Task.CharacterName, config.Task.InputFile, config.Task.OutputMode);

        var summarize = provider.GetRequiredService<SummarizePipeline>();
        await summarize.RunAsync(config.Task, config.Slicing, cancellationToken);

        var skills = provider.GetRequiredService<SkillsPipeline>();
        await skills.RunAsync(config.Task, cancellationToken);
    }

    logger.LogInformation("全部流程执行完毕");
    return 0;
});

return await rootCommand.Parse(args).InvokeAsync();

static (CliConfig Config, IServiceProvider Provider) BuildConfigAndServices(ParseResult parseResult, Option<string> configOption)
{
    var configPath = parseResult.GetValue(configOption);

    if (string.IsNullOrEmpty(configPath))
    {
        throw new InvalidOperationException("必须指定配置文件路径。请使用 --config 或 -c 参数。");
    }

    if (!File.Exists(configPath))
    {
        throw new FileNotFoundException($"配置文件不存在：{configPath}");
    }

    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(configPath, optional: false)
        .AddEnvironmentVariables(prefix: "GCS_")
        .Build();

    CliConfig config;
    try
    {
        config = configuration.Get<CliConfig>() ?? new CliConfig();
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException($"配置文件解析失败：{configPath}，原因：{ex.Message}", ex);
    }

    var apiKey = Environment.GetEnvironmentVariable("GCS_APIKEY");
    if (!string.IsNullOrEmpty(apiKey))
    {
        config.Llm.ApiKey = apiKey;
    }

    for (int i = 0; i < config.Jobs.Count; i++)
    {
        ValidateJobConfig(config.Jobs[i], i);
    }

    var services = new ServiceCollection();
    ServiceRegistrar.Register(services, config);
    var provider = services.BuildServiceProvider();

    return (config, provider);
}

static TaskConfig MapJobToTask(JobConfig job)
{
    return new TaskConfig
    {
        InputFiles = job.InputFiles,
        CharacterName = job.CharacterName,
        OutputDirectory = job.OutputDirectory,
        OutputMode = job.OutputMode,
        SkillsMaxContextChars = job.SkillsMaxContextChars
    };
}

static void ValidateJobConfig(JobConfig job, int index)
{
    if (job.InputFiles.Count == 0)
    {
        throw new ArgumentException($"Jobs[{index}]: InputFiles 不能为空");
    }

    if (string.IsNullOrWhiteSpace(job.CharacterName))
    {
        throw new ArgumentException($"Jobs[{index}]: CharacterName 不能为空");
    }

    if (!job.OutputMode.Equals("roleplay", StringComparison.OrdinalIgnoreCase)
        && !job.OutputMode.Equals("template", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException($"Jobs[{index}]: OutputMode 必须是 'roleplay' 或 'template'，当前值: '{job.OutputMode}'");
    }
}

static void ValidateTaskConfig(TaskConfig task, bool requireInput)
{
    if (requireInput && task.InputFiles.Count == 0 && string.IsNullOrWhiteSpace(task.InputFile))
    {
        throw new InvalidOperationException("未指定剧本文件。请在配置文件的 Task/InputFile 或 Task/InputFiles 中设置。");
    }

    if (string.IsNullOrWhiteSpace(task.CharacterName))
    {
        throw new InvalidOperationException("未指定角色名称。请在配置文件的 Task/CharacterName 中设置。");
    }
}

static async Task RunJobsAsync(
    IEnumerable<TaskConfig> jobs,
    int maxConcurrency,
    Func<TaskConfig, CancellationToken, Task> executeAsync,
    CancellationToken ct)
{
    if (maxConcurrency <= 1)
    {
        foreach (var job in jobs)
        {
            await executeAsync(job, ct);
        }
    }
    else
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(jobs, options, async (job, innerCt) =>
        {
            await executeAsync(job, innerCt);
        });
    }
}
