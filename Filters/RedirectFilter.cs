using System.Web.Mvc;
using Contrib.RewriteRules.Exceptions;
using Contrib.RewriteRules.Models;
using Contrib.RewriteRules.Services;
using Orchard;
using Orchard.Caching;
using Orchard.ContentManagement;
using Orchard.Logging;
using Orchard.Mvc.Filters;
using Orchard.Services;

namespace Contrib.RewriteRules.Filters {
    public class RedirectFilter : FilterProvider, IActionFilter {
        private readonly IOrchardServices _services;
        private readonly IClock _clock;
        private readonly IRewriteRulesService _rewriteRulesService;

        public RedirectFilter(IOrchardServices services, IClock clock, IRewriteRulesService rewriteRulesService) {
            _services = services;
            _clock = clock;
            _rewriteRulesService = rewriteRulesService;
            Logger = NullLogger.Instance;
        }

        public ILogger Logger { get; set; }

        public void OnActionExecuted(ActionExecutedContext filterContext) { }

        public void OnActionExecuting(ActionExecutingContext filterContext) {

            var settings = _rewriteRulesService.GetSettings();
            
            if (!settings.IsEnabled || string.IsNullOrWhiteSpace(settings.Rules)) {
                return;
            }

            try {
                var interpretter = new RulesInterpreter(_clock);
                filterContext.Result = interpretter.Interpret(filterContext.HttpContext, settings.Rules) ?? filterContext.Result;
            }
            catch(RuleEvaluationException e) {
                Logger.Error("Rewrite rule evaluation failed for url {0} on rule {1} with message: ", e.Url, e.Rule, e.Message);
            }
        }
    }
}