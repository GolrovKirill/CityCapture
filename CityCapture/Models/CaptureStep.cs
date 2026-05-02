namespace CityCapture.Models
{
    public class CaptureStep
    {
        public int[] Owners { get; } // Owners[i] = номер государства (1..k) или 0
        public CaptureStep(int[] owners)
        {
            Owners = (int[])owners.Clone();
        }
    }
}