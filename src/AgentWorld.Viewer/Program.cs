using AgentWorld.Simulation.Harness;
using AgentWorld.Viewer.Observation;

var builder = WebApplication.CreateBuilder(args);
var advanceFixture = builder.Configuration.GetValue<bool>("AgentWorld:Runtime:AdvanceScript");
if (advanceFixture)
{
    builder.Services.AddSingleton(new LiveSeededWorldRuntime(SeededWorldObservationStore.SampleSeed));
    builder.Services.AddSingleton<SeededWorldObservationStore>(services =>
        new SeededWorldObservationStore(services.GetRequiredService<LiveSeededWorldRuntime>()));
    builder.Services.AddHostedService<LiveWorldRuntimeService>();
}
else
{
    builder.Services.AddSingleton<SeededWorldObservationStore>();
}

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/v1/handshake", (SeededWorldObservationStore store) => Results.Ok(store.GetHandshake()));
app.MapGet("/api/v1/world", (SeededWorldObservationStore store) => Results.Ok(store.GetSnapshot()));
app.MapGet("/api/v1/events", (long? afterEventId, SeededWorldObservationStore store) =>
{
    if (afterEventId is < 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["afterEventId"] = ["The event cursor cannot be negative."],
        });
    }

    return Results.Ok(store.GetEventsAfter(afterEventId ?? 0));
});
app.MapGet("/api/v1/reconnect", (long? afterEventId, SeededWorldObservationStore store) =>
{
    if (afterEventId is < 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["afterEventId"] = ["The event cursor cannot be negative."],
        });
    }

    return Results.Ok(store.GetReconnectBaseline(afterEventId ?? 0));
});

app.Run();

public partial class Program;
