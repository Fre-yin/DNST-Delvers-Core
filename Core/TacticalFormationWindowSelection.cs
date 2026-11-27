using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DungeonSettlersDelvers.Core;

// The fixed selection of the formation window. Each of the book's sets shows its own rows, whoever is selected
// or on the map: the add button opens the choice of every living character with where it is and of the summon kinds of the set's
// summoners, a right click on a row takes it out, a summoner with its summon places. A member who is not on the shown map stays in the list and the grid, faded, its
// place kept; the group AI passes over it, and while the leader is away the top member there is the middle (TacticalFormationBook.
// Apply). A member who died falls out of the shown set, as the book drops it on saving. Each summon place (summoner, kind, number;
// TacticalFormationSummons) is a row of its own with the summon's sprite from the game as a still picture and its number; a place
// whose summon is not alive now is faded. A member down at near-death stays faded on its place, reserved, and takes it again once
// rescued; a member who died falls out. A set without rows starts with the group the window opened for.
internal static partial class TacticalFormationWindow
{
    // The choice of what to add shows tiles, two in a line, the picture above the name.
    private const float TileWidth = 132f, TileHeight = 88f, TileGap = 6f, TileIcon = 36f;
    private const int MaxTileLines = 3;
    // The add choice's width while it is built: AddWidth, wider by the bar when the tiles scroll.
    private static float addWidth = AddWidth;
    private const float AddWidth = 2f * TileWidth + 3f * TileGap, AddHeight = 34f, AddIcon = 28f;
    // A member away or a summon place without its summon: the picture faded.
    private static readonly Color AwayTint = new(1f, 1f, 1f, 0.4f);
    private static readonly Color AwayText = new(0.8f, 0.75f, 0.72f, 0.55f);

    private static readonly Dictionary<Guid, TacticalFormationSummons.Place> summonPlaces = new();
    private static readonly Dictionary<Guid, UnitEntity> liveSummons = new();
    private static readonly Dictionary<Guid, MapType> awayAt = new();
    private static readonly HashSet<Guid> down = new();
    // The casts of every member seen while the window is open, so a set's page keeps those edited on another.
    private static readonly Dictionary<Guid, string[]> castsSeen = new();
    // A kind's picture and name for the session, from a living summon; a place without one shows them.
    private static readonly Dictionary<string, Sprite> kindFaces = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> kindNames = new(StringComparer.Ordinal);
    private static readonly List<TMP_Text> cellNumbers = new();
    private static GameObject adder;

    // Summoning skills the catalogue lacks, each named once per session (TacticalFormationSummonCatalog).
    private static readonly HashSet<string> unknownSummonSkills = new(StringComparer.Ordinal);

    private static void ClearSelection()
    {
        summonPlaces.Clear();
        liveSummons.Clear();
        awayAt.Clear();
        down.Clear();
        castsSeen.Clear();
        cellNumbers.Clear();
        adder = null;
    }

    private static void ForgetKinds()
    {
        kindFaces.Clear();
        kindNames.Clear();
        unknownSummonSkills.Clear();
    }

    // ---- the grid of a set ----

    // The grid of a set: its rows where it has some, the members who died left out; else the group the window opened for.
    private static TacticalFormationGrid GridFor(int index)
    {
        summonPlaces.Clear();
        liveSummons.Clear();
        awayAt.Clear();
        down.Clear();
        var set = Living(book.Sets[index]);
        if (set == null)
        {
            foreach (var unit in order) Note(unit);
            return TacticalFormationGrid.Create(order, Guid.Empty, null, Casts(order));
        }
        var members = set.Members();
        foreach (var unit in members) Note(unit);
        foreach (var (place, summon) in TacticalFormation.SummonsOf(members))
        {
            liveSummons[place.Id] = summon;
            SeeKind(place.Kind, summon);
        }
        var saved = new List<TacticalFormationRules.Slot>(set.Places);
        var ids = new List<Guid>();
        foreach (var summon in set.Summons)
        {
            // A place whose summoner is no member of the set (left from before a summoner took its places along, RemoveRow) is not taken,
            // and so leaves the saved set with the next close of the window (StorePage).
            if (!members.Contains(summon.Summoner)) continue;
            summonPlaces[summon.Id] = summon;
            saved.Add(new TacticalFormationRules.Slot(summon.Id, summon.X, summon.Y));
            ids.Add(summon.Id);
        }
        return TacticalFormationGrid.Create(members, set.Leader, saved, Casts(members), ids, set.Roles);
    }

