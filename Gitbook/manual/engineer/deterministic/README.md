# Deterministic Toolkit

Everything a simulation needs to give the same result on every machine, runtime and compiler: numbers, vectors and rotations in fixed point, and the framework's one source of randomness. Networked simulation, lockstep, replays and procedural generation build on it.

The toolkit lives in the `Threadlink.Deterministic` assembly (namespace `Threadlink.Deterministic`), which references no engine type, so a deterministic simulation assembly can depend on it without depending on Unity. Its only Threadlink dependencies are `Threadlink.Shared` (hashing) and `Threadlink.Generated` (RNG domains).

| Page | Covers |
|---|---|
| [Fixed Point](fixed-point.md) | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion`, and conversion to and from Unity types. |
| [StatelessRNG](stateless-rng.md) | Streams, sources, identity, samplers, domains. |

`Bits` provides bit counts and rotations of 64-bit words that are the same on every runtime (Mono, IL2CPP and CoreCLR): `TrailingZeroCount`, `LeadingZeroCount`, `PopCount` and `RotateLeft`.

State that is copied, hashed and saved as raw bytes, such as a simulation's frames, is the domain of the [Memory](../memory.md) toolkit.
