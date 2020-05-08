// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

using JSObject = Interop.JavaScript.JSObject;
using JSException = Interop.JavaScript.JSException;
using HostObject = Interop.JavaScript.HostObject;
using Uint8Array = Interop.JavaScript.Uint8Array;
using Function = Interop.JavaScript.Function;

namespace System.Net.Http
{
    // **Note** on `Task.ConfigureAwait(continueOnCapturedContext: true)` for the WebAssembly Browser.
    // The current implementation of WebAssembly for the Browser does not have a SynchronizationContext nor a Scheduler
    // thus forcing the callbacks to run on the main browser thread.  When threading is eventually implemented using
    // emscripten's threading model of remote worker threads, via SharedArrayBuffer, any API calls will have to be
    // remoted back to the main thread.  Most APIs only work on the main browser thread.
    // During discussions the concensus has been that it will not matter right now which value is used for ConfigureAwait
    // we should put this in place now.
    internal partial class BrowserHttpHandler : HttpMessageHandler
    {
        // This partial implementation contains members common to Browser WebAssembly running on .NET Core.
        private static readonly JSObject? s_fetch = (JSObject)Interop.Runtime.GetGlobalObject("fetch");
        private static readonly JSObject? s_window = (JSObject)Interop.Runtime.GetGlobalObject("window");

        /// <summary>
        /// Gets whether the current Browser supports streaming responses
        /// </summary>
        private static bool StreamingSupported { get; }

        static BrowserHttpHandler()
        {
            using (var streamingSupported = new Function("return typeof Response !== 'undefined' && 'body' in Response.prototype && typeof ReadableStream === 'function'"))
                StreamingSupported = (bool)streamingSupported.Call();
        }

        public bool UseCookies
        {
            get => throw new PlatformNotSupportedException("Property UseCookies is not supported.");
            set => throw new PlatformNotSupportedException("Property UseCookies is not supported.");
        }

        public CookieContainer CookieContainer
        {
            get => throw new PlatformNotSupportedException("Property CookieContainer is not supported.");
            set => throw new PlatformNotSupportedException("Property CookieContainer is not supported.");
        }

        public DecompressionMethods AutomaticDecompression
        {
            get => throw new PlatformNotSupportedException("Property AutomaticDecompression is not supported.");
            set => throw new PlatformNotSupportedException("Property AutomaticDecompression is not supported.");
        }

        public bool UseProxy
        {
            get => throw new PlatformNotSupportedException("Property UseProxy is not supported.");
            set => throw new PlatformNotSupportedException("Property UseProxy is not supported.");
        }

        public IWebProxy? Proxy
        {
            get => throw new PlatformNotSupportedException("Property Proxy is not supported.");
            set => throw new PlatformNotSupportedException("Property Proxy is not supported.");
        }

        public ICredentials? DefaultProxyCredentials
        {
            get => throw new PlatformNotSupportedException("Property Credentials is not supported.");
            set => throw new PlatformNotSupportedException("Property Credentials is not supported.");
        }

        public bool PreAuthenticate
        {
            get => throw new PlatformNotSupportedException("Property PreAuthenticate is not supported.");
            set => throw new PlatformNotSupportedException("Property PreAuthenticate is not supported.");
        }

        public ICredentials? Credentials
        {
            get => throw new PlatformNotSupportedException("Property Credentials is not supported.");
            set => throw new PlatformNotSupportedException("Property Credentials is not supported.");
        }

        public bool AllowAutoRedirect
        {
            get => throw new PlatformNotSupportedException("Property AllowAutoRedirect is not supported.");
            set => throw new PlatformNotSupportedException("Property AllowAutoRedirect is not supported.");
        }

        public int MaxAutomaticRedirections
        {
            get => throw new PlatformNotSupportedException("Property MaxAutomaticRedirections is not supported.");
            set => throw new PlatformNotSupportedException("Property MaxAutomaticRedirections is not supported.");
        }

        public int MaxConnectionsPerServer
        {
            get => throw new PlatformNotSupportedException("Property MaxConnectionsPerServer is not supported.");
            set => throw new PlatformNotSupportedException("Property MaxConnectionsPerServer is not supported.");
        }

        public int MaxResponseDrainSize
        {
            get => throw new PlatformNotSupportedException("Property MaxResponseDrainSize is not supported.");
            set => throw new PlatformNotSupportedException("Property MaxResponseDrainSize is not supported.");
        }

        public TimeSpan ResponseDrainTimeout
        {
            get => throw new PlatformNotSupportedException("Property ResponseDrainTimeout is not supported.");
            set => throw new PlatformNotSupportedException("Property ResponseDrainTimeout is not supported.");
        }

