# Fixed Point

## FP

A Q32.32 fixed-point number: 32 integer and 32 fractional bits in a `long`, for a range of about ±2.1 billion at a uniform resolution of 2^-32 (about 2.3e-10). Every operation is integer arithmetic.

- **Exact arithmetic.** `+`, `-`, `*`, `/` and `%` give the true result, rounded to nearest with ties away from zero, so results are symmetric in sign: `(-a) * b == -(a * b)`. `%` is the remainder of truncated division, with the dividend's sign. `MultiplyTowardZero(a, b)` truncates the exact product instead, for conservative geometry that must never overshoot.
- **Saturation.** Results beyond the range stop at `MaxValue` or `MinValue` instead of wrapping to the opposite sign.
- **Nothing throws.** Dividing by zero saturates toward the dividend's sign (zero over zero is zero). Each function states its value outside its domain: `Sqrt` of a negative value is zero, `Log` and `Log2` of zero or less are `MinValue`, `Asin` and `Acos` clamp their argument to [−1, 1], and `Pow` of a negative base with a fractional exponent is zero.
- **Functions.** `Sqrt` is exact: the true root, rounded to nearest. `Sin`, `Cos`, `SinCos`, `Tan`, `Asin`, `Acos`, `Atan`, `Atan2`, `Exp`, `Log`, `Log2` and `Pow` are evaluated in Q3.61 and rounded once, to within about one raw unit of the true value. Trigonometric arguments are reduced exactly, so `Sin(1e6)` is as accurate as `Sin(1)`. `Pow` with a whole exponent multiplies by repeated squaring.
- **Rounding.** `Floor`, `Ceiling`, `Round` (halves away from zero), `Truncate`, `Frac`, and `FloorToInt`, `CeilToInt` and `RoundToInt`.
- **Helpers.** `Abs`, `Sign`, `Min`, `Max`, `Clamp`, `Clamp01`, `Lerp`, `LerpUnclamped`, `InverseLerp` and `MoveTowards`.
- **Constants.** `Zero`, `One`, `MinusOne`, `Half`, `Two`, `Epsilon` (2^-32), `MaxValue`, `MinValue`, `Pi`, `TwoPi`, `PiOver2`, `PiOver4`, `E`, `Ln2`, `Deg2Rad` and `Rad2Deg`.
- **Construction and conversion.** `FromInt`, `FromFraction(numerator, denominator)` and `FromRaw(raw)`; `int` converts implicitly and exactly; `long`, `float` and `double` convert explicitly, saturating (NaN is zero). Converting to `int` rounds toward negative infinity.

```csharp
FP speed = 3;                           // integers convert implicitly and exactly
FP chance = FP.FromFraction(3, 20);     // exactly the nearest value to 0.15
FP friction = (FP)0.92;                 // a literal, converted once: deterministic
FP angle = FP.Atan2(dy, dx) * FP.Rad2Deg;
```

Converting a literal or an authored value (a serialized field) is deterministic, since its bits are the same everywhere. A float computed at runtime is not, so it never flows into simulated state. Serialize and hash `RawValue`, never a converted float. `(float)value` is for presentation. `GetHashCode` is defined explicitly from `RawValue`, so no runtime can change it, and `ToString` formats with the invariant culture.

## Vectors and rotations

`FPVector2`, `FPVector3` and `FPQuaternion` are mutable structs of `FP` components, with the usual operators.

- **Exact lengths.** `Magnitude` is the true length of the raw components (a 128-bit sum of squares and an exact root), rounded once. `SqrMagnitude` is rounded once and saturated. `Normalized` scales the components exactly before measuring, so a vector one raw unit long normalizes as well as one a million units long; the zero vector normalizes to zero.
- **`FPVector2`.** `Zero`, `One`, `Up`, `Down`, `Left`, `Right`; `Dot`, `Cross` (the z of the 3D cross product), `Distance`, `SqrDistance`, `Lerp`, `LerpUnclamped`, `MoveTowards`, `ClampMagnitude`, `Perpendicular` (a quarter turn counterclockwise), `Rotate`, `FromAngle`, `Angle` and `SignedAngle`.
- **`FPVector3`.** `Zero`, `One`, `Up`, `Down`, `Left`, `Right`, `Forward`, `Back`; `Dot`, `Cross`, `Distance`, `SqrDistance`, `Lerp`, `LerpUnclamped`, `MoveTowards`, `ClampMagnitude`, `Angle`, `Project` and `ProjectOnPlane`. Axes follow Unity: y up, z forward.
- **`FPQuaternion`.** Unit rotations: `Identity`, `FromAxisAngle` (a zero axis is no turn), composition with `*` (`a * b` rotates by `b`, then by `a`), `Rotate` (or `rotation * vector`), `Conjugate`, `Inverse`, `Dot`, `Magnitude`, `SqrMagnitude`, `Normalized`, `Lerp` (normalized), `Slerp` and `Angle`. Renormalize a rotation composed many times over, since each product rounds.

## Conversions

`FixedPointConversions`, in `Threadlink.Runtime` (namespace `Threadlink.Deterministic`), converts between these and Unity's types:

| Direction | Members | Use |
|---|---|---|
| To fixed point | `ToFP()` on `float`, `Vector2`, `Vector3`, `Vector2Int`, `Vector3Int` and `Quaternion` (renormalized after conversion) | Authored data and setup |
| To Unity types | `ToFloat()`, `ToVector2()`, `ToVector3()` (from `FPVector3`, or from `FPVector2` in the xy plane) and `ToQuaternion()` | Presentation only |
