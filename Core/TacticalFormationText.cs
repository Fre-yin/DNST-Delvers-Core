namespace DungeonSettlersDelvers.Core;

// The tactical formation's words in the game's ten languages, short in the window (the explanation is a topic of the guide). The button's name and text are rows of the game's text table,
// read by the game's tooltip under the key TooltipKey (like SummaryOptionUI_AutoAttack); the window picks the rest straight
// from here (MenuText.Pick).
internal static class TacticalFormationText
{
    internal const string TooltipKey = "SummaryOptionUI_DelversFormation";
    internal static readonly string NameKey = "TEXTKEY_" + TooltipKey + "_NAME";
    internal static readonly string DescKey = "TEXTKEY_" + TooltipKey + "_DESC";
    // The tempo button's tooltip, read by the game's tooltip the same way (TacticalFormationWindowTempo).
    internal const string TempoTooltipKey = "SummaryOptionUI_DelversFormationTempo";
    internal static readonly string TempoNameKey = "TEXTKEY_" + TempoTooltipKey + "_NAME";
    internal static readonly string TempoDescKey = "TEXTKEY_" + TempoTooltipKey + "_DESC";

    internal static readonly UniqueCandidateTranslations Name = new(
        "Formation", "대형", "Formation", "Formation", "Строй", "队形", "隊形", "隊形", "Formación", "Formação");

    // The button's tooltip (left click switches like the game's options, right click plans; each
    // click on a line of its own, the mouse button coloured).
    internal static readonly UniqueCandidateTranslations Description = new(
        "Keeps this member in the group's formation.\n<color=#C98A55>Left click</color>  on or off\n<color=#C98A55>Right click</color>  plan the formation",
        "이 대원을 그룹 대형에 둡니다.\n<color=#C98A55>왼쪽 클릭</color>  켜기 또는 끄기\n<color=#C98A55>오른쪽 클릭</color>  대형 계획",
        "Garde ce membre dans la formation du groupe.\n<color=#C98A55>Clic gauche</color>  activer ou désactiver\n<color=#C98A55>Clic droit</color>  préparer la formation",
        "Hält diese Figur in der Formation der Gruppe.\n<color=#C98A55>Linksklick</color>  an oder aus\n<color=#C98A55>Rechtsklick</color>  Formation planen",
        "Держит персонажа в строю группы.\n<color=#C98A55>Левый щелчок</color>  включить или выключить\n<color=#C98A55>Правый щелчок</color>  спланировать строй",
        "让该成员保持在队伍的队形中。\n<color=#C98A55>左键</color>  开启或关闭\n<color=#C98A55>右键</color>  规划队形",
        "讓該成員保持在隊伍的隊形中。\n<color=#C98A55>左鍵</color>  開啟或關閉\n<color=#C98A55>右鍵</color>  規劃隊形",
        "このメンバーをグループの隊形に入れます。\n<color=#C98A55>左クリック</color>  オンまたはオフ\n<color=#C98A55>右クリック</color>  隊形を計画",
        "Mantiene a este miembro en la formación del grupo.\n<color=#C98A55>Clic izquierdo</color>  activar o desactivar\n<color=#C98A55>Clic derecho</color>  planear la formación",
        "Mantém este membro na formação do grupo.\n<color=#C98A55>Clique esquerdo</color>  ligar ou desligar\n<color=#C98A55>Clique direito</color>  planejar a formação");

    // The state beside the tooltip's title, for the selection the button shows.
    internal static readonly UniqueCandidateTranslations StateOn = new(
        "on", "켜짐", "activée", "an", "вкл", "开", "開", "オン", "activada", "ativada");

    internal static readonly UniqueCandidateTranslations StateOff = new(
        "off", "꺼짐", "désactivée", "aus", "выкл", "关", "關", "オフ", "desactivada", "desativada");

    // The title with the state after it: on in the roles' green, off in the window's faded grey.
    internal static string TitleWithState(string title, string state, bool on)
        => title + "  <color=" + (on ? "#6FB85A" : "#8C8590") + ">" + state + "</color>";