        public int MaxResponseHeadersLength
        {
            get => throw new PlatformNotSupportedException("Property MaxResponseHeadersLength is not supported.");
            set => throw new PlatformNotSupportedException("Property MaxResponseHeadersLength is not supported.");
        }

        public SslClientAuthenticationOptions SslOptions
        {
            get => throw new PlatformNotSupportedException("Property SslOptions is not supported.");
            set => throw new PlatformNotSupportedException("Property SslOptions is not supported.");
        }

        public TimeSpan PooledConnectionLifetime
        {
            get => throw new PlatformNotSupportedException("Property PooledConnectionLifetime is not supported.");
            set => throw new PlatformNotSupportedException("Property PooledConnectionLifetime is not supported.");
        }

        public TimeSpan PooledConnectionIdleTimeout
        {
            get => throw new PlatformNotSupportedException("Property PooledConnectionLifetime is not supported.");
            set => throw new PlatformNotSupportedException("Property PooledConnectionLifetime is not supported.");
        }

        public TimeSpan ConnectTimeout
        {
            get => throw new PlatformNotSupportedException("Property ConnectTimeout is not supported.");
            set => throw new PlatformNotSupportedException("Property ConnectTimeout is not supported.");
        }

        public TimeSpan Expect100ContinueTimeout
        {
            get => throw new PlatformNotSupportedException("Property Expect100ContinueTimeout is not supported.");
            set => throw new PlatformNotSupportedException("Property Expect100ContinueTimeout is not supported.");
        }

        public IDictionary<string, object?> Properties => throw new PlatformNotSupportedException("Property Properties is not supported.");
        
        protected internal override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return DoFetch(request, cancellationToken);
        }

