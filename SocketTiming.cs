﻿namespace WebStunnel;

internal sealed class SocketTiming : IDisposable {
    private readonly Config config;
    private readonly CancellationTokenSource cts;

    internal SocketTiming(Config config, CancellationToken token) {
        this.config = config;
        cts = CancellationTokenSource.CreateLinkedTokenSource(token);
    }

    internal void Cancel() {
        cts.Cancel();
    }

    internal TimeSpan LingerDelay=>config.LingerDelay;

    internal CancellationTokenSource ConnectTimeout(CancellationToken token) {
        return Timeout(config.ConnectTimeout, token);
    }

    internal CancellationTokenSource IdleTimeout() {
        return Timeout(config.IdleTimeout);
    }

    internal CancellationTokenSource SendTimeout() {
        return Timeout(config.SendTimeout);
    }

    public void Dispose() {
        cts.Dispose();
    }

    private CancellationTokenSource Timeout(TimeSpan t) {
        var c = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        c.CancelAfter(t);
        return c;
    }

    private CancellationTokenSource Timeout(TimeSpan t, CancellationToken token) {
        var c = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);
        c.CancelAfter(t);
        return c;
    }
}
