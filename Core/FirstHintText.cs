namespace DungeonSettlersDelvers.Core;

// The words of the first-time hints (FirstHints). Each hint is a page of the
// game's own guide window: its title and text rows (TEXTKEY_GAMEGUIDE_<hint>_NAME and _DESC) go into
// the game's text table (UniqueCandidateLocalization), its picture is borrowed from one of the game's
// own guide pages (ImageKey, FirstHintImagePatch). Titles reuse the rule names of the Apocalypse
// tooltip where there is one. PageBreak splits a text into pages of the guide window.
internal static class FirstHintText
{
    internal const string Wounds = "DelversCoreWounds";
    internal const string NoPause = "DelversCoreNoPause";
    internal const string InstantCombat = "DelversCoreInstantCombat";
    internal const string Revive = "DelversCoreRevive";
    internal const string EnemyAbilities = "DelversCoreEnemyAbilities";

    internal static readonly string[] Keys = { Wounds, NoPause, InstantCombat, Revive, EnemyAbilities };

    internal const string PageBreak = "\n##\n";

    internal static readonly UniqueCandidateTranslations WoundsDescription = new(
        "Bleeding no longer stops on its own. It keeps draining health, and the body does not heal on its own.\n##\nBandages stop bleeding. Merchants sell them, and you can craft them. The portal stops bleeding too.",
        "출혈이 저절로 멈추지 않습니다. 계속 생명력을 잃으며, 몸은 스스로 회복되지 않습니다.\n##\n붕대로 출혈을 멈출 수 있습니다. 상인이 판매하며 직접 제작할 수도 있습니다. 포털도 출혈을 멈춥니다.",
        "Les saignements ne s'arrêtent plus seuls. Ils continuent de vider la santé et le corps ne guérit pas seul.\n##\nLes bandages arrêtent les saignements. Les marchands en vendent et on peut les fabriquer. Le portail arrête aussi les saignements.",
        "Blutungen hören nicht mehr von selbst auf. Sie bluten nach, und der Körper heilt nicht von selbst.\n##\nVerbände stillen Blutungen. Händler führen sie, und man kann sie herstellen. Auch das Portal stillt Blutungen.",
        "Кровотечения больше не останавливаются сами. Они продолжают отнимать здоровье, а тело не восстанавливается само.\n##\nБинты останавливают кровотечения. Их продают торговцы, их можно изготовить. Портал тоже останавливает кровотечения.",
        "流血不会再自行停止。它会持续消耗生命，身体也不会自行恢复。\n##\n绷带可以止血。商人有售，也可以自行制作。传送门同样能止血。",
        "流血不會再自行停止。它會持續消耗生命，身體也不會自行恢復。\n##\n繃帶可以止血。商人有售，也可以自行製作。傳送門同樣能止血。",
        "出血はもう自然には止まりません。体力を失い続け、体は自然に回復しません。\n##\n包帯で出血を止められます。商人が扱っており、製作もできます。ポータルでも出血は止まります。",
        "Las hemorragias ya no se detienen solas. Siguen quitando salud y el cuerpo no se cura solo.\n##\nLas vendas detienen las hemorragias. Los comerciantes las venden y también puedes fabricarlas. El portal también las detiene.",
        "Sangramentos não param mais sozinhos. Eles continuam tirando vida e o corpo não se cura sozinho.\n##\nBandagens estancam sangramentos. Os mercadores as vendem, e elas também podem ser fabricadas. O portal também estanca sangramentos.");

