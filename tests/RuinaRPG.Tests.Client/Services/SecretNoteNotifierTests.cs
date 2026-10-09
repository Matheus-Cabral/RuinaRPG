using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class SecretNoteNotifierTests
{
    private readonly FakeNotificationConnection _connection = new();
    private readonly List<HttpRequestMessage> _requests = new();
    private List<UnreadSecretNotesResponse> _unread = new();
    private HttpStatusCode _unreadStatus = HttpStatusCode.OK;

    private SecretNoteNotifier CreateNotifier() => new(FakeHttpMessageHandler.CreateClient(request =>
    {
        _requests.Add(request);
        if (request.RequestUri!.AbsolutePath.EndsWith("secret-notes/unread"))
            return new HttpResponseMessage(_unreadStatus) { Content = JsonContent.Create(_unread) };
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }), _connection);

    [Fact]
    public async Task Start_loads_the_unread_counts_and_opens_the_connection()
    {
        _unread = new() { new("c1", "Ruína", 2), new("c2", "Outra", 1) };
        var notifier = CreateNotifier();
        var changed = 0;
        notifier.Changed += () => changed++;

        await notifier.StartAsync();

        notifier.TotalUnread.Should().Be(3);
        notifier.UnreadFor("c1").Should().Be(2);
        notifier.UnreadFor("unknown").Should().Be(0);
        _connection.StartCount.Should().Be(1);
        changed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Starting_twice_opens_a_single_connection()
    {
        var notifier = CreateNotifier();

        await Task.WhenAll(notifier.StartAsync(), notifier.StartAsync());
        await notifier.StartAsync();

        _connection.StartCount.Should().Be(1);
    }

    [Fact]
    public async Task Start_survives_a_failing_unread_request_and_still_connects()
    {
        _unreadStatus = HttpStatusCode.InternalServerError;
        var notifier = CreateNotifier();

        await notifier.StartAsync();

        notifier.TotalUnread.Should().Be(0);
        _connection.StartCount.Should().Be(1);
    }

    [Fact]
    public async Task Start_swallows_a_connection_failure_and_a_later_start_retries()
    {
        var notifier = CreateNotifier();
        _connection.StartShouldThrow = true;

        await notifier.StartAsync();
        _connection.StartShouldThrow = false;
        await notifier.StartAsync();

        _connection.StartCount.Should().Be(2);
        _connection.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task A_received_note_increments_its_campaign_and_raises_both_events()
    {
        _unread = new() { new("c1", "Ruína", 1) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();
        var received = new List<SecretNoteNotification>();
        var changed = 0;
        notifier.Received += received.Add;
        notifier.Changed += () => changed++;

        await _connection.RaiseReceivedAsync(new SecretNoteNotification("c1", "Ruína"));
        await _connection.RaiseReceivedAsync(new SecretNoteNotification("c9", "Nova"));

        notifier.UnreadFor("c1").Should().Be(2);
        notifier.UnreadFor("c9").Should().Be(1);
        received.Should().Equal(new SecretNoteNotification("c1", "Ruína"), new SecretNoteNotification("c9", "Nova"));
        changed.Should().Be(2);
    }

    [Fact]
    public async Task SecretNotesChanged_reloads_the_counts_without_raising_Received()
    {
        _unread = new() { new("c1", "Ruína", 2) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();
        var received = 0;
        notifier.Received += _ => received++;
        _unread = new();

        await _connection.RaiseChangedAsync();

        notifier.TotalUnread.Should().Be(0);
        received.Should().Be(0);
    }

    [Fact]
    public async Task MarkCampaignRead_posts_and_clears_only_that_campaign()
    {
        _unread = new() { new("c1", "Ruína", 2), new("c2", "Outra", 1) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();

        await notifier.MarkCampaignReadAsync("c1");

        _requests.Should().Contain(r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath.EndsWith("campaigns/c1/secret-notes/mark-read"));
        notifier.UnreadFor("c1").Should().Be(0);
        notifier.TotalUnread.Should().Be(1);
    }

    [Fact]
    public async Task Stop_closes_the_connection_and_clears_the_counts()
    {
        _unread = new() { new("c1", "Ruína", 2) };
        var notifier = CreateNotifier();
        await notifier.StartAsync();

        await notifier.StopAsync();

        _connection.StopCount.Should().Be(1);
        notifier.TotalUnread.Should().Be(0);
    }
}
