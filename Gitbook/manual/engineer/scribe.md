# Scribe: Logging

```csharp
using Threadlink.Core.NativeSubsystems.Scribe;

this.Send("Loaded ", count, " entries.").ToUnityConsole();
Scribe.Send<MySystem>("Static context message.").ToUnityConsole(DebugType.Error);
```

`Send` takes `params object[]` and appends each argument to a ZString UTF-8 builder, so passing several arguments avoids building an interpolated string. It returns the builder; `ToUnityConsole(DebugType)` writes it to the console. `DebugType` is `Info` (the default), `Warning` or `Error`.

Each message starts with a type name in colour: the receiver's runtime type for the extension form, or the type argument for the static form. Severities are tinted white, yellow and red.

The runtime and most editor tools log through Scribe. A few build-time tools (the Sentinel build pipeline, the Steam App ID processor, the code formatter and **Match Addressables to Paths**) use `Debug` directly. Scribe counts the messages it sends this session, by severity, and keeps the last six; the [Runtime Monitor](editor-tooling.md#runtime-monitor) shows both.
