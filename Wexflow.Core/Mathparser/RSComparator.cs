using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.IO;
using System.Threading;
using Wexflow.Core;
using Scada.Client;
using Scada.Data.Models;
using Scada.Data.Tables;
using System.Data;

using org.mariuszgromada.math.mxparser;

namespace Wexflow.Tasks.RSComparator
{
	public class RSComparator : Task
	{
		public string indata { get; private set;}
		public string outdata { get; private set;}
		public string[] cnlNums { get; private set;}
		public string[] xVars { get; private set;}
		public string condition { get; private set;}
		public string[] outdataTrues { get; private set;}
		public string[] outdataFalses { get; private set;}
		public STLs stls { get; private set;}

		public RSServers srvs { get; private set;}

		public List<Argument> xArgs { get; private set;}
		public List<Cnl> Cnls { get; private set;}
		public List<Argument> CalcArgs {get;private set;}

		public List<Argument> odts { get; private set;}
		public List<Argument> odfs { get; private set;}


		public SrezAdapter sa { get; private set;}




		public RSComparator(XElement xe,Workflow wf) : base(xe,wf)
		{
			indata = GetSetting ("indata");
			cnlNums = GetSettings ("cnlnum");
			xVars = GetSettings ("var");
			condition = GetSetting ("condition");
			outdataTrues = GetSettings ("outdataTrue");
			outdataFalses = GetSettings ("outdataFalses");

			sa = new SrezAdapter ();
			stls = new STLs ();
			xArgs = new List<Argument> ();
			Cnls = new List<Cnl> ();
			CalcArgs = new List<Argument> ();

			odts = new List<Argument> ();
			odfs = new List<Argument> ();


			srvs = wf.RSSrvs;


			LoadCnls ();
			LoadVars ();
			LoadODTs ();

		}

		public void LoadODTs()
		{
			foreach(string odt in outdataTrues)
			{
				Argument lArg = new Argument (odt);
				odts.Add (lArg);
			}
			
		}

		public void LoadODFs()
		{
			foreach(string odf in outdataFalses)
			{
				Argument lArg = new Argument (odf);
				odfs.Add (lArg);
			}
		}
			

		public void LoadCnls()
		{
			foreach(string cnl in cnlNums)
			{
				int srvId = int.Parse(cnl.Split(new char[]{'.'},StringSplitOptions.RemoveEmptyEntries)[0]);
				int cnlNum = int.Parse(cnl.Split(new char[]{'.'},StringSplitOptions.RemoveEmptyEntries)[1]);
				Argument lArg = new Argument("c"+ srvId + "_" + cnlNum.ToString(),0);
				Cnl cCnl = new Cnl (srvId, cnlNum);
				Cnls.Add (cCnl);
			}
		}

		public void LoadVars()
		{
			foreach(string xVar in xVars)
			{
				Argument lArg = new Argument (xVar);
				xArgs.Add (lArg);
			}
		}

		public override TaskStatus Run ()
		{
			Info ("Start Comparator task");

			bool success = false;

			bool IsIndata = false;

			try
			{
				if(indata=="file")
				{
					foreach(FileInf fi in SelectFiles())
					{
						SrezTableLight stl = new SrezTableLight ();
						sa.FileName = fi.Path;
						sa.Fill(stl);
						int id = int.Parse (fi.FileName.Split (new char[]{'.'},StringSplitOptions.RemoveEmptyEntries)[1]);
						stls.AddSrez(id,stl.SrezList.Values[0]);
					}
					IsIndata  = true;
				}

				if (indata == "mem")
				{
					
					foreach(RSServer srv in srvs.GetRSServers())
					{
						stls.AddSrez(srv.Id,srv.GetCurrSrez());
					}
					IsIndata = true;

				}

				if(IsIndata) 
				{
					foreach(Cnl cnl in Cnls)
					{
						Argument cArg = new Argument(cnl.ToString(),stls.GetCnlData(cnl.ID,cnl.Num));
						CalcArgs.Add(cArg);
					}
					CalcArgs.AddRange(xArgs);

					foreach(Argument arg in CalcArgs)
					{
						Info(arg.getArgumentName() + "   " + arg.getArgumentValue().ToString());
					}

					Expression expr = new Expression(condition,CalcArgs.ToArray());

					double pvExpr = expr.calculate();

					if (pvExpr == 0)
					{
						success = false;
					}
					else if (pvExpr == 1)
					{
						success = true;
					}

					if(success)
					{
						foreach(Argument arg in odts)
						{
							arg.addDefinitions(CalcArgs.ToArray());

						}
					}


					InfoFormat("Condition: {0}",condition);
					InfoFormat("Result calculate: {0}",expr.calculate());
					InfoFormat("Success value: {0}",success);
				}
				else
				{
					
					Info("Not set parametr indata");
				}

				CalcArgs.Clear();
				stls.ClearSrez();

			}
			catch(ThreadAbortException)
			{
				throw;
			}
			catch(Exception ex)
			{
				ErrorFormat ("Ann error calc comparator. Error: {0}", ex.Message);
			}
				
			Info ("Task RSComparator finished.");
			return new TaskStatus (Status.Success, success);


		}


	}
}

