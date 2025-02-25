#if ENABLE_LOCALIZATION
namespace Threadlink.Templates.UIUtilities
{
	using UnityEngine;
	using UnityEngine.Localization.Components;

#if UNITY_EDITOR
#if ODIN_INSPECTOR
	using Sirenix.OdinInspector;
	using UnityEngine.Localization.Metadata;
	using UnityEngine.Localization.Settings;
#elif THREADLINK_INSPECTOR
	using Editor.Attributes;
#endif
#endif

	[RequireComponent(typeof(ThreadlinkText), typeof(LocalizeStringEvent))]
	public sealed class LocalizedAutoLayoutText : AutoLayoutText
	{
#if UNITY_EDITOR && (ODIN_INSPECTOR || THREADLINK_INSPECTOR)
		[ReadOnly]
#endif
		[SerializeField] private LocalizeStringEvent localizationEvent = null;

		protected override void Reset()
		{
			base.Reset();

			TryGetComponent(out localizationEvent);
		}

		public override void Discard()
		{
			if (localizationEvent != null)
			{
				localizationEvent.OnUpdateString.RemoveAllListeners();
				localizationEvent.StringReference.Clear();
				localizationEvent = null;
			}

			base.Discard();
		}

		public override void Boot()
		{
			base.Boot();

			void OnLocaleChanged(string newLocalizedText)
			{
				if (targetLabel != null) targetLabel.text = newLocalizedText;
			}

			if (localizationEvent != null && !localizationEvent.StringReference.IsEmpty)
				localizationEvent.StringReference.StringChanged += OnLocaleChanged;
		}

		public override void Refresh()
		{
			if (localizationEvent != null) localizationEvent.RefreshString();
			base.Refresh();
		}

		public bool TryGetComment(out string comment)
		{
			if (localizationEvent != null && !localizationEvent.StringReference.IsEmpty)
			{
				var table = LocalizationSettings.StringDatabase.GetTable(localizationEvent.StringReference.TableReference);

				if (table != null && table.TryGetValue(localizationEvent.StringReference.TableEntryReference.KeyId, out var entry))
				{
					comment = entry.GetMetadata<Comment>()?.CommentText;
					return !string.IsNullOrEmpty(comment);
				}
			}

			comment = null;
			return false;
		}
	}
}
#endif