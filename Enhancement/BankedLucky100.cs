/*
name: Banked Lucky 100 Manager
description: Processes banked level 100 Lucky-enhanced class items one at a time.
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
    /// Finds banked level 100 Lucky-enhanced class items, then processes each
    /// item individually so only one class is unbanked at a time.
    /// </summary>
    private void Run()
    {
        List<(int ID, string Name)> lucky100Classes = Bot.Bank.Items
            .Where(item => item != null
                && item.Category == ItemCategory.Class
                && item.EnhancementLevel == 100
                && item.EnhancementPatternID == (int)EnhancementType.Lucky)
            .Select(item => (item.ID, item.Name ?? $"Item {item.ID}"))
            .ToList();

        if (lucky100Classes.Count == 0)
        {
            Core.Logger("No level 100 Lucky-enhanced class items were found in the bank.");
            return;
        }

        Core.Logger($"Found {lucky100Classes.Count} level 100 Lucky-enhanced class item(s):");
        for (int i = 0; i < lucky100Classes.Count; i++)
            Core.Logger($"  {i + 1}/{lucky100Classes.Count}: {lucky100Classes[i].Name} (ID: {lucky100Classes[i].ID})");

        int processed = 0;

        foreach ((int itemID, string itemName) in lucky100Classes)
        {
            if (Bot.Bank?.Contains(itemID) != true)
            {
                Core.Logger($"Skipping {itemName}: no longer in the bank.");
                continue;
            }

            Core.Logger($"Processing {itemName} ({processed + 1}/{lucky100Classes.Count}).");

            Core.Unbank(itemID);
            Core.Sleep();

            if (!Bot.Inventory.Contains(itemID))
            {
                Core.Logger($"Failed to unbank {itemName}. Skipping it.");
                continue;
            }

            // SmartEnhance targets the class item by name and also enhances equipped gear.
            Adv.SmartEnhance(itemName, true);

            // Your custom processing can go here while this class is in inventory.

            // Core.ToBank intentionally rejects class items in the current Game4000 client.
            // Try the inventory's direct bank transfer and verify the result.
            bool banked = Bot.Inventory.EnsureToBank(itemID);
            Core.Sleep();

            if (banked || Bot.Bank?.Contains(itemID) == true)
            {
                processed++;
                Core.Logger($"Returned {itemName} to the bank.");
            }
            else
                Core.Logger($"Could not return {itemName} to the bank. It remains in inventory.");
        }

        Core.Logger($"Finished. Successfully processed and returned {processed}/{lucky100Classes.Count} class item(s).");
    }
}
