namespace Threadforge.Threadlink
{
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>Explicit optional 2D/3D physics queries. Candidate targets may be ordinary MonoBehaviours implementing IInteractable.</summary>
    public static class PhysicsTargets
    {
        /// <summary>Finds nearby 3D targets with a caller-owned query buffer. Buffer saturation is explicit.</summary>
        public static bool Collect3D(
            Vector3 origin,
            float radius,
            int layerMask,
            Collider[] buffer,
            List<InteractionCandidate> results
        ) =>
            Collect3D(
                UnityEngine.Physics.defaultPhysicsScene,
                origin,
                radius,
                layerMask,
                buffer,
                results
            );

        /// <summary>Queries an explicitly selected physics scene; stepping remains the game's policy.</summary>
        public static bool Collect3D(
            PhysicsScene scene,
            Vector3 origin,
            float radius,
            int layerMask,
            Collider[] buffer,
            List<InteractionCandidate> results
        )
        {
            results.Clear();
            int count = scene.OverlapSphere(
                origin,
                radius,
                buffer,
                layerMask,
                QueryTriggerInteraction.Collide
            );
            for (int i = 0; i < count; i++)
            {
                var collider = buffer[i];
                foreach (var component in collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IInteractable target)
                    {
                        results.Add(
                            new InteractionCandidate(
                                target,
                                (collider.ClosestPoint(origin) - origin).sqrMagnitude
                            )
                        );
                        break;
                    }
                buffer[i] = null;
            }
            return count < buffer.Length;
        }

        /// <summary>Finds nearby 2D targets; does not install a player-loop callback.</summary>
        public static bool Collect2D(
            Vector2 origin,
            float radius,
            LayerMask layerMask,
            Collider2D[] buffer,
            List<InteractionCandidate> results
        ) => Collect2D(Physics2D.defaultPhysicsScene, origin, radius, layerMask, buffer, results);

        /// <summary>Queries a selected 2D physics scene without simulating it implicitly.</summary>
        public static bool Collect2D(
            PhysicsScene2D scene,
            Vector2 origin,
            float radius,
            LayerMask layerMask,
            Collider2D[] buffer,
            List<InteractionCandidate> results
        )
        {
            results.Clear();
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = layerMask,
                useTriggers = true,
            };
            int count = scene.OverlapCircle(origin, radius, filter, buffer);
            for (int i = 0; i < count; i++)
            {
                var collider = buffer[i];
                foreach (var component in collider.GetComponentsInParent<MonoBehaviour>())
                    if (component is IInteractable target)
                    {
                        results.Add(
                            new InteractionCandidate(
                                target,
                                (collider.ClosestPoint(origin) - origin).sqrMagnitude
                            )
                        );
                        break;
                    }
                buffer[i] = null;
            }
            return count < buffer.Length;
        }
    }
}
