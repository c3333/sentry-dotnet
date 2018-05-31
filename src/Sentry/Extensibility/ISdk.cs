using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Threading.Tasks;
using Sentry.Infrastructure;
using Sentry.Protocol;

namespace Sentry.Extensibility
{
    /// <summary>
    /// The SDK entry API set
    /// </summary>
    /// <remarks>
    /// The contract of which <see cref="SentryCore"/> exposes statically.
    /// This interface exist to allow better testability of integrations which otherwise
    /// would require dependency to the static <see cref="SentryCore"/>
    /// </remarks>
    public interface ISdk
    {
        bool IsEnabled { get; }

        // Scope stuff:
        void ConfigureScope(Action<Scope> configureScope);
        IDisposable PushScope();
        IDisposable PushScope<TState>(TState state);

        void AddBreadcrumb(
            string message,
            string type = null,
            string category = null,
            IDictionary<string, string> data = null,
            BreadcrumbLevel level = default);

        [EditorBrowsable(EditorBrowsableState.Never)]
        void AddBreadcrumb(
            ISystemClock clock,
            string message,
            string type = null,
            string category = null,
            IDictionary<string, string> data = null,
            BreadcrumbLevel level = default);

        // Client or Client/Scope stuff:
        SentryResponse CaptureEvent(SentryEvent evt);
        SentryResponse CaptureEvent(Func<SentryEvent> eventFactory);
        Task<SentryResponse> CaptureEventAsync(Func<Task<SentryEvent>> eventFactory);
        SentryResponse CaptureException(Exception exception);
        Task<SentryResponse> CaptureExceptionAsync(Exception exception);
        SentryResponse WithClientAndScope(Func<ISentryClient, Scope, SentryResponse> handler);
        Task<SentryResponse> WithClientAndScopeAsync(Func<ISentryClient, Scope, Task<SentryResponse>> handler);
    }
}
