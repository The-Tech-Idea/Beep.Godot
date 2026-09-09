using Beep.ECS;
using Godot;

/// <summary>
/// One id normaliser, and the bug it fixes: a resource wallet and the strings that
/// name resources agree that "Iron Ore", "iron-ore" and "iron_ore" are one thing.
/// The wallet used to key with a normaliser that kept spaces while other callers
/// replaced them, so a cost written "Iron Ore" never found the "iron_ore" coins.
/// </summary>
public partial class GridIdsSmoke : Node
{
    public bool Run()
    {
        // The rule: trim, lower, space and dash to underscore; empty stays empty.
        if (GridIds.Normalize("Iron Ore") != "iron_ore") return Fail("space was not normalised");
        if (GridIds.Normalize("iron-ore") != "iron_ore") return Fail("dash was not normalised");
        if (GridIds.Normalize("  GRASS  ") != "grass") return Fail("trim/lower failed");
        if (GridIds.Normalize("") != "" || GridIds.Normalize("   ") != "" || GridIds.Normalize(null) != "")
            return Fail("empty did not stay empty");
        // The normaliser invents no default; the caller says the fallback.
        if (GridIds.NormalizeOr("", "grass") != "grass") return Fail("NormalizeOr lost its fallback");
        if (GridIds.NormalizeOr("Deep Water", "grass") != "deep_water") return Fail("NormalizeOr did not normalise a real value");

        // End to end: the wallet keys "iron_ore" and answers to every spelling of it.
        var wallet = new GridResourceWalletComponent { Name = "Wallet" };
        AddChild(wallet);
        wallet.AddAmount("iron_ore", 10);
        if (wallet.GetAmount("Iron Ore") != 10) return Fail("wallet did not match 'Iron Ore' against its 'iron_ore' balance");
        if (wallet.GetAmount("iron-ore") != 10) return Fail("wallet did not match 'iron-ore' against its 'iron_ore' balance");
        if (!wallet.TrySpendAmount("Iron Ore", 4)) return Fail("could not spend against a differently-spelled balance");
        if (wallet.GetAmount("iron_ore") != 6) return Fail("spend debited the wrong key");
        wallet.Free();

        GD.Print("[grid-ids] Iron Ore = iron-ore = iron_ore across the normaliser and the wallet OK");
        return true;
    }

    private static bool Fail(string message)
    {
        GD.PushError("[grid-ids] " + message);
        return false;
    }
}
