namespace Threadlink.Core
{
	using UnityEngine;

	public abstract class ThreadlinkException : UnityException { }

	public sealed class InvalidDeploymentException : ThreadlinkException
	{
		public override string Message => "The Threadlink Framework must by deployed only through the dedicated 'Threadlink Scene'!";
	}

	public sealed class ExistingSingletonException : ThreadlinkException
	{
		public override string Message => "A Singleton for this type already exists!";
	}

	public sealed class ExistingIDException : ThreadlinkException
	{
		public override string Message => "This ID already exists in the Registry!";
	}

	public sealed class InvalidIDException : ThreadlinkException
	{
		public override string Message => "Failed to find an entity corresponding to the provided ID!";
	}

	public sealed class ConstantIDsBufferException : ThreadlinkException
	{
		public override string Message => "An Singleton Entry was detected but it is not pointing to a valid Singleton Instance ID";
	}

	public sealed class SearchedNullAddressablesExtensionException : ThreadlinkException
	{
		public override string Message => "A request to search the Addressables Extension was made, however no extension has been provided! Please provide an extension before proceeding!";
	}

	public sealed class AddressablesExtensionNotFoundException : ThreadlinkException
	{
		public override string Message => "No Custom Addressables Extension specified! Please provide an extension before proceeding!";
	}

	public sealed class InvalidLinkableAssetNameException : ThreadlinkException
	{
		public override string Message => "Names of Linkable Assets can neither be NULL nor empty!";
	}

	public sealed class InvalidTimescaleException : ThreadlinkException
	{
		public override string Message => "Invalid Timescale requested! Valid values are 0 and 1. Check your Timescale assignments!";
	}
}

namespace Threadlink.Systems.Dextra
{
	using Core;

	public sealed class UserInterfaceNotFoundException : ThreadlinkException
	{
		public override string Message => "Could not find the requested linked interface! This should never happen!";
	}

	public sealed class InvalidUICastException : ThreadlinkException
	{
		public override string Message => "This User Interface is not a stacking data processor!";
	}
}

namespace Threadlink.StateMachines
{
	using Core;

	public sealed class ProcessorNotFoundException : ThreadlinkException
	{
		public override string Message => "The requested processor could not be found! Check your request!";
	}

	public sealed class ParameterNotFoundException : ThreadlinkException
	{
		public override string Message => "The requested parameter could not be found! Check your request!";
	}

	public sealed class InvalidScriptableStateCastException : ThreadlinkException
	{
		public override string Message => "This state is not Scriptable and cannot process any data!";
	}
}

namespace Threadlink.Utilities.Events
{
	using Core;

	public sealed class NullScriptableEventException : ThreadlinkException
	{
		public override string Message => "The Scriptable Event Asset is NULL!";
	}
}

namespace Threadlink.Utilities.Addressables
{
	using Threadlink.Core;

	public sealed class AddressableLoadingFailedException : ThreadlinkException
	{
		public override string Message => "Failed to load addressable asset!";
	}
}
