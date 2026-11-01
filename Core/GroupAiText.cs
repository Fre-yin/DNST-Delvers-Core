namespace DungeonSettlersDelvers.Core;

// The group AI's menu section, switches, lock reason and the notice when it is switched on (rather an
// accessibility feature, locked from Very Hard up; the notice on every switch from off to on). Drafts in German and English
// for review before release; the other eight languages carry the English text until they are translated.
internal static class GroupAiText
{
    internal const string NoticeKey = "TEXTKEY_DelversCore_GroupAiNotice";

    internal static readonly UniqueCandidateTranslations MenuSection = Draft(
        "Group AI",
        "KI für die Gruppe");

    internal static readonly UniqueCandidateTranslations MenuHint = Draft(
        "Rather an accessibility feature: the AI helps steer your party in combat. Locked from Very Hard difficulty up.",
        "Eher eine Barrierefreiheitsfunktion: Die KI steuert deine Gruppe im Kampf mit. Ab der Schwierigkeit Sehr schwer ist sie gesperrt.");

    internal static readonly UniqueCandidateTranslations MenuEnabled = Draft(
        "Enable group AI",
        "KI für deine Gruppe einschalten");

    // What the AI takes from the bag (GroupAiBandageRules.BagCounts): the four bandages and the aid kit; the aid kit by the game's
    // own name (TEXTKEY_ITEM_AidKit_NAME: Aid Kit, Verbandkasten).
    internal static readonly UniqueCandidateTranslations MenuBandagesFromBag = Draft(
        "AI may use bandages and aid kits from the bag",
        "KI darf Verbände und Verbandkästen aus der Tasche benutzen");

    internal static readonly UniqueCandidateTranslations MenuLocked = Draft(
        "Locked: this campaign is on Very Hard or higher.",
        "Gesperrt: Diese Kampagne läuft auf Sehr schwer oder höher.");

    internal static readonly UniqueCandidateTranslations Notice = Draft(
        "Group AI is on. It applies bandages placed on the item quick slots (Q and any extra slots). It uses bandages and aid kits from the bag only if \"AI may use bandages and aid kits from the bag\" is on in this menu section.",
        "Die KI für deine Gruppe ist an. Sie legt Verbände an, die auf den Schnelltasten für Gegenstände liegen (Q und gegebenenfalls Zusatzplätze). Verbände und Verbandkästen aus der Tasche benutzt sie nur, wenn \"KI darf Verbände und Verbandkästen aus der Tasche benutzen\" in diesem Menüabschnitt an ist.");

    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> All => new[]
    {
        ("GroupAiMenuSection", MenuSection),
        ("GroupAiMenuHint", MenuHint),
        ("GroupAiMenuEnabled", MenuEnabled),
        ("GroupAiMenuBandagesFromBag", MenuBandagesFromBag),
        ("GroupAiMenuLocked", MenuLocked),
        (NoticeKey, Notice),
    };

    private static UniqueCandidateTranslations Draft(string english, string german)
        => new(english, english, english, german, english, english, english, english, english, english);
}
