namespace Threadlink.Core.Subsystems.Dextra
{
	using Addressables;
	using Core;
	using Cysharp.Threading.Tasks;
	using Initium;
	using Scribe;
	using System;
	using System.Collections.Generic;
	using UnityEngine;

#if UNITY_EDITOR && ODIN_INSPECTOR
	using Sirenix.OdinInspector;
#endif

	[Flags]
	public enum StackingFeatures : byte
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
		public List<T> Buttons { get; }
		public T LastSelectedButton { get; set; }
	}

	public interface IStackingDataPreprocessor<T> { public void Preprocess(T data); }

	[CreateAssetMenu(menuName = "Threadlink/Dextra/UI State Machine")]
	internal sealed class UIStateMachine : LinkableAsset, IBootable, IInitializable, IAddressablesPreloader
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

		internal Action OnInterfaceCancelled = null;

#if UNITY_EDITOR && ODIN_INSPECTOR
		[ShowInInspector, ReadOnly]
#endif
		private Stack<string> StackedInterfaces { get; set; }

		[Space(10)]

		[SerializeField] private GroupedAssetPointer[] uiPrefabPointers = new GroupedAssetPointer[0];

		public override void Discard()
		{
			StackedInterfaces.Clear();
			StackedInterfaces.TrimExcess();

			if (uiPrefabPointers != null)
			{
				int length = uiPrefabPointers.Length;

				for (int i = 0; i < length; i++)
				{
					var pointer = uiPrefabPointers[i];

					Threadlink.ReleasePrefab(pointer.Group, pointer.IndexInDatabase);
				}

				if (IsInstance) uiPrefabPointers = null;
			}

			OnInterfaceCancelled = null;
			StackedInterfaces = null;
			base.Discard();
		}

		public void Boot()
		{
			StackedInterfaces = new(uiPrefabPointers.Length);
		}

		public void Initialize()
		{
			var interfaces = new UserInterface[uiPrefabPointers.Length];
			int length = interfaces.Length;

			for (int i = 0; i < length; i++)
			{
				var pointer = uiPrefabPointers[i];

				if (Threadlink.TryGetAssetReference(pointer.Group, pointer.IndexInDatabase, out var reference))
				{
					var ui = Dextra.Instance.Weave(reference.Asset as UserInterface);

					if (ui is IBootable bootable) Initium.Boot(bootable);
					interfaces[i] = ui;
				}
			}

			uiPrefabPointers = null;

			for (int i = 0; i < length; i++) if (interfaces[i] is IInitializable initializable) Initium.Initialize(initializable);

			if (length > 0) Stack(interfaces[0]);
		}

		public async UniTask PreloadAssetsAsync()
		{
			int length = uiPrefabPointers.Length;
			var tasks = new UniTask[length];

			for (int i = 0; i < length; i++)
			{
				var pointer = uiPrefabPointers[i];
				tasks[i] = Threadlink.LoadPrefabAsync<UserInterface>(pointer.Group, pointer.IndexInDatabase);
			}

			await UniTask.WhenAll(tasks);
		}

		internal bool IsTopInterface(UserInterface userInterface) => userInterface != null && userInterface.Equals(TopInterface);

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
				OnInterfaceCancelled?.Invoke();
			}
		}

		private static void Throw()
		{
			throw new ArgumentException(Scribe.FromSubsystem<Dextra>("The requested User Interface was not found!").ToString());
		}

		private static void Warn()
		{
			Scribe.FromSubsystem<Dextra>("The requested interface to stack is already at the top!").ToUnityConsole(Dextra.Instance, Scribe.WARN);
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

			StackedInterfaces.Push(target.name);
			target.OnStacked();
		}

		internal void Stack<T>(UserInterface target, T stackingData)
		{
			var topUI = TopInterface;

			if (target.Equals(topUI)) Warn();
			else
			{
				if (topUI != null) topUI.OnCovered();

				StackedInterfaces.Push(target.name);

				if (target is IStackingDataPreprocessor<T> preprocessor)
					preprocessor.Preprocess(stackingData);
				else throw new InvalidCastException(Scribe.FromSubsystem<Dextra>("The UI to stack cannot process any data, or the cast is invalid!").ToString());

				target.OnStacked();
			}
		}
	}
}
