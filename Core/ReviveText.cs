namespace DungeonSettlersDelvers.Core;

// Words of the revive (drafts, the author revises them). The effects read their
// name and description by key, so these rows go into the game's text table
// (UniqueCandidateLocalization).
internal static class ReviveText
{
    internal const string SicknessKey = CoreSaveEffects.Prefix + "RevivalSickness";
    internal const string PanicKey = CoreSaveEffects.Prefix + "DungeonPanic";
    internal const string SicknessNameKey = "TEXTKEY_" + SicknessKey + "_NAME";
    internal const string SicknessDescKey = "TEXTKEY_" + SicknessKey + "_DESC";
    internal const string PanicNameKey = "TEXTKEY_" + PanicKey + "_NAME";
    internal const string PanicDescKey = "TEXTKEY_" + PanicKey + "_DESC";

    internal static readonly UniqueCandidateTranslations SicknessName = new(
        "Revival Sickness", "부활 후유증", "Mal de la résurrection", "Wiederbelebungskrankheit", "Болезнь воскрешения",
        "复活虚弱", "復活虛弱", "蘇生の後遺症", "Mal de la resurrección", "Mal da ressurreição");

    internal static readonly UniqueCandidateTranslations SicknessDescription = new(
        "Health and energy are capped and recover as the sickness fades.",
        "생명력과 에너지가 제한되며, 후유증이 사라질수록 회복됩니다.",
        "La santé et l'énergie sont plafonnées et reviennent à mesure que le mal s'estompe.",
        "Lebenspunkte und Energie sind begrenzt und erholen sich, bis die Krankheit abklingt.",
        "Здоровье и энергия ограничены и восстанавливаются по мере того, как болезнь проходит.",
        "生命值和精力受到限制，会随着虚弱消退而恢复。",
        "生命值和精力受到限制，會隨著虛弱消退而恢復。",
        "体力とエネルギーに上限がかかり、後遺症が薄れるにつれて回復します。",
        "La salud y la energía están limitadas y se recuperan a medida que el mal remite.",
        "Vida e energia ficam limitadas e se recuperam conforme o mal passa.");

    internal static readonly UniqueCandidateTranslations PanicName = new(
        "Dungeon Panic", "던전 공황", "Panique du donjon", "Dungeonpanik", "Паника в подземелье",
        "地城恐慌", "地城恐慌", "ダンジョンパニック", "Pánico en la mazmorra", "Pânico na masmorra");

    internal static readonly UniqueCandidateTranslations PanicDescription = new(
        "Barely escaped death. Mood drops sharply inside the dungeon.",
        "가까스로 죽음에서 돌아왔습니다. 던전 안에서는 기분이 크게 떨어집니다.",
        "Vient à peine d'échapper à la mort. Le moral chute fortement dans le donjon.",
        "Gerade erst dem Tod entkommen. Im Dungeon sinkt die Laune stark.",
        "Едва вернулся с того света. В подземелье настроение резко падает.",
        "刚从死亡中被拉回来。在地城中心情会大幅下降。",
        "剛從死亡中被拉回來。在地城中心情會大幅下降。",
        "死の淵から戻ったばかりです。ダンジョンの中では気分が大きく下がります。",
        "Acaba de escapar de la muerte. El ánimo cae mucho dentro de la mazmorra.",
        "Acabou de escapar da morte. O humor cai muito dentro da masmorra.");

    internal const string ScrollKey = CoreSaveItems.Prefix + "ReviveScroll";
    internal const string ScrollNameKey = "TEXTKEY_" + ScrollKey + "_NAME";
    internal const string ScrollDescKey = "TEXTKEY_" + ScrollKey + "_DESC";
    internal const string ScrollFlavorKey = "TEXTKEY_" + ScrollKey + "_FLAVOR";

    internal static readonly UniqueCandidateTranslations ScrollName = new(
        "Scroll of Revival", "부활의 두루마리", "Parchemin de résurrection", "Wiederbelebungsrolle", "Свиток воскрешения",
        "复活卷轴", "復活卷軸", "蘇生の巻物", "Pergamino de resurrección", "Pergaminho da ressurreição");

    internal static readonly UniqueCandidateTranslations ScrollDescription = new(
        "Use to bring a fallen comrade back to life.",
        "사용하면 쓰러진 동료를 되살립니다.",
        "À utiliser pour ramener un camarade tombé à la vie.",
        "Benutzen, um einen gefallenen Kameraden wiederzubeleben.",
        "Используйте, чтобы вернуть павшего товарища к жизни.",
        "使用后可复活一名倒下的同伴。",
        "使用後可復活一名倒下的同伴。",
        "使用すると倒れた仲間を蘇生させます。",
        "Úsalo para devolver la vida a un compañero caído.",
        "Use para trazer um companheiro caído de volta à vida.");

    // The author's text, fitted to the game's tone: the soul is dragged back, the body mended just
    // enough, and every further use costs more.
    internal static readonly UniqueCandidateTranslations ScrollFlavor = new(
        "Drags the soul back into its body and mends the body just enough for life to hold. The soul suffers for it, and every further use weighs heavier.",
        "영혼을 육신으로 끌어당기고, 생명이 붙어 있을 만큼만 육신을 되돌려 놓는다. 이 과정은 영혼을 해치며, 거듭 사용할수록 대가는 무거워진다.",
        "Arrache l'âme pour la ramener dans le corps et le répare juste assez pour que la vie tienne. L'âme en souffre, et chaque nouvel usage pèse plus lourd.",
        "Zerrt die Seele zurück in den Körper und stellt ihn gerade so weit wieder her, dass Leben möglich ist. Das schadet der Seele, und jede weitere Anwendung wiegt schwerer.",
        "Затягивает душу обратно в тело и восстанавливает его ровно настолько, чтобы в нём удержалась жизнь. Это ранит душу, и каждое новое применение обходится дороже.",
        "将灵魂拽回躯体，并将躯体修复到刚好能维持生命的程度。这个过程会伤害灵魂，每多用一次，代价就更沉重。",
        "將靈魂拽回軀體，並將軀體修復到剛好能維持生命的程度。這個過程會傷害靈魂，每多用一次，代價就更沉重。",
        "魂を肉体へ引き戻し、命がつなぎとめられるだけ肉体を癒やす。この過程は魂を傷つけ、使うたびに代償は重くなる。",
        "Arrastra el alma de vuelta al cuerpo y lo repara lo justo para que la vida se sostenga. El alma lo paga, y cada nuevo uso pesa más.",
        "Arrasta a alma de volta ao corpo e o restaura apenas o suficiente para a vida se manter. A alma sofre com isso, e cada novo uso pesa mais.");

