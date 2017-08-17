
namespace Wexflow.Core.ExecutionGraph
{
    public class Node
    {
        public int Id { get; private set; }
        public int ParentId { get; private set; }

        public Node(int id, int parentId)
        {
            Id = id;
            ParentId = parentId;
        }
    }
}
