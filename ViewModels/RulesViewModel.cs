using System.Web.Mvc;

namespace Contrib.RewriteRules.ViewModels {
    public class RulesViewModel {
        public string Rules { get; set; }
        public string Url { get; set; }
        public string ApplicationPath { get; set; }
        public ActionResult Result { get; set; }
    }
}