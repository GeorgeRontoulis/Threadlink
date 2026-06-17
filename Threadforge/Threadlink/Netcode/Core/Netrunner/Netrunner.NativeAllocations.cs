namespace Threadlink.Netcode
{
    using System.Runtime.CompilerServices;
    using Unity.Collections;
    using Utilities.Collections;

    public partial class Netrunner
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AllocateNativeResources()
        {
            connections = new(MAX_PLAYERS, Allocator.Persistent);
            playerIndicesMap = new(MAX_PLAYERS, Allocator.Persistent);
            availablePlayerIndices = new(Allocator.Persistent);
            sessionFlowEventsBuffer = new(64, Allocator.Persistent);
            this.PreventEditorMemoryLeaks();
        }
    }
}
