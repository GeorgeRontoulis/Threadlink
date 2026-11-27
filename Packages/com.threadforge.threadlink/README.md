# Threadlink

The Threadlink core. Subsystems register at `SubsystemRegistration`, deploy before the first scene's Awake and shut down in reverse order; `ThreadlinkSubsystem<T>.TryGetSingleton` and `Instance` give access from anywhere. Includes Iris events, Chronos game time (pause claims and playtime), content loading by generated ID through Addressables (the Content mapping), Vault typed definitions (generated mapping and designer-built field sets), generated IDs and project ID domains, engineering settings, memory toolkits, project setup, the Scenes toolbar and the Editor Workspace (Hub, Runtime monitor, Vault browser, Code Generation and Content pages). Asynchronous APIs return UniTask.

Open **Threadlink ▸ Workspace** for the Hub, the Runtime monitor and project validation. Documentation: https://threadforge.gitbook.io/threadlink-documentation/
