using System;
using System.Collections.Generic;
using Contrib.RewriteRules.Models;
using Contrib.RewriteRules.Services;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Drivers;
using Orchard.Localization;

namespace Contrib.RewriteRules.Drivers {
    public class RedirectSettingsPartDriver : ContentPartDriver<RedirectSettingsPart> {
        private readonly IRewriteRulesService _rewriteRulesService;
        private readonly IContentManager _contentManager;

        public RedirectSettingsPartDriver(
            IRewriteRulesService rewriteRulesService,
            IContentManager contentManager) {
            _rewriteRulesService = rewriteRulesService;
            _contentManager = contentManager;

            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        protected override string Prefix { get { return "RewriteRulesSettings"; } }

        protected override DriverResult Editor(RedirectSettingsPart part, dynamic shapeHelper) {
            return Editor(part, null, shapeHelper);
        }

        protected override DriverResult Editor(RedirectSettingsPart part, IUpdateModel updater, dynamic shapeHelper) {

            var settings = _rewriteRulesService.GetSettings();

            if(updater != null) {
                if(updater.TryUpdateModel(settings, Prefix, null, null)) {
                    _rewriteRulesService.SetSettings(settings);
                }
            }

            return ContentShape("Parts_RewriteRules_RedirectSettings",
                () => shapeHelper.EditorTemplate(TemplateName: "Parts/RewriteRules.RedirectSettings", Model: settings, Prefix: Prefix)).OnGroup("Rewrite Rules");
        }
    }
}