    // The set without the members who died (a dead leader hands the crown to the next member, TacticalFormationBook.Drop); null
    // when nobody is left. A member no loaded map holds counts as living, away.
    private static TacticalFormationBook.Set Living(TacticalFormationBook.Set set)
    {
        if (set == null) return null;
        var copy = new TacticalFormationBook();
        copy.Sets[0] = new TacticalFormationBook.Set(set.Leader, set.Places, set.Summons, set.Roles);
        copy.Drop(unit => !TacticalFormation.Locate(unit).Dead);
        return copy.Sets[0];
    }

    // A member's unit for its picture and name, wherever it is; away when not on the shown map.
    private static void Note(Guid member)
    {
        var (unit, map, here, _) = TacticalFormation.Locate(member);
        if (unit != null) units[member] = unit;
        if (!here) awayAt[member] = map;
        if (TacticalFormation.IsDown(unit)) down.Add(member);
    }

    private static Dictionary<Guid, string[]> Casts(IEnumerable<Guid> members)
    {
        var saved = TacticalFormation.AllPriorityCasts();
        var result = new Dictionary<Guid, string[]>();
        foreach (var unit in members)
            if (castsSeen.TryGetValue(unit, out var seen) || saved.TryGetValue(unit, out seen)) result[unit] = seen;
        return result;
    }

    // The shown grid's casts kept before it is left.
    private static void KeepCasts()
    {
        if (grid == null) return;
        var casts = grid.AllCasts();
        foreach (var unit in grid.Members) castsSeen[unit] = casts.TryGetValue(unit, out var keys) ? keys : new string[TacticalFormationGrid.CastSlots];
    }

    // The grid's summon places with their cells, for the book.
    private static List<TacticalFormationSummons.Place> SummonPlacesOf(TacticalFormationGrid shown)
    {
        var result = new List<TacticalFormationSummons.Place>();
        foreach (var slot in shown.SummonSlots())
            if (summonPlaces.TryGetValue(slot.Unit, out var place))
                result.Add(new TacticalFormationSummons.Place(place.Summoner, place.Kind, place.Number, slot.X, slot.Y));
        return result;
    }

    // ---- what a row or cell shows ----

    private static bool IsAway(Guid unit) => summonPlaces.ContainsKey(unit) ? !liveSummons.ContainsKey(unit) : awayAt.ContainsKey(unit) || down.Contains(unit);

    // A member's portrait; a summon place's picture is the kind's (a summon has no portrait, and the game logs an error for each
    // ask).
    private static Sprite FaceOf(Guid unit)
        => summonPlaces.TryGetValue(unit, out var place) ? KindFace(place.Kind) : PortraitOf(unit);

    private static string RowName(Guid unit)
        => summonPlaces.TryGetValue(unit, out var place) ? KindName(place.Kind) + " " + place.Number : NameOf(unit);

    // The line below a row's name: where an absent member is, who summoned a summon.
    private static string RowNote(Guid unit)
    {
        if (summonPlaces.TryGetValue(unit, out var place))
            return string.Format(MenuText.Pick(TacticalFormationText.SummonedBy), NameOf(place.Summoner));
        if (down.Contains(unit)) return MenuText.Pick(TacticalFormationText.Fallen);
        if (!awayAt.TryGetValue(unit, out var map)) return "";
        return MenuText.Pick(TacticalFormationText.Away) + ": " + MenuText.Pick(map == MapType.Dungeon ? TacticalFormationText.InDungeon : TacticalFormationText.InVillage);
    }

    // A kind's still picture: the sprite the game draws a living summon of it with (the summon's sprite, no
    // portrait of its own), seen this session; else the summoning skill's icon, else none (the row keeps its name).
    private static Sprite KindFace(string kind)
    {
        if (kindFaces.TryGetValue(kind, out var face) && face != null) return face;
        var skill = TacticalFormationSummonCatalog.SkillOf(kind);
        return skill != null ? SkillIcon(skill) : null;
    }

    // A kind's name: a living summon's, seen this session; before that the game's text for the unit key, when it has one under
    // the usual keys; else the key without its "UNIT_".
    private static string KindName(string kind)
    {
        if (kindNames.TryGetValue(kind, out var name)) return name;
        try
        {
            var table = DataSheetManager.Instance?._text?._textTable;
            if (table != null)
                foreach (var key in new[] { "TEXTKEY_" + kind + "_NAME", "TEXTKEY_" + kind })
                    if (table.TryGetValue(key, out var text) && !string.IsNullOrWhiteSpace(text)) return text;
        }
        catch (Exception) { /* The key below is still a name. */ }
        return kind.StartsWith("UNIT_", StringComparison.Ordinal) ? kind.Substring(5) : kind;
    }

    // A living summon's sprite and name for its kind: the sprite of its graphic's own renderer (the body), else the largest sprite its
    // object draws. The largest one was a small blue mark; CORE_TACTICAL_SUMMON_FACE names the
    // renderers once per kind, so the choice can be checked. A frame of the summoning (Unit_Bubbleling_Summon_*, right after the
    // cast, the creature barely visible) is never kept; the skill's icon stands in until the creature is seen (KindFace).
    private static readonly HashSet<string> faceLogged = new(StringComparer.Ordinal);

    private static bool IsSummoningFrame(Sprite sprite) => sprite != null && sprite.name.IndexOf("_Summon", StringComparison.OrdinalIgnoreCase) >= 0;

    private static void SeeKind(string kind, UnitEntity summon)
    {
        try
        {
            var name = NameOfUnit(summon);
            if (name != "?") kindNames[kind] = name;
            if (kindFaces.TryGetValue(kind, out var known) && known != null && !IsSummoningFrame(known)) return;
            var entityObject = BanditTint.EntityObjectOf(summon.Guid);
            var own = entityObject?.Graphic?._renderer;
            var body = entityObject?.Transform;
            Sprite best = own != null ? own.sprite : null;
            var area = 0f;
            var seen = new List<string>();
            foreach (var renderer in body != null ? body.GetComponentsInChildren<SpriteRenderer>(true) : Array.Empty<SpriteRenderer>())
            {
                var sprite = renderer?.sprite;
                seen.Add((renderer?.name ?? "?") + ":" + (sprite != null ? sprite.name + ":" + sprite.rect.width.ToString("0") + "x" + sprite.rect.height.ToString("0") : "-")
                         + ":" + (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy ? 1 : 0));
                if (own != null && best != null) continue;
                if (sprite == null) continue;
                var size = sprite.rect.width * sprite.rect.height;
                if (size <= area) continue;
                best = sprite;
                area = size;
            }
            if (faceLogged.Add(kind + (IsSummoningFrame(best) ? "" : "|kept")))
                DelversHost.Info($"CORE_TACTICAL_SUMMON_FACE kind={kind} own={(own != null ? own.name + ":" + (own.sprite != null ? own.sprite.name : "-") : "-")} picked={(best != null ? best.name : "-")} renderers={string.Join(",", seen)}");
            // A frame of the summoning is never kept: cut, the first one is a few pixels of light (Unit_Bubbleling_Summon_0, 2x2,
            // P08e run on f23e488). The kind keeps its skill's icon until a later look sees the creature itself.
            if (best != null && !IsSummoningFrame(best)) kindFaces[kind] = Cropped(best);
        }
        catch (Exception ex) { TacticalFormationButton.ReportOnce("CORE_TACTICAL_SUMMON_FACE_FAILED: " + ex.Message); }
    }

    // The sprite cut to what it draws: a bubbleling fills only a small part of its 64 by 64 frame, and its
    // outline is the whole frame. The frame is copied once per kind through a render texture, which reads
    // also a texture the game keeps unreadable, and cut to its pixels that are not clear; the sprite as it is when that fails.
    private static Sprite Cropped(Sprite sprite)
    {
        RenderTexture copy = null;
        var before = RenderTexture.active;
        try
        {
            var texture = sprite.texture;
            var area = sprite.textureRect;
            int w = (int)area.width, h = (int)area.height;
            if (texture == null || w < 2 || h < 2) return sprite;
            copy = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(texture, copy);
            RenderTexture.active = copy;
            var frame = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            frame.ReadPixels(new Rect(area.x, area.y, w, h), 0, 0);
            frame.Apply();
            var pixels = frame.GetPixels32();
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a < 16) continue;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            if (maxX < minX || maxY < minY) return sprite;
            var cut = Sprite.Create(frame, new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
            cut.name = sprite.name + "_Cut";
            frame.hideFlags = HideFlags.DontUnloadUnusedAsset;
            cut.hideFlags = HideFlags.DontUnloadUnusedAsset;
            DelversHost.Info($"CORE_TACTICAL_SUMMON_CROP sprite={sprite.name} frame={w}x{h} cut={maxX - minX + 1}x{maxY - minY + 1}");
            return cut;
        }
        catch (Exception ex)
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_SUMMON_CROP_FAILED: " + ex.Message);
            return sprite;
        }
        finally
        {
            RenderTexture.active = before;
            if (copy != null) RenderTexture.ReleaseTemporary(copy);
        }
    }

    // The small number at a summon's picture in a grid cell.
    private static TMP_Text NumberBadge(Transform parent)
    {
        var clone = DelversMenuWindow.Clone(uiParts.BodyText, parent, "Number");
        var text = clone.GetComponent<TMP_Text>();
        text.enableWordWrapping = false;
        text.enableAutoSizing = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.fontSize = 16f;
        text.alignment = TextAlignmentOptions.BottomRight;
        text.color = FrontColor;
        (clone.GetComponent<LayoutElement>() ?? clone.AddComponent<LayoutElement>()).ignoreLayout = true;
        var rect = clone.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-2f, 0f);
        DelversMenuWindow.SetText(text, "");
        clone.SetActive(true);
        return text;
    }

    private static string NumberText(Guid unit) => summonPlaces.TryGetValue(unit, out var place) ? place.Number.ToString() : "";

    // ---- adding and taking out ----

    // A character added to the shown set: a member wherever it is (away when not on the shown map), with its saved casts
    // (a member taken out and added again had lost them).
    internal static bool AddMember(Guid member)
    {
        if (grid == null || !grid.Add(member)) return false;
        Note(member);
        if (Casts(new[] { member }).TryGetValue(member, out var keys))
            for (var place = 0; place < keys.Length; place++) grid.SetCast(member, place, keys[place]);
        DelversHost.Info($"CORE_TACTICAL_ADDED unit={member:D} away={(awayAt.ContainsKey(member) ? 1 : 0)} rows={grid.Count}");
        CloseAdder();
        Refresh();
        return true;
    }

    // A summon place added for a summoner of the set and a kind, with the lowest number the set's places of both leave free.
    internal static bool AddSummon(Guid summoner, string kind)
    {
        if (grid == null || string.IsNullOrEmpty(kind) || !grid.Members.Contains(summoner)) return false;
        if (!TacticalFormationSummonCatalog.CanPlanMore(kind, summonPlaces.Values.Count(place => place.Summoner == summoner && place.Kind == kind))) return false;
        var number = TacticalFormationSummons.NextNumber(summonPlaces.Values, summoner, kind);
        var place = new TacticalFormationSummons.Place(summoner, kind, number, 0, 0);
        if (!grid.AddSummon(place.Id)) return false;
        summonPlaces[place.Id] = place;
        foreach (var (live, summon) in TacticalFormation.SummonsOf(grid.Members))
            if (live.Id == place.Id) liveSummons[place.Id] = summon;
        DelversHost.Info($"CORE_TACTICAL_ADDED summon={kind} of={summoner:D} n={number} rows={grid.Count}");
        CloseAdder();
        Refresh();
        return true;
    }

    // A row taken out (a right click); the crown passes to the next member.
    internal static bool RemoveRow(Guid unit)
    {
        if (grid != null && grid.Members.Contains(unit))
            castsSeen[unit] = grid.Casts(unit) is IReadOnlyList<string> keys ? new List<string>(keys).ToArray() : new string[TacticalFormationGrid.CastSlots];
        var member = grid != null && grid.Members.Contains(unit);
        if (grid == null || !grid.Remove(unit)) return false;
        summonPlaces.Remove(unit);
        liveSummons.Remove(unit);
        // A summoner taken out takes its summon places along; they would stay as rows no summon ever fills, and their numbers
        // would count for the next places (NextNumber).
        if (member)
            foreach (var place in TacticalFormationSummons.GoWith(new List<TacticalFormationSummons.Place>(summonPlaces.Values), unit, grid.Members.Count > 0))
            {
                grid.Remove(place);
                summonPlaces.Remove(place);
                liveSummons.Remove(place);
            }
        DelversHost.Info($"CORE_TACTICAL_REMOVED unit={unit:D} rows={grid.Count} leader={grid.Leader:D}");
        CloseAdder();
        Refresh();
        return true;
    }

    // What the add button offers: the living characters not in the set with where they are, then the summon kinds of the set's summoners
    // (from their living summons, the set's places and their summoning skills), each as "kind|summoner", while the summoner may
    // have another place of that kind.
    internal static List<(string Key, Sprite Face, string Name, string Note)> Candidates()
    {
        var result = new List<(string, Sprite, string, string)>();
        if (grid == null) return result;
        foreach (var (unit, map) in TacticalFormation.PartyEverywhere())
        {
            var guid = CoreGame.GuidOf(unit);
            if (grid.Members.Contains(guid)) continue;
            units[guid] = unit;
            var where = map == CoreGame.CurrentMap ? "" : MenuText.Pick(map == MapType.Dungeon ? TacticalFormationText.InDungeon : TacticalFormationText.InVillage);
            result.Add((guid.ToString("D"), PortraitOf(guid), NameOf(guid), where));
        }
        var kinds = new List<(Guid Summoner, string Kind)>();
        foreach (var (place, summon) in TacticalFormation.SummonsOf(grid.Members))
        {
            SeeKind(place.Kind, summon);
            if (!kinds.Contains((place.Summoner, place.Kind))) kinds.Add((place.Summoner, place.Kind));
        }
        foreach (var place in summonPlaces.Values)
            if (grid.Members.Contains(place.Summoner) && !kinds.Contains((place.Summoner, place.Kind))) kinds.Add((place.Summoner, place.Kind));
        // The kinds a member can summon by its learned skills, also before one lives (P08e of the 0.5.2 hand test): a place
        // planned so stays faded until its summon comes and takes it by number (TacticalFormation.SummonsOf).
        foreach (var member in grid.Members)
            foreach (var kind in SummonKindsOf(member))
                if (!kinds.Contains((member, kind))) kinds.Add((member, kind));
        foreach (var (summoner, kind) in kinds)
        {
            // No more places of a kind than the game lets one summoner have (TacticalFormationSummonCatalog.PerSummoner).
            var placed = summonPlaces.Values.Count(place => place.Summoner == summoner && place.Kind == kind);
            if (!TacticalFormationSummonCatalog.CanPlanMore(kind, placed)) continue;
            result.Add((kind + "|" + summoner.ToString("D"), KindFace(kind), KindName(kind), string.Format(MenuText.Pick(TacticalFormationText.SummonedBy), NameOf(summoner))));
        }
        return result;
    }

    // The summon kinds of a member's learned skills; a summoning skill the catalogue lacks is named once.
    private static List<string> SummonKindsOf(Guid member)
    {
        try
        {
            if (!units.TryGetValue(member, out var unit) || unit == null) return new List<string>();
            var kinds = TacticalFormationSummonCatalog.KindsFor(TacticalFormation.ActiveSkills(unit), out var unknown);
            foreach (var skill in unknown)
                if (unknownSummonSkills.Add(skill)) DelversHost.Info($"CORE_TACTICAL_SUMMON_SKILL_UNKNOWN skill={skill} unit={member:D}");
            return kinds;
        }
        catch (Exception ex)
        {
            TacticalFormationButton.ReportOnce("CORE_TACTICAL_SUMMON_KINDS_FAILED: " + ex.Message);
            return new List<string>();
        }
    }

    // A candidate's key from Candidates added.
    internal static bool AddCandidate(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        var bar = key.IndexOf('|');
        if (bar < 0) return Guid.TryParse(key, out var member) && AddMember(member);
        return Guid.TryParse(key.Substring(bar + 1), out var summoner) && AddSummon(summoner, key.Substring(0, bar));
    }

    // The choice behind the add button, tiles below the free row like the casts' choice below its place, above it when it would run
    // past the content box (KeepInside).
    private static void OpenAdder(RectTransform rowRect)
    {
        ClosePicker();
        CloseAdder();
        if (grid == null || grid.Count >= TacticalFormationGrid.MaxRows || rowRect == null) return;
        var candidates = Candidates();
        pickerBlocker = new GameObject("AddBlocker", Il2CppType.Of<RectTransform>());
        pickerBlocker.transform.SetParent(root.transform, false);
        var blockerRect = pickerBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = blockerRect.offsetMax = Vector2.zero;
        pickerBlocker.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
        DelversMenuWindow.EnsureButton(pickerBlocker).onClick.AddListener((Action)CloseAdder);

        adder = new GameObject("AddChoice", Il2CppType.Of<RectTransform>());
        adder.transform.SetParent(root.transform, false);
        var layout = adder.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        var fitter = adder.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var rect = adder.GetComponent<RectTransform>();
        rect.pivot = new Vector2(0f, 1f);
        rect.position = rowRect.position;
        // Right below the free row with the add button, its left edge on the list's left edge, as the game's dropdowns hang below
        // what opens them (the author's hand test, it hung at the row's right over the grid); never into the grid (KeepInside).
        rect.anchoredPosition += new Vector2(-RowWidth * 0.5f, -EntryHeight * 0.5f) * ScaleLikeWindow(rect);

        var lines = (candidates.Count + 1) / 2;
        var tilesHeight = lines * TileHeight + Math.Max(0, lines - 1) * TileGap + 2f * TileGap;
        // More tiles than MaxTileLines lines scroll inside the choice, so it stays within the content box (KeepInside): half a line more
        // shows that more follow, and a copy of the options window's scroll bar stands at the right; the title, the hint and the end
        // line are then as wide as the tiles with the bar.
        var shownHeight = Math.Min(tilesHeight, (MaxTileLines + 0.5f) * (TileHeight + TileGap) + TileGap);
        var barWidth = 0f;
        if (tilesHeight > shownHeight && listBar != null)
        {
            var barRect = listBar.GetComponent<RectTransform>();
            barWidth = barRect != null && barRect.sizeDelta.x > 0f ? barRect.sizeDelta.x : 12f;
        }
        addWidth = AddWidth + (barWidth > 0f ? barWidth + TileGap : 0f);
        AddLine(null, MenuText.Pick(TacticalFormationText.AddTitle), FrontColor, null);
        Transform tilesParent = adder.transform;
        ScrollRect tilesScroll = null;
        if (candidates.Count > 0 && tilesHeight > shownHeight)
        {
            var view = new GameObject("TilesScroll", Il2CppType.Of<RectTransform>());
            view.transform.SetParent(adder.transform, false);
            AddChoiceGround(view);
            view.AddComponent<RectMask2D>();
            Fixed(view, addWidth, shownHeight);
            tilesScroll = view.AddComponent<ScrollRect>();
            tilesScroll.horizontal = false;
            tilesScroll.vertical = true;
            tilesScroll.movementType = ScrollRect.MovementType.Clamped;
            tilesScroll.scrollSensitivity = TileHeight * 0.5f;
            tilesScroll.viewport = view.GetComponent<RectTransform>();
            tilesParent = view.transform;
        }
        // Without anyone to add only the title and the hint show, no empty dark band between them.
        var tiles = new GameObject("Tiles", Il2CppType.Of<RectTransform>());
        tiles.transform.SetParent(tilesParent, false);
        if (tilesScroll == null) AddChoiceGround(tiles);
        else tiles.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        tiles.SetActive(candidates.Count > 0);
        var tileGrid = tiles.AddComponent<GridLayoutGroup>();
        tileGrid.cellSize = new Vector2(TileWidth, TileHeight);
        tileGrid.spacing = new Vector2(TileGap, TileGap);
        tileGrid.padding = new RectOffset((int)TileGap, (int)TileGap, (int)TileGap, (int)TileGap);
        tileGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        tileGrid.constraintCount = 2;
        // A single tile in the middle, not docked at the left with empty room beside it (the author's hand test).
        tileGrid.childAlignment = TextAnchor.UpperCenter;
        if (tilesScroll == null) Fixed(tiles, AddWidth, tilesHeight);
        else
        {
            var tilesRect = tiles.GetComponent<RectTransform>();
            tilesRect.anchorMin = tilesRect.anchorMax = new Vector2(0f, 1f);
            tilesRect.pivot = new Vector2(0f, 1f);
            tilesRect.anchoredPosition = Vector2.zero;
            tilesRect.sizeDelta = new Vector2(AddWidth, tilesHeight);
            tilesScroll.content = tilesRect;
            if (listBar != null)
            {
                var copy = DelversMenuWindow.Clone(listBar.transform, tilesScroll.transform, "Scrollbar");
                var barRect = copy.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(1f, 0f);
                barRect.anchorMax = Vector2.one;
                barRect.pivot = new Vector2(1f, 1f);
                barRect.sizeDelta = new Vector2(barWidth, 0f);
                barRect.anchoredPosition = new Vector2(-ChoiceSide, 0f);
                tilesScroll.verticalScrollbar = copy.GetComponent<Scrollbar>();
                tilesScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
                copy.SetActive(true);
            }
        }
        foreach (var (key, face, name, note) in candidates) AddTile(tiles.transform, face, name, note, () => AddCandidate(key));
        AddLine(null, "<size=80%><i>" + MenuText.Pick(TacticalFormationText.RemoveHint) + "</i></size>", AwayText, null);
        AddChoiceEnd(adder.transform, addWidth);
        // A solid ground behind the whole choice, inside the lines' margins: opened above its row it lies over the list, and the
        // rows shone through its title line (P08e run on f23e488).
        AddChoiceGround(adder);
        var ground = adder.transform.Find("Ground");
        ground.GetComponent<Image>().color = new Color(TilesGround.r, TilesGround.g, TilesGround.b, 1f);
        // It ends above the end piece, which draws the choice's bottom edge itself.
        if (uiParts?.DropdownEnd != null) ground.GetComponent<RectTransform>().offsetMin = new Vector2(ChoiceSide, 4f);
        adder.transform.SetAsLastSibling();
        KeepInside(adder.GetComponent<RectTransform>(), rowRect, true);
        DelversHost.Info($"CORE_TACTICAL_ADD_OFFERED candidates={candidates.Count} rows={grid.Count}");
    }

    // One tile of the choice: the options window's list row as its ground, the picture at its top, the name below and where the
    // character is or who summons it smaller under that.
    private static void AddTile(Transform parent, Sprite face, string name, string note, Action pick)
    {
        GameObject tile;
        if (uiParts?.DropdownRow != null) tile = DelversMenuWindow.Clone(uiParts.DropdownRow, parent, "Add");
        else
        {
            tile = new GameObject("Add", Il2CppType.Of<RectTransform>());
            tile.transform.SetParent(parent, false);
            tile.AddComponent<Image>().color = CastColor;
        }
        if (face != null)
        {
            var icon = Picture(tile.transform, "Icon", face, TileIcon);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -6f);
        }
        var label = tile.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.enableWordWrapping = true;
            label.richText = true;
            label.color = Beige;
            label.alignment = TextAlignmentOptions.Top;
            label.margin = Vector4.zero;
            var place = label.rectTransform;
            place.anchorMin = Vector2.zero;
            place.anchorMax = new Vector2(1f, 0f);
            place.pivot = new Vector2(0.5f, 0f);
            place.sizeDelta = new Vector2(-8f, TileHeight - TileIcon - 10f);
            place.anchoredPosition = new Vector2(0f, 2f);
            DelversMenuWindow.SetText(label, note.Length > 0 ? name + "\n<size=75%><i>" + note + "</i></size>" : name);
        }
        DelversMenuWindow.EnsureButton(tile).onClick.AddListener(pick);
        tile.SetActive(true);
    }

    // One line of the choice: the options window's list row with a picture at its left and the text beside it; a line without an
    // action is a title or a hint.
    private static void AddLine(Sprite face, string text, Color color, Action pick)
    {
        GameObject line;
        if (uiParts?.DropdownRow != null) line = DelversMenuWindow.Clone(uiParts.DropdownRow, adder.transform, "Add");
        else
        {
            line = new GameObject("Add", Il2CppType.Of<RectTransform>());
            line.transform.SetParent(adder.transform, false);
            line.AddComponent<Image>().color = CastColor;
        }
        Fixed(line, addWidth, AddHeight);
        if (face != null)
        {
            var icon = Picture(line.transform, "Icon", face, AddIcon);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(6f, 0f);
        }
        var label = line.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.enableWordWrapping = false;
            label.richText = true;
            label.color = color;
            label.margin = new Vector4(face != null ? AddIcon + 12f : 8f, 0f, 4f, 0f);
            DelversMenuWindow.SetText(label, text);
        }
        var button = DelversMenuWindow.EnsureButton(line);
        if (pick != null) button.onClick.AddListener(pick);
        else button.interactable = false;
        line.SetActive(true);
    }

    private static void CloseAdder()
    {
        foreach (var open in new[] { adder, pickerBlocker })
        {
            if (open == null) continue;
            open.SetActive(false);
            UnityEngine.Object.Destroy(open);
        }
        adder = pickerBlocker = null;
    }

    // A click on a list row: the left button on the free row (beside its add button) opens the choice, the right button on a filled
    // row takes it out.
    private static void ClickRow(int index, PointerEventData pointer, RectTransform rowRect)
    {
        if (grid == null || pointer == null) return;
        if (pointer.button == PointerEventData.InputButton.Right)
        {
            if (RowUnit(index) is { } unit && unit != Guid.Empty) RemoveRow(unit);
            return;
        }
        if (pointer.button == PointerEventData.InputButton.Left && index >= grid.Count) OpenAdder(rowRect);
    }

    // The unit or summon place of a list row: each member with its summon places below it (TacticalFormationListOrder).
    private static Guid? RowUnit(int index)
    {
        var shownRows = ListOrder();
        return index >= 0 && index < shownRows.Count ? shownRows[index].Unit : null;
    }

    private static string NameOfUnit(UnitEntity entity)
    {
        if (entity == null) return "?";
        var key = entity.NameTextKey;
        try
        {
            var table = DataSheetManager.Instance?._text?._textTable;
            if (table != null && key != null && table.TryGetValue(key, out var name) && !string.IsNullOrWhiteSpace(name)) return name;
        }
        catch (Exception) { /* The key below is still a name. */ }
        return string.IsNullOrWhiteSpace(key) ? "?" : key;
    }
}
