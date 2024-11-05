using System.Collections;
using System.Collections.Immutable;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Fluss.Aggregates;
using Fluss.Authentication;
using Fluss.Events;
using Fluss.ReadModel;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Fluss.HotChocolate.IntegrationTest;

public class Tests
{
    protected WebApplication Host = null!;
    protected string Address = null!;

    [SetUp]
    public async Task Setup()
    {
        var hostBuilder = WebApplication.CreateBuilder();
        hostBuilder.Services.AddEventSourcing()
            .ProvideUserIdFrom(_ => Guid.Empty)
            .AddPolicy<AllowAllPolicy>()
            .AddHttpContextAccessor()
            .AddGraphQLServer()
            .UseDefaultPipeline()
            .AddQueryType()
            .AddMutationType()
            .AddTypeExtension<TodoQueries>()
            .AddTypeExtension<TodoMutations>()
            .AddLiveEventSourcing();

        hostBuilder.WebHost.ConfigureKestrel(serverOptions =>
            serverOptions.Configure().Endpoint(IPAddress.Loopback, 0));

        Host = hostBuilder.Build();
        Host.UseWebSockets();
        Host.MapGraphQL();
        Host.MapGraphQLWebSocket();
        
        await Host.StartAsync();
        
        var server = Host.Services.GetRequiredService<IServer>();
        var addressFeature = server.Features.Get<IServerAddressesFeature>();
        Address = addressFeature!.Addresses.First();
    }

    [Test]
    public async Task SubscriberReceivesLiveUpdates()
    {
        var tokenSource = new CancellationTokenSource();
        tokenSource.CancelAfter(30000);
        
        var channel = await SubscribeToTodos(default);
        
        // Receive initial response
        await channel.Reader.WaitToReadAsync(tokenSource.Token);
        var ids = await channel.Reader.ReadAsync(tokenSource.Token);
        Assert.That(ids, Is.Empty);
        
        for (var i = 0; i < 10; i++)
        {
            var newId = await CreateTodo($"Todo {i}");
            await channel.Reader.WaitToReadAsync(tokenSource.Token);
            ids = await channel.Reader.ReadAsync(tokenSource.Token);
            Assert.That(ids, Has.Count.EqualTo(i + 1));
            Assert.That(ids, Contains.Item(newId));
        }

        await tokenSource.CancelAsync();
    }

    private async Task<Guid> CreateTodo(string todo)
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(Address);

        var body = new
        {
            query = "mutation CreateTodo($todo: String!) { createTodo(todo: $todo) { id } }",
            variables = new { todo },
        };
        
        var response = await httpClient.PostAsJsonAsync("/graphql", body);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = responseBody.GetProperty("data").GetProperty("createTodo").GetProperty("id").GetString();

        return Guid.Parse(id ?? "");
    }

    private async Task<Channel<List<Guid>>> SubscribeToTodos(CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<List<Guid>>();
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("graphql-transport-ws");
        await socket.ConnectAsync(new Uri(Address.Replace("http", "ws") + "/graphql"), CancellationToken.None);

        Task.Run(async () =>
        {
            while (true)
            {
                var buffer = WebSocket.CreateClientBuffer(1024, 1024);
                var result = await socket.ReceiveAsync(buffer, ct);
                var message = Encoding.UTF8.GetString(buffer.AsSpan(0, result.Count));

                if (message.Contains("connection_ack")) continue;
                
                var document = JsonDocument.Parse(message);
                var ids = document.RootElement.GetProperty("payload").GetProperty("data").GetProperty("todos").EnumerateArray()
                    .Select(t => t.GetProperty("id").GetGuid())
                    .ToList();

                await channel.Writer.WriteAsync(ids, ct);

                if (ct.IsCancellationRequested) break;
            }
        }, ct);

        const string init = """{"type":"connection_init"}""";
        await socket.SendAsync(Encoding.UTF8.GetBytes(init).AsMemory(), WebSocketMessageType.Text, true, ct);
        
        const string subscription = """{"id":"0","type":"subscribe","payload":{"query":"\n  query Todos {\n todos { id } }\n","operationName":"Todos","variables":{}}}""";
        await socket.SendAsync(Encoding.UTF8.GetBytes(subscription).AsMemory(), WebSocketMessageType.Text, true, ct);
        
        return channel;
    }
    
    [TearDown]
    public async Task TearDown()
    {
        await Host.StopAsync();
        await Host.DisposeAsync();
    }
}

