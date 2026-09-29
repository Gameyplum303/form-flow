using Bunit;
using FluentAssertions;
using FormFlow.Blazor.Components.Pages.Admin;
using FormFlow.Blazor.Services;
using FormFlow.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace FormFlow.Blazor.Tests.Admin;

public class AdminSignUpsTests
{
    private readonly FakeAccountService _accounts = new();

    private BunitContext CreateContext()
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddMudServices();
        ctx.Services.AddSingleton<IAccountService>(_accounts);
        ctx.Render<MudPopoverProvider>();
        return ctx;
    }

    private static PendingAccount Pending(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = $"{name.ToLowerInvariant()}@lab.example",
        DateOfBirth = "1990-12-10",
        IntendedUse = "Surveys for my studies.",
        Organization = "Analytical Engines Lab",
        CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Lists_each_sign_up_with_its_details()
    {
        await using var ctx = CreateContext();
        _accounts.Pending.Add(Pending("Ada"));

        var cut = ctx.Render<AdminSignUps>();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(1));
        var row = cut.Find("tbody tr").TextContent;
        row.Should().Contain("ada@lab.example").And.Contain("Analytical Engines Lab").And.Contain("1990-12-10")
            .And.Contain("Surveys for my studies.");
    }

    [Fact]
    public async Task Shows_whether_each_person_verified_their_email()
    {
        await using var ctx = CreateContext();
        var ada = Pending("Ada");
        ada.EmailVerified = true;
        _accounts.Pending.AddRange([ada, Pending("Grace")]);

        var cut = ctx.Render<AdminSignUps>();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(2));
        cut.FindAll("[data-email-verified]").Select(c => c.TextContent.Trim()).Should().Equal("Verified", "Not verified yet");
    }

    [Fact]
    public async Task Says_when_no_one_is_waiting()
    {
        await using var ctx = CreateContext();

        var cut = ctx.Render<AdminSignUps>();

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("No one is waiting for approval."));
    }

    [Fact]
    public async Task Approving_removes_the_row_and_confirms()
    {
        await using var ctx = CreateContext();
        var ada = Pending("Ada");
        _accounts.Pending.Add(ada);
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = ctx.Render<AdminSignUps>();
        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(1));

        cut.Find("button[aria-label='Approve Ada']").Click();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().BeEmpty());
        _accounts.Approved.Should().Equal(ada.Id);
        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("Approved Ada. They can sign in now."));
    }

    [Fact]
    public async Task Declining_asks_first_then_removes_the_row()
    {
        await using var ctx = CreateContext();
        var ada = Pending("Ada");
        _accounts.Pending.Add(ada);
        var dialogs = ctx.Render<MudDialogProvider>();
        var cut = ctx.Render<AdminSignUps>();
        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(1));

        cut.Find("button[aria-label='Decline Ada']").Click();
        dialogs.WaitForAssertion(() => dialogs.Markup.Should().Contain("Decline Ada's sign-up?"));
        dialogs.FindAll("button").First(b => b.TextContent.Trim() == "Decline").Click();

        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().BeEmpty());
        _accounts.Declined.Should().Equal(ada.Id);
    }

    [Fact]
    public async Task A_failed_approval_keeps_the_row_and_shows_why()
    {
        await using var ctx = CreateContext();
        _accounts.Pending.Add(Pending("Ada"));
        _accounts.NextError = "That sign-up is no longer waiting. Another administrator may have handled it.";
        var snackbars = ctx.Render<MudSnackbarProvider>();
        var cut = ctx.Render<AdminSignUps>();
        cut.WaitForAssertion(() => cut.FindAll("tbody tr").Should().HaveCount(1));

        cut.Find("button[aria-label='Approve Ada']").Click();

        snackbars.WaitForAssertion(() => snackbars.Markup.Should().Contain("no longer waiting"));
        cut.FindAll("tbody tr").Should().HaveCount(1);
    }

    [Theory]
    [InlineData("professor")]
    [InlineData("student")]
    public async Task Other_roles_and_previews_see_a_notice_instead(string role)
    {
        await using var ctx = CreateContext();
        _accounts.Pending.Add(Pending("Ada"));

        var cut = ctx.Render<AdminSignUps>(p => p.AddCascadingValue(new AdminAccess(role, Guid.NewGuid())));

        cut.Markup.Should().Contain("Only administrators can review sign-ups.");
        cut.Markup.Should().NotContain("ada@lab.example");
        _accounts.Loads.Should().Be(0);
    }

    private sealed class FakeAccountService : IAccountService
    {
        public List<PendingAccount> Pending { get; } = [];
        public List<Guid> Approved { get; } = [];
        public List<Guid> Declined { get; } = [];
        public string? NextError { get; set; }
        public int Loads { get; private set; }

        public Task<List<PendingAccount>> GetPendingAsync()
        {
            Loads++;
            return Task.FromResult(Pending.ToList());
        }

        public Task<List<SentEmail>?> GetOutboxAsync() => Task.FromResult<List<SentEmail>?>(null);

        public Task<string?> ApproveAsync(Guid id)
        {
            if (NextError is null)
            {
                Approved.Add(id);
            }
            return Task.FromResult(NextError);
        }

        public Task<string?> DeclineAsync(Guid id)
        {
            if (NextError is null)
            {
                Declined.Add(id);
            }
            return Task.FromResult(NextError);
        }
    }
}
