/*
name: Find Top 3 Best Server Pings
description: measures AQW server latency using the same concurrent verChk/apiOK handshake as the Manager
tags: server, ping, ultra
*/

//cs_include Scripts/CoreBots.cs

using Skua.Core.Interfaces;
using Skua.Core.Models.Servers;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

public class FindBestPing
{
    private const int TimeoutMs = 2500;
    private const int SampleCount = 3;
    private static readonly byte[] VersionCheck = Encoding.UTF8.GetBytes("<msg t='sys'><body action='verChk' r='0'><ver v='165' /></body></msg>\\0");

    private IScriptInterface Bot => IScriptInterface.Instance;
    private CoreBots Core => CoreBots.Instance;

    public async Task ScriptMain(IScriptInterface Bot)
    {
        Core.SetOptions(disableClassSwap: true);

        try
        {
            string result = await FindTopThreeServers();
            Bot.Log(result);
        }
        finally
        {
            Core.SetOptions(false);
        }
    }

    /// <summary>
    /// Finds the three fastest game servers using the Manager's concurrent AQW handshake measurement.
    /// Each server receives three samples, and its median successful latency is used for ranking.
    /// </summary>
    private async Task<string> FindTopThreeServers()
    {
        List<Server> servers = await Bot.Servers.GetServers(true);
        Task<(Server Server, long Ping)?>[] measurements = servers
            .Select(MeasureServerAsync)
            .ToArray();

        (Server Server, long Ping)?[] measuredServers = await Task.WhenAll(measurements);

        List<(Server Server, long Ping)> bestServers = measuredServers
            .Where(result => result.HasValue)
            .Select(result => result.GetValueOrDefault())
            .OrderBy(result => result.Ping)
            .ThenBy(result => result.Server.Name, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (bestServers.Count == 0)
            return "Could not determine an optimal server, all hosts failed their AQW handshake.";

        return $"Top {bestServers.Count} servers by AQW handshake latency:{Environment.NewLine}" +
            string.Join(Environment.NewLine, bestServers.Select((result, index) =>
                $"{index + 1}. {result.Server.Name} ({result.Server.IP}) - {result.Ping}ms"));
    }

    /// <summary>
    /// Measures one server using the same connection, handshake, sample count, and timeout rules as the Manager.
    /// </summary>
    private async Task<(Server Server, long Ping)?> MeasureServerAsync(Server server)
    {
        if (!server.Online || string.IsNullOrWhiteSpace(server.IP) || server.Port <= 0 || server.Port > 65535)
            return null;

        List<double> samples = new(SampleCount);

        for (int sample = 0; sample < SampleCount; sample++)
        {
            try
            {
                // Measure the complete connection and AQW handshake, not just the server response after send.
                Stopwatch stopwatch = Stopwatch.StartNew();
                using TcpClient client = new();
                await client.ConnectAsync(server.IP, server.Port)
                    .WaitAsync(TimeSpan.FromMilliseconds(TimeoutMs))
                    .ConfigureAwait(false);

                using NetworkStream stream = client.GetStream();
                await stream.WriteAsync(VersionCheck.AsMemory()).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);

                using CancellationTokenSource timeout = new();
                timeout.CancelAfter(TimeoutMs);
                byte[] buffer = new byte[1];
                StringBuilder response = new();

                while (!timeout.IsCancellationRequested)
                {
                    int bytesRead = await stream.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false);
                    if (bytesRead == 0)
                        break;

                    if (buffer[0] != 0)
                    {
                        response.Append((char)buffer[0]);
                        continue;
                    }

                    string message = response.ToString();
                    response.Clear();

                    if (message.Contains("action='apiOK'", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("action=\\"apiOK\\"", StringComparison.OrdinalIgnoreCase))
                    {
                        stopwatch.Stop();
                        samples.Add(stopwatch.Elapsed.TotalMilliseconds);
                        break;
                    }

                    if (message.Contains("action='apiKO'", StringComparison.OrdinalIgnoreCase)
                        || message.Contains("action=\\"apiKO\\"", StringComparison.OrdinalIgnoreCase))
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // A timed-out sample is invalid; try the next sample.
            }
            catch
            {
                // A failed connection or missing protocol response is not a valid latency sample.
            }
        }

        if (samples.Count == 0)
            return null;

        samples.Sort();
        long medianPing = (long)Math.Round(samples[samples.Count / 2], MidpointRounding.AwayFromZero);
        return (server, medianPing);
    }
}
