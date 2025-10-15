namespace WPFSfChartsBench
{
    public class DataModel
    {
        public double XValue { get; set; }
        public double YValue { get; set; }

        public DataModel(double x, double y) 
        {
            XValue = x;
            YValue = y; 
        }
    }
}
