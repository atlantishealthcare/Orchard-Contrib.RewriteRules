using System.Web.Mvc;

namespace Contrib.RewriteRules.Controllers {
    public class HomeController : Controller {

        public ActionResult Rewrite(string path) {
            return new HttpNotFoundResult();
        }
    }
}