using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Drawing;
using Tesseract;
using CLIT.OcrMicroOrchestration.Infrastructure.Interfaces;

namespace CLIT.OcrMicroOrchestration.Infrastructure.Services
{
    public class OrientationService : IOrientationService
    {
        public int FindRotationCorrection(byte[] imageBytes, ImreadModes imreadModes)
        {
            Mat originalImage = new Mat();
            
            CvInvoke.Imdecode(imageBytes, imreadModes, originalImage);
            Mat rotatedImage;

            int bestRotation = 0;
            double bestConfidence = double.MinValue;

            using var engine = new TesseractEngine(@"C:\Program Files\Tesseract-OCR\tessdata", "eng", EngineMode.Default);


            for (int angle = -30; angle < 360; angle += 10)
            {
                // Obracanie obrazka o 5 stopni
                var rotationMatrix = new RotationMatrix2D(new PointF(originalImage.Width / 2, originalImage.Height / 2), angle, 1);
                rotatedImage = new Mat();
                CvInvoke.WarpAffine(originalImage, rotatedImage, rotationMatrix, originalImage.Size);

                using var page = engine.Process(Pix.LoadFromMemory(rotatedImage.ToImage<Bgr, byte>().ToJpegData(100)));
                {
                    var text = page.GetText().Replace(@"\n", "").Replace(@"\\","").Trim();
                    if (!string.IsNullOrEmpty(text) && (text.Contains("EXPIRY DATE") || text.Contains("PERSONAL NUMBER") || text.Contains("ISSUING AUTHORITY")))
                    {
                        var confidence = page.GetMeanConfidence();
                        if (confidence > bestConfidence)
                        {
                            bestConfidence = confidence;
                            bestRotation = angle;
                        }
                    }
                }
            }
            return bestRotation - 360;
        }

    }


}


