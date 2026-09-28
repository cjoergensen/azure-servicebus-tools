namespace Cjoergensen.Azure.ServiceBus.Tools.Shared;

public static class ConsoleUiHelpers
{
    private const string HeaderLine = "==========================================================";
    private const string DividerLine = "----------------------------------------------------------";

    public static void PrintBanner(string title)
    {
        Console.ResetColor();
        Console.WriteLine(HeaderLine);
        Console.WriteLine($"  {title}");
        Console.WriteLine(HeaderLine);
        Console.ResetColor();
    }

    public static void PrintDivider()
    {
        Console.ResetColor();
        Console.WriteLine($"{DividerLine}\n");
        Console.ResetColor();
    }

    public static void WriteLabelValue(string label, string value, ConsoleColor valueColor)
    {
        Console.ResetColor();
        Console.Write($"{label,-22}: ");
        Console.ResetColor();
        Console.WriteLine(value);
        Console.ResetColor();
    }

    public static string FormatAuthentication(string? serviceBusNamespace)
        => !string.IsNullOrWhiteSpace(serviceBusNamespace)
            ? $"Azure credential ({serviceBusNamespace})"
            : "Connection string";
}
