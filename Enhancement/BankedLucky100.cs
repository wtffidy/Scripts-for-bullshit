/*
name: Banked Lucky 100 Manager
description: Scans banked gear and upgrades eligible items to level 100 Lucky enhancement one at a time.
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
    /// Scans every bank item, skips items already at level 100 Lucky, and
    /// enhances each eligible remaining item individually before rebanking it.
    /// </summary>
    private void Run()
    {
        if (Bot.Player.Level < 100)
        {
            Core.Logger($"Level 100 Lucky enhancements require level 100. Current level: {Bot.Player.Level}. Stopping.");
            return;
        }

        List<(int ID, string Name, ItemCategory Category, bool AlreadyLucky100)> bankItems = Bot.Bank.Items
            .Where(item => item != null)
            .Select(item => (
                item.ID,
                item.Name ?? $"Item {item.ID}",
                item.Category,
                item.EnhancementLevel == 100
                    && item.EnhancementPatternID == (int)EnhancementType.Lucky
            ))
            .ToList();

        if (bankItems.Count == 0)
        {
            Core.Logger("Your bank is empty. Nothing to process.");
            return;
        }

        Core.Logger($"Found {bankItems.Count} item(s) in the bank:");
        foreach ((int itemID, string itemName, ItemCategory category, bool alreadyLucky100) in bankItems)
            Core.Logger($"  {itemName} (ID: {itemID}, Category: {category}){(alreadyLucky100 ? " [Already Lucky 100]" : string.Empty)}");

        List<(int ID, string Name, ItemCategory Category)> itemsToEnhance = bankItems
            .Where(item => !item.AlreadyLucky100 && Adv.EnhanceableCatagories.Contains(item.Category))
            .Select(item => (item.ID, item.Name, item.Category))
            .ToList();

        int skipped = bankItems.Count - itemsToEnhance.Count;
        Core.Logger($"Queued {itemsToEnhance.Count} item(s) for Lucky 100. Skipping {skipped} already-correct or unenhanceable item(s).");

        int enhanced = 0;
        int failed = 0;

        foreach ((int itemID, string itemName, ItemCategory category) in itemsToEnhance)
        {
            if (Bot.ShouldExit)
                break;

            if (Bot.Bank?.Contains(itemID) != true)
            {
                Core.Logger($"Skipping {itemName}: it is no longer in the bank.");
                failed++;
                continue;
            }

            Core.Logger($"[{enhanced + failed + 1}/{itemsToEnhance.Count}] Unbanking {itemName} ({category}).");
            Core.Unbank(itemID);
            Core.Sleep();

            if (!Bot.Inventory.Contains(itemID))
            {
                Core.Logger($"Failed to unbank {itemName}. Moving to the next item.");
                failed++;
                continue;
            }

            // Apply standard Lucky enhancement to this item only.
            Adv.EnhanceItem(itemName, EnhancementType.Lucky, logging: true, bypassDisableAutoEnhance: true);
            Core.Sleep();

            InventoryItem? updatedItem = Bot.Inventory.Items.FirstOrDefault(item => item.ID == itemID);
            if (updatedItem == null
                || updatedItem.EnhancementLevel != 100
                || updatedItem.EnhancementPatternID != (int)EnhancementType.Lucky)
            {
                Core.Logger($"Enhancement was not verified for {itemName}. Leaving it in inventory for review.");
                failed++;
                continue;
            }

            // Use the direct inventory transfer because Core.ToBank currently blocks class items.
            bool banked = Bot.Inventory.EnsureToBank(itemID);
            Core.Sleep();

            if (banked || Bot.Bank?.Contains(itemID) == true)
            {
                enhanced++;
                Core.Logger($"Successfully enhanced and banked {itemName}.");
            }
            else
            {
                Core.Logger($"Could not return {itemName} to the bank. It remains in inventory.");
                failed++;
            }
        }

        Core.Logger($"Finished. Enhanced and banked: {enhanced}. Failed or left for review: {failed}.");
    }
}
