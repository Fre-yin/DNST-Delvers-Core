using System.Globalization;

namespace DungeonSettlersDelvers.Core;

// The greatsword bandit's gimmick beside its greatsword skills: more bleeding on its
// hits. An active skill
// in Blade Grinding's place: Blade Grinding's effect ({0} % damage, {1} % accuracy of the next melee skill) plus the shown
// bleeding of every hit ({2} stacks, {3} % chance; "every hit", as before). Own key, the author's icon, a copy after way B.
// Whether the game already gives the blade extra bleeding is read by CORE_BANDIT_BLADE_PROBE (AFFECTER_
// ExcessiveBleedingInscription; the game's other ways, BloodshotInscription, AbyssRebound and five skills of their own, are not
// read). If not, Core adds it: a hit of the
// blade's one bleeding roll is ChanceKey for StacksKey (75 %, 3), instead of the bandits' common 40 % for 2.
// Rules without the game.
internal static class BanditSerratedRules
{
    internal const string SkillKey = "SKILL_DelversCoreSerratedBlade";
    internal const string IconFile = "SerratedBlade_32x32.png";
    internal const string BladeUnitKey = CoreUnitRules.Prefix + "ForestBanditBlade";
    internal const string InscriptionKey = "AFFECTER_ExcessiveBleedingInscription";
    internal const string ChanceKey = "bandits.blade.serrated.chance";
    internal const string StacksKey = "bandits.blade.serrated.stacks";
    internal const int MaxStacks = 10;
    // (stacks "3"): the blade's one bleeding roll replaces the bandits'
    // common one (40 %, 2 stacks); the settings override.
    internal const double DefaultChancePercent = 75.0;
    internal const int DefaultStacks = 3;

    // The blade's bleeding roll (chance 0..1, stacks) from the settings (chance in percent), the author's 75 % and 3 by default.
    internal static (double Chance, int Stacks)? Of(double? chancePercent, double? stacks)
    {
        var c = chancePercent is double cp && !double.IsNaN(cp) ? cp : DefaultChancePercent;
        var s = stacks is double sv && !double.IsNaN(sv) ? sv : DefaultStacks;
        return (Math.Clamp(c, 0.0, 100.0) / 100.0, (int)Math.Clamp(Math.Round(s), 1.0, MaxStacks));
    }

    // The values the tooltip fills in: {0} damage and {1} accuracy in percent (Blade Grinding's row), {2} stacks, {3} chance
    // in percent; 0 and 0 for the bleeding while unset.
    internal static float[] TooltipValues(float damage, float accuracy, (double Chance, int Stacks)? extra)
        => extra is { } e ? new[] { damage, accuracy, (float)e.Stacks, (float)Math.Round(e.Chance * 100.0) } : new[] { damage, accuracy, 0f, 0f };

    // Old blade kits in saves: Blade Grinding becomes Serrated Blade, Sweeping Cleave becomes Deadly Fear.
    internal static readonly (string From, string To)[] Migration =
    {
        ("SKILL_BladeGrinding", SkillKey),
        ("SKILL_SweepingCleave", BanditFearRules.SkillKey)
    };

    internal static string MigrateLine(string unit, string changes) => "CORE_BANDIT_BLADE_KIT_MIGRATED unit=" + unit + " changes=" + changes;

    // The game's Blade Grinding buff (AFFECTER_BladeGrinding.Passive_BladeGrinding) gives +damage and +hit chance
    // when an active, non-basic skill starts (OnDamage) and takes both back and ends when one ends (AfterDamage), but it
    // skips only the key "SKILL_BladeGrinding". Our copy has its own key, so without this its own end took back what was never
    // given (the blade fell to 0 damage after two casts). The copy is skipped like the original.
    internal static bool BuffSkips(string skillKey) => skillKey == SkillKey;

    internal const int BuffLogCap = 300;

