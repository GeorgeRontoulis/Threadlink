// Threadlink 1.0 examples: subsystems, events and the core. Compiled with the project; not used by any scene.
namespace Threadlink.User.Examples
{
    using Cysharp.Threading.Tasks;
    using System.Threading;
    using Threadforge.Threadlink;
    using UnityEngine;

    public readonly struct CoinsChanged
    {
        public readonly int Total;

        public CoinsChanged(int total) => Total = total;
    }

    /// <summary>
    /// A project subsystem. Deriving is all it takes: it registers itself, starts before the first scene's Awake and shuts
    /// down with the session. It boots first (its own state and its Iris subscriptions), then initializes once every
    /// subsystem has booted.
    /// </summary>
    public sealed class Wallet : ThreadlinkSubsystem<Wallet>
    {
        private SubscriptionToken ending;

        public int Coins { get; private set; }

        // Boot sets up the subsystem itself. Iris is the only other subsystem available while booting: subscribe here, and
        // the events other subsystems announce while they initialize arrive.
        protected override void OnSubsystemBoot() => ending = Iris.Subscribe<SessionEnding>(_ => Debug.Log("The session ends with " + Coins + " coins."));

        protected override void OnSubsystemShutdown()
        {
            ending.Dispose();
            Coins = 0;
        }

        /// <summary>The subsystem's API: static methods on the class.</summary>
        public static void Add(int amount)
        {
            Instance.Coins += amount;
            Iris.Publish(new CoinsChanged(Instance.Coins));
        }

        public override void Report(MonitorReport report)
        {
            report.Summary(Coins + " coins");
            if (report.Detailed)
                report.Section("Wallet").Field("Coins", Coins);
        }
    }

    /// <summary>Any MonoBehaviour can use subsystems directly in Awake or Start: they are already initialized.</summary>
    public sealed class CoinPickup : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other) => Wallet.Add(1);
    }

    /// <summary>TryGetSingleton checks that a subsystem exists and is linked to the Threadlink core.</summary>
    public sealed class CoinCounter : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Text that shows the coin total.")]
        private TextMesh label;

        private void Start()
        {
            if (Wallet.TryGetSingleton(out var wallet))
                label.text = wallet.Coins.ToString();

            // Ends when this component is destroyed; no OnDisable bookkeeping.
            Iris.Subscribe<CoinsChanged>(this, change => label.text = change.Total.ToString());

            // Frame callbacks without an Update method, also owner-bound.
            Iris.OnUpdate(this, deltaTime => label.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.time * 4f) * 0.03f));
        }
    }

    public readonly struct LevelCompleted
    {
        public readonly int Level;

        public LevelCompleted(int level) => Level = level;
    }

    public readonly struct NextLevelQuery
    {
        public readonly int After;

        public NextLevelQuery(int after) => After = after;
    }

    /// <summary>Awaited events: every listener runs in order and is awaited. Requests have exactly one responder.</summary>
    public sealed class LevelFlow : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Faded in when a level is completed.")]
        private CanvasGroup curtain;

        private void Start()
        {
            Iris.SubscribeAsync<LevelCompleted>(this, async (done, token) =>
            {
                for (float t = 0; t < 1; t += Time.deltaTime)
                {
                    curtain.alpha = t;
                    await UniTask.NextFrame(token);
                }
            });
            Iris.Respond<NextLevelQuery, int>(this, (query, token) => UniTask.FromResult(query.After + 1));
        }

        public async UniTask<int> CompleteAsync(int level, CancellationToken token)
        {
            await Iris.PublishAsync(new LevelCompleted(level), token);
            return await Iris.RequestAsync<NextLevelQuery, int>(new NextLevelQuery(level), token);
        }
    }

    /// <summary>Workers post events; they arrive on the main thread at the next Update.</summary>
    public static class BackgroundWork
    {
        public static void ReportFromWorker(int total) => Iris.TryPost(new CoinsChanged(total));
    }

    /// <summary>Recording an operation in the session log, visible in Threadlink ▸ Runtime Monitor.</summary>
    public static class ObservedWork
    {
        public static async UniTask UploadAsync(CancellationToken token)
        {
            var upload = Threadlink.Begin("Cloud", "Upload");
            upload.Stage("Sending");
            try
            {
                await UniTask.WaitForSeconds(0.5f, cancellationToken: token);
                upload.Succeed();
            }
            catch (System.OperationCanceledException)
            {
                upload.Cancel();
                throw;
            }
        }
    }
}
