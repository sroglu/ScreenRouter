using System;

namespace PFound.ScreenRouter
{
    /// <summary>
    /// Realizes navigation content. The router stays engine-independent; a Unity implementation
    /// (uGUI / UI Toolkit) creates the GameObject/VisualElement, shows/hides and destroys it.
    /// </summary>
    public interface IContentRenderer
    {
        /// <summary>Instantiate the content object for <paramref name="contentType"/> (a <see cref="ContentBase"/>).</summary>
        ContentBase Create(Type contentType);
        void Show(ContentBase content);
        void Hide(ContentBase content);
        void Destroy(ContentBase content);
    }

    /// <summary>Content that receives a typed config right before it opens.</summary>
    public interface IConfigurable<in TConfig>
    {
        void Configure(TConfig config);
    }

    /// <summary>What a navigation guard decides.</summary>
    public enum NavigationGuardOutcome { Allow, Cancel, Redirect }

    /// <summary>A guard's decision: allow the navigation, cancel it, or redirect to another content type.</summary>
    public readonly struct NavigationGuardResult
    {
        public readonly NavigationGuardOutcome Outcome;
        public readonly Type RedirectTo;

        private NavigationGuardResult(NavigationGuardOutcome outcome, Type redirectTo)
        {
            Outcome = outcome;
            RedirectTo = redirectTo;
        }

        public static NavigationGuardResult Allow => new NavigationGuardResult(NavigationGuardOutcome.Allow, null);
        public static NavigationGuardResult Cancel => new NavigationGuardResult(NavigationGuardOutcome.Cancel, null);
        public static NavigationGuardResult Redirect<T>() where T : ContentBase =>
            new NavigationGuardResult(NavigationGuardOutcome.Redirect, typeof(T));
    }
}
