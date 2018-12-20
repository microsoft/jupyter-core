using System;
using System.Linq;
using System.Collections.Generic;
using Kusto.Data.Net.Client;
using Kusto.Data.Common;
using Kusto.Data.Exceptions;
using Kusto.Data;
using Microsoft.Extensions.Options;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Extensions.Logging;

namespace Microsoft.Jupyter.Core
{

    public class KustoEngine : BaseEngine
    {
        private ICslQueryProvider client;
        private AppSettings settings;

        public KustoEngine(
            IShellServer shell,
            IOptions<KernelContext> context,
            ILogger logger,
            AppSettings settings
        ) : base(shell, context, logger)
        {
            client = null;
            this.settings = settings;
        }

        public override ExecutionResult ExecuteMundane(string input, Action<string> stdout, Action<string> stderr)
        {
            var table = "";
            if (client == null)
            {
                stderr("Not connected to a cluster.");
                return new ExecutionResult
                {
                    Status = ExecuteStatus.Error,
                    Output = null
                };
            }

            try
            {
                using (var reader = client.ExecuteQuery(input))
                {
                    var fieldNames = reader.GetFieldNames();
                    var rows = reader.ReadAllRows();

                    var tableBody = String.Join("\n",
                        rows.Select(row =>
                            "<tr>" +
                            String.Join("", row.Select(field =>
                                $"<td>{field}</td>"
                            )) + "</tr>"
                        )
                    );
                    var headerRow = "<tr>" + String.Join(
                        "",
                        fieldNames.Select(fieldName => $"<th>{fieldName}</th>")
                    ) + "</tr>";
                    table = $"<table><thead>{headerRow}</thead><tbody>{tableBody}</tbody></table>";
                }
                return new ExecutionResult
                {
                    Status = ExecuteStatus.Ok,
                    Output = new Dictionary<string, string>
                    {
                        ["text/html"] = table
                    }
                };
            } catch (Exception ex) {
                stderr(ex.ToString());
                return new ExecutionResult
                {
                    Status = ExecuteStatus.Error,
                    Output = null
                };
            }

        }

        [MagicCommand("%connect")]
        public ExecutionResult ExecuteConnect(string input, Action<string> stdout, Action<string> stderr)
        {
            try
            {
                var connectionString =
                    settings.AddAuthentication(input);
                        // .WithAadUserTokenAuthentication() ← not supported on .NET Standard.
                client = KustoClientFactory.CreateCslQueryProvider(connectionString);
                return "Connected!".ToExecutionResult();
            }
            catch (KustoClientInvalidConnectionStringException ex)
            {
                stderr(ex.ToString());
                return new ExecutionResult
                {
                    Output = null,
                    Status = ExecuteStatus.Error
                };
            }
        }
    }
}
