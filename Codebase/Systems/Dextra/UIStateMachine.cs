namespace Threadlink.Systems.Dextra
{
	using Core;
	using Cysharp.Threading.Tasks;
	using Initium;
	using Sirenix.OdinInspector;
	using System;
	using System.Collections.Generic;
	using UnityEngine;
	using Utilities.Addressables;
	using Utilities.Events;

	[Flags]
	public enum StackingFeatures
	{
		Default = HideOnCover | IsUninteractableWhenCovered,
		HideOnCover = 1 << 0,
		IsUninteractableWhenCovered = 1 << 1,
		IsInteractableWhenOnTop = 1 << 2
	}

	public interface ICancellableInterface { public void OnCancelled(); }

	public interface IInteractableInterface { }
	public interface IInteractableInterface<T> : IInteractableInterface where T : DextraButton
	{
		public T[] Buttons { get; }
		public T LastSelectedButton { get; set; }
	}

	public interface IScriptableStackingDataProcessor<T> { public void Process(T data); }

	[CreateAssetMenu(menuName = "Threadlink/Dextra/UI State Machine")]
	internal sealed class UIStateMachine : LinkableAsset, IBootable, IInitializable, IAssetPreloader
	{
		internal UserInterface TopInterface
		{
			get
			{
				if (StackedInterfaces.Count <= 0) return null;
				else
				{
					Dextra.Instance.TryGetLinkedEntity(StackedInterfaces.Peek(), out var ui);
					return ui;
				}
			}
		}

		internal int StackedInterfacesCount => StackedInterfaces.Count;

		internal VoidEvent OnInterfaceCancelled => onInterfaceCancelled;

		[ShowInInspector, ReadOnly] private Stack<string> StackedInterfaces { get; set; }

		[Space(10)]

		[SerializeField] private AddressablePrefab<UserInterface>[] userInterfaceReferences = new AddressablePrefab<UserInterface>[0];

		[NonSerialized] private VoidEvent onInterfaceCancelled = new();

		public override Empty Discard(Empty _ = default)
		{
			onInterfaceCancelled.Discard();

			StackedInterfaces.Clear();
			StackedInterfaces.TrimExcess();

			int length = userInterfaceReferences.Length;
			for (int i = 0; i < length; i++) userInterfaceReferences[i].Unload();

			if (IsInstance) userInterfaceReferences = null;

			onInterfaceCancelled = null;
			return base.Discard(_);
		}

		public void Boot()
		{
			StackedInterfaces = new(userInterfaceReferences.Length);
		}

		public void Initialize()
		{
			var interfaces = new UserInterface[userInterfaceReferences.Length];
			int length = interfaces.Length;

			for (int i = 0; i < length; i++)
			{
				var ui = Dextra.Instance.Weave(userInterfaceReferences[i].Result);

				Initium.Boot(ui);
				interfaces[i] = ui;
			}

			userInterfaceReferences = null;

			for (int i = 0; i < length; i++) Initium.Initialize(interfaces[i]);
		}

		public async UniTask PreloadAssetsAsync()
		{
			int length = userInterfaceReferences.Length;
			var tasks = new UniTask[length];

			for (int i = 0; i < length; i++) tasks[i] = userInterfaceReferences[i].LoadAsync();

			await UniTask.WhenAll(tasks);
		}

		internal bool IsTopInterface(UserInterface userInterface)
		{
			return userInterface != null && userInterface.Equals(TopInterface);
		}

		internal void Cancel()
		{
			var topUI = TopInterface;

			if (topUI is ICancellableInterface)
			{
				Dextra.ClearEventSystemSelection();
				StackedInterfaces.Pop();

				topUI.OnPopped();

				var newTopUI = TopInterface;
				if (newTopUI != null) newTopUI.OnResurfaced();

				(topUI as ICancellableInterface).OnCancelled();
				onInterfaceCancelled.Invoke();
			}
		}

		private static void Throw()
		{
			Dextra.Instance.SystemLog<UserInterfaceNotFoundException>();
		}

		private static void Warn()
		{
			Dextra.Instance.SystemLog(Scribe.WarningNotif, "The requested interface to stack is already at the top!");
		}

		internal void Stack(string interfaceID)
		{
			Dextra.Instance.TryGetLinkedEntity(interfaceID, out var target);

			if (target == null) Throw(); else Stack(target);
		}

		internal void Stack<T>(string interfaceID, T stackingData)
		{
			Dextra.Instance.TryGetLinkedEntity(interfaceID, out var target);

			if (target == null) Throw(); else Stack(target, stackingData);
		}

		internal void Stack(UserInterface target)
		{
			var topUI = TopInterface;

			if (target.Equals(topUI))
			{
				Warn();
				return;
			}

			if (topUI != null) topUI.OnCovered();

			StackedInterfaces.Push(target.LinkID);
			target.OnStacked();
		}

		internal void Stack<T>(UserInterface target, T stackingData)
		{
			var topUI = TopInterface;

			if (target.Equals(topUI)) Warn();
			else
			{
				if (topUI != null) topUI.OnCovered();

				StackedInterfaces.Push(target.LinkID);

				if (target is IScriptableStackingDataProcessor<T> processor) processor.Process(stackingData);
				else Dextra.Instance.SystemLog<InvalidUICastException>();

				target.OnStacked();
			}
		}
	}
}
