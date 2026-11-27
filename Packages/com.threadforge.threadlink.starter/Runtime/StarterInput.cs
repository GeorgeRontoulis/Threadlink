namespace Threadforge.Threadlink.Starter
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Typed IDs of the starter's own input actions (<c>Configuration/StarterInput</c>), in the form Threadlink ▸ Generate
    /// IDs produces for a project's actions. A game made from the starter uses its generated <c>GameInput</c> instead.
    /// </summary>
    public static class StarterInput
    {
        /// <summary>Action maps.</summary>
        public static class Maps
        {
            /// <summary>The Gameplay map.</summary>
            public static readonly InputMapId Gameplay = new InputMapId(new Guid("5b0e3c1a-1d2e-4f30-8a41-0b1c2d3e4f51"), "Gameplay");

            /// <summary>The UI map.</summary>
            public static readonly InputMapId UI = new InputMapId(new Guid("39e614f8-fb0c-4d1e-822f-e3f40516273f"), "UI");
        }

        /// <summary>Gameplay actions.</summary>
        public static class Gameplay
        {
            /// <summary>Movement on the ground plane.</summary>
            public static readonly InputId<Vector2> Move = new InputId<Vector2>(new Guid("6c1f4d2b-2e3f-4041-9b52-1c2d3e4f5a62"), "Gameplay/Move");

            /// <summary>Interact with the selected target.</summary>
            public static readonly InputId<float> Interact = new InputId<float>(new Guid("7d2a5e3c-3f40-4152-8c63-2d3e4f5a6b73"), "Gameplay/Interact");

            /// <summary>Open or close the pause menu.</summary>
            public static readonly InputId<float> Pause = new InputId<float>(new Guid("8e3b6f4d-4051-4263-9d74-3e4f5a6b7c84"), "Gameplay/Pause");
        }

        /// <summary>UI actions.</summary>
        public static class UI
        {
            /// <summary>Close the current screen.</summary>
            public static readonly InputId<float> Cancel = new InputId<float>(new Guid("4af72509-0c1d-4e2f-9340-f40516273840"), "UI/Cancel");
        }
    }
}
