using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace TLDTrainer
{
    // Вызовы в игру можно делать только из игрового потока, поэтому команды от окна ставятся в очередь.
    internal static class MainThread
    {
        private static readonly ConcurrentQueue<(Func<object> Work, TaskCompletionSource<object> Done)> Queue = new();

        public static object Run(Func<object> work, int timeoutMs = 8000)
        {
            var done = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            Queue.Enqueue((work, done));
            if (!done.Task.Wait(timeoutMs)) throw new TimeoutException("Игра не отвечает");
            return done.Task.Result;
        }

        public static void Pump()
        {
            while (Queue.TryDequeue(out var item))
            {
                try
                {
                    item.Done.SetResult(item.Work());
                }
                catch (Exception e)
                {
                    item.Done.SetException(e);
                }
            }
        }
    }
}
