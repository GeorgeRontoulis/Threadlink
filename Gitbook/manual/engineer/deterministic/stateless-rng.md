# StatelessRNG

The one source of random numbers for gameplay, simulation, procedural generation and presentation alike. A **stream** is a pure function of the world's seed, a **domain** (what the numbers are for) and **identity parts** (whose, and when: an entity, a tick, a map cell), folded in order. The same inputs give the same numbers on every machine, in any order and on any thread, however many draws happen elsewhere. Nothing is shared or advanced globally.

```csharp
static readonly StatelessRNG.Domain Loot = StatelessRNG.Domain.Named("Loot");

var rng = new StatelessRNG.Source(worldSeed);     // one per world; the world's save keeps the seed
var loot = rng.CreateStream(Loot, chestID, day);

int gold = loot.Range(10, 50);                    // [10, 50), every value equally likely
bool rare = loot.Chance(FP.FromFraction(1, 20));  // exact to the raw unit
bool coin = loot.Chance(1, 2);                    // integer odds
FP scale = loot.Range(FP.Half, (FP)1.5);
int pick = loot.WeightedIndex(weights);           // in proportion to the weights
loot.Shuffle(deck);                               // Fisher–Yates, every order equally likely
FPVector2 scatter = loot.InsideUnitCircle();

var item = loot.Fork(itemIndex);                  // a finer identity: independent of loot's own draws
```

## Sources and seeds

A `StatelessRNG.Source` holds a world's seed. Every machine simulating a world uses the same seed, and the world's save keeps it. There is no process-wide seed: a host's authoritative simulation and its prediction, two worlds in a test, or a world and a preview each keep their own source, so they never share randomness. Sources are values; two sources with the same seed give the same streams, and a `default` source is the source of seed 0.

## Streams and identity

- **Streams.** Each draw advances the stream copy it is made on; `Position` counts the draws. Create streams where you roll, one per purpose, entity and tick, so a result never depends on how many draws came before it. Two streams created from the same identity give the same numbers. `Fork(part)` derives a finer stream from a stream's identity, independent of the draws made on it.
- **Identity.** Parts fold in order through SplitMix64's finalizer, with each part mixed on its own first. Order matters, equal parts don't cancel, a zero seed with zero parts isn't degenerate, and a domain's whole 32-bit ID counts. `CreateStream` takes a domain alone, up to three `ulong` parts, a `ReadOnlySpan<ulong>` of any number (`stackalloc` allocates nothing), or an `IContext`.
- **Contexts.** An `IContext` is a readonly struct that adds parts that belong together, always in the same order, through `Compose(ref StatelessRNG.Identity identity)`. `Identity.Add` accepts `ulong`, `long`, `uint`, `int`, `FP` (its raw value) and `string`.
- **Strings.** `StatelessRNG.Part(text)` turns a string into a part, hashed as Threadlink hashes names (trimmed, ignoring case). Hash fixed strings once and keep the part.

```csharp
public readonly struct AttackRoll : StatelessRNG.IContext
{
    public readonly uint Attacker, Target, Tick;

    public void Compose(ref StatelessRNG.Identity identity)
    {
        identity.Add(Attacker);
        identity.Add(Target);
        identity.Add(Tick);
    }
}
```

## Samplers

| Member | Result |
|---|---|
| `NextUInt64()`, `NextUInt32()` | Raw bits. |
| `Range(int min, int max)`, `Index(count)` | An integer in [min, max), or [0, count). |
| `Range(FP min, FP max)`, `Value01()` | An `FP` in [min, max), or [0, 1). |
| `Bool()`, `Chance(FP probability)`, `Chance(numerator, denominator)` | A coin flip, or an outcome at the given odds. |
| `Angle()` | An angle in [0, 2π). |
| `OnUnitCircle()`, `InsideUnitCircle()`, `OnUnitSphere()`, `InsideUnitSphere()` | Points on or within the unit circle or sphere. |
| `Rotation()` | A uniformly random rotation. |
| `Pick(items)` | An element of a span, array or read-only list. |
| `Shuffle(items)` | Fisher–Yates over a span, array or list. |
| `WeightedIndex(weights)` | An index in proportion to `int` or `FP` weights. |

Every sampler is unbiased (Lemire's method, redrawing the rare biased case), and makes the same draws whatever its arguments' values: a probability of zero advances the stream as much as any other.

**Not for secrets or uniqueness.** Session tokens and nonces come from `System.Security.Cryptography.RandomNumberGenerator`, and unique IDs from `Guid`.

## Domains

Domains partition streams: systems drawing from one seed under distinct domains do not interfere. A `StatelessRNG.Domain` is a 32-bit value. Create one with an explicit number, `new StatelessRNG.Domain(17)`, or from a name, `StatelessRNG.Domain.Named("Loot")`, which hashes the name the same way as `Part`. Keep domains in static fields so the name is hashed once.

> A domain's value is part of the save and replay format, as is the stream format itself. Changing a domain's number or the name it is hashed from changes its streams, causing divergence in anything that reproduces a sequence from a stored seed or replay. Rename the C# field freely, but keep the value.

## Generated domains

Instead of calling `StatelessRNG.Domain.Named` in code, list the domains in an [ID domain](../editor-tooling.md#id-domains) that extends **Random domains**. It generates `RandomDomains.Name`, each a `StatelessRNG.Domain` with a fixed id. A new entry starts with the id `Domain.Named(name)` would give, so replacing a named domain with its generated one keeps every stream identical, and renaming the entry later keeps the id.
