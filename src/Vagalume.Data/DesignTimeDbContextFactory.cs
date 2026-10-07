using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vagalume.Data;

/// <summary>
/// Lets <c>dotnet ef</c> create the context to scaffold migrations without a running host or database.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VagalumeDbContext>
{
    public VagalumeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VagalumeDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=vagalume-design-time")
            .Options;
        return new VagalumeDbContext(options);
    }
}
