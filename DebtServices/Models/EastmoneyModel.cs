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
        public string SECURITY_CODE { get; set; }
        public string SECUCODE { get; set; }
        public string TRADE_MARKET { get; set; }
        public string SECURITY_NAME_ABBR { get; set; }
        public object DELIST_DATE { get; set; }
        public string LISTING_DATE { get; set; }
        public string CONVERT_STOCK_CODE { get; set; }
        public string BOND_EXPIRE { get; set; }
        public string RATING { get; set; }
        public string VALUE_DATE { get; set; }
        public string ISSUE_YEAR { get; set; }
        public string CEASE_DATE { get; set; }
        public string EXPIRE_DATE { get; set; }
        public string PAY_INTEREST_DAY { get; set; }
        public string INTEREST_RATE_EXPLAIN { get; set; }
        public string BOND_COMBINE_CODE { get; set; }
        public float ACTUAL_ISSUE_SCALE { get; set; }
        public int ISSUE_PRICE { get; set; }
        public string REMARK { get; set; }
        public int PAR_VALUE { get; set; }
        public string ISSUE_OBJECT { get; set; }
        public object REDEEM_TYPE { get; set; }
        public object EXECUTE_REASON_HS { get; set; }
        public object NOTICE_DATE_HS { get; set; }
        public object NOTICE_DATE_SH { get; set; }
        public object EXECUTE_PRICE_HS { get; set; }
        public object EXECUTE_PRICE_SH { get; set; }
        public object RECORD_DATE_SH { get; set; }
        public object EXECUTE_START_DATESH { get; set; }
        public object EXECUTE_START_DATEHS { get; set; }
        public object EXECUTE_END_DATE { get; set; }
        public string CORRECODE { get; set; }
        public string CORRECODE_NAME_ABBR { get; set; }
        public string PUBLIC_START_DATE { get; set; }
        public string CORRECODEO { get; set; }
        public string CORRECODE_NAME_ABBRO { get; set; }
        public string BOND_START_DATE { get; set; }
        public string SECURITY_START_DATE { get; set; }
        public string SECURITY_SHORT_NAME { get; set; }
        public float FIRST_PER_PREPLACING { get; set; }
        public int ONLINE_GENERAL_AAU { get; set; }
        public float? ONLINE_GENERAL_LWR { get; set; }
        public float INITIAL_TRANSFER_PRICE { get; set; }
        public string TRANSFER_END_DATE { get; set; }
        public string TRANSFER_START_DATE { get; set; }
        public string RESALE_CLAUSE { get; set; }
        public string REDEEM_CLAUSE { get; set; }
        public string PARTY_NAME { get; set; }
        public float CONVERT_STOCK_PRICE { get; set; }
        public float TRANSFER_PRICE { get; set; }
        public float TRANSFER_VALUE { get; set; }
        public object CURRENT_BOND_PRICE { get; set; }
        public float TRANSFER_PREMIUM_RATIO { get; set; }
        public object CONVERT_STOCK_PRICEHQ { get; set; }
        public object MARKET { get; set; }
        public object RESALE_TRIG_PRICE { get; set; }
        public float REDEEM_TRIG_PRICE { get; set; }
        public float PBV_RATIO { get; set; }
        public string IB_START_DATE { get; set; }
        public string IB_END_DATE { get; set; }
        public string CASHFLOW_DATE { get; set; }
        public float? COUPON_IR { get; set; }
        public string PARAM_NAME { get; set; }
        public string ISSUE_TYPE { get; set; }
        public object EXECUTE_REASON_SH { get; set; }
        public string PAYDAYNEW { get; set; }
        public int? CURRENT_BOND_PRICENEW { get; set; }
    }

}
