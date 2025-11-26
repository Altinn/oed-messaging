using System.Reflection;

namespace Altinn.Dd.Correspondence;

internal sealed class AssemblyMarker
{
    public static readonly Assembly Assembly = typeof(AssemblyMarker).Assembly;
}

