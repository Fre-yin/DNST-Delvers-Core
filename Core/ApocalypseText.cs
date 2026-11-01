namespace DungeonSettlersDelvers.Core;

// Words of the Apocalypse difficulty in the game's ten languages. The
// campaign screen reads its texts by key, so the first three are registered in the game's
// text table (UniqueCandidateLocalization); the menu words are picked directly.
internal static class ApocalypseText
{
    internal const string TitleKey = "TEXTKEY_CreateCampaign_ApocalypseDifficulty";
    internal const string CombatDescriptionKey = "TEXTKEY_CreateCampaign_ApocalypseCombatDifficulty_DESC";
    internal const string ManagementDescriptionKey = "TEXTKEY_CreateCampaign_ApocalypseManagementDifficulty_DESC";

    // The game colours Hard, Very Hard and Devastation in their text (<color=#f16071>); Apocalypse
    // takes the hue of the revival sickness (ReviveSicknessBars.Purple, #74428C), lightened to the
    // brightness of the game's red so it reads on the dark grounds (#B581CF for
    // texts, the bars keep #74428C). The plain words stay for the Delvers menu.
    internal const string Colour = "#B581CF";

    internal static readonly UniqueCandidateTranslations Title = new(
        "Apocalypse", "아포칼립스", "Apocalypse", "Apokalypse", "Апокалипсис",
        "天启", "天啟", "アポカリプス", "Apocalipsis", "Apocalipse");

    internal static readonly UniqueCandidateTranslations TitleColoured = new(
        Tint("Apocalypse"), Tint("아포칼립스"), Tint("Apocalypse"), Tint("Apokalypse"), Tint("Апокалипсис"),
        Tint("天启"), Tint("天啟"), Tint("アポカリプス"), Tint("Apocalipsis"), Tint("Apocalipse"));

    // New debuffs and mechanics, enemies with special abilities, more stressful
    // fights, enemies attack at once. Highlights in the game's own description colour (#ccbeb8).
    internal static readonly UniqueCandidateTranslations CombatDescription = new(
        "Adds new debuffs and mechanics. Enemies gain special abilities and attack at once, making fights <color=#ccbeb8>far more stressful for your adventurers</color>.",
        "새로운 디버프와 메커니즘이 추가됩니다. 적은 특수 능력을 얻고 즉시 공격하여 <color=#ccbeb8>모험가들에게 전투가 훨씬 더 큰 부담</color>이 됩니다.",
        "Ajoute de nouveaux affaiblissements et mécaniques. Les ennemis gagnent des capacités spéciales et attaquent aussitôt, ce qui rend les combats <color=#ccbeb8>bien plus éprouvants pour vos aventuriers</color>.",
        "Bringt neue Debuffs und Mechaniken ins Spiel. Gegner erhalten Spezialfähigkeiten und greifen sofort an, Kämpfe werden <color=#ccbeb8>für deine Abenteurer deutlich stressiger</color>.",
        "Добавляет новые дебаффы и механики. Враги получают особые способности и нападают сразу, поэтому бои становятся <color=#ccbeb8>гораздо напряжённее для ваших искателей приключений</color>.",
        "加入新的减益效果与机制。敌人获得特殊能力并立即进攻，<color=#ccbeb8>战斗对冒险者来说压力倍增</color>。",
        "加入新的減益效果與機制。敵人獲得特殊能力並立即進攻，<color=#ccbeb8>戰鬥對冒險者來說壓力倍增</color>。",
        "新たなデバフとメカニクスが加わります。敵は特殊能力を得てすぐに攻撃してくるため、<color=#ccbeb8>冒険者にとって戦闘の負担が大きく増します</color>。",
        "Añade nuevos perjuicios y mecánicas. Los enemigos obtienen habilidades especiales y atacan de inmediato, así que los combates son <color=#ccbeb8>mucho más estresantes para tus aventureros</color>.",
        "Adiciona novas penalidades e mecânicas. Os inimigos ganham habilidades especiais e atacam imediatamente, tornando os combates <color=#ccbeb8>muito mais estressantes para seus aventureiros</color>.");

