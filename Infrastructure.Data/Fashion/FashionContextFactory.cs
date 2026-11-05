using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data.Fashion;

public class FashionContextFactory : IDesignTimeDbContextFactory<FashionContext>
{
    public FashionContext CreateDbContext(string[] args)
    {
        // Never fall back to the legacy DefaultConnection. This placeholder
        // permits offline migration generation but cannot reach a real server.
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__FashionConnection")
            ?? "Server=fashion-design-only.invalid;Database=XmodeFashion;Integrated Security=True;TrustServerCertificate=True";
        return new FashionContext(new DbContextOptionsBuilder<FashionContext>()
            .UseSqlServer(connection).Options);
    }
}