# region EventListener

public record TodoWrite : AggregateRoot<Guid>
{
    public static async Task<TodoWrite> Create(IWriteUnitOfWork writeUnitOfWork, string todo)
    {
        var id = Guid.NewGuid();
        var aggregate = await writeUnitOfWork.GetAggregate<TodoWrite, Guid>(id);
        await aggregate.Apply(new Events.TodoCreated(id, todo));

        return aggregate;
    }

    private string Todo { get; init; } = "";

    protected override AggregateRoot When(EventEnvelope envelope)
    {
        return envelope.Event switch
        {
            Events.TodoCreated created when created.Id == Id => this with { Todo = created.Todo },
            _ => this,
        };
    }

    public static class Events
    {
        public record TodoCreated(Guid Id, string Todo) : Event;
    }
}

public record AllTodoIds : RootReadModel, IEnumerable<Guid>
{
    public ImmutableHashSet<Guid> Ids = ImmutableHashSet<Guid>.Empty;

    protected override EventListener When(EventEnvelope envelope)
    {
        return envelope.Event switch
        {
            TodoWrite.Events.TodoCreated created => this with { Ids = Ids.Add(created.Id) },
            _ => this,
        };
    }

    public IEnumerator<Guid> GetEnumerator()
    {
        return Ids.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public record TodoRead : ReadModelWithKey<Guid>
{
    public string Todo { get; set; } = "";

    protected override EventListener When(EventEnvelope envelope)
    {
        return envelope.Event switch
        {
            TodoWrite.Events.TodoCreated created when created.Id == Id => this with { Todo = created.Todo },
            _ => this,
        };
    }
}

public record TodoIndex : ReadModelWithKey<Guid>
{
    private bool _found;
    private int _index;
    public int Index => _found ? -1 : _index;

    protected override EventListener When(EventEnvelope envelope)
    {
        return envelope.Event switch
        {
            TodoWrite.Events.TodoCreated created when created.Id == Id => this with { _found = true, _index = _index + 1 },
            TodoWrite.Events.TodoCreated => this with { _index = _index + 1 },
            _ => this,
        };
    }
}

# endregion EventListener

# region Policy

internal class AllowAllPolicy : Policy
{
    public ValueTask<bool> AuthenticateEvent(EventEnvelope envelope, IAuthContext authContext)
    {
        return ValueTask.FromResult(true);
    }

    public ValueTask<bool> AuthenticateReadModel(IReadModel readModel, IAuthContext authContext)
    {
        return ValueTask.FromResult(true);
    }
}

# endregion Policy

# region HotChocolate

[QueryType]
public class TodoQueries
{
    public async Task<IEnumerable<TodoRead>> GetTodos(IUnitOfWork unitOfWork)
    {
        var ids = await unitOfWork.GetReadModel<AllTodoIds>();
        await Task.Delay(500);
        return await unitOfWork.GetMultipleReadModels<TodoRead, Guid>(ids);
    }
}

[MutationType]
public class TodoMutations
{
    public async Task<TodoRead> CreateTodo([Service] IServiceProvider serviceProvider, string todo)
    {
        var unitOfWorkFactory = serviceProvider.GetSystemUserUnitOfWorkFactory();
        
        return await unitOfWorkFactory.Commit(async unitOfWork =>
        {
            var created = await TodoWrite.Create(unitOfWork, todo);
            return await unitOfWork.GetReadModel<TodoRead, Guid>(created.Id);
        });
    }
}

# endregion HotChocolate
