# Deterministic Toolkit

Everything a simulation needs to give the same result on every machine, runtime and compiler: numbers, vectors and rotations in fixed point, and stateless randomness. Networked simulation, lockstep, replays and procedural generation build on it.

## Package

Install `com.threadforge.threadlink.deterministic`; its assembly is `Threadforge.Threadlink.Deterministic` and its types are in the `Threadforge.Threadlink` namespace. The assembly references no engine type, core subsystem or generated code, so a simulation assembly can depend on it without depending on Unity. It needs unsafe code only inside the package.

| Page | Covers |
|---|---|
| [Fixed Point](fixed-point.md) | `FP`, `FPVector2`, `FPVector3`, `FPQuaternion` and conversions. |
| [StatelessRNG](stateless-rng.md) | Sources, domains, streams, identity and samplers. |

`Bits` provides bit counts and rotations of 64-bit words that are the same on every runtime (Mono, IL2CPP and CoreCLR): `TrailingZeroCount`, `LeadingZeroCount`, `PopCount` and `RotateLeft`.

## Compatibility

Fixed-point results and StatelessRNG streams are part of the replay and save format. The package's tests include recorded golden values, so a change to any result fails the tests.

These primitives do not make Unity physics, animation or other floating-point engine code deterministic. State that is copied, hashed and saved as raw bytes, such as a simulation's frames, belongs in the [Memory](../memory.md) toolkit.
