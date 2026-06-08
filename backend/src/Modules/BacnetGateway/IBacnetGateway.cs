using ChillerPlantOptimization.Models;

namespace ChillerPlantOptimization.Modules.BacnetGateway;

public interface IBacnetGateway
{
    Task StartCollectionAsync(CancellationToken cancellationToken);
    Task<DeviceData?> ReadDeviceDataAsync(string deviceId);
    Task<IEnumerable<Device>> GetAllDevicesAsync();
}
