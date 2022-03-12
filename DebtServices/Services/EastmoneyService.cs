using DebtServices.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DebtServices.Services
{
    public class EastmoneyService
    {
        private HttpClient _httpClient { get; set; } = new HttpClient();

        public async Task<EastmoneyModel> GetFullResource()
        {
            string response = await _httpClient.GetStringAsync("https://datacenter-web.eastmoney.com/api/data/v1/get?sortColumns=PUBLIC_START_DATE&sortTypes=-1&pageSize=50&pageNumber=1&reportName=RPT_BOND_CB_LIST&quoteColumns=f2~01~CONVERT_STOCK_CODE~CONVERT_STOCK_PRICE%2Cf235~10~SECURITY_CODE~TRANSFER_PRICE%2Cf236~10~SECURITY_CODE~TRANSFER_VALUE%2Cf2~10~SECURITY_CODE~CURRENT_BOND_PRICE%2Cf237~10~SECURITY_CODE~TRANSFER_PREMIUM_RATIO%2Cf239~10~SECURITY_CODE~RESALE_TRIG_PRICE%2Cf240~10~SECURITY_CODE~REDEEM_TRIG_PRICE%2Cf23~01~CONVERT_STOCK_CODE~PBV_RATIO&columns=ALL");

            if (response == null)
            {
                EastmoneyModel emm = JsonSerializer.Deserialize<EastmoneyModel>(response);
                return emm;
            }
            else
            {
                return null;
            }
        }

        public async Task<EastmoneyData[]> GetNewDebtToday()
        {
            EastmoneyModel emm = await GetFullResource();
            return emm.result.data.Where(e => DateOnly.Parse(e.PUBLIC_START_DATE) == DateOnly.FromDateTime(DateTime.Today)).ToArray();
        }
    }
}
