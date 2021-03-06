using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Sentry.Internal.Http;
using Sentry.Protocol;
using Sentry.Testing;
using Sentry.Tests.Helpers;
using Xunit;

namespace Sentry.Tests.Internals.Http
{
    public class HttpTransportTests
    {
        [Fact]
        public async Task SendEnvelopeAsync_CancellationToken_PassedToClient()
        {
            // Arrange
            using var source = new CancellationTokenSource();
            source.Cancel();
            var token = source.Token;

            var httpHandler = Substitute.For<MockableHttpMessageHandler>();

            httpHandler.VerifyableSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
                .Returns(_ => SentryResponses.GetOkResponse());

            var httpTransport = new HttpTransport(
                new SentryOptions {Dsn = DsnSamples.ValidDsnWithSecret},
                new HttpClient(httpHandler),
                _ => { }
            );

            var envelope = Envelope.FromEvent(
                new SentryEvent(id: SentryResponses.ResponseId)
            );

            // Act
            await httpTransport.SendEnvelopeAsync(envelope, token);

            // Assert
            await httpHandler
                .Received(1)
                .VerifyableSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Is<CancellationToken>(c => c.IsCancellationRequested));
        }

        [Fact]
        public async Task SendEnvelopeAsync_ResponseNotOkWithMessage_LogsError()
        {
            // Arrange
            const HttpStatusCode expectedCode = HttpStatusCode.BadGateway;
            const string expectedMessage = "Bad Gateway!";

            var httpHandler = Substitute.For<MockableHttpMessageHandler>();

            httpHandler.VerifyableSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
                .Returns(_ => SentryResponses.GetErrorResponse(expectedCode, expectedMessage));

            var logger = new AccumulativeDiagnosticLogger();

            var httpTransport = new HttpTransport(
                new SentryOptions
                {
                    Dsn = DsnSamples.ValidDsnWithSecret,
                    Debug = true,
                    DiagnosticLogger = logger
                },
                new HttpClient(httpHandler),
                _ => { }
            );

            var envelope = Envelope.FromEvent(new SentryEvent());

            // Act
            await httpTransport.SendEnvelopeAsync(envelope);

            // Assert
            logger.Entries.Any(e =>
                e.Level == SentryLevel.Error &&
                e.Message == "Sentry rejected the envelope {0}. Status code: {1}. Sentry response: {2}" &&
                e.Exception == null &&
                e.Args[0].ToString() == envelope.TryGetEventId().ToString() &&
                e.Args[1].ToString() == expectedCode.ToString() &&
                e.Args[2].ToString() == expectedMessage
            ).Should().BeTrue();
        }

        [Fact]
        public async Task SendEnvelopeAsync_ResponseNotOkNoMessage_LogsError()
        {
            // Arrange
            const HttpStatusCode expectedCode = HttpStatusCode.BadGateway;

            var httpHandler = Substitute.For<MockableHttpMessageHandler>();

            httpHandler.VerifyableSendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<CancellationToken>())
                .Returns(_ => SentryResponses.GetErrorResponse(expectedCode, null));

            var logger = new AccumulativeDiagnosticLogger();

            var httpTransport = new HttpTransport(
                new SentryOptions
                {
                    Dsn = DsnSamples.ValidDsnWithSecret,
                    Debug = true,
                    DiagnosticLogger = logger
                },
                new HttpClient(httpHandler),
                _ => { }
            );

            var envelope = Envelope.FromEvent(new SentryEvent());

            // Act
            await httpTransport.SendEnvelopeAsync(envelope);

            // Assert
            logger.Entries.Any(e =>
                e.Level == SentryLevel.Error &&
                e.Message == "Sentry rejected the envelope {0}. Status code: {1}. Sentry response: {2}" &&
                e.Exception == null &&
                e.Args[0].ToString() == envelope.TryGetEventId().ToString() &&
                e.Args[1].ToString() == expectedCode.ToString() &&
                e.Args[2].ToString() == HttpTransport.NoMessageFallback
            ).Should().BeTrue();
        }

        [Fact]
        public void CreateRequest_AuthHeader_Invoked()
        {
            // Arrange
            var callbackInvoked = false;

            var httpTransport = new HttpTransport(
                new SentryOptions {Dsn = DsnSamples.ValidDsnWithSecret},
                new HttpClient(),
                _ => callbackInvoked = true
            );

            var envelope = Envelope.FromEvent(new SentryEvent());

            // Act
            httpTransport.CreateRequest(envelope);

            // Assert
            callbackInvoked.Should().BeTrue();
        }

        [Fact]
        public void CreateRequest_RequestMethod_Post()
        {
            // Arrange
            var httpTransport = new HttpTransport(
                new SentryOptions {Dsn = DsnSamples.ValidDsnWithSecret},
                new HttpClient(),
                _ => { }
            );

            var envelope = Envelope.FromEvent(new SentryEvent());

            // Act
            var request = httpTransport.CreateRequest(envelope);

            // Assert
            request.Method.Should().Be(HttpMethod.Post);
        }

        [Fact]
        public void CreateRequest_SentryUrl_FromOptions()
        {
            // Arrange
            var httpTransport = new HttpTransport(
                new SentryOptions {Dsn = DsnSamples.ValidDsnWithSecret},
                new HttpClient(),
                _ => { }
            );

            var envelope = Envelope.FromEvent(new SentryEvent());

            var uri = Dsn.Parse(DsnSamples.ValidDsnWithSecret).GetEnvelopeEndpointUri();

            // Act
            var request = httpTransport.CreateRequest(envelope);

            // Assert
            request.RequestUri.Should().Be(uri);
        }

        [Fact]
        public async Task CreateRequest_Content_IncludesEvent()
        {
            // Arrange
            var httpTransport = new HttpTransport(
                new SentryOptions {Dsn = DsnSamples.ValidDsnWithSecret},
                new HttpClient(),
                _ => { }
            );

            var envelope = Envelope.FromEvent(new SentryEvent());

            // Act
            var request = httpTransport.CreateRequest(envelope);
            var requestContent = await request.Content.ReadAsStringAsync();

            // Assert
            requestContent.Should().Contain(envelope.TryGetEventId().ToString());
        }
    }
}
