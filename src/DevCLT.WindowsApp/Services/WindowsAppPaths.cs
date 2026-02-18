using System.IO;
using DevCLT.Core.Interfaces;

namespace DevCLT.WindowsApp.Services;

public class WindowsAppPaths : IAppPaths
{
    public string DataDirectory { get; }
    public string DatabasePath { get; }

    public WindowsAppPaths()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DevCLTTimer");
        DatabasePath = Path.Combine(DataDirectory, "devclt.db");
    }
}
