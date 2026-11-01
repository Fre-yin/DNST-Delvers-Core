using DungeonSettlersDelvers.Core;
using static TestKit;

// Fixed traits of a pack profile.
internal static class FixedTraitsSection
{
    static readonly string[] FixedOrder = { "AFFECTER_Elf", "AFFECTER_ExampleBackground", "AFFECTER_ExampleIndividual" };

    internal static void Run()
    {
        MissingTraitsInOrder();
        FixedTraitRegistration();
    }

    static void MissingTraitsInOrder()
    {
        // Fixed traits: a save written while a pack was inactive keeps only the profile.
        Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_BlessOfWorldTree", "AFFECTER_FireplaceWarmth" }, FixedOrder)
                .SequenceEqual(FixedOrder.Concat(new[] { "AFFECTER_BlessOfWorldTree", "AFFECTER_FireplaceWarmth" })),
            "a wiped unit regains every fixed trait first, in registered order");
        Check(FixedTraitRules.WithMissing(Array.Empty<string>(), FixedOrder).SequenceEqual(FixedOrder),
            "an empty affecter list regains every fixed trait");
        Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_NewRecruit", "AFFECTER_Elf", "AFFECTER_Hungry" }, FixedOrder)
                .SequenceEqual(new[] { "AFFECTER_NewRecruit", "AFFECTER_Elf", "AFFECTER_ExampleBackground",
                    "AFFECTER_ExampleIndividual", "AFFECTER_Hungry" }),
            "missing fixed traits follow their present predecessor; other keys keep their order");
        Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_Elf", "AFFECTER_ExampleIndividual" }, FixedOrder)
                .SequenceEqual(FixedOrder),
            "a missing middle trait is inserted between its neighbours");
        Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_ExampleIndividual" }, FixedOrder)
                .SequenceEqual(FixedOrder),
            "leading fixed traits are inserted before the first present one without duplicates");
        Check(FixedTraitRules.WithMissing(new[] { "AFFECTER_Hungry", "AFFECTER_ExampleIndividual", "AFFECTER_Elf",
                "AFFECTER_ExampleBackground" }, FixedOrder) == null,
            "an intact unit needs no change, whatever the saved order");
        Check(FixedTraitRules.WithMissing(new[] { null, "AFFECTER_Elf", "AFFECTER_Elf" }, FixedOrder)
                .SequenceEqual(new[] { null, "AFFECTER_Elf", "AFFECTER_ExampleBackground", "AFFECTER_ExampleIndividual",
                    "AFFECTER_Elf" }),
            "unreadable and duplicate entries keep their position and anchor only once");
    }

    static void FixedTraitRegistration()
    {
        var fixedTraits = new FixedTraitRegistry();
        var exampleFixedLease = fixedTraits.Register("example-pack", "UNITVISUAL_Example", FixedOrder);
        var duplicateFixedLease = fixedTraits.Register("example-pack", "UNITVISUAL_Example", FixedOrder.ToList());
        Check(fixedTraits.TryGet("UNITVISUAL_Example", out var fixedPack, out var registeredTraits)
                && fixedPack == "example-pack" && registeredTraits.SequenceEqual(FixedOrder) && fixedTraits.Count == 1,
            "a pack registers ordered fixed traits for its profile; a repeated identical registration is leased");
        static bool RegisterFails(FixedTraitRegistry registry, string packId, string profileKey, string[] traits)
        {
            try { registry.Register(packId, profileKey, traits).Dispose(); return false; }
            catch (ArgumentException) { return true; }
            catch (InvalidOperationException) { return true; }
        }
        Check(RegisterFails(fixedTraits, "other-pack", "UNITVISUAL_Example", FixedOrder)
                && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Example", new[] { "AFFECTER_Elf" }),
            "another pack or different traits cannot take over a registered profile");
        Check(RegisterFails(fixedTraits, "Bad Pack", "UNITVISUAL_Other", FixedOrder)
                && RegisterFails(fixedTraits, "example-pack", "", FixedOrder)
                && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Other", Array.Empty<string>())
                && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Other", new[] { "AFFECTER_Elf", "AFFECTER_Elf" })
                && RegisterFails(fixedTraits, "example-pack", "UNITVISUAL_Other", new[] { "AFFECTER_Elf", " " }),
            "invalid pack IDs, profiles and trait lists are rejected");
        Check(fixedTraits.Count == 1 && fixedTraits.ShouldReportFailure("UNITVISUAL_Example")
                && !fixedTraits.ShouldReportFailure("UNITVISUAL_Example")
                && !fixedTraits.ShouldReportFailure("UNITVISUAL_Unregistered"),
            "a failing restore is reported once per registered profile");
        duplicateFixedLease.Dispose();
        Check(fixedTraits.TryGet("UNITVISUAL_Example", out _, out _), "one released lease keeps a duplicate registration");
        exampleFixedLease.Dispose();
        exampleFixedLease.Dispose();
        Check(fixedTraits.Count == 0 && !fixedTraits.TryGet("UNITVISUAL_Example", out _, out _),
            "the last released lease removes the registration; double dispose is harmless");
        var fixedCleanupLease = fixedTraits.Register("example-pack", "UNITVISUAL_Example", FixedOrder);
        fixedTraits.Clear();
        fixedCleanupLease.Dispose();
        Check(fixedTraits.Count == 0, "Core shutdown cleanup clears fixed traits; a later lease release is harmless");
    }
}
