﻿using System.Text.Json;

namespace DebtReminder.Models
{
    public class DebtReminderModel : DbRecordBaseModel
    {
        public string UserName { get; set; }
        public string DebtCode { get; set; }
        public string DebtName { get; set; }
        public string ConvertStockCode { get; set; }

        public ReminderType ReminderType { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public enum ReminderType
    {
        LISTING,
        RELEASE
    }
}
