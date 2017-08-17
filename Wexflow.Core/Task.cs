using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;


namespace Wexflow.Core
{
    public abstract class Task
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public bool IsEnabled { get; private set; }
        public Workflow Workflow { get; private set; }
        public List<FileInf> Files
        {
            get
            {
                return Workflow.FilesPerTask[Id];
            }
        }
        public List<Entity> Entities
        {
            get
            {
                return Workflow.EntitiesPerTask[Id];
            }
        }

        readonly XElement _xElement;

        protected Task(XElement xe, Workflow wf)
        {
            _xElement = xe;
            var xId = xe.Attribute("id");
            if (xId == null) throw new Exception("Task id attribute not found.");
            Id = int.Parse(xId.Value);
            var xName = xe.Attribute("name");
            if (xName == null) throw new Exception("Task name attribute not found.");
            Name = xName.Value;
            var xDesc = xe.Attribute("description");
            if (xDesc == null) throw new Exception("Task description attribute not found.");
            Description = xDesc.Value;
            var xEnabled = xe.Attribute("enabled");
            if (xEnabled == null) throw new Exception("Task enabled attribute not found.");
            IsEnabled = bool.Parse(xEnabled.Value);
            Workflow = wf;
            Workflow.FilesPerTask.Add(Id, new List<FileInf>());
            Workflow.EntitiesPerTask.Add(Id, new List<Entity>());

        }

        public abstract TaskStatus Run();

        public string GetSetting(string name)
        {
            var xSetting = _xElement.XPathSelectElement(string.Format("wf:Setting[@name='{0}']", name),
                    Workflow.XmlNamespaceManager).Attribute("value");

            if (xSetting == null)
            {
                throw new Exception("Setting " + name + " value attribute not found.");
            }

            return xSetting.Value;
        }

        public string GetSetting(string name, string defaultValue)
        {
            var xe = _xElement.XPathSelectElement(string.Format("wf:Setting[@name='{0}']", name), Workflow.XmlNamespaceManager);
            if (xe == null) return defaultValue;
            var xSetting = xe.Attribute("value");

            if (xSetting == null)
            {
                throw new Exception("Setting " + name + " value attribute not found.");
            }

            return xSetting.Value;
        }

        public string[] GetSettings(string name)
        {
            return _xElement.XPathSelectElements(string.Format("wf:Setting[@name='{0}']", name), Workflow.XmlNamespaceManager).Select(xe =>
            {
                var xAttribute = xe.Attribute("value");
                if (xAttribute == null)
                {
                    throw new Exception("Setting " + name + " value attribute not found.");
                }

                return xAttribute.Value;
            }).ToArray();
        }

        public int[] GetSettingsInt(string name)
        {
            return GetSettings(name).Select(int.Parse).ToArray();
        }

        public XElement[] GetXSettings(string name)
        {
            return _xElement.XPathSelectElements(string.Format("wf:Setting[@name='{0}']", name), Workflow.XmlNamespaceManager).ToArray();
        }

        public FileInf[] SelectFiles()
        {
            var files = new List<FileInf>();
            foreach (var xSelectFile in GetXSettings("selectFiles"))
            {
                var xTaskId = xSelectFile.Attribute("value");
                if (xTaskId != null)
                {
                    var taskId = int.Parse(xTaskId.Value);

                    var qf = QueryFiles(Workflow.FilesPerTask[taskId], xSelectFile).ToArray();

                    files.AddRange(qf);
                }
                else
                {
                    var qf = (from lf in Workflow.FilesPerTask.Values
                              from f in QueryFiles(lf, xSelectFile)
                              select f).Distinct().ToArray();

                    files.AddRange(qf);
                }
            }
            return files.ToArray();
        }

        IEnumerable<FileInf> QueryFiles(IEnumerable<FileInf> files, XElement xSelectFile)
        {
            var fl = new List<FileInf>();

            if (xSelectFile.Attributes().Count(t => t.Name != "value") == 1)
            {
                return files;
            }

            foreach (var file in files)
            {
                // Check file tags
                bool ok = true;
                foreach (var xa in xSelectFile.Attributes())
                {
                    if (xa.Name != "name" && xa.Name != "value")
                    {
                        ok &= file.Tags.Any(tag => tag.Key == xa.Name && tag.Value == xa.Value);
                    }
                }

                if (ok)
                {
                    fl.Add(file);
                }
            }

            return fl;
        }

        public Entity[] SelectEntities()
        {
            var entities = new List<Entity>();
            foreach (string id in GetSettings("selectEntities"))
            {
                var taskId = int.Parse(id);
                entities.AddRange(Workflow.EntitiesPerTask[taskId]);
            }
            return entities.ToArray();
        }

        string BuildLogMsg(string msg)
        {
            return string.Format("{0} [{1}] {2}", Workflow.LogTag, GetType().Name, msg);
        }

        public void Info(string msg)
        {
            Logger.Info(BuildLogMsg(msg));
        }

        public void InfoFormat(string msg, params object[] args)
        {
            Logger.InfoFormat(BuildLogMsg(msg), args);
        }

        public void Debug(string msg)
        {
            Logger.Debug(BuildLogMsg(msg));
        }

        public void DebugFormat(string msg, params object[] args)
        {
            Logger.DebugFormat(BuildLogMsg(msg), args);
        }

        public void Error(string msg)
        {
            Logger.Error(BuildLogMsg(msg));
        }

        public void ErrorFormat(string msg, params object[] args)
        {
            Logger.ErrorFormat(BuildLogMsg(msg), args);
        }

        public void Error(string msg, Exception e)
        {
            Logger.Error(BuildLogMsg(msg), e);
        }

        public void ErrorFormat(string msg, Exception e, params object[] args)
        {
            Logger.Error(string.Format(BuildLogMsg(msg), args), e);
        }
    }
}
