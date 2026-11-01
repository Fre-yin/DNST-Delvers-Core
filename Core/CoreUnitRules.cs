namespace DungeonSettlersDelvers.Core;

// The forest bandits as plain data: Core's own enemy units, four roles on goblin rows of the game
// (blade and guard on the brawler, archer and healer on the worker), drawn as human party members
// with their kit. No game types here; CoreUnits writes the rows into the game's unit table and
// dresses the units, CoreUnitCombat steers them in combat.
internal static class CoreUnitRules
{
    internal sealed class Definition
    {
        internal string Key;
        internal string Role;
        internal string Template;
        internal string[] Skills;
        internal bool Lifebloom;
        internal (string Part, string Item)[] Kit;
        // The share of the template's physical attack the unit's row gets (PhysicalAttackOf).
        internal float DamageFactor = 1f;
        // The weak bandits (WeakBanditRules): their level (0 for the boss party), the share of the boss row's health and magic
        // attack, their own armour bonus instead of the settings, and how they heed a player's taunt (null: the settings).
        internal int Level;
        internal float HealthFactor = 1f;
        internal float MagicFactor = 1f;
        internal double? ArmorBonus;
        internal double? TauntFactor;
        internal double? TauntBreakDrop;
    }

    internal const string Prefix = "UNIT_DelversCore";

    // The game's FactionType.Enemy, SubFactionType.NPC and EntityAI.DefaultEnemy.
    internal const int FactionEnemy = 3;
    internal const int SubFactionNpc = 2;
    internal const int BehaviourDefaultEnemy = 2;
    internal const string Pattern = "OnlyBasicAttack";
    internal const string EliteTrait = "AFFECTER_EliteMonster";
    // Written for a bandit in the game's part of a save (CoreSaveUnits), so a save loads without Core.
    internal const string StandIn = "UNIT_GoblinBrawler";
    internal const string PlayerPrefabPrefix = "UNIT_PlayerUnit_";

    // Body parts as the game names them (BodyPartsType).
    internal const string Body = "Equipment_Body";
    internal const string Head = "Equipment_Head";
    internal const string Primary = "Equipment_Hand_Primary";
    internal const string Secondary = "Equipment_Hand_Secondary";
    internal const string Hood = "Equipment_Hood";
    internal const string CarryItem = "Hand_CarryItem";

    internal static readonly string[] EquipSlots = { Body, Primary, Secondary, Head, Hood, CarryItem };
    internal const string EmptySlot = "NULL";

    // The game's TargetType.GridTarget and TargetType.SelfTarget.
    internal const int TargetTypeGrid = 2;
    internal const int TargetTypeSelf = 3;

    // Effects that lay a unit down to be carried (CoreUnitCombat.SkipsAdditionalEffect).
    internal static readonly string[] UnconsciousEffects = { "AFFECTER_Deadly", "AFFECTER_SevereExhausted" };

