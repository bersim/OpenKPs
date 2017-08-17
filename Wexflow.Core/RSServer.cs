using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using Scada.Client;
using Scada.Data.Models;
using Scada.Data.Tables;

namespace Wexflow.Core
{
	public class RSServer : ServerComm
	{
		public bool cn { get; private set;}
		public int Id { get;private set;}

		private RSServer rssrv;



		//public RSServer(){}

		public RSServer (CommSettings cs, int id)
		{
			rssrv = this;
			rssrv.Id = id;
			rssrv.commSettings = cs;
		}
		public void RSConnect ()
		{
			if(!cn)
				cn =  rssrv.Connect ();
		}
		public void RSClose()
		{
			rssrv.Close ();
			cn = false;
		}

		public SrezTableLight.Srez GetCurrSrez()
		{
			lock(rssrv)
			{
				SrezTableLight stl = new SrezTableLight ();
				bool rec;
				rec = rssrv.ReceiveSrezTable ("current.dat", stl);
				return stl.SrezList.Values [0];
			}
		}

		public bool SetCurrSrez(SrezTableLight.Srez srez)
		{
			bool rec;
			lock(rssrv)
			{
				rec = rssrv.SendSrez (srez,out rec);
			}
			return rec;
		}


	}

	public class RSServers
	{

		private List<RSServer> rssrvs;


		public RSServers(XDocument xdoc)
		{
			rssrvs = new List<RSServer> ();
			IEnumerable<XElement> cnfs = xdoc.XPathSelectElements ("//RSServer");
			foreach(XElement cnf in cnfs)
			{
				CommSettings cs = new CommSettings () 
				{
					ServerHost = cnf.Attribute("ServerHost").Value,
					ServerPort = int.Parse(cnf.Attribute("ServerPort").Value),
					ServerTimeout = 200,
					ServerUser = cnf.Attribute("ServerUser").Value,
					ServerPwd = cnf.Attribute("ServerPwd").Value
				};
				RSServer rssrv = new RSServer (cs,int.Parse(cnf.Attribute("Id").Value));
				rssrvs.Add (rssrv);
			}


		}





		public RSServer[] GetRSServers()
		{
			return rssrvs.ToArray();
		}


		public RSServer GetRSSrvId(int id)
		{
			RSServer RSSrv = null;
			foreach(RSServer srv in rssrvs)
			{
				RSSrv = srv;
				if(srv.Id == id)
				{
					srv.RSConnect ();
					RSSrv = srv;
					break;
				}
			}
			return RSSrv;
		}

	

		public void SendCurrSrez(List<Cnl> cnls)
		{
			bool res = false;
			DateTime dt = DateTime.Now;
			foreach(Cnl cnl in cnls)
			{
				RSServer RSSrv = GetRSSrvId (cnl.ID);

				SrezTableLight stl = new SrezTableLight ();
				SrezTableLight.Srez curSrezSrc = new SrezTableLight.Srez (dt, 1);
				curSrezSrc.CnlNums [0] = cnl.Num;
				curSrezSrc.CnlData [0] = new SrezTableLight.CnlData (){ Val = cnl.Value, Stat = cnl.State };

				res = RSSrv.SendSrez (curSrezSrc,out res);
			}



		}





	}


}

