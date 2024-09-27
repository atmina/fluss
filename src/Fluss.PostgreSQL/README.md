# Fluss.PostgreSQL

Use a PostgreSQL database as event storage for Fluss.

## Usage

To configure, call `.AddPostgresEventSourcingRepository(connectionString)`. Doing so will run the required migrations on
startup, register the database as event storage and register a database-trigger for new events.
