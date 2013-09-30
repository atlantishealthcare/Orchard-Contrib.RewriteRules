using Contrib.RewriteRules.Models;
using Orchard;

namespace Contrib.RewriteRules.Services {
    public interface IRewriteRulesService : IDependency {
        RedirectSettings GetSettings();
        void SetSettings(RedirectSettings settings);
    }
}