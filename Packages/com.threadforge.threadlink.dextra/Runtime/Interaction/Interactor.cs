namespace Threadforge.Threadlink
{
    using Cysharp.Threading.Tasks;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>Current availability and the prompt/reason shown to an actor.</summary>
    public readonly struct InteractionAvailability
    {
        public bool Available { get; }
        public string Prompt { get; }

        public InteractionAvailability(bool available, string prompt)
        {
            Available = available;
            Prompt = prompt;
        }
    }

    /// <summary>Optional interaction implemented by ordinary C# objects or components; no registration/base class is required.</summary>
    public interface IInteractable
    {
        Guid Identity { get; }
        InteractionAvailability Evaluate(object actor);
        UniTask ExecuteAsync(object actor, CancellationToken token);
    }

    /// <summary>A detector-provided candidate score; smaller scores are preferred.</summary>
    public readonly struct InteractionCandidate
    {
        public IInteractable Target { get; }
        public double Score { get; }

        public InteractionCandidate(IInteractable target, double score)
        {
            Target = target;
            Score = score;
        }
    }

    /// <summary>Target selection, current prompts and guarded required execution, independent of input and physics.</summary>
    public sealed class Interactor
    {
        public IInteractable Selected { get; private set; }
        public bool IsExecuting { get; private set; }

        public InteractionAvailability Availability(object actor) =>
            Selected?.Evaluate(actor) ?? new InteractionAvailability(false, string.Empty);

        /// <summary>Chooses the best available candidate, breaking score ties by stable identity.</summary>
        public void Select(IEnumerable<InteractionCandidate> candidates, object actor)
        {
            IInteractable best = null;
            double score = double.PositiveInfinity;
            foreach (var candidate in candidates)
            {
                if (
                    candidate.Target == null
                    || double.IsNaN(candidate.Score)
                    || !candidate.Target.Evaluate(actor).Available
                )
                    continue;
                if (
                    best == null
                    || candidate.Score < score
                    || (
                        candidate.Score == score
                        && candidate.Target.Identity.CompareTo(best.Identity) < 0
                    )
                )
                {
                    best = candidate.Target;
                    score = candidate.Score;
                }
            }
            Selected = best;
        }

        /// <summary>Revalidates the prompted target immediately before execution. Required work is awaited directly.</summary>
        public async UniTask ExecuteAsync(
            Guid expectedTarget,
            object actor,
            CancellationToken token = default
        )
        {
            token.ThrowIfCancellationRequested();
            var target = Selected;
            if (
                IsExecuting
                || target == null
                || target.Identity != expectedTarget
                || !target.Evaluate(actor).Available
            )
                throw new InvalidOperationException(
                    "The prompted interaction is no longer available."
                );
            IsExecuting = true;
            try
            {
                await target.ExecuteAsync(actor, token);
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }
}
