# Scribe: Logging

```csharp
using Threadlink.Core.NativeSubsystems.Scribe;

this.Send("Loaded ", count, " entries.").ToUnityConsole();
Scribe.Send<MySystem>("Static context message.").ToUnityConsole(DebugType.Error);
```

`Send` accepts `params object[]` and appends each element to a ZString UTF-8 builder, so multi-argument invocation avoids materialising an interpolated string. It returns the builder; `ToUnityConsole(DebugType)` writes it to the Unity console. `DebugType` enumerates `Info` (the default), `Warning` and `Error`.

The message prefix is a type name, in rich-text colour: the runtime type of the receiver for the extension form, or the supplied type argument for the static form. Each severity is also tinted (white, yellow, red).

Scribe is the logging path throughout the framework's runtime and most of its editor tooling. A few build-time tools (the Sentinel build pipeline, the Steam App ID processor, the code formatter, and **Match Addressables to Paths**) log through `Debug` directly.
