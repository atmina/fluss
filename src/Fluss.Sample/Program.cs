// See https://aka.ms/new-console-template for more information

using Fluss;
using Fluss.Authentication;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("Hello, World!");

var sc = new ServiceCollection();

var sp = sc.AddEventSourcing().ProvideUserIdFrom(_ => Guid.Empty).BuildServiceProvider();

var unitOfWork = sp.GetRequiredService<UnitOfWork>();

var version = await unitOfWork.ConsistentVersion();

var result = await unitOfWork.SelectAdd(1, 2);

Console.WriteLine($"{version} {result}");