using Orchard.Data.Migration;

namespace Contrib.RewriteRules {
    public class Migrations : DataMigrationImpl {
        public int Create() {

            SchemaBuilder.CreateTable("RedirectSettingsPartRecord", 
                table => table
                    .ContentPartRecord()
                    .Column<bool>("Enabled")
                    .Column<string>("Rules", c => c.Unlimited())
                );

            return 1;
        }
    }
}