namespace Qinglong.Models
{
    public class QinglongCronModel
    {
        public int code { get; set; }
        public QinglongCronDataSet data { get; set; }
    }

    public class QinglongCronDataSet
    {
        public int total { get; set; }
        public QinglongCronData[] data { get; set; }

    }

    public class QinglongCronData
    {
        public int id { get; set; }
        public string name { get; set; }
        public string command { get; set; }
        public string schedule { get; set; }
        public string timestamp { get; set; }
        public bool saved { get; set; }
        public int status { get; set; }
        public int isSystem { get; set; }
        public object pid { get; set; }
        public int isDisabled { get; set; }
        public int isPinned { get; set; }
        public string log_path { get; set; }
        public string[] labels { get; set; }
        public int last_running_time { get; set; }
        public int last_execution_time { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime updatedAt { get; set; }
    }
}
