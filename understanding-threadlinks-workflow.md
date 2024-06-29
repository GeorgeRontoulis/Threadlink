# 🕹️ Understanding Threadlink's Workflow

Threadlink is based on a linked node structure, a network in other words. With the core system acting as the central node, sub-systems form connections (threads[^1]) that link them to that core, hence the name.

This structure allows for the creation of an absolute chain of command in the game's lifecycle. The core system kicks everything off, creating and initializing its linked sub-systems when the game boots up. The linked sub-systems, in turn, initialize their own linked entities and start performing their designated tasks. For every event, be it system or entity creation, initialization, or even disposal, there are various [callbacks ](#user-content-fn-2)[^2]that propagate throughout the network, which in turn makes it easier to automate tasks and type cleaner and more efficient code.

Threadlink is a tool tailored towards experienced programmers. To make the most out of this tool, extended knowledge of both Unity and C# is required.

[^1]: Not to be confused with the threads of parallel programming.

[^2]: Pieces of code executed as responses to events.
