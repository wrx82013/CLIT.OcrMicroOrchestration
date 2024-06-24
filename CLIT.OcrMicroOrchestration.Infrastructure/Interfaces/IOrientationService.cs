using Emgu.CV.CvEnum;

namespace CLIT.OcrMicroOrchestration.Infrastructure.Interfaces
{
    public interface IOrientationService
    {
        int FindRotationCorrection(byte[] imageBytes, ImreadModes imreadModes);
    }
}