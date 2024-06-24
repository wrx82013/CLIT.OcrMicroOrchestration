namespace CLIT.OcrMicroOrchestration.Domain.Models
{
    public class OcrProcess
    {
        public OcrProcess(Guid corrletaionId, byte[] fileData)
        {
            CorrelationId = corrletaionId;
            FileData = fileData;
        }

        public OcrProcess()
        {
            
        }

        public Guid CorrelationId { get; set; }
        public byte[] FileData { get; set; }
        public int Tries { get; set; }
        public string Errors { get; set; }
    }
}
