# Dependency distribution notices

MessagePack 3.1.12 runtime, annotations, analyzers and source generator originate from MessagePack-CSharp commit eb108f89700db93e67f10148b777380c244ac6ae. See LICENSE.MessagePack.txt for its MIT, lz4net BSD and BufferWriter notices; LICENSE.Apache-2.0.txt supplies the referenced Apache license.

Microsoft.NET.StringTools 17.11.4 originates from Microsoft MSBuild commit 37eb419ad2c986ac5530292e6ee08e962390249e. See LICENSE.StringTools.txt. System.Collections.Immutable 8.0.0 retains its upstream LICENSE.TXT inside the corresponding directory. Original NuGet metadata and binary importer GUIDs accompany all components. Binaries are unchanged from the existing working project.

This is a pinned local UPM redistribution wrapper, not a claim of upstream authorship. Unity's base framework provides the runtime dependencies already supplied by its .NET Standard profile; clean consumer builds verify the selected closure. Unity-specific formatter source is a separate optional package.
