using System.Web.Mvc;
using Contrib.RewriteRules.Models;
using Contrib.RewriteRules.Services;
using Contrib.RewriteRules.ViewModels;
using Orchard;
using Orchard.ContentManagement;
using Orchard.UI.Admin;

namespace Contrib.RewriteRules.Controllers {
    [Admin]
    public class AdminController : Controller {

        public AdminController(IOrchardServices services) {
            Services = services;    
        }

        public IOrchardServices Services { get; set; }

        [HttpGet]
        public ActionResult Index() {
            var settings = Services.WorkContext.CurrentSite.As<RedirectSettingsPart>();
            var url = Services.WorkContext.CurrentSite.BaseUrl;
            var applicationPath = "";
            var rules = settings != null ? settings.Rules : "";

            return IndexPost(url, applicationPath, rules);
        }

        [HttpPost, ActionName("Index")]
        public ActionResult IndexPost(string url, string applicationPath, string rules) {
            var model = new RulesViewModel {
                Rules = rules, 
                Url = url,
                ApplicationPath = applicationPath
            };

            var simulation = new SimulationService();
            model.Result = simulation.Simulate(url, applicationPath, rules);

            return View("Index", model);
        }

    }
}