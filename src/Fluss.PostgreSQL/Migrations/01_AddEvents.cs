using FluentMigrator;

namespace EventSourcing.PostgreSQL.Migrations
{
    [Migration(1)]
    public class AddEvent : Migration
    {
        public override void Up()
        {
            Create.Table("Events")
                .WithColumn("Version").AsInt64().PrimaryKey().Identity()
                .WithColumn("At").AsDateTimeOffset().NotNullable()
                .WithColumn("By").AsGuid().Nullable()
                .WithColumn("Event").AsCustom("jsonb").NotNullable();

            Execute.Sql(@"CREATE FUNCTION notify_events()
                            RETURNS trigger AS
                        $BODY$
                        BEGIN
                            NOTIFY new_event;
                            RETURN NULL;
                        END;
                        $BODY$ LANGUAGE plpgsql;

                        CREATE TRIGGER new_event AFTER INSERT ON ""Events"" EXECUTE PROCEDURE notify_events();");
        }

        public override void Down()
        {
            Delete.Table("Events");
        }
    }
}