    // Above the list, short like every word in the window (the small crown shows which member it is; the
    // explanation stands in the game's guide).
    internal static readonly UniqueCandidateTranslations LeaderTitle = new(
        "Group leader", "그룹 리더", "Chef de groupe", "Gruppenanführer", "Лидер группы", "队长", "隊長", "グループリーダー",
        "Líder del grupo", "Líder do grupo");

    // Above the grid: the leader walks towards the top row.
    internal static readonly UniqueCandidateTranslations Front = new(
        "Front", "앞쪽", "Avant", "Vorne", "Перед", "前方", "前方", "前方", "Frente", "Frente");

    // The last row of a priority place's dropdown, set apart, beside the cross: the place becomes empty.
    internal static readonly UniqueCandidateTranslations ClearCast = new(
        "Clear slot", "칸 비우기", "Vider la case", "Platz leeren", "Очистить ячейку", "清空栏位", "清空欄位", "枠を空ける", "Vaciar casilla", "Limpar espaço");

    internal static readonly UniqueCandidateTranslations Reset = new(
        "Reset", "초기화", "Réinitialiser", "Zurücksetzen", "Сбросить", "重置", "重設", "リセット", "Restablecer", "Redefinir");

    internal static readonly UniqueCandidateTranslations Test = new(
        "Test formation", "대형 시험", "Tester la formation", "Formation testen", "Проверить строй", "测试队形", "測試隊形", "隊形を試す",
        "Probar formación", "Testar formação");

    // The tempo button beside the grid (TacticalFormationTempo): outside a fight the group walks at the speed of
    // its slowest member. Named for moving in formation, the speed in its tooltip.
    internal static readonly UniqueCandidateTranslations Tempo = new(
        "Move in formation", "대형으로 이동", "Se déplacer en formation", "In Formation bewegen", "Двигаться строем", "保持队形移动",
        "保持隊形移動", "隊形で移動", "Moverse en formación", "Mover em formação");

    internal static readonly UniqueCandidateTranslations TempoDescription = new(
        "The group matches the movement speed of its slowest member.",
        "그룹이 가장 느린 대원의 이동 속도에 맞춥니다.",
        "Le groupe s'aligne sur la vitesse de déplacement de son membre le plus lent.",
        "Die Gruppe passt sich der Bewegungsgeschwindigkeit des Langsamsten an.",
        "Группа подстраивается под скорость передвижения самого медленного участника.",
        "队伍会配合最慢成员的移动速度。",
        "隊伍會配合最慢成員的移動速度。",
        "グループは最も遅いメンバーの移動速度に合わせます。",
        "El grupo se ajusta a la velocidad de movimiento de su miembro más lento.",
        "O grupo acompanha a velocidade de movimento do seu membro mais lento.");

    // The fixed selection: a member who is not on the shown map stays in the list, faded, with where it is.
    internal static readonly UniqueCandidateTranslations Away = new(
        "Absent", "부재 중", "Ailleurs", "Abwesend", "Отсутствует", "不在场", "不在場", "不在", "Ausente", "Fora");

    // A member down at near-death (still to be rescued): faded on its place, which stays reserved.
    internal static readonly UniqueCandidateTranslations Fallen = new(
        "Fallen", "쓰러짐", "À terre", "Gefallen", "Пал", "倒地", "倒下", "倒れている", "Caído", "Caída");

    internal static readonly UniqueCandidateTranslations InVillage = new(
        "In the village", "마을에 있음", "Au village", "Im Dorf", "В деревне", "在村庄", "在村莊", "村にいる", "En la aldea", "Na aldeia");

    internal static readonly UniqueCandidateTranslations InDungeon = new(
        "In the dungeon", "던전에 있음", "Au donjon", "Im Dungeon", "В подземелье", "在地下城", "在地城", "ダンジョンにいる", "En la mazmorra", "Na masmorra");

    // The title of the add choice (the add button below the rows): every living character and the summon kinds of the set's summoners.
    internal static readonly UniqueCandidateTranslations AddTitle = new(
        "Add to formation", "대형에 추가", "Ajouter à la formation", "Zur Formation hinzufügen", "Добавить в строй", "加入队形", "加入隊形",
        "隊形に加える", "Añadir a la formación", "Adicionar à formação");

