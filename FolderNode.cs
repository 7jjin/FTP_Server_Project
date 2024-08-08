using System.Collections.Generic;

namespace FTP_Server_Project
{
    internal class FolderNode
    {
        public string Name { get; set; }
        public List<string> Files { get; set; }
        public List<FolderNode> SubFolders { get; set; }

        public FolderNode()
        {
            Files = new List<string>();
            SubFolders = new List<FolderNode>();
        }
    }
}