    // The world is ending, resources are scarce and the catastrophe has cut off
    // many supply routes. Two lines like the game's own management texts. Only what
    // happens, scarce resources and cut off trade routes with far fewer goods; no new debuffs or mechanics.
    internal static readonly UniqueCandidateTranslations ManagementDescription = new(
        "The world is ending. Resources are scarce, and the catastrophe has cut off many trade routes.\nMerchants offer <color=#ccbeb8>far fewer goods</color>.",
        "세상이 종말을 맞고 있습니다. 자원은 부족하고, 재앙으로 많은 교역로가 끊겼습니다.\n상인들은 <color=#ccbeb8>훨씬 적은 물건</color>을 내놓습니다.",
        "Le monde touche à sa fin. Les ressources sont rares et la catastrophe a coupé de nombreuses routes commerciales.\nLes marchands proposent <color=#ccbeb8>bien moins de marchandises</color>.",
        "Die Welt geht unter. Ressourcen sind knapp, die Katastrophe hat viele Handelswege abgeschnitten.\nHändler bieten <color=#ccbeb8>deutlich weniger Waren</color>.",
        "Мир гибнет. Ресурсов мало, а катастрофа отрезала многие торговые пути.\nТорговцы предлагают <color=#ccbeb8>гораздо меньше товаров</color>.",
        "世界正走向终结。资源匮乏，灾难切断了许多贸易路线。\n商人提供的<color=#ccbeb8>货物大幅减少</color>。",
        "世界正走向終結。資源匱乏，災難切斷了許多貿易路線。\n商人提供的<color=#ccbeb8>貨物大幅減少</color>。",
        "世界は終わりを迎えつつあります。資源は乏しく、災厄によって多くの交易路が断たれました。\n商人の<color=#ccbeb8>品物が大きく減ります</color>。",
        "El mundo se acaba. Los recursos escasean y la catástrofe ha cortado muchas rutas comerciales.\nLos comerciantes ofrecen <color=#ccbeb8>muchos menos bienes</color>.",
        "O mundo está chegando ao fim. Os recursos são escassos e a catástrofe cortou muitas rotas comerciais.\nOs mercadores oferecem <color=#ccbeb8>muito menos mercadorias</color>.");

    // Tooltip rows, added to the game's monster and ally sections (ApocalypseTooltip): what
    // Apocalypse changes today, as label and value. Short like the game's rows, so label and value
    // keep the native size and do not run into each other.
    internal static readonly UniqueCandidateTranslations TooltipEnemiesAttack = new(
        "Enemies attack", "적의 공격", "Attaque des ennemis", "Gegner greifen an", "Атака врагов",
        "敌人进攻", "敵人進攻", "敵の攻撃", "Ataque enemigo", "Ataque inimigo");

    internal static readonly UniqueCandidateTranslations TooltipAtOnce = new(
        "At once", "즉시", "Aussitôt", "Sofort", "Сразу", "立即", "立即", "即座", "De inmediato", "Imediato");

    internal static readonly UniqueCandidateTranslations TooltipAmbush = new(
        "Surprise bonus", "기습 보너스", "Bonus de surprise", "Überraschungsbonus", "Бонус внезапности",
        "偷袭加成", "偷襲加成", "奇襲ボーナス", "Bonus sorpresa", "Bônus surpresa");

    internal static readonly UniqueCandidateTranslations TooltipRemoved = new(
        "Removed", "없음", "Supprimé", "Entfällt", "Отменён", "取消", "取消", "なし", "Eliminada", "Removido");

    internal static readonly UniqueCandidateTranslations TooltipPauseKey = new(
        "Pause key", "일시정지 키", "Touche pause", "Pausentaste", "Клавиша паузы",
        "暂停键", "暫停鍵", "一時停止キー", "Tecla de pausa", "Tecla de pausa");

    internal static readonly UniqueCandidateTranslations TooltipSlowMotion = new(
        "Slow motion", "슬로모션", "Ralenti", "Zeitlupe", "Замедление",
        "慢动作", "慢動作", "スロー", "Cámara lenta", "Câmera lenta");

    // Management tooltip, resource section: merchants offer 40 % fewer goods (ApocalypseMerchants).
    internal static readonly UniqueCandidateTranslations TooltipMerchantGoods = new(
        "Merchant goods", "상인 물품", "Stock des marchands", "Händlerware", "Товары торговцев",
        "商人货物", "商人貨物", "商人の品物", "Bienes de comerciantes", "Estoque de mercadores");

    internal static readonly UniqueCandidateTranslations TooltipFewer = new(
        "40% fewer", "40% 감소", "40 % de moins", "40 % weniger", "На 40% меньше",
        "减少 40%", "減少 40%", "40%減少", "40 % menos", "40% a menos");

    // A word in the Apocalypse colour; also the boss bandits' item names (BossItemText).
    internal static string Tint(string word) => "<color=" + Colour + ">" + word + "</color>";

    internal static readonly UniqueCandidateTranslations MenuSection = new(
        "Difficulty", "난이도", "Difficulté", "Schwierigkeit", "Сложность",
        "难度", "難度", "難易度", "Dificultad", "Dificuldade");

    internal static readonly UniqueCandidateTranslations NoPause = new(
        "No pause in Apocalypse",
        "아포칼립스에서 일시정지 없음",
        "Pas de pause en Apocalypse",
        "Keine Pause bei Apokalypse",
        "Без паузы в Апокалипсисе",
        "天启难度下无法暂停",
        "天啟難度下無法暫停",
        "アポカリプスでは一時停止なし",
        "Sin pausa en Apocalipsis",
        "Sem pausa no Apocalipse");

