namespace DungeonSettlersDelvers.Core;

// The Delvers menu's own words in the game's ten languages, with the terms of Core's other texts
// (LoadIntegrityNoticeText). The pages of the mods bring their own texts.
internal static class DelversMenuText
{
    internal static readonly UniqueCandidateTranslations Close = new(
        "Close", "닫기", "Fermer", "Schließen", "Закрыть", "关闭", "關閉", "閉じる", "Cerrar", "Fechar");

    internal static readonly UniqueCandidateTranslations PageFailed = new(
        "This page could not be loaded. Details are in the log.",
        "이 페이지를 불러오지 못했습니다. 자세한 내용은 로그를 확인하세요.",
        "Impossible de charger cette page. Les détails figurent dans le journal.",
        "Diese Seite konnte nicht geladen werden. Details stehen im Log.",
        "Не удалось загрузить эту страницу. Подробности в журнале.",
        "无法加载此页面。详情请查看日志。",
        "無法載入此頁面。詳情請查看記錄檔。",
        "このページを読み込めませんでした。詳細はログを確認してください。",
        "No se pudo cargar esta página. Los detalles están en el registro.",
        "Não foi possível carregar esta página. Os detalhes estão no log.");

    internal static readonly UniqueCandidateTranslations VerifiedGame = new(
        "Verified game version: ",
        "확인된 게임 버전: ",
        "Version du jeu vérifiée : ",
        "Geprüfte Spielversion: ",
        "Проверенная версия игры: ",
        "已验证的游戏版本：",
        "已驗證的遊戲版本：",
        "動作確認済みのゲームバージョン：",
        "Versión del juego verificada: ",
        "Versão do jogo verificada: ");

    internal static readonly UniqueCandidateTranslations SaveAllowed = new(
        "Save protection: saving is allowed.",
        "저장 보호: 저장할 수 있습니다.",
        "Protection des sauvegardes : la sauvegarde est autorisée.",
        "Speicherschutz: Speichern ist möglich.",
        "Защита сохранений: сохранять можно.",
        "存档保护：可以正常存档。",
        "存檔保護：可以正常存檔。",
        "セーブ保護：セーブできます。",
        "Protección de guardado: se puede guardar.",
        "Proteção de salvamento: é possível salvar.");

    internal static readonly UniqueCandidateTranslations SaveBlocked = new(
        "Save protection: saving is blocked.",
        "저장 보호: 저장이 차단되었습니다.",
        "Protection des sauvegardes : la sauvegarde est bloquée.",
        "Speicherschutz: Speichern ist gesperrt.",
        "Защита сохранений: сохранение заблокировано.",
        "存档保护：当前无法存档。",
        "存檔保護：目前無法存檔。",
        "セーブ保護：セーブできません。",
        "Protección de guardado: el guardado está bloqueado.",
        "Proteção de salvamento: o salvamento está bloqueado.");

    internal static readonly UniqueCandidateTranslations Mods = new(
        "Mods", "모드", "Mods", "Mods", "Моды", "模组", "模組", "MOD", "Mods", "Mods");

    internal static readonly UniqueCandidateTranslations NoOtherMods = new(
        "No other mod has settings here.",
        "여기에 설정이 있는 다른 모드가 없습니다.",
        "Aucun autre mod n’a de paramètres ici.",
        "Kein anderer Mod hat hier Einstellungen.",
        "Других модов с настройками здесь нет.",
        "暂无其他模组在此提供设置。",
        "目前沒有其他模組在此提供設定。",
        "ここに設定がある他のMODはありません。",
        "Ningún otro mod tiene ajustes aquí.",
        "Nenhum outro mod tem configurações aqui.");

    internal static readonly UniqueCandidateTranslations Founders = new(
        "Founders", "창립 멤버", "Fondateurs", "Gründer", "Основатели",
        "创始成员", "創始成員", "創設メンバー", "Fundadores", "Fundadores");

    internal static readonly UniqueCandidateTranslations FoundersHint = new(
        "Which founders can appear in the guild and the start group. Founders you already have stay.",
        "길드와 시작 그룹에 등장할 수 있는 창립 멤버입니다. 이미 영입한 창립 멤버는 그대로 남습니다.",
        "Les fondateurs qui peuvent apparaître dans la guilde et le groupe de départ. Ceux que vous avez déjà restent.",
        "Welche Gründer in der Gilde und der Startgruppe erscheinen können. Bereits rekrutierte Gründer bleiben.",
        "Какие основатели могут появляться в гильдии и в стартовой группе. Уже нанятые основатели остаются.",
        "可以在公会和初始队伍中出现的创始成员。已招募的创始成员会保留。",
        "可以在公會和初始隊伍中出現的創始成員。已招募的創始成員會保留。",
        "ギルドと初期メンバーに登場できる創設メンバーです。すでに仲間にした創設メンバーはそのまま残ります。",
        "Qué fundadores pueden aparecer en el gremio y en el grupo inicial. Los que ya tienes se quedan.",
        "Quais fundadores podem aparecer na guilda e no grupo inicial. Os que você já tem permanecem.");

    // Under the locked Delvers entry of the options window while a campaign runs (DelversMenuOptionsTab).
    internal static readonly UniqueCandidateTranslations OnlyInTitleMenu = new(
        "Title menu only",
        "타이틀 메뉴에서만",
        "Seulement au menu principal",
        "Nur im Titelmenü",
        "Только в главном меню",
        "仅限标题菜单",
        "僅限標題選單",
        "タイトルメニューのみ",
        "Solo en el menú principal",
        "Somente no menu principal");

    internal static IEnumerable<(string Name, UniqueCandidateTranslations Text)> All => new[]
    {
        (nameof(Close), Close), (nameof(PageFailed), PageFailed), (nameof(VerifiedGame), VerifiedGame),
        (nameof(SaveAllowed), SaveAllowed), (nameof(SaveBlocked), SaveBlocked), (nameof(Mods), Mods),
        (nameof(NoOtherMods), NoOtherMods), (nameof(Founders), Founders), (nameof(FoundersHint), FoundersHint),
        (nameof(OnlyInTitleMenu), OnlyInTitleMenu)
    };
}
