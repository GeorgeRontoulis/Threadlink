# Code Generation & External Plugins

#### Code Generation (CodeGen)

To maintain performance and type-safety, Threadlink utilizes aggressive edit-time code generation (`Threadlink/Editor/CodeGen/`).

* Generates static lookup IDs for Addressables, Iris Events, Nexus Spawn Points, RNG Domains, and Vault Fields.
* This prevents runtime string hashing and ensures broken references result in compile-time errors rather than runtime exceptions.

#### Integrated Third-Party Tooling

Threadlink natively wraps and extends industry-standard Unity packages:

* UniTask: Replaces Coroutines with zero-allocation `async/await` state machines. The framework includes deep Addressables and DOTween async extensions.
* MessagePack: Secure and highly optimized binary serializer used for data persistence, networking or other operations.
* ZString: A zero-allocation string builder to format text for UI and logs without triggering the Garbage Collector.
