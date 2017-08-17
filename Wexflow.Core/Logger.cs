using System;
using log4net;
using log4net.Config;
using System.Reflection;


namespace Wexflow.Core
{
    public static class Logger
    {
        static readonly ILog Ilogger = LogManager.GetLogger(typeof(Logger));

		public static void Configure(bool en)
		{
			if(en)
			{
				Assembly ass = Assembly.GetExecutingAssembly ();
				XmlConfigurator.Configure (ass.GetManifestResourceStream ("Wexflow.Core.log4netConf.xml"));
			}
		}

        public static void Info(string msg)
        {
            Ilogger.Info(msg);
        }

        public static void InfoFormat(string msg, params object[] args)
        {
            Ilogger.InfoFormat(msg, args);
        }

        public static void Debug(string msg)
        {
            Ilogger.Debug(msg);
        }

        public static void DebugFormat(string msg, params object[] args)
        {
            Ilogger.DebugFormat(msg, args);
        }

        public static void Error(string msg)
        {
            Ilogger.Error(msg);
        }

        public static void ErrorFormat(string msg, params object[] args)
        {
            Ilogger.ErrorFormat(msg, args);
        }

        public static void Error(string msg, Exception e)
        {
            Ilogger.Error(msg, e);
        }

        public static void ErrorFormat(string msg, Exception e, params object[] args)
        {
            Ilogger.Error(string.Format(msg, args), e);
        }
    }
}
