/*
name: Find Top 3 Best Server Pings
description: measures AQW handshake latency and logs the three fastest servers ( diagnostics > logs )
tags: server, ping, ultra
*/

//cs_include Scripts/CoreBots.cs

using Skua.Core.Interfaces;

public class FindBestPing
{
    private IScriptInterface Bot => IScriptInterface.Instance;
    private CoreBots Core => CoreBots.Instance;

    public async Task ScriptMain(IScriptInterface Bot)
    {
        Core.SetOptions(disableClassSwap: true);

        try
        {
            string result = await Core.FindBestServer();
            Bot.Log(result);
        }
        finally
        {
            Core.SetOptions(false);
        }
    }


}



