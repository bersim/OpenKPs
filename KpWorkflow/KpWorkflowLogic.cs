using Scada.Comm.Channels;
using Scada.Data.Models;
using Scada.Data.Tables;
using System;
using System.Collections.Generic;
using System.Threading;
using Wexflow.Core;





namespace Scada.Comm.Devices
{
	public sealed class KpWorkflowLogic : KPLogic
	{
		private WexflowEngine we;
		private List<WeWatch> wfperiods;

		public KpWorkflowLogic (int number) : base(number)
		{
			ConnRequired = false;
			CanSendCmd = true;

			WorkState = WorkStates.Normal;
		}

		public override void Session ()
		{
			base.Session ();

			foreach(WeWatch ww in wfperiods)
			{
				if( ww.CalcWatch())
				{
					Workflow wf = we.GetWorkflow (ww.ID);
					if(!wf.IsRunning)
					{
						wf.Start ();
					}
				}

			}
			Thread.Sleep (100);

			//Workflow wf = we.GetWorkflow (41);
			//WriteToLog(we.Workflows.Length.ToString());
		}

		public override void OnAddedToCommLine ()
		{
			base.OnAddedToCommLine ();

			bool IsWflogger = bool.Parse(CustomParams["KpWorkflowLogger"]);

			Logger.Configure(IsWflogger);
			string fnm = ReqParams.CmdLine.Trim ();
			we = new WexflowEngine (AppDirs.ConfigDir + fnm);
			WriteToLog ("Загружена конфигурация: " + AppDirs.ConfigDir + fnm);
			we.Run ();

			wfperiods = new List<WeWatch> ();

			foreach(Workflow wf in we.Workflows)
			{
				if(wf.LaunchType==LaunchType.Periodic)
				{
					WeWatch ww = new WeWatch (wf.Id, wf.Period);
					wfperiods.Add(ww);
				}
			}

			WriteToLog ("KpWorkflow в работе.");

		}
			

		public override void SendCmd (Command cmd)
		{
			base.SendCmd (cmd);

			if (cmd.CmdTypeID == BaseValues.CmdTypes.Standard)
			{
				switch(cmd.CmdNum)
				{
				case 1:
					we.StartWorkflow (Convert.ToInt32(cmd.CmdVal));
					break;
				case 2:
					we.StopWorkflow (Convert.ToInt32(cmd.CmdVal));
					break;
				case 3:
					we.PauseWorkflow (Convert.ToInt32(cmd.CmdVal));
					break;
				}
			}
				

				
		}
	}
}

