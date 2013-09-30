using Contrib.RewriteRules.Models;
using Orchard;
using Orchard.Caching;
using Orchard.ContentManagement;

namespace Contrib.RewriteRules.Services {
    public class RewriteRulesService : IRewriteRulesService {
        private readonly ICacheManager _cacheManager;
        private readonly IOrchardServices _orchardServices;
        private readonly ISignals _signals;

        public RewriteRulesService(ICacheManager cacheManager, IOrchardServices orchardServices, ISignals signals) {
            _cacheManager = cacheManager;
            _orchardServices = orchardServices;
            _signals = signals;
        }

        public RedirectSettings GetSettings() {
            return _cacheManager.Get("RedirectSettings", context => {
                var settings = _orchardServices.WorkContext.CurrentSite.As<RedirectSettingsPart>();
                return new RedirectSettings {
                    IsEnabled = settings.Enabled,
                    Rules = settings.Rules
                };
            });
        }

        public void SetSettings(RedirectSettings settings) {
            var settingsPart = _orchardServices.WorkContext.CurrentSite.As<RedirectSettingsPart>();
            settingsPart.Rules = settings.Rules;
            settingsPart.Enabled = settings.IsEnabled;

            // invalidates the cache
            _signals.Trigger("RedirectSettings");
        }
    }
}