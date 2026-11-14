using Microsoft.EntityFrameworkCore;

namespace Jarvis.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Song> Songs { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

    }
}
