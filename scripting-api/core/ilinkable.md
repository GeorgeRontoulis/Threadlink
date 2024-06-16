# ILinkable

The `ILinkable` interface defines the base contract for all objects that are compatible with the Threadlink system. It extends the `IIdentifiable` interface and includes methods and events specific to the lifecycle of Threadlink-compatible objects.

{% code title="Namespace" overflow="wrap" fullWidth="false" %}
```csharp
Threadlink.Core
```
{% endcode %}

## Dependencies

* Utilities.Collections
* Utilities.Events

The `ILinkable` interface provides a framework for managing the lifecycle of objects within the Threadlink system. It ensures that all implementing classes have methods for booting, initializing, and discarding objects, as well as an event that triggers before an object is discarded.

## Properties

{% code overflow="wrap" %}
```csharp
public VoidEvent OnBeforeDiscarded { get; }
```
{% endcode %}

The `OnBeforeDiscarded` property is an event that gets raised just before the object is discarded. This allows other components to respond to the imminent destruction of the object, performing any necessary cleanup or final operations.

## Methods

{% code overflow="wrap" %}
```csharp
public void Boot();
```
{% endcode %}

The `Boot` method is intended to perform any initial setup required for the object. This could include allocating resources, setting initial state, or other preparatory steps.

{% code overflow="wrap" %}
```csharp
public void Initialize();
```
{% endcode %}

The `Initialize` method is called to configure the object after it has been booted. This is where the main setup work occurs, making the object ready for use.

The two methods are identical to the Awake and Start Unity messages, with their main difference being that they can be invoked through Threadlink's custom Initialization System, Initium, or manually. When called manually, keep in mind that they work as expected only when called at the correct time during gameplay. This is left at the programmer's discretion.

{% code overflow="wrap" %}
```csharp
public void Discard();
```
{% endcode %}

The `Discard` method is responsible for cleaning up the object before it is destroyed. The `OnBeforeDiscarded` event can be utilized to perform actions right before this cleanup occurs. Note that this method internally calls Unity's `Destroy` method when the `ILinkable` is a `LinkableBehaviour` living in a scene, or an instantiated `LinkableAsset` living in memory. With that in mind, it is considered good practice to `null`-ify all reference-type properties and fields when overriding this method, to ensure Unity will properly mark this object for garbage collection after destroying it.
