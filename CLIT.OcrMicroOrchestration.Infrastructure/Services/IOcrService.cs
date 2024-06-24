using CLIT.OcrMicroOrchestration.Domain.Models;
using CLIT.OcrMicroOrchestration.Infrastructure.Enum;

namespace CLIT.OcrMicroOrchestration.Infrastructure.Services
{
    public interface IOcrService
    {
        Task<ValidityStatusEnum> CheckIdCardValidity(OcrProcess process);
    }
}