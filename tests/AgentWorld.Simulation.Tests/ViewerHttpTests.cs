using System.Net;
using System.Net.Http.Json;
using AgentWorld.Viewer.Observation;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentWorld.Simulation.Tests;

public sealed class ViewerHttpTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task BrowserEndpointsServeAReadOnlyProtocolAndStaticPage()
    {
        using var client = factory.CreateClient();

        var handshake = await client.GetFromJsonAsync<ViewerHandshake>("/api/v1/handshake");
        var snapshot = await client.GetFromJsonAsync<ViewerWorldSnapshot>("/api/v1/world");
        var events = await client.GetFromJsonAsync<ViewerEventSlice>("/api/v1/events?afterEventId=3");
        var page = await client.GetStringAsync("/");
        using var attemptedWrite = await client.PostAsync("/api/v1/world", content: null);

        Assert.NotNull(handshake);
        Assert.NotNull(snapshot);
        Assert.NotNull(events);
        Assert.Equal(new ProtocolVersion(1, 0), handshake.Protocol);
        Assert.Equal(snapshot.WorldTick, events.SnapshotTick);
        Assert.All(events.Events, worldEvent => Assert.True(worldEvent.EventId > events.AfterEventId));
        Assert.Contains("read-only deterministic inspection", page, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, attemptedWrite.StatusCode);
    }

    [Fact]
    public async Task NegativeEventCursorProducesATypedValidationFailure()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/events?afterEventId=-1");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
