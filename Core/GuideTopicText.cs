namespace DungeonSettlersDelvers.Core;

// The "Delvers" topics at the bottom of the game's guide (the rules' descriptions where players look them
// up, not only once as a first hint). The guide builds one button per GameGuideType and reads a topic's title and text as
// TEXTKEY_GAMEGUIDE_<type>_NAME and _DESC and its picture as LoadGameGuideImage(<type>), with the type written as text
// (SubUI_TopicExplainPanel.SetSelectedTopic, SetGuideMedia). Core's topics are numbers far past the game's own types, so
// their keys are TEXTKEY_GAMEGUIDE_9000_NAME and so on; GuideTopics adds their buttons. Pure, so the offline tests cover it.
// The guide's text drops "##", so pages are plain paragraphs.
internal static class GuideTopicText
{
    internal const int First = 9000;
    internal const string Paragraph = "\n\n";

    internal static string NameKey(int id) => "TEXTKEY_GAMEGUIDE_" + id + "_NAME";

    internal static string DescKey(int id) => "TEXTKEY_GAMEGUIDE_" + id + "_DESC";

    // The game's guide picture for one of Core's topics (the key is the topic's number as text), else null.
    internal static string ImageKey(string key)
    {
        var index = IndexOf(key);
        return index < 0 ? null : Topics[index].Image;
    }

    // A topic's own picture in the package folder (Toolkit's screenshots): Guide_<number>.png at the size of the
    // guide's picture (Img_GuideImage, 796 x 284 at 1920 x 1080). Without the file the topic keeps the game's picture (ImageKey).
    internal const int ImageWidth = 796;
    internal const int ImageHeight = 284;

    internal static string ImageFile(int id) => "Guide_" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png";

    // The number of one of Core's topics written as text (a picture key, or the guide's selection), else null.
    internal static int? IdOf(string key)
    {
        var index = IndexOf(key);
        return index < 0 ? null : Topics[index].Id;
    }

    // Whether a guide type is one of Core's topics.
    internal static bool IsTopic(int id) => Array.FindIndex(Topics, topic => topic.Id == id) >= 0;

    private static int IndexOf(string key)
    {
        for (var i = 0; i < Topics.Length; i++)
            if (key == Topics[i].Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) return i;
        return -1;
    }

    // Every text row with its key, for the game's text table.
    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All
    {
        get
        {
            foreach (var topic in Topics)
            {
                yield return (NameKey(topic.Id), topic.Title);
                yield return (DescKey(topic.Id), topic.Text);
            }
        }
    }

    private static readonly UniqueCandidateTranslations DelversTitle = new(
        "Delvers and Apocalypse", "Delvers와 Apocalypse", "Delvers et Apocalypse", "Delvers und Apocalypse", "Delvers и Apocalypse",
        "Delvers 与 Apocalypse", "Delvers 與 Apocalypse", "Delvers と Apocalypse", "Delvers y Apocalypse", "Delvers e Apocalypse");

    // What the switches of the Delvers menu do (no explaining lines under the switches, the guide says it):
    // Apocalypse and its pause under Difficulty, the single rules for other difficulties, the two comfort options.
    private static readonly UniqueCandidateTranslations DelversText = new(
        "Delvers adds new rules to Dungeon Settlers. Most of them apply in combat Apocalypse, the difficulty above Downfall. Some can also be switched on for other campaigns in the Delvers menu." + Paragraph
            + "In the Delvers menu, the Apocalypse switch under Difficulty offers Apocalypse above the highest level when you create a campaign. Existing campaigns keep their level." + Paragraph
            + "The options for other difficulties switch single rules on for campaigns without Apocalypse: bleeding, no pause, enemies attack at once, enemy abilities and the Forest Bandits. They take effect on the next load." + Paragraph
            + "Without pause, the pause key slows time to half speed instead, for up to 5 seconds. It recharges while the game runs. Enemies who attack at once give no surprise bonus. Enemy abilities are bleeding hits, silence and sleep. The Forest Bandits bring their camp and their boss party with its loot. Smaller bandit groups come only together with the Forest Bandits." + Paragraph
            + "Under Quality of life, the shield band shows a unit's shield as a honeycomb band over the health bars. With the tactical formation off, the formation button is hidden and formations and priority places rest. They stay saved." + Paragraph
            + "The topics below explain each rule.",
        "Delvers는 Dungeon Settlers에 새 규칙을 더합니다. 대부분은 Downfall보다 높은 난이도인 전투 Apocalypse에서 적용됩니다. 일부는 Delvers 메뉴에서 다른 캠페인에도 켤 수 있습니다." + Paragraph
            + "Delvers 메뉴의 난이도 아래에 있는 Apocalypse 스위치를 켜면 캠페인을 만들 때 최고 난이도 위에 Apocalypse가 추가됩니다. 기존 캠페인은 난이도가 그대로 유지됩니다." + Paragraph
            + "다른 난이도용 옵션으로 Apocalypse가 아닌 캠페인에도 규칙을 하나씩 켤 수 있습니다: 출혈, 일시정지 없음, 적이 즉시 공격, 적 특수 능력, 숲 도적단. 다음 불러오기부터 적용됩니다." + Paragraph
            + "일시정지가 없으면 대신 일시정지 키가 시간을 절반 속도로 늦춥니다. 최대 5초 동안 지속되며, 게임이 진행되는 동안 다시 충전됩니다. 적이 즉시 공격하면 기습 보너스가 없습니다. 적 특수 능력은 출혈 공격, 침묵, 수면입니다. 숲 도적단은 야영지와 보스 일행, 그리고 그 전리품을 가져옵니다. 소규모 도적단은 숲 도적단과 함께일 때만 나타납니다." + Paragraph
            + "편의 기능에서 보호막 띠는 체력 막대 위의 벌집 띠로 유닛의 보호막을 보여 줍니다. 전술 대형을 끄면 대형 버튼이 사라지고 대형과 우선 순위 칸이 쉬게 됩니다. 저장된 내용은 그대로 남습니다." + Paragraph
            + "아래 항목에서 규칙을 하나씩 설명합니다.",
        "Delvers ajoute de nouvelles règles à Dungeon Settlers. La plupart s'appliquent en Apocalypse de combat, la difficulté qui suit Downfall. Certaines peuvent aussi être activées pour d'autres campagnes dans le menu Delvers." + Paragraph
            + "Dans le menu Delvers, l'interrupteur Apocalypse sous Difficulté propose Apocalypse comme niveau supérieur au plus élevé lors de la création d'une campagne. Les campagnes existantes gardent leur niveau." + Paragraph
            + "Les options pour les autres difficultés activent des règles une par une pour les campagnes sans Apocalypse : saignements, pas de pause, les ennemis attaquent aussitôt, capacités ennemies et les Bandits des bois. Elles prennent effet au prochain chargement." + Paragraph
            + "Sans pause, la touche pause ralentit plutôt le temps de moitié, jusqu'à 5 secondes. Elle se recharge pendant que le jeu tourne. Les ennemis qui attaquent aussitôt ne donnent pas de bonus de surprise. Les capacités ennemies sont des coups qui font saigner, le silence et le sommeil. Les Bandits des bois apportent leur camp et leur groupe de boss avec son butin. Les petits groupes de bandits ne viennent qu'avec les Bandits des bois." + Paragraph
            + "Sous Confort, la bande de bouclier montre le bouclier d'une unité par une bande en nid d'abeilles au dessus des barres de vie. Formation tactique désactivée, le bouton de formation disparaît et les formations et les emplacements prioritaires restent inactifs. Ils restent enregistrés." + Paragraph
            + "Les sujets suivants expliquent chaque règle.",
        "Delvers erweitert Dungeon Settlers um neue Regeln. Die meisten gelten auf der Kampfstufe Apocalypse, eine Stufe über Downfall. Einige lassen sich im Menü Delvers auch für andere Kampagnen einschalten." + Paragraph
            + "Im Menü Delvers bietet der Schalter Apocalypse unter Schwierigkeit beim Anlegen einer Kampagne Apocalypse über der höchsten Stufe an. Bestehende Kampagnen behalten ihre Stufe." + Paragraph
            + "Die Optionen für andere Schwierigkeitsgrade schalten einzelne Regeln für Kampagnen ohne Apocalypse ein: Blutung, keine Pause, sofort angreifende Gegner, Gegnerfähigkeiten und die Waldbanditen. Sie wirken ab dem nächsten Laden." + Paragraph
            + "Ohne Pause verlangsamt die Pausentaste die Zeit stattdessen auf halbe Geschwindigkeit, bis zu 5 Sekunden. Sie lädt sich auf, während das Spiel läuft. Greifen Gegner sofort an, entfällt der Überraschungsbonus. Gegnerfähigkeiten sind blutende Treffer, Schweigen und Schlaf. Die Waldbanditen bringen ihr Lager und ihre Bossgruppe mit ihrer Beute. Kleinere Banditengruppen kommen nur zusammen mit den Waldbanditen." + Paragraph
            + "Unter Komfort zeigt das Schildband den Schild einer Figur als Wabenband über den Lebensleisten. Ist die taktische Formation aus, ist der Formationsknopf ausgeblendet, Formationen und Prioritäten ruhen. Sie bleiben gespeichert." + Paragraph
            + "Die Themen darunter erklären die Regeln einzeln.",
        "Delvers добавляет в Dungeon Settlers новые правила. Большинство действует в боевом Apocalypse, на сложности выше Downfall. Некоторые можно включить и для других кампаний в меню Delvers." + Paragraph
            + "В меню Delvers переключатель Apocalypse в разделе «Сложность» при создании кампании добавляет Apocalypse выше самого высокого уровня. Существующие кампании сохраняют свой уровень." + Paragraph
            + "Опции для других сложностей включают отдельные правила для кампаний без Apocalypse: кровотечение, без паузы, враги атакуют сразу, способности врагов и Лесные бандиты. Они действуют со следующей загрузки." + Paragraph
            + "Без паузы клавиша паузы вместо этого замедляет время вдвое, до 5 секунд. Запас восстанавливается, пока идёт игра. Враги, которые атакуют сразу, не дают бонуса внезапности. Способности врагов: кровоточащие удары, безмолвие и сон. Лесные бандиты приносят свой лагерь и отряд боссов с его добычей. Небольшие группы бандитов появляются только вместе с Лесными бандитами." + Paragraph
            + "В разделе «Удобство» полоса щита показывает щит юнита полосой в виде сот над полосами здоровья. Если выключить тактический строй, кнопка строя исчезнет, а строи и приоритетные места перестанут действовать. Они останутся сохранёнными." + Paragraph
            + "Темы ниже объясняют каждое правило.",
        "Delvers 为 Dungeon Settlers 加入新规则。大多数只在战斗 Apocalypse 中生效，这是高于 Downfall 的难度。部分规则也可以在 Delvers 菜单中为其他战役开启。" + Paragraph
            + "在 Delvers 菜单的难度下打开 Apocalypse 开关后，创建战役时会在最高难度之上提供 Apocalypse。已有战役保持原难度。" + Paragraph
            + "其他难度的选项可以为没有 Apocalypse 的战役逐条开启规则：流血、无法暂停、敌人立即进攻、敌人能力和森林强盗团。下次读取时生效。" + Paragraph
            + "无法暂停时，暂停键改为将时间减慢至一半速度，最多 5 秒。游戏运行时会重新充能。敌人立即进攻时没有偷袭加成。敌人能力包括流血攻击、沉默和睡眠。森林强盗团会带来他们的营地和首领小队及其战利品。小股强盗团只会与森林强盗团一同出现。" + Paragraph
            + "在便利功能中，护盾条会在生命条上方用蜂窝条显示单位的护盾。关闭战术队形后，队形按钮会隐藏，队形和优先位置不再生效。它们仍会保存。" + Paragraph
            + "下面的主题逐条说明这些规则。",
        "Delvers 為 Dungeon Settlers 加入新規則。大多數只在戰鬥 Apocalypse 中生效，這是高於 Downfall 的難度。部分規則也可以在 Delvers 選單中為其他戰役開啟。" + Paragraph
            + "在 Delvers 選單的難度下開啟 Apocalypse 開關後，建立戰役時會在最高難度之上提供 Apocalypse。現有戰役維持原難度。" + Paragraph
            + "其他難度的選項可以為沒有 Apocalypse 的戰役逐條開啟規則：流血、無法暫停、敵人立即進攻、敵人能力和森林強盜團。下次讀取時生效。" + Paragraph
            + "無法暫停時，暫停鍵改為將時間減慢至一半速度，最多 5 秒。遊戲運行時會重新充能。敵人立即進攻時沒有偷襲加成。敵人能力包括流血攻擊、沉默和睡眠。森林強盜團會帶來他們的營地和首領小隊及其戰利品。小股強盜團只會與森林強盜團一同出現。" + Paragraph
            + "在便利功能中，護盾條會在生命條上方用蜂窩條顯示單位的護盾。關閉戰術隊形後，隊形按鈕會隱藏，隊形和優先位置不再生效。它們仍會保存。" + Paragraph
            + "下面的主題逐條說明這些規則。",
        "Delvers は Dungeon Settlers に新しいルールを加えます。多くは Downfall より上の難易度である戦闘 Apocalypse で適用されます。一部は Delvers メニューで他のキャンペーンでも有効にできます。" + Paragraph
            + "Delvers メニューの難易度にある Apocalypse のスイッチをオンにすると、キャンペーン作成時に最高難易度の上に Apocalypse が追加されます。既存のキャンペーンの難易度は変わりません。" + Paragraph
            + "他の難易度向けのオプションで、Apocalypse ではないキャンペーンにもルールを一つずつ有効にできます：出血、一時停止なし、敵が即座に攻撃、敵の特殊能力、森の盗賊団。次回のロードから有効です。" + Paragraph
            + "一時停止なしでは、代わりに一時停止キーで時間が半分の速さになります。最大5秒で、ゲームの進行中に回復します。敵が即座に攻撃するときは奇襲ボーナスがありません。敵の特殊能力は出血攻撃、沈黙、睡眠です。森の盗賊団は野営地とボス一行、その戦利品をもたらします。小規模な盗賊団は森の盗賊団と一緒の場合にのみ現れます。" + Paragraph
            + "快適機能では、シールドバーが体力バーの上のハニカムの帯でユニットのシールドを表示します。戦術隊形をオフにすると隊形ボタンが消え、隊形と優先枠は働かなくなります。保存はそのまま残ります。" + Paragraph
            + "以下の項目で各ルールを説明します。",
        "Delvers añade nuevas reglas a Dungeon Settlers. La mayoría se aplican en Apocalypse de combate, la dificultad por encima de Downfall. Algunas también se pueden activar para otras campañas en el menú Delvers." + Paragraph
            + "En el menú Delvers, el interruptor Apocalypse en Dificultad ofrece Apocalypse por encima del nivel más alto al crear una campaña. Las campañas existentes mantienen su nivel." + Paragraph
            + "Las opciones para otras dificultades activan reglas sueltas en campañas sin Apocalypse: hemorragias, sin pausa, los enemigos atacan de inmediato, habilidades enemigas y los Bandidos del bosque. Se aplican en la próxima carga." + Paragraph
            + "Sin pausa, la tecla de pausa ralentiza el tiempo a la mitad, hasta 5 segundos. Se recarga mientras el juego avanza. Los enemigos que atacan de inmediato no dan bonus sorpresa. Las habilidades enemigas son golpes sangrantes, silencio y sueño. Los Bandidos del bosque traen su campamento y su grupo jefe con su botín. Los grupos pequeños de bandidos solo vienen junto con los Bandidos del bosque." + Paragraph
            + "En Comodidad, la barra de escudo muestra el escudo de una unidad con una barra de panal sobre las barras de vida. Con la formación táctica desactivada, el botón de formación desaparece y las formaciones y los huecos de prioridad dejan de actuar. Siguen guardados." + Paragraph
            + "Los temas de abajo explican cada regla.",
        "Delvers adiciona novas regras a Dungeon Settlers. A maioria vale no Apocalypse de combate, a dificuldade acima de Downfall. Algumas também podem ser ativadas para outras campanhas no menu Delvers." + Paragraph
            + "No menu Delvers, o interruptor Apocalypse em Dificuldade oferece Apocalypse acima do nível mais alto ao criar uma campanha. As campanhas existentes mantêm seu nível." + Paragraph
            + "As opções para outras dificuldades ativam regras avulsas em campanhas sem Apocalypse: sangramentos, sem pausa, inimigos atacam imediatamente, habilidades inimigas e os Bandidos da floresta. Valem a partir do próximo carregamento." + Paragraph
            + "Sem pausa, a tecla de pausa deixa o tempo na metade da velocidade, por até 5 segundos. Ela recarrega enquanto o jogo roda. Inimigos que atacam imediatamente não dão bônus surpresa. As habilidades inimigas são golpes que sangram, silêncio e sono. Os Bandidos da floresta trazem seu acampamento e seu grupo chefe com seu saque. Os grupos menores de bandidos só vêm junto com os Bandidos da floresta." + Paragraph
            + "Em Conforto, a barra de escudo mostra o escudo de uma unidade com uma barra de favo sobre as barras de vida. Com a formação tática desativada, o botão de formação some e as formações e os espaços de prioridade deixam de agir. Eles continuam salvos." + Paragraph
            + "Os tópicos abaixo explicam cada regra.");

