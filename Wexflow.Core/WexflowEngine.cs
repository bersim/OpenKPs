using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using System.IO;
using System.Threading;

namespace Wexflow.Core
{
    public class WexflowEngine
    {
        public string SettingsFile { get; private set; }
        public string WorkflowsFolder { get; private set; }
        public string TempFolder { get; private set; }
        public string XsdPath { get; private set; }
        public Workflow[] Workflows { get; private set; }

		public RSServers RSSrvs { get; private set;}


        public WexflowEngine(string settingsFile)
        {
			
            SettingsFile = settingsFile;
            LoadSettings();



            LoadWorkflows();


        }

        void LoadSettings()
        {
            var xdoc = XDocument.Load(SettingsFile);
            WorkflowsFolder = GetWexflowSetting(xdoc, "workflowsFolder");
            TempFolder = GetWexflowSetting(xdoc, "tempFolder");
            if (!Directory.Exists(TempFolder)) Directory.CreateDirectory(TempFolder);
            XsdPath = GetWexflowSetting(xdoc, "xsd");

			LoadRSServers (xdoc);


        }

        string GetWexflowSetting(XDocument xdoc, string name)
        {
            var xValue = xdoc.XPathSelectElement(string.Format("/Wexflow/Setting[@name='{0}']", name)).Attribute("value");
            if (xValue == null) throw new Exception("Wexflow Setting Value attribute not found.");
            return xValue.Value;
        }

        void LoadWorkflows()
        {
            var workflows = new List<Workflow>();
            foreach (string file in Directory.GetFiles(WorkflowsFolder))
            {
                try
                {
                    var workflow = new Workflow(file, TempFolder, XsdPath,RSSrvs);
                    workflows.Add(workflow);
                    Logger.InfoFormat("Workflow loaded: {0}", workflow);
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("An error occured while loading the workflow : {0} Please check the workflow configuration. Error: {1}", file, e.Message);
                }
            }
            Workflows = workflows.ToArray();
        }

		void LoadRSServers(XDocument xdoc)
		{
			RSSrvs = new RSServers (xdoc);
		}

        public void Run()
        {
            foreach (Workflow workflow in Workflows)
            {
                if (workflow.IsEnabled)
                {
                    if (workflow.LaunchType == LaunchType.Startup)
                    {
                        workflow.Start();
                    }
                    else if (workflow.LaunchType == LaunchType.Periodic)
                    {
						/*
                        Action<object> callback = o =>
                        {
                            var wf = (Workflow)o;
                            if (!wf.IsRunning) wf.Start();
                        };

                        var timer = new WexflowTimer(new TimerCallback(callback), workflow, workflow.Period);
                        timer.Start();
                        */
                    }
                }
            }
        }

        public Workflow GetWorkflow(int workflowId)
        {
            return Workflows.FirstOrDefault(wf => wf.Id == workflowId);
        }

        public void StartWorkflow(int workflowId)
        {
            var wf = GetWorkflow(workflowId);

            if (wf == null)
            {
                Logger.ErrorFormat("Workflow {0} not found.", workflowId);
            }
            else
            {
                if (wf.IsEnabled) wf.Start();
            }
        }

        public void StopWorkflow(int workflowId)
        {
            var wf = GetWorkflow(workflowId);

            if (wf == null)
            {
                Logger.ErrorFormat("Workflow {0} not found.", workflowId);
            }
            else
            {
                if (wf.IsEnabled) wf.Stop();
            }
        }

        public void PauseWorkflow(int workflowId)
        {
            var wf = GetWorkflow(workflowId);

            if (wf == null)
            {
                Logger.ErrorFormat("Workflow {0} not found.", workflowId);
            }
            else
            {
                if (wf.IsEnabled) wf.Pause();
            }
        }

        public void ResumeWorkflow(int workflowId)
        {
            var wf = GetWorkflow(workflowId);

            if (wf == null)
            {
                Logger.ErrorFormat("Workflow {0} not found.", workflowId);
            }
            else
            {
                if (wf.IsEnabled) wf.Resume();
            }
        }

    }
}
