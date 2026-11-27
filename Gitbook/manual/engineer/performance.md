# Performance

## Guarantees

- Warmed synchronous `Iris.Publish` and installed frame delivery allocate no managed memory, excluding consumer callbacks. The maintained runtime suites verify this with an allocation instrument that must first detect a known allocation.
- Awaited publication adds no allocation of its own. Asynchronous APIs return UniTask, whose async machinery is pooled; an `async UniTask` call that completes synchronously allocates nothing in release builds. The Editor compiles in Debug mode, where each `async` method call allocates its state machine once, so the runtime suites compare awaited publication against a plain `async UniTask` call.
- Disabled diagnostics capture allocates nothing per operation.

Other operations allocate where their contract requires it: subscriptions and posts allocate records, suspended async work allocates state machines, content requests allocate request records, and serialization allocates its output. Keep those out of per-frame loops.

## Measurements

Timings are hardware-specific baselines, not limits. Profile your own target backend with representative workloads.

Iris, Windows Standalone Mono player, non-development build, AMD Ryzen 9 9950X3D, Unity 6000.6.5f1, 101 samples of 10,000 operations after warm-up, including one trivial callback:

| Operation | Median ns/op | p95 | p99 |
| --- | ---: | ---: | ---: |
| Synchronous publication | 36.6 | 37.5 | 37.9 |
| Installed Update callback (empty ingress drain, two callbacks) | 74.7 | 75.9 | 78.6 |

Diagnostics, Editor Mono, same machine, Unity 6000.6.9f1, 101 samples after warm-up; allocations counted with the profiler's `GC.Alloc` recorder:

| Operation | Workload | Median | p95 | GC allocations per operation |
| --- | --- | ---: | ---: | ---: |
| Disabled capture | Begin, Stage, Trace, Succeed | 49.5 ns | 55.8 ns | 0 |
| Shipping capture (16 history) | Begin, Stage, Trace, Succeed | 0.77 µs | 1.77 µs | 2 |
| Development capture (128 history) | Begin, Stage, Trace, Succeed | 0.77 µs | 1.07 µs | 2 |
| Development capture with traces | Begin, Stage, Trace, Succeed | 1.17 µs | 1.91 µs | 3 |

## Practices

| Practice | Rationale |
|---|---|
| Subscribe once and keep the token; dispose it with its owner. | Subscription churn allocates; publication does not. |
| Post from workers with `Iris.TryPost`; posts arrive at the next Update. | Ingress is bounded and rejects instead of growing. |
| Keep replay-sensitive logic in `FP` and `StatelessRNG`. | Hardware floating point and `UnityEngine.Random` are not reproducible across machines. |
| Keep state that is copied, hashed or saved byte for byte in [Memory](memory.md) blocks. | Raw-byte hashing is stable only when layout and padding are. |
| Hash fixed strings once (`StatelessRNG.Part`, `StatelessRNG.Domain.Named`) and keep the result. | Hashing trims and lower-cases the string first, which allocates. |
| Reuse objects with `UnityEngine.Pool.ObjectPool<T>`, and use `ListPool<T>` or `CollectionPool` for scratch collections. | Unity's pools need no extra package. |
| Retain content requests deliberately and release them at a clear boundary. | Each acquisition holds its Addressables handle until released. |
| Keep awaited events for work that must finish before the caller continues. | Synchronous events are cheaper; awaited ones suspend and resume. |
| Start work you do not await with `.Forget()`. | Failures are reported at once instead of when the task is collected. |