    private static readonly UniqueCandidateTranslations WoundsTitle = new(
        "Bleeding",
        "출혈",
        "Saignement",
        "Blutung",
        "Кровотечение",
        "流血",
        "流血",
        "出血",
        "Hemorragias",
        "Sangramento");

    private static readonly UniqueCandidateTranslations WoundsText = new(
        "Every bleeding stack of a hit adds 10 blood loss. The blood loss bleeds on and takes the body first, then the head. When the head is empty, the character falls near death. While a bleeding is open, the body does not heal on its own." + Paragraph
            + "The stages follow the blood loss compared to the whole health, body and head together. Light Bleeding up to 10%, it bleeds 1 health every 3 seconds. Deep Bleeding from 10%, 1 health per second. Heavy Bleeding from 25%, 2 health per second." + Paragraph
            + "Lethal Bleeding: from the heavy stage on, a bleeding turns lethal as soon as the blood loss covers all the health left. It bleeds 2 health per second and stays lethal until a bandage brings the blood loss below the health left or the bleeding is stopped." + Paragraph
            + "In a bed every stage bleeds half as fast. Healing with a source, from skills and items, is weaker: 90% on Light Bleeding, 80% on Deep, 70% on Heavy, 50% on Lethal. When the blood loss is larger than the whole health, healing on Lethal Bleeding drops further, but never below 25%. Healing beyond full health takes blood loss away instead." + Paragraph
            + "A bleeding ends when its blood loss has bled out. It is stopped by the herbal bandage, the salve bandage, the aid kit, the black serum, the portal, a rescue from near death and a revival. See Bandages and first aid.",
        "공격의 출혈 중첩마다 출혈량이 10 늘어납니다. 출혈량은 계속 흘러나가며 먼저 몸을, 그다음 머리를 깎습니다. 머리가 비면 캐릭터는 빈사 상태가 됩니다. 출혈이 이어지는 동안 몸은 스스로 회복되지 않습니다." + Paragraph
            + "단계는 전체 체력(몸과 머리 합계)에 대한 출혈량의 비율로 정해집니다. 가벼운 출혈은 10%까지이며 3초마다 체력 1을 잃습니다. 깊은 출혈은 10%부터이며 초당 1, 심한 출혈은 25%부터이며 초당 2를 잃습니다." + Paragraph
            + "치명적인 출혈: 심한 출혈 단계부터, 출혈량이 남은 체력 전부를 덮는 순간 출혈이 치명적으로 바뀝니다. 초당 체력 2를 잃으며, 붕대로 출혈량을 남은 체력 아래로 낮추거나 출혈이 멈출 때까지 치명적인 상태로 남습니다." + Paragraph
            + "침대에서는 모든 단계가 절반 속도로 흐릅니다. 스킬이나 아이템처럼 출처가 있는 회복은 약해집니다: 가벼운 출혈 90%, 깊은 출혈 80%, 심한 출혈 70%, 치명적인 출혈 50%. 출혈량이 전체 체력보다 크면 치명적인 출혈의 회복은 더 떨어지지만 25% 아래로는 내려가지 않습니다. 체력이 가득 찬 뒤의 초과 회복은 대신 출혈량을 줄입니다." + Paragraph
            + "출혈은 출혈량이 다 흘러나가면 끝납니다. 약초 붕대, 연고 붕대, 구급상자, 검은 혈청, 포털, 빈사 상태에서의 구조, 부활로 멈출 수 있습니다. 자세한 내용은 붕대와 응급처치를 보세요.",
        "Chaque cumul de saignement d'un coup ajoute 10 de perte de sang. La perte de sang continue de couler et prend d'abord le corps, puis la tête. Quand la tête est vide, le personnage tombe au seuil de la mort. Tant qu'un saignement est ouvert, le corps ne guérit pas seul." + Paragraph
            + "Les stades dépendent de la perte de sang rapportée à la vie entière, corps et tête ensemble. Saignement léger jusqu'à 10 %, il fait perdre 1 point de vie toutes les 3 secondes. Saignement profond dès 10 %, 1 point par seconde. Saignement grave dès 25 %, 2 points par seconde." + Paragraph
            + "Saignement mortel : dès le stade grave, un saignement devient mortel dès que la perte de sang couvre toute la vie restante. Il fait perdre 2 points de vie par seconde et reste mortel jusqu'à ce qu'un bandage ramène la perte de sang sous la vie restante ou que le saignement soit arrêté." + Paragraph
            + "Dans un lit, chaque stade saigne deux fois moins vite. Les soins avec une source, compétences et objets, sont plus faibles : 90 % en saignement léger, 80 % en profond, 70 % en grave, 50 % en mortel. Si la perte de sang dépasse la vie entière, les soins en saignement mortel baissent encore, mais jamais sous 25 %. Les soins qui dépassent la vie pleine retirent de la perte de sang à la place." + Paragraph
            + "Un saignement prend fin quand sa perte de sang s'est écoulée. Il est arrêté par le bandage aux herbes, le bandage à l'onguent, la trousse de soins, le sérum noir, le portail, un sauvetage au seuil de la mort et une résurrection. Voir Bandages et premiers secours.",
        "Jeder Blutungsstapel eines Treffers bringt 10 Blutverlust. Der Blutverlust blutet nach und nimmt erst den Körper, dann den Kopf. Ist der Kopf leer, fällt die Figur in die Todesnähe. Solange eine Blutung offen ist, heilt der Körper nicht von selbst." + Paragraph
            + "Die Stufen richten sich nach dem Blutverlust im Verhältnis zum ganzen Leben, Körper und Kopf zusammen. Leichte Blutung bis 10 %, sie blutet 1 Leben alle 3 Sekunden nach. Tiefe Blutung ab 10 %, 1 Leben pro Sekunde. Schwere Blutung ab 25 %, 2 Leben pro Sekunde." + Paragraph
            + "Tödliche Blutung: Ab der schweren Stufe wird eine Blutung tödlich, sobald der Blutverlust das ganze restliche Leben deckt. Sie blutet 2 Leben pro Sekunde nach und bleibt tödlich, bis ein Verband den Blutverlust unter das restliche Leben drückt oder die Blutung gestillt ist." + Paragraph
            + "Im Bett blutet jede Stufe halb so schnell. Heilung mit Quelle, also durch Fähigkeiten und Gegenstände, wirkt schwächer: bei leichter Blutung zu 90 %, bei tiefer zu 80 %, bei schwerer zu 70 %, bei tödlicher zu 50 %. Ist der Blutverlust größer als das ganze Leben, sinkt die Heilung bei tödlicher Blutung weiter, aber nie unter 25 %. Heilung über das volle Leben hinaus nimmt stattdessen Blutverlust weg." + Paragraph
            + "Eine Blutung endet, wenn ihr Blutverlust ausgeblutet ist. Gestillt wird sie durch Kräuterverband, Salbenverband, den Verbandskasten, das schwarze Serum, das Portal, eine Rettung aus der Todesnähe und eine Wiederbelebung. Mehr dazu unter Verbände und Erste Hilfe.",
        "Каждый стак кровотечения от удара добавляет 10 потери крови. Потеря крови продолжает вытекать и забирает сначала тело, затем голову. Когда голова пуста, персонаж оказывается на пороге смерти. Пока кровотечение открыто, тело само не восстанавливается." + Paragraph
            + "Стадии зависят от потери крови по отношению ко всему здоровью, телу и голове вместе. Лёгкое кровотечение до 10%, отнимает 1 здоровье каждые 3 секунды. Глубокое кровотечение от 10%, 1 здоровье в секунду. Тяжёлое кровотечение от 25%, 2 здоровья в секунду." + Paragraph
            + "Смертельное кровотечение: начиная с тяжёлой стадии, кровотечение становится смертельным, как только потеря крови покрывает всё оставшееся здоровье. Оно отнимает 2 здоровья в секунду и остаётся смертельным, пока бинт не опустит потерю крови ниже оставшегося здоровья или кровотечение не будет остановлено." + Paragraph
            + "В кровати каждая стадия кровоточит вдвое медленнее. Лечение с источником, от навыков и предметов, слабее: 90% при лёгком кровотечении, 80% при глубоком, 70% при тяжёлом, 50% при смертельном. Если потеря крови больше всего здоровья, лечение при смертельном кровотечении падает ещё сильнее, но не ниже 25%. Лечение сверх полного здоровья вместо этого убирает потерю крови." + Paragraph
            + "Кровотечение заканчивается, когда его потеря крови вытекла. Его останавливают травяной бинт, мазевый бинт, аптечка, чёрная сыворотка, портал, спасение с порога смерти и воскрешение. Подробнее в разделе «Бинты и первая помощь».",
        "攻击的每层流血会带来 10 点失血量。失血量会持续流失，先扣身体，再扣头部。头部清空时，角色陷入濒死。流血未止住期间，身体不会自行恢复。" + Paragraph
            + "流血阶段取决于失血量占全部生命（身体和头部合计）的比例。轻度流血不超过 10%，每 3 秒流失 1 点生命。深度流血从 10% 起，每秒 1 点。重度流血从 25% 起，每秒 2 点。" + Paragraph
            + "致命流血：从重度阶段起，一旦失血量覆盖了剩余的全部生命，流血就会变为致命。它每秒流失 2 点生命，并保持致命，直到绷带让失血量低于剩余生命，或流血被止住。" + Paragraph
            + "在床上，每个阶段的流血速度减半。有来源的治疗，即技能和物品带来的治疗，效果会减弱：轻度流血 90%，深度 80%，重度 70%，致命 50%。失血量大于全部生命时，致命流血下的治疗还会继续降低，但不会低于 25%。超出满生命的治疗会改为减少失血量。" + Paragraph
            + "失血量流尽时，流血结束。草药绷带、药膏绷带、急救箱、黑色血清、传送门、濒死救援和复活都能止住流血。详见“绷带与急救”。",
        "攻擊的每層流血會帶來 10 點失血量。失血量會持續流失，先扣身體，再扣頭部。頭部清空時，角色陷入瀕死。流血未止住期間，身體不會自行恢復。" + Paragraph
            + "流血階段取決於失血量占全部生命（身體和頭部合計）的比例。輕度流血不超過 10%，每 3 秒流失 1 點生命。深度流血從 10% 起，每秒 1 點。重度流血從 25% 起，每秒 2 點。" + Paragraph
            + "致命流血：從重度階段起，一旦失血量覆蓋了剩餘的全部生命，流血就會變為致命。它每秒流失 2 點生命，並保持致命，直到繃帶讓失血量低於剩餘生命，或流血被止住。" + Paragraph
            + "在床上，每個階段的流血速度減半。有來源的治療，即技能和物品帶來的治療，效果會減弱：輕度流血 90%，深度 80%，重度 70%，致命 50%。失血量大於全部生命時，致命流血下的治療還會繼續降低，但不會低於 25%。超出滿生命的治療會改為減少失血量。" + Paragraph
            + "失血量流盡時，流血結束。草藥繃帶、藥膏繃帶、急救箱、黑色血清、傳送門、瀕死救援和復活都能止住流血。詳見「繃帶與急救」。",
        "攻撃の出血スタック1つごとに失血量が10増えます。失血量は流れ続け、まず体、次に頭を削ります。頭が空になるとキャラクターは瀕死になります。出血が止まっていない間、体は自然に回復しません。" + Paragraph
            + "段階は、体力全体（体と頭の合計）に対する失血量の割合で決まります。軽い出血は10%まで、3秒ごとに体力を1失います。深い出血は10%から、毎秒1。重い出血は25%から、毎秒2です。" + Paragraph
            + "致命的な出血：重い段階から、失血量が残りの体力をすべて覆った時点で出血は致命的になります。毎秒体力を2失い、包帯で失血量を残りの体力より下げるか、出血が止まるまで致命的なままです。" + Paragraph
            + "ベッドではどの段階も半分の速さで出血します。スキルやアイテムなど、出どころのある回復は弱まります：軽い出血で90%、深い出血で80%、重い出血で70%、致命的な出血で50%。失血量が体力全体より大きいと、致命的な出血での回復はさらに下がりますが、25%を下回ることはありません。満タンを超える回復は、代わりに失血量を減らします。" + Paragraph
            + "失血量を出し切ると出血は終わります。薬草包帯、軟膏包帯、救急箱、黒い血清、ポータル、瀕死からの救助、蘇生で止められます。詳しくは「包帯と応急手当」を見てください。",
        "Cada acumulación de sangrado de un golpe suma 10 de pérdida de sangre. La pérdida de sangre sigue fluyendo y quita primero el cuerpo y después la cabeza. Cuando la cabeza se vacía, el personaje queda al borde de la muerte. Mientras una hemorragia siga abierta, el cuerpo no se cura solo." + Paragraph
            + "Las etapas dependen de la pérdida de sangre frente a la vida total, cuerpo y cabeza juntos. Hemorragia leve hasta el 10 %, quita 1 de vida cada 3 segundos. Hemorragia profunda desde el 10 %, 1 por segundo. Hemorragia grave desde el 25 %, 2 por segundo." + Paragraph
            + "Hemorragia mortal: a partir de la etapa grave, una hemorragia se vuelve mortal en cuanto la pérdida de sangre cubre toda la vida que queda. Quita 2 de vida por segundo y sigue siendo mortal hasta que una venda baje la pérdida de sangre por debajo de la vida restante o se detenga la hemorragia." + Paragraph
            + "En una cama, cada etapa sangra la mitad de rápido. La curación con fuente, de habilidades y objetos, es más débil: 90 % con hemorragia leve, 80 % con profunda, 70 % con grave, 50 % con mortal. Si la pérdida de sangre supera la vida total, la curación con hemorragia mortal baja más, pero nunca del 25 %. La curación por encima de la vida completa quita pérdida de sangre en su lugar." + Paragraph
            + "Una hemorragia termina cuando su pérdida de sangre se ha agotado. La detienen la venda de hierbas, la venda de ungüento, el botiquín, el suero negro, el portal, un rescate al borde de la muerte y una resurrección. Ver Vendas y primeros auxilios.",
        "Cada acúmulo de sangramento de um golpe soma 10 de perda de sangue. A perda de sangue continua escorrendo e tira primeiro o corpo, depois a cabeça. Quando a cabeça esvazia, o personagem fica à beira da morte. Enquanto um sangramento estiver aberto, o corpo não se cura sozinho." + Paragraph
            + "Os estágios dependem da perda de sangue em relação à vida total, corpo e cabeça juntos. Sangramento leve até 10%, tira 1 de vida a cada 3 segundos. Sangramento profundo a partir de 10%, 1 por segundo. Sangramento grave a partir de 25%, 2 por segundo." + Paragraph
            + "Sangramento letal: a partir do estágio grave, um sangramento se torna letal assim que a perda de sangue cobre toda a vida restante. Ele tira 2 de vida por segundo e continua letal até que uma bandagem baixe a perda de sangue abaixo da vida restante ou o sangramento seja estancado." + Paragraph
            + "Na cama, cada estágio sangra com metade da velocidade. A cura com fonte, de habilidades e itens, é mais fraca: 90% no sangramento leve, 80% no profundo, 70% no grave, 50% no letal. Se a perda de sangue for maior que a vida total, a cura no sangramento letal cai ainda mais, mas nunca abaixo de 25%. A cura além da vida cheia tira perda de sangue no lugar." + Paragraph
            + "Um sangramento termina quando sua perda de sangue escorreu toda. Ele é estancado pela bandagem de ervas, pela bandagem de pomada, pelo kit de primeiros socorros, pelo soro negro, pelo portal, por um resgate à beira da morte e por uma ressurreição. Veja Bandagens e primeiros socorros.");

