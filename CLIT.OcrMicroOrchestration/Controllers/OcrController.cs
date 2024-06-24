using CLIT.OcrMicroOrchestration.Infrastructure.Handlers;
using Microsoft.AspNetCore.Mvc;

namespace CLIT.OcrMicroService
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class OcrController : ControllerBase
    {
        public static ReceiverHandler _receiverHandler;
        public OcrController(IServiceProvider serviceProvider)
        {
            _receiverHandler = serviceProvider.GetRequiredService<ReceiverHandler>();
        }
        [HttpGet]
        public async Task<IActionResult> CheckIsAlive()
        {
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> OrchestrationWorkingTask()
        {
            try
            {
                var executingTask = _receiverHandler.ExecuteTask?.Status;
                if (executingTask == null)
                {
                    return new JsonResult("No task Executing");

                }
                return new JsonResult(_receiverHandler.ExecuteTask?.Status);

            }
            catch (Exception ex)
            {
                return new JsonResult($"Message: {ex.Message} | StackTrace: {ex.StackTrace} | Source: {ex.Source} | InnerException: {ex.InnerException}") { StatusCode = 500 };
            }
        }

        [HttpGet]
        public async Task<IActionResult> StartOrchestration()
        {
            try
            {

                await _receiverHandler.StartAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                return new JsonResult($"Message: {ex.Message} | StackTrace: {ex.StackTrace} | Source: {ex.Source} | InnerException: {ex.InnerException}") { StatusCode = 500 };
            }

        }

        [HttpGet]
        public async Task<IActionResult> StopOrchestration()
        {
            try
            {

                await _receiverHandler.StopAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                return new JsonResult($"Message: {ex.Message} | StackTrace: {ex.StackTrace} | Source: {ex.Source} | InnerException: {ex.InnerException}") { StatusCode = 500 };
            }
        }
        [HttpGet]
        public async Task<IActionResult> Dispose()
        {
            _receiverHandler.Dispose();
            return Ok();
        }


    }
}