using OpenCvSharp;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CLIT.OcrMicroOrchestration.Domain.Models;
using CLIT.OcrMicroOrchestration.Infrastructure.Enum;
using CLIT.OcrMicroOrchestration.Infrastructure.Interfaces;
using Tesseract;

namespace CLIT.OcrMicroOrchestration.Infrastructure.Services
{
    public class OcrService : IOcrService
    {
        private readonly IOrientationService _orientationService;

        public OcrService( IOrientationService orientationService)
        {
            _orientationService = orientationService;
        }

        public async Task<ValidityStatusEnum> CheckIdCardValidity(OcrProcess process)
        {
            var side = IDCardSideEnum.Back;
            var tempPath = "temp";
            var defaultExtension = ".jpg";
            var fileName = $"Temp_IDCard_{Guid.NewGuid()}_{DateTime.Now.ToString("s", CultureInfo.InvariantCulture).Replace(":", "_")}{defaultExtension}";
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), tempPath, fileName);
            double procentToUp = 0.0;
            var sb = new StringBuilder();
            EngineMode engineMode = GetEngineMode(process.Tries);

            try
            {
                EnsureDirectoryExists(tempPath);
                await File.WriteAllBytesAsync(filePath, process.FileData);

                procentToUp = CalculateProcentToUp(process.Tries);

                using var engine = new TesseractEngine(@"C:\Program Files\Tesseract-OCR\tessdata", "pol", engineMode);
                Mat image = PreprocessImage(filePath, procentToUp, GetImreadMode(process.Tries));
                Mat orientedImage = CorrectOrientationBasedOnText(image, GetEmguImreadMode(image));

                orientedImage.ImWrite(filePath);

                using var img = Pix.LoadFromMemory(orientedImage.ToBytes(".bmp"));
                using var page = engine.Process(img, PageSegMode.AutoOsd);
                var text = page.GetText().Trim().Replace(" ", "");
                sb.Append(text);

                var dictionaryData = ExtractDetailsFromIdCardBackSide(sb.ToString(), side, out string userDataOut);
                Console.WriteLine(userDataOut);

                return dictionaryData.Count > 0 ? ValidityStatusEnum.Completed_with_Success : ValidityStatusEnum.In_Processing;
            }
            catch (IOException ex)
            {
                LogError(process, ex, sb.ToString());
                return ValidityStatusEnum.Completed_with_Internal_Error;
            }
            catch (Exception ex)
            {
                LogError(process, ex, sb.ToString());
                return ValidityStatusEnum.Completed_with_Fail;
            }

        }

        private void LogError(OcrProcess process, Exception ex, string readText)
        {
            process.Errors += ($"[ Try no.{process.Tries}, DateTime: {DateTimeOffset.Now}: \n Readed Text:{readText} \n");
            process.Errors += ex.Message + " ]";
        }

        private static EngineMode GetEngineMode(int tries)
        {
            return tries switch
            {
                0 => EngineMode.Default,
                1 => EngineMode.TesseractOnly,
                2 => EngineMode.LstmOnly,
                _ => EngineMode.Default,
            };
        }

        private static double CalculateProcentToUp(int tries)
        {
            return tries switch
            {
                3 => 0.1,
                > 3 and <= 10 => tries,
                > 12 => (tries - 2) / 10.0,
                _ => 0.0,
            };
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static ImreadModes GetImreadMode(int tries)
        {
            return tries > 10 ? ImreadModes.Color : ImreadModes.Grayscale;
        }

        private static Emgu.CV.CvEnum.ImreadModes GetEmguImreadMode(Mat image)
        {
            return System.Enum.Parse<Emgu.CV.CvEnum.ImreadModes>(image.Channels() == 1 ? ImreadModes.Grayscale.ToString() : ImreadModes.Color.ToString());
        }

        public Mat CorrectOrientationBasedOnText(Mat sourceImage, Emgu.CV.CvEnum.ImreadModes imreadModes)
        {
            try
            {
                var detectedAngle = _orientationService.FindRotationCorrection(sourceImage.ToBytes(".bmp"), imreadModes);
                if (detectedAngle != 0)
                {
                    Point2f center = new Point2f(sourceImage.Cols / 2, sourceImage.Rows / 2);
                    Mat rotationMatrix = Cv2.GetRotationMatrix2D(center, detectedAngle, 1);
                    Mat rotated = new Mat();
                    Cv2.WarpAffine(sourceImage, rotated, rotationMatrix, sourceImage.Size());
                    return rotated;
                }
                return sourceImage;
            }
            catch (Exception)
            {
                return sourceImage;
            }
        }

        private Dictionary<string, string> ExtractDetailsFromIdCardBackSide(string idData, IDCardSideEnum iDCardSide, out string userData)
        {
            string[] codes = { "I<POL", "IO<POL", "IZ<POL", "IS<POL", "IK<POL", "IE<POL", "IM<POL", "KP<POL", "DR<POL" };
            string[] codesZero = { "I<P0L", "I0<P0L", "IZ<P0L", "IS<P0L", "IK<P0L", "IE<P0L", "IM<P0L", "KP<P0L", "DR<P0L" };
            string foundCode = null;
            int startIndex = -1;

            string[] combinedCodes = codes.Concat(codesZero).ToArray();
            GetStartIndex(idData, combinedCodes, ref foundCode, ref startIndex);

            if (startIndex == -1)
                throw new ArgumentNullException(nameof(startIndex), "Nie znaleziono ¿adnego zdefiniowanego kodu dokumentu w danych wejœciowych. np. I<POL etc.");

            var data = idData.Substring(startIndex);
            var dictionaryData = new Dictionary<string, string>();

            ExtractFirstLineDetails(data, dictionaryData);
            ExtractBirthdayDetails(data, dictionaryData);
            ExtractPeselDetails(data, dictionaryData);
            ExtractNameDetails(data, dictionaryData);
            ExtractExpiryDateDetails(data, dictionaryData);

            userData = data;
            return dictionaryData;
        }

        private static void ExtractFirstLineDetails(string data, Dictionary<string, string> dictionaryData)
        {
            string[] codes = { "I<POL", "IO<POL", "IZ<POL", "IS<POL", "IK<POL", "IE<POL", "IM<POL", "KP<POL", "DR<POL" };
            string[] codesZero = { "I<P0L", "I0<P0L", "IZ<P0L", "IS<P0L", "IK<P0L", "IE<P0L", "IM<P0L", "KP<P0L", "DR<P0L" };
            string pattern = string.Join("|", codes.Concat(codesZero));

            string firstLinePattern = @"(" + pattern + @")(\w{9})(\w)";
            Match firstLineMatch = Regex.Match(data, firstLinePattern);

            if (firstLineMatch.Success)
            {
                dictionaryData.Add("Country", firstLineMatch.Groups[1].Value);
                var idCardNumber = ReplaceCharactersWithZero(firstLineMatch.Groups[2].Value, 3);

                dictionaryData.Add("IdCardNumber", idCardNumber);
                var checkDigit = CalculateCheckDigit(idCardNumber);
                dictionaryData.Add("IdCardNumerCheckDigit", checkDigit.ToString());
                var checkDigitMatched = CalculateCheckDigit(firstLineMatch.Groups[2].Value);
                var checkDigitValid = checkDigitMatched == checkDigit;
                dictionaryData.Add("IDCardCheckSumIsValid", checkDigitValid.ToString());
            }
        }

        private static void ExtractBirthdayDetails(string data, Dictionary<string, string> dictionaryData)
        {
            string birthdayPattern = @"(\d{6})(\d)([MF])";
            Match birthdayMatch = Regex.Match(data, birthdayPattern);

            if (birthdayMatch.Success)
            {
                var birthdayMatched = ReplaceCharactersWithZero(birthdayMatch.Groups[1].Value, 0);
                DateTime dateOfBirth = DateTime.ParseExact(birthdayMatched, "yyMMdd", CultureInfo.InvariantCulture);
                dictionaryData.Add("BirthDate", dateOfBirth.ToString("yyyy-MM-dd"));

                var checkDigit = CalculateCheckDigit(birthdayMatched);
                dictionaryData.Add("BirthDateCheckDigit", checkDigit.ToString());
                var checkDigitMatched = ReplaceCharactersWithZero(birthdayMatch.Groups[2].Value, 0);
                var checkDigitValid = Int32.Parse(checkDigitMatched) == checkDigit;
                dictionaryData.Add("BirthDateChecksumIsValid", checkDigitValid.ToString());
                dictionaryData.Add("Gender", birthdayMatch.Groups[3].Value);
            }
        }

        private static void ExtractPeselDetails(string data, Dictionary<string, string> dictionaryData)
        {
            string peselPattern = @"POL((\d{10})(\d))";
            string peselPatternZero = @"P0L((\d{10})(\d))";
            Match peselMatch = Regex.Match(data, peselPattern) ?? Regex.Match(data, peselPatternZero);

            if (peselMatch.Success)
            {
                var pesel = ReplaceCharactersWithZero(peselMatch.Groups[1].Value, 0);
                dictionaryData.Add("PESEL", pesel);

                var checkDigit = CalculatePeselCheckDigit(pesel);
                dictionaryData.Add("PESELCheckDigit", checkDigit.ToString());
                var checkDigitMatched = Int32.Parse(ReplaceCharactersWithZero(peselMatch.Groups[3].Value, 0));
                var checkDigitValid = checkDigitMatched == checkDigit;
                dictionaryData.Add("PESELChecksumIsValid", checkDigitValid.ToString());
            }
        }

        private static void ExtractNameDetails(string data, Dictionary<string, string> dictionaryData)
        {
            string namePattern = @"([A-Z]+)<<([A-Z]+)";
            Match nameMatch = Regex.Match(data, namePattern);

            if (nameMatch.Success)
            {
                dictionaryData.Add("LastName", nameMatch.Groups[1].Value);
                dictionaryData.Add("FirstName", nameMatch.Groups[2].Value);
            }
        }

        private static void ExtractExpiryDateDetails(string data, Dictionary<string, string> dictionaryData)
        {
            string expiryDatePattern = @"(?<=[MF])(\d{6})(\d)";
            Match expiryDateMatch = Regex.Match(data, expiryDatePattern);

            if (expiryDateMatch.Success)
            {
                var expiryDateMatched = ReplaceCharactersWithZero(expiryDateMatch.Groups[1].Value, 0);
                DateTime expiryDate = DateTime.ParseExact(expiryDateMatched, "yyMMdd", CultureInfo.InvariantCulture);
                dictionaryData.Add("ExpiryDate", expiryDate.ToString("yyyy-MM-dd"));

                var checkDigit = CalculateCheckDigit(expiryDateMatched);
                dictionaryData.Add("ExpiryDateDigit", checkDigit.ToString());
                var expiryDateDigitMatched = Int32.Parse(ReplaceCharactersWithZero(expiryDateMatch.Groups[2].Value, 0));
                var checkDigitValid = expiryDateDigitMatched == checkDigit;
                dictionaryData.Add("ExpiryDateChecksumIsValid", checkDigitValid.ToString());
            }
        }

        private static void GetStartIndex(string idData, string[] codesZero, ref string foundCode, ref int startIndex)
        {
            foreach (var code in codesZero)
            {
                startIndex = idData.IndexOf(code);
                if (startIndex != -1)
                {
                    foundCode = code;
                    break;
                }
            }
        }

        public Mat PreprocessImage(string filePath, double increase, ImreadModes imreadModes)
        {
            Mat sourceImage = Cv2.ImRead(filePath, imreadModes);
            return imreadModes switch
            {
                ImreadModes.Grayscale => ProcessGrayScale(increase, sourceImage),
                ImreadModes.Color => ProcessColorImage(increase, sourceImage),
                _ => throw new NotImplementedException(),
            };
        }

        private static Mat ProcessColorImage(double increase, Mat sourceImage)
        {
            increase = increase < 1.0 ? increase * 10 : increase;

            Mat grayImage = new Mat();
            Cv2.CvtColor(sourceImage, grayImage, ColorConversionCodes.BGR2GRAY);

            Mat binaryImage = new Mat();
            Cv2.Threshold(grayImage, binaryImage, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            double width = Math.Max(1, 1 + Math.Abs(Math.Round(increase)));
            double height = Math.Max(1, 1 + Math.Abs(Math.Round(increase)));

            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size((int)width, (int)height));

            Mat dilatedImage = new Mat();
            Cv2.Dilate(binaryImage, dilatedImage, kernel);

            Mat blurredImage = new Mat();
            Cv2.GaussianBlur(dilatedImage, blurredImage, new Size(5, 5), 0);

            return blurredImage;
        }

        private static Mat ProcessGrayScale(double increase, Mat sourceImage)
        {
            increase = increase < 1.0 ? increase * 10 : increase;

            Mat grayImage = sourceImage.Channels() > 1 ? new Mat() : sourceImage.Clone();
            if (sourceImage.Channels() > 1)
            {
                Cv2.CvtColor(sourceImage, grayImage, ColorConversionCodes.BGR2GRAY);
            }

            Mat binaryImage = new Mat();
            Cv2.Threshold(grayImage, binaryImage, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            double width = Math.Max(1, 1 + Math.Abs(Math.Round(increase)));
            double height = Math.Max(1, 1 + Math.Abs(Math.Round(increase)));

            Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size((int)width, (int)height));

            Mat dilatedImage = new Mat();
            Cv2.Dilate(binaryImage, dilatedImage, kernel);

            Mat blurredImage = new Mat();
            Cv2.GaussianBlur(dilatedImage, blurredImage, new Size(5, 5), 0);

            return blurredImage;
        }

        public static int CalculateCheckDigit(string data)
        {
            var weights = new int[] { 7, 3, 1, 7, 3, 1, 7, 3, 1, 7, 3, 1, 7, 3, 1, 7, 3, 1, 7, 3, 1, 7, 3, 1 };
            int sum = 0;

            for (int i = 0; i < data.Length; i++)
            {
                int value = data[i] switch
                {
                    >= '0' and <= '9' => data[i] - '0',
                    >= 'A' and <= 'Z' => data[i] - 'A' + 10,
                    _ => throw new ArgumentException("Invalid character in data"),
                };

                sum += value * weights[i % weights.Length];
            }

            return sum % 10;
        }

        public static int CalculatePeselCheckDigit(string pesel)
        {
            var weights = new int[] { 1, 3, 7, 9, 1, 3, 7, 9, 1, 3 };
            int sum = 0;

            for (int i = 0; i < pesel.Length - 1; i++)
            {
                int value = pesel[i] - '0';
                sum += value * weights[i];
            }

            int lastDigitOfSum = sum % 10;
            int controlDigit = lastDigitOfSum == 0 ? 0 : 10 - lastDigitOfSum;

            return controlDigit;
        }

        public static string ReplaceCharactersWithZero(string input, int firstCharsToOmit)
        {
            var stringToReplace = input.Substring(firstCharsToOmit);
            return input.Remove(firstCharsToOmit) + stringToReplace.Replace('ó', '6').Replace('O', '0').Replace('o', '0');
        }
    }
}
