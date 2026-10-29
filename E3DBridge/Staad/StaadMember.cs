namespace E3DBridge.Staad
{
    public class StaadMember
    {
        public int Id;
        public int StartNode;
        public int EndNode;

        public override string ToString()
        {
            return "Member=" + Id +
                   " Start=" + StartNode +
                   " End=" + EndNode;
        }
    }
}