using System;

namespace Scada.Comm.Devices
{
	public class KpMQTTView : KPView
	{
		public KpMQTTView () : this(0)
		{
		}

		public KpMQTTView(int number)
			: base(number)
		{
		}

		public override string KPDescr
		{
			get
			{
				return Localization.UseRussian ? 
					"Библиотека КП для тестирования." : 
					"Device library for testing.";
			}
		}

	}
}

