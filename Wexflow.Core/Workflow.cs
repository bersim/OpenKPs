using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml.XPath;
using Wexflow.Core.ExecutionGraph;
using Wexflow.Core.ExecutionGraph.Flowchart;


namespace Wexflow.Core
{
    public class Workflow
    {
        public const int StartId = -1;

        public string WorkflowFilePath { get; private set; }
        public string WexflowTempFolder { get; private set; }
        public string WorkflowTempFolder { get; private set; }
        public string XsdPath { get; private set; }
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public LaunchType LaunchType { get; private set; }
        public TimeSpan Period { get; private set; }
        public bool IsEnabled { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
		public bool IsCreateTempFolder { get;private set;}
        public Task[] Taks { get; private set; }
        public Dictionary<int, List<FileInf>> FilesPerTask { get; private set; }
        public Dictionary<int, List<Entity>> EntitiesPerTask { get; private set; }
        public int JobId { get; private set; }
        public string LogTag { get { return string.Format("[{0} / {1}]", Name, JobId); } }
        public XmlNamespaceManager XmlNamespaceManager { get; private set; }
        public Graph ExecutionGraph { get; private set; }

		public RSServers RSSrvs { get;private set;}

        private Thread _thread;

        public Workflow(string path, string wexflowTempFolder, string xsdPath, RSServers rssrvs)
        {
            JobId = 1;
            _thread = null;
            WorkflowFilePath = path;
            WexflowTempFolder = wexflowTempFolder;
            XsdPath = xsdPath;
            FilesPerTask = new Dictionary<int, List<FileInf>>();
            EntitiesPerTask = new Dictionary<int, List<Entity>>();
			RSSrvs = rssrvs;



            Check();
            Load();
        }



        public override string ToString()
        {
            return string.Format("{{id: {0}, name: {1}, enabled: {2}, launchType: {3}}}"
                , Id, Name, IsEnabled, LaunchType);
        }

        void Check()
        {
            var schemas = new XmlSchemaSet();
            schemas.Add("urn:wexflow-schema", XsdPath);

            var doc = XDocument.Load(WorkflowFilePath);
            string msg = string.Empty;
            doc.Validate(schemas, (o, e) =>
            {
                msg += e.Message + Environment.NewLine;
            });

            if (!string.IsNullOrEmpty(msg))
            {
                throw new Exception("The workflow XML document is not valid. Error: " + msg);
            }
        }

        void Load()
        {
            var xmlReader = XmlReader.Create(WorkflowFilePath);
            var xmlNameTable = xmlReader.NameTable;
            if (xmlNameTable != null)
            {
                XmlNamespaceManager = new XmlNamespaceManager(xmlNameTable);
                XmlNamespaceManager.AddNamespace("wf", "urn:wexflow-schema");
            }
            else
            {
                throw new Exception("xmlNameTable of " + WorkflowFilePath + " is null");
            }

            // Loading settings
            var xdoc = XDocument.Load(WorkflowFilePath);
            Id = int.Parse(GetWorkflowAttribute(xdoc, "id"));
            Name = GetWorkflowAttribute(xdoc, "name");
            Description = GetWorkflowAttribute(xdoc, "description");
            LaunchType = (LaunchType)Enum.Parse(typeof(LaunchType), GetWorkflowSetting(xdoc, "launchType"), true);
            if (LaunchType == LaunchType.Periodic) Period = TimeSpan.Parse(GetWorkflowSetting(xdoc, "period"));
            IsEnabled = bool.Parse(GetWorkflowSetting(xdoc, "enabled"));
			IsCreateTempFolder = bool.Parse (GetWorkflowSetting (xdoc, "createTF"));

            // Loading tasks
            var tasks = new List<Task>();
            foreach (var xTask in xdoc.XPathSelectElements("/wf:Workflow/wf:Tasks/wf:Task", XmlNamespaceManager))
            {
                var xAttribute = xTask.Attribute("name");
                if (xAttribute != null)
                {
                    var name = xAttribute.Value;
                    var assemblyName = "Wexflow.Tasks." + name;
                    var typeName = "Wexflow.Tasks." + name + "." + name + ", " + assemblyName;
                    var type = Type.GetType(typeName);

                    if (type != null)
                    {
                        var task = (Task)Activator.CreateInstance(type, xTask, this);
                        tasks.Add(task);
                    }
                    else
                    {
                        throw new Exception("The type of the task " + name + "could not be loaded.");
                    }
                }
                else
                {
                    throw new Exception("Name attribute of the task " + xTask + " does not exist.");
                }
            }
            Taks = tasks.ToArray();

            // Loading execution graph
            var xExectionGraph = xdoc.XPathSelectElement("/wf:Workflow/wf:ExecutionGraph", XmlNamespaceManager);
            if (xExectionGraph != null)
            {
                var taskNodes = GetTaskNodes(xExectionGraph);

                // Check startup node, parallel tasks and infinite loops
                if (taskNodes.Any()) CheckStartupNode(taskNodes, "Startup node with parentId=-1 not found in ExecutionGraph execution graph.");
                CheckParallelTasks(taskNodes, "Parallel tasks execution detected in ExecutionGraph execution graph.");
                CheckInfiniteLoop(taskNodes, "Infinite loop detected in ExecutionGraph execution graph.");

                // OnSuccess
                GraphEvent onSuccess = null;
                var xOnSuccess = xExectionGraph.XPathSelectElement("wf:OnSuccess", XmlNamespaceManager);
                if (xOnSuccess != null)
                {
                    var onSuccessNodes = GetTaskNodes(xOnSuccess);
                    CheckStartupNode(onSuccessNodes, "Startup node with parentId=-1 not found in OnSuccess execution graph.");
                    CheckParallelTasks(onSuccessNodes, "Parallel tasks execution detected in OnSuccess execution graph.");
                    CheckInfiniteLoop(onSuccessNodes, "Infinite loop detected in OnSuccess execution graph.");
                    onSuccess = new GraphEvent(onSuccessNodes);
                }

                // OnWarning
                GraphEvent onWarning = null;
                var xOnWarning = xExectionGraph.XPathSelectElement("wf:OnWarning", XmlNamespaceManager);
                if (xOnWarning != null)
                {
                    var onWarningNodes = GetTaskNodes(xOnWarning);
                    CheckStartupNode(onWarningNodes, "Startup node with parentId=-1 not found in OnWarning execution graph.");
                    CheckParallelTasks(onWarningNodes, "Parallel tasks execution detected in OnWarning execution graph.");
                    CheckInfiniteLoop(onWarningNodes, "Infinite loop detected in OnWarning execution graph.");
                    onWarning = new GraphEvent(onWarningNodes);
                }

                // OnError
                GraphEvent onError = null;
                var xOnError = xExectionGraph.XPathSelectElement("wf:OnError", XmlNamespaceManager);
                if (xOnError != null)
                {
                    var onErrorNodes = GetTaskNodes(xOnError);
                    CheckStartupNode(onErrorNodes, "Startup node with parentId=-1 not found in OnError execution graph.");
                    CheckParallelTasks(onErrorNodes, "Parallel tasks execution detected in OnError execution graph.");
                    CheckInfiniteLoop(onErrorNodes, "Infinite loop detected in OnError execution graph.");
                    onError = new GraphEvent(onErrorNodes);
                }

                ExecutionGraph = new Graph(taskNodes, onSuccess, onWarning, onError);
            }
        }

        Node[] GetTaskNodes(XElement xExectionGraph)
        {
            var nodes = xExectionGraph
                .Elements()
                .Where(xe => xe.Name.LocalName != "OnSuccess" && xe.Name.LocalName != "OnWarning" && xe.Name.LocalName != "OnError")
                .Select(XNodeToNode)
                .ToArray();

            return nodes;
        }

        If XIfToIf(XElement xIf)
        {
            var xId = xIf.Attribute("id");
            if (xId == null) throw new Exception("If id attribute not found.");
            var id = int.Parse(xId.Value);
            var xParent = xIf.Attribute("parent");
            if (xParent == null) throw new Exception("If parent attribute not found.");
            var parentId = int.Parse(xParent.Value);
            var xIfId = xIf.Attribute("if");
            if (xIfId == null) throw new Exception("If attribute not found.");
            var ifId = int.Parse(xIfId.Value);

            // Do nodes
            var doNodes = xIf.XPathSelectElement("wf:Do", XmlNamespaceManager)
                .Elements()
                .Select(XNodeToNode)
                .ToArray();

            CheckStartupNode(doNodes, "Startup node with parentId=-1 not found in DoIf>Do execution graph.");
            CheckParallelTasks(doNodes, "Parallel tasks execution detected in DoIf>Do execution graph.");
            CheckInfiniteLoop(doNodes, "Infinite loop detected in DoIf>Do execution graph.");

            // Otherwise nodes
            Node[] elseNodes = null;
            var xElse = xIf.XPathSelectElement("wf:Else", XmlNamespaceManager);
            if (xElse != null)
            {
                elseNodes = xElse
                    .Elements()
                    .Select(XNodeToNode)
                    .ToArray();

                CheckStartupNode(elseNodes, "Startup node with parentId=-1 not found in DoIf>Otherwise execution graph.");
                CheckParallelTasks(elseNodes, "Parallel tasks execution detected in DoIf>Otherwise execution graph.");
                CheckInfiniteLoop(elseNodes, "Infinite loop detected in DoIf>Otherwise execution graph.");
            }

            return new If(id, parentId, ifId, doNodes, elseNodes);
        }

        While XWhileToWhile(XElement xWhile)
        {
            var xId = xWhile.Attribute("id");
            if (xId == null) throw new Exception("While Id attribute not found.");
            var id = int.Parse(xId.Value);
            var xParent = xWhile.Attribute("parent");
            if (xParent == null) throw new Exception("While parent attribute not found.");
            var parentId = int.Parse(xParent.Value);
            var xWhileId = xWhile.Attribute("while");
            if (xWhileId == null) throw new Exception("While attribute not found.");
            var whileId = int.Parse(xWhileId.Value);

            var doNodes = xWhile
                .Elements()
                .Select(XNodeToNode)
                .ToArray();

            CheckStartupNode(doNodes, "Startup node with parentId=-1 not found in DoWhile>Do execution graph.");
            CheckParallelTasks(doNodes, "Parallel tasks execution detected in DoWhile>Do execution graph.");
            CheckInfiniteLoop(doNodes, "Infinite loop detected in DoWhile>Do execution graph.");

            return new While(id, parentId, whileId, doNodes);
        }

        Switch XSwitchToSwitch(XElement xSwitch)
        {
            var xId = xSwitch.Attribute("id");
            if (xId == null) throw new Exception("Switch Id attribute not found.");
            var id = int.Parse(xId.Value);
            var xParent = xSwitch.Attribute("parent");
            if (xParent == null) throw new Exception("Switch parent attribute not found.");
            var parentId = int.Parse(xParent.Value);
            var xSwitchId = xSwitch.Attribute("switch");
            if (xSwitchId == null) throw new Exception("Switch attribute not found.");
            var switchId = int.Parse(xSwitchId.Value);

            var cases = xSwitch
                .XPathSelectElements("wf:Case", XmlNamespaceManager)
                .Select(xCase =>
                {
                    var xValue = xCase.Attribute("value");
                    if (xValue == null) throw new Exception("Case value attribute not found.");
                    var val = xValue.Value;

                    var nodes = xCase
                        .Elements()
                        .Select(XNodeToNode)
                        .ToArray();

                    var nodeName = string.Format("Switch>Case(value={0})", val);
                    CheckStartupNode(nodes, "Startup node with parentId=-1 not found in " + nodeName + " execution graph.");
                    CheckParallelTasks(nodes, "Parallel tasks execution detected in " + nodeName + " execution graph.");
                    CheckInfiniteLoop(nodes, "Infinite loop detected in " + nodeName + " execution graph.");

                    return new Case(val, nodes);
                });

            var xDefault = xSwitch.XPathSelectElement("wf:Default", XmlNamespaceManager);
            if (xDefault == null) return new Switch(id, parentId, switchId, cases, null);
            var @default = xDefault
                .Elements()
                .Select(XNodeToNode)
                .ToArray();

            CheckStartupNode(@default, "Startup node with parentId=-1 not found in Switch>Default execution graph.");
            CheckParallelTasks(@default, "Parallel tasks execution detected in Switch>Default execution graph.");
            CheckInfiniteLoop(@default, "Infinite loop detected in Switch>Default execution graph.");

            return new Switch(id, parentId, switchId, cases, @default);
        }

        Node XNodeToNode(XElement xNode)
        {
            switch (xNode.Name.LocalName)
            {
                case "Task":
                    var xId = xNode.Attribute("id");
                    if (xId == null) throw new Exception("Task id not found.");
                    var id = int.Parse(xId.Value);

                    var xParentId = xNode.XPathSelectElement("wf:Parent", XmlNamespaceManager)
                        .Attribute("id");

                    if (xParentId == null) throw new Exception("Parent id not found.");
                    var parentId = int.Parse(xParentId.Value);

                    var node = new Node(id, parentId);
                    return node;
                case "If":
                    return XIfToIf(xNode);
                case "While":
                    return XWhileToWhile(xNode);
                case "Switch":
                    return XSwitchToSwitch(xNode);
                default:
                    throw new Exception(xNode.Name.LocalName + " is not supported.");
            }
        }

        void CheckStartupNode(Node[] nodes, string errorMsg)
        {
            if (nodes == null) throw new ArgumentNullException("Nodes name");
            if (nodes.All(n => n.ParentId != StartId))
            {
                throw new Exception(errorMsg);
            }
        }

        void CheckParallelTasks(Node[] taskNodes, string errorMsg)
        {
            bool parallelTasksDetected = false;
            foreach (var taskNode in taskNodes)
            {
                if (taskNodes.Count(n => n.ParentId == taskNode.Id) > 1)
                {
                    parallelTasksDetected = true;
                    break;
                }
            }

            if (parallelTasksDetected)
            {
                throw new Exception(errorMsg);
            }
        }

        void CheckInfiniteLoop(Node[] taskNodes, string errorMsg)
        {
            var startNode = taskNodes.FirstOrDefault(n => n.ParentId == StartId);

            if (startNode != null)
            {
                var infiniteLoopDetected = CheckInfiniteLoop(startNode, taskNodes);

                if (!infiniteLoopDetected)
                {
                    foreach (var taskNode in taskNodes.Where(n => n.Id != startNode.Id))
                    {
                        infiniteLoopDetected |= CheckInfiniteLoop(taskNode, taskNodes);

                        if (infiniteLoopDetected) break;
                    }
                }

                if (infiniteLoopDetected)
                {
                    throw new Exception(errorMsg);
                }
            }
        }

        bool CheckInfiniteLoop(Node startNode, Node[] taskNodes)
        {
            foreach (var taskNode in taskNodes.Where(n => n.ParentId != startNode.ParentId))
            {
                if (taskNode.Id == startNode.Id)
                {
                    return true;
                }
            }

            return false;
        }

        string GetWorkflowAttribute(XDocument xdoc, string attr)
        {
            var xAttribute = xdoc.XPathSelectElement("/wf:Workflow", XmlNamespaceManager).Attribute(attr);
            if (xAttribute != null)
            {
                return xAttribute.Value;
            }

            throw new Exception("Workflow attribute " + attr + "not found.");
        }

        string GetWorkflowSetting(XDocument xdoc, string name)
        {
            var xAttribute = xdoc
                .XPathSelectElement(
                    string.Format("/wf:Workflow[@id='{0}']/wf:Settings/wf:Setting[@name='{1}']", Id, name),
                    XmlNamespaceManager)
                .Attribute("value");
            if (xAttribute != null)
            {
                return xAttribute.Value;
            }

            throw new Exception("Workflow setting " + name + " not found.");
        }

        public void Start()
        {
            if (IsRunning) return;

            var thread = new Thread(() =>
            {
                try
                {
                    IsRunning = true;
                    Logger.InfoFormat("{0} Workflow started.", LogTag);

                    // Create the temp folder
					if(IsCreateTempFolder)
						CreateTempFolder();

                    // Run the tasks
                    if (ExecutionGraph == null)
                    {
                        bool success = true;
                        bool warning = false;
                        bool error = false;
                        RunSequentialTasks(Taks, ref success, ref warning, ref error);
                    }
                    else
                    {
                        var status = RunTasks(ExecutionGraph.Nodes, Taks);

                        switch (status)
                        {
                            case Status.Success:
                                if (ExecutionGraph.OnSuccess != null)
                                {
                                    var successTasks = NodesToTasks(ExecutionGraph.OnSuccess.DoNodes);
                                    RunTasks(ExecutionGraph.OnSuccess.DoNodes, successTasks);
                                }
                                break;
                            case Status.Warning:
                                if (ExecutionGraph.OnWarning != null)
                                {
                                    var warningTasks = NodesToTasks(ExecutionGraph.OnWarning.DoNodes);
                                    RunTasks(ExecutionGraph.OnWarning.DoNodes, warningTasks);
                                }
                                break;
                            case Status.Error:
                                if (ExecutionGraph.OnError != null)
                                {
                                    var errorTasks = NodesToTasks(ExecutionGraph.OnError.DoNodes);
                                    RunTasks(ExecutionGraph.OnError.DoNodes, errorTasks);
                                }
                                break;
                        }
                    }

                }
                catch (ThreadAbortException)
                {
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("An error occured while running the workflow. Error: {0}", e.Message, this);
                }
                finally
                {
                    // Cleanup
                    foreach (List<FileInf> files in FilesPerTask.Values) files.Clear();
                    foreach (List<Entity> entities in EntitiesPerTask.Values) entities.Clear();
                    _thread = null;
                    IsRunning = false;
                    GC.Collect();

                    Logger.InfoFormat("{0} Workflow finished.", LogTag);
                    JobId++;
                }
            });

            _thread = thread;
            thread.Start();
        }

        Task[] NodesToTasks(Node[] nodes)
        {
            var tasks = new List<Task>();

            if (nodes == null) return tasks.ToArray();

            foreach (var node in nodes)
            {
                var @if = node as If;
                if (@if != null)
                {
                    var doTasks = NodesToTasks(@if.DoNodes);
                    var otherwiseTasks = NodesToTasks(@if.ElseNodes);

                    var ifTasks = new List<Task>(doTasks);
                    foreach (var task in otherwiseTasks)
                    {
                        if (ifTasks.All(t => t.Id != task.Id))
                        {
                            ifTasks.Add(task);
                        }
                    }

                    tasks.AddRange(ifTasks);
                }
                else if (node is While)
                {
                    tasks.AddRange(NodesToTasks(((While)node).Nodes));
                }
                else if (node is Switch)
                {
                    var @switch = (Switch)node;
                    tasks.AddRange(NodesToTasks(@switch.Default).Where(task => tasks.All(t => t.Id != task.Id)));
                    tasks.AddRange(NodesToTasks(@switch.Cases.SelectMany(@case => @case.Nodes).ToArray()).Where(task => tasks.All(t => t.Id != task.Id)));
                }
                else
                {
                    var task = GetTask(node.Id);

                    if (task != null)
                    {
                        if (tasks.All(t => t.Id != task.Id))
                        {
                            tasks.Add(task);
                        }
                    }
                    else
                    {
                        throw new Exception("Task " + node.Id + " not found.");
                    }
                }
            }

            return tasks.ToArray();
        }

        Status RunTasks(Node[] nodes, Task[] tasks)
        {
            var success = true;
            var warning = false;
            var atLeastOneSucceed = false;

            if (nodes.Any())
            {
                var startNode = GetStartupNode(nodes);

                var @if = startNode as If;
                if (@if != null)
                {
                    var doIf = @if;
                    RunIf(tasks, nodes, doIf, ref success, ref warning, ref atLeastOneSucceed);
                }
                else if (startNode is While)
                {
                    var doWhile = (While)startNode;
                    RunWhile(tasks, nodes, doWhile, ref success, ref warning, ref atLeastOneSucceed);
                }
                else
                {
                    if (startNode.ParentId == StartId)
                    {
                        RunTasks(tasks, nodes, startNode, ref success, ref warning, ref atLeastOneSucceed);
                    }
                }
            }

            if (success)
            {
                return Status.Success;
            }

            if (atLeastOneSucceed || warning)
            {
                return Status.Warning;
            }

            return Status.Error;
        }

        private static void RunSequentialTasks(IEnumerable<Task> tasks, ref bool success, ref bool warning, ref bool atLeastOneSucceed)
        {
            foreach (var task in tasks)
            {
                if (!task.IsEnabled) continue;
                var status = task.Run();
                success &= status.Status == Status.Success;
                warning |= status.Status == Status.Warning;
                if (!atLeastOneSucceed && status.Status == Status.Success) atLeastOneSucceed = true;
            }
        }

        void RunTasks(Task[] tasks, Node[] nodes, Node node, ref bool success, ref bool warning, ref bool atLeastOneSucceed)
        {
            if (node != null)
            {
                if (node is If || node is While || node is Switch)
                {
                    var if1 = node as If;
                    if (if1 != null)
                    {
                        var @if = if1;
                        RunIf(tasks, nodes, @if, ref success, ref warning, ref atLeastOneSucceed);
                    }
                    else if (node is While)
                    {
                        var @while = (While)node;
                        RunWhile(tasks, nodes, @while, ref success, ref warning, ref atLeastOneSucceed);
                    }
                    else
                    {
                        var @switch = (Switch)node;
                        RunSwitch(tasks, nodes, @switch, ref success, ref warning, ref atLeastOneSucceed);
                    }
                }
                else
                {
                    var task = GetTask(tasks, node.Id);
                    if (task != null)
                    {
                        if (task.IsEnabled)
                        {
                            var status = task.Run();

                            success &= status.Status == Status.Success;
                            warning |= status.Status == Status.Warning;
                            if (!atLeastOneSucceed && status.Status == Status.Success) atLeastOneSucceed = true;

                            var childNode = nodes.FirstOrDefault(n => n.ParentId == node.Id);

                            if (childNode != null)
                            {
                                var if1 = childNode as If;
                                if (if1 != null)
                                {
                                    var @if = if1;
                                    RunIf(tasks, nodes, @if, ref success, ref warning, ref atLeastOneSucceed);
                                }
                                else if (childNode is While)
                                {
                                    var @while = (While)childNode;
                                    RunWhile(tasks, nodes, @while, ref success, ref warning, ref atLeastOneSucceed);
                                }
                                else if (childNode is Switch)
                                {
                                    var @switch = (Switch)childNode;
                                    RunSwitch(tasks, nodes, @switch, ref success, ref warning, ref atLeastOneSucceed);
                                }
                                else
                                {
                                    var childTask = GetTask(tasks, childNode.Id);
                                    if (childTask != null)
                                    {
                                        if (childTask.IsEnabled)
                                        {
                                            var childStatus = childTask.Run();

                                            success &= childStatus.Status == Status.Success;
                                            warning |= childStatus.Status == Status.Warning;
                                            if (!atLeastOneSucceed && status.Status == Status.Success) atLeastOneSucceed = true;

                                            // Recusive call
                                            var ccNode = nodes.FirstOrDefault(n => n.ParentId == childNode.Id);

                                            var node1 = ccNode as If;
                                            if (node1 != null)
                                            {
                                                var @if = node1;
                                                RunIf(tasks, nodes, @if, ref success, ref warning, ref atLeastOneSucceed);
                                            }
                                            else if (ccNode is While)
                                            {
                                                var @while = (While)ccNode;
                                                RunWhile(tasks, nodes, @while, ref success, ref warning, ref atLeastOneSucceed);
                                            }
                                            else if (ccNode is Switch)
                                            {
                                                var @switch = (Switch)ccNode;
                                                RunSwitch(tasks, nodes, @switch, ref success, ref warning, ref atLeastOneSucceed);
                                            }
                                            else
                                            {
                                                RunTasks(tasks, nodes, ccNode, ref success, ref warning, ref atLeastOneSucceed);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        throw new Exception("Task " + childNode.Id + " not found.");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new Exception("Task " + node.Id + " not found.");
                    }
                }
            }
        }

        void RunIf(Task[] tasks, Node[] nodes, If @if, ref bool success, ref bool warning, ref bool atLeastOneSucceed)
        {
            var ifTask = GetTask(@if.IfId);

            if (ifTask != null)
            {
                if (ifTask.IsEnabled)
                {
                    var status = ifTask.Run();

                    success &= status.Status == Status.Success;
                    warning |= status.Status == Status.Warning;
                    if (!atLeastOneSucceed && status.Status == Status.Success) atLeastOneSucceed = true;

                    if (status.Status == Status.Success && status.Condition)
                    {
                        if (@if.DoNodes.Length > 0)
                        {
                            // Build Tasks
                            var doIfTasks = NodesToTasks(@if.DoNodes);

                            // Run Tasks
                            var doIfStartNode = GetStartupNode(@if.DoNodes);

                            if (doIfStartNode.ParentId == StartId)
                            {
                                RunTasks(doIfTasks, @if.DoNodes, doIfStartNode, ref success, ref warning, ref atLeastOneSucceed);
                            }
                        }
                    }
                    else if (status.Condition == false)
                    {
                        if (@if.ElseNodes.Length > 0)
                        {
                            // Build Tasks
                            var elseTasks = NodesToTasks(@if.ElseNodes);

                            // Run Tasks
                            var elseStartNode = GetStartupNode(@if.ElseNodes);

                            RunTasks(elseTasks, @if.ElseNodes, elseStartNode, ref success, ref warning, ref atLeastOneSucceed);
                        }
                    }

                    // Child node
                    var childNode = nodes.FirstOrDefault(n => n.ParentId == @if.Id);

                    if (childNode != null)
                    {
                        RunTasks(tasks, nodes, childNode, ref success, ref warning, ref atLeastOneSucceed);
                    }
                }
            }
            else
            {
                throw new Exception("Task " + @if.Id + " not found.");
            }
        }

        void RunWhile(Task[] tasks, Node[] nodes, While @while, ref bool success, ref bool warning, ref bool atLeastOneSucceed)
        {
            var whileTask = GetTask(@while.WhileId);

            if (whileTask != null)
            {
                if (whileTask.IsEnabled)
                {
                    while (true)
                    {
                        var status = whileTask.Run();

                        success &= status.Status == Status.Success;
                        warning |= status.Status == Status.Warning;
                        if (!atLeastOneSucceed && status.Status == Status.Success) atLeastOneSucceed = true;

                        if (status.Status == Status.Success && status.Condition)
                        {
                            if (@while.Nodes.Length > 0)
                            {
                                // Build Tasks
                                var doWhileTasks = NodesToTasks(@while.Nodes);

                                // Run Tasks
                                var doWhileStartNode = GetStartupNode(@while.Nodes);

                                RunTasks(doWhileTasks, @while.Nodes, doWhileStartNode, ref success, ref warning, ref atLeastOneSucceed);
                            }
                        }
                        else if (status.Condition == false)
                        {
                            break;
                        }
                    }

                    // Child node
                    var childNode = nodes.FirstOrDefault(n => n.ParentId == @while.Id);

                    if (childNode != null)
                    {
                        RunTasks(tasks, nodes, childNode, ref success, ref warning, ref atLeastOneSucceed);
                    }
                }
            }
            else
            {
                throw new Exception("Task " + @while.Id + " not found.");
            }

        }

        void RunSwitch(Task[] tasks, Node[] nodes, Switch @switch, ref bool success, ref bool warning, ref bool atLeastOneSucceed)
        {
            var switchTask = GetTask(@switch.SwitchId);

            if (switchTask != null)
            {
                if (switchTask.IsEnabled)
                {
                    var status = switchTask.Run();

                    success &= status.Status == Status.Success;
                    warning |= status.Status == Status.Warning;
                    if (!atLeastOneSucceed && status.Status == Status.Success) atLeastOneSucceed = true;

                    if (status.Status == Status.Success)
                    {
                        bool aCaseHasBeenExecuted = false;
                        foreach (var @case in @switch.Cases)
                        {
                            if (@case.Value == status.SwitchValue)
                            {
                                if (@case.Nodes.Length > 0)
                                {
                                    // Build Tasks
                                    var switchTasks = NodesToTasks(@case.Nodes);

                                    // Run Tasks
                                    var switchStartNode = GetStartupNode(@case.Nodes);

                                    RunTasks(switchTasks, @case.Nodes, switchStartNode, ref success, ref warning, ref atLeastOneSucceed);
                                }
                                aCaseHasBeenExecuted = true;
                                break;
                            }
                        }

                        if (!aCaseHasBeenExecuted && @switch.Default != null && @switch.Default.Any())
                        {
                            // Build Tasks
                            var defalutTasks = NodesToTasks(@switch.Default);

                            // Run Tasks
                            var defaultStartNode = GetStartupNode(@switch.Default);

                            RunTasks(defalutTasks, @switch.Default, defaultStartNode, ref success, ref warning, ref atLeastOneSucceed);
                        }

                        // Child node
                        var childNode = nodes.FirstOrDefault(n => n.ParentId == @switch.Id);

                        if (childNode != null)
                        {
                            RunTasks(tasks, nodes, childNode, ref success, ref warning, ref atLeastOneSucceed);
                        }
                    }

                }
            }
        }

        public void Stop()
        {
            if (IsRunning)
            {
                try
                {
                    _thread.Abort();
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("An error occured while stopping the workflow : {0}", e, this);
                }
            }
        }

        public void Pause()
        {
            if (IsRunning)
            {
                try
                {
#pragma warning disable 618
                    _thread.Suspend();
#pragma warning restore 618
                    IsPaused = true;
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("An error occured while suspending the workflow : {0}", e, this);
                }
            }
        }

        public void Resume()
        {
            if (IsPaused)
            {
                try
                {
#pragma warning disable 618
                    _thread.Resume();
#pragma warning restore 618
                }
                catch (Exception e)
                {
                    Logger.ErrorFormat("An error occured while resuming the workflow : {0}", e, this);
                }
                finally
                {
                    IsPaused = false;
                }
            }
        }

        void CreateTempFolder()
        {
            // WorkflowId/dd-MM-yyyy/HH-mm-ss-fff
            var wfTempFolder = Path.Combine(WexflowTempFolder, Id.ToString(CultureInfo.CurrentCulture));
            if (!Directory.Exists(wfTempFolder)) Directory.CreateDirectory(wfTempFolder);

            var wfDayTempFolder = Path.Combine(wfTempFolder, string.Format("{0:yyyy-MM-dd}", DateTime.Now));
            if (!Directory.Exists(wfDayTempFolder)) Directory.CreateDirectory(wfDayTempFolder);

            var wfJobTempFolder = Path.Combine(wfDayTempFolder, string.Format("{0:HH-mm-ss-fff}", DateTime.Now));
            if (!Directory.Exists(wfJobTempFolder)) Directory.CreateDirectory(wfJobTempFolder);

            WorkflowTempFolder = wfJobTempFolder;
        }

        Node GetStartupNode(IEnumerable<Node> nodes)
        {
            return nodes.First(n => n.ParentId == StartId);
        }

        Task GetTask(int id)
        {
            return Taks.FirstOrDefault(t => t.Id == id);
        }

        Task GetTask(IEnumerable<Task> tasks, int id)
        {
            return tasks.FirstOrDefault(t => t.Id == id);
        }




    }
}
