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

            输出格式要求（严格遵守）：
            - 使用中文
            - 每个维度使用三级标题，格式为 "### 1. 标题"，序号在前，点后有空格，标题简明
            - 每个要点使用列表项，格式为 "- **加粗小标题**：具体内容"
            - 每个列表项必须独占一行，小标题加粗，冒号后空一格再写内容
            - 不同维度之间用空行分隔
            - 只输出与目标角色相关的内容，忽略其他角色
            - 如果片段中未出现目标角色，请只输出一行："本片段未出现该角色。"

            错误示例（不要这样输出）：
            ### . 性格与行事风格1
            - **外冷内热**：登场时沉默寡言眼神不带一丝感情...（一行内超过 80 字）

            正确示例（必须这样输出）：
            ### 1. 性格与行事风格
            - **外冷内热**：登场时沉默寡言，眼神不带一丝感情。
            - **使魔至上**：对使魔抱有极端的爱惜与执着。
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

            请从上述文本中归纳角色「{characterName}」的相关信息，严格按照系统提示中的格式要求输出。
            """;
    }
}
