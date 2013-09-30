using Orchard.Localization;
using Orchard.Security;
using Orchard.UI.Navigation;

namespace Contrib.RewriteRules {
    public class AdminMenu : INavigationProvider {
        public Localizer T { get; set; }
        public string MenuName { get { return "admin"; } }

        public void GetNavigation(NavigationBuilder builder) {
            //builder.Add(T("Settings"),
            //    menu => menu.Add(T("Rewrite Rules"), "10", item => item.Action("Index", "Admin", new { area = "Settings", groupInfoId = "Rewrite Rules" }).Permission(StandardPermissions.SiteOwner))
            //        .Add(T("Simulate Rules"), "10", sub => sub.Action("Index", "Admin", new { area = "Contrib.RewriteRules" }).Permission(StandardPermissions.SiteOwner).LocalNav())
            //);

            builder.Add(T("Settings"), menu => menu
                    .Add(T("Rewrite Rules"), "10", subMenu => subMenu.Action("Index", "Admin", new { area = "Settings", groupInfoId = "Rewrite Rules" }).Permission(StandardPermissions.SiteOwner)
                        .Add(T("Settings"), "10", item => item.Action("Index", "Admin", new { area = "Settings", groupInfoId = "Rewrite Rules" }).Permission(StandardPermissions.SiteOwner).LocalNav())
                        .Add(T("Simulations"), "10", item => item.Action("Index", "Admin", new { area = "Contrib.RewriteRules" }).Permission(StandardPermissions.SiteOwner).LocalNav())
                    ));

        }
    }
}
