namespace Threadlink.Core.Exceptions
{
	using System;

	public sealed class InvalidLinkIDException : Exception
	{
		public InvalidLinkIDException() { }
		public InvalidLinkIDException(string message) : base(message) { }
		public InvalidLinkIDException(string message, Exception inner) : base(message, inner) { }
	}

	public sealed class AddressableLoadingFailedException : Exception
	{
		public AddressableLoadingFailedException() { }
		public AddressableLoadingFailedException(string message) : base(message) { }
		public AddressableLoadingFailedException(string message, Exception inner) : base(message, inner) { }
	}

	public sealed class CorruptConstantsBufferException : Exception
	{
		public CorruptConstantsBufferException() { }
		public CorruptConstantsBufferException(string message) : base(message) { }
		public CorruptConstantsBufferException(string message, Exception inner) : base(message, inner) { }
	}

	public sealed class NullAddressablesExtensionException : Exception
	{
		public NullAddressablesExtensionException() { }
		public NullAddressablesExtensionException(string message) : base(message) { }
		public NullAddressablesExtensionException(string message, Exception inner) : base(message, inner) { }
	}

	public sealed class ExistingSingletonException : Exception
	{
		public ExistingSingletonException() { }
		public ExistingSingletonException(string message) : base(message) { }
		public ExistingSingletonException(string message, Exception inner) : base(message, inner) { }
	}
}