using Giretra.Model.Entities;
using Microsoft.EntityFrameworkCore;

namespace Giretra.Model;

public class GiretraDbContext(DbContextOptions<GiretraDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Bot> Bots => Set<Bot>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchPlayer> MatchPlayers => Set<MatchPlayer>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DealAction> DealActions => Set<DealAction>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<EloHistory> EloHistories => Set<EloHistory>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Block> Blocks => Set<Block>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GiretraDbContext).Assembly);
    }
}
