using Beep.ECS;
using Godot;

// DUP-14: the wallet, storage and cost totals must key every resource by ONE canonical form
// (GridIds.Normalize), so a cost of "Iron Ore" is checked, debited, stored and reserved against the
// same "iron_ore" bucket. Before the fix, GridResourceAmount.TryTotals kept spaces/dashes, so
// Wallet.Spend committed a debit to a phantom "iron ore" key while the balance lived under
// "iron_ore" (silent no-op: infinite resources), and storage keyed a third way. This probe drives
// the real components with spaced/dashed spellings and asserts they resolve to one bucket.
[GlobalClass]
public partial class ResourceWalletKeySmoke : Node
{
    public bool Run()
    {
        // --- Wallet: a spaced cost is actually debited, under the canonical key ---
        var wallet = new GridResourceWalletComponent();
        wallet.SetAmount("Iron Ore", 100);                 // stored under "iron_ore"
        bool spent = wallet.Spend(Costs(("Iron Ore", 10))); // cost id spelled with a space
        if (!spent) { wallet.Free(); return Fail("Spend('Iron Ore', 10) returned false on a funded wallet"); }
        if (wallet.GetAmount("Iron Ore") != 90)
        { int g = wallet.GetAmount("Iron Ore"); wallet.Free(); return Fail($"after Spend, GetAmount('Iron Ore') = {g}, expected 90 (the debit no-opped to a phantom key)"); }
        Godot.Collections.Dictionary state = wallet.CaptureState();
        if (state.Count != 1 || !state.ContainsKey("iron_ore"))
        { string keys = string.Join(",", state.Keys); wallet.Free(); return Fail($"CaptureState keys = [{keys}], expected exactly [iron_ore] (a phantom key was written)"); }

        // --- Wallet save round-trip: the debited balance survives, no key collision ---
        var restored = new GridResourceWalletComponent();
        restored.RestoreState(state);
        if (restored.GetAmount("iron_ore") != 90 || restored.CaptureState().Count != 1)
        { int g = restored.GetAmount("iron_ore"); wallet.Free(); restored.Free(); return Fail($"after save round-trip, GetAmount('iron_ore') = {g}, expected 90 with one key"); }
        wallet.Free();
        restored.Free();

        // --- Storage: space- and dash-spelled ids resolve to one stored bucket ---
        var storage = new GridStorageComponent { Capacity = 500 };
        int loaded = storage.Load("Iron Ore", 50);          // stored under "iron_ore"
        if (loaded != 50) { storage.Free(); return Fail($"storage.Load('Iron Ore', 50) took {loaded}, expected 50"); }
        if (!storage.CanProvide(Costs(("iron_ore", 50))))    // canonical spelling
        { storage.Free(); return Fail("storage.CanProvide('iron_ore', 50) is false against a stored 'Iron Ore' 50"); }
        if (!storage.TryConsume(Costs(("iron-ore", 50))))    // dashed spelling
        { storage.Free(); return Fail("storage.TryConsume('iron-ore', 50) is false against a stored 'Iron Ore' 50"); }
        if (storage.Stored("iron_ore") != 0)
        { int s = storage.Stored("iron_ore"); storage.Free(); return Fail($"after TryConsume, Stored('iron_ore') = {s}, expected 0 (the three spellings did not share a bucket)"); }
        storage.Free();

        // --- Storage acceptance list: an author's spaced "Iron Ore" admits a canonical query ---
        // The allowed id is deliberately NON-canonical (a space), so this only passes when the filter
        // normalises BOTH sides - an OrdinalIgnoreCase compare would keep the space and reject it.
        var filtered = new GridStorageComponent { Capacity = 500 };
        filtered.AllowedResourceIds.Add("Iron Ore");
        if (!filtered.CanAccept("iron_ore"))
        { filtered.Free(); return Fail("storage.CanAccept('iron_ore') is false though AllowedResourceIds lists 'Iron Ore'"); }
        if (filtered.CanAccept("copper"))
        { filtered.Free(); return Fail("storage.CanAccept('copper') is true though it is not in AllowedResourceIds"); }
        filtered.Free();

        // --- Hauler cargo port: the accept filter agrees with its storage twin (canonical id) ---
        // Allowed id NON-canonical (a dash) for the same reason.
        var hauler = new GridHaulerComponent();
        hauler.AllowedResourceIds.Add("Iron-Ore");
        if (!hauler.CanAccept("iron_ore"))
        { hauler.Free(); return Fail("hauler.CanAccept('iron_ore') is false though AllowedResourceIds lists 'Iron-Ore'"); }
        if (hauler.CanAccept("copper"))
        { hauler.Free(); return Fail("hauler.CanAccept('copper') is true though it is not allowed"); }
        hauler.Free();

        // --- Extractor buffer port: one cargo bucket across space/dash/canonical spellings ---
        var extractor = new GridExtractorComponent { BufferCapacity = 100 };
        int buffered = extractor.Load("Iron Ore", 30);       // fills the buffer under "iron_ore"
        if (buffered != 30) { extractor.Free(); return Fail($"extractor.Load('Iron Ore', 30) took {buffered}, expected 30"); }
        if (extractor.Stored("iron_ore") != 30)
        { int s = extractor.Stored("iron_ore"); extractor.Free(); return Fail($"extractor.Stored('iron_ore') = {s}, expected 30"); }
        if (!extractor.CanAccept("iron-ore"))
        { extractor.Free(); return Fail("extractor.CanAccept('iron-ore') is false against a buffer of 'Iron Ore'"); }
        if (extractor.Unload("iron_ore", 30) != 30)
        { extractor.Free(); return Fail("extractor.Unload('iron_ore', 30) did not release the 'Iron Ore' buffer"); }
        extractor.Free();

        GD.Print("[resource-wallet-key] OK");
        return true;
    }

    private static Godot.Collections.Array Costs(params (string Id, int Amount)[] entries)
    {
        var array = new Godot.Collections.Array();
        foreach ((string id, int amount) in entries)
            array.Add(new GridResourceAmount { ResourceId = id, Amount = amount });
        return array;
    }

    private static bool Fail(string message) { GD.PushError(message); return false; }
}
