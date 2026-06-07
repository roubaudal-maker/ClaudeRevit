using System;
using System.Collections.Generic;

namespace ClaudeRevit.Services;

public static class UsageTracker
{
    // Per-model accumulators
    private static readonly Dictionary<string, ModelUsage> _byModel = new();

    // Base rates per million tokens
    private static readonly Dictionary<string, (decimal Input, decimal Output)> _rates = new()
    {
        ["sonnet-4-6"] = (3.0m, 15.0m),
        ["opus-4-7"] = (15.0m, 75.0m),
        ["haiku-4-5"] = (0.80m, 4.0m)
    };

    // 1-hour cache: write = 2x base input rate; read = 0.1x base input rate
    private const decimal CacheWriteMultiplier = 2.0m;
    private const decimal CacheReadMultiplier = 0.1m;

    public static event Action? Updated;

    public static void Add(
        string modelTag,
        long inputTokens,
        long outputTokens,
        long cacheCreationTokens,
        long cacheReadTokens)
    {
        if (!_byModel.TryGetValue(modelTag, out var cur))
            cur = new ModelUsage();

        cur.Input += inputTokens;
        cur.Output += outputTokens;
        cur.CacheCreation += cacheCreationTokens;
        cur.CacheRead += cacheReadTokens;
        _byModel[modelTag] = cur;

        Updated?.Invoke();
    }

    public static (long Input, long Output, long CacheCreation, long CacheRead, decimal Cost) Totals
    {
        get
        {
            long ti = 0, to = 0, tcc = 0, tcr = 0;
            decimal cost = 0;
            foreach (var kv in _byModel)
            {
                ti += kv.Value.Input;
                to += kv.Value.Output;
                tcc += kv.Value.CacheCreation;
                tcr += kv.Value.CacheRead;
                if (_rates.TryGetValue(kv.Key, out var rate))
                {
                    cost += kv.Value.Input / 1_000_000m * rate.Input
                          + kv.Value.Output / 1_000_000m * rate.Output
                          + kv.Value.CacheCreation / 1_000_000m * rate.Input * CacheWriteMultiplier
                          + kv.Value.CacheRead / 1_000_000m * rate.Input * CacheReadMultiplier;
                }
            }
            return (ti, to, tcc, tcr, cost);
        }
    }

    public static void Reset()
    {
        _byModel.Clear();
        Updated?.Invoke();
    }

    public static string Format()
    {
        var (i, o, cc, cr, cost) = Totals;
        if (i == 0 && o == 0 && cc == 0 && cr == 0) return "";

        var totalInput = i + cc + cr;
        var cacheLabel = (cc + cr) > 0
            ? $" ({(int)(cr * 100.0 / Math.Max(1, totalInput))}% cached)"
            : "";

        return $"{FormatTokens(totalInput)} in{cacheLabel} · {FormatTokens(o)} out · ${cost:F3}";
    }

    private static string FormatTokens(long n) => n switch
    {
        < 1_000 => n.ToString(),
        < 1_000_000 => $"{n / 1000.0:F1}K",
        _ => $"{n / 1_000_000.0:F2}M"
    };

    private sealed class ModelUsage
    {
        public long Input;
        public long Output;
        public long CacheCreation;
        public long CacheRead;
    }
}
