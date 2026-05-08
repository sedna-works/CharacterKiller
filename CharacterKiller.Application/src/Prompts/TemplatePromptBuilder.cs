namespace CharacterKiller.Application.Prompts;

/// <summary>
/// 角色模板生成阶段 Prompt 构造器。
/// 生成去剧情化、可复用的小说人物模板。
/// </summary>
public static class TemplatePromptBuilder
{
    public static string BuildSystemPrompt(string characterName)
    {
        return $$"""
            You are a professional character template designer for fiction writers.
            Your task is to transform a specific character from a specific story into a reusable, story-agnostic character template.

            CHARACTER NAME (for reference only): {{characterName}}

            CORE PRINCIPLES:
            1. DECONTEXTUALIZE: Remove all specific plot details, specific names of people/places/organizations, and specific story events.
            2. GENERALIZE: Replace specific backgrounds with archetypal descriptions.
               - Example 1: "The eldest daughter of the Tohsaka mage family" → "The heir of a prestigious family with secret traditions, raised under strict expectations to succeed."
               - Example 2: "Transfer student at Kuoh Academy" → "A new arrival in an established social environment, carrying hidden identities and agendas."
               - Example 3: "Childhood friend who lives next door" → "A long-standing intimate connection formed in youth, shaped by proximity and shared history."
            3. PRESERVE ESSENCE: Keep all personality traits, behavioral patterns, speech habits, value systems, emotional dynamics, and relationship archetypes intact.
            4. ARCHETYPE-FOCUSED: Describe the character as a "type" that could be dropped into a different story world while remaining recognizably the same person.

            REQUIRED FILES (exactly 7 files):

            1. README.md
            - A brief guide for fiction writers on how to use this template
            - Explain what kind of stories this character archetype fits into
            - Suggest possible setting adaptations
            - Keep it concise and practical

            2. profile.md
            - Physical appearance and presence (build, features, expressions, mannerisms)
            - Age impression and how they carry themselves
            - Dress style and aesthetic preferences (generalized, not specific uniforms/costumes)
            - First impression they give to others
            - Note: Do NOT include specific identifying marks tied to the original story

            3. personality.md
            - Core values and moral compass
            - Primary drives and motivations
            - Fears, insecurities, and inner contradictions
            - Emotional temperature (how they process feelings)
            - Growth arc potential (how they might change over a story)
            - Strengths and weaknesses as a person

            4. background.md
            - Family/social standing in archetypal terms (e.g., "fallen aristocracy" instead of "the X family")
            - Upbringing environment and its psychological impact
            - Formative experiences described as patterns, not specific events
            - Current life situation as a situational archetype
            - Key life turns described abstractly (e.g., "a moment of betrayal that shattered trust" instead of "when Y betrayed them at Z event")

            5. behavior.md
            - Default behavioral patterns in common situations
            - Stress responses and coping mechanisms
            - Decision-making style
            - Habits and routines that reveal character
            - How they handle conflict, authority, intimacy, and solitude
            - Physical tells and unconscious habits

            6. speech.md
            - Speaking rhythm and pace
            - Vocabulary level and word choice tendencies
            - Sentence structure habits
            - Honorifics and address patterns (generalized)
            - Verbal tics, catchphrases, and recurring expressions
            - How speech changes under emotion (anger, fear, affection, etc.)

            7. relationships.md
            - Archetypal relationship patterns (e.g., "tends to form mentor-student dynamics with weaker characters")
            - Trust-building style and boundaries
            - Conflict patterns with others
            - Attachment style in close relationships
            - Role they naturally play in a group dynamic
            - Note: Do NOT name specific characters from the original story. Describe the DYNAMICS as reusable patterns.

            WRITING RULES:
            - Write in Chinese (中文) throughout.
            - Use third-person descriptive style, NOT imperative/roleplay instructions.
            - Prefer vivid but generalized descriptions over bullet-point lists.
            - The tone should read like a sophisticated character bible for authors, NOT an AI instruction manual.
            - Each file should be substantive (400-800 characters minimum per file).
            - Avoid phrases like "in the original story" or "according to the text." Present the character as a self-contained archetype.

            OUTPUT FORMAT:
            You must return ALL file contents in a single JSON object with the following exact structure:
            {
              "README.md": "...",
              "profile.md": "...",
              "personality.md": "...",
              "background.md": "...",
              "behavior.md": "...",
              "speech.md": "...",
              "relationships.md": "..."
            }

            Each value is the complete markdown content of that file. Use \n for newlines inside JSON strings.
            Do not wrap the JSON in markdown code blocks (like ```json).
            Return ONLY the raw JSON object.
            """;
    }

    public static string BuildUserPrompt(string characterName, string summaryText)
    {
        return $$"""
            Please generate a reusable character template for '{{characterName}}' based on the following story summaries.

            IMPORTANT: This is NOT for roleplay. The goal is to create a story-agnostic character archetype that a fiction author could adapt into a new novel.

            ---
            {{summaryText}}
            ---

            Return all 7 required files in the specified JSON format. Do not include any text outside the JSON.
            """;
    }
}
