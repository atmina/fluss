using FluentMigrator;

namespace EventSourcing.PostgreSQL.Migrations
{
    [Migration(2)]
    public class VersionConstraintIsDeferrable : Migration
    {
        public override void Up()
        {
            Execute.Sql(@"ALTER TABLE ""Events"" DROP CONSTRAINT ""PK_Events"";");
            Execute.Sql(@"ALTER TABLE ""Events"" ADD CONSTRAINT ""PK_Events"" PRIMARY KEY (""Version"") DEFERRABLE INITIALLY IMMEDIATE;");
        }

        public override void Down()
        {
            Execute.Sql(@"ALTER TABLE ""Events"" DROP CONSTRAINT ""PK_Events"";");
            Execute.Sql(@"ALTER TABLE ""Events"" ADD CONSTRAINT ""PK_Events"" PRIMARY KEY (""Version"") NOT DEFERRABLE;");
        }
    }
}