    internal static readonly UniqueCandidateTranslations InstantCombatDescription = new(
        "Enemies give no time to prepare and attack at once. Unaware enemies no longer take extra damage.",
        "적은 준비할 시간을 주지 않고 즉시 공격합니다. 눈치채지 못한 적도 더 이상 추가 피해를 받지 않습니다.",
        "Les ennemis ne laissent aucun temps de préparation et attaquent aussitôt. Les ennemis inattentifs ne subissent plus de dégâts supplémentaires.",
        "Gegner lassen keine Vorbereitungszeit und greifen sofort an. Unbemerkte Gegner nehmen keinen Zusatzschaden mehr.",
        "Враги не дают времени на подготовку и атакуют сразу. Ничего не подозревающие враги больше не получают дополнительный урон.",
        "敌人不给准备时间，会立即进攻。未察觉的敌人不再受到额外伤害。",
        "敵人不給準備時間，會立即進攻。未察覺的敵人不再受到額外傷害。",
        "敵は準備の時間を与えず、即座に攻撃してきます。気づいていない敵も追加ダメージを受けなくなります。",
        "Los enemigos no dan tiempo a prepararse y atacan de inmediato. Los enemigos desprevenidos ya no reciben daño adicional.",
        "Os inimigos não dão tempo para preparação e atacam na hora. Inimigos desatentos não sofrem mais dano extra.");

    internal static readonly UniqueCandidateTranslations ReviveTitle = new(
        "Revival", "부활", "Résurrection", "Wiederbelebung", "Воскрешение",
        "复活", "復活", "蘇生", "Resurrección", "Ressurreição");

    internal static readonly UniqueCandidateTranslations ReviveDescription = new(
        "Fallen characters can be brought back with the Scroll of Revival. How often depends on the combat difficulty. Afterwards health and energy are capped for a while.",
        "쓰러진 동료는 부활의 두루마리로 되살릴 수 있습니다. 횟수는 전투 난이도에 따라 다릅니다. 이후 한동안 체력과 에너지가 제한됩니다.",
        "Les personnages tombés peuvent être ramenés avec le parchemin de résurrection. Le nombre de fois dépend de la difficulté de combat. Ensuite, la vie et l'énergie sont limitées pendant un moment.",
        "Gefallene lassen sich mit der Wiederbelebungsrolle zurückholen. Wie oft, hängt von der Kampfstufe ab. Danach sind Leben und Energie eine Zeit lang begrenzt.",
        "Павших можно вернуть свитком воскрешения. Сколько раз, зависит от сложности боя. После этого здоровье и энергия на время ограничены.",
        "倒下的角色可以用复活卷轴救回。次数取决于战斗难度。之后生命和精力会在一段时间内受限。",
        "倒下的角色可以用復活卷軸救回。次數取決於戰鬥難度。之後生命和精力會在一段時間內受限。",
        "倒れたキャラクターは蘇生の巻物で呼び戻せます。回数は戦闘難易度によります。その後しばらく体力と気力が制限されます。",
        "Los personajes caídos pueden volver con el pergamino de resurrección. Cuántas veces depende de la dificultad de combate. Después, la vida y la energía quedan limitadas un tiempo.",
        "Personagens caídos podem voltar com o pergaminho da ressurreição. Quantas vezes depende da dificuldade de combate. Depois, vida e energia ficam limitadas por um tempo.");

    internal static readonly UniqueCandidateTranslations EnemyAbilitiesDescription = new(
        "Some enemies silence characters or put them to sleep. Silenced characters only use their basic attack, sleeping ones wake when hurt.",
        "일부 적은 캐릭터를 침묵시키거나 잠들게 합니다. 침묵 상태에서는 기본 공격만 사용하며, 잠든 캐릭터는 피해를 입으면 깨어납니다.",
        "Certains ennemis réduisent les personnages au silence ou les endorment. Réduits au silence, ils n'utilisent que l'attaque de base ; endormis, ils se réveillent quand ils sont blessés.",
        "Manche Gegner lassen Figuren verstummen oder schläfern sie ein. Verstummte nutzen nur den Grundangriff, Schlafende wachen bei Schaden auf.",
        "Некоторые враги накладывают безмолвие или усыпляют персонажей. При безмолвии доступна только обычная атака, спящие просыпаются от урона.",
        "部分敌人会使角色沉默或陷入睡眠。沉默时只能普通攻击，睡眠中的角色受到伤害时会醒来。",
        "部分敵人會使角色沉默或陷入睡眠。沉默時只能普通攻擊，睡眠中的角色受到傷害時會醒來。",
        "一部の敵はキャラクターを沈黙させたり眠らせたりします。沈黙中は通常攻撃のみ、眠ったキャラクターはダメージで目覚めます。",
        "Algunos enemigos silencian a los personajes o los duermen. Silenciados solo usan el ataque básico; dormidos despiertan al recibir daño.",
        "Alguns inimigos silenciam os personagens ou os fazem dormir. Silenciados só usam o ataque básico; adormecidos acordam ao sofrer dano.");

