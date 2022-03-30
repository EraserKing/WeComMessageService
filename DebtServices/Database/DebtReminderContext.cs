using DebtServices.Models;
using Microsoft.EntityFrameworkCore;

namespace DebtServices.Database
{
    public class DebtReminderContext : DbContext
    {
        public DebtReminderContext(DbContextOptions<DebtReminderContext> options): base(options)
        {
            this.Database.EnsureCreated();
        }

        public DbSet<DebtReminderModel> DebtReminders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultContainer("Reminders");
            modelBuilder.Entity<DebtReminderModel>().ToContainer("Reminders");
        }
    }
}
