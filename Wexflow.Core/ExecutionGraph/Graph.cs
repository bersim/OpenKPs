using System.Collections.Generic;
using System.Linq;

namespace Wexflow.Core.ExecutionGraph
{
    public class Graph
    {
        public Node[] Nodes { get; private set; }
        public GraphEvent OnSuccess { get; private set; }
        public GraphEvent OnWarning { get; private set; }
        public GraphEvent OnError { get; private set; }

        public Graph(IEnumerable<Node> nodes,
            GraphEvent onSuccess,
            GraphEvent onWarning,
            GraphEvent onError)
        {
            if (nodes != null) Nodes = nodes.ToArray();
            OnSuccess = onSuccess;
            OnWarning = onWarning;
            OnError = onError;
        }
    }
}
