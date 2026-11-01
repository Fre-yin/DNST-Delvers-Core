namespace DungeonSettlersDelvers.Core;

// Words of the bleeding wound (drafts, the author approves EN/DE). The three
// effects read their name and description by key, so those rows go into the game's text table
// (UniqueCandidateLocalization); the health tooltip line and the Apocalypse tooltip row are picked
// directly.
internal static class WoundText
{
    internal const string LightKey = CoreSaveEffects.Prefix + "WoundLight";
    internal const string DeepKey = CoreSaveEffects.Prefix + "WoundDeep";
    // The third stage is "heavy" now; its effect key keeps the old name, so older saves still load.
    internal const string HeavyKey = CoreSaveEffects.Prefix + "WoundLethal";
    // The lethal wound, a new key; a save without Core keeps it through its stand-in (ReviveEffects).
    internal const string FatalKey = CoreSaveEffects.Prefix + "WoundFatal";

    internal static string NameKey(string effect) => "TEXTKEY_" + effect + "_NAME";
    internal static string DescKey(string effect) => "TEXTKEY_" + effect + "_DESC";

    // The band's colour per stage, one table for the portrait, the status window and the bar over the head, and for the stage's
    // name in the tooltip (all red, from the lightest red at light bleeding to the dark red at lethal, in
    // even steps; the lethal stage's zigzag with the darker LethalDarkColour sets it apart).
    internal static readonly IReadOnlyDictionary<WoundRules.Stage, string> Colours = new Dictionary<WoundRules.Stage, string>
    {
        [WoundRules.Stage.Light] = "#F04848",
        [WoundRules.Stage.Deep] = "#DE2230",
        [WoundRules.Stage.Heavy] = "#C01424",
        [WoundRules.Stage.Fatal] = "#A00818"
    };

    internal const string LethalDarkColour = "#5E000E";

    // The band over the head shows only that the unit bleeds, in one red for every stage.
    internal const string HudColour = "#B9062A";

    internal static readonly UniqueCandidateTranslations LightName = new(
        "Light Bleeding",
        "가벼운 출혈",
        "Saignement léger",
        "Leichte Blutung",
        "Лёгкое кровотечение",
        "轻度流血",
        "輕度流血",
        "軽い出血",
        "Hemorragia leve",
        "Sangramento leve");

    internal static readonly UniqueCandidateTranslations DeepName = new(
        "Deep Bleeding",
        "깊은 출혈",
        "Saignement profond",
        "Tiefe Blutung",
        "Глубокое кровотечение",
        "深度流血",
        "深度流血",
        "深い出血",
        "Hemorragia profunda",
        "Sangramento profundo");

    internal static readonly UniqueCandidateTranslations HeavyName = new(
        "Heavy Bleeding",
        "심한 출혈",
        "Saignement grave",
        "Schwere Blutung",
        "Тяжёлое кровотечение",
        "重度流血",
        "重度流血",
        "重い出血",
        "Hemorragia grave",
        "Sangramento grave");

    // The names of the lethal wound are the third stage's names.
    internal static readonly UniqueCandidateTranslations FatalName = new(
        "Lethal Bleeding",
        "치명적인 출혈",
        "Saignement mortel",
        "Tödliche Blutung",
        "Смертельное кровотечение",
        "致命流血",
        "致命流血",
        "致命的な出血",
        "Hemorragia mortal",
        "Sangramento letal");

    // Natural words without numbers; the exact amount that still drains stands
    // in the health bar's tooltip (StatusLine). Nothing about what helps (the player finds it out).
    internal static readonly UniqueCandidateTranslations LightDescription = new(
        "Bleeds lightly and does not heal on its own. Healing is slightly weaker.",
        "가볍게 출혈이 이어지며 저절로 낫지 않습니다. 회복 효과가 약간 줄어듭니다.",
        "Saigne légèrement et ne guérit pas seul. Les soins sont un peu moins efficaces.",
        "Blutet leicht nach und heilt nicht von selbst. Heilung wirkt etwas schwächer.",
        "Слегка кровоточит и не проходит само. Лечение немного слабее.",
        "轻微持续流血，不会自行愈合。治疗效果略微降低。",
        "輕微持續流血，不會自行癒合。治療效果略微降低。",
        "軽く出血が続き、自然には治らない。回復効果がやや下がる。",
        "Sangra levemente y no sana sola. La curación es algo más débil.",
        "Sangra levemente e não cura sozinho. A cura fica um pouco mais fraca.");

    internal static readonly UniqueCandidateTranslations DeepDescription = new(
        "Bleeds heavily and does not heal on its own. Healing is weaker.",
        "출혈이 심하게 이어지며 저절로 낫지 않습니다. 회복 효과가 줄어듭니다.",
        "Saigne abondamment et ne guérit pas seul. Les soins sont moins efficaces.",
        "Blutet stark nach und heilt nicht von selbst. Heilung wirkt schwächer.",
        "Сильно кровоточит и не проходит само. Лечение слабее.",
        "大量持续流血，不会自行愈合。治疗效果降低。",
        "大量持續流血，不會自行癒合。治療效果降低。",
        "激しく出血が続き、自然には治らない。回復効果が下がる。",
        "Sangra mucho y no sana sola. La curación es más débil.",
        "Sangra muito e não cura sozinho. A cura fica mais fraca.");

