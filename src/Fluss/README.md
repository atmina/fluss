## Key-Subjects

### Event-Sourcing

Instead of a database or other storage mechanism in an event-sourced system the application state is not explicitly
recorded, but rather infered from a series of facts from the past. A common example is a banking ledger where we don't
want to save the current amount of money in the account, but rather the list of transactions. The money in the account
is then simply the sum of all transactions.

This also easily allows a later change of business rules. For example one could define all deposits on a sunday to be
worth double and no data would need to be changed, just the business rules.

### CQRS / CQS

Command–query separation requires the separation of the read and write parts of an application. This is an added side
effect of most event-sourcing implementations and allows one to find a good data representation for read and write which
would usually require a tradeoff between them.

### Command

Request for a fact to be recorded as an event by a client. This needs to be validated against business rules and if that
succeeds can be transformed into one or multiple events.

### Event

A timestamped fact of history that is defined as valid. The primary key (version) is an ever-increasing number.

### EventRepository

Provides a way of saving and getting a list of events, as well as notifications for new events coming in. There is
currently an in-memory implementation as well as a postgres implementation.

WIP: Filtering of the events based on aggregate or readmodel to reduce number of uninteresting events being queried.
This idea prevents using a more traditional event database.

### ReadModel

For the read part of the application this allows us to get to the state of the application. The state is saved as fields
on the object and the `When` method is called for every event. In general this implements a fold operation from
functional programming languages. The contract for providing snapshots and change detection with the `When` method is
that a call to `Accepted` happens whenever an event changed the state of the ReadModel (This does not have to be 100%
precise, but more precision allows higher accuracy).

### Aggregate

For the write part of the application this provides the aforementioned transformation of Commands into Events. Commands
for an Aggregate are encoded as methods on the Aggregate. To record the Event the `Apply` method has to be called on the
aggregate.

### UnitOfWork

This is follows the unit of work pattern and provides values for both read and write parts of the application. For both
of them it holds a `ConsistentVersion` to not have one part of the read models or aggregates use different facts of
history.

For the read side it allows developers to get a ReadModel at that consistent version.

For the write side it tracks the list of events returned from aggregates and as such provides a model for a transaction
in the form of a list of events that can either be published as whole or not.

### Cross Aggregate Validation (WIP)

Currently it is not easily possible to validate Commands using state from other aggregates. One example is checking for
the uniqueness of a username during registration. We need to find a way to do this.

### Snapshots (WIP)

At the start the number of requested events for finding the current state of a read model should be still fine, but at
some point we need to introduce the concept of snapshots and a new snapshot repository for that. Then we can get the
latest read model from the snapshots and only apply the events after the snapshot.

### SideEffect (WIP)

Some of the events require external actions to be taken that are listening to our events i. e. sending an e-mail after
registration. A system that tracks and executes these side effects would allow us to decouple the logic for that from
the rest of the application.

### Upcaster (WIP)

At some point we will need to update our event definitions to included more or different data. When there are two
versions of a UserRegistrated event all readmodels interested in users registering need to listen to both of them.
Upcasters allow us to centralize that logic. 
