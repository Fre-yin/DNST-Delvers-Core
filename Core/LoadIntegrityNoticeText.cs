namespace DungeonSettlersDelvers.Core;

// In-game texts shown while the load guard blocks saving. Without them the only
// trace of a blocked autosave is the loader log. The dialog explains; the short
// banner fits the one-line help notice that accompanies blocked autosaves.
internal static class LoadIntegrityNoticeText
{
    internal const string SaveBlockedKey = "TEXTKEY_DelversCore_SaveBlocked";
    internal const string SaveBlockedShortKey = "TEXTKEY_DelversCore_SaveBlockedShort";

    internal static readonly UniqueCandidateTranslations SaveBlocked = new(
        "Delvers Core: Saving is blocked. A load in this session could not be completed because mod data was missing or a mod was incompatible. Progress is not being saved; your save file remains unchanged. Quit the game, check your mods, and restart it.",
        "Delvers Core: 저장이 차단되었습니다. 모드 데이터가 누락되었거나 호환되지 않는 모드가 있어 이번 세션에서 불러오기가 정상적으로 완료되지 않았습니다. 진행 상황은 저장되지 않으며 저장 파일은 변경되지 않습니다. 게임을 종료하고 모드를 확인한 뒤 다시 시작하세요.",
        "Delvers Core : la sauvegarde est bloquée. Un chargement effectué pendant cette session n’a pas pu aboutir parce que des données de mod manquaient ou qu’un mod était incompatible. La progression n’est pas sauvegardée ; votre fichier de sauvegarde reste inchangé. Quittez le jeu, vérifiez vos mods et relancez-le.",
        "Delvers Core: Das Speichern ist gesperrt. Ein Ladevorgang in dieser Sitzung konnte nicht abgeschlossen werden, weil Mod-Daten fehlten oder ein Mod inkompatibel war. Dein Fortschritt wird nicht gespeichert; deine Speicherdatei bleibt unverändert. Beende das Spiel, überprüfe deine Mods und starte es neu.",
        "Delvers Core: сохранение заблокировано. Во время этой сессии загрузка завершилась не полностью из-за отсутствующих данных мода или несовместимого мода. Прогресс не сохраняется, а файл сохранения остаётся без изменений. Выйдите из игры, проверьте моды и перезапустите игру.",
        "Delvers Core：当前无法存档。本次游戏中，由于缺少模组数据或模组不兼容，读档未能正常完成。当前进度不会保存，存档文件保持不变。请退出游戏，检查模组后重新启动。",
        "Delvers Core：目前無法存檔。本次遊戲中，由於缺少模組資料或模組不相容，讀檔未能正常完成。目前進度不會儲存，原有存檔將維持不變。請退出遊戲，檢查模組後重新啟動。",
        "Delvers Core：セーブできません。このセッションでは、MODデータの欠落または互換性のないMODが原因で、ロードが正常に完了しませんでした。進行状況は保存されず、セーブファイルも変更されません。ゲームを終了し、MODを確認してから再起動してください。",
        "Delvers Core: el guardado está bloqueado. Durante esta sesión, una carga no pudo completarse porque faltaban datos de un mod o había un mod incompatible. El progreso no se guarda; tu archivo de guardado permanece intacto. Sal del juego, revisa tus mods y reinícialo.",
        "Delvers Core: o salvamento está bloqueado. Nesta sessão, um carregamento não pôde ser concluído porque faltavam dados de um mod ou havia um mod incompatível. O progresso não está sendo salvo; seu arquivo de salvamento permanece inalterado. Saia do jogo, verifique seus mods e reinicie o jogo.");

    internal static readonly UniqueCandidateTranslations SaveBlockedShort = new(
        "Delvers Core: Saving blocked due to a mod error while loading. Please restart the game.",
        "Delvers Core: 모드 불러오기 문제로 저장이 차단되었습니다. 게임을 다시 시작하세요.",
        "Delvers Core : sauvegarde bloquée après un problème de mod au chargement. Relancez le jeu.",
        "Delvers Core: Speichern wegen eines Mod-Fehlers beim Laden gesperrt. Spiel neu starten.",
        "Delvers Core: сохранение заблокировано. Проблема с модом при загрузке. Перезапустите игру.",
        "Delvers Core：读档时出现模组问题，当前无法存档。请重新启动游戏。",
        "Delvers Core：讀檔時發生模組問題，目前無法存檔。請重新啟動遊戲。",
        "Delvers Core：ロード中にMODの問題が発生したため、セーブできません。ゲームを再起動してください。",
        "Delvers Core: guardado bloqueado por un problema con un mod al cargar. Reinicia el juego.",
        "Delvers Core: salvamento bloqueado por um problema de mod ao carregar. Reinicie o jogo.");

    internal static IEnumerable<(string Key, UniqueCandidateTranslations Text)> All => new[]
    {
        (SaveBlockedKey, SaveBlocked),
        (SaveBlockedShortKey, SaveBlockedShort)
    };
}
