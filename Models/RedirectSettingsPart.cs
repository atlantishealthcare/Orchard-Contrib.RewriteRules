using Orchard.ContentManagement;

namespace Contrib.RewriteRules.Models {
    public class RedirectSettingsPart : ContentPart<RedirectSettingsPartRecord> {
        public string Rules {
            get { return Record.Rules; }
            set { Record.Rules = value; }
        }

        public bool Enabled {
            get { return Record.Enabled; }
            set { Record.Enabled = value; }
        }
    }
}