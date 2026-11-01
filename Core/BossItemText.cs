namespace DungeonSettlersDelvers.Core;

// Words of the boss bandits' parts (BossItemRules): name, effect and flavour, read by key like the bandages' (BandageText.NameKey,
// DescKey, FlavorKey; rows in CoreTextRows). German names and flavours from the author's draft; drafts until the author approves them. No
// hyphens in any language. Effects laid out like the author asked (no wall of text): the effect's
// name on a line of its own, then short lines; the game's tooltip text is TextMeshPro and breaks at "\n". The
// Legendary word of the subtitle reads Special for these parts (BossItemLook.TooltipShown).
// The effects are equipment effects (BossItemRules.EffectKey): name and text are the effect's
// (TEXTKEY_<effect>_NAME and _DESC), the tooltip shows them in the game's own block; the item keeps name and flavour.
// Gold as in the game's own descriptions: the tooltip runs the text through
// Tooltip_Base.ApplyTooltipFormatText and StringTools.FormatText, so #l...# turns the effect's name into the game's
// yellow #E1B638 and [AFFECTER_Bleeding] into its yellow linked term, as in the game's
// skills; numbers stay plain, as the game's {n} values do. The names in Apocalypse violet (the rarity stays Legendary, the
// name shows them as Apocalypse items).
internal static class BossItemText
{
    internal static readonly UniqueCandidateTranslations GreatSwordName = new(
        ApocalypseText.Tint("Ruthless Greatsword"), ApocalypseText.Tint("무자비한 대검"), ApocalypseText.Tint("Grande épée impitoyable"), ApocalypseText.Tint("Ruchloses Großschwert"), ApocalypseText.Tint("Безжалостный двуручный меч"),
        ApocalypseText.Tint("无情巨剑"), ApocalypseText.Tint("無情巨劍"), ApocalypseText.Tint("非情の大剣"), ApocalypseText.Tint("Mandoble despiadado"), ApocalypseText.Tint("Montante impiedoso"));

    internal static readonly UniqueCandidateTranslations GreatSwordEffectName = new(
        "Bloodletting",
        "사혈",
        "Saignée",
        "Aderlass",
        "Кровопускание",
        "放血",
        "放血",
        "瀉血",
        "Sangría",
        "Sangria");

    internal static readonly UniqueCandidateTranslations GreatSwordDescription = new(
        "Hits with attacks and skills deal an extra 30% of their damage as [AFFECTER_Bleeding]. 20% of that bleeding's damage heals the bearer, at most 30 health per second.",
        "공격과 스킬 적중 시 피해의 30%를 추가로 [AFFECTER_Bleeding] 피해로 입힙니다. 이 출혈 피해의 20%만큼 착용자의 생명력을 회복합니다(초당 최대 30).",
        "Les coups des attaques et compétences infligent en plus 30 % de leurs dégâts sous forme de [AFFECTER_Bleeding]. 20 % des dégâts de ce saignement soignent le porteur, jusqu'à 30 PV par seconde.",
        "Treffer mit Angriffen und Fertigkeiten verursachen zusätzlich 30 % ihres Schadens als [AFFECTER_Bleeding]. 20 % des Blutungsschadens heilen den Träger, höchstens 30 Leben pro Sekunde.",
        "Удары атаками и навыками дополнительно наносят 30% своего урона в виде эффекта «[AFFECTER_Bleeding]». 20% урона от этого кровотечения исцеляют владельца, не более 30 здоровья в секунду.",
        "攻击和技能命中时额外造成其伤害30%的[AFFECTER_Bleeding]伤害。该出血伤害的20%会治疗持有者，每秒最多30点生命。",
        "攻擊和技能命中時額外造成其傷害30%的[AFFECTER_Bleeding]傷害。該出血傷害的20%會治療持有者，每秒最多30點生命。",
        "攻撃とスキルの命中時、ダメージの30%を追加で[AFFECTER_Bleeding]ダメージとして与える。この出血ダメージの20%だけ装備者を回復する（毎秒最大30）。",
        "Los golpes de ataques y habilidades infligen además un 30 % de su daño como [AFFECTER_Bleeding]. El 20 % del daño de ese sangrado cura al portador, hasta 30 de salud por segundo.",
        "Os acertos de ataques e habilidades causam adicionalmente 30% do seu dano como [AFFECTER_Bleeding]. 20% do dano desse sangramento cura o portador, até 30 de vida por segundo.");