    // The section of the switch in Core's menu page (hints apart from the Apocalypse options).
    internal static readonly UniqueCandidateTranslations MenuSection = new(
        "Hints",
        "안내",
        "Conseils",
        "Hinweise",
        "Подсказки",
        "提示",
        "提示",
        "ヒント",
        "Consejos",
        "Dicas");

    // The switch in Core's menu page.
    internal static readonly UniqueCandidateTranslations MenuToggle = new(
        "Hints", "안내", "Conseils", "Ersthinweise", "Подсказки",
        "首次提示", "首次提示", "初回ヒント", "Consejos", "Dicas");

    internal static readonly UniqueCandidateTranslations MenuHint = new(
        "Explains new rules the first time they appear. The game pauses while the window is open.",
        "새 규칙이 처음 나타날 때 설명합니다. 창이 열려 있는 동안 게임이 일시정지됩니다.",
        "Explique les nouvelles règles la première fois qu'elles apparaissent. Le jeu est en pause tant que la fenêtre est ouverte.",
        "Erklärt neue Regeln beim ersten Auftreten. Das Spiel pausiert, solange das Fenster offen ist.",
        "Объясняет новые правила при первом появлении. Пока окно открыто, игра на паузе.",
        "新规则首次出现时进行说明。窗口打开期间游戏暂停。",
        "新規則首次出現時進行說明。視窗開啟期間遊戲暫停。",
        "新しいルールが初めて出たときに説明します。ウィンドウ表示中はゲームが一時停止します。",
        "Explica las reglas nuevas la primera vez que aparecen. El juego se pausa mientras la ventana está abierta.",
        "Explica regras novas na primeira vez que aparecem. O jogo pausa enquanto a janela estiver aberta.");

    // Every text row of the hints with its key.
    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (NameKey(Wounds), WoundText.TooltipWounds),
        (DescKey(Wounds), WoundsDescription),
        (NameKey(NoPause), ApocalypseText.RulesNoPause),
        (DescKey(NoPause), ApocalypseText.NoPauseHint),
        (NameKey(InstantCombat), ApocalypseText.RulesInstantCombat),
        (DescKey(InstantCombat), InstantCombatDescription),
        (NameKey(Revive), ReviveTitle),
        (DescKey(Revive), ReviveDescription),
        (NameKey(EnemyAbilities), ApocalypseText.RulesEnemyAbilities),
        (DescKey(EnemyAbilities), EnemyAbilitiesDescription)
    };

    // Whether hints show: a session override of the Toolkit wins, else the menu switch. Without an
    // override (null) the menu decides again (FirstHints.ClearTest).
    internal static bool EffectiveEnabled(bool? testOverride, bool menu) => testOverride ?? menu;

    internal static string NameKey(string hint) => "TEXTKEY_GAMEGUIDE_" + hint + "_NAME";

    internal static string DescKey(string hint) => "TEXTKEY_GAMEGUIDE_" + hint + "_DESC";

    // The game's guide picture shown with a hint; null for every other key.
    internal static string ImageKey(string hint) => hint switch
    {
        Wounds => "Health",
        NoPause => "PauseInCombat",
        InstantCombat => "Attack",
        Revive => "DeadUnitTreatment",
        EnemyAbilities => "DeadlyStatus",
        _ => null
    };
}
