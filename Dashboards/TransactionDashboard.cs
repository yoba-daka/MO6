using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Dashboards;

namespace MyProject12.Dashboards
{
    [Weight(-5)]
    public class TransactionDashboard : IDashboard
    {
        public string Alias => "עסקאות";

        public string[] Sections => new[]
        {
            Constants.Applications.Members,
            Constants.Applications.Content
        };

        public string View => "/umbraco/backoffice/plugins/Transactions";

        public IAccessRule[] AccessRules => Array.Empty<IAccessRule>();
    }
}
