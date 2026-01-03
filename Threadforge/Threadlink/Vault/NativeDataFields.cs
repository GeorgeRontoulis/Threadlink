namespace Threadlink.Vault.DataFields
{
    using System;
    using Unity.Mathematics;
    using UnityEngine;

#if THREADLINK_MATHEMATICS
    [Serializable] public sealed class Integer2 : DataField<int2> { }
#endif

    [Serializable] public sealed class Integer : DataField<int> { }
    [Serializable] public sealed class Float : DataField<float> { }
    [Serializable] public sealed class Boolean : DataField<bool> { }
    [Serializable] public sealed class Double : DataField<double> { }
    [Serializable] public sealed class Long : DataField<long> { }
    [Serializable] public sealed class Vector2D : DataField<Vector2> { }
    [Serializable] public sealed class Vector3D : DataField<Vector3> { }
    [Serializable] public sealed class Rotation : DataField<Quaternion> { }
}
