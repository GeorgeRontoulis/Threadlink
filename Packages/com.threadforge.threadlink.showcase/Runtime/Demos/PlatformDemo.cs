namespace Threadforge.Threadlink.Showcase.Demos
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using UnityEngine;
    using UnityEngine.UIElements;

    /// <summary>Sentinel accounts and capability routing.</summary>
    public sealed class PlatformDemo : ShowcaseDemo
    {
        // A project uses its generated Achievements class.
        private static readonly AchievementId Visited = new AchievementId("showcase-visited");

        public override string Title => "Platform";

        public override string Summary =>
            "Sentinel starts with a local profile. A platform package such as Steam adds its provider and capabilities route to it. Asking for something the platform cannot do returns Unsupported or Unavailable, never a silent no-op.";

        public override string[] Packages => new[] { "sentinel" };

        public override void Build(VisualElement panel, ShowcaseContext context)
        {
            var account = Sentinel.Account;
            panel.Add(Value("Account: " + (account.HasValue ? account.Value.Provider + " / " + account.Value.Value : "none")));
            panel.Add(Value("Achievements: " + Sentinel.Status(Capability.Achievements).Availability));
            panel.Add(Row(
                Action(context, "Unlock an achievement", async () =>
                {
                    var unlocked = await Sentinel.UnlockAchievementAsync(Visited);
                    context.Log(unlocked.Succeeded ? "Unlocked." : "Not unlocked: " + unlocked.Error + " " + unlocked.Detail);
                }),
                Action(context, "Switch to profile 2", () =>
                {
                    Sentinel.SelectProfile("Player 2");
                    context.Log("Saves now belong to " + Sentinel.Account.Value.Value + ".");
                })));
        }

        public override async UniTask SmokeAsync(ShowcaseContext context, CancellationToken token)
        {
            Check(Sentinel.Account.HasValue, "a local profile is selected from the start");
            Check(Sentinel.TryGet<IAccounts>(out var accounts) && accounts.Current.HasValue, "accounts route to the local provider");
            var unlocked = await Sentinel.UnlockAchievementAsync(Visited, token);
            Check(unlocked.Succeeded || unlocked.Error == PlatformError.Unsupported || unlocked.Error == PlatformError.Unavailable, "achievements answer explicitly");
        }
    }
}
