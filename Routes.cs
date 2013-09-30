using System.Collections.Generic;
using System.Web.Mvc;
using System.Web.Routing;
using Orchard.Mvc.Routes;

namespace Contrib.RewriteRules {
    public class Routes : IRouteProvider {
        public void GetRoutes(ICollection<RouteDescriptor> routes) {
            foreach (var routeDescriptor in GetRoutes())
                routes.Add(routeDescriptor);
        }

        public IEnumerable<RouteDescriptor> GetRoutes() {
            return new[] {
                             new RouteDescriptor {
                                                     Priority = -99,
                                                     Route = new Route(
                                                         "{*path}",
                                                         new RouteValueDictionary {
                                                                                      {"area", "Contrib.RewriteRules"},
                                                                                      {"controller", "Home"},
                                                                                      {"action", "Rewrite"}
                                                                                  },
                                                         new RouteValueDictionary(),
                                                         new RouteValueDictionary {
                                                                                      {"area", "Contrib.RewriteRules"}
                                                                                  },
                                                         new MvcRouteHandler())
                                                 }
                         };
        }
    }
}