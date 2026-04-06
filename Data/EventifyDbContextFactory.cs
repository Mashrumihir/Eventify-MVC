using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eventify.Data;

public class EventifyDbContextFactory : IDesignTimeDbContextFactory<EventifyDbContext>
{
    public EventifyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EventifyDbContext>();
        var connectionString =
            "Server=(localdb)\\MSSQLLocalDB;Database=EventifyMvcDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString);

        return new EventifyDbContext(optionsBuilder.Options);
    }
}
