using System.Security.Cryptography;

namespace EnterpriseWorkflow.Core.Model;

/// <summary>Human-facing reference; storage must enforce uniqueness and retry collisions.</summary>
public static class WorkflowPublicReference
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static int _serial;
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public static string Create(DateTimeOffset time)
    {
        var minutes = (long)(time - Epoch).TotalMinutes;
        if (time < Epoch || minutes >= 60466176) throw new ArgumentOutOfRangeException(nameof(time));
        return Encode(minutes, 5) + Encode((uint)Interlocked.Increment(ref _serial) % 1296, 2) + Encode(RandomNumberGenerator.GetInt32(46656), 3);
    }
    private static string Encode(long value, int width)
    {
        Span<char> result = stackalloc char[width];
        for (var index = width - 1; index >= 0; index--) { result[index] = Alphabet[(int)(value % 36)]; value /= 36; }
        return new string(result);
    }
    public static bool IsValid(string? value) => value is { Length: 10 } && value.All(character => Alphabet.Contains(character, StringComparison.Ordinal));
}