    internal static readonly UniqueCandidateTranslations GreatSwordFlavor = new(
        "This blade has seen much and forgotten none of it.", "이 칼날은 많은 것을 보았고, 그 무엇도 잊지 않았다.",
        "Cette lame a beaucoup vu et n'en a rien oublié.", "Diese Klinge hat schon viel gesehen und nichts davon vergessen.",
        "Этот клинок многое повидал и ничего не забыл.", "这把剑见过太多，却什么也没有忘记。", "這把劍見過太多，卻什麼也沒有忘記。",
        "この刃は多くを見てきた。そして何ひとつ忘れていない。", "Esta hoja ha visto mucho y no ha olvidado nada.",
        "Esta lâmina já viu muito e não esqueceu nada.");

    internal static readonly UniqueCandidateTranslations ShieldName = new(
        ApocalypseText.Tint("Ruthless Warden"), ApocalypseText.Tint("무자비한 수호자"), ApocalypseText.Tint("Gardien impitoyable"), ApocalypseText.Tint("Ruchloser Wächter"), ApocalypseText.Tint("Безжалостный страж"),
        ApocalypseText.Tint("无情守卫"), ApocalypseText.Tint("無情守衛"), ApocalypseText.Tint("非情の守り手"), ApocalypseText.Tint("Guardián despiadado"), ApocalypseText.Tint("Guardião impiedoso"));

    internal static readonly UniqueCandidateTranslations ShieldEffectName = new(
        "Blood Warden",
        "피의 수호자",
        "Gardien de sang",
        "Blutwächter",
        "Кровавый страж",
        "血之守卫",
        "血之守衛",
        "血の守護",
        "Guardián de sangre",
        "Guardião de Sangue");

    internal static readonly UniqueCandidateTranslations ShieldDescription = new(
        "When another party member falls below 35% health, the bearer sacrifices 40% of their current health and grants that member three times that amount as a shield for 8 seconds.\nHalf of any shield left over heals the member.\nOnce per battle.",
        "다른 아군의 체력이 35% 미만으로 떨어지면 착용자가 현재 체력의 40%를 바쳐 그 3배만큼의 보호막을 8초간 부여합니다.\n남은 보호막의 절반만큼 그 아군을 치유합니다.\n전투당 한 번.",
        "Quand un autre membre du groupe passe sous 35 % de vie, le porteur sacrifie 40 % de sa vie actuelle et lui accorde le triple de cette valeur en bouclier pendant 8 secondes.\nLa moitié du bouclier restant soigne ce membre.\nUne fois par combat.",
        "Fällt ein anderes Gruppenmitglied unter 35 % Leben, opfert der Träger 40 % seines aktuellen Lebens und gibt ihm das Dreifache davon als Schild für 8 Sekunden.\nWas vom Schild übrig bleibt, heilt das Mitglied zur Hälfte.\nEinmal pro Kampf.",
        "Когда здоровье другого союзника падает ниже 35%, носитель жертвует 40% своего текущего здоровья и даёт союзнику щит, втрое больший этого значения, на 8 секунд.\nПоловина оставшегося щита лечит союзника.\nРаз за бой.",
        "其他队友生命值低于35%时，佩戴者牺牲当前生命值的40%，为其提供相当于该值三倍的护盾，持续8秒。\n剩余护盾的一半会治疗该队友。\n每场战斗一次。",
        "其他隊友生命值低於35%時，佩戴者犧牲當前生命值的40%，為其提供相當於該值三倍的護盾，持續8秒。\n剩餘護盾的一半會治療該隊友。\n每場戰鬥一次。",
        "他の仲間のHPが35%未満になると、装備者は現在のHPの40%を捧げ、その3倍のシールドを8秒間付与する。\n残ったシールドの半分だけ仲間を回復する。\n戦闘ごとに1回。",
        "Cuando otro miembro del grupo baja del 35 % de vida, el portador sacrifica el 40 % de su vida actual y le otorga el triple como escudo durante 8 segundos.\nLa mitad del escudo restante cura al miembro.\nUna vez por combate.",
        "Quando outro membro do grupo fica abaixo de 35% de vida, o portador sacrifica 40% da vida atual e concede o triplo disso como escudo por 8 segundos.\nMetade do escudo restante cura o membro.\nUma vez por combate.");