    // Blades that cast Serrated Blade before the fix carry k times -{1} hit chance and -{0} damage dealt bonus (Blade Grinding's
    // row: {0}=50, {1}=30). k when both stats sit exactly that many steps below the unit's defaults, else 0: only this bug's
    // pattern is undone, any other change of the two stats is left alone.
    internal static int OldPenalty(float liveHit, float defaultHit, float liveDealt, float defaultDealt, float hitStep, float dealtStep)
    {
        if (!(hitStep > 0f) || !(dealtStep > 0f)) return 0;
        var hitSteps = (defaultHit - liveHit) / hitStep;
        var dealtSteps = (defaultDealt - liveDealt) / dealtStep;
        var k = (int)Math.Round(hitSteps);
        if (k < 1 || Math.Abs(hitSteps - k) > 0.01 || Math.Abs(dealtSteps - k) > 0.01) return 0;
        return k;
    }

    internal static string PenaltyLine(string unit, int steps, float hit, float dealt)
        => string.Format(CultureInfo.InvariantCulture, "CORE_BANDIT_SERRATED_PENALTY_RESET unit={0} steps={1} hit={2:0.#} dealt={3:0.#}", unit, steps, hit, dealt);

    internal static string BuffLine(string at, string owner, string skill, bool skipped)
        => "CORE_BANDIT_SERRATED_BUFF at=" + at + " owner=" + (owner ?? "-") + " skill=" + (skill ?? "-") + " skipped=" + (skipped ? 1 : 0);

    internal static string NameKey => "TEXTKEY_" + SkillKey + "_NAME";
    internal static string DescKey => "TEXTKEY_" + SkillKey + "_DESC";

    // The author's go, in the game's format ({n} from the row's values, [AFFECTER_x] as the yellow term). The name stays
    // English in every language.
    internal static readonly UniqueCandidateTranslations Name = new(
        "Serrated Blade", "Serrated Blade", "Serrated Blade", "Serrated Blade", "Serrated Blade",
        "Serrated Blade", "Serrated Blade", "Serrated Blade", "Serrated Blade", "Serrated Blade");

    internal static readonly UniqueCandidateTranslations Description = new(
        "Your next melee skill deals {0}% more damage and has {1}% more accuracy. Each hit has a {3}% chance to leave {2} [AFFECTER_Bleeding].",
        "다음 근접 스킬의 피해량이 {0}% 증가하고 적중률이 {1}% 증가합니다. 적중할 때마다 {3}% 확률로 {2}의 [AFFECTER_Bleeding]을 남깁니다.",
        "Votre prochaine compétence de mêlée inflige {0} % de dégâts en plus et gagne {1} % de précision. Chaque coup a {3} % de chances de laisser {2} [AFFECTER_Bleeding].",
        "Der nächste Nahkampfskill verursacht {0}% mehr Schaden und hat {1}% mehr Treffergenauigkeit. Jeder Treffer hinterlässt mit {3}% Chance {2} [AFFECTER_Bleeding].",
        "Следующий навык ближнего боя наносит на {0}% больше урона и получает на {1}% больше точности. Каждый удар с шансом {3}% оставляет {2} [AFFECTER_Bleeding].",
        "下一个近战技能的伤害提高{0}%，命中率提高{1}%。每次命中有{3}%的几率造成{2}层[AFFECTER_Bleeding]。",
        "下一個近戰技能的傷害提高{0}%，命中率提高{1}%。每次命中有{3}%的機率造成{2}層[AFFECTER_Bleeding]。",
        "次の近接スキルのダメージが{0}%、命中率が{1}%上昇する。命中するたびに{3}%の確率で[AFFECTER_Bleeding]を{2}付与する。",
        "Tu próxima habilidad cuerpo a cuerpo inflige un {0}% más de daño y tiene un {1}% más de precisión. Cada golpe tiene un {3}% de probabilidad de dejar {2} [AFFECTER_Bleeding].",
        "Sua próxima habilidade corpo a corpo causa {0}% a mais de dano e tem {1}% a mais de precisão. Cada golpe tem {3}% de chance de deixar {2} [AFFECTER_Bleeding].");

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[] { (NameKey, Name), (DescKey, Description) };

    internal static string ProbeLine(string what, string value) => "CORE_BANDIT_BLADE_PROBE " + what + "=" + (value ?? "-");
}
