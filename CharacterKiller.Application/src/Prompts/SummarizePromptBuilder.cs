namespace CharacterKiller.Application.Prompts;

/// <summary>
/// Summarize 阶段 Prompt 构造器。
/// </summary>
public static class SummarizePromptBuilder
{
    public static string BuildSystemPrompt()
    {
        return """
            你是一位精通 Galgame/视觉小说文本分析的助手。
            你的任务是从给定的文本片段中，提取并归纳特定角色的信息。
            
            请按以下维度进行归纳：
            1. 性格特征与行事风格
            2. 背景故事与成长经历
            3. 人际关系与互动模式
            4. 语言习惯与口头禅
            5. 关键剧情节点中的表现
            
            输出要求：
            - 使用中文
            - 条理清晰，按要点罗列
            - 只输出与目标角色相关的内容，忽略其他角色
            - 如果片段中未出现目标角色，请明确说明"本片段未出现该角色"
            """;
    }

    public static string BuildUserPrompt(string characterName, string chunkContent)
    {
        return $"""
            目标角色：{characterName}

            文本片段：
            ---
            {chunkContent}
            ---

            请从上述文本中归纳角色「{characterName}」的相关信息。
            """;
    }
}
