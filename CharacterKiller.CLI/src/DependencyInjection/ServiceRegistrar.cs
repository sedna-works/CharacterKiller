using CharacterKiller.Core.Interfaces;
using CharacterKiller.Core.Models;
using CharacterKiller.Infrastructure.Llm;
using CharacterKiller.Infrastructure.Storage;
using CharacterKiller.Infrastructure.Tokenization;
using CharacterKiller.Application.Pipelines;
using CharacterKiller.CLI.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CharacterKiller.CLI.DependencyInjection;

/// <summary>
/// 依赖注册中心。
/// </summary>
public static class ServiceRegistrar
{
    public static void Register(IServiceCollection services, CliConfig config)
    {
        // 配置对象
        services.AddSingleton(config);
        services.AddSingleton(config.Llm);
        services.AddSingleton(config.Task);
        services.AddSingleton(config.Slicing);
        services.AddSingleton(config.Checkpoint);

        // 日志
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Token 估算器：默认字符近似，如需精确可改为 TiktokenEstimator
        services.AddSingleton<ITokenEstimator, CharBasedEstimator>();

        // 文件读取
        services.AddSingleton<IFileReader, LocalFileReader>();

        // Checkpoint 存储
        var checkpointDir = Path.IsPathRooted(config.Checkpoint.Directory)
            ? config.Checkpoint.Directory
            : Path.Combine(Directory.GetCurrentDirectory(), config.Checkpoint.Directory);
        services.AddSingleton<ICheckpointStore>(new JsonCheckpointStore(checkpointDir));

        // LLM 客户端（工厂模式创建）
        services.AddSingleton<ILlmClient>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return LlmClientFactory.Create(config.Llm, loggerFactory);
        });

        // Pipeline
        services.AddSingleton<SummarizePipeline>();
        services.AddSingleton<SkillsPipeline>();
    }
}
