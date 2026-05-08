namespace CharacterKiller.Application.Prompts;

/// <summary>
/// 去剧情化角色模板（Template）Skill 生成器。
/// 输出结构与 roleplay skill 完全一致，但内容去剧情化、模糊世界观绑定，保留角色原型特征，
/// 使其可被直接整合进 AI 剧本工作流或跨作品复用。
/// </summary>
public static class TemplatePromptBuilder
{
    public static string BuildSystemPrompt(string characterName)
    {
        return $$"""
            You are a professional skill folder generator for reusable character templates.
            Your task is to create a complete skill folder based on the provided summaries,
            but with all plot-specific details, proprietary world-building terms, and exclusive lore removed.
            The character must remain immediately recognizable as a unique individual,
            while being abstracted into a reusable fiction prototype.

            CHARACTER NAME (for reference only): {{characterName}}

            CORE PRINCIPLES:

            1. PRESERVE SPECIFICITY, REMOVE PLOT BINDING
               Keep every distinctive quirk, habit, preference, verbal tic, and emotional trigger.
               Strip away chronological storylines, specific narrative arcs, and proprietary world-building terms.
               - KEEP: "她对某种特定动物会产生莫名的共情；对某一种高热量食物有近乎偏执的执念"
               - REMOVE: "六年前在某地水族馆，她和同名的朋友一起看到该动物" (specific plot event)
               - KEEP: "战斗时会哼唱某首童谣，讽刺敌人时会用外语说出特定的亲密语句"
               - REMOVE: "这些习惯来自她母亲对某位作家的喜爱" (exclusive lore)
               - KEEP: "她的住所内绝对禁止某种常见饮品，对另一种饮品的水温、原料、器具有偏执标准"
               - REMOVE: "这条规则是她和某位同伴在某座宅邸同居时确立的" (world-bound context)

            2. DECONTEXTUALIZE PLOT, NOT CHARACTER
               Abstract specific events into emotional patterns and behavioral logic.
               - BAD: "在某游乐园被同伴用特殊攻击击败后耿耿于怀" (specific plot)
               - GOOD: "她在与亲密同伴的竞争中吃过一次决定性败仗，对此怀有长久的不甘，这种执念渗透到她日常的完美主义中" (abstracted emotional pattern)
               - BAD: "在某公园的电话亭被某人背回住所" (specific plot)
               - GOOD: "她极度不擅长向他人示弱或求助，即便在濒死边缘也只会发出间接而微弱的求救信号；被救助后，表达感谢对她而言比忍受伤口更加困难" (character trait derived from plot)

            3. RELATIONSHIPS: DETAIL OVER LABELS
               Describe each significant relationship with rich, specific dynamics.
               You MAY reference original character names to make the dynamics concrete,
               but ALSO describe the relationship archetype so authors can substitute their own characters.
               - BAD: "与'莽撞的共犯'形成互补型共生" (too abstract)
               - GOOD: "与[原作角色名]（可替换为：性格直率的竞争对手/室友）形成一种'共犯'式的关系：表面是互相容忍的同居人，实则是背靠背的战斗搭档"

            4. WORLDVIEW-AGNOSTIC LANGUAGE
               Replace proprietary terms with descriptive equivalents.
               - "魔术" → "她所使用的某种非日常能力体系" (or keep as "her unique abilities" without naming the system)
               - "使魔" → "她所驱使的某种非人存在/造物/伙伴"
               - 专有家族名 → "某个古老的神秘家族"
               - 具体地名 → 删除或泛化为"某座偏远的洋馆/居所"

            REQUIRED FILES (exactly 7 files, same structure as roleplay skill):

            1. SKILL.md
            - This is the entry point of the skill
            - YAML frontmatter with `name` and `description`
            - The body must describe this as a REUSABLE CHARACTER TEMPLATE, not a specific story role
            - Explain how to activate and use this template across different stories/worldviews
            - Reference the detailed files in `resource/` and `soul.md`

            Expected structure:
            ```markdown
            ---
            name: {{characterName}}-template
            description: |
              [Character]的可复用角色模板。
              用途：将[Character]的原型特征移植到新的故事或世界观中。
              激活方式：/{{characterName}}_template [场景描述]
            ---

            # [Character] - 可复用角色模板

            ## 使用说明
            **当此 skill 被激活时，以[Character]的原型特征为基础进行角色扮演或创作。**
            - 保留角色的核心性格、行为模式、语言习惯，但去除原作剧情绑定
            - 可将角色置于任何世界观（科幻、古风、现代都市、奇幻等）
            - 回答问题时直接使用"我"，而非"[Character]会想..."

            ## 核心原型特征
            [提取角色最不可妥协的3-5个核心特质，这些是在任何世界观中都必须保留的元素]

            ## 改编方向示例
            - 现代都市版：[如何移植]
            - 科幻版：[如何移植]
            - 古风版：[如何移植]

            ## 资源映射
            - Read `soul.md` for the inner drive, values, and emotional core
            - Read `resource/speech_patterns.md` for verbal style and phrasing habits
            - Read `resource/behavior_guide.md` for behavioral rules and situational responses
            - Read `resource/relationship_dynamics.md` for important relationship dynamics
            - Read `resource/key_life_events.md` for formative experiences (decontextualized)
            - Read `limit.md` for boundaries and unsupported areas
            ```

            2. soul.md
            - Summarize the character's inner core
            - Focus on motivation, values, fears, contradictions, attachments, and emotional center
            - Keep it interpretive but evidence-based
            - Remove all world-building-specific context (e.g., "because of her family's magic heritage" → "due to her inherited burden")

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
            - Define guardrails for the template
            - Include unsupported topics, evidence limits, and tone boundaries
            - State that this is a prototype template, not a canonical biography
            - Clarify which details are confirmed from text vs. inferred

            Suggested sections:
            ```markdown
            # Limitations
            ## Scope Boundaries
            ## Evidence Rules
            ## Topic Restrictions
            ## Adaptation Guidelines
            ```

            4. resource/behavior_guide.md
            - Describe repeatable behavior rules, habits, reactions, and situational defaults
            - Include specific triggers and routines with precise detail
            - Use abstracted language: "当她所珍视的存在受到威胁时" instead of "当她的使魔受伤时"

            5. resource/speech_patterns.md
            - Describe speech rhythm, wording, sentence habits, address patterns, tone shifts
            - Provide concrete examples (5-10 representative phrases per major emotional state)
            - Include foreign language / ancient language usage patterns with examples

            6. resource/relationship_dynamics.md
            - Important relationships with other characters
            - Include relationship type, emotional dynamic, behavior around that person, trust/conflict pattern
            - For each relationship, provide: original character name + replaceable archetype in parentheses
            - Include at least 1-2 abstracted interaction scene examples per relationship

            7. resource/key_life_events.md
            - Formative experiences and turning points
            - Abstract specific plot events into emotional/behavioral impact descriptions
            - Organize thematically rather than chronologically
            - Example: Instead of "In year X at location Y, event Z happened",
              write "A decisive defeat in a close competition left a lasting obsession with perfectionism"

            WRITING RULES:
            - ALL content must be written in Chinese (中文).
            - Use third-person descriptive prose, NOT imperative/roleplay instructions.
            - Prioritize concrete, sensory details over abstract summaries.
            - Each bullet point must contain specific, usable material (no empty labels).
            - Avoid phrases like "in the original story" or "according to the text".
            - Present the character as a complete, independent prototype.

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
            """;
    }

    public static string BuildUserPrompt(string characterName, string summaryText)
    {
        return $$"""
            Please generate a reusable character template skill folder for '{{characterName}}' based on the following story summaries.

            IMPORTANT INSTRUCTIONS:
            - The output must be a complete skill folder with the SAME file structure as a roleplay skill (SKILL.md, soul.md, limit.md, resource/*.md).
            - Preserve ALL unique personality details, habits, speech patterns, preferences, and relationship dynamics.
            - Remove ONLY: specific plot events, chronological storylines, proprietary world-building terms, and exclusive lore.
            - The character should remain immediately recognizable even without knowledge of the original story.
            - Do NOT dilute the character into a generic archetype.

            ---
            {{summaryText}}
            ---

            Return all 7 required files in the specified JSON format. Do not include any text outside the JSON.
            """;
    }
}
