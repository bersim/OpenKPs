using System;
using System.Threading;
using System.Diagnostics;

namespace Wexflow.Core
{
    public class WexflowTimer
    {
        public TimerCallback TimerCallback { get; set; }
        public object State { get; set; }
        public TimeSpan Period { get; set; }

        bool _doWork;

        public WexflowTimer(TimerCallback timerCallback, object state, TimeSpan period)
        {
            TimerCallback = timerCallback;
            State = state;
            Period = period;
        }
			

        public void Start()
        {
			_doWork = true;

            var thread = new Thread(() =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

				while (_doWork)
				{
					if (stopwatch.ElapsedMilliseconds >= Period.TotalMilliseconds)
					{
						stopwatch.Reset();
						stopwatch.Start();
						TimerCallback.Invoke(State);
					}
					Thread.Sleep(100);
				}	
                
            });

            thread.Start();
        }

        public void Stop()
        {
			_doWork = false;
			Logger.Info ("Stop timer");
        }
    }
}
