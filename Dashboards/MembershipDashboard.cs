using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Dashboards;

namespace MyProject12.Dashboards
{
    [Weight(-7)]
    public class MembershipDashboard : IDashboard
    {
        public string Alias => "מנויים";

        public string[] Sections => new[]
        {
            Constants.Applications.Members,
            Constants.Applications.Content
        };

        public string View => "/umbraco/backoffice/plugins/Memberships";

        public IAccessRule[] AccessRules => Array.Empty<IAccessRule>();
    }
}
