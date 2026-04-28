using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using FCT.G6T.Application.Interfaces;
using FCT.G6T.Application.Services;
using FCT.G6T.Domain.Interfaces;
using FCT.G6T.Domain.Models;
using FCT.G6T.HAL;
using FCT.G6T.Infrastructure.Camera;
using FCT.G6T.Infrastructure.Configuration;
using FCT.G6T.Infrastructure.Logging;
using FCT.G6T.Infrastructure.Serial;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FCT.G6T
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            var baseDirectory = AppContext.BaseDirectory;
            var configuration = BuildConfiguration(baseDirectory);

            var serialSettings = configuration.GetSection("Serial").Get<SerialSettings>() ?? new SerialSettings();
            var testTimeouts = configuration.GetSection("TestTimeouts").Get<TestTimeoutSettings>() ?? new TestTimeoutSettings();
            var cameraRuntime = configuration.GetSection("CameraRuntime").Get<CameraRuntimeSettings>() ?? new CameraRuntimeSettings();
            var loggingSettings = configuration.GetSection("Logging").Get<LoggingSettings>() ?? new LoggingSettings();

            var cameraConfig = LoadCameraConfig(baseDirectory);
            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                var logDirectory = Path.Combine(baseDirectory, loggingSettings.Directory);
                builder.AddProvider(new FileLoggerProvider(logDirectory, loggingSettings.FilePrefix, loggingSettings.RetentionDays));
            });

            services.AddSingleton(cameraConfig);
            services.AddSingleton<IComPortProvider, ComPortProvider>();
            services.AddSingleton<ICameraService>(_ =>
                new SdkCameraAdapter(TimeSpan.FromSeconds(cameraRuntime.CameraRetryIntervalSeconds), cameraRuntime.FrameBufferSize));
            services.AddSingleton<ICameraPreviewAppService, CameraPreviewAppService>();

            services.AddTransient<ISerialPortWrapper>(_ =>
                new SerialPortWrapper(serialSettings.ReadTimeoutMs, serialSettings.WriteTimeoutMs));
            services.AddSingleton<IG6TAdapter>(sp =>
                new G6TAdapter(
                    sp.GetRequiredService<ISerialPortWrapper>(),
                    sp.GetRequiredService<ILogger<G6TAdapter>>(),
                    TimeSpan.FromSeconds(serialSettings.FrameAckTimeoutSeconds),
                    serialSettings.FrameRetryCount));
            services.AddSingleton<IDetectorAdapter>(sp =>
                new DetectorAdapter(
                    sp.GetRequiredService<ISerialPortWrapper>(),
                    sp.GetRequiredService<ILogger<DetectorAdapter>>(),
                    TimeSpan.FromSeconds(serialSettings.DetectorAckTimeoutSeconds),
                    serialSettings.DetectorRetryCount));
            services.AddSingleton<TestOrchestrator>();
            services.AddSingleton<ISmokeDeviceTestService>(sp =>
                new SmokeDeviceTestService(
                    sp.GetRequiredService<TestOrchestrator>(),
                    sp.GetRequiredService<IG6TAdapter>(),
                    sp.GetRequiredService<IDetectorAdapter>(),
                    sp.GetRequiredService<ICameraPreviewAppService>(),
                    sp.GetRequiredService<ILogger<SmokeDeviceTestService>>(),
                    serialSettings.G6tBaudRate,
                    serialSettings.DetectorBaudRate,
                    TimeSpan.FromSeconds(testTimeouts.CommandAckTimeoutSeconds),
                    TimeSpan.FromSeconds(serialSettings.DetectorAckTimeoutSeconds),
                    TimeSpan.FromSeconds(testTimeouts.LedDetectTimeoutSeconds),
                    TimeSpan.FromSeconds(testTimeouts.ButtonTestTimeoutSeconds),
                    TimeSpan.FromMilliseconds(testTimeouts.LedDetectPollDelayMs)));

            services.AddSingleton<ITestCaseProvider>(_ =>
                new JsonTestCaseProvider(Path.Combine(baseDirectory, "config")));
            services.AddSingleton<Presentation.Forms.Mainform>();

            using var serviceProvider = services.BuildServiceProvider();
            var mainForm = serviceProvider.GetRequiredService<Presentation.Forms.Mainform>();
            System.Windows.Forms.Application.Run(mainForm);
        }

        private static IConfiguration BuildConfiguration(string baseDirectory)
        {
            return new ConfigurationBuilder()
                .SetBasePath(baseDirectory)
                .AddJsonFile(Path.Combine("config", "appsettings.json"), optional: false, reloadOnChange: false)
                .Build();
        }

        private static CameraConfig LoadCameraConfig(string baseDirectory)
        {
            var configPath = Path.Combine(baseDirectory, "config", "camera.json");
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException($"Không tìm thấy file cấu hình camera: {configPath}");
            }

            var json = File.ReadAllText(configPath);
            var cameraConfig = JsonSerializer.Deserialize<CameraConfig>(json);
            if (cameraConfig is null)
            {
                throw new InvalidOperationException("File cấu hình camera không hợp lệ.");
            }

            return cameraConfig;
        }
    }
}

