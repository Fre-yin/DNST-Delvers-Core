namespace DungeonSettlersDelvers.Core;

// The tactical formation's words in the game's ten languages, short in the window (the explanation is a topic of the guide). The button's name and text are rows of the game's text table,
// read by the game's tooltip under the key TooltipKey (like SummaryOptionUI_AutoAttack); the window picks the rest straight
// from here (MenuText.Pick).
internal static class TacticalFormationText
{
    internal const string TooltipKey = "SummaryOptionUI_DelversFormation";
    internal static readonly string NameKey = "TEXTKEY_" + TooltipKey + "_NAME";
    internal static readonly string DescKey = "TEXTKEY_" + TooltipKey + "_DESC";

    internal static readonly UniqueCandidateTranslations Name = new(
        "Formation", "대형", "Formation", "Formation", "Строй", "队形", "隊形", "隊形", "Formación", "Formação");

    // The button's tooltip (left click switches like the game's options, right click plans).
    internal static readonly UniqueCandidateTranslations Description = new(
        "Keeps this member in the group's formation. Left click: on or off. Right click: plan the formation.",
        "이 대원을 그룹 대형에 둡니다. 왼쪽 클릭: 켜기 또는 끄기. 오른쪽 클릭: 대형 계획.",
        "Garde ce membre dans la formation du groupe. Clic gauche : activer ou désactiver. Clic droit : préparer la formation.",
        "Hält diese Figur in der Formation der Gruppe. Linksklick: an oder aus. Rechtsklick: Formation planen.",
        "Держит персонажа в строю группы. Левый щелчок: включить или выключить. Правый щелчок: спланировать строй.",
        "让该成员保持在队伍的队形中。左键：开启或关闭。右键：规划队形。",
        "讓該成員保持在隊伍的隊形中。左鍵：開啟或關閉。右鍵：規劃隊形。",
        "このメンバーをグループの隊形に入れます。左クリック：オンまたはオフ。右クリック：隊形を計画。",
        "Mantiene a este miembro en la formación del grupo. Clic izquierdo: activar o desactivar. Clic derecho: planear la formación.",
        "Mantém este membro na formação do grupo. Clique esquerdo: ligar ou desligar. Clique direito: planejar a formação.");

    // Above the list, short like every word in the window (the small crown shows which member it is; the
    // explanation stands in the game's guide).
    internal static readonly UniqueCandidateTranslations LeaderTitle = new(
        "Group leader", "그룹 리더", "Chef de groupe", "Gruppenanführer", "Лидер группы", "队长", "隊長", "グループリーダー",
        "Líder del grupo", "Líder do grupo");

    // Above the grid: the leader walks towards the top row.
    internal static readonly UniqueCandidateTranslations Front = new(
        "Front", "앞쪽", "Avant", "Vorne", "Перед", "前方", "前方", "前方", "Frente", "Frente");

    // The pager of the formation book, "3 of 8".
    internal static readonly UniqueCandidateTranslations PageOf = new(
        "{0} of {1}", "{0} / {1}", "{0} sur {1}", "{0} von {1}", "{0} из {1}", "{0} / {1}", "{0} / {1}", "{0} / {1}", "{0} de {1}", "{0} de {1}");

    // The first row of a priority place's dropdown, beside the cross: the place becomes empty.
    internal static readonly UniqueCandidateTranslations ClearCast = new(
        "Clear", "비우기", "Vider", "Leeren", "Очистить", "清空", "清空", "空にする", "Vaciar", "Limpar");

    internal static readonly UniqueCandidateTranslations Reset = new(
        "Reset", "초기화", "Réinitialiser", "Zurücksetzen", "Сбросить", "重置", "重設", "リセット", "Restablecer", "Redefinir");

    internal static readonly UniqueCandidateTranslations Test = new(
        "Test formation", "대형 시험", "Tester la formation", "Formation testen", "Проверить строй", "测试队形", "測試隊形", "隊形を試す",
        "Probar formación", "Testar formação");

    // Every word of the formation, for the check of the languages (CoreTextRows.Sources); the first two are also rows of the
    // game's text table (CoreTextRows.Registered).
    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> All => new[]
    {
        (NameKey, Name), (DescKey, Description),
        (nameof(TacticalFormationText) + "." + nameof(LeaderTitle), LeaderTitle),
        (nameof(TacticalFormationText) + "." + nameof(Front), Front),
        (nameof(TacticalFormationText) + "." + nameof(PageOf), PageOf),
        (nameof(TacticalFormationText) + "." + nameof(ClearCast), ClearCast),
        (nameof(TacticalFormationText) + "." + nameof(Reset), Reset),
        (nameof(TacticalFormationText) + "." + nameof(Test), Test)
    };
}