    internal static readonly UniqueCandidateTranslations HeavyDescription = new(
        "Bleeds severely and does not heal on its own. Healing is much weaker.",
        "심하게 출혈하며 저절로 낫지 않습니다. 회복 효과가 크게 줄어듭니다.",
        "Saigne gravement et ne guérit pas seul. Les soins sont bien moins efficaces.",
        "Blutet heftig und heilt nicht von selbst. Heilung wirkt deutlich schwächer.",
        "Обильно кровоточит и не проходит само. Лечение намного слабее.",
        "严重流血，不会自行愈合。治疗效果大幅降低。",
        "嚴重流血，不會自行癒合。治療效果大幅降低。",
        "ひどく出血し、自然には治らない。回復効果が大きく下がる。",
        "Sangra gravemente y no sana sola. La curación es mucho más débil.",
        "Sangra gravemente e não cura sozinho. A cura fica muito mais fraca.");

    // Draft, (the author approves DE/EN, the other languages follow them).
    internal static readonly UniqueCandidateTranslations FatalDescription = new(
        "The blood loss exceeds the remaining health. Without treatment the unit dies. Healing works at most by half, and the greater the blood loss, the weaker it gets.",
        "출혈량이 남은 생명력을 넘어섭니다. 치료하지 않으면 죽습니다. 회복 효과는 최대 절반만 적용되며, 출혈이 클수록 더 약해집니다.",
        "La perte de sang dépasse la santé restante. Sans soins, l'unité meurt. Les soins sont au plus efficaces à moitié, et d'autant plus faibles que la perte de sang est grande.",
        "Der Blutverlust übersteigt das restliche Leben. Ohne Behandlung stirbt die Figur. Heilung wirkt höchstens zur Hälfte, je größer der Blutverlust, desto schwächer.",
        "Кровопотеря превышает оставшееся здоровье. Без лечения персонаж умрёт. Лечение действует не больше чем наполовину и тем слабее, чем больше кровопотеря.",
        "失血量超过剩余生命值。若不治疗将会死亡。治疗效果最多减半，失血越多效果越弱。",
        "失血量超過剩餘生命值。若不治療將會死亡。治療效果最多減半，失血越多效果越弱。",
        "失血が残りの体力を上回っている。治療しなければ死ぬ。回復効果は最大でも半分で、失血が多いほど弱くなる。",
        "La pérdida de sangre supera la salud restante. Sin tratamiento, la unidad muere. La curación funciona como mucho a la mitad, y cuanto mayor es la pérdida de sangre, más débil es.",
        "A perda de sangue excede a vida restante. Sem tratamento, a unidade morre. A cura funciona no máximo pela metade, e quanto maior a perda de sangue, mais fraca fica.");

    // The line in the health bar's tooltip: {0} stage name, {1} buffer.
    internal static readonly UniqueCandidateTranslations StatusLine = new(
        "{0}: bleeds out {1} more health without treatment.",
        "{0}: 치료하지 않으면 생명력 {1}만큼 더 출혈합니다.",
        "{0} : sans soins, perd encore {1} points de santé en saignant.",
        "{0}: blutet ohne Behandlung noch {1} Leben aus.",
        "{0}: без лечения истечёт кровью ещё на {1} здоровья.",
        "{0}：若不治疗，还会流失{1}点生命值。",
        "{0}：若不治療，還會流失{1}點生命值。",
        "{0}：治療しないと、さらに体力{1}を失血する。",
        "{0}: sin tratamiento, se desangra {1} de salud más.",
        "{0}: sem tratamento, sangra mais {1} de vida.");
    // Apocalypse combat tooltip, monster section.
    internal static readonly UniqueCandidateTranslations TooltipWounds = new(
        "Bleeding",
        "출혈",
        "Saignements",
        "Blutungen",
        "Кровотечения",
        "流血",
        "流血",
        "出血",
        "Hemorragias",
        "Sangramentos");

    internal static readonly UniqueCandidateTranslations TooltipBleedOn = new(
        "Bleed on", "출혈 지속", "Saignent encore", "Bluten nach", "Кровоточат",
        "持续流血", "持續流血", "出血が続く", "Siguen sangrando", "Continuam sangrando");

    internal static UniqueCandidateTranslations Name(WoundRules.Stage stage) => stage switch
    {
        WoundRules.Stage.Deep => DeepName,
        WoundRules.Stage.Heavy => HeavyName,
        WoundRules.Stage.Fatal => FatalName,
        _ => LightName
    };

    internal static string Colour(WoundRules.Stage stage) => Colours.TryGetValue(stage, out var colour) ? colour : Colours[WoundRules.Stage.Light];

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (NameKey(LightKey), LightName), (DescKey(LightKey), LightDescription),
        (NameKey(DeepKey), DeepName), (DescKey(DeepKey), DeepDescription),
        (NameKey(HeavyKey), HeavyName), (DescKey(HeavyKey), HeavyDescription),
        (NameKey(FatalKey), FatalName), (DescKey(FatalKey), FatalDescription),
        (nameof(StatusLine), StatusLine), (nameof(TooltipWounds), TooltipWounds), (nameof(TooltipBleedOn), TooltipBleedOn)
    };
}
