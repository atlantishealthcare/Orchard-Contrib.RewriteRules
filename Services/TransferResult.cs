using System;
using System.Transactions;
using System.Web;
using System.Web.Mvc;

namespace Contrib.RewriteRules.Services {
    /// <summary>
    /// Transfers execution to the supplied url.
    /// </summary>
    public class TransferResult : RedirectResult {
        public TransferResult(string url)
            : base(url) { }

        public override void ExecuteResult(ControllerContext context) {
            using (new TransactionScope(TransactionScopeOption.RequiresNew)) {
                var httpContext = HttpContext.Current;

                // See http://stackoverflow.com/questions/799511/how-to-simulate-server-transfer-in-asp-net-mvc/799534
                // MVC 3 running on IIS 7+
                if (HttpRuntime.UsingIntegratedPipeline) {
                    httpContext.Server.TransferRequest(Url, true);
                }
                else {
                    // Pre MVC 3
                    httpContext.RewritePath(Url, false);

                    IHttpHandler httpHandler = new MvcHttpHandler();
                    httpHandler.ProcessRequest(httpContext);
                }
            }
        }
    }
}