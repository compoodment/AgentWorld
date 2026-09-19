using AgentWorld.Viewer.Observation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<SeededWorldObservationStore>();

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

app.Run();

public partial class Program;
