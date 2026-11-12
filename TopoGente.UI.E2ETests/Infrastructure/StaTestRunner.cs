using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace TopoGente.UI.E2ETests.Infrastructure;

/// <summary>
/// Executa cenários de UI WPF em thread STA dedicada com DispatcherFrame isolado,
/// garantindo inicialização segura e única de Application.Current e preservando
/// o stack trace original de falhas via ExceptionDispatchInfo.
/// </summary>
internal static class StaTestRunner
{
    private static readonly object _appLock = new();

    public static void Executar(Func<Task> cenarioAsync)
    {
        Exception? capturedException = null;

        var thread = new Thread(() =>
        {
            try
            {
                lock (_appLock)
                {
                    if (Application.Current == null)
                    {
                        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    }
                }

                var frame = new DispatcherFrame();

                Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(async () =>
                    {
                        try
                        {
                            await cenarioAsync();
                        }
                        catch (Exception innerEx)
                        {
                            capturedException = innerEx;
                        }
                        finally
                        {
                            frame.Continue = false;
                        }
                    }));

                Dispatcher.PushFrame(frame);
            }
            catch (Exception ex)
            {
                capturedException = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (capturedException != null)
        {
            ExceptionDispatchInfo.Capture(capturedException).Throw();
        }
    }

    public static void Executar(Action cenarioSync) =>
        Executar(() =>
        {
            cenarioSync();
            return Task.CompletedTask;
        });
}
