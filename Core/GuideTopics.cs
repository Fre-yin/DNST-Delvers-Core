using HarmonyLib;
#if BEPINEX
using GameGuideSessionState = global::Refactor.Runtime.GameGuideSessionState;
#else
using GameGuideSessionState = Il2CppRefactor.Runtime.GameGuideSessionState;
#endif

namespace DungeonSettlersDelvers.Core;

// The "Delvers" topics at the bottom of the game's guide (GuideTopicText). SubUI_TopicSelectPanel.Init builds one button per
// GameGuideType from the guide's button prefab under its list and keeps them in guideButtons. After it, Core adds its own
// topics the same way (SubUI_GuideButton.Init, guideButtons.Add, BuildGuideSearchCache), last in the list; the game then
// shows, highlights and searches them like its own. Their texts are rows of the game's text table (CoreTextRows), their
// pictures the game's guide pictures (FirstHintImagePatch). Nothing goes into a save.
// A click on a topic reaches GameGuidePresenter.OnSelectGuide as the type written as text, and the game accepts only its own
// types there (it throws for 9001). For Core's topics, GuideTopicSelectPatch does the same steps itself: remember the state,
// show the topic's content with the list's and the content's scroll positions, remember again.
// The guide remembers its topic across closing in GameGuideSessionState.RecordView, which also takes only the game's own types
// (after reopening, the first game topic showed). For Core's topics GuideTopicRecordPatch records the same
// four values itself, so reopening shows the topic last chosen.
internal static class GuideTopics
{
    internal static void Add(SubUI_TopicSelectPanel panel, Il2CppSystem.Action<GameGuideType> onSelectGuide)
    {
        var buttons = panel?.guideButtons;
        var prefab = panel?.subUI_GuideButtonPrefab;
        var holder = panel?.buttonListHolder;
        if (buttons == null || prefab == null || holder == null) return;
        var added = 0;
        foreach (var topic in GuideTopicText.Topics)
        {
            var type = (GameGuideType)topic.Id;
            if (buttons.ContainsKey(type)) continue;
            var button = UnityEngine.Object.Instantiate(prefab, holder);
            button.Init(type, onSelectGuide ?? panel._onSelectGuide);
            buttons.Add(type, button);
            panel.BuildGuideSearchCache(type);
            added++;
        }
        if (added > 0) DelversHost.Info("CORE_GUIDE_TOPICS added=" + added);
    }

    internal static void Select(GameGuidePresenter presenter, GameGuideType type)
    {
        presenter.CaptureState();
        var view = presenter.View;
        if (view == null) return;
        var content = presenter._sessionState?.GetContentScrollPosition(type) ?? 0f;
        if (view.SelectedGuide != type)
        {
            var list = view.TopicListScrollPosition;
            view.SetGuideContent(type);
            view.RestoreScrollPositions(list, content);
        }
        presenter.CaptureState();
    }

    // What RecordView keeps for one of the game's topics: the topic, the search text, both scroll positions, clamped as the game does.
    internal static void Record(GameGuideSessionState state, GameGuideType type, string searchQuery, float topicList, float content)
    {
        state.SelectedGuide = type;
        state.SearchQuery = searchQuery ?? string.Empty;
        state.TopicListScrollPosition = state.ClampScrollPosition(topicList);
        state._contentScrollPositions[type] = state.ClampScrollPosition(content);
    }
}

[HarmonyPatch(typeof(SubUI_TopicSelectPanel), nameof(SubUI_TopicSelectPanel.Init))]
internal static class GuideTopicsPatch
{
    private static bool failed;

    private static void Postfix(SubUI_TopicSelectPanel __instance, Il2CppSystem.Action<GameGuideType> onSelectGuide)
    {
        if (failed) return;
        try { GuideTopics.Add(__instance, onSelectGuide); }
        catch (Exception ex)
        {
            failed = true;
            DelversHost.Warning("CORE_GUIDE_TOPICS_FAILED: " + ex.Message);
        }
    }
}

[HarmonyPatch(typeof(GameGuidePresenter), nameof(GameGuidePresenter.OnSelectGuide))]
internal static class GuideTopicSelectPatch
{
    private static int failures;

    // The game's own topics run through unchanged.
    private static bool Prefix(GameGuidePresenter __instance, IViewUpdateData updateData)
    {
        try
        {
            var id = GuideTopicText.IdOf(updateData?.TryCast<ViewStringData>()?.Value);
            if (id == null) return true;
            GuideTopics.Select(__instance, (GameGuideType)id.Value);
            return false;
        }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_GUIDE_TOPIC_SELECT_FAILED: " + ex.Message);
            return false;
        }
    }
}

[HarmonyPatch(typeof(GameGuideSessionState), nameof(GameGuideSessionState.RecordView))]
internal static class GuideTopicRecordPatch
{
    private static int failures;

    // The game's own topics run through unchanged.
    private static bool Prefix(GameGuideSessionState __instance, GameGuideType guideType, string searchQuery, float topicListScrollPosition,
        float contentScrollPosition)
    {
        if (!GuideTopicText.IsTopic((int)guideType)) return true;
        try { GuideTopics.Record(__instance, guideType, searchQuery, topicListScrollPosition, contentScrollPosition); }
        catch (Exception ex)
        {
            if (++failures <= 3) DelversHost.Warning("CORE_GUIDE_TOPIC_RECORD_FAILED: " + ex.Message);
        }
        return false;
    }
}
