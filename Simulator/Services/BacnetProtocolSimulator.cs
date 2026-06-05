using System.Net;
using System.Net.Sockets;
using System.Text;
using BacnetSimulator.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BacnetSimulator.Services;

public class BacnetProtocolSimulator
{
    private readonly ILogger<BacnetProtocolSimulator> _logger;
    private readonly SimulatorConfig _config;
    private readonly UdpClient _udpClient;
    private readonly Random _random = new();

    public BacnetProtocolSimulator(
        ILogger<BacnetProtocolSimulator> logger,
        IOptions<SimulatorConfig> config)
    {
        _logger = logger;
        _config = config.Value;
        _udpClient = new UdpClient();
    }

    public async Task SendDeviceDataAsync(SimulatedDevice device)
    {
        try
        {
            var packet = BuildBacnetPacket(device);
            var endPoint = new IPEndPoint(IPAddress.Parse(_config.TargetAddress), _config.TargetPort);
            await _udpClient.SendAsync(packet, packet.Length, endPoint);
            
            _logger.LogInformation(
                "Sent BACnet data: Instance={Instance}, Type={Type}, Name={Name}, Power={Power:F1}kW, COP={COP:F2}, Load={Load:F1}%",
                device.BacnetInstance,
                device.DeviceType,
                device.DeviceName,
                device.CurrentPower,
                device.COP,
                device.LoadRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send BACnet data for device {Instance}", device.BacnetInstance);
        }
    }

    private byte[] BuildBacnetPacket(SimulatedDevice device)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write((byte)0x81);
        writer.Write((byte)0x0A);
        writer.Write((ushort)0x0000);
        
        var apdu = BuildApdu(device);
        writer.Write((ushort)(apdu.Length + 4));
        writer.Write((byte)0x01);
        writer.Write((byte)0x00);
        writer.Write(apdu);
        
        return ms.ToArray();
    }

    private byte[] BuildApdu(SimulatedDevice device)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write((byte)0x10);
        
        WriteBacnetReal(writer, 85, (float)device.CurrentPower);
        WriteBacnetReal(writer, 86, (float)device.SupplyWaterTemp);
        WriteBacnetReal(writer, 87, (float)device.ReturnWaterTemp);
        WriteBacnetReal(writer, 88, (float)device.CoolingWaterInTemp);
        WriteBacnetReal(writer, 89, (float)device.CoolingWaterOutTemp);
        WriteBacnetReal(writer, 90, (float)device.FlowRate);
        WriteBacnetReal(writer, 91, (float)device.SupplyPressure);
        WriteBacnetReal(writer, 92, (float)device.ReturnPressure);
        WriteBacnetReal(writer, 93, (float)device.LoadRate);
        WriteBacnetReal(writer, 94, (float)device.Frequency);
        WriteBacnetReal(writer, 95, (float)device.Vibration);
        WriteBacnetReal(writer, 96, (float)device.Current);
        WriteBacnetReal(writer, 97, (float)device.Voltage);
        WriteBacnetReal(writer, 98, (float)device.COP);
        WriteBacnetSigned(writer, 99, device.BacnetInstance);
        WriteBacnetSigned(writer, 100, (int)device.RunningHours);
        WriteBacnetSigned(writer, 101, device.Status);
        
        var timestampBytes = Encoding.UTF8.GetBytes(device.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"));
        WriteBacnetString(writer, 102, timestampBytes);
        
        return ms.ToArray();
    }

    private static void WriteBacnetTag(BinaryWriter writer, int tagNumber, int valueType, int length)
    {
        if (tagNumber < 15)
        {
            byte tagClass = 0x00;
            byte tagByte = (byte)((tagClass << 4) | tagNumber);
            
            if (length < 5)
            {
                tagByte |= (byte)(length << 2);
            }
            else
            {
                tagByte |= 0x06;
            }
            
            writer.Write(tagByte);
            
            if (length >= 5)
            {
                if (length < 254)
                {
                    writer.Write((byte)length);
                }
                else if (length < 65535)
                {
                    writer.Write((byte)0xFE);
                    writer.Write((ushort)length);
                }
                else
                {
                    writer.Write((byte)0xFF);
                    writer.Write((uint)length);
                }
            }
        }
        else
        {
            writer.Write((byte)0xF0);
            writer.Write((byte)tagNumber);
            writer.Write((byte)(valueType << 4));
            
            if (length < 254)
            {
                writer.Write((byte)length);
            }
            else if (length < 65535)
            {
                writer.Write((byte)0xFE);
                writer.Write((ushort)length);
            }
            else
            {
                writer.Write((byte)0xFF);
                writer.Write((uint)length);
            }
        }
    }

    private static void WriteBacnetReal(BinaryWriter writer, int tagNumber, float value)
    {
        WriteBacnetTag(writer, tagNumber, 4, 4);
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    private static void WriteBacnetSigned(BinaryWriter writer, int tagNumber, int value)
    {
        WriteBacnetTag(writer, tagNumber, 2, 4);
        var bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }
        writer.Write(bytes);
    }

    private static void WriteBacnetString(BinaryWriter writer, int tagNumber, byte[] value)
    {
        WriteBacnetTag(writer, tagNumber, 7, value.Length + 1);
        writer.Write((byte)0x00);
        writer.Write(value);
    }

    public void Dispose()
    {
        _udpClient?.Close();
        _udpClient?.Dispose();
    }
}