    private static readonly UniqueCandidateTranslations BandagesTitle = new(
        "Bandages and first aid", "붕대와 응급처치", "Bandages et premiers secours", "Verbände und Erste Hilfe", "Бинты и первая помощь",
        "绷带与急救", "繃帶與急救", "包帯と応急手当", "Vendas y primeros auxilios", "Bandagens e primeiros socorros");

    private static readonly UniqueCandidateTranslations BandagesText = new(
        "Light bandage: takes 10% of the whole health off the blood loss, on every stage, Lethal Bleeding too. It does not stop the bleeding and leaves the bleeding stacks." + Paragraph
            + "Heavy bandage: takes 20% of the whole health off the blood loss, otherwise like the light one." + Paragraph
            + "Herbal bandage: stops a light, deep or heavy bleeding completely and takes a third of the bleeding stacks. On Lethal Bleeding it takes 30% of the whole health off the blood loss." + Paragraph
            + "Salve bandage: stops a light, deep or heavy bleeding completely, takes half of the bleeding stacks and keeps healing afterwards. On Lethal Bleeding it takes 40% of the whole health off the blood loss." + Paragraph
            + "Aid kit: stops every bleeding completely, a lethal one too, along with the bleeding stacks." + Paragraph
            + "A standing, bleeding character applies the aid kit to itself with Q. With Shift+Q or the inventory option Use on another character, a bandage or the aid kit goes on another bleeding character, a fallen one too. Every item works there with its normal strength. Raw herbs work on another character like a light bandage. The game's ointment only heals." + Paragraph
            + "Merchants sell light and heavy bandages, and the workbench makes them. After the First Aid research, the alchemy table makes herbal and salve bandages.",
        "가벼운 붕대: 단계와 관계없이, 치명적인 출혈에서도 출혈량에서 전체 체력의 10%를 줄입니다. 출혈을 멈추지 않으며 출혈 중첩은 그대로 둡니다." + Paragraph
            + "무거운 붕대: 출혈량에서 전체 체력의 20%를 줄이며, 나머지는 가벼운 붕대와 같습니다." + Paragraph
            + "약초 붕대: 가벼운, 깊은, 심한 출혈을 완전히 멈추고 출혈 중첩의 3분의 1을 없앱니다. 치명적인 출혈에서는 출혈량에서 전체 체력의 30%를 줄입니다." + Paragraph
            + "연고 붕대: 가벼운, 깊은, 심한 출혈을 완전히 멈추고 출혈 중첩의 절반을 없애며 그 뒤에도 계속 회복합니다. 치명적인 출혈에서는 출혈량에서 전체 체력의 40%를 줄입니다." + Paragraph
            + "구급상자: 치명적인 출혈을 포함해 모든 출혈을 출혈 중첩과 함께 완전히 멈춥니다." + Paragraph
            + "서 있는 상태로 피를 흘리는 캐릭터는 Q로 구급상자를 스스로 사용합니다. Shift+Q 또는 인벤토리의 다른 캐릭터에게 사용으로 붕대나 구급상자를 피를 흘리는 다른 캐릭터에게, 쓰러진 캐릭터에게도 쓸 수 있습니다. 모든 아이템은 그때도 원래 효과 그대로 작용합니다. 야생 약초는 다른 캐릭터에게 가벼운 붕대처럼 작용합니다. 게임의 연고는 회복만 합니다." + Paragraph
            + "가벼운 붕대와 무거운 붕대는 상인이 판매하며 작업대에서 만들 수 있습니다. 응급처치 연구 후에는 연금술 작업대에서 약초 붕대와 연고 붕대를 만듭니다.",
        "Bandage léger : retire 10 % de la vie entière de la perte de sang, à chaque stade, en saignement mortel aussi. Il n'arrête pas le saignement et laisse les cumuls de saignement." + Paragraph
            + "Bandage lourd : retire 20 % de la vie entière de la perte de sang, sinon comme le léger." + Paragraph
            + "Bandage aux herbes : arrête entièrement un saignement léger, profond ou grave et retire un tiers des cumuls de saignement. En saignement mortel, il retire 30 % de la vie entière de la perte de sang." + Paragraph
            + "Bandage à l'onguent : arrête entièrement un saignement léger, profond ou grave, retire la moitié des cumuls de saignement et continue de soigner ensuite. En saignement mortel, il retire 40 % de la vie entière de la perte de sang." + Paragraph
            + "Trousse de soins : arrête entièrement tout saignement, même mortel, avec les cumuls de saignement." + Paragraph
            + "Un personnage debout qui saigne s'applique la trousse de soins avec Q. Avec Maj+Q ou l'option d'inventaire Utiliser sur un autre personnage, un bandage ou la trousse de soins va sur un autre personnage qui saigne, même tombé. Chaque objet y agit avec sa force normale. Les herbes sauvages agissent sur un autre personnage comme un bandage léger. L'onguent du jeu ne fait que soigner." + Paragraph
            + "Les marchands vendent les bandages légers et lourds, et l'établi les fabrique. Après la recherche Premiers secours, la table d'alchimie fabrique des bandages aux herbes et à l'onguent.",
        "Leichter Verband: nimmt 10 % des ganzen Lebens vom Blutverlust, auf jeder Stufe, auch bei tödlicher Blutung. Er stillt nicht und lässt die Blutungsstapel stehen." + Paragraph
            + "Schwerer Verband: nimmt 20 % des ganzen Lebens vom Blutverlust, sonst wie der leichte." + Paragraph
            + "Kräuterverband: stillt eine leichte, tiefe oder schwere Blutung ganz und nimmt ein Drittel der Blutungsstapel. Bei tödlicher Blutung nimmt er 30 % des ganzen Lebens vom Blutverlust." + Paragraph
            + "Salbenverband: stillt eine leichte, tiefe oder schwere Blutung ganz, nimmt die Hälfte der Blutungsstapel und heilt danach weiter. Bei tödlicher Blutung nimmt er 40 % des ganzen Lebens vom Blutverlust." + Paragraph
            + "Verbandskasten: stillt jede Blutung ganz, auch eine tödliche, samt den Blutungsstapeln." + Paragraph
            + "Eine stehende, blutende Figur legt sich den Verbandskasten mit Q selbst an. Mit Shift+Q oder der Inventaroption Auf andere Figur anwenden geht ein Verband oder der Verbandskasten an eine andere blutende Figur, auch an eine gefallene. Jeder Gegenstand wirkt dort mit seiner normalen Stärke. Wildkräuter wirken auf eine andere Figur wie ein leichter Verband. Die Salbe des Spiels heilt nur." + Paragraph
            + "Leichte und schwere Verbände führen Händler, die Werkbank stellt sie her. Nach der Forschung Erste Hilfe entstehen am Alchemietisch Kräuterverbände und Salbenverbände.",
        "Лёгкий бинт: снимает с потери крови 10% всего здоровья на любой стадии, в том числе при смертельном кровотечении. Он не останавливает кровотечение и оставляет стаки кровотечения." + Paragraph
            + "Тяжёлый бинт: снимает с потери крови 20% всего здоровья, в остальном как лёгкий." + Paragraph
            + "Травяной бинт: полностью останавливает лёгкое, глубокое или тяжёлое кровотечение и убирает треть стаков кровотечения. При смертельном кровотечении он снимает с потери крови 30% всего здоровья." + Paragraph
            + "Мазевый бинт: полностью останавливает лёгкое, глубокое или тяжёлое кровотечение, убирает половину стаков кровотечения и потом продолжает лечить. При смертельном кровотечении он снимает с потери крови 40% всего здоровья." + Paragraph
            + "Аптечка: полностью останавливает любое кровотечение, даже смертельное, вместе со стаками кровотечения." + Paragraph
            + "Стоящий персонаж с кровотечением сам накладывает аптечку клавишей Q. Через Shift+Q или пункт инвентаря «Применить к другому персонажу» бинт или аптечка достаются другому персонажу с кровотечением, в том числе павшему. Каждый предмет действует там с обычной силой. Дикие травы действуют на другого персонажа как лёгкий бинт. Мазь из игры только лечит." + Paragraph
            + "Лёгкие и тяжёлые бинты продают торговцы, а делает верстак. После исследования «Первая помощь» алхимический стол делает травяные и мазевые бинты.",
        "轻绷带：在任何阶段，包括致命流血，都能减少相当于全部生命 10% 的失血量。它不会止住流血，也不会减少流血层数。" + Paragraph
            + "重绷带：减少相当于全部生命 20% 的失血量，其余与轻绷带相同。" + Paragraph
            + "草药绷带：彻底止住轻度、深度或重度流血，并减少三分之一的流血层数。在致命流血时，减少相当于全部生命 30% 的失血量。" + Paragraph
            + "药膏绷带：彻底止住轻度、深度或重度流血，减少一半的流血层数，之后还会继续治疗。在致命流血时，减少相当于全部生命 40% 的失血量。" + Paragraph
            + "急救箱：彻底止住任何流血，包括致命流血，并清除流血层数。" + Paragraph
            + "站立且正在流血的角色按 Q 即可自己使用急救箱。通过 Shift+Q 或背包选项“对其他角色使用”，可以把绷带或急救箱用在其他流血的角色身上，倒下的角色也可以。每种物品在那里都以正常效果生效。野生草药对其他角色的效果与轻绷带相同。游戏的药膏只会治疗。" + Paragraph
            + "商人出售轻绷带和重绷带，工作台也能制作。完成急救研究后，炼金台可以制作草药绷带和药膏绷带。",
        "輕繃帶：在任何階段，包括致命流血，都能減少相當於全部生命 10% 的失血量。它不會止住流血，也不會減少流血層數。" + Paragraph
            + "重繃帶：減少相當於全部生命 20% 的失血量，其餘與輕繃帶相同。" + Paragraph
            + "草藥繃帶：徹底止住輕度、深度或重度流血，並減少三分之一的流血層數。在致命流血時，減少相當於全部生命 30% 的失血量。" + Paragraph
            + "藥膏繃帶：徹底止住輕度、深度或重度流血，減少一半的流血層數，之後還會繼續治療。在致命流血時，減少相當於全部生命 40% 的失血量。" + Paragraph
            + "急救箱：徹底止住任何流血，包括致命流血，並清除流血層數。" + Paragraph
            + "站立且正在流血的角色按 Q 即可自己使用急救箱。透過 Shift+Q 或背包選項「對其他角色使用」，可以把繃帶或急救箱用在其他流血的角色身上，倒下的角色也可以。每種物品在那裡都以正常效果生效。野生草藥對其他角色的效果與輕繃帶相同。遊戲的藥膏只會治療。" + Paragraph
            + "商人出售輕繃帶和重繃帶，工作台也能製作。完成急救研究後，煉金台可以製作草藥繃帶和藥膏繃帶。",
        "軽い包帯：どの段階でも、致命的な出血でも、失血量から体力全体の10%を取り除きます。出血は止めず、出血スタックもそのままです。" + Paragraph
            + "重い包帯：失血量から体力全体の20%を取り除き、それ以外は軽い包帯と同じです。" + Paragraph
            + "薬草包帯：軽い、深い、重い出血を完全に止め、出血スタックの3分の1を取り除きます。致命的な出血では、失血量から体力全体の30%を取り除きます。" + Paragraph
            + "軟膏包帯：軽い、深い、重い出血を完全に止め、出血スタックの半分を取り除き、その後も回復を続けます。致命的な出血では、失血量から体力全体の40%を取り除きます。" + Paragraph
            + "救急箱：致命的な出血も含めてすべての出血を、出血スタックごと完全に止めます。" + Paragraph
            + "立っていて出血しているキャラクターは、Q で救急箱を自分に使います。Shift+Q かインベントリの「他のキャラクターに使う」で、包帯や救急箱を出血している別のキャラクターに、倒れたキャラクターにも使えます。どのアイテムもそこで通常の効果で働きます。野草は別のキャラクターには軽い包帯と同じように働きます。ゲームの軟膏は回復するだけです。" + Paragraph
            + "軽い包帯と重い包帯は商人が扱っており、作業台で作れます。応急手当の研究後、錬金台で薬草包帯と軟膏包帯を作れます。",
        "Venda ligera: quita el 10 % de la vida total de la pérdida de sangre, en cualquier etapa, también con hemorragia mortal. No detiene la hemorragia y deja las acumulaciones de sangrado." + Paragraph
            + "Venda pesada: quita el 20 % de la vida total de la pérdida de sangre; por lo demás, como la ligera." + Paragraph
            + "Venda de hierbas: detiene por completo una hemorragia leve, profunda o grave y quita un tercio de las acumulaciones de sangrado. Con hemorragia mortal quita el 30 % de la vida total de la pérdida de sangre." + Paragraph
            + "Venda de ungüento: detiene por completo una hemorragia leve, profunda o grave, quita la mitad de las acumulaciones de sangrado y sigue curando después. Con hemorragia mortal quita el 40 % de la vida total de la pérdida de sangre." + Paragraph
            + "Botiquín: detiene por completo cualquier hemorragia, también una mortal, junto con las acumulaciones de sangrado." + Paragraph
            + "Un personaje de pie que sangra se aplica el botiquín con Q. Con Mayús+Q o la opción de inventario Usar en otro personaje, una venda o el botiquín van a otro personaje que sangra, también a uno caído. Cada objeto actúa allí con su fuerza normal. Las hierbas silvestres actúan en otro personaje como una venda ligera. El ungüento del juego solo cura." + Paragraph
            + "Los comerciantes venden vendas ligeras y pesadas, y el banco de trabajo las fabrica. Tras la investigación Primeros auxilios, la mesa de alquimia fabrica vendas de hierbas y de ungüento.",
        "Bandagem leve: tira 10% da vida total da perda de sangue, em qualquer estágio, também no sangramento letal. Ela não estanca o sangramento e deixa os acúmulos de sangramento." + Paragraph
            + "Bandagem pesada: tira 20% da vida total da perda de sangue; no resto, como a leve." + Paragraph
            + "Bandagem de ervas: estanca por completo um sangramento leve, profundo ou grave e tira um terço dos acúmulos de sangramento. No sangramento letal, tira 30% da vida total da perda de sangue." + Paragraph
            + "Bandagem de pomada: estanca por completo um sangramento leve, profundo ou grave, tira metade dos acúmulos de sangramento e continua curando depois. No sangramento letal, tira 40% da vida total da perda de sangue." + Paragraph
            + "Kit de primeiros socorros: estanca por completo qualquer sangramento, também um letal, junto com os acúmulos de sangramento." + Paragraph
            + "Um personagem de pé que está sangrando aplica em si o kit de primeiros socorros com Q. Com Shift+Q ou a opção de inventário Usar em outro personagem, uma bandagem ou o kit vão para outro personagem que está sangrando, também um caído. Cada item age ali com sua força normal. As ervas silvestres agem em outro personagem como uma bandagem leve. A pomada do jogo só cura." + Paragraph
            + "Os mercadores vendem bandagens leves e pesadas, e a bancada de trabalho as fabrica. Após a pesquisa Primeiros Socorros, a mesa de alquimia fabrica bandagens de ervas e de pomada.");

