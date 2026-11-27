namespace DungeonSettlersDelvers.Core;

// The group AI's menu section, switches, lock reason and the notice when it is switched on (rather an
// accessibility feature, locked from Very Hard up; the notice on every switch from off to on). All ten languages (the
// section stood in English in Korean), with the game's words as Core's guide uses them: Very Hard,
// the aid kit, the inventory, the item quick slots.
internal static class GroupAiText
{
    internal const string NoticeKey = "TEXTKEY_DelversCore_GroupAiNotice";

    internal static readonly UniqueCandidateTranslations MenuSection = new(
        "Group AI",
        "그룹 AI",
        "IA de groupe",
        "KI für die Gruppe",
        "ИИ отряда",
        "队伍 AI",
        "隊伍 AI",
        "グループAI",
        "IA de grupo",
        "IA de grupo");

    internal static readonly UniqueCandidateTranslations MenuHint = new(
        "Rather an accessibility feature: the AI helps steer your party in combat. Locked from Very Hard difficulty up.",
        "접근성 기능에 가깝습니다: AI가 전투에서 그룹 조작을 돕습니다. 매우 어려움 난이도부터는 잠깁니다.",
        "Plutôt une fonction d'accessibilité : l'IA aide à diriger votre groupe en combat. Verrouillée à partir de la difficulté Très difficile.",
        "Eher eine Barrierefreiheitsfunktion: Die KI steuert deine Gruppe im Kampf mit. Ab der Schwierigkeit Sehr schwer ist sie gesperrt.",
        "Скорее функция доступности: ИИ помогает управлять отрядом в бою. Недоступна начиная со сложности «Очень сложно».",
        "更像是一项辅助功能：AI 会在战斗中帮你指挥队伍。从非常困难难度起锁定。",
        "更像是一項輔助功能：AI 會在戰鬥中幫你指揮隊伍。從非常困難難度起鎖定。",
        "どちらかといえばアクセシビリティ機能です：戦闘中にAIがグループの操作を手伝います。ベリーハード以上の難易度ではロックされます。",
        "Más bien una función de accesibilidad: la IA ayuda a dirigir tu grupo en combate. Bloqueada desde la dificultad Muy difícil.",
        "Mais uma função de acessibilidade: a IA ajuda a comandar seu grupo em combate. Bloqueada a partir da dificuldade Muito difícil.");

    internal static readonly UniqueCandidateTranslations MenuEnabled = new(
        "Enable group AI",
        "그룹 AI 켜기",
        "Activer l'IA de groupe",
        "KI für deine Gruppe einschalten",
        "Включить ИИ отряда",
        "启用队伍 AI",
        "啟用隊伍 AI",
        "グループAIを有効にする",
        "Activar la IA de grupo",
        "Ativar a IA de grupo");

    // What the AI takes from the bag (GroupAiBandageRules.BagCounts): the four bandages and the aid kit; the aid kit by the game's
    // own name (TEXTKEY_ITEM_AidKit_NAME: Aid Kit, Verbandkasten).
    internal static readonly UniqueCandidateTranslations MenuBandagesFromBag = new(
        "AI may use bandages and aid kits from the bag",
        "AI가 인벤토리의 붕대와 구급상자를 사용할 수 있음",
        "L'IA peut utiliser les bandages et trousses de soins de l'inventaire",
        "KI darf Verbände und Verbandkästen aus der Tasche benutzen",
        "ИИ может брать бинты и аптечки из инвентаря",
        "AI 可以使用背包中的绷带和急救箱",
        "AI 可以使用背包中的繃帶和急救箱",
        "AIがインベントリの包帯と救急箱を使える",
        "La IA puede usar vendas y botiquines del inventario",
        "A IA pode usar bandagens e kits de primeiros socorros do inventário");

