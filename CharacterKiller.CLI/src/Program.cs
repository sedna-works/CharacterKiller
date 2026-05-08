using System.CommandLine;
using CharacterKiller.CLI.Configuration;
using CharacterKiller.CLI.DependencyInjection;
using CharacterKiller.Application.Pipelines;
using CharacterKiller.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// --- 定义全局选项 ---
var configOption = new Option<string>("--config", "-c")
{
    Description = "配置文件路径（模型、切片、Checkpoint 等稳定配置）"
};

// --- 定义命令级选项 ---
var inputOption = new Option<string>("--input", "-i")
{
    Description = "剧本文件路径（单任务模式使用）"
};

var characterOption = new Option<string>("--character", "-n")
{
    Description = "角色分析名称（单任务模式使用）"
};

var modeOption = new Option<string>("--mode", "-m")
{
    Description = "输出模式：roleplay（默认，生成 AI 角色扮演 skill）或 template（生成可复用的小说人物模板）"
};

// --- 定义命令 ---
var rootCommand = new RootCommand("CharacterKiller - Galgame 角色信息提炼工具")
{
    configOption
};

var summarizeCommand = new Command("summarize", "执行文本切片归纳");
var skillsCommand = new Command("skills", "基于 summary 生成角色技能");
var runCommand = new Command("run", "执行 summarize + skills（默认）");

rootCommand.Subcommands.Add(summarizeCommand);
rootCommand.Subcommands.Add(skillsCommand);
rootCommand.Subcommands.Add(runCommand);

// summarize 和 run 需要 input + character
summarizeCommand.Options.Add(inputOption);
summarizeCommand.Options.Add(characterOption);
runCommand.Options.Add(inputOption);
runCommand.Options.Add(characterOption);

skillsCommand.Options.Add(characterOption);

summarizeCommand.Options.Add(modeOption);
skillsCommand.Options.Add(modeOption);
runCommand.Options.Add(modeOption);

// --- 设置 Action ---

summarizeCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var (config, provider) = BuildConfigAndServices(parseResult, configOption);
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("CharacterKiller");

    if (config.Jobs.Count > 0)
    {
        logger.LogInformation("检测到批量任务，共 {Count} 个角色", config.Jobs.Count);
        var pipeline = provider.GetRequiredService<SummarizePipeline>();
        foreach (var taskConfig in config.Jobs.Select(MapJobToTask))
        {
            logger.LogInformation("批量 Summarize：角色={Character}，文件数={FileCount}", taskConfig.CharacterName, taskConfig.InputFiles.Count);
            await pipeline.RunAsync(taskConfig, config.Slicing, cancellationToken);
        }
    }
    else
    {
        ApplyTaskOverrides(config, parseResult, inputOption, characterOption, modeOption);
        ValidateTaskConfig(config.Task, requireInput: true);
        logger.LogInformation("执行 Summarize：角色={Character}，剧本={Input}", config.Task.CharacterName, config.Task.InputFile);
        var pipeline = provider.GetRequiredService<SummarizePipeline>();
        await pipeline.RunAsync(config.Task, config.Slicing, cancellationToken);
    }

    logger.LogInformation("Summarize 执行完毕");
    return 0;
});

skillsCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var (config, provider) = BuildConfigAndServices(parseResult, configOption);
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("CharacterKiller");

    if (config.Jobs.Count > 0)
    {
        logger.LogInformation("检测到批量任务，共 {Count} 个角色", config.Jobs.Count);
        var pipeline = provider.GetRequiredService<SkillsPipeline>();
        foreach (var taskConfig in config.Jobs.Select(MapJobToTask))
        {
            logger.LogInformation("批量 Skills：角色={Character}", taskConfig.CharacterName);
            await pipeline.RunAsync(taskConfig, cancellationToken);
        }
    }
    else
    {
        ApplyTaskOverrides(config, parseResult, inputOption: null, characterOption, modeOption);
        ValidateTaskConfig(config.Task, requireInput: false);
        logger.LogInformation("执行 Skills：角色={Character}，模式={Mode}", config.Task.CharacterName, config.Task.OutputMode);
        var pipeline = provider.GetRequiredService<SkillsPipeline>();
        await pipeline.RunAsync(config.Task, cancellationToken);
    }

    logger.LogInformation("Skills 执行完毕");
    return 0;
});

runCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var (config, provider) = BuildConfigAndServices(parseResult, configOption);
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("CharacterKiller");

    if (config.Jobs.Count > 0)
    {
        logger.LogInformation("检测到批量任务，共 {Count} 个角色", config.Jobs.Count);
        var summarizePipeline = provider.GetRequiredService<SummarizePipeline>();
        var skillsPipeline = provider.GetRequiredService<SkillsPipeline>();

        foreach (var taskConfig in config.Jobs.Select(MapJobToTask))
        {
            logger.LogInformation("批量 Run：角色={Character}，文件数={FileCount}", taskConfig.CharacterName, taskConfig.InputFiles.Count);
            await summarizePipeline.RunAsync(taskConfig, config.Slicing, cancellationToken);
            await skillsPipeline.RunAsync(taskConfig, cancellationToken);
        }
    }
    else
    {
        ApplyTaskOverrides(config, parseResult, inputOption, characterOption, modeOption);
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

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    // 没有子命令时默认执行 run
    var (config, provider) = BuildConfigAndServices(parseResult, configOption);
    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("CharacterKiller");

    if (config.Jobs.Count > 0)
    {
        logger.LogInformation("检测到批量任务，共 {Count} 个角色", config.Jobs.Count);
        var summarizePipeline = provider.GetRequiredService<SummarizePipeline>();
        var skillsPipeline = provider.GetRequiredService<SkillsPipeline>();

        foreach (var taskConfig in config.Jobs.Select(MapJobToTask))
        {
            logger.LogInformation("批量 Run：角色={Character}，文件数={FileCount}", taskConfig.CharacterName, taskConfig.InputFiles.Count);
            await summarizePipeline.RunAsync(taskConfig, config.Slicing, cancellationToken);
            await skillsPipeline.RunAsync(taskConfig, cancellationToken);
        }
    }
    else
    {
        ApplyTaskOverrides(config, parseResult, inputOption, characterOption, modeOption);
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

// --- 入口 ---
return await rootCommand.Parse(args).InvokeAsync();

// --- 辅助方法 ---

static (CliConfig Config, IServiceProvider Provider) BuildConfigAndServices(ParseResult parseResult, Option<string> configOption)
{
    var configPath = parseResult.GetValue(configOption) ?? "appsettings.json";

    if (!File.Exists(configPath))
    {
        Console.Error.WriteLine($"配置文件不存在：{configPath}");
        Environment.Exit(1);
    }

    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile(configPath, optional: false)
        .AddEnvironmentVariables(prefix: "GCS_")
        .Build();

    var config = configuration.Get<CliConfig>() ?? new CliConfig();

    var apiKey = Environment.GetEnvironmentVariable("GCS_APIKEY");
    if (!string.IsNullOrEmpty(apiKey))
    {
        config.Llm.ApiKey = apiKey;
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
        VndbCharacterId = job.VndbCharacterId,
        OutputDirectory = job.OutputDirectory,
        OutputMode = job.OutputMode
    };
}

static void ApplyTaskOverrides(CliConfig config, ParseResult parseResult, Option<string>? inputOption, Option<string>? characterOption, Option<string>? modeOption = null)
{
    if (inputOption != null)
    {
        var input = parseResult.GetValue(inputOption);
        if (!string.IsNullOrEmpty(input))
        {
            config.Task.InputFile = input;
        }
    }

    if (characterOption != null)
    {
        var character = parseResult.GetValue(characterOption);
        if (!string.IsNullOrEmpty(character))
        {
            config.Task.CharacterName = character;
        }
    }

    if (modeOption != null)
    {
        var mode = parseResult.GetValue(modeOption);
        if (!string.IsNullOrEmpty(mode))
        {
            config.Task.OutputMode = mode;
        }
    }
}

static void ValidateTaskConfig(TaskConfig task, bool requireInput)
{
    if (requireInput && task.InputFiles.Count == 0 && string.IsNullOrWhiteSpace(task.InputFile))
    {
        Console.Error.WriteLine("错误：未指定剧本文件。请使用 --input 或 -i 参数指定，或在配置文件中设置 Jobs/InputFiles。");
        Environment.Exit(1);
    }

    if (string.IsNullOrWhiteSpace(task.CharacterName))
    {
        Console.Error.WriteLine("错误：未指定角色名称。请使用 --character 或 -n 参数指定，或在配置文件中设置 Jobs/CharacterName。");
        Environment.Exit(1);
    }
}
