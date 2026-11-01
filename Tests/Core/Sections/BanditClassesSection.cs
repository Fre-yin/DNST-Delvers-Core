using DungeonSettlersDelvers.Core;
using static TestKit;

// Short class names and roles of the bandits.
internal static class BanditClassesSection
{
    // (17./): short class names, at most 20 characters; that they are robbers says the journal text.
    const int MaxNameLength = 20;

    internal static void Run()
    {
        // (17./): short class names, any people for three roles, the herbalist only female, humans half the time.
        {
            var names = new[] { CoreUnitRules.BladeName, CoreUnitRules.GuardName, CoreUnitRules.ArcherName, CoreUnitRules.HealerName };
            var healer = CoreUnitRules.Units.First(u => u.Role == "healer");
            var blade = CoreUnitRules.Units.First(u => u.Role == "blade");
            Func<string, int> all = _ => 5;
            var healerForms = new HashSet<string>();
            var bladeForms = new HashSet<string>();
            var humans = 0;
            for (var i = 0; i < 4000; i++)
            {
                var guid = new Guid(i, (short)(i * 7), (short)(i * 13), (byte)i, (byte)(i >> 8), 3, 4, 5, 6, 7, 8);
                healerForms.Add(CoreUnitRules.PickForm(guid, CoreUnitRules.HumanFormsFor(healer), CoreUnitRules.OtherFormsFor(healer), all));
                var form = CoreUnitRules.PickForm(guid, CoreUnitRules.HumanFormsFor(blade), CoreUnitRules.OtherFormsFor(blade), all);
                bladeForms.Add(form);
                if (Array.IndexOf(CoreUnitRules.HumanForms, form) >= 0) humans++;
            }
            Check(names.All(n => n.AllLanguages.Count == 10 && n.AllLanguages.All(t => !string.IsNullOrWhiteSpace(t) && t.Length <= MaxNameLength
                        && t.IndexOf('-') < 0 && t.IndexOf('–') < 0 && t.IndexOf('—') < 0))
                    && CoreUnitRules.BladeName.German == "Schlächter" && CoreUnitRules.GuardName.German == "Schildträger" && CoreUnitRules.ArcherName.German == "Schütze"
                    && CoreUnitRules.HealerName.German == "Kräuterkundige" && CoreUnitRules.BladeName.English == "Slayer" && CoreUnitRules.HealerName.English == "Herbalist"
                    && healerForms.SetEquals(new[] { "HumanStandardFemale", "ElfStandardFemale", "LycanFemale" })
                    && bladeForms.Contains("LizardMan") && bladeForms.Contains("LycanMale") && bladeForms.Contains("HumanFatUnisex") && bladeForms.Count == 9
                    && humans > 1800 && humans < 2200
                    && CoreUnitRules.PickForm(Guid.Empty, CoreUnitRules.HumanForms, CoreUnitRules.OtherForms, f => f == "LizardMan" ? 3 : 0) == "LizardMan"
                    && CoreUnitRules.PickForm(Guid.Empty, CoreUnitRules.HumanForms, CoreUnitRules.OtherForms, _ => 0) == null
                    && CoreUnitRules.ProfileIndexIn(Guid.Empty, 0) == -1 && CoreUnitRules.ProfileIndexIn(Guid.Empty, 7) is >= 0 and < 7
                    && CoreUnitRules.ProfilePrefixes.Length == 9 && CoreUnitRules.ProfilePrefix("LizardMan") == "UNITVISUAL_LizardMan_"
                    && CoreUnitRules.VisualTypeOf("UNITVISUAL_LizardMan_4") == "LizardMan",
                "bandits C23: short names in 10 languages, the herbalist only in the three female forms, the others in all nine, humans about half");
        }
        Check(new[] { CoreUnitRules.BladeDescription, CoreUnitRules.GuardDescription, CoreUnitRules.ArcherDescription, CoreUnitRules.HealerDescription }
                    .All(d => d.AllLanguages.Count == 10 && d.AllLanguages.All(text => !string.IsNullOrWhiteSpace(text) && text.IndexOf('-') < 0
                        && text.IndexOf('–') < 0 && text.IndexOf('—') < 0))
                && CoreUnitRules.DescKey(CoreUnitRules.Prefix + "ForestBanditGuard") == "TEXTKEY_UNIT_DelversCoreForestBanditGuard_DESC",
            "bandits: every bandit has a description in 10 languages (drafts), under the game's _DESC key");
    }
}
