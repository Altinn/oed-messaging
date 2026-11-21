using System.Diagnostics;
using Xunit.Abstractions;

namespace Altinn.Dd.Tests.Shared;

internal sealed class Logger
{
    // OS-level stdout, captured once before xUnit replaces Console.Out. Writes here are flushed to
    // the test host's actual stdout in real time, so progress shows up during the multi-minute run
    // instead of nothing-until-completion.
    private static readonly TextWriter LiveOut =
        TextWriter.Synchronized(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });

    private readonly ITestOutputHelper _output;
    private readonly string _tag;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    // Each gate passes its own tag so interleaved output is attributable.
    public Logger(ITestOutputHelper output, string tag)
    {
        _output = output;
        _tag = tag;
    }

    public void Log(string message)
    {
        var line = $"[{_clock.Elapsed:hh\\:mm\\:ss}] [{_tag}] {message}";
        _output.WriteLine(line);
        LiveOut.WriteLine(line);
    }

    public static void StreamSubprocessLine(string line) => LiveOut.WriteLine($"    {line}");
}
