namespace DungeonSettlersDelvers.Core;

// The notice when Core stays off because the game was started without Steam (SteamGuard; the author's wording): the
// game's dialog once in the title, and the short line on Core's page of the Delvers menu.
internal static class SteamGuardText
{
    internal const string NoticeKey = "TEXTKEY_DelversCore_SteamOffNotice";

    internal static readonly UniqueCandidateTranslations Notice = new(
        "Delvers is off. This game was started without Steam by an old version of a mod loader. Start Dungeon Settlers through Steam, or update the mod loader, to play with Delvers.",
        "Delvers가 꺼져 있습니다. 이 게임은 오래된 버전의 모드 로더로 Steam 없이 시작되었습니다. Delvers로 플레이하려면 Steam에서 Dungeon Settlers를 시작하거나 모드 로더를 업데이트하세요.",
        "Delvers est désactivé. Ce jeu a été lancé sans Steam par une ancienne version d'un chargeur de mods. Lancez Dungeon Settlers via Steam, ou mettez à jour le chargeur de mods, pour jouer avec Delvers.",
        "Delvers ist aus. Dieses Spiel wurde von einer alten Version eines Modloaders ohne Steam gestartet. Starte Dungeon Settlers über Steam oder aktualisiere den Modloader, um mit Delvers zu spielen.",
        "Delvers отключён. Эта игра запущена без Steam старой версией загрузчика модов. Запустите Dungeon Settlers через Steam или обновите загрузчик модов, чтобы играть с Delvers.",
        "Delvers 已关闭。本游戏由旧版本的模组加载器在没有 Steam 的情况下启动。请通过 Steam 启动 Dungeon Settlers，或更新模组加载器，即可使用 Delvers 游玩。",
        "Delvers 已關閉。本遊戲由舊版本的模組載入器在沒有 Steam 的情況下啟動。請透過 Steam 啟動 Dungeon Settlers，或更新模組載入器，即可使用 Delvers 遊玩。",
        "Delvers はオフです。このゲームは古いバージョンの MOD ローダーによって Steam なしで起動されました。Delvers で遊ぶには、Steam から Dungeon Settlers を起動するか、MOD ローダーを更新してください。",
        "Delvers está desactivado. Este juego se inició sin Steam con una versión antigua de un cargador de mods. Inicia Dungeon Settlers desde Steam, o actualiza el cargador de mods, para jugar con Delvers.",
        "O Delvers está desativado. Este jogo foi iniciado sem a Steam por uma versão antiga de um carregador de mods. Inicie Dungeon Settlers pela Steam, ou atualize o carregador de mods, para jogar com o Delvers.");

    internal static readonly UniqueCandidateTranslations MenuLine = new(
        "Delvers is off: the game was started without Steam.",
        "Delvers가 꺼져 있습니다: 게임이 Steam 없이 시작되었습니다.",
        "Delvers est désactivé : le jeu a été lancé sans Steam.",
        "Delvers ist aus: Das Spiel wurde ohne Steam gestartet.",
        "Delvers отключён: игра запущена без Steam.",
        "Delvers 已关闭：游戏在没有 Steam 的情况下启动。",
        "Delvers 已關閉：遊戲在沒有 Steam 的情況下啟動。",
        "Delvers はオフです：ゲームが Steam なしで起動されました。",
        "Delvers está desactivado: el juego se inició sin Steam.",
        "O Delvers está desativado: o jogo foi iniciado sem a Steam.");

    // The menu line for the check of the languages (CoreTextRows.Sources); the notice is a registered row.
    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> All => new[]
    {
        (nameof(SteamGuardText) + "." + nameof(MenuLine), MenuLine),
    };
}