        private async Task<HttpResponseMessage> DoFetch(HttpRequestMessage request, CancellationToken cancellationToken)
        {

            var tcs = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Wrap the cancellationToken in a using so that it can be disposed of whether
            // we successfully fetched or failed trying.
            using (cancellationToken.Register(() => tcs.TrySetCanceled()))
            {
                try
                {
                    var requestObject = new JSObject();

                    if (request.Properties.TryGetValue("WebAssemblyFetchOptions", out object? fetchOoptionsValue) &&
                        fetchOoptionsValue is IDictionary<string, object> fetchOptions)
                    {
                        foreach (KeyValuePair<string, object> item in fetchOptions)
                        {
                            requestObject.SetObjectProperty(item.Key, item.Value);
                        }
                    }

                    requestObject.SetObjectProperty("method", request.Method.Method);

                    // We need to check for body content
                    if (request.Content != null)
                    {
                        if (request.Content is StringContent)
                        {
                            requestObject.SetObjectProperty("body", await request.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: true));
                        }
                        else
                        {
                            // 2.1.801 seems to have a problem with the line
                            // using (var uint8Buffer = Uint8Array.From(await request.Content.ReadAsByteArrayAsync ()))
                            // so we split it up into two lines.
                            var byteAsync = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(continueOnCapturedContext: true);
                            using (Uint8Array uint8Buffer = Uint8Array.From(byteAsync))
                            {
                                requestObject.SetObjectProperty("body", uint8Buffer);
                            }
                        }
                    }

                    // Process headers
                    // Cors has it's own restrictions on headers.
                    // https://developer.mozilla.org/en-US/docs/Web/API/Headers
                    using (HostObject jsHeaders = new HostObject("Headers"))
                    {
                        if (request.Headers != null)
                        {
                            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
                            {
                                foreach (string value in header.Value)
                                {
                                    jsHeaders.Invoke("append", header.Key, value);
                                }
                            }
                        }
                        if (request.Content?.Headers != null)
                        {
                            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
                            {
                                foreach (string value in header.Value)
                                {
                                    jsHeaders.Invoke("append", header.Key, value);
                                }
                            }
                        }
                        requestObject.SetObjectProperty("headers", jsHeaders);
                    }

                    WasmHttpReadStream? wasmHttpReadStream = null;

                    JSObject abortController = new HostObject("AbortController");
                    JSObject signal = (JSObject)abortController.GetObjectProperty("signal");
                    requestObject.SetObjectProperty("signal", signal);
                    signal.Dispose();

                    CancellationTokenSource abortCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    CancellationTokenRegistration abortRegistration = abortCts.Token.Register((Action)(() =>
                    {
                        if (abortController.JSHandle != -1)
                        {
                            abortController.Invoke("abort");
                            abortController?.Dispose();
                        }
                        wasmHttpReadStream?.Dispose();
                    }));

                    var args = new Interop.JavaScript.Array();
                    if (request.RequestUri != null)
                    {
                        args.Push(request.RequestUri.ToString());
                        args.Push(requestObject);
                    }

                    requestObject.Dispose();

                    var response = s_fetch?.Invoke("apply", s_window, args) as Task<object>;
                    args.Dispose();
                    if (response == null)
                        throw new Exception("Internal error marshalling the response Promise from `fetch`.");

                    JSObject t = (JSObject)await response.ConfigureAwait(continueOnCapturedContext: true);

                    var status = new WasmFetchResponse(t, abortController, abortCts, abortRegistration);

                    HttpResponseMessage httpresponse = new HttpResponseMessage((HttpStatusCode)Enum.Parse(typeof(HttpStatusCode), status.Status.ToString()));

                    bool streamingEnabled = request.Properties.TryGetValue("WebAssemblyEnableStreamingResponse", out object? streamingEnabledValue) && (bool)(streamingEnabledValue ?? false);

                    httpresponse.Content = StreamingSupported && streamingEnabled
                        ? new StreamContent(wasmHttpReadStream = new WasmHttpReadStream(status))
                        : (HttpContent)new BrowserHttpContent(status);

                    // Fill the response headers
                    // CORS will only allow access to certain headers.
                    // If a request is made for a resource on another origin which returns the CORs headers, then the type is cors.
                    // cors and basic responses are almost identical except that a cors response restricts the headers you can view to
                    // `Cache-Control`, `Content-Language`, `Content-Type`, `Expires`, `Last-Modified`, and `Pragma`.
                    // View more information https://developers.google.com/web/updates/2015/03/introduction-to-fetch#response_types
                    //
                    // Note: Some of the headers may not even be valid header types in .NET thus we use TryAddWithoutValidation
                    using (JSObject respHeaders = status.Headers)
                    {
                        if (respHeaders != null)
                        {
                            using (var entriesIterator = (JSObject)respHeaders.Invoke("entries"))
                            {
                                JSObject? nextResult = null;
                                try
                                {
                                    nextResult = (JSObject)entriesIterator.Invoke("next");
                                    while (!(bool)nextResult.GetObjectProperty("done"))
                                    {
                                        using (var resultValue = (Interop.JavaScript.Array)nextResult.GetObjectProperty("value"))
                                        {
                                            var name = (string)resultValue[0];
                                            var value = (string)resultValue[1];
                                            if (!httpresponse.Headers.TryAddWithoutValidation(name, value))
                                                if (httpresponse.Content != null)
                                                    httpresponse.Content.Headers.TryAddWithoutValidation(name, value);
                                        }
                                        nextResult?.Dispose();
                                        nextResult = (JSObject)entriesIterator.Invoke("next");
                                    }
                                }
                                finally
                                {
                                    nextResult?.Dispose();
                                }
                            }
                        }
                    }

                    tcs.SetResult(httpresponse);
                }
                catch (JSException jsExc)
                {
                    var httpExc = new System.Net.Http.HttpRequestException(jsExc.Message);
                    tcs.SetException(httpExc);
                }
                catch (Exception exception)
                {
                    tcs.SetException(exception);
                }
                return await tcs.Task.ConfigureAwait(continueOnCapturedContext: true);
            }
        }

        private class WasmFetchResponse : IDisposable
        {
            private readonly JSObject _fetchResponse;
            private readonly JSObject _abortController;
            private readonly CancellationTokenSource _abortCts;
            private readonly CancellationTokenRegistration _abortRegistration;

            public WasmFetchResponse(JSObject fetchResponse, JSObject abortController, CancellationTokenSource abortCts, CancellationTokenRegistration abortRegistration)
            {
                _fetchResponse = fetchResponse ?? throw new ArgumentNullException(nameof(fetchResponse));
                _abortController = abortController ?? throw new ArgumentNullException(nameof(abortController));
                _abortCts = abortCts;
                _abortRegistration = abortRegistration;
            }

            public bool IsOK => (bool)_fetchResponse.GetObjectProperty("ok");
            public bool IsRedirected => (bool)_fetchResponse.GetObjectProperty("redirected");
            public int Status => (int)_fetchResponse.GetObjectProperty("status");
            public string StatusText => (string)_fetchResponse.GetObjectProperty("statusText");
            public string ResponseType => (string)_fetchResponse.GetObjectProperty("type");
            public string Url => (string)_fetchResponse.GetObjectProperty("url");
            public bool IsBodyUsed => (bool)_fetchResponse.GetObjectProperty("bodyUsed");
            public JSObject Headers => (JSObject)_fetchResponse.GetObjectProperty("headers");
            public JSObject Body => (JSObject)_fetchResponse.GetObjectProperty("body");

            public Task<object> ArrayBuffer() => (Task<object>)_fetchResponse.Invoke("arrayBuffer");
            public Task<object> Text() => (Task<object>)_fetchResponse.Invoke("text");
            public Task<object> JSON() => (Task<object>)_fetchResponse.Invoke("json");

            public void Dispose()
            {
                // Dispose of unmanaged resources.
                Dispose(true);
            }

            // Protected implementation of Dispose pattern.
            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // Free any other managed objects here.
                    //
                    _abortCts.Cancel();

                    _abortRegistration.Dispose();
                }

                // Free any unmanaged objects here.
                //
                _fetchResponse?.Dispose();
                _abortController?.Dispose();
            }

        }

        private sealed class BrowserHttpContent : HttpContent
        {
            private byte[]? _data;
            private readonly WasmFetchResponse _status;

            public BrowserHttpContent(WasmFetchResponse status)
            {
                _status = status ?? throw new ArgumentNullException(nameof(status));
            }

            private async Task<byte[]> GetResponseData()
            {
                if (_data != null)
                {
                    return _data;
                }

                using (Interop.JavaScript.ArrayBuffer dataBuffer = (Interop.JavaScript.ArrayBuffer)await _status.ArrayBuffer().ConfigureAwait(continueOnCapturedContext: true))
                {
                    using (Uint8Array dataBinView = new Uint8Array(dataBuffer))
                    {
                        _data = dataBinView.ToArray();
                        _status.Dispose();
                    }
                }

                return _data;
            }

            protected override async Task<Stream> CreateContentReadStreamAsync()
            {
                byte[] data = await GetResponseData().ConfigureAwait(continueOnCapturedContext: true);
                return new MemoryStream(data, writable: false);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                byte[] data = await GetResponseData().ConfigureAwait(continueOnCapturedContext: true);
                await stream.WriteAsync(data, 0, data.Length).ConfigureAwait(continueOnCapturedContext: true);
            }

            protected internal override bool TryComputeLength(out long length)
            {
                if (_data != null)
                {
                    length = _data.Length;
                    return true;
                }

                length = 0;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                _status?.Dispose();
                base.Dispose(disposing);
            }
        }

        private sealed class WasmHttpReadStream : Stream
        {
            private WasmFetchResponse? _status;
            private JSObject? _reader;

            private byte[]? _bufferedBytes;
            private int _position;

            public WasmHttpReadStream(WasmFetchResponse status)
            {
                _status = status;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }
                if (offset < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(offset));
                }
                if (count < 0 || buffer.Length - offset < count)
                {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }

                if (_reader == null)
                {
                    // If we've read everything, then _reader and _status will be null
                    if (_status == null)
                    {
                        return 0;
                    }

                    try
                    {
                        using (JSObject body = _status.Body)
                        {
                            _reader = (JSObject)body.Invoke("getReader");
                        }
                    }
                    catch (JSException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        throw;
                    }
                }

                if (_bufferedBytes != null && _position < _bufferedBytes.Length)
                {
                    return ReadBuffered();
                }

                try
                {
                    var t = (Task<object>)_reader.Invoke("read");
                    using (var read = (JSObject)await t.ConfigureAwait(continueOnCapturedContext: true))
                    {
                        if ((bool)read.GetObjectProperty("done"))
                        {
                            _reader.Dispose();
                            _reader = null;

                            _status?.Dispose();
                            _status = null;
                            return 0;
                        }

                        _position = 0;
                        // value for fetch streams is a Uint8Array
                        using (Uint8Array binValue = (Uint8Array)read.GetObjectProperty("value"))
                            _bufferedBytes = binValue.ToArray();
                    }
                }
                catch (JSException)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    throw;
                }

                return ReadBuffered();

                int ReadBuffered()
                {
                    int n = _bufferedBytes.Length - _position;
                    if (n > count)
                        n = count;
                    if (n <= 0)
                        return 0;

                    Buffer.BlockCopy(_bufferedBytes, _position, buffer, offset, n);
                    _position += n;

                    return n;
                }
            }

            protected override void Dispose(bool disposing)
            {
                _reader?.Dispose();
                _status?.Dispose();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new PlatformNotSupportedException("Synchronous reads are not supported, use ReadAsync instead");
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
