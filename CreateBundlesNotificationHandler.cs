using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.WebAssets;
using Umbraco.Cms.Core;

namespace MyProject12
{
    public class CreateBundlesNotificationHandler : INotificationHandler<UmbracoApplicationStartingNotification>
    {
        private readonly IRuntimeMinifier _runtimeMinifier;
        private readonly IRuntimeState _runtimeState;

        public CreateBundlesNotificationHandler(IRuntimeMinifier runtimeMinifier, IRuntimeState runtimeState)
        {
            _runtimeMinifier = runtimeMinifier;
            _runtimeState = runtimeState;
        }
        public void Handle(UmbracoApplicationStartingNotification notification)
        {
            if (_runtimeState.Level == RuntimeLevel.Run)
            {
                _runtimeMinifier.CreateJsBundle("js-bundle",
                BundlingOptions.OptimizedAndComposite,
                new[] { "/js/core/jquery.min.js", "/js/core/popper.min.js", "/js/core/bootstrap-material-design.min.js" , "/js/plugins/moment.min.js" , "/js/plugins/bootstrap-datetimepicker.js" , "/js/plugins/nouislider.min.js" , "/js/material-kit.js" , "/js/modernizr.js" , "/js/jquery.validate.min.js" , "/js/jquery.validate.unobtrusive.min.js" , "/js/jquery.unobtrusive-ajax.min.js" , "/js/accessibility.js", "/js/plyr.js" });

                _runtimeMinifier.CreateCssBundle("css-bundle",
                    BundlingOptions.OptimizedAndComposite,
                    new[] { "/css/material-kit.min.css", "/css/mdi.css", "/css/vertical-nav.css", "/css/accessibility.css" , "/css/plyr.css" });
            }
        }
    }
}
