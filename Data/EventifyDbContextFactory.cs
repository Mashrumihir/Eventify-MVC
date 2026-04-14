using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Eventify.Data;

public class EventifyDbContextFactory : IDesignTimeDbContextFactory<EventifyDbContext>
{
    public EventifyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EventifyDbContext>();
        var connectionString =
            "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=EventifyMvcDb;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False";

        optionsBuilder.UseSqlServer(connectionString);

        return new EventifyDbContext(optionsBuilder.Options);
    }
}
