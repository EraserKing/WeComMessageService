namespace DebtServices.Models
{
    public class EastmoneyModel
    {
        public string version { get; set; }
        public EastmoneyResult result { get; set; }
        public bool success { get; set; }
        public string message { get; set; }
        public int code { get; set; }
    }

    public class EastmoneyResult
    {
        public int pages { get; set; }
        public EastmoneyData[] data { get; set; }
        public int count { get; set; }
    }

    public class EastmoneyData
    {
        public string CONVERT_STOCK_CODE { get; set; }
        public string SECURITY_CODE { get; set; }
        public string SECURITY_NAME_ABBR { get; set; }
        public string LISTING_DATE { get; set; }
        public string PUBLIC_START_DATE { get; set; }
        public object CONVERT_STOCK_PRICE { get; set; }
        public object TRANSFER_PRICE { get; set; }
        public object TRANSFER_VALUE { get; set; }
        public object CURRENT_BOND_PRICE { get; set; }
        public object TRANSFER_PREMIUM_RATIO { get; set; }

        public string MakeCardContent()
        {
            return string.Join(Environment.NewLine,
                $"债券代码 {SECURITY_CODE}",
                $"债券简称 {SECURITY_NAME_ABBR}",
                $"证券代码 {CONVERT_STOCK_CODE}",
                $"申购日期 {PUBLIC_START_DATE}",
                $"正股价 {CONVERT_STOCK_PRICE}",
                $"转股价 {TRANSFER_PRICE}",
                $"转股价值 {TRANSFER_VALUE}",
                $"债现价 {CURRENT_BOND_PRICE}",
                $"转股溢价率 {TRANSFER_PREMIUM_RATIO}");
        }
    }
}