    internal static readonly UniqueCandidateTranslations ShieldFlavor = new(
        "Ruthless as they were, they stood together. What drove them to it?",
        "그토록 무자비했음에도 그들은 함께였다. 무엇이 그들을 그렇게 만들었을까?",
        "Malgré toute leur cruauté, ils restaient unis. Quelle force les y poussait ?",
        "Trotz aller Skrupellosigkeit hielten sie zusammen. Was sie wohl dazu bewegt hat?",
        "При всей своей безжалостности они держались вместе. Что же их к этому побудило?",
        "纵然冷酷无情，他们依旧团结一心。究竟是什么让他们如此？", "縱然冷酷無情，他們依舊團結一心。究竟是什麼讓他們如此？",
        "どれほど非情でも、彼らは結束していた。何がそうさせたのだろう？",
        "Pese a toda su crueldad, se mantenían unidos. ¿Qué los movería a ello?",
        "Apesar de toda a crueldade, eles permaneciam unidos. O que os teria levado a isso?");

    internal static readonly UniqueCandidateTranslations BowName = new(
        ApocalypseText.Tint("Ruthless Bow"), ApocalypseText.Tint("무자비한 활"), ApocalypseText.Tint("Arc impitoyable"), ApocalypseText.Tint("Ruchloser Bogen"), ApocalypseText.Tint("Безжалостный лук"),
        ApocalypseText.Tint("无情之弓"), ApocalypseText.Tint("無情之弓"), ApocalypseText.Tint("非情の弓"), ApocalypseText.Tint("Arco despiadado"), ApocalypseText.Tint("Arco impiedoso"));

    internal static readonly UniqueCandidateTranslations BowEffectName = new(
        "Falcon Eyes",
        "매의 눈",
        "Yeux de faucon",
        "Falkenaugen",
        "Соколиный глаз",
        "鹰眼",
        "鷹眼",
        "鷹の目",
        "Ojos de halcón",
        "Olhos de Falcão");

    internal static readonly UniqueCandidateTranslations BowDescription = new(
        "Sight range +3, attack range +2.\nEach tile of distance to the target beyond 4 raises critical chance by 3%.",
        "시야 +3, 공격 사거리 +2.\n대상과의 거리가 4칸을 넘는 칸마다 치명타 확률이 3% 증가합니다.",
        "Vision +3, portée d'attaque +2.\nChaque case de distance au delà de 4 augmente les chances de coup critique de 3 %.",
        "Sichtweite +3, Angriffsreichweite +2.\nJedes Feld Abstand zum Ziel über 4 erhöht die kritische Trefferchance um 3 %.",
        "Обзор +3, дальность атаки +2.\nКаждая клетка расстояния до цели сверх 4 повышает шанс крита на 3%.",
        "视野+3，攻击距离+2。\n与目标的距离超过4格后，每多1格暴击几率提高3%。",
        "視野+3，攻擊距離+2。\n與目標的距離超過4格後，每多1格暴擊機率提高3%。",
        "視界+3、攻撃射程+2。\n対象との距離が4マスを超えると、1マスごとにクリティカル率が3%上がる。",
        "Visión +3, alcance de ataque +2.\nCada casilla de distancia al objetivo más allá de 4 aumenta la probabilidad de crítico un 3 %.",
        "Visão +3, alcance de ataque +2.\nCada casa de distância do alvo além de 4 aumenta a chance de crítico em 3%.");