    // The button below the list's rows that opens the choice of what to add (like the bottom bar's buttons).
    internal static readonly UniqueCandidateTranslations AddButton = new(
        "Add", "추가", "Ajouter", "Hinzufügen", "Добавить", "添加", "新增", "追加", "Añadir", "Adicionar");

    internal static readonly UniqueCandidateTranslations RemoveHint = new(
        "Right click removes a row", "우클릭으로 줄 제거", "Clic droit : retirer la ligne", "Rechtsklick nimmt eine Zeile heraus",
        "ПКМ убирает строку", "右键移除此行", "右鍵移除此行", "右クリックで行を外す", "Clic derecho quita una fila", "Clique direito remove uma linha");

    // A summon's row: its number in the name, the summoner below it.
    internal static readonly UniqueCandidateTranslations SummonedBy = new(
        "summoned by {0}", "{0}의 소환수", "invoqué par {0}", "beschworen von {0}", "призван: {0}", "{0}的召唤物", "{0}的召喚物", "{0}の召喚",
        "invocado por {0}", "convocado por {0}");

    // The four roles of the role column (TacticalRole).
    internal static readonly UniqueCandidateTranslations RoleTank = new(
        "Tank", "탱커", "Tank", "Tank", "Танк", "坦克", "坦克", "タンク", "Tanque", "Tanque");

    internal static readonly UniqueCandidateTranslations RoleDamage = new(
        "Damage", "딜러", "Dégâts", "Schaden", "Урон", "输出", "輸出", "アタッカー", "Daño", "Dano");

    internal static readonly UniqueCandidateTranslations RoleSupport = new(
        "Support", "서포터", "Soutien", "Support", "Поддержка", "辅助", "輔助", "サポート", "Apoyo", "Suporte");

    internal static readonly UniqueCandidateTranslations RoleNeutral = new(
        "Neutral", "중립", "Neutre", "Neutral", "Нейтрально", "中立", "中立", "ニュートラル", "Neutral", "Neutro");

    internal static UniqueCandidateTranslations RoleName(TacticalRole role) => role switch
    {
        TacticalRole.Tank => RoleTank,
        TacticalRole.Damage => RoleDamage,
        TacticalRole.Support => RoleSupport,
        _ => RoleNeutral
    };

    // Every word of the formation, for the check of the languages (CoreTextRows.Sources); the first two are also rows of the
    // game's text table (CoreTextRows.Registered).
    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> All => new[]
    {
        (NameKey, Name), (DescKey, Description), (TempoNameKey, Tempo), (TempoDescKey, TempoDescription),
        (nameof(TacticalFormationText) + "." + nameof(LeaderTitle), LeaderTitle),
        (nameof(TacticalFormationText) + "." + nameof(Front), Front),
        (nameof(TacticalFormationText) + "." + nameof(ClearCast), ClearCast),
        (nameof(TacticalFormationText) + "." + nameof(Reset), Reset),
        (nameof(TacticalFormationText) + "." + nameof(Test), Test),
        (nameof(TacticalFormationText) + "." + nameof(Away), Away),
        (nameof(TacticalFormationText) + "." + nameof(Fallen), Fallen),
        (nameof(TacticalFormationText) + "." + nameof(InVillage), InVillage),
        (nameof(TacticalFormationText) + "." + nameof(InDungeon), InDungeon),
        (nameof(TacticalFormationText) + "." + nameof(AddTitle), AddTitle),
        (nameof(TacticalFormationText) + "." + nameof(AddButton), AddButton),
        (nameof(TacticalFormationText) + "." + nameof(StateOn), StateOn),
        (nameof(TacticalFormationText) + "." + nameof(StateOff), StateOff),
        (nameof(TacticalFormationText) + "." + nameof(RemoveHint), RemoveHint),
        (nameof(TacticalFormationText) + "." + nameof(SummonedBy), SummonedBy),
        (nameof(TacticalFormationText) + "." + nameof(RoleTank), RoleTank),
        (nameof(TacticalFormationText) + "." + nameof(RoleDamage), RoleDamage),
        (nameof(TacticalFormationText) + "." + nameof(RoleSupport), RoleSupport),
        (nameof(TacticalFormationText) + "." + nameof(RoleNeutral), RoleNeutral)
    };
}
