using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using HardwareTestApp.src.Application.Interfaces;
using HardwareTestApp.src.Application.Services;
using HardwareTestApp.src.Domain.Interfaces;
using HardwareTestApp.src.Domain.Models;
using HardwareTestApp.src.HAL;
using HardwareTestApp.src.Infrastructure.Camera;
using HardwareTestApp.src.Infrastructure.Serial;
using Microsoft.Extensions.Logging.Abstractions;

namespace HardwareTestApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var cameraConfig = LoadCameraConfig();
            var cameraService = new SdkCameraAdapter();
            var cameraPreviewAppService = new CameraPreviewAppService(cameraService, cameraConfig);
            IComPortProvider comPortProvider = new ComPortProvider();
            ISerialPortWrapper g6tSerialPortWrapper = new SerialPortWrapper();
            ISerialPortWrapper detectorSerialPortWrapper = new SerialPortWrapper();
            IG6TAdapter g6tAdapter = new G6TAdapter(g6tSerialPortWrapper, NullLogger<G6TAdapter>.Instance);
            IDetectorAdapter detectorAdapter = new DetectorAdapter(detectorSerialPortWrapper, NullLogger<DetectorAdapter>.Instance);
            var testOrchestrator = new TestOrchestrator(g6tAdapter, NullLogger<TestOrchestrator>.Instance);
            ISmokeDeviceTestService smokeDeviceTestService = new SmokeDeviceTestService(
                testOrchestrator,
                g6tAdapter,
                detectorAdapter,
                cameraPreviewAppService,
                NullLogger<SmokeDeviceTestService>.Instance);

            Application.Run(new src.Presentation.Forms.Mainform(cameraPreviewAppService, comPortProvider, smokeDeviceTestService));
        }

        private static CameraConfig LoadCameraConfig()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "config", "camera.json");
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
