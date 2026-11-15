#pragma warning disable CA1822
#pragma warning disable SA1204

namespace Atya.Tooling.Cli;

[ExcludeFromCodeCoverage(Justification = "Constant holder has no behavior.")]
internal static class CliExitCodes
{
    internal const int Success = 0;
    internal const int ValidationFailed = 1;
    internal const int InvalidArguments = 2;
    internal const int ExternalCommandFailed = 3;
    internal const int NotAnAtyaRepository = 4;
    internal const int UnhandledException = 10;
}
