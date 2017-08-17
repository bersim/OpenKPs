using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Wexflow.Core
{
    public class FileInf
    {
        string _path;
        string _renameTo;

        public string Path
        {
            get
            {
                return _path;
            }
            set
            {
                _path = value;
                FileName = System.IO.Path.GetFileName(value);
            }
        }
        public string FileName { get; private set; }
        public int TaskId { get; private set; }
        public string RenameTo
        {
            get
            {
                return _renameTo;
            }
            set
            {
                _renameTo = value;

                if (!string.IsNullOrEmpty(value))
                    RenameToOrName = value;
            }
        }
        public string RenameToOrName { get; private set; }
        public List<Tag> Tags { get; private set; }

        public FileInf(string path, int taskId)
        {
            Path = path;
            TaskId = taskId;
            RenameToOrName = FileName;
            Tags = new List<Tag>();
        }

        public override string ToString()
        {
            return ToXElement().ToString();
        }

        public XElement ToXElement()
        {
            return new XElement("File",
                new XAttribute("taskId", TaskId),
                new XAttribute("path", Path),
                new XAttribute("name", FileName),
                new XAttribute("renameTo", RenameTo ?? string.Empty),
                new XAttribute("renameToOrName", RenameToOrName),
                from tag in Tags
                select new XAttribute(tag.Key, tag.Value));
        }
    }
}