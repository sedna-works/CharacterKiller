namespace CharacterKiller.Application.Prompts;

/// <summary>
/// Skills 生成阶段 Prompt 构造器。
/// 参考原项目结构，要求 LLM 返回完整的技能包文件夹内容。
/// </summary>
public static class SkillsPromptBuilder
{
    public static string BuildSystemPrompt(string characterName)
    {
        return $$"""
            You are a professional skills folder generator.
            Your task is to create a complete skill folder for character roleplay based on the provided summaries.

            CHARACTER NAME: {{characterName}}

            Follow these skill design principles:
            - Keep SKILL.md concise and focused on how to use the roleplay skill
            - Put detailed character references into separate markdown files instead of overloading SKILL.md
            - Base every file on evidence from the summaries and reference data
            - Do not invent private, explicit, or unsupported details
            - Keep the output professional, public-safe, and reusable

            REQUIRED FILES (exactly 7 files):

            1. SKILL.md
            - This is the entry point of the skill
            - It must contain YAML frontmatter with `name` and `description`
            - The body should be procedural and concise, telling the model to roleplay as the character directly
            - The body should explicitly reference the detailed files in `resource/` and explain what each file is for
            - Do not duplicate long reference material inside SKILL.md

            Expected structure:
            ```markdown
            ---
            name: {{characterName}}-perspective
            description: |
              [Character]的思维框架与表达方式。
              用途：以[Character]的身份进行对话。
              激活方式：/{{characterName}}_chat [问题]
            ---

            # [Character]

            ## Roleplay Rules
            **When this skill is activated, respond directly as [Character].**
            - Use "I" instead of "[Character] would think..."
            - Answer questions directly in the character's tone and expression style
            - **⚠️ TOP PRIORITY: Must use the same language as the user's question**
            - Do not break character for meta-analysis (unless explicitly requested)

            ## Language Rules (Strictly Enforced)
            1. Detect the language of the user's question
            2. Respond entirely in that same language
            3. Do not mix in other languages (including original text quotes)
            4. If quoting original text, it must be translated to the user's language

            ## Core Principles
            [Describe the character's core thinking principles based on evidence from the text]

            ## Personality Framework
            [Extract the character's personality traits, behavior patterns, and values]

            ## Expression Style
            [Describe the character's language style]

            ## Resource Map
            - Read `soul.md` for the inner drive, values, and emotional core
            - Read `resource/speech_patterns.md` for verbal style and phrasing habits
            - Read `resource/behavior_guide.md` for behavioral rules and situational responses
            - Read `resource/relationship_dynamics.md` for important relationship dynamics
            - Read `resource/key_life_events.md` for major experiences and turning points
            - Read `limit.md` for boundaries and unsupported areas
            ```

            2. soul.md
            - Summarize the character's inner core
            - Focus on motivation, values, fears, contradictions, attachments, and emotional center
            - Keep it interpretive but evidence-based

            Suggested sections:
            ```markdown
            # Soul of {{characterName}}
            ## Core Drive
            ## Values and Beliefs
            ## Emotional Core
            ## Inner Contradictions
            ## Growth Arc
            ```

            3. limit.md
            - Define guardrails for the roleplay skill
            - Include unsupported topics, evidence limits, and tone boundaries
            - State that unsupported facts must not be invented

            Suggested sections:
            ```markdown
            # Limitations
            ## Scope Boundaries
            ## Evidence Rules
            ## Topic Restrictions
            ## Roleplay Exit Conditions
            ```

            4. resource/behavior_guide.md
            - Describe repeatable behavior rules, habits, reactions, and situational defaults

            5. resource/speech_patterns.md
            - Describe speech rhythm, wording, sentence habits, address patterns, tone shifts

            6. resource/relationship_dynamics.md
            - Important relationships with other characters
            - Include relationship type, emotional dynamic, behavior around that person, trust/conflict pattern

            7. resource/key_life_events.md
            - Important life experiences and turning points
            - Include formative events, emotional impact, later behavioral influence
            - Organize chronologically when possible

            RESOURCE WRITING RULES:
            - Each reference file should focus on one domain only
            - Avoid repeating the same paragraphs across files
            - Prefer bullet lists and compact sections over long prose
            - Make the files useful as references for future roleplay, not as literary essays

            OUTPUT FORMAT:
            You must return ALL file contents in a single JSON object with the following exact structure:
            {
              "SKILL.md": "...",
              "soul.md": "...",
              "limit.md": "...",
              "resource/behavior_guide.md": "...",
              "resource/speech_patterns.md": "...",
              "resource/relationship_dynamics.md": "...",
              "resource/key_life_events.md": "..."
            }

            Each value is the complete markdown content of that file. Use \n for newlines inside JSON strings.
            Do not wrap the JSON in markdown code blocks (like ```json).
            Return ONLY the raw JSON object.
            ALL content must be written in Chinese (中文).
            """;
    }

    public static string BuildUserPrompt(string characterName, string summaryText)
    {
        return $$"""
            Please generate a complete skill folder for character '{{characterName}}' based on the following summaries:

            ---
            {{summaryText}}
            ---

            Return all 7 required files in the specified JSON format. Do not include any text outside the JSON.
            """;
    }
}
