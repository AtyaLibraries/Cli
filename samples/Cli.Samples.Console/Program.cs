namespace Atya.Tooling.Cli.Samples.ConsoleApp;

/// <summary>
/// Runs the sample console application.
/// </summary>
public static class Program
{
    /// <summary>
    /// Writes the installed command name.
    /// </summary>
    public static void Main()
    {
        Console.WriteLine("Install with: dotnet tool install --global Atya.Tooling.Cli");
        Console.WriteLine("Run: atya doctor --ci");
    }
}