    internal static readonly UniqueCandidateTranslations MenuLocked = new(
        "Locked: this campaign is on Very Hard or higher.",
        "잠김: 이 캠페인은 매우 어려움 이상입니다.",
        "Verrouillé : cette campagne est en Très difficile ou plus.",
        "Gesperrt: Diese Kampagne läuft auf Sehr schwer oder höher.",
        "Недоступно: эта кампания на сложности «Очень сложно» или выше.",
        "已锁定：此战役为非常困难或更高难度。",
        "已鎖定：此戰役為非常困難或更高難度。",
        "ロック中：このキャンペーンはベリーハード以上です。",
        "Bloqueado: esta campaña está en Muy difícil o superior.",
        "Bloqueado: esta campanha está em Muito difícil ou acima.");

    internal static readonly UniqueCandidateTranslations Notice = new(
        "Group AI is on. It applies bandages placed on the item quick slots (Q and any extra slots). It uses bandages and aid kits from the bag only if \"AI may use bandages and aid kits from the bag\" is on in this menu section.",
        "그룹 AI가 켜졌습니다. 아이템 단축 슬롯(Q와 추가 슬롯)에 놓인 붕대를 사용합니다. 인벤토리의 붕대와 구급상자는 이 메뉴 항목에서 \"AI가 인벤토리의 붕대와 구급상자를 사용할 수 있음\"이 켜져 있을 때만 사용합니다.",
        "L'IA de groupe est activée. Elle applique les bandages placés sur les emplacements rapides d'objets (Q et les emplacements supplémentaires). Elle n'utilise les bandages et trousses de soins de l'inventaire que si \"L'IA peut utiliser les bandages et trousses de soins de l'inventaire\" est activé dans cette section du menu.",
        "Die KI für deine Gruppe ist an. Sie legt Verbände an, die auf den Schnelltasten für Gegenstände liegen (Q und gegebenenfalls Zusatzplätze). Verbände und Verbandkästen aus der Tasche benutzt sie nur, wenn \"KI darf Verbände und Verbandkästen aus der Tasche benutzen\" in diesem Menüabschnitt an ist.",
        "ИИ отряда включён. Он накладывает бинты, лежащие в быстрых ячейках предметов (Q и дополнительные ячейки). Бинты и аптечки из инвентаря он берёт, только если в этом разделе меню включено «ИИ может брать бинты и аптечки из инвентаря».",
        "队伍 AI 已开启。它会使用放在物品快捷栏（Q 以及额外栏位）上的绷带。只有在此菜单部分开启“AI 可以使用背包中的绷带和急救箱”时，它才会使用背包中的绷带和急救箱。",
        "隊伍 AI 已開啟。它會使用放在物品快捷欄（Q 以及額外欄位）上的繃帶。只有在此選單部分開啟「AI 可以使用背包中的繃帶和急救箱」時，它才會使用背包中的繃帶和急救箱。",
        "グループAIがオンになりました。アイテムのクイックスロット（Qと追加スロット）に置いた包帯を使います。インベントリの包帯と救急箱は、このメニュー項目で「AIがインベントリの包帯と救急箱を使える」がオンのときだけ使います。",
        "La IA de grupo está activada. Aplica las vendas colocadas en las ranuras rápidas de objetos (Q y las ranuras adicionales). Solo usa vendas y botiquines del inventario si \"La IA puede usar vendas y botiquines del inventario\" está activado en esta sección del menú.",
        "A IA de grupo está ativada. Ela aplica as bandagens colocadas nos espaços rápidos de itens (Q e os espaços extras). Só usa bandagens e kits de primeiros socorros do inventário se \"A IA pode usar bandagens e kits de primeiros socorros do inventário\" estiver ativado nesta seção do menu.");

    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> All => new[]
    {
        ("GroupAiMenuSection", MenuSection),
        ("GroupAiMenuHint", MenuHint),
        ("GroupAiMenuEnabled", MenuEnabled),
        ("GroupAiMenuBandagesFromBag", MenuBandagesFromBag),
        ("GroupAiMenuLocked", MenuLocked),
        (NoticeKey, Notice),
    };
}
