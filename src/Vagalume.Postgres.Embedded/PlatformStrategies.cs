namespace Vagalume.Postgres.Embedded;

/// <summary>
/// Factory that picks the <see cref="IPlatformStrategy"/> for the running operating system in one place.
/// </summary>
public static class PlatformStrategies
{
    public static IPlatformStrategy Detect()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
            != System.Runtime.InteropServices.Architecture.X64)
        {
            throw new PlatformNotSupportedException("Only x64 is supported.");
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxPlatform();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsPlatform();
        }

        throw new PlatformNotSupportedException("Only Linux x64 and Windows x64 are supported.");
    }
}