    internal static readonly UniqueCandidateTranslations NoPauseHint = new(
        "The pause key slows time to half speed instead, for up to 5 seconds. It recharges while the game runs.",
        "대신 일시정지 키가 시간을 절반 속도로 늦춥니다. 최대 5초 동안 지속되며, 게임이 진행되는 동안 다시 충전됩니다.",
        "La touche pause ralentit plutôt le temps de moitié, jusqu'à 5 secondes. Elle se recharge pendant que le jeu tourne.",
        "Die Pausentaste verlangsamt die Zeit stattdessen auf halbe Geschwindigkeit, bis zu 5 Sekunden. Sie lädt sich auf, während das Spiel läuft.",
        "Вместо паузы клавиша замедляет время вдвое, до 5 секунд. Запас восстанавливается, пока идёт игра.",
        "暂停键改为将时间减慢至一半速度，最多 5 秒。游戏运行时会重新充能。",
        "暫停鍵改為將時間減慢至一半速度，最多 5 秒。遊戲運行時會重新充能。",
        "代わりに一時停止キーで時間が半分の速さになります。最大5秒で、ゲームの進行中に回復します。",
        "En su lugar, la tecla de pausa ralentiza el tiempo a la mitad, hasta 5 segundos. Se recarga mientras el juego avanza.",
        "Em vez disso, a tecla de pausa deixa o tempo na metade da velocidade, por até 5 segundos. Ela recarrega enquanto o jogo roda.");

    // The Delvers menu's section that lends single Apocalypse rules to campaigns without
    // Apocalypse (ApocalypseRules); read when a campaign is loaded or created.
    // His heading for the options of other difficulties.
    internal static readonly UniqueCandidateTranslations RulesSection = new(
        "Apocalypse options for other difficulties",
        "다른 난이도용 아포칼립스 옵션",
        "Options d'Apocalypse pour les autres difficultés",
        "Apocalypse Optionen für andere Schwierigkeitsgrade",
        "Опции Апокалипсиса для других сложностей",
        "其他难度的天启选项",
        "其他難度的天啟選項",
        "他の難易度向けのアポカリプスオプション",
        "Opciones de Apocalipsis para otras dificultades",
        "Opções do Apocalipse para outras dificuldades");

    internal static readonly UniqueCandidateTranslations RulesWounds = new(
        "Bleeding",
        "출혈",
        "Saignements",
        "Blutung",
        "Кровотечение",
        "流血",
        "流血",
        "出血",
        "Hemorragias",
        "Sangramentos");

    internal static readonly UniqueCandidateTranslations RulesNoPause = new(
        "No pause", "일시정지 없음", "Pas de pause", "Keine Pause", "Без паузы", "无法暂停", "無法暫停", "一時停止なし", "Sin pausa", "Sem pausa");

    internal static readonly UniqueCandidateTranslations RulesInstantCombat = new(
        "Enemies attack at once",
        "적이 즉시 공격",
        "Les ennemis attaquent aussitôt",
        "Gegner greifen sofort an",
        "Враги атакуют сразу",
        "敌人立即进攻",
        "敵人立即進攻",
        "敵が即座に攻撃",
        "Los enemigos atacan de inmediato",
        "Inimigos atacam imediatamente");

    internal static readonly UniqueCandidateTranslations RulesEnemyAbilities = new(
        "Enemy abilities",
        "적 특수 능력",
        "Capacités ennemies",
        "Gegnerfähigkeiten",
        "Способности врагов",
        "敌人能力",
        "敵人能力",
        "敵の特殊能力",
        "Habilidades enemigas",
        "Habilidades inimigas");

    internal static readonly UniqueCandidateTranslations RulesWeakBandits = new(
        "Smaller bandit groups",
        "소규모 도적단",
        "Petits groupes de bandits",
        "Kleinere Banditengruppen",
        "Небольшие группы бандитов",
        "小股强盗团",
        "小股強盜團",
        "小規模な盗賊団",
        "Grupos pequeños de bandidos",
        "Grupos menores de bandidos");

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (TitleKey, TitleColoured), (nameof(Title), Title), (CombatDescriptionKey, CombatDescription),
        (ManagementDescriptionKey, ManagementDescription),
        (nameof(TooltipEnemiesAttack), TooltipEnemiesAttack), (nameof(TooltipAtOnce), TooltipAtOnce),
        (nameof(TooltipAmbush), TooltipAmbush), (nameof(TooltipRemoved), TooltipRemoved),
        (nameof(TooltipPauseKey), TooltipPauseKey), (nameof(TooltipSlowMotion), TooltipSlowMotion),
        (nameof(TooltipMerchantGoods), TooltipMerchantGoods), (nameof(TooltipFewer), TooltipFewer),
        (nameof(MenuSection), MenuSection),
        (nameof(NoPause), NoPause), (nameof(NoPauseHint), NoPauseHint),
        (nameof(RulesSection), RulesSection),
        (nameof(RulesWounds), RulesWounds), (nameof(RulesNoPause), RulesNoPause),
        (nameof(RulesInstantCombat), RulesInstantCombat),
        (nameof(RulesEnemyAbilities), RulesEnemyAbilities), (nameof(RulesWeakBandits), RulesWeakBandits)
    };
}
