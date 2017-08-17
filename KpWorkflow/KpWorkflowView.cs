

namespace Scada.Comm.Devices
{
	public class KpWorkflowView : KPView
	{
		public KpWorkflowView () : this(0)
		{
		}

		public KpWorkflowView(int number) : base(number)
		{
		}

		public override string KPDescr {
			get {
				return Localization.UseRussian ?
					"Библиотека КП для Workflow" : 
					"Device library KP for Workflow";
			}
		}
	}
}

