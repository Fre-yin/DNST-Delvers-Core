namespace DungeonSettlersDelvers.Core;

// Names and descriptions of the two effects of the enemy abilities (EnemyAbilityRules.SilenceKey,
// SleepKey). The effects read them by key, so the rows go into the game's text table
// (UniqueCandidateLocalization).
internal static class EnemyAbilityText
{
    internal static readonly UniqueCandidateTranslations SilenceName = new(
        "Silence", "침묵", "Silence", "Schweigen", "Безмолвие",
        "沉默", "沉默", "沈黙", "Silencio", "Silêncio");

    internal static readonly UniqueCandidateTranslations SilenceDescription = new(
        "No skills, basic attack only.",
        "스킬 사용 불가, 기본 공격만 가능.",
        "Aucune compétence, attaque de base uniquement.",
        "Keine Fertigkeiten, nur Grundangriff.",
        "Без умений, только обычная атака.",
        "无法使用技能，只能普通攻击。",
        "無法使用技能，只能普通攻擊。",
        "スキル使用不可、通常攻撃のみ。",
        "Sin habilidades, solo ataque básico.",
        "Sem habilidades, só ataque básico.");

    internal static readonly UniqueCandidateTranslations SleepName = new(
        "Asleep", "수면", "Endormi", "Schlaf", "Сон",
        "睡眠", "睡眠", "睡眠", "Dormido", "Dormindo");

    internal static readonly UniqueCandidateTranslations SleepDescription = new(
        "Cannot act, wakes when hurt.",
        "행동 불가, 피해를 입으면 깨어남.",
        "Ne peut pas agir, se réveille quand il est blessé.",
        "Handlungsunfähig, wacht bei Schaden auf.",
        "Не может действовать, просыпается от урона.",
        "无法行动，受到伤害时醒来。",
        "無法行動，受到傷害時醒來。",
        "行動不能、ダメージで目覚める。",
        "No puede actuar, despierta al recibir daño.",
        "Não pode agir, acorda ao sofrer dano.");

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (WoundText.NameKey(EnemyAbilityRules.SilenceKey), SilenceName), (WoundText.DescKey(EnemyAbilityRules.SilenceKey), SilenceDescription),
        (WoundText.NameKey(EnemyAbilityRules.SleepKey), SleepName), (WoundText.DescKey(EnemyAbilityRules.SleepKey), SleepDescription)
    };
}
