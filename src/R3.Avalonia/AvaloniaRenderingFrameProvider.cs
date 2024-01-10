﻿using Avalonia.Controls;
using R3.Collections;

namespace R3.Avalonia;

public class AvaloniaRenderingFrameProvider : FrameProvider, IDisposable
{
    Func<TopLevel> topLevelFactory;
    TopLevel? topLevel;
    bool disposed;
    long frameCount;
    FreeListCore<IFrameRunnerWorkItem> list;
    readonly object gate = new object();

    Action<TimeSpan> messageLoop;

    public AvaloniaRenderingFrameProvider(Func<TopLevel> topLevelFactory)
    {
        this.topLevelFactory = topLevelFactory;
        this.messageLoop = Run;
        this.list = new FreeListCore<IFrameRunnerWorkItem>(gate);
    }

    public override long GetFrameCount()
    {
        ThrowObjectDisposedIf(disposed, typeof(NewThreadSleepFrameProvider));
        return frameCount;
    }

    public override void Register(IFrameRunnerWorkItem callback)
    {
        ThrowObjectDisposedIf(disposed, typeof(NewThreadSleepFrameProvider));
        list.Add(callback, out _);

        (topLevel ??= topLevelFactory()).RequestAnimationFrame(this.messageLoop);
    }

    public void Dispose()
    {
        disposed = true;
        list.Dispose();
    }

    void Run(TimeSpan tick)
    {
        if (disposed) return;

        frameCount++;

        var span = list.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var item = ref span[i];
            if (item != null)
            {
                try
                {
                    if (!item.MoveNext(frameCount))
                    {
                        list.Remove(i);
                    }
                }
                catch (Exception ex)
                {
                    list.Remove(i);
                    try
                    {
                        ObservableSystem.GetUnhandledExceptionHandler().Invoke(ex);
                    }
                    catch { }
                }
            }
        }

        // Schedule next frame right after this one.
        topLevel!.RequestAnimationFrame(this.messageLoop);
    }

    static void ThrowObjectDisposedIf(/*[DoesNotReturnIf(true)]*/ bool condition, Type type)
    {
        if (condition)
        {
            ThrowObjectDisposedException(type);
        }
    }

    // [DoesNotReturn]
    internal static void ThrowObjectDisposedException(Type? type) => throw new ObjectDisposedException(type?.FullName);
}
