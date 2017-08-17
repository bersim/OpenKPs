
namespace Wexflow.Core
{
    public class Tag
    {
        public string Key { get; private set; }
        public string Value { get; private set; }

        public Tag(string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
}
