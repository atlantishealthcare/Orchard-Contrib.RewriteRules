using System;
using System.Web;
using System.Web.Mvc;
using Moq;
using Orchard.Services;
using System.Collections.Specialized;

namespace Contrib.RewriteRules.Services {
    public class SimulationService {

        private readonly RulesInterpreter _rulesInterpreter;
        private readonly Mock<HttpContextBase> _context;

        public SimulationService() {
            var clock = new Mock<IClock>();
            clock.SetupGet(c => c.UtcNow).Returns(DateTime.UtcNow);

            _rulesInterpreter = new RulesInterpreter(clock.Object);
            _context = new Mock<HttpContextBase>();
        }

        public ActionResult Simulate(string url, string applicationPath, string rule) {
            _context.SetupGet(c => c.Request.Url).Returns(new Uri(url));
            _context.SetupGet(c => c.Request.ApplicationPath).Returns(applicationPath);

            var headers = new NameValueCollection();
            headers["Host"] = new Uri(url).Host;

            _context.SetupGet(c => c.Request.UserAgent).Returns(HttpContext.Current.Request.UserAgent);
            _context.SetupGet(c => c.Request.UrlReferrer).Returns(HttpContext.Current.Request.UrlReferrer);
            _context.SetupGet(c => c.Request.Headers).Returns(headers);
            _context.SetupGet(c => c.Request.LogonUserIdentity).Returns(HttpContext.Current.Request.LogonUserIdentity);
            _context.SetupGet(c => c.Request.HttpMethod).Returns(HttpContext.Current.Request.HttpMethod);
            
            var cookies = new HttpCookieCollection();
            _context.SetupGet(c => c.Response.Cookies).Returns(cookies);
            _context.Setup(c => c.Response.SetCookie(It.IsAny<HttpCookie>())).Callback<HttpCookie>(cookies.Set);

            return _rulesInterpreter.Interpret(_context.Object, rule);
        } 
    }
}