    private static readonly UniqueCandidateTranslations ReviveTitle = FirstHintText.ReviveTitle;

    private static readonly UniqueCandidateTranslations ReviveText = new(
        "With the Scroll of Revival a living character brings a fallen one back. They stand up next to the body after 20 seconds." + Paragraph
            + "How often a character can come back depends on the combat difficulty: 2 times on Apocalypse and Downfall, 4 times on Very Hard, unlimited below." + Paragraph
            + "What comes after is explained under After a fall. The guild merchant always sells the scroll, dungeon merchants sometimes.",
        "살아 있는 캐릭터가 부활의 두루마리로 쓰러진 동료를 되살립니다. 20초 후 시신 옆에서 일어납니다." + Paragraph
            + "되살릴 수 있는 횟수는 전투 난이도에 따라 다릅니다: Apocalypse와 Downfall은 2회, 매우 어려움은 4회, 그 아래는 무제한입니다." + Paragraph
            + "그 뒤의 일은 쓰러진 뒤 항목에서 설명합니다. 길드 상인은 두루마리를 항상, 던전 상인은 가끔 판매합니다.",
        "Avec le parchemin de résurrection, un personnage vivant ramène un personnage tombé. Il se relève à côté du corps au bout de 20 secondes." + Paragraph
            + "Le nombre de retours dépend de la difficulté de combat : 2 en Apocalypse et Downfall, 4 en Très difficile, illimité en dessous." + Paragraph
            + "La suite est décrite sous Après une chute. Le marchand de guilde vend toujours le parchemin, les marchands du donjon parfois.",
        "Mit der Wiederbelebungsrolle holt eine lebende Figur eine Gefallene zurück. Sie steht nach 20 Sekunden neben der Leiche auf." + Paragraph
            + "Wie oft eine Figur zurückkommen kann, hängt von der Kampfstufe ab: Apocalypse und Downfall zweimal, Sehr schwer viermal, darunter unbegrenzt." + Paragraph
            + "Was danach folgt, steht unter Nach dem Fallen. Der Gildenhändler führt die Rolle immer, Händler im Dungeon gelegentlich.",
        "Живой персонаж возвращает павшего свитком воскрешения. Тот встаёт рядом с телом через 20 секунд." + Paragraph
            + "Сколько раз персонаж может вернуться, зависит от сложности боя: 2 раза в Apocalypse и Downfall, 4 раза на «Очень сложно», ниже без ограничений." + Paragraph
            + "Что бывает после этого, описано в разделе «После падения». Гильдейский торговец продаёт свиток всегда, торговцы в подземелье иногда.",
        "活着的角色可以用复活卷轴救回倒下的角色。20 秒后，对方会在遗体旁站起来。" + Paragraph
            + "一个角色能被救回几次取决于战斗难度：Apocalypse 和 Downfall 为 2 次，非常困难为 4 次，更低难度不限次数。" + Paragraph
            + "之后的影响见“倒下之后”。公会商人总会出售卷轴，地下城商人偶尔出售。",
        "活著的角色可以用復活卷軸救回倒下的角色。20 秒後，對方會在遺體旁站起來。" + Paragraph
            + "一個角色能被救回幾次取決於戰鬥難度：Apocalypse 和 Downfall 為 2 次，非常困難為 4 次，更低難度不限次數。" + Paragraph
            + "之後的影響見「倒下之後」。公會商人總會出售卷軸，地下城商人偶爾出售。",
        "生きているキャラクターが蘇生の巻物で倒れた仲間を呼び戻します。20秒後に遺体のそばで立ち上がります。" + Paragraph
            + "呼び戻せる回数は戦闘難易度で決まります：Apocalypse と Downfall は2回、ベリーハードは4回、それより下は無制限です。" + Paragraph
            + "その後のことは「倒れた後」で説明しています。ギルド商人は巻物を常に、ダンジョン商人はときどき扱います。",
        "Con el pergamino de resurrección, un personaje vivo trae de vuelta a uno caído. Se levanta junto al cuerpo a los 20 segundos." + Paragraph
            + "Cuántas veces puede volver un personaje depende de la dificultad de combate: 2 veces en Apocalypse y Downfall, 4 en Muy difícil, sin límite por debajo." + Paragraph
            + "Lo que viene después se explica en Después de caer. El comerciante del gremio siempre vende el pergamino; los de la mazmorra, a veces.",
        "Com o pergaminho da ressurreição, um personagem vivo traz de volta um caído. Ele se levanta ao lado do corpo depois de 20 segundos." + Paragraph
            + "Quantas vezes um personagem pode voltar depende da dificuldade de combate: 2 vezes em Apocalypse e Downfall, 4 em Muito difícil, sem limite abaixo disso." + Paragraph
            + "O que vem depois está em Depois de cair. O mercador da guilda sempre vende o pergaminho; os da masmorra, às vezes.");

    private static readonly UniqueCandidateTranslations NoPauseTitle = ApocalypseText.RulesNoPause;

    private static readonly UniqueCandidateTranslations NoPauseText = new(
        "In combat Apocalypse the pause key does not stop the game. It slows time to half speed instead. Pressing it again ends the slow motion." + Paragraph
            + "The bar under the clock in the top left, below the time display and the buttons for pause, play and fast forward, shows how much slow motion is stored. Full, it lasts 5 seconds. While slow motion runs, it drains in real time. Afterwards it fills again, one second of slow motion every 4 seconds, so 20 seconds from empty to full. While the game itself is paused, for example by a window, it stays as it is." + Paragraph
            + "Slow motion starts from half a second stored. Below that the bar is grey, and a press only makes it blink red. When it is full again, it flashes once. The rule can be turned off in the Delvers menu.",
        "전투 Apocalypse에서는 일시정지 키가 게임을 멈추지 않습니다. 대신 시간을 절반 속도로 늦춥니다. 한 번 더 누르면 슬로모션이 끝납니다." + Paragraph
            + "왼쪽 위 시계 아래, 시간 표시와 일시정지, 재생, 빨리 감기 버튼 밑의 막대가 모아 둔 슬로모션의 양을 보여 줍니다. 가득 차면 5초 동안 쓸 수 있습니다. 슬로모션 중에는 실제 시간으로 줄어듭니다. 그 뒤 4초마다 슬로모션 1초씩 다시 차서, 빈 상태에서 가득 찰 때까지 20초가 걸립니다. 창이 열리는 등 게임 자체가 멈춰 있는 동안에는 그대로 있습니다." + Paragraph
            + "슬로모션은 0.5초 이상 모였을 때 시작할 수 있습니다. 그보다 적으면 막대가 회색이 되고, 눌러도 빨갛게 깜박이기만 합니다. 다시 가득 차면 한 번 반짝입니다. 이 규칙은 Delvers 메뉴에서 끌 수 있습니다.",
        "En Apocalypse de combat, la touche pause n'arrête pas le jeu. Elle ralentit plutôt le temps de moitié. Un second appui met fin au ralenti." + Paragraph
            + "La barre sous l'horloge en haut à gauche, sous l'affichage de l'heure et les boutons pause, lecture et avance rapide, montre le ralenti en réserve. Pleine, elle dure 5 secondes. Pendant le ralenti, elle se vide en temps réel. Ensuite elle se remplit de nouveau, une seconde de ralenti toutes les 4 secondes, donc 20 secondes de vide à plein. Quand le jeu s'arrête, par exemple à cause d'une fenêtre, elle reste telle quelle." + Paragraph
            + "Le ralenti démarre à partir de 0,5 seconde en réserve. En dessous, la barre est grise et un appui la fait seulement clignoter en rouge. Quand elle est de nouveau pleine, elle s'illumine une fois. La règle peut être désactivée dans le menu Delvers.",
        "Auf der Kampfstufe Apocalypse hält die Pausentaste das Spiel nicht an. Sie verlangsamt die Zeit stattdessen auf halbe Geschwindigkeit. Ein zweiter Druck beendet die Zeitlupe." + Paragraph
            + "Der Balken unter der Uhr oben links, unter der Zeitanzeige und den Knöpfen für Pause, Abspielen und Vorlauf, zeigt den Vorrat an Zeitlupe. Voll reicht er für 5 Sekunden. Während die Zeitlupe läuft, leert er sich in echter Zeit. Danach füllt er sich wieder, eine Sekunde Zeitlupe alle 4 Sekunden, also in 20 Sekunden von leer bis voll. Hält das Spiel selbst an, etwa bei einem Fenster, steht er still." + Paragraph
            + "Starten lässt sich die Zeitlupe ab einer halben Sekunde Vorrat. Darunter ist der Balken grau, und ein Druck lässt ihn nur rot aufblinken. Ist er wieder voll, leuchtet er einmal auf. Die Regel lässt sich im Menü Delvers abschalten.",
        "В боевом Apocalypse клавиша паузы не останавливает игру. Вместо этого она замедляет время вдвое. Повторное нажатие завершает замедление." + Paragraph
            + "Полоска под часами в левом верхнем углу, под временем и кнопками паузы, воспроизведения и ускорения, показывает запас замедления. Полной хватает на 5 секунд. Пока идёт замедление, она убывает в реальном времени. Потом снова наполняется, одна секунда замедления каждые 4 секунды, то есть 20 секунд от пустой до полной. Пока игра сама стоит на паузе, например при открытом окне, полоска не меняется." + Paragraph
            + "Замедление включается при запасе от 0,5 секунды. Меньше этого полоска серая, а нажатие лишь заставляет её мигнуть красным. Когда она снова полна, она один раз вспыхивает. Правило можно отключить в меню Delvers.",
        "在战斗 Apocalypse 中，暂停键不会停止游戏，而是将时间减慢至一半速度。再按一次即可结束慢动作。" + Paragraph
            + "左上角时钟下方，也就是时间显示和暂停、播放、快进按钮下面的条，显示储存的慢动作。充满时可持续 5 秒。慢动作期间，它按真实时间减少。之后会重新充能，每 4 秒恢复 1 秒慢动作，从空到满需要 20 秒。游戏本身暂停时，例如打开窗口时，它保持不变。" + Paragraph
            + "储存至少 0.5 秒时才能开始慢动作。低于这个值时条为灰色，按键只会让它闪一下红光。重新充满时，它会亮一下。此规则可在 Delvers 菜单中关闭。",
        "在戰鬥 Apocalypse 中，暫停鍵不會停止遊戲，而是將時間減慢至一半速度。再按一次即可結束慢動作。" + Paragraph
            + "左上角時鐘下方，也就是時間顯示和暫停、播放、快轉按鈕下面的條，顯示儲存的慢動作。充滿時可持續 5 秒。慢動作期間，它按真實時間減少。之後會重新充能，每 4 秒恢復 1 秒慢動作，從空到滿需要 20 秒。遊戲本身暫停時，例如開啟視窗時，它保持不變。" + Paragraph
            + "儲存至少 0.5 秒時才能開始慢動作。低於這個值時條為灰色，按鍵只會讓它閃一下紅光。重新充滿時，它會亮一下。此規則可在 Delvers 選單中關閉。",
        "戦闘 Apocalypse では一時停止キーでゲームは止まりません。代わりに時間が半分の速さになります。もう一度押すとスローが終わります。" + Paragraph
            + "左上の時計の下、時刻表示と一時停止、再生、早送りのボタンの下にあるバーが、ためてあるスローの量を示します。満タンで5秒続きます。スロー中は実時間で減ります。その後また回復し、4秒ごとにスロー1秒分、空から満タンまで20秒です。ウィンドウを開いたときなど、ゲーム自体が止まっている間は変わりません。" + Paragraph
            + "スローは0.5秒分以上たまっていれば始められます。それより少ないとバーは灰色になり、押しても赤く点滅するだけです。満タンに戻ると一度光ります。このルールは Delvers メニューでオフにできます。",
        "En Apocalypse de combate, la tecla de pausa no detiene el juego. En su lugar ralentiza el tiempo a la mitad. Una segunda pulsación termina la cámara lenta." + Paragraph
            + "La barra bajo el reloj de arriba a la izquierda, debajo de la hora y de los botones de pausa, reproducir y avance rápido, muestra cuánta cámara lenta hay guardada. Llena, dura 5 segundos. Mientras dura la cámara lenta, se vacía en tiempo real. Después se vuelve a llenar, un segundo de cámara lenta cada 4 segundos, así que 20 segundos de vacía a llena. Mientras el propio juego está en pausa, por ejemplo por una ventana, se queda como está." + Paragraph
            + "La cámara lenta empieza a partir de medio segundo guardado. Por debajo, la barra es gris y una pulsación solo la hace parpadear en rojo. Cuando vuelve a estar llena, destella una vez. La regla se puede desactivar en el menú Delvers.",
        "No Apocalypse de combate, a tecla de pausa não para o jogo. Em vez disso, deixa o tempo na metade da velocidade. Apertar de novo encerra a câmera lenta." + Paragraph
            + "A barra sob o relógio no canto superior esquerdo, abaixo da hora e dos botões de pausa, reproduzir e avançar, mostra quanta câmera lenta está guardada. Cheia, dura 5 segundos. Durante a câmera lenta, ela esvazia em tempo real. Depois volta a encher, um segundo de câmera lenta a cada 4 segundos, ou seja, 20 segundos de vazia a cheia. Enquanto o próprio jogo está pausado, por exemplo por uma janela, ela fica como está." + Paragraph
            + "A câmera lenta começa a partir de meio segundo guardado. Abaixo disso a barra fica cinza, e apertar só a faz piscar em vermelho. Quando fica cheia de novo, ela brilha uma vez. A regra pode ser desligada no menu Delvers.");

    private static readonly UniqueCandidateTranslations InstantTitle = ApocalypseText.RulesInstantCombat;

    private static readonly UniqueCandidateTranslations InstantText = new(
        "In combat Apocalypse enemies give no time to prepare and attack at once. Unaware enemies no longer take extra damage.",
        "전투 Apocalypse에서는 적이 준비할 시간을 주지 않고 즉시 공격합니다. 눈치채지 못한 적도 더 이상 추가 피해를 받지 않습니다.",
        "En Apocalypse de combat, les ennemis ne laissent aucun temps de préparation et attaquent aussitôt. Les ennemis inattentifs ne subissent plus de dégâts supplémentaires.",
        "Auf der Kampfstufe Apocalypse lassen Gegner keine Vorbereitungszeit und greifen sofort an. Unbemerkte Gegner nehmen keinen Zusatzschaden mehr.",
        "В боевом Apocalypse враги не дают времени на подготовку и атакуют сразу. Ничего не подозревающие враги больше не получают дополнительный урон.",
        "在战斗 Apocalypse 中，敌人不给准备时间，会立即进攻。未察觉的敌人不再受到额外伤害。",
        "在戰鬥 Apocalypse 中，敵人不給準備時間，會立即進攻。未察覺的敵人不再受到額外傷害。",
        "戦闘 Apocalypse では、敵は準備の時間を与えず即座に攻撃してきます。気づいていない敵も追加ダメージを受けなくなります。",
        "En Apocalypse de combate, los enemigos no dan tiempo a prepararse y atacan de inmediato. Los enemigos desprevenidos ya no reciben daño adicional.",
        "No Apocalypse de combate, os inimigos não dão tempo para preparação e atacam na hora. Inimigos desatentos não sofrem mais dano extra.");

    private static readonly UniqueCandidateTranslations AbilitiesTitle = ApocalypseText.RulesEnemyAbilities;

    private static readonly UniqueCandidateTranslations AbilitiesText = new(
        "In combat Apocalypse many enemies cause bleeding with their hits, above all animals with teeth, claws or spikes and armed enemies such as goblins, knights and the Forest Bandits. Golems, slimes and acid insects cause no bleeding." + Paragraph
            + "Some casters such as the goblin shamans silence. A silenced character only uses its basic attack for 5 seconds." + Paragraph
            + "Spore enemies such as the spore slimes put characters to sleep. A sleeping character does nothing for 8 seconds, damage wakes it at once." + Paragraph
            + "Every bleeding stack turns into blood loss that bleeds on until a bandage helps. See Bleeding and Bandages and first aid.",
        "전투 Apocalypse에서는 많은 적이 공격으로 출혈을 일으킵니다. 특히 이빨, 발톱, 가시가 있는 동물과 고블린, 기사, 숲 도적단 같은 무장한 적입니다. 골렘, 슬라임, 산성 곤충은 출혈을 일으키지 않습니다." + Paragraph
            + "고블린 주술사 같은 일부 시전자는 침묵을 겁니다. 침묵한 캐릭터는 5초 동안 기본 공격만 사용합니다." + Paragraph
            + "포자 슬라임 같은 포자 적은 캐릭터를 잠들게 합니다. 잠든 캐릭터는 8초 동안 아무것도 하지 않으며, 피해를 입으면 바로 깨어납니다." + Paragraph
            + "출혈 중첩은 모두 출혈량이 되어 붕대로 치료할 때까지 계속 흘러나갑니다. 자세한 내용은 출혈, 붕대와 응급처치를 보세요.",
        "En Apocalypse de combat, beaucoup d'ennemis font saigner avec leurs coups, surtout les animaux à dents, griffes ou piquants et les ennemis armés comme les gobelins, les chevaliers et les Bandits des bois. Les golems, les slimes et les insectes acides ne font pas saigner." + Paragraph
            + "Certains lanceurs de sorts comme les chamanes gobelins réduisent au silence. Un personnage réduit au silence n'utilise que son attaque de base pendant 5 secondes." + Paragraph
            + "Les ennemis à spores comme les slimes à spores endorment. Un personnage endormi ne fait rien pendant 8 secondes, les dégâts le réveillent aussitôt." + Paragraph
            + "Chaque cumul de saignement devient une perte de sang qui continue de couler jusqu'à ce qu'un bandage aide. Voir Saignement et Bandages et premiers secours.",
        "Auf der Kampfstufe Apocalypse lassen viele Gegner mit ihren Treffern bluten, vor allem Tiere mit Zähnen, Klauen oder Stacheln und bewaffnete Gegner wie Goblins, Ritter und die Waldbanditen. Golems, Schleime und Säureinsekten lassen nicht bluten." + Paragraph
            + "Manche Zauberer wie die Goblinschamanen lassen verstummen. Eine verstummte Figur nutzt 5 Sekunden lang nur ihren Grundangriff." + Paragraph
            + "Sporengegner wie die Sporenschleime schläfern ein. Eine schlafende Figur tut 8 Sekunden lang nichts, Schaden weckt sie sofort." + Paragraph
            + "Jeder Blutungsstapel wird zu Blutverlust, der nachblutet, bis ein Verband hilft. Mehr dazu unter Blutung und unter Verbände und Erste Hilfe.",
        "В боевом Apocalypse многие враги вызывают кровотечение ударами, прежде всего звери с зубами, когтями или шипами и вооружённые враги вроде гоблинов, рыцарей и Лесных бандитов. Големы, слизни и кислотные насекомые кровотечения не вызывают." + Paragraph
            + "Некоторые заклинатели, например шаманы гоблинов, накладывают безмолвие. Персонаж под безмолвием 5 секунд использует только базовую атаку." + Paragraph
            + "Споровые враги, например споровые слизни, усыпляют. Спящий персонаж 8 секунд ничего не делает, урон сразу его будит." + Paragraph
            + "Каждый стак кровотечения превращается в потерю крови, которая вытекает, пока не поможет бинт. Подробнее в разделах «Кровотечение» и «Бинты и первая помощь».",
        "在战斗 Apocalypse 中，许多敌人的攻击会造成流血，尤其是有牙齿、爪子或尖刺的动物，以及哥布林、骑士和森林强盗团这类持有武器的敌人。魔像、史莱姆和酸性昆虫不会造成流血。" + Paragraph
            + "哥布林萨满等部分施法者会让角色沉默。沉默的角色在 5 秒内只能使用普通攻击。" + Paragraph
            + "孢子史莱姆等孢子敌人会让角色陷入睡眠。睡着的角色 8 秒内什么也不做，受到伤害会立刻醒来。" + Paragraph
            + "每层流血都会变成失血量，持续流失，直到用绷带处理。详见“流血”和“绷带与急救”。",
        "在戰鬥 Apocalypse 中，許多敵人的攻擊會造成流血，尤其是有牙齒、爪子或尖刺的動物，以及哥布林、騎士和森林強盜團這類持有武器的敵人。魔像、史萊姆和酸性昆蟲不會造成流血。" + Paragraph
            + "哥布林薩滿等部分施法者會讓角色沉默。沉默的角色在 5 秒內只能使用普通攻擊。" + Paragraph
            + "孢子史萊姆等孢子敵人會讓角色陷入睡眠。睡著的角色 8 秒內什麼也不做，受到傷害會立刻醒來。" + Paragraph
            + "每層流血都會變成失血量，持續流失，直到用繃帶處理。詳見「流血」和「繃帶與急救」。",
        "戦闘 Apocalypse では、多くの敵の攻撃で出血します。特に牙や爪、とげのある動物と、ゴブリン、騎士、森の盗賊団のような武装した敵です。ゴーレム、スライム、酸の虫は出血させません。" + Paragraph
            + "ゴブリンシャーマンなど一部の術者はキャラクターを沈黙させます。沈黙したキャラクターは5秒間、通常攻撃しか使いません。" + Paragraph
            + "胞子スライムなどの胞子の敵はキャラクターを眠らせます。眠ったキャラクターは8秒間何もせず、ダメージを受けるとすぐに目覚めます。" + Paragraph
            + "出血スタックはすべて失血量になり、包帯で手当てするまで流れ続けます。詳しくは「出血」と「包帯と応急手当」を見てください。",
        "En Apocalypse de combate, muchos enemigos causan hemorragias con sus golpes, sobre todo los animales con dientes, garras o púas y los enemigos armados como los goblins, los caballeros y los Bandidos del bosque. Los gólems, los slimes y los insectos ácidos no causan hemorragias." + Paragraph
            + "Algunos hechiceros, como los chamanes goblin, silencian. Un personaje silenciado solo usa su ataque básico durante 5 segundos." + Paragraph
            + "Los enemigos de esporas, como los slimes de esporas, duermen a los personajes. Un personaje dormido no hace nada durante 8 segundos; el daño lo despierta al instante." + Paragraph
            + "Cada acumulación de sangrado se convierte en pérdida de sangre que sigue fluyendo hasta que una venda ayude. Ver Hemorragias y Vendas y primeros auxilios.",
        "No Apocalypse de combate, muitos inimigos causam sangramento com seus golpes, sobretudo animais com dentes, garras ou espinhos e inimigos armados como goblins, cavaleiros e os Bandidos da floresta. Golens, slimes e insetos ácidos não causam sangramento." + Paragraph
            + "Alguns conjuradores, como os xamãs goblins, silenciam. Um personagem silenciado só usa o ataque básico por 5 segundos." + Paragraph
            + "Inimigos de esporos, como os slimes de esporos, fazem os personagens dormir. Um personagem adormecido não faz nada por 8 segundos; o dano o acorda na hora." + Paragraph
            + "Cada acúmulo de sangramento vira perda de sangue que continua escorrendo até que uma bandagem ajude. Veja Sangramento e Bandagens e primeiros socorros.");

    private static readonly UniqueCandidateTranslations GearTitle = new(
        "Gear and stripping", "장비와 벗기기", "Équipement et dépouillement", "Ausrüstung und Ausziehen", "Снаряжение и снятие",
        "装备与卸下", "裝備與卸下", "装備と剥ぎ取り", "Equipo y despojo", "Equipamento e remoção");

    private static readonly UniqueCandidateTranslations GearText = new(
        "When a party member dies, the body keeps its equipped gear. Before a dressed fallen member is buried you are asked first, because burial loses the gear." + Paragraph
            + "Holding Alt while right clicking a body that still wears something offers \"Strip Equipment\". The body then drops everything it wears.",
        "파티원이 죽으면 시신이 착용한 장비를 그대로 유지합니다. 장비를 입은 시신을 매장하기 전에는 먼저 확인을 받습니다. 매장하면 장비가 사라지기 때문입니다." + Paragraph
            + "Alt를 누른 채 아직 무언가를 입고 있는 시신을 우클릭하면 \"장비 벗기기\"가 나옵니다. 그러면 시신이 입고 있던 것을 모두 떨어뜨립니다.",
        "Quand un membre du groupe meurt, son corps garde l'équipement porté. Avant d'enterrer un membre tombé encore équipé, une confirmation est demandée, car l'enterrement fait perdre l'équipement." + Paragraph
            + "Un clic droit avec Alt enfoncée sur un corps qui porte encore quelque chose propose « Retirer l'équipement ». Le corps lâche alors tout ce qu'il porte.",
        "Stirbt eine Figur der Gruppe, behält ihre Leiche die angelegte Ausrüstung. Vor dem Begraben einer angezogenen Gefallenen kommt eine Rückfrage, denn beim Begraben geht die Ausrüstung verloren." + Paragraph
            + "Ein Rechtsklick bei gedrückter Taste Alt auf eine Leiche, die noch etwas trägt, bietet „Ausrüstung ausziehen“. Die Leiche lässt dann alles Angezogene fallen.",
        "Когда член отряда погибает, тело сохраняет надетое снаряжение. Перед погребением одетого павшего появляется вопрос, ведь при погребении снаряжение теряется." + Paragraph
            + "Правый щелчок с зажатой клавишей Alt по телу, на котором ещё надеты вещи, предлагает «Снять снаряжение». Тогда тело роняет всё надетое.",
        "队员死亡后，遗体会保留身上的装备。埋葬仍穿着装备的倒下者之前会先询问，因为埋葬会让装备丢失。" + Paragraph
            + "按住 Alt 右键点击仍穿着东西的遗体，会出现“卸下装备”。遗体随后会掉落身上的所有装备。",
        "隊員死亡後，遺體會保留身上的裝備。埋葬仍穿著裝備的倒下者之前會先詢問，因為埋葬會讓裝備遺失。" + Paragraph
            + "按住 Alt 右鍵點擊仍穿著東西的遺體，會出現「卸下裝備」。遺體隨後會掉落身上的所有裝備。",
        "パーティーの仲間が死ぬと、遺体は装備したままになります。装備したままの仲間を埋葬する前には確認が出ます。埋葬すると装備が失われるからです。" + Paragraph
            + "Altを押しながら、まだ何かを身につけている遺体を右クリックすると「装備を剥ぐ」が出ます。遺体は身につけているものをすべて落とします。",
        "Cuando muere un miembro del grupo, el cuerpo conserva el equipo puesto. Antes de enterrar a un caído equipado se pide confirmación, porque al enterrarlo se pierde el equipo." + Paragraph
            + "Al hacer clic derecho con Alt pulsada en un cuerpo que aún lleva algo aparece «Quitar el equipo». El cuerpo suelta entonces todo lo que lleva puesto.",
        "Quando um membro do grupo morre, o corpo mantém o equipamento vestido. Antes de enterrar um caído equipado, é feita uma confirmação, porque enterrar faz perder o equipamento." + Paragraph
            + "Clicar com o botão direito segurando Alt em um corpo que ainda veste algo oferece \"Remover equipamento\". O corpo então larga tudo o que veste.");

    // The tactical formation (the button in the option row, the window, the dungeon). German and English name the game's three
    // option buttons as the game does; the other languages take them from the game's text table (TEXTKEY_SummaryOptionUI_*_NAME).
    private static readonly UniqueCandidateTranslations FormationTitle = new(
        "Formation", "대형", "Formation", "Formation", "Строй", "队形", "隊形", "隊形", "Formación", "Formação");

    private static readonly UniqueCandidateTranslations FormationText = new(
        "The tactical formation is still experimental." + Paragraph
            + "The formation lines your group up around the group leader. Select a member of your group. The formation button then appears at the bottom, next to Auto Skill, Hold Position and Prioritize Sleep. Left click turns it on or off for that member, right click opens the window." + Paragraph
            + "The top member of the list is the group leader. Drag a member to the top to make them the leader. Drag members to their places in the grid, the top is the front. Use the arrows to switch between 8 formations, the one shown is used." + Paragraph
            + "Each member has 5 slots for skills. They prefer the first one that is ready. This only applies to members with Auto Skill." + Paragraph
            + "In a dungeon the group follows the leader in this formation. In combat they line up and attack whatever is in reach. Afterwards each member returns to their place." + Paragraph
            + "In the village, \"Test formation\" shows the formation for 20 seconds.",
        "전술 대형은 아직 실험적인 기능입니다." + Paragraph
            + "대형으로 그룹 리더를 중심으로 그룹을 배치합니다. 그룹의 대원을 선택하면 아래쪽 자동 스킬, 명령 대기, 휴식 우선 옆에 대형 버튼이 나타납니다. 왼쪽 클릭으로 그 대원의 대형을 켜거나 끄고, 오른쪽 클릭으로 창을 엽니다." + Paragraph
            + "목록 맨 위의 대원이 그룹 리더입니다. 대원을 맨 위로 끌어 리더로 만드세요. 격자에서 대원을 각자의 자리로 끌어 놓으세요. 위쪽이 앞쪽입니다. 화살표로 8개의 대형을 오갈 수 있으며, 표시된 대형이 적용됩니다." + Paragraph
            + "각 대원은 스킬 자리 5개를 가집니다. 그중 준비된 첫 번째 스킬을 우선 사용합니다. 자동 스킬이 켜진 대원에게만 적용됩니다." + Paragraph
            + "던전에서 그룹은 이 대형으로 리더를 따라갑니다. 전투에서는 자리를 잡고 사거리 안의 적을 공격합니다. 그 뒤 각 대원이 자기 자리로 돌아갑니다." + Paragraph
            + "마을에서는 \"대형 시험\"이 20초 동안 대형을 보여 줍니다.",
        "La formation tactique est encore expérimentale." + Paragraph
            + "La formation place le groupe autour du chef de groupe. Sélectionnez un membre du groupe. Le bouton de la formation apparaît alors en bas, à côté de Compétence auto, Rester sur place et Prioriser le sommeil. Clic gauche : l'active ou la désactive pour ce membre. Clic droit : ouvre la fenêtre." + Paragraph
            + "Le membre en haut de la liste est le chef de groupe. Faites glisser un membre en haut pour en faire le chef. Faites glisser les membres à leur place dans la grille, le haut est l'avant. Les flèches passent d'une formation à l'autre parmi 8, celle affichée s'applique." + Paragraph
            + "Chaque membre a 5 emplacements de compétences. Il utilise en priorité la première qui est prête. Cela ne vaut que pour les membres avec Compétence auto." + Paragraph
            + "Dans un donjon, le groupe suit le chef dans cette formation. En combat, il se met en place et attaque ce qui est à portée. Ensuite, chaque membre retourne à sa place." + Paragraph
            + "Au village, « Tester la formation » montre la formation pendant 20 secondes.",
        "Die taktische Formation ist noch experimentell." + Paragraph
            + "Mit der Formation stellst du deine Gruppe um den Gruppenanführer auf. Wähle eine Figur deiner Gruppe. Unten neben Autoskill, Position halten und Schlaf priorisieren erscheint dann der Knopf für die Formation. Linksklick schaltet sie für diese Figur an oder aus, Rechtsklick öffnet das Fenster." + Paragraph
            + "Die oberste Figur der Liste ist der Gruppenanführer. Ziehe eine Figur nach oben, um sie zum Anführer zu machen. Ziehe die Figuren im Raster an ihre Plätze, oben ist vorne. Mit den Pfeilen wechselst du zwischen 8 Formationen, die angezeigte gilt." + Paragraph
            + "Jede Figur hat 5 Plätze für Fähigkeiten. Die erste bereite davon setzt sie bevorzugt ein. Das gilt nur für Figuren mit Autoskill." + Paragraph
            + "Im Dungeon folgt die Gruppe dem Anführer in dieser Aufstellung. Im Kampf stellt sie sich auf und greift an, was in Reichweite ist. Danach kehrt jede Figur an ihren Platz zurück." + Paragraph
            + "Im Dorf zeigt „Formation testen“ die Aufstellung für 20 Sekunden.",
        "Тактический строй пока экспериментальный." + Paragraph
            + "Строй расставляет отряд вокруг лидера группы. Выберите члена отряда. Внизу, рядом с «Автонавык», «Удерживать позицию» и «Отдавать приоритет сну», появится кнопка строя. Левый щелчок включает или выключает строй для этого члена отряда, правый щелчок открывает окно." + Paragraph
            + "Лидер группы стоит первым в списке. Перетащите члена отряда наверх, чтобы сделать его лидером. Расставьте членов отряда по местам в сетке, верх означает перед. Стрелками можно переключаться между 8 строями, действует показанный." + Paragraph
            + "У каждого члена отряда 5 мест для навыков. Он предпочитает первый готовый из них. Это действует только для тех, у кого включён «Автонавык»." + Paragraph
            + "В подземелье отряд следует за лидером в этом строю. В бою он занимает места и атакует всё, что в досягаемости. После боя каждый возвращается на своё место." + Paragraph
            + "В деревне «Проверить строй» показывает расстановку 20 секунд.",
        "战术队形目前仍是实验性功能。" + Paragraph
            + "队形让队伍围绕队长站位。选择队伍中的一名成员，下方的自动技能、原地停留和优先睡眠旁就会出现队形按钮。左键为该成员开启或关闭队形，右键打开窗口。" + Paragraph
            + "列表最上方的成员就是队长。把成员拖到最上方即可让其成为队长。在网格中把成员拖到各自的位置，上方为前方。用箭头可在 8 个队形之间切换，显示的队形即为生效的队形。" + Paragraph
            + "每名成员有 5 个技能位置，会优先使用其中第一个就绪的技能。这只适用于开启了自动技能的成员。" + Paragraph
            + "在地下城中，队伍按此队形跟随队长。战斗时队伍会站好位置，攻击射程内的敌人。战斗结束后，每名成员回到自己的位置。" + Paragraph
            + "在村庄里，“测试队形”会展示队形 20 秒。",
        "戰術隊形目前仍是實驗性功能。" + Paragraph
            + "隊形讓隊伍圍繞隊長站位。選擇隊伍中的一名成員，下方的自動技能、保持位置和優先睡眠旁就會出現隊形按鈕。左鍵為該成員開啟或關閉隊形，右鍵開啟視窗。" + Paragraph
            + "清單最上方的成員就是隊長。把成員拖到最上方即可讓其成為隊長。在網格中把成員拖到各自的位置，上方為前方。用箭頭可在 8 個隊形之間切換，顯示的隊形即為生效的隊形。" + Paragraph
            + "每名成員有 5 個技能位置，會優先使用其中第一個就緒的技能。這只適用於開啟了自動技能的成員。" + Paragraph
            + "在地下城中，隊伍按此隊形跟隨隊長。戰鬥時隊伍會站好位置，攻擊射程內的敵人。戰鬥結束後，每名成員回到自己的位置。" + Paragraph
            + "在村莊裡，「測試隊形」會展示隊形 20 秒。",
        "戦術隊形はまだ試験的な機能です。" + Paragraph
            + "隊形では、グループリーダーを中心にグループを配置します。グループの仲間を選ぶと、下のオートスキル、その場で待機、睡眠優先の横に隊形のボタンが出ます。左クリックでその仲間の隊形をオンまたはオフにし、右クリックでウィンドウを開きます。" + Paragraph
            + "リストの一番上の仲間がグループリーダーです。仲間を一番上へドラッグするとリーダーになります。グリッドで仲間をそれぞれの位置へドラッグします。上が前です。矢印で 8 つの隊形を切り替えられ、表示中の隊形が使われます。" + Paragraph
            + "仲間ごとにスキルの枠が 5 つあります。その中で最初に準備できたスキルを優先して使います。オートスキルがオンの仲間だけに適用されます。" + Paragraph
            + "ダンジョンではグループがこの隊形でリーダーについて行きます。戦闘では位置につき、射程内の敵を攻撃します。その後、各仲間は自分の位置に戻ります。" + Paragraph
            + "村では「隊形を試す」で隊形を 20 秒間確認できます。",
        "La formación táctica todavía es experimental." + Paragraph
            + "La formación coloca al grupo alrededor del líder del grupo. Selecciona a un miembro del grupo. El botón de la formación aparece abajo, junto a Habilidad automática, Mantener posición y Priorizar el sueño. Clic izquierdo: la activa o la desactiva para ese miembro. Clic derecho: abre la ventana." + Paragraph
            + "El miembro de arriba de la lista es el líder del grupo. Arrastra a un miembro arriba para hacerlo líder. Arrastra a los miembros a sus puestos en la cuadrícula; arriba es el frente. Con las flechas cambias entre 8 formaciones; se usa la que se muestra." + Paragraph
            + "Cada miembro tiene 5 huecos para habilidades. Usa antes la primera que esté lista. Solo vale para miembros con Habilidad automática." + Paragraph
            + "En una mazmorra el grupo sigue al líder con esta formación. En combate se coloca y ataca lo que está a su alcance. Después, cada miembro vuelve a su puesto." + Paragraph
            + "En el pueblo, «Probar formación» muestra la formación durante 20 segundos.",
        "A formação tática ainda é experimental." + Paragraph
            + "A formação posiciona o grupo em volta do líder do grupo. Selecione um membro do grupo. O botão da formação aparece embaixo, ao lado de Habilidade automática, Manter posição e Priorizar sono. Clique esquerdo: ativa ou desativa para esse membro. Clique direito: abre a janela." + Paragraph
            + "O membro no topo da lista é o líder do grupo. Arraste um membro para o topo para que ele seja o líder. Arraste os membros para seus lugares na grade; o topo é a frente. Use as setas para alternar entre 8 formações; vale a que está sendo mostrada." + Paragraph
            + "Cada membro tem 5 espaços para habilidades. Ele usa primeiro a primeira que estiver pronta. Isso vale apenas para membros com Habilidade automática." + Paragraph
            + "Na masmorra, o grupo segue o líder nesta formação. Em combate, ele toma posição e ataca o que estiver ao alcance. Depois, cada membro volta ao seu lugar." + Paragraph
            + "Na vila, \"Testar formação\" mostra a formação por 20 segundos.");

    private static readonly UniqueCandidateTranslations AfterFallTitle = new(
        "After a fall", "쓰러진 뒤", "Après une chute", "Nach dem Fallen", "После падения",
        "倒下之后", "倒下之後", "倒れた後", "Después de caer", "Depois de cair");

    // What a rescue from near death and a revival leave behind: the game's after effect with Core's 65 % cap,
    // the revival sickness and the dungeon panic (ReviveRules, ReviveSickness, ReviveEffects; every combat difficulty).
    private static readonly UniqueCandidateTranslations AfterFallText = new(
        "Whoever falls lies near death. If someone rescues them in time, they get back up but carry the game's Near Death Aftereffect. It lasts about 13 hours of game time. While it runs, mood drops by 5, and health and energy start at 65% and rise evenly to full when the aftereffect ends. Whoever falls again during this time no longer goes near death but dies at once. The same happens when hunger drops to zero. A rescue from near death does not count as a revival." + Paragraph
            + "Whoever dies and comes back with the Scroll of Revival gets the Revival Sickness. Health and energy start at 10% and rise evenly to full when the sickness ends. While it runs, hit chance drops by 20% and dodge chance by 10%. On Apocalypse the first sickness lasts two weeks, on Downfall and Very Hard one week, below that half a week. With every further revival of the same character the duration doubles, up to four weeks at most." + Paragraph
            + "On top of that comes the Dungeon Panic: mood drops by 25. The panic applies only in the dungeon and only while the sickness runs." + Paragraph
            + "All of this applies on every combat difficulty. The combat difficulty only decides how long the sickness lasts and how often a character can come back.",
        "쓰러진 캐릭터는 빈사 상태가 됩니다. 누군가 제때 구하면 다시 일어나지만, 게임의 죽음 임박 후유증을 얻습니다. 후유증은 게임 시간으로 약 13시간 지속됩니다. 지속되는 동안 기분이 5 낮아지고, 체력과 에너지는 65%에서 시작해 후유증이 끝날 때까지 고르게 최대치로 올라갑니다. 이 기간에 다시 쓰러지면 빈사 상태를 거치지 않고 즉시 사망합니다. 배고픔이 0이 되어도 마찬가지입니다. 빈사 상태에서의 구조는 부활 횟수에 포함되지 않습니다." + Paragraph
            + "죽은 뒤 부활의 두루마리로 돌아온 캐릭터는 부활 후유증을 얻습니다. 체력과 에너지는 10%에서 시작해 후유증이 끝날 때까지 고르게 최대치로 올라갑니다. 지속되는 동안 명중률이 20%, 회피율이 10% 낮아집니다. Apocalypse에서는 첫 후유증이 2주, Downfall과 매우 어려움에서는 1주, 그 아래에서는 반 주 동안 지속됩니다. 같은 캐릭터를 다시 부활시킬 때마다 지속 시간이 두 배가 되며, 최대 4주입니다." + Paragraph
            + "여기에 던전 공황이 더해집니다: 기분이 25 낮아집니다. 공황은 던전 안에서만, 그리고 후유증이 지속되는 동안에만 적용됩니다." + Paragraph
            + "이 모든 것은 모든 전투 난이도에서 적용됩니다. 전투 난이도에 따라 달라지는 것은 후유증의 지속 시간과 캐릭터가 돌아올 수 있는 횟수뿐입니다.",
        "Un personnage qui tombe se retrouve au seuil de la mort. Si quelqu'un le sauve à temps, il se relève, mais porte l'effet du jeu Séquelle d'un état proche de la mort. Il dure environ 13 heures de jeu. Tant qu'il dure, l'humeur baisse de 5, et la vie et l'énergie commencent à 65 % et remontent régulièrement jusqu'au maximum à la fin de la séquelle. S'il tombe à nouveau pendant ce temps, il ne passe plus par le seuil de la mort et meurt aussitôt. Il en va de même si la faim tombe à zéro. Un sauvetage au seuil de la mort ne compte pas parmi les résurrections." + Paragraph
            + "Un personnage qui meurt et revient grâce au parchemin de résurrection contracte le Mal de la résurrection. La vie et l'énergie commencent à 10 % et remontent régulièrement jusqu'au maximum à la fin du mal. Tant qu'il dure, la probabilité de toucher baisse de 20 % et la probabilité d'esquive de 10 %. En Apocalypse, le premier mal dure deux semaines, en Downfall et Très difficile une semaine, en dessous la moitié d'une semaine. À chaque nouvelle résurrection du même personnage, la durée double, jusqu'à quatre semaines au plus." + Paragraph
            + "S'y ajoute la Panique du donjon : l'humeur baisse de 25. La panique ne s'applique que dans le donjon et seulement tant que le mal dure." + Paragraph
            + "Tout cela vaut pour chaque difficulté de combat. La difficulté de combat ne décide que de la durée du mal et du nombre de retours possibles d'un personnage.",
        "Wer fällt, liegt in der Todesnähe. Rettet ihn jemand rechtzeitig, steht er wieder auf, trägt aber die Nahtod Nachwirkung des Spiels. Sie hält etwa 13 Stunden Spielzeit. Solange sie läuft, sinkt die Stimmung um 5, und Leben und Energie beginnen bei 65 % und steigen gleichmäßig bis zum vollen Wert, wenn die Nachwirkung endet. Wer in dieser Zeit noch einmal fällt, kommt nicht mehr in die Todesnähe, sondern stirbt sofort. Dasselbe gilt, wenn der Hunger auf null sinkt. Ein Down zählt nicht zu den Wiederbelebungen." + Paragraph
            + "Wer stirbt und mit der Wiederbelebungsrolle zurückkommt, bekommt die Wiederbelebungskrankheit. Leben und Energie beginnen bei 10 % und steigen gleichmäßig bis zum vollen Wert, wenn die Krankheit endet. Solange sie läuft, sinkt die Trefferchance um 20 % und das Ausweichen um 10 %. Auf Apocalypse dauert die erste Krankheit zwei Wochen, auf Downfall und Sehr schwer eine Woche, darunter eine halbe Woche. Mit jeder weiteren Wiederbelebung derselben Figur verdoppelt sich die Dauer, höchstens bis vier Wochen." + Paragraph
            + "Dazu kommt die Dungeonpanik: Die Stimmung sinkt um 25. Die Panik gilt nur im Dungeon und nur, solange die Krankheit läuft." + Paragraph
            + "All das gilt auf jeder Kampfstufe. Nach der Kampfstufe richten sich nur die Dauer der Krankheit und wie oft eine Figur zurückkommen kann.",
        "Упавший персонаж оказывается на пороге смерти. Если его вовремя спасут, он встаёт, но получает эффект игры «Последствие близкой смерти». Он длится около 13 часов игрового времени. Пока он действует, настроение снижается на 5, а здоровье и энергия начинаются с 65% и равномерно растут до полного значения к концу эффекта. Кто упадёт снова за это время, уже не окажется на пороге смерти, а сразу умрёт. То же самое происходит, если сытость падает до нуля. Спасение с порога смерти не считается воскрешением." + Paragraph
            + "Кто умер и вернулся со свитком воскрешения, получает «Болезнь воскрешения». Здоровье и энергия начинаются с 10% и равномерно растут до полного значения к концу болезни. Пока она длится, шанс попадания снижается на 20%, а шанс уклонения на 10%. В Apocalypse первая болезнь длится две недели, в Downfall и на «Очень сложно» одну неделю, ниже полнедели. С каждым следующим воскрешением того же персонажа длительность удваивается, но не больше чем до четырёх недель." + Paragraph
            + "К этому добавляется «Паника в подземелье»: настроение снижается на 25. Паника действует только в подземелье и только пока длится болезнь." + Paragraph
            + "Всё это действует на любой сложности боя. От сложности боя зависят только длительность болезни и то, сколько раз персонаж может вернуться.",
        "倒下的角色会陷入濒死。如果有人及时救援，他会重新站起来，但会带上游戏的濒死后遗症。它持续约 13 小时游戏时间。在此期间心情降低 5，生命和精力从 65% 开始，并在后遗症结束时均匀恢复到满值。在此期间再次倒下的角色不会再陷入濒死，而是立即死亡。饥饿值降到零时也是如此。濒死救援不计入复活次数。" + Paragraph
            + "死亡后用复活卷轴回来的角色会得到复活虚弱。生命和精力从 10% 开始，并在虚弱结束时均匀恢复到满值。在此期间命中几率降低 20%，闪避几率降低 10%。在 Apocalypse 中，第一次虚弱持续两周；在 Downfall 和非常困难中持续一周；更低难度为半周。同一角色每多复活一次，持续时间翻倍，最多四周。" + Paragraph
            + "此外还会出现地城恐慌：心情降低 25。恐慌只在地下城中、且只在虚弱持续期间生效。" + Paragraph
            + "以上内容适用于所有战斗难度。战斗难度只决定虚弱的持续时间和角色能被救回的次数。",
        "倒下的角色會陷入瀕死。如果有人及時救援，他會重新站起來，但會帶上遊戲的瀕死後遺症。它持續約 13 小時遊戲時間。在此期間心情降低 5，生命和精力從 65% 開始，並在後遺症結束時均勻恢復到滿值。在此期間再次倒下的角色不會再陷入瀕死，而是立即死亡。飽食度降到零時也是如此。瀕死救援不計入復活次數。" + Paragraph
            + "死亡後用復活卷軸回來的角色會得到復活虛弱。生命和精力從 10% 開始，並在虛弱結束時均勻恢復到滿值。在此期間命中機率降低 20%，閃避機率降低 10%。在 Apocalypse 中，第一次虛弱持續兩週；在 Downfall 和非常困難中持續一週；更低難度為半週。同一角色每多復活一次，持續時間翻倍，最多四週。" + Paragraph
            + "此外還會出現地城恐慌：心情降低 25。恐慌只在地下城中、且只在虛弱持續期間生效。" + Paragraph
            + "以上內容適用於所有戰鬥難度。戰鬥難度只決定虛弱的持續時間和角色能被救回的次數。",
        "倒れたキャラクターは瀕死になります。誰かが間に合って救助すると立ち上がりますが、ゲームの瀕死の後遺症を負います。後遺症はゲーム内時間で約13時間続きます。その間、気分が5下がり、体力と気力は65%から始まって、後遺症が終わるまでに均等に最大値まで戻ります。この間にもう一度倒れると、瀕死を経ずにすぐ死亡します。満腹度が0になった場合も同じです。瀕死からの救助は蘇生の回数に含まれません。" + Paragraph
            + "死亡して蘇生の巻物で戻ったキャラクターは蘇生の後遺症を負います。体力と気力は10%から始まって、後遺症が終わるまでに均等に最大値まで戻ります。その間、命中率が20%、回避率が10%下がります。Apocalypse では最初の後遺症が2週間、Downfall とベリーハードでは1週間、それより下では半週間続きます。同じキャラクターを蘇生するたびに期間は倍になり、最長で4週間です。" + Paragraph
            + "さらにダンジョンパニックが加わります：気分が25下がります。パニックはダンジョンの中だけで、後遺症が続いている間だけ有効です。" + Paragraph
            + "これらはすべての戦闘難易度で適用されます。戦闘難易度で変わるのは、後遺症の期間とキャラクターが戻れる回数だけです。",
        "Quien cae queda al borde de la muerte. Si alguien lo rescata a tiempo, se levanta, pero lleva el efecto del juego Secuela de muerte inminente. Dura unas 13 horas de juego. Mientras dura, el ánimo baja 5, y la vida y la energía empiezan en el 65 % y suben de forma uniforme hasta el máximo cuando la secuela termina. Quien vuelve a caer en ese tiempo ya no queda al borde de la muerte, sino que muere en el acto. Lo mismo ocurre si el hambre llega a cero. Un rescate al borde de la muerte no cuenta como resurrección." + Paragraph
            + "Quien muere y vuelve con el pergamino de resurrección contrae el Mal de la resurrección. La vida y la energía empiezan en el 10 % y suben de forma uniforme hasta el máximo cuando el mal termina. Mientras dura, la probabilidad de golpe baja un 20 % y la de esquiva un 10 %. En Apocalypse el primer mal dura dos semanas, en Downfall y Muy difícil una semana, por debajo media semana. Con cada nueva resurrección del mismo personaje la duración se duplica, hasta cuatro semanas como máximo." + Paragraph
            + "A eso se suma el Pánico en la mazmorra: el ánimo baja 25. El pánico solo rige en la mazmorra y solo mientras dura el mal." + Paragraph
            + "Todo esto vale en cualquier dificultad de combate. La dificultad de combate solo decide cuánto dura el mal y cuántas veces puede volver un personaje.",
        "Quem cai fica à beira da morte. Se alguém o resgatar a tempo, ele se levanta, mas carrega o Efeito Pós À Beira da Morte do jogo. Ele dura cerca de 13 horas de jogo. Enquanto dura, o humor cai 5, e vida e energia começam em 65% e sobem por igual até o máximo quando o efeito termina. Quem cair de novo nesse tempo não fica mais à beira da morte, mas morre na hora. O mesmo vale quando a saciedade chega a zero. Um resgate à beira da morte não conta como ressurreição." + Paragraph
            + "Quem morre e volta com o pergaminho da ressurreição fica com o Mal da ressurreição. Vida e energia começam em 10% e sobem por igual até o máximo quando o mal termina. Enquanto dura, a chance de acerto cai 20% e a chance de esquiva 10%. Em Apocalypse o primeiro mal dura duas semanas, em Downfall e Muito difícil uma semana, abaixo disso meia semana. A cada nova ressurreição do mesmo personagem a duração dobra, até no máximo quatro semanas." + Paragraph
            + "Além disso vem o Pânico na masmorra: o humor cai 25. O pânico vale só na masmorra e só enquanto o mal durar." + Paragraph
            + "Tudo isso vale em qualquer dificuldade de combate. A dificuldade de combate só decide quanto tempo o mal dura e quantas vezes um personagem pode voltar.");

    // The topics in the guide's order, with the game's guide picture each one borrows. Declared after the texts, so they are set
    // when this list is built.
    internal static readonly (int Id, string Image, UniqueCandidateTranslations Title, UniqueCandidateTranslations Text)[] Topics =
    {
        (First, "Health", DelversTitle, DelversText),
        (First + 1, "Health", WoundsTitle, WoundsText),
        (First + 2, "Health", BandagesTitle, BandagesText),
        (First + 3, "DeadUnitTreatment", ReviveTitle, ReviveText),
        (First + 9, "DeadUnitTreatment", AfterFallTitle, AfterFallText),
        (First + 4, "PauseInCombat", NoPauseTitle, NoPauseText),
        (First + 5, "Attack", InstantTitle, InstantText),
        (First + 6, "DeadlyStatus", AbilitiesTitle, AbilitiesText),
        (First + 7, "DeadUnitTreatment", GearTitle, GearText),
        (First + 8, "UnitBehaviorOptions", FormationTitle, FormationText),
    };
}
