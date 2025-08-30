using Eastmoney.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Eastmoney.Services
{
    public class EastmoneyService
    {
        private readonly ILogger<EastmoneyService> Logger;

        private static DateTime LastFetchedDateTime { get; set; }
        private static EastmoneyModel Resource { get; set; }
        private SemaphoreSlim RefreshLock = new SemaphoreSlim(1);

        public EastmoneyService(ILogger<EastmoneyService> logger)
        {
            Logger = logger;
        }

        public async Task<EastmoneyModel> GetFullResource(bool forceRefresh = false)
        {
            await RefreshLock.WaitAsync();
            try
            {
                if (forceRefresh || LastFetchedDateTime.Date < DateTime.Today || Resource == null)
                {
                    int retry = 0;
                    while (retry++ < 3)
                    {
                        try
                        {
                            HttpClient httpClient = new HttpClient();
                            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.51 Safari/537.36 Edg/99.0.1150.39");
                            httpClient.DefaultRequestHeaders.Add("Accept", "*/*");

                            Logger.LogInformation("Accessing Eastmoney Server");
                            var response = await httpClient.GetAsync("https://datacenter-web.eastmoney.com/api/data/v1/get?sortColumns=PUBLIC_START_DATE&sortTypes=-1&pageSize=50&pageNumber=1&reportName=RPT_BOND_CB_LIST&columns=CONVERT_STOCK_CODE,SECURITY_CODE,SECURITY_NAME_ABBR,LISTING_DATE,PUBLIC_START_DATE&quoteColumns=f2~01~CONVERT_STOCK_CODE~CONVERT_STOCK_PRICE,f235~10~SECURITY_CODE~TRANSFER_PRICE,f236~10~SECURITY_CODE~TRANSFER_VALUE,f2~10~SECURITY_CODE~CURRENT_BOND_PRICE,f237~10~SECURITY_CODE~TRANSFER_PREMIUM_RATIO");
                            Logger.LogInformation("Response get from server, will deserialize");
                            var content = await response.Content.ReadAsStringAsync();
                            Logger.LogInformation("{Content}", content);
                            if (content != null)
                            {
                                Resource = JsonSerializer.Deserialize<EastmoneyModel>(content);
                                LastFetchedDateTime = DateTime.Now;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Unable to connect to EastMoney");
                            Thread.Sleep(5000);
                        }
                    }
                }
            }
            finally
            {
                RefreshLock.Release();
            }
            return Resource;
        }

        public static DateTime GetChinaDateTimeNow()
        {
            DateTime utcNow = DateTime.UtcNow;
            TimeZoneInfo timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");
            return TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZoneInfo);
        }

        public static DateOnly GetChinaDateToday()
        {
            return DateOnly.FromDateTime(GetChinaDateTimeNow());
        }

        public async Task<EastmoneyData[]> GetNewReleasesAsync(DateOnly? date = null)
        {
            Logger.LogInformation("Get new releases of {Date}", date?.ToString() ?? "TODAY");
            DateOnly today = date ?? GetChinaDateToday();
            return (await GetFullResource())?.result.data.Where(e => !string.IsNullOrWhiteSpace(e.PUBLIC_START_DATE) && DateOnly.FromDateTime(DateTime.Parse(e.PUBLIC_START_DATE)) == today).ToArray();
        }

        public async Task<EastmoneyData[]> GetNewListingAsync(DateOnly? date = null)
        {
            Logger.LogInformation("Get new listings of {Date}", date?.ToString() ?? "TODAY");
            DateOnly today = date ?? GetChinaDateToday();
            return (await GetFullResource())?.result.data.Where(e => !string.IsNullOrWhiteSpace(e.LISTING_DATE) && DateOnly.FromDateTime(DateTime.Parse(e.LISTING_DATE)) == today).ToArray();
        }
    }
}
