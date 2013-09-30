using Orchard.ContentManagement.Records;
using Orchard.Data.Conventions;

namespace Contrib.RewriteRules.Models {
    public class RedirectSettingsPartRecord : ContentPartRecord {
        public virtual bool Enabled { get; set; }

        [StringLengthMax]
        public virtual string Rules { get; set; }
    }
}