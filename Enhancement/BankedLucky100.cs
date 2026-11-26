/*
name: Banked Lucky 100 Manager
description: Unbanks items already enhanced with level 100 Lucky, runs SmartEnhance for the current class, then rebanks the selected items.
tags: bank, lucky, enhancement, smart, enhance
*/
//cs_include Scripts/CoreBots.cs
//cs_include Scripts/CoreAdvanced.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Skua.Core.Interfaces;

#nullable enable

public class BankedLucky100
{
    private IScriptInterface Bot => IScriptInterface.Instance;
    private CoreBots Core => CoreBots.Instance;
    private CoreAdvanced Adv => CoreAdvanced.Instance;

    public void ScriptMain(IScriptInterface bot)
    {
        Core.SetOptions(disableClassSwap: true);

        try
        {
            Run();
        }
        finally
        {
            Core.SetOptions(false);
        }
    }

    /// <summary>
    /// Unbanks every bank item that already has a level 100 Lucky enhancement,
    /// runs SmartEnhance for the currently equipped class, and returns those
    /// items to the bank.
    /// </summary>
    private void Run()
    {
        List<int> lucky100ItemIDs = Bot.Bank.Items
            .Where(item => item != null
                && item.EnhancementLevel == 100
                && item.EnhancementPatternID == (int)EnhancementType.Lucky)
            .Select(item => item.ID)
            .ToList();

        if (lucky100ItemIDs.Count == 0)
        {
            Core.Logger("No level 100 Lucky-enhanced items were found in the bank.");
            return;
        }

        Core.Logger($"Found {lucky100ItemIDs.Count} level 100 Lucky-enhanced bank item(s).");

        Core.Unbank(lucky100ItemIDs.ToArray());

        List<int> unbankedItemIDs = lucky100ItemIDs
            .Where(itemID => Bot.Inventory.Contains(itemID))
            .ToList();

        if (unbankedItemIDs.Count == 0)
        {
            Core.Logger("None of the matching items could be moved into inventory.");
            return;
        }

        string? className = Bot.Player.CurrentClass?.Name;
        if (!string.IsNullOrWhiteSpace(className))
        {
            // SmartEnhance selects the optimal enhancement setup for this class
            // and applies it to the currently equipped gear.
            Adv.SmartEnhance(className, true);
        }
        else
            Core.Logger("Could not determine the currently equipped class. Skipping SmartEnhance.");

        // Your custom processing can go here before the selected items are banked again.

        Core.ToBank(unbankedItemIDs.ToArray());
        Core.Logger($"Finished. Returned {unbankedItemIDs.Count} selected item(s) to the bank.");
    }
}