    // The boss party's weapon, shield, bow and staff are its own violet blood wood parts (BossItemRules).
    internal static readonly Definition[] Units =
    {
        new Definition
        {
            Key = Prefix + "ForestBanditBlade",
            Role = "blade",
            Template = "UNIT_GoblinBrawler",
            // Serrated Blade in Blade Grinding's place (BanditSerratedRules), Deadly Fear in Sweeping Cleave's
            // (BanditFearRules); the passive entry is gone.
            Skills = new[] { "SKILL_BasicGreatswordAttack", "SKILL_Slay", BanditSerratedRules.SkillKey, BanditFearRules.SkillKey, "SKILL_DestructiveSplit" },
            Kit = new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, BossItemRules.GreatSwordKey) }
        },
        new Definition
        {
            Key = Prefix + "ForestBanditGuard",
            Role = "guard",
            Template = "UNIT_GoblinBrawler",
            // Guardian's Taunt is a slowing ring for bandits (BanditSlowRing); the chain pull is the bandits'
            // own copy with the author's icon (BanditChainRules).
            Skills = new[] { "SKILL_BasicClubAttack", "SKILL_Hammering", "SKILL_IronClad", "SKILL_WildFlurry", "SKILL_ShieldUp", "SKILL_StunCharge", BanditChainRules.SkillKey, "SKILL_GuardiansTaunt" },
            Kit = new[] { (Body, "ITEM_BoneArmor"), (Head, "ITEM_BoneHelmet"), (Primary, "ITEM_BoneClub"), (Secondary, BossItemRules.ShieldKey) },
            // (camp balance): the boss party's shieldbearer deals a fifth less damage; health, armour and the
            // three others stay.
            DamageFactor = GuardDamageFactor
        },
        new Definition
        {
            Key = Prefix + "ForestBanditArcher",
            Role = "archer",
            Template = "UNIT_GoblinWorker",
            Skills = new[] { "SKILL_BasicBowAttack", "SKILL_SnipingShot", "SKILL_ArrowRain", "SKILL_AnkleBreak", "SKILL_HeadAim" },
            Kit = new[] { (Body, "ITEM_LeatherArmor"), (Head, "ITEM_LeatherHelmet"), (Primary, BossItemRules.BowKey) }
        },
        new Definition
        {
            Key = Prefix + "ForestBanditHealer",
            Role = "healer",
            Template = "UNIT_GoblinWorker",
            // No healing touch: the healer keeps her group alive with Lifebloom
            // and by keeping her distance; the magic missile stays her only basic attack.
            // SKILL_Run: the game's flee skill for her kiting (GroupAiBandits.Kite).
            // No Fire Missile; she heals and uses her green staff. No flee skill
            // either: Core's own escape steps (EscapeRules).
            // The Strange Seed with its mucus sprayer and the passive Life Burst, the
            // game's own skills; Core filters the aura and sets the burst's heal for her (GroupAiHealerSeed).
            Skills = new[] { "SKILL_BasicMagicMissile", "SKILL_ClingingVines", GroupAiHealerSeedRules.SeedSkill, GroupAiHealerSeedRules.SprayerSkill,
                GroupAiHealerSeedRules.LifeBurstSkill },
            Lifebloom = true,
            Kit = new[] { (Body, "ITEM_LeatherArmor"), (Head, "ITEM_LeatherHelmet"), (Primary, BossItemRules.StaffKey) }
        }
    };

    // The looks a bandit can get: the game's human visual profiles, without the presets.
    // (17./): any people for the slayer, the shieldbearer and the archer, lizards and lycans included; the
    // herbalist only as one of the three female forms. Humans half of the time, the other forms share the rest evenly. Only
    // the look: a profile row holds looks only (gender, skin, hair, face, eyes, head, body), no traits, so no people effects.
    internal static readonly string[] HumanForms = { "HumanStandardMale", "HumanStandardFemale", "HumanFatUnisex" };
    internal static readonly string[] OtherForms = { "ElfStandardMale", "ElfStandardFemale", "ElfSlimUnisex", "LizardMan", "LycanMale", "LycanFemale" };
    internal static readonly string[] HealerHumanForms = { "HumanStandardFemale" };
    internal static readonly string[] HealerOtherForms = { "ElfStandardFemale", "LycanFemale" };
    internal const double HumanShare = 0.5;

    internal static string ProfilePrefix(string form) => "UNITVISUAL_" + form + "_";

    // Every form the bandits may take, for reading the profiles once.
    internal static readonly string[] ProfilePrefixes = Array.ConvertAll(Concat(HumanForms, OtherForms), ProfilePrefix);

    internal static string[] HumanFormsFor(Definition unit) => unit?.Role == "healer" ? HealerHumanForms : HumanForms;

    internal static string[] OtherFormsFor(Definition unit) => unit?.Role == "healer" ? HealerOtherForms : OtherForms;

    // The form for a unit: humans with HumanShare, else one of the other forms, each with the same chance; forms without
    // profiles drop out (counts by form). Fixed per unit (its guid), like the profile before. Null when none has profiles.
    internal static string PickForm(Guid unit, string[] humans, string[] others, Func<string, int> count)
    {
        var bytes = unit.ToByteArray();
        var humanAvail = Array.FindAll(humans, f => count(f) > 0);
        var otherAvail = Array.FindAll(others, f => count(f) > 0);
        if (humanAvail.Length == 0 && otherAvail.Length == 0) return null;
        var roll = Hash(bytes, 1) % 1000 / 1000.0;
        var group = otherAvail.Length == 0 || (humanAvail.Length > 0 && roll < HumanShare) ? humanAvail : otherAvail;
        return group[(int)(Hash(bytes, 2) % (uint)group.Length)];
    }

    // The profile within its form, fixed per unit.
    internal static int ProfileIndexIn(Guid unit, int count) => count <= 0 ? -1 : (int)(Hash(unit.ToByteArray(), 3) % (uint)count);

    private static uint Hash(byte[] bytes, int salt)
    {
        uint hash = 17u + (uint)salt * 7919u;
        foreach (var b in bytes) hash = hash * 31u + b;
        return hash;
    }

    private static T[] Concat<T>(T[] a, T[] b)
    {
        var all = new T[a.Length + b.Length];
        a.CopyTo(all, 0);
        b.CopyTo(all, a.Length);
        return all;
    }

    // Health per role from the settings (bandits.health.<role>); without a setting the template's.
    internal const string HealthKeyPrefix = "bandits.health.";
    internal const double MaxHealthSetting = 1000000.0;


    // The forest's boss party carries the game's boss marker, as the golem and the
    // chain knight do: the Capital Punishment inscription (execution at 20 %) skips them, the boss HUD replaces their bars over
    // the head. Every unit of Units is that party today; weaker parties later get only the elite trait.
    internal const string BossTrait = "AFFECTER_BossMonster";

    // The weak bandits carry neither: no boss bar, no elite treatment of their wounds (Wounds.IsEliteOrBoss).
    internal static string[] TraitsFor(Definition unit)
        => unit == null ? new[] { EliteTrait } : unit.Level > 0 ? Array.Empty<string>() : new[] { EliteTrait, BossTrait };

    // Only the boss party belongs in the boss bar; elite bandits beside it keep their bars over the head. Today
    // every unit of Units is the boss party (TraitsFor), so this is always true for them (checked); it takes
    // effect once elite bandits get definitions of their own. A second group stays out of the bar by its role (RoleShown).
    internal static bool IsBossParty(Definition unit) => Array.IndexOf(TraitsFor(unit), BossTrait) >= 0;

    // A string list of a unit row is never empty: the game's rows write "NULL" for none, and UnitSpawner.SetNonPlayableAffecter reads
    // DefaultTraits[0] without a length check (an empty one threw in the room's spawn and the floor change was aborted).
    internal const string NoRowValue = "NULL";
    internal static string[] RowValues(string[] values) => values == null || values.Length == 0 ? new[] { NoRowValue } : values;
    internal static readonly UniqueCandidateTranslations BladeName = new(
        "Slayer", "도살자", "Boucher", "Schlächter", "Мясник",
        "屠夫", "屠夫", "屠殺者", "Carnicero", "Carniceiro");

    internal static readonly UniqueCandidateTranslations GuardName = new(
        "Shieldbearer", "방패병", "Porteur de bouclier", "Schildträger", "Щитоносец",
        "盾卫", "盾衛", "盾持ち", "Escudero", "Escudeiro");

    internal static readonly UniqueCandidateTranslations ArcherName = new(
        "Archer", "궁수", "Archer", "Schütze", "Стрелок",
        "弓手", "弓手", "弓兵", "Arquero", "Arqueiro");

    internal static readonly UniqueCandidateTranslations HealerName = new(
        "Herbalist", "약초사", "Herboriste", "Kräuterkundige", "Травница",
        "草药师", "草藥師", "薬草師", "Herbolaria", "Herbalista");

    internal static bool AimsAtCell(int targetType) => targetType == TargetTypeGrid;

    internal static bool AimsAtSelf(int targetType) => targetType == TargetTypeSelf;

    // A skill on a cell falls back to a basic attack at the target (CoreUnitCombat.Decide, the log line's fallback=): the
    // reason, or null when the skill is aimed at its cell. Checked in this order: no cell, a forced (provoked) target, the
    // cell beyond the skill's range.
    internal const string FallbackNoCell = "basic";
    internal const string FallbackForced = "forced";
    internal const string FallbackRange = "range";

    internal static string AimFallback(bool hasCell, bool forced, bool inRange)
        => !hasCell ? FallbackNoCell : forced ? FallbackForced : !inRange ? FallbackRange : null;

    internal static bool IsUnconsciousEffect(string key)
    {
        foreach (var effect in UnconsciousEffects)
            if (effect == key) return true;
        return false;
    }

    internal static bool IsRotationProfile(string profileKey)
    {
        if (profileKey == null || profileKey.EndsWith("_Preset", StringComparison.Ordinal)) return false;
        foreach (var prefix in ProfilePrefixes)
            if (profileKey.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    // UNITVISUAL_HumanStandardMale_3 -> HumanStandardMale, the suffix of the player prefab.
    internal static string VisualTypeOf(string profileKey)
    {
        const string visual = "UNITVISUAL_";
        if (profileKey == null || !profileKey.StartsWith(visual, StringComparison.Ordinal)) return null;
        var end = profileKey.LastIndexOf('_');
        if (end <= visual.Length) return null;
        return profileKey.Substring(visual.Length, end - visual.Length);
    }

    internal static string[] SkillsFor(Definition unit, bool lifebloom)
    {
        if (!lifebloom || !unit.Lifebloom) return unit.Skills;
        var skills = new string[unit.Skills.Length + 1];
        Array.Copy(unit.Skills, skills, unit.Skills.Length);
        skills[unit.Skills.Length] = LifebloomRules.SkillKey;
        return skills;
    }

    internal static string HealthKey(Definition unit) => HealthKeyPrefix + unit.Role;

    // The healer's magic attack from the
    // settings (the row value, 100 by default, see below); her seed's heal and her sprayer's hits scale with it in the game
    // (all bandits had 6.4, Frieren 115.5). Every other role keeps the template's. Her Lifebloom is a share of the
    // target and stays as it is.
    // The setting is the healer row's value. The monster affecters' MagicAttackPowerPerModifier:
    // Easy -85, Normal -60, VeryHard +25, Downfall +50 (CORE_UNIT_MAGIC_PROBE). "40 on Normal, scaled" is a row of 100:
    // Easy 15, Normal 40, VeryHard 125, Downfall 150.
    internal const string HealerMagicKey = "bandits.healer.matk";
    internal const float HealerMagicDefault = 100f;
    internal const float NormalMagicPerModifier = -60f;

    // The game's magic attack is the row's value times (1 + MagicAttackPowerPerModifier / 100) (Status_MagicAttackPower: a
    // multiplier of stat 83 x 0.01); monsters get stat 83 from the difficulty's monster affecter,
    // except on Hard, which has none (UnitSpawner.SetEnemyAffecter).
    internal static float EffectiveOf(float row, float perModifier) => row * Math.Max(0f, 1f + perModifier / 100f);

    // The weak mages (WeakBanditRules) cast from the same row value as the herbalist, then take their share (MagicFactor).
    internal static float MagicAttackOf(Definition unit, float template, double? setting)
    {
        if (unit?.Role is not ("healer" or "watermage" or "firemage")) return template;
        return setting is double value && value >= 0.0 && !double.IsNaN(value) ? (float)Math.Min(value, MaxHealthSetting) : HealerMagicDefault;
    }

    internal const float GuardDamageFactor = 0.8f;

    // The row's physical attack, from which the game's hits and skills reckon their damage: the template's times the unit's share.
    internal static float PhysicalAttackOf(Definition unit, float template) => template * (unit?.DamageFactor ?? 1f);

    internal static float HealthOf(float template, double? setting)
    {
        if (setting is double value && value >= 1.0) return (float)Math.Min(value, MaxHealthSetting);
        return template;
    }

    // What a dead bandit leaves on the ground: its kit.
    // partGone: a boss corpse whose boss part is gone (BossItemRules.ChosenIndex picked another boss); that part never drops.
    internal static List<string> DropItems(Definition unit, bool partGone = false)
    {
        var items = new List<string>();
        foreach (var entry in unit.Kit)
        {
            var item = entry.Item;
            if (string.IsNullOrEmpty(item) || item == EmptySlot || (partGone && BossItemRules.IsBossItem(item))) continue;
            items.Add(item);
        }
        return items;
    }

    // Every slot the game draws, with the kit's item or the empty slot.
    // Step 2: an undressed bandit corpse is drawn with every slot empty.
    // With the boss part gone its slot is drawn empty too.
    internal static List<(string Part, string Item)> EquipInfo(Definition unit, bool undressed, bool partGone = false)
    {
        var info = EquipInfo(unit);
        for (var i = 0; i < info.Count; i++)
            if (undressed || (partGone && BossItemRules.IsBossItem(info[i].Item))) info[i] = (info[i].Part, EmptySlot);
        return info;
    }

    // How many boss parts the unit's kit holds.
    internal static int SpecialCount(Definition unit)
    {
        var count = 0;
        foreach (var (_, item) in unit.Kit)
            if (BossItemRules.IsBossItem(item)) count++;
        return count;
    }

    internal static List<(string Part, string Item)> EquipInfo(Definition unit)
    {
        var info = new List<(string Part, string Item)>();
        foreach (var slot in EquipSlots)
        {
            var item = EmptySlot;
            foreach (var (part, kitItem) in unit.Kit)
                if (part == slot) item = kitItem;
            info.Add((slot, item));
        }
        return info;
    }

    internal static bool IsCoreUnit(string key) => key?.StartsWith(Prefix, StringComparison.Ordinal) ?? false;

    // Every Core unit: the boss party, then the weak bandits of every level (WeakBanditRules). Rows, saves and lookups go over
    // this list; what belongs to the boss party alone (boss items, the boss bar, the chain) keeps reading Units.
    internal static readonly Definition[] All = Concat(Units, WeakBanditRules.Definitions);

    internal static bool TryGet(string key, out Definition definition)
    {
        definition = null;
        if (!IsCoreUnit(key)) return false;
        foreach (var unit in All)
        {
            if (unit.Key == key)
            {
                definition = unit;
                return true;
            }
        }
        return false;
    }

    internal static string NameKey(string unitKey) => "TEXTKEY_" + unitKey + "_NAME";
    internal static string DescKey(string unitKey) => "TEXTKEY_" + unitKey + "_DESC";

    // (variant B, the slayer with the bleeding; "A herbalist"; the slayer's text speaks of the weapon,
    // so the look may take any gender): the journal texts. German and English are
    // ; the other eight languages follow them and are read before the release. Neutral for the three roles of any people
    // and gender; the herbalist is always a woman.
    internal static readonly UniqueCandidateTranslations BladeDescription = new(
        "The heavy greatsword causes bleeding that will not stop.\nThose who do not bandage up will bleed out.",
        "무거운 대검이 멈추지 않는 출혈을 일으킵니다.\n붕대를 감지 않는 자는 피를 흘리다 쓰러집니다.",
        "La lourde épée à deux mains provoque des saignements qui ne cessent pas.\nQui ne se bande pas finit par se vider de son sang.",
        "Das schwere Großschwert schlägt Blutungen, die nicht aufhören.\nWer sich nicht verbindet, verblutet.",
        "Тяжёлый двуручный меч вызывает кровотечения, которые не прекращаются.\nКто не перевяжется, истечёт кровью.",
        "沉重的巨剑造成止不住的流血。\n不及时包扎的人终将失血而亡。",
        "沉重的巨劍造成止不住的流血。\n不及時包紮的人終將失血而亡。",
        "重い大剣が、止まらない出血を引き起こす。\n包帯を巻かない者は失血して倒れる。",
        "El pesado mandoble causa hemorragias que no se detienen.\nQuien no se venda acabará desangrándose.",
        "A pesada espada grande causa sangramentos que não param.\nQuem não fizer curativos acabará sangrando até a morte.");

    internal static readonly UniqueCandidateTranslations GuardDescription = new(
        "Tough and entrenched behind a bone shield.\nWhoever comes too close to the band's healer will feel the chain.",
        "질기고, 뼈 방패 뒤에 몸을 숨깁니다.\n무리의 치유사에게 너무 가까이 다가가는 자는 사슬 맛을 보게 됩니다.",
        "Coriace et retranché derrière un bouclier d'os.\nQuiconque s'approche trop de la guérisseuse de la bande goûtera à la chaîne.",
        "Zäh und hinter einem Knochenschild verschanzt.\nWer der Heilerin der Bande zu nahe kommt, bekommt die Kette zu spüren.",
        "Стойкий боец, укрывшийся за костяным щитом.\nКто подойдёт слишком близко к целительнице банды, познакомится с цепью.",
        "坚韧，躲在骨盾之后。\n靠近团伙治疗者的人会尝到锁链的滋味。",
        "堅韌，躲在骨盾之後。\n靠近團夥治療者的人會嘗到鎖鏈的滋味。",
        "しぶとく、骨の盾の陰に身を固める。\n一味の癒し手に近づきすぎた者は鎖を味わう。",
        "Duro y atrincherado tras un escudo de hueso.\nQuien se acerque demasiado a la sanadora de la banda sentirá la cadena.",
        "Resistente e entrincheirado atrás de um escudo de osso.\nQuem chegar perto demais da curandeira do bando vai sentir a corrente.");

    internal static readonly UniqueCandidateTranslations ArcherDescription = new(
        "Keeps behind the others and shoots from a safe distance.\nThose who stand close together make an easy target for the rain of arrows.",
        "동료들 뒤에 머물며 안전한 거리에서 활을 쏩니다.\n가까이 모여 선 자들은 화살비의 손쉬운 표적이 됩니다.",
        "Reste derrière ses compagnons et tire à distance sûre.\nCeux qui se serrent les uns contre les autres sont une cible facile pour la pluie de flèches.",
        "Hält sich hinter den Kumpanen und schießt aus sicherer Entfernung.\nWer dicht beieinandersteht, bietet dem Pfeilregen ein leichtes Ziel.",
        "Держится за спинами товарищей и стреляет с безопасного расстояния.\nКто стоит плотно, становится лёгкой целью для града стрел.",
        "躲在同伙身后，从安全距离射击。\n挤在一起的人会成为箭雨的活靶子。",
        "躲在同夥身後，從安全距離射擊。\n擠在一起的人會成為箭雨的活靶子。",
        "仲間の後ろに控え、安全な距離から射る。\n固まって立つ者は矢の雨の格好の的になる。",
        "Se queda detrás de los demás y dispara desde una distancia segura.\nQuienes se agrupan son un blanco fácil para la lluvia de flechas.",
        "Fica atrás dos outros e atira de uma distância segura.\nQuem fica muito junto vira alvo fácil para a chuva de flechas.");

    internal static readonly UniqueCandidateTranslations HealerDescription = new(
        "A herbalist who keeps her band alive with healing herbs.\nShe avoids close combat, and her companions protect her.",
        "약초로 무리를 살려 두는 약초사입니다.\n근접전을 피하며, 동료들이 그녀를 지킵니다.",
        "Une herboriste qui maintient sa bande en vie grâce aux herbes médicinales.\nElle évite le corps à corps, et ses compagnons la protègent.",
        "Eine Kräuterkundige, die ihre Bande mit Heilkräutern am Leben hält.\nSie meidet den Nahkampf, und ihre Gefährten schützen sie.",
        "Травница, которая поддерживает жизнь банды целебными травами.\nОна избегает ближнего боя, а товарищи защищают её.",
        "一位用草药让团伙活下去的草药师。\n她避开近战，同伴们会保护她。",
        "一位用草藥讓團夥活下去的草藥師。\n她避開近戰，同伴們會保護她。",
        "薬草で一味を生かし続ける薬草師。\n接近戦を避け、仲間たちが彼女を守る。",
        "Una herbolaria que mantiene con vida a su banda con hierbas curativas.\nEvita el cuerpo a cuerpo, y sus compañeros la protegen.",
        "Uma herbalista que mantém o bando vivo com ervas curativas.\nEla evita o combate corpo a corpo, e seus companheiros a protegem.");}