    internal static readonly UniqueCandidateTranslations MenuSection = new(
        "Revival", "부활", "Résurrection", "Wiederbelebung", "Воскрешение",
        "复活", "復活", "蘇生", "Resurrección", "Ressurreição");

    // The warning before burying a dressed party corpse (CorpseBurialWarning) and its switch.
    internal const string BurialNoticeKey = "TEXTKEY_DelversCore_BurialWarning";

    internal static readonly UniqueCandidateTranslations BurialNotice = new(
        "Burying ruins the equipment: everything this unit still wears is lost for good. Continue anyway?",
        "매장하면 장비가 망가집니다. 이 유닛이 착용한 모든 것이 영영 사라집니다. 그래도 계속하시겠습니까?",
        "L'inhumation ruine l'équipement : tout ce que cette unité porte encore sera perdu pour toujours. Continuer quand même ?",
        "Beim Begraben wird die Ausrüstung ruiniert: Alles, was diese Figur noch trägt, geht endgültig verloren. Trotzdem fortfahren?",
        "При погребении снаряжение портится: всё, что ещё надето на этого персонажа, будет утеряно навсегда. Всё равно продолжить?",
        "埋葬会毁掉装备：该单位身上的所有装备都将永久丢失。仍要继续吗？",
        "埋葬會毀掉裝備：該單位身上的所有裝備都將永久遺失。仍要繼續嗎？",
        "埋葬すると装備は失われます。このユニットが身に着けているものはすべて永久に失われます。それでも続けますか？",
        "Enterrar arruina el equipo: todo lo que esta unidad aún lleva puesto se perderá para siempre. ¿Continuar de todos modos?",
        "Enterrar arruína o equipamento: tudo o que esta unidade ainda veste será perdido para sempre. Continuar mesmo assim?");

    internal static readonly UniqueCandidateTranslations MenuBurialWarning = new(
        "Warn before burying a unit in its equipment", "장비를 입은 채 매장하기 전에 경고", "Avertir avant d'inhumer une unité équipée",
        "Warnung vor dem Begraben mit Ausrüstung", "Предупреждать перед погребением в снаряжении", "埋葬穿着装备的单位前提醒",
        "埋葬穿著裝備的單位前提醒", "装備したまま埋葬する前に警告", "Avisar antes de enterrar una unidad con su equipo",
        "Avisar antes de enterrar uma unidade com o equipamento");

    internal static readonly UniqueCandidateTranslations MenuScroll = new(
        "Scroll of Revival at merchants", "상인이 부활의 두루마리를 판매", "Parchemin de résurrection chez les marchands",
        "Wiederbelebungsrolle bei Händlern", "Свиток воскрешения у торговцев", "商人出售复活卷轴", "商人出售復活卷軸",
        "商人が蘇生の巻物を扱う", "Pergamino de resurrección en los comerciantes", "Pergaminho da ressurreição nos mercadores");

    internal static readonly UniqueCandidateTranslations MenuScrollHint = new(
        "Merchants offer a scroll that brings dead characters back. How often per character depends on the combat difficulty.",
        "상인이 죽은 캐릭터를 되살리는 두루마리를 판매합니다. 캐릭터당 횟수는 전투 난이도에 따라 다릅니다.",
        "Les marchands proposent un parchemin qui ramène les personnages morts. Le nombre de fois par personnage dépend de la difficulté de combat.",
        "Händler bieten eine Rolle an, die tote Figuren wiederbelebt. Wie oft je Figur, hängt von der Kampfstufe ab.",
        "Торговцы продают свиток, возвращающий погибших персонажей. Сколько раз на персонажа, зависит от сложности боя.",
        "商人会出售能复活阵亡角色的卷轴。每个角色可复活的次数取决于战斗难度。",
        "商人會出售能復活陣亡角色的卷軸。每個角色可復活的次數取決於戰鬥難度。",
        "商人が倒れたキャラクターを蘇生させる巻物を扱います。キャラクターごとの回数は戦闘難易度で決まります。",
        "Los comerciantes ofrecen un pergamino que devuelve la vida a los personajes caídos. Cuántas veces por personaje depende de la dificultad de combate.",
        "Os mercadores oferecem um pergaminho que traz personagens mortos de volta. Quantas vezes por personagem depende da dificuldade de combate.");

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (SicknessNameKey, SicknessName), (SicknessDescKey, SicknessDescription),
        (PanicNameKey, PanicName), (PanicDescKey, PanicDescription),
        (ScrollNameKey, ScrollName), (ScrollDescKey, ScrollDescription), (ScrollFlavorKey, ScrollFlavor),
        (nameof(MenuSection), MenuSection), (nameof(MenuScroll), MenuScroll), (nameof(MenuScrollHint), MenuScrollHint),
        (BurialNoticeKey, BurialNotice), (nameof(MenuBurialWarning), MenuBurialWarning)
    };
}
