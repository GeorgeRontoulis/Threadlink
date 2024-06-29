# IIdentifiable

The `IIdentifiable` interface defines the base contract for all objects that are associated with an ID. The ID is of type `string`, however the exact value of that ID can be customized. For the lowest-level objects Threadlink uses to run, like `LinkableBehaviour` and `LinkableAsset`, that ID is usually the name of the GameObject, ScriptableObject, or Asset for simplicity, however it is generally a good idea to ensure some sort of uniqueness when working with IDs for your game. MassTransit provides a very robust nuget package for generating unique IDs, both in the Unity Editor and at runtime, which is a good starting point.

{% code title="Namespace" overflow="wrap" fullWidth="false" %}
```csharp
Threadlink.Utilities.Collections
```
{% endcode %}

## Dependencies

_This interface has no dependencies._

The `IIdentifiable` interface provides a framework for managing collections within the Threadlink system. Utility Methods like BinarySearch and SortByID depend on this interface to perform lookups or sort collections according to the IDs associated with objects.

## Properties

{% code overflow="wrap" %}
```csharp
public string LinkID { get; }
```
{% endcode %}

The `LinkID` property is a `string` that is compared during lookups or sort operations of collections exclusively containing Threadilnk-Compatible objects. It enables the more efficient management of collections in your codebase, including Arrays and Lists.
