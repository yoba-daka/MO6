using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Dashboards;

namespace MyProject12.Dashboards
{
    [Weight(-10)]
    public class ContactDashboard : IDashboard
    {
        public string Alias => "צור קשר";

        public string[] Sections => new[]
        {
            Constants.Applications.Members,
            Constants.Applications.Content
        };

        public string View => "/umbraco/backoffice/plugins/ContactMessages";

        public IAccessRule[] AccessRules => Array.Empty<IAccessRule>();
    }
}
