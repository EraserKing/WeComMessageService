using DebtReminder.Models;
using Microsoft.EntityFrameworkCore;

namespace DebtReminder.Database
{
    public class DebtReminderContext : DbContext
    {
        public DebtReminderContext(DbContextOptions<DebtReminderContext> options) : base(options)
        {
        }

        public DbSet<DebtReminderModel> DebtReminders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultContainer("Reminders");
            modelBuilder.Entity<DebtReminderModel>().ToContainer("Reminders");
        }
    }
}
