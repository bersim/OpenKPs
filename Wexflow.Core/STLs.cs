using System;
using System.Collections.Generic;

using Scada.Data.Tables;
using Scada.Data.Models;

namespace Wexflow.Core
{
	public class STLs
	{
		private SortedList<int,SrezTableLight.Srez> stls { get; set;}

		public STLs ()
		{
			stls = new SortedList<int, SrezTableLight.Srez> ();
		}

		public void AddSrez(int id,SrezTableLight.Srez srez)
		{
			stls.Add (id, srez);
		}

		public void ClearSrez()
		{
			stls.Clear ();	
		}

		public int GetCnt()
		{
			return stls.Count;
		}

		public SrezTableLight.Srez GetSrez(int id)
		{
			return stls.Values [id];
		}

		public Double GetCnlData(int id,int numCnl)
		{
			SrezTableLight.CnlData pv = stls.Values [id - 1].GetCnlData (numCnl);
			return pv.Val;
		}


	}
}

