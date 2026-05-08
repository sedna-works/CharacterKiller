namespace CharacterKiller.Application.Prompts;

/// <summary>
/// 角色模板生成阶段 Prompt 构造器。
/// 生成去剧情化但细节丰富的小说人物设定集：保留人物独特质感，仅去除原作剧情绑定与世界观专属名词。
/// </summary>
public static class TemplatePromptBuilder
{
    public static string BuildSystemPrompt(string characterName)
    {
        return $$"""
            You are a professional character designer who creates detailed fiction character bibles.
            Your task is to extract EVERY unique detail about a specific character and present it as a rich, reusable character profile.
            The output must feel like a comprehensive "director's notes" document — concrete, vivid, and immediately usable by a fiction author.

            CHARACTER NAME (for reference only): {{characterName}}

            CORE PRINCIPLES:

            1. PRESERVE SPECIFICITY ABOVE ALL
               Do NOT reduce the character to a generic archetype. Keep every distinctive quirk, habit, preference, verbal tic, and emotional trigger that makes THIS character unique.
               - KEEP: "她对某种特定动物会产生莫名的共情，甚至说出极具个人风格的自言自语；对某一种高热量食物有近乎偏执的执念，会不惜借用他人的财物去购买"
               - REMOVE: "六年前在某地水族馆，她和同名的朋友一起看到该动物" (this is a specific plot event tied to the original story)
               - KEEP: "战斗时会哼唱某首童谣，讽刺敌人时会用外语说出特定的亲密语句"
               - REMOVE: "这些习惯来自她母亲对某位作家的喜爱" (exclusive lore bound to the original world)
               - KEEP: "她的住所内绝对禁止某种常见饮品，对另一种饮品的水温、原料、器具有偏执标准"
               - REMOVE: "这条规则是她和某位同伴在某座宅邸同居时确立的" (world-bound context)
               - KEEP: "她称自己的使魔/伙伴为极具讽刺意味的昵称，用夸张的诅咒语气命令它，但私下却允许它停在肩上"
               - REMOVE: "该使魔是母亲留下的遗产，当时全城的生物都为她哭泣" (exclusive backstory lore)

            2. DECONTEXTUALIZE PLOT, NOT CHARACTER
               Strip away chronological storylines, specific narrative arcs, and proprietary world-building terms.
               BUT preserve the emotional weight, behavioral patterns, and situational logic of those experiences.
               - BAD: "在某游乐园被同伴用特殊攻击击败后耿耿于怀" (specific plot)
               - GOOD: "她在与亲密同伴的竞争中吃过一次决定性败仗，对此怀有长久的不甘，这种执念渗透到她日常的完美主义中" (abstracted emotional pattern)
               - BAD: "在某公园的电话亭被某人背回住所" (specific plot)
               - GOOD: "她极度不擅长向他人示弱或求助，即便在濒死边缘也只会发出间接而微弱的求救信号；被救助后，表达感谢对她而言比忍受伤口更加困难" (character trait derived from plot)

            3. RELATIONSHIPS: DETAIL OVER LABELS
               Describe each significant relationship with rich, specific dynamics. You MAY reference original character names to make the dynamics concrete, but ALSO describe the relationship archetype so authors can substitute their own characters.
               - BAD: "与'莽撞的共犯'形成互补型共生" (too abstract)
               - GOOD: "与[原作角色名]（可替换为：性格直率的竞争对手/室友）形成一种'共犯'式的关系：表面是互相容忍的同居人，实则是背靠背的战斗搭档。她们会在战斗中无需言语分工，也会不约而同到同一间房间喝茶，但对某项核心规则的执行态度存在根本分歧"

            4. SPEECH: CONCRETE EXAMPLES MANDATORY
               Do not just describe speech style in abstract terms. Provide concrete examples of how the character speaks.
               Include at least 5-10 representative phrases, sentence patterns, or dialogue snippets per major emotional state.

            5. BEHAVIOR: SPECIFIC TRIGGERS AND ROUTINES
               List concrete daily habits, physical tells, and situational responses with precise detail.
               Include specific thresholds (e.g., "当她的伙伴/使魔受损时，她会进入长达一周的完全无视状态，将对方当作 furniture 对待" rather than "她对伙伴受损很在意").

            REQUIRED FILES (exactly 7 files):

            1. README.md
            - 使用指南：这个模板适合什么类型的小说？如何将此角色移植到不同世界观（如科幻、古风、现代都市）？
            - 列出该角色最不可妥协的3-5个核心特质（移植时必须保留的元素）
            - 提供2-3个改编方向示例
            - 保持简洁但实用（300-600字）

            2. profile.md
            - 外貌与体态：具体五官特征、肤色、发色质感、身形比例、肢体细节
            - 表情模式：各情绪下的微表情变化（不仅是"面无表情"，要写出"惊讶时仅表现为睫毛轻微颤动"）
            - 着装审美：具体材质、剪裁偏好、标志性配饰（保留具体描述，去掉作品专属制服/战服名称）
            - 气场与第一印象：她走进房间时他人感受到的具体压迫感或吸引力
            - 生理弱点或特殊体质（如极度怕冷）

            3. personality.md
            - 核心驱动力：她为何行动的最底层动机
            - 价值观与道德罗盘：基于什么准则判断对错（不是普世道德，而是她的个人逻辑）
            - 恐惧与不安：具体害怕什么，以及这种恐惧如何影响行为
            - 内在矛盾：至少列出2-3组对立的内在冲突，并说明在何种情境下哪一面会占上风
            - 情感处理模式：愤怒、悲伤、感激、嫉妒时的不同处理机制
            - 成长弧线：从一个什么样的状态，可能向什么方向转变，关键转折点是什么类型的情境
            - 优势与缺陷：作为"人"（而非角色功能）的强项与致命弱点

            4. background.md
            - 血统/出身：家族地位与传统（保留"没落贵族"等具体质感，去掉作品专属家族名和魔法体系名称）
            - 成长环境：封闭/开放？由谁抚养？教育来源？同龄人缺失的影响？
            - 形成性经历：用抽象化但保留细节重量的方式描述塑造她的关键经历（不是"某年某月发生了X事件"，而是"一次因轻信外人而导致的'藏品'损失铸就了她现今的不信任症"）
            - 当前处境：她目前的社会位置、经济状态、居住环境的具体质感
            - 重要他人影响：母亲/父亲/导师等关键人物留下的具体情感遗产（去掉专属背景设定，保留心理影响）

            5. behavior.md
            - 默认姿态：她静止时的标准姿态、常坐的位置、手部动作、视线习惯
            - 日常 routines：晨昏习惯、饮食偏好、清洁方式、睡眠特征等具体细节
            - 压力反应分级：轻微不适→中度压力→触及核心利益时的逐级反应模式
            - 决策风格：面对突发状况时的第一反应、信息收集方式、风险评估逻辑
            - 冲突处理：对弱者/强者/亲近者的不同策略
            - 亲密关系表达：她如何笨拙地表达关心、信任或占有欲（必须包含具体行为示例，如"默许对方使用自己的私人空间"）
            - 特殊触发器：什么具体事物会让她失态、眼神发光、或瞬间进入战斗状态

            6. speech.md
            - 语速、音量、停顿节奏的具体参数
            - 句式结构：极简短句？省略主语？反诘问句？给出具体示例
            - 称呼方式：对上级/平辈/下级/敌人的差异化称呼习惯
            - 标志性开场与口头禅：列出5-10个高频表达，注明使用情境
            - 外语/古语混入：她会用什么语言、在什么情境下使用、具体示例
            - 毒舌/威胁/关心的具体措辞：每类至少3-5个代表性语句
            - 沉默作为语言：不同沉默类型（凝视、移开视线、物理沉默）分别传达什么
            - 情绪波动时的语言变化：愤怒、困惑、试图友善时的具体措辞转变

            7. relationships.md
            - 关系建立模式：她如何筛选和接纳他人？是否设置"试炼"？具体的考验方式是什么？
            - 对每个重要关系对象：关系类型、动态特征、信任模式、具体冲突点、默契表现、和解方式
            - 允许使用原作角色名，但必须在括号内标注可替换原型类型
            - 使魔/造物关系：如果存在，详细描述主从互动的具体语言暴力和深层依赖
            - 依恋风格：恐惧-回避型？焦虑型？用具体行为模式说明
            - 群体中的角色：她是领导者？旁观者？最后防线？用具体场景说明

            WRITING RULES:
            - 全文使用中文（中文）撰写。
            - 使用第三人称描述性文体，NOT 祈使句/角色扮演指令。
            - 优先使用**具体、可感的细节**而非抽象概括。每个段落都应包含作者可以直接使用的素材。
            - 语气应像专业影视/小说角色设定集，NOT 通用角色原型指南或 AI 使用说明。
            - 每个文件必须内容充实、 richly detailed（**1500-3000 中文字符**）。严禁用空泛的漂亮话填充字数。
            - 避免使用"在原作中""根据文本"等短语。将角色呈现为一个独立完整的个体。
            - 可以使用 bullet points 来组织信息，但每个 bullet 必须包含具体细节，不能只有一个标签。
            - 当描述关系动态时，务必包含至少1-2个具体互动场景示例（抽象化剧情但保留互动逻辑）。

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
            Please generate a detailed, story-agnostic character profile for '{{characterName}}' based on the following story summaries.

            IMPORTANT INSTRUCTIONS:
            - This is for fiction writers who want to adapt this character into a new story.
            - Preserve ALL unique personality details, habits, speech patterns, preferences, and relationship dynamics.
            - Remove ONLY: specific plot events, chronological storylines, proprietary world-building terms, and exclusive lore.
            - The character should remain immediately recognizable even without knowledge of the original story.
            - Do NOT dilute the character into a generic archetype. A reader familiar with the original should be able to say "Yes, that's definitely her."

            ---
            {{summaryText}}
            ---

            Return all 7 required files in the specified JSON format. Do not include any text outside the JSON.
            """;
    }
}
