using System;
using System.Diagnostics;

namespace Wexflow.Core
{
	public class WeWatch
	{
		public int ID { get; private set;}

		private TimeSpan Period;
		private Stopwatch sw;


		public WeWatch (int id,TimeSpan period)
		{
			ID = id;
			Period = period;
			sw = new Stopwatch ();
			sw.Start ();
		}

		public bool CalcWatch()
		{
			bool st = false;
			if(sw.ElapsedMilliseconds>=Period.TotalMilliseconds)
			{
				sw.Reset ();
				sw.Start ();
				st = true;
			}
			return st;
		}

	}
}

