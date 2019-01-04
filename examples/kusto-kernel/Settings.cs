using System.Configuration;
using Kusto.Data;

namespace Microsoft.Jupyter.Core
{
    public class AppSettings
    {
        private Configuration config;
        public AppSettings()
        {
            this.config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
        }

        public KustoConnectionStringBuilder AddAuthentication(string connectionString)
        {
            var appSettings = config.AppSettings.Settings;
            var connectionBuilder = new KustoConnectionStringBuilder(connectionString);
            var token = appSettings[connectionBuilder.DataSource];
            if (token != null)
            {
                var tokenParts = token.Value.Split(":", 2);

                connectionBuilder = connectionBuilder.
                    WithKustoBasicAuthentication(tokenParts[0], tokenParts[1]);
            }
            else // fall back to application ID/secret
            {
                connectionBuilder = connectionBuilder
                    .WithAadApplicationKeyAuthentication(
                        appSettings["applicationId"].Value,
                        appSettings["applicationSecret"].Value,
                        appSettings["authority"].Value
                    );
            }
            return connectionBuilder;
        }
    }
}