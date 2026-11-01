namespace DungeonSettlersDelvers.Core;

// Words of the bandages (name plus one short sentence, like the game's items).
// The items read name, description and flavour by key (UniqueCandidateLocalization).
internal static class BandageText
{
    internal const string LightItemKey = CoreSaveItems.Prefix + "LightBandage";
    internal const string HeavyItemKey = CoreSaveItems.Prefix + "HeavyBandage";

    internal static string NameKey(string item) => "TEXTKEY_" + item + "_NAME";
    internal static string DescKey(string item) => "TEXTKEY_" + item + "_DESC";
    internal static string FlavorKey(string item) => "TEXTKEY_" + item + "_FLAVOR";
    internal static string ResearchNameKey(string research) => "TEXTKEY_" + research + "_NAME";

    internal static readonly UniqueCandidateTranslations LightName = new(
        "Light Bandage", "가벼운 붕대", "Bandage léger", "Leichter Verband", "Лёгкая повязка",
        "轻型绷带", "輕型繃帶", "簡易包帯", "Venda ligera", "Bandagem leve");

    // Wording wounds are called bleeding for players.
    internal static readonly UniqueCandidateTranslations LightDescription = new(
        "Eases bleeding.",
        "출혈을 완화합니다.",
        "Soulage un saignement.",
        "Lindert eine Blutung.",
        "Ослабляет кровотечение.",
        "缓解流血。",
        "緩解流血。",
        "出血を和らげる。",
        "Alivia una hemorragia.",
        "Alivia um sangramento.");

    internal static readonly UniqueCandidateTranslations LightFlavor = new(
        "Clean strips of linen.", "깨끗한 아마포 조각.", "Des bandes de lin propres.", "Saubere Leinenstreifen.",
        "Чистые полоски льна.", "干净的亚麻布条。", "乾淨的亞麻布條。", "清潔な亜麻布の切れ端。",
        "Tiras limpias de lino.", "Tiras limpas de linho.");

    internal static readonly UniqueCandidateTranslations HeavyName = new(
        "Heavy Bandage", "두꺼운 붕대", "Bandage épais", "Schwerer Verband", "Плотная повязка",
        "厚绷带", "厚繃帶", "厚手の包帯", "Venda gruesa", "Bandagem grossa");

    internal static readonly UniqueCandidateTranslations HeavyDescription = new(
        "Greatly eases bleeding.",
        "출혈을 크게 완화합니다.",
        "Soulage fortement un saignement.",
        "Lindert eine Blutung stark.",
        "Сильно ослабляет кровотечение.",
        "大幅缓解流血。",
        "大幅緩解流血。",
        "出血を大きく和らげる。",
        "Alivia mucho una hemorragia.",
        "Alivia muito um sangramento.");

    internal static readonly UniqueCandidateTranslations HeavyFlavor = new(
        "Layered, tightly woven cloth.", "촘촘하게 짠 여러 겹의 천.", "Plusieurs couches de tissu à trame serrée.",
        "Mehrlagiger, dicht gewebter Stoff.", "Многослойная ткань плотного плетения.", "织得紧密的多层布料。",
        "織得緊密的多層布料。", "目の詰まった布を重ねたもの。", "Tela en capas de tejido apretado.",
        "Tecido em camadas de trama apertada.");

    // The two bandages of first aid (FirstAid), made at the workbench and the alchemy bench.
    internal const string HerbalItemKey = CoreSaveItems.Prefix + "HerbalBandage";
    internal const string SalveItemKey = CoreSaveItems.Prefix + "SalveBandage";

    internal static readonly UniqueCandidateTranslations HerbalName = new(
        "Herbal Bandage", "약초 붕대", "Bandage aux herbes", "Kräuterverband", "Травяная повязка",
        "草药绷带", "草藥繃帶", "薬草の包帯", "Venda de hierbas", "Bandagem de ervas");

    internal static readonly UniqueCandidateTranslations HerbalDescription = new(
        "Stops bleeding and reduces its stacks.",
        "출혈을 멈추고 출혈 중첩을 줄입니다.",
        "Arrête un saignement et réduit les cumuls de saignement.",
        "Stillt eine Blutung und verringert die Blutungsstapel.",
        "Останавливает кровотечение и уменьшает стаки кровотечения.",
        "止住流血并减少流血层数。",
        "止住流血並減少流血層數。",
        "出血を止め、出血の重複を減らす。",
        "Detiene una hemorragia y reduce las acumulaciones de sangrado.",
        "Estanca um sangramento e reduz os acúmulos de sangramento.");

