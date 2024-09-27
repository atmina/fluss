# Fluss.HotChocolate

This package contains an adapter to connect Fluss to [HotChocolate](https://chillicream.com/docs/hotchocolate/v13) to
turn all GraphQL queries into live-queries backed by event-sourcing to get fresh data in your client the moment it changes.

## Usage

Assuming you have already configured both Fluss for your general event-sourcing needs and HotChocolate for everything
GraphQL, setting up the adapter is as easy as calling `requestExecutorBuilder.AddLiveEventSourcing()`.

> [!NOTE]
> You also need to configure your client to run `query` operations through a websocket to benefit from the
> subscription-like behaviour. This is different between different clients, so consult the respective documentation for
> that.
