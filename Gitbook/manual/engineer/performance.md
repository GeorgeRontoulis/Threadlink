# Performance Constraints

| Practice | Rationale |
|---|---|
| Cache `Chronos.DeltaTime` into a local once per tick. | Eliminates repeated static property access in hot loops. |
| Log through `Scribe` rather than `Debug.Log`. | ZString composition avoids interpolated strings, and the prefix identifies the source. |
| Use `UniTask` exclusively; avoid `System.Threading.Tasks` and coroutines in framework code. | Mixing violates the single-threaded model the framework assumes. |
| Unsubscribe every Iris listener in `Discard()`. | Retained delegates keep destroyed objects reachable and dispatch to stale state. |
| Confine replay-sensitive and cross-machine deterministic logic to `FP` and `StatelessRNG`. | Hardware floating point and `UnityEngine.Random` are not suitable for Threadlink's deterministic contract. |
| Keep state that is copied, hashed or saved byte for byte in [Memory](memory.md) blocks and inline collections. | Raw-byte hashing and comparison are only stable when layout and padding are. |
| Prefer `OnLateUpdate` for camera-relative computation. | Dispatch occurs after camera transformation. |
| Hash fixed strings once (`StatelessRNG.Part`, `HashFunctions`) and keep the result. | Hashing a string trims and lower-cases it first, which allocates; a kept hash costs nothing. |
