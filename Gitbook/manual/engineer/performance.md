# Performance Constraints

| Practice | Why |
|---|---|
| Copy `Chronos.DeltaTime` into a local once per tick. | Avoids repeated static property access in hot loops. |
| Log through `Scribe`, not `Debug.Log`. | ZString avoids interpolated strings, and the prefix names the source. |
| Use `UniTask` only; no `System.Threading.Tasks` or coroutines in framework code. | Mixing them breaks the single-threaded model the framework assumes. |
| Unsubscribe every Iris listener in `Discard()`. | A delegate left subscribed keeps a destroyed object alive and runs on stale state. |
| Keep replay-sensitive and cross-machine deterministic logic to `FP` and `StatelessRNG`. | Hardware floats and `UnityEngine.Random` don't give the same results everywhere. |
| Keep state that is copied, hashed or saved byte for byte in [Memory](memory.md) blocks and inline collections. | Raw-byte hashing and comparison are only stable when layout and padding are. |
| Do camera-relative work on `OnLateUpdate`. | It runs after the camera has moved. |
| Hash fixed strings once (`StatelessRNG.Part`, `HashFunctions`) and keep the result. | Hashing a string trims and lower-cases it, which allocates; a kept hash costs nothing. |

## Measuring

The [Runtime Monitor](editor-tooling.md#runtime-monitor) shows FPS, main-thread time against a frame budget, GC allocation per frame, managed heap and memory while in Play Mode. Use it to spot regressions while iterating. Editor figures include the Editor's own work, so use the Unity Profiler on a development build for numbers to budget against.
