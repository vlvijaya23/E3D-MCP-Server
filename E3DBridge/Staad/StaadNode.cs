namespace E3DBridge.Staad
{
    public class StaadNode
    {
        public int Id;
        public double X;
        public double Y;
        public double Z;

        public override string ToString()
        {
            return "Node=" + Id +
                   " X=" + X +
                   " Y=" + Y +
                   " Z=" + Z;
        }
    }
}