namespace RusEfiTools.Gui;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new DesktopApplication();
        application.Run();
    }
}
