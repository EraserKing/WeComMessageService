using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace WeComMessageService.Utilities
{
    public class OnDemandAzureAppConfigurationRefresher
    {
        private readonly List<IConfigurationRefresher> _configurationRefreshers = new List<IConfigurationRefresher>();

        public OnDemandAzureAppConfigurationRefresher(IConfiguration configuration)
        {
            var configurationRoot = configuration as IConfigurationRoot;

            if (configurationRoot == null)
            {
                throw new InvalidOperationException("The 'IConfiguration' injected in OnDemantConfigurationRefresher is not an 'IConfigurationRoot', and needs to be as well.");
            }

            foreach (var provider in configurationRoot.Providers)
            {
                if (provider is IConfigurationRefresher refresher)
                {
                    _configurationRefreshers.Add(refresher);
                }
            }
        }

        public async Task RefreshAllRegisteredKeysAsync()
        {
            Task compositeTask = null;
            var refreshersTasks = new List<Task>();
            try
            {
                _configurationRefreshers.ForEach(r => refreshersTasks.Add(r.RefreshAsync()));
                compositeTask = Task.WhenAll(refreshersTasks);
                await compositeTask;
            }
            catch (Exception)
            {
                throw compositeTask.Exception;
            }
        }
    }
}
