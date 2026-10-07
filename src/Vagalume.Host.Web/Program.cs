using Vagalume.Host.Web;

try
{
    await VagalumeWebHost.Create(args).RunAsync();
    return 0;
}
catch (StartupException error)
{
    await Console.Error.WriteLineAsync(error.Message);
    return 1;
}

/// <summary>Entry point marker so integration tests can start the host.</summary>
public partial class Program;
