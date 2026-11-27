namespace Threadforge.Threadlink.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine.UIElements;
    using Object = UnityEngine.Object;

    /// <summary>A page contributed to the Threadlink workspace window.</summary>
    /// <remarks>
    /// Pages are discovered in the Editor through <see cref="TypeCache"/>; runtime assemblies are never scanned.
    /// A page must have a public parameterless constructor. Pages present existing Unity editors and shared
    /// validation; they never become a runtime dependency.
    /// </remarks>
    public abstract class WorkspacePage
    {
        /// <summary>Navigation title.</summary>
        public abstract string Title { get; }

        /// <summary>Navigation group, such as "Project" or "Modules".</summary>
        public virtual string Group => "Modules";

        /// <summary>Sort order within the group.</summary>
        public virtual int Order => 100;

        /// <summary>Whether the window refreshes this page periodically while in Play mode.</summary>
        public virtual bool RefreshesInPlayMode => false;

        /// <summary>Builds the page content. Called each time the page is selected.</summary>
        public abstract VisualElement CreateContent(WorkspaceContext context);

        /// <summary>Updates live content. Called by the Refresh button and, when enabled, in Play mode.</summary>
        public virtual void Refresh() { }
    }

    /// <summary>Severity of a validation issue.</summary>
    public enum IssueSeverity
    {
        /// <summary>Informational.</summary>
        Info,

        /// <summary>Usable, but likely to cause problems.</summary>
        Warning,

        /// <summary>Invalid; the corresponding runtime operation would reject it.</summary>
        Error,
    }

    /// <summary>One validation result with an optional asset for navigation.</summary>
    public sealed class ValidationIssue
    {
        /// <summary>Validator that reported the issue.</summary>
        public string Source { get; }

        /// <summary>Severity.</summary>
        public IssueSeverity Severity { get; }

        /// <summary>Description.</summary>
        public string Message { get; }

        /// <summary>Asset or object to select, if any.</summary>
        public Object Context { get; }

        /// <summary>Creates an issue.</summary>
        public ValidationIssue(string source, IssueSeverity severity, string message, Object context = null)
        {
            Source = source ?? string.Empty;
            Severity = severity;
            Message = message ?? string.Empty;
            Context = context;
        }

        /// <inheritdoc />
        public override string ToString() => $"[{Severity}] {Source}: {Message}";
    }

    /// <summary>Read-only project validation contributed by a module.</summary>
    /// <remarks>Implementations should call the same domain validation the runtime operation uses, so authoring
    /// and operation guards cannot disagree. Validators must not modify assets.</remarks>
    public interface IWorkspaceValidator
    {
        /// <summary>Display name.</summary>
        string Name { get; }

        /// <summary>Appends issues for the current project state.</summary>
        void Validate(List<ValidationIssue> issues);
    }

    /// <summary>Runs every discovered validator. A failing validator is reported as an error issue.</summary>
    public static class WorkspaceValidation
    {
        /// <summary>Discovers public validator types with public parameterless constructors. Nested private or internal
        /// types, such as test fixtures, are not discovered.</summary>
        public static IReadOnlyList<IWorkspaceValidator> Discover()
        {
            var result = new List<IWorkspaceValidator>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IWorkspaceValidator>())
                if (type.IsVisible && !type.IsAbstract && !type.IsInterface && type.GetConstructor(Type.EmptyTypes) != null)
                    result.Add((IWorkspaceValidator)Activator.CreateInstance(type));
            result.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return result;
        }

        /// <summary>Runs all validators.</summary>
        public static List<ValidationIssue> RunAll() => Run(Discover());

        /// <summary>Runs the supplied validators.</summary>
        public static List<ValidationIssue> Run(IEnumerable<IWorkspaceValidator> validators)
        {
            var issues = new List<ValidationIssue>();
            foreach (var validator in validators)
            {
                int before = issues.Count;
                try
                {
                    validator.Validate(issues);
                }
                catch (Exception error)
                {
                    issues.RemoveRange(before, issues.Count - before);
                    issues.Add(
                        new ValidationIssue(
                            validator.Name,
                            IssueSeverity.Error,
                            "Validator failed: " + error.Message
                        )
                    );
                }
            }
            return issues;
        }
    }

    /// <summary>Navigation to existing Unity editors and other workspace pages.</summary>
    public sealed class WorkspaceContext
    {
        private readonly Action<Type> openPage;
        private readonly Action refresh;

        internal WorkspaceContext(Action<Type> openPage, Action refresh)
        {
            this.openPage = openPage;
            this.refresh = refresh;
        }

        /// <summary>Selects and pings an asset or object in the existing Project/Hierarchy windows.</summary>
        public void Select(Object target)
        {
            if (target == null)
                return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        /// <summary>Opens a Project Settings page, such as "Project/Player".</summary>
        public void OpenProjectSettings(string path) => SettingsService.OpenProjectSettings(path);

        /// <summary>Executes an existing Unity menu item. Returns false when the menu is unavailable.</summary>
        public bool OpenMenu(string menuPath) => EditorApplication.ExecuteMenuItem(menuPath);

        /// <summary>Navigates to another workspace page.</summary>
        public void OpenPage<T>()
            where T : WorkspacePage => openPage(typeof(T));

        /// <summary>Rebuilds the current page.</summary>
        public void Refresh() => refresh();
    }
}
