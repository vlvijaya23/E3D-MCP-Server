using System.Collections.Generic;

namespace E3DBridge.Staad
{
    public class StaadModel
    {
        public List<StaadNode> Nodes =
            new List<StaadNode>();

        public List<StaadMember> Members =
            new List<StaadMember>();
    }
}