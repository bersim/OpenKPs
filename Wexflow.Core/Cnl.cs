using System;

namespace Wexflow.Core
{
	public class Cnl
	{
		public int ID { get; private set;}
		public int Num { get; private set;}

		public double Value { get; private set;}
		public int State { get;private set;}

		public Cnl (int id,int num)
		{
			ID = id;
			Num = num;
			Value = 0;
			State = 0;
		}

		public Cnl(int id,int num,double value,int state)
		{
			ID = id;
			Num = num;
			Value = value;
			State = state;
		}

		public Cnl (string cnlNm)
		{
			ID = int.Parse (cnlNm.Split (new char[]{ '_' }) [0].Substring (1));
			Num = int.Parse (cnlNm.Split (new char[]{ '_' }) [1]);
		}

		public Cnl (string cnlNm,double value,int state)
		{
			ID = int.Parse (cnlNm.Split (new char[]{ '_' }) [0].Substring (1));
			Num = int.Parse (cnlNm.Split (new char[]{ '_' }) [1]);
			Value = value;
			State = state;
		}

		public override string ToString ()
		{
			return string.Format ("c{0}_{1}", ID, Num);
		}
	}
}

