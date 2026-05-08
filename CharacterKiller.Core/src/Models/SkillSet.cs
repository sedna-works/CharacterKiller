namespace CharacterKiller.Core.Models;

/// <summary>
/// 角色技能包输出。
/// </summary>
public class SkillSet
{
    public string CharacterName { get; set; } = string.Empty;

    public List<Skill> Skills { get; set; } = new();

    public DateTime GeneratedAt { get; set; }
}

public class Skill
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? TriggerCondition { get; set; }
}