    internal static readonly UniqueCandidateTranslations HerbalFlavor = new(
        "Laced with wild herbs, it stops blood where plain linen fails.",
        "야생 약초를 섞어, 아마포만으로는 못 막는 피를 멎게 한다.",
        "Imprégné d'herbes sauvages, il arrête le sang là où le lin seul échoue.",
        "Mit Wildkräutern versetzt, stillt er Blut, wo Leinen allein versagt.",
        "С дикими травами он останавливает кровь там, где одного льна мало.",
        "掺入野生草药，能止住单靠亚麻止不住的血。",
        "摻入野生草藥，能止住單靠亞麻止不住的血。",
        "野草を練り込み、亜麻布だけでは止まらぬ血を止める。",
        "Con hierbas silvestres, detiene la sangre donde el lino solo no basta.",
        "Com ervas silvestres, estanca o sangue onde o linho sozinho falha.");

    internal static readonly UniqueCandidateTranslations SalveName = new(
        "Salve Bandage", "연고 붕대", "Bandage à l'onguent", "Salbenverband", "Повязка с мазью",
        "药膏绷带", "藥膏繃帶", "軟膏の包帯", "Venda con ungüento", "Bandagem com unguento");

    internal static readonly UniqueCandidateTranslations SalveDescription = new(
        "Stops bleeding, reduces its stacks and heals slowly.",
        "출혈을 멈추고 출혈 중첩을 줄이며 천천히 회복시킵니다.",
        "Arrête un saignement, réduit les cumuls de saignement et soigne lentement.",
        "Stillt eine Blutung, verringert die Blutungsstapel und heilt langsam.",
        "Останавливает кровотечение, уменьшает стаки кровотечения и медленно лечит.",
        "止住流血，减少流血层数并缓慢治疗。",
        "止住流血，減少流血層數並緩慢治療。",
        "出血を止め、出血の重複を減らし、ゆっくり回復させる。",
        "Detiene una hemorragia, reduce las acumulaciones de sangrado y cura lentamente.",
        "Estanca um sangramento, reduz os acúmulos de sangramento e cura lentamente.");

    internal static readonly UniqueCandidateTranslations SalveFlavor = new(
        "Thickly spread with salve.", "연고를 두껍게 바른 붕대.", "Généreusement enduit d'onguent.", "Dick mit Salbe bestrichen.",
        "Густо смазана мазью.", "厚厚地涂满药膏。", "厚厚地塗滿藥膏。", "軟膏がたっぷり塗られている。",
        "Untada con abundante ungüento.", "Coberta com bastante unguento.");

    // The research that unlocks both (FirstAid).
    internal const string FirstAidResearchKey = CoreSaveResearch.Prefix + "FirstAid";

    internal static readonly UniqueCandidateTranslations FirstAidName = new(
        "First Aid", "응급 처치", "Premiers soins", "Erste Hilfe", "Первая помощь",
        "急救", "急救", "応急手当", "Primeros auxilios", "Primeiros socorros");

    // The inventory's option that puts an item on another character (UseOnOther).
    internal const string UseOnOtherKey = "TEXTKEY_DelversCore_UseOnOther";

    internal static readonly UniqueCandidateTranslations UseOnOtherName = new(
        "Use on another character", "다른 캐릭터에게 사용", "Utiliser sur un autre personnage", "Auf andere Figur anwenden",
        "Применить к другому персонажу", "对其他角色使用", "對其他角色使用", "他のキャラクターに使う", "Usar en otro personaje",
        "Usar em outro personagem");

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (UseOnOtherKey, UseOnOtherName),
        (NameKey(LightItemKey), LightName), (DescKey(LightItemKey), LightDescription), (FlavorKey(LightItemKey), LightFlavor),
        (NameKey(HeavyItemKey), HeavyName), (DescKey(HeavyItemKey), HeavyDescription), (FlavorKey(HeavyItemKey), HeavyFlavor),
        (NameKey(HerbalItemKey), HerbalName), (DescKey(HerbalItemKey), HerbalDescription), (FlavorKey(HerbalItemKey), HerbalFlavor),
        (NameKey(SalveItemKey), SalveName), (DescKey(SalveItemKey), SalveDescription), (FlavorKey(SalveItemKey), SalveFlavor),
        (ResearchNameKey(FirstAidResearchKey), FirstAidName)
    };
}
