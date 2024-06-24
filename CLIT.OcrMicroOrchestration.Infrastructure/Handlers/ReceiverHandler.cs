using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using CLIT.OcrMicroOrchestration.Domain.Models;
using CLIT.OcrMicroOrchestration.Infrastructure.Enum;
using CLIT.OcrMicroOrchestration.Infrastructure.Services;

namespace CLIT.OcrMicroOrchestration.Infrastructure.Handlers
{
    public class ReceiverHandler : BackgroundService
    {
        public Guid InstanceGuid { get; set; } = Guid.NewGuid();

        private static readonly Queue<OcrProcess> _queue = new Queue<OcrProcess>();
        public static CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        private readonly IConfiguration _configuration;
        private RabbitMQ.Client.IModel channel;
        private string _host;
        private int _port;
        private string _queueMQ;
        private int _maxProcessingCount;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public ReceiverHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private async Task<bool> StartReceiving()
        {

            using (var scope = _serviceScopeFactory.CreateScope())
            {

                try
                {
                    Console.WriteLine("StartReceiving");
                    await Initialize();
                    return true;

                }
                catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException ex)
                {
                    Console.WriteLine(ex.Message);
                    return await Reconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }

        }

        private async Task<bool> Reconnect()
        {
            var initialized = false;
            try
            {
                initialized = await Initialize();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(10));
                if (!initialized)
                {
                    initialized = await Reconnect();
                    if (initialized)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private async Task<bool> Initialize()
        {
            try
            {
                var increment = 0;
                var maxIncemernt = 12;
                while (true)
                {
                    var file = new FileInfo("IDCard.png");
                    byte[] imageBytes;

                    using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
                    {
                        imageBytes = new byte[stream.Length];
                        await stream.ReadAsync(imageBytes, 0, (int)stream.Length);
                    }
                    await using var scope = _serviceScopeFactory.CreateAsyncScope();
                    IOcrService ocrService = scope.ServiceProvider.GetService<IOcrService>();
                    var ocrProcess = new OcrProcess(Guid.NewGuid(), imageBytes);
                    ocrProcess.Tries = increment;
                    var result = await ocrService.CheckIdCardValidity(ocrProcess);
                    if (result == ValidityStatusEnum.Completed_with_Success)
                    {
                        Console.WriteLine("Success");
                        break;
                    }
                    increment++;
                    Console.WriteLine(result.ToString());

                    if (increment == maxIncemernt)
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return true;
        }


        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await StartReceiving();
        }

        public async Task StartAsync()
        {
            if (CancellationTokenSource.Token.IsCancellationRequested)
            {
                CancellationTokenSource = new CancellationTokenSource();
                Debug.WriteLine($"[Instance: {InstanceGuid}][x] Start Working {DateTime.UtcNow}");
                await base.StartAsync(CancellationTokenSource.Token);
            }

        }

        public async Task StopAsync()
        {
            Console.WriteLine($"[Instance: {InstanceGuid}][x] Stopped");
            CancellationTokenSource.Cancel();
            await base.StopAsync(CancellationTokenSource.Token);
        }


    }
}
