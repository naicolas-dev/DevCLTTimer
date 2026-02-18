using DevCLT.Core.Interfaces;

namespace DevCLT.WindowsApp.Services;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