    internal static readonly UniqueCandidateTranslations BowFlavor = new(
        "If you can see him, you are already too close. If you can't, even more so.",
        "그를 본다면 이미 너무 가까이 있는 것이다. 보지 못한다면 더더욱.",
        "Si vous le voyez, vous êtes déjà trop près. Si vous ne le voyez pas, encore plus.",
        "Wer ihn sieht, ist schon zu nah. Wer ihn nicht sieht, erst recht.",
        "Кто его видит, тот уже слишком близко. Кто не видит, тем более.",
        "看得见他，就已经太近了。看不见他，更是如此。", "看得見他，就已經太近了。看不見他，更是如此。",
        "彼が見えるなら、もう近すぎる。見えないなら、なおさらだ。",
        "Si lo ves, ya estás demasiado cerca. Si no lo ves, más aún.",
        "Se você o vê, já está perto demais. Se não o vê, mais ainda.");

    internal static readonly UniqueCandidateTranslations StaffName = new(
        ApocalypseText.Tint("Ruthless Nature"), ApocalypseText.Tint("무자비한 자연"), ApocalypseText.Tint("Nature impitoyable"), ApocalypseText.Tint("Ruchlose Natur"), ApocalypseText.Tint("Безжалостная природа"),
        ApocalypseText.Tint("无情自然"), ApocalypseText.Tint("無情自然"), ApocalypseText.Tint("非情の自然"), ApocalypseText.Tint("Naturaleza despiadada"), ApocalypseText.Tint("Natureza impiedosa"));

    internal static readonly UniqueCandidateTranslations StaffEffectName = new(
        "Overgrowth",
        "웃자람",
        "Prolifération",
        "Wildwuchs",
        "Буйный рост",
        "疯长",
        "瘋長",
        "繁茂",
        "Crecimiento salvaje",
        "Crescimento selvagem");

    internal static readonly UniqueCandidateTranslations StaffDescription = new(
        "Energy cost +25%, healing done +20%.\nHeals ignore 40% of the healing reduction from [AFFECTER_Bleeding].",
        "에너지 소모 +25%, 주는 치유량 +20%.\n치유가 [AFFECTER_Bleeding] 효과로 인한 치유 감소의 40%를 무시합니다.",
        "Coût en énergie +25 %, soins prodigués +20 %.\nLes soins ignorent 40 % de la réduction de soins due à [AFFECTER_Bleeding].",
        "Energiekosten +25 %, ausgeteilte Heilung +20 %.\nHeilungen ignorieren 40 % der Heilungsminderung durch [AFFECTER_Bleeding].",
        "Затраты энергии +25%, исходящее лечение +20%.\nЛечение игнорирует 40% снижения лечения от эффекта «[AFFECTER_Bleeding]».",
        "能量消耗+25%，造成的治疗+20%。\n治疗无视[AFFECTER_Bleeding]造成的40%治疗削减。",
        "能量消耗+25%，造成的治療+20%。\n治療無視[AFFECTER_Bleeding]造成的40%治療削減。",
        "エネルギー消費+25%、与える回復量+20%。\n回復は[AFFECTER_Bleeding]による回復量低下の40%を無視する。",
        "Coste de energía +25 %, curación realizada +20 %.\nLas curaciones ignoran el 40 % de la reducción de curación por [AFFECTER_Bleeding].",
        "Custo de energia +25%, cura realizada +20%.\nAs curas ignoram 40% da redução de cura causada por [AFFECTER_Bleeding].");

    internal static readonly UniqueCandidateTranslations StaffFlavor = new(
        "She heals what she wants. And she wants a lot.", "그녀는 원하는 것을 치유한다. 그리고 원하는 것이 많다.",
        "Elle soigne ce qu'elle veut. Et elle en veut beaucoup.", "Sie heilt, was sie will. Und sie will viel.",
        "Она лечит что захочет. А хочет она многого.", "她想治愈什么，就治愈什么。而她想要的很多。", "她想治癒什麼，就治癒什麼。而她想要的很多。",
        "彼女は癒したいものを癒す。そして、癒したいものは多い。", "Cura lo que quiere. Y quiere mucho.", "Ela cura o que quer. E quer muito.");

    // The word in place of Legendary in the tooltip's subtitle (special primary or secondary gear), drafts
    // in the game's way of naming a rarity before the slot.
    internal static readonly UniqueCandidateTranslations Special = new(
        "Special", "특수", "Spécial", "Spezielle", "Особое", "特殊", "特殊", "スペシャル", "Especial", "Especial");
}
