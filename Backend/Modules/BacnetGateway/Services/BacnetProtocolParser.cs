using System.Net;
using System.Net.Sockets;
using System.Text;
using ChillerPlant.Modules.BacnetGateway.Models;

namespace ChillerPlant.Modules.BacnetGateway.Services
{
    public class BacnetProtocolParser
    {
        private const byte BACNET_IP_TYPE = 0x81;
        private const byte BACNET_NPDU_VERSION = 1;
        private const byte BACNET_CONFIRMED_SERVICE = 0x04;
        private const byte BACNET_UNCONFIRMED_SERVICE = 0x10;
        private const byte BACNET_SERVICE_READ_PROPERTY_MULTIPLE = 0x0E;
        private const byte BACNET_SERVICE_I_AM = 0x01;
        private const byte BACNET_SERVICE_COV_NOTIFICATION = 0x01;
        private const byte BACNET_TAG_OBJECT_IDENTIFIER = 0x0C;
        private const byte BACNET_TAG_REAL = 0x44;
        private const byte BACNET_TAG_ENUMERATED = 0x21;
        private const byte BACNET_TAG_UNSIGNED_INTEGER = 0x22;
        private const byte BACNET_APPLICATION_TAG_REAL = 5;
        private const byte BACNET_APPLICATION_TAG_ENUMERATED = 9;
        private const byte BACNET_APPLICATION_TAG_UNSIGNED_INT = 2;
        private const byte BACNET_APPLICATION_TAG_SIGNED_INT = 1;

        public bool TryParseBacnetIpPacket(byte[] buffer, int received, out BacnetDataDto data, out int bacnetInstance)
        {
            data = null;
            bacnetInstance = 0;
            
            try
            {
                if (received < 10) return false;
                if (buffer[0] != BACNET_IP_TYPE) return false;

                var bacnetFunction = buffer[1];
                var bacnetLength = (buffer[2] << 8) | buffer[3];

                if (bacnetLength != received) return false;

                var npduOffset = 4;
                if (buffer[npduOffset] != BACNET_NPDU_VERSION) return false;

                var npduControl = buffer[npduOffset + 1];
                var isConfirmed = (npduControl & 0x04) != 0;

                int apduOffset = npduOffset + 2;
                if (apduOffset >= received) return false;

                var apduType = buffer[apduOffset] >> 4;
                if (apduType == BACNET_UNCONFIRMED_SERVICE >> 4)
                {
                    var serviceChoice = buffer[apduOffset + 1];
                    var serviceDataOffset = apduOffset + 2;

                    if (serviceChoice == BACNET_SERVICE_I_AM)
                    {
                        return TryParseIAm(buffer, serviceDataOffset, received, out bacnetInstance);
                    }
                    else if (serviceChoice == BACNET_SERVICE_COV_NOTIFICATION)
                    {
                        return TryParseCovNotification(buffer, serviceDataOffset, received, out data, out bacnetInstance);
                    }
                }
                else if (apduType == BACNET_CONFIRMED_SERVICE >> 4)
                {
                    var serviceChoice = buffer[apduOffset + 3];
                    if (serviceChoice == BACNET_SERVICE_READ_PROPERTY_MULTIPLE)
                    {
                        return TryParseReadPropertyMultipleResponse(buffer, apduOffset + 4, received, out data, out bacnetInstance);
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseIAm(byte[] buffer, int offset, int length, out int bacnetInstance)
        {
            bacnetInstance = 0;
            try
            {
                if (offset + 4 > length) return false;
                var deviceType = (buffer[offset] << 8) | buffer[offset + 1];
                bacnetInstance = (buffer[offset + 2] << 16) | (buffer[offset + 3] << 8) | buffer[offset + 4];
                return deviceType == 8;
            }
            catch
            {
                return false;
            }
        }

        private bool TryParseCovNotification(byte[] buffer, int offset, int length, out BacnetDataDto data, out int bacnetInstance)
        {
            data = null;
            bacnetInstance = 0;
            try
            {
                var current = offset;
                if (current + 5 > length) return false;

                var monitorObjectId = (buffer[current] << 16) | (buffer[current + 1] << 8) | buffer[current + 2];
                bacnetInstance = monitorObjectId & 0xFFFFF;
                current += 3;

                var timeRemaining = (buffer[current] << 8) | buffer[current + 1];
                current += 2;

                data = new BacnetDataDto
                {
                    BacnetInstance = bacnetInstance,
                    Timestamp = DateTime.Now
                };

                while (current < length)
                {
                    if (current + 1 > length) break;
                    var tag = buffer[current];
                    var tagNumber = (tag & 0xF0) >> 4;
                    var tagClass = (tag & 0x08) != 0;
                    var lengthValueType = tag & 0x07;

                    current++;
                    int len = 0;
                    if (lengthValueType <= 4) len = lengthValueType;
                    else if (lengthValueType == 5) { if (current >= length) break; len = buffer[current]; current++; }
                    else if (lengthValueType == 6) { if (current + 1 >= length) break; len = (buffer[current] << 8) | buffer[current + 1]; current += 2; }

                    if (current + len > length) break;

                    var tagValue = 0;
                    if (tagClass) { if (len > 0) tagValue = buffer[current]; }

                    if (tagClass && len == 1)
                    {
                        ParseCovPropertyValue(data, tagValue, buffer, current + 1, len - 1);
                    }
                    current += len;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ParseCovPropertyValue(BacnetDataDto data, int propertyId, byte[] buffer, int offset, int length)
        {
            if (offset >= buffer.Length) return;
            
            var appTag = buffer[offset];
            var tagValue = appTag >> 4;

            switch (tagValue)
            {
                case BACNET_APPLICATION_TAG_REAL:
                    if (offset + 4 < buffer.Length)
                    {
                        var rawValue = (buffer[offset + 1] << 24) | (buffer[offset + 2] << 16) | (buffer[offset + 3] << 8) | buffer[offset + 4];
                        var floatValue = BitConverter.ToSingle(BitConverter.GetBytes(rawValue), 0);
                        AssignPropertyValue(data, propertyId, floatValue);
                    }
                    break;
                case BACNET_APPLICATION_TAG_UNSIGNED_INT:
                    if (offset + 1 < buffer.Length)
                    {
                        var intValue = (int)buffer[offset + 1];
                        if (propertyId == 11) data.Status = intValue;
                    }
                    break;
                case BACNET_APPLICATION_TAG_ENUMERATED:
                    if (offset + 1 < buffer.Length)
                    {
                        var enumValue = buffer[offset + 1];
                        if (propertyId == 11) data.Status = enumValue;
                    }
                    break;
            }
        }

        private void AssignPropertyValue(BacnetDataDto data, int propertyId, float value)
        {
            switch (propertyId)
            {
                case 81: data.Power = (decimal)value; break;
                case 85: data.SupplyWaterTemp = (decimal)value; break;
                case 86: data.ReturnWaterTemp = (decimal)value; break;
                case 87: data.CoolingWaterInTemp = (decimal)value; break;
                case 88: data.CoolingWaterOutTemp = (decimal)value; break;
                case 103: data.FlowRate = (decimal)value; break;
                case 105: data.LoadRate = (decimal)value; break;
                case 114: data.Frequency = (decimal)value; break;
                case 118: data.Vibration = (decimal)value; break;
                case 119: data.Current = (decimal)value; break;
                case 120: data.Voltage = (decimal)value; break;
                case 121: data.RunningHours = (long)value; break;
                case 137: data.SupplyPressure = (decimal)value; break;
                case 138: data.ReturnPressure = (decimal)value; break;
            }
        }

        private bool TryParseReadPropertyMultipleResponse(byte[] buffer, int offset, int length, out BacnetDataDto data, out int bacnetInstance)
        {
            data = new BacnetDataDto();
            bacnetInstance = 0;
            try
            {
                var current = offset;
                var objectIdTag = buffer[current];
                if (objectIdTag != 0x0C) return false;
                current++;

                bacnetInstance = (buffer[current] << 16) | (buffer[current + 1] << 8) | buffer[current + 2];
                current += 3;

                data.BacnetInstance = bacnetInstance;
                data.Timestamp = DateTime.Now;

                while (current < length)
                {
                    var tag = buffer[current];
                    if (tag == 0x0E) break;
                    if (tag == 0x4E) break;
                    current++;

                    var propertyId = buffer[current];
                    current++;

                    if (current >= length) break;
                    var openingTag = buffer[current];
                    if (openingTag == 0x2E)
                    {
                        current++;
                        var appTag = buffer[current];
                        var tagValue = appTag >> 4;

                        if (tagValue == BACNET_APPLICATION_TAG_REAL && current + 4 < length)
                        {
                            var rawValue = (buffer[current + 1] << 24) | (buffer[current + 2] << 16) | (buffer[current + 3] << 8) | buffer[current + 4];
                            var floatValue = BitConverter.ToSingle(BitConverter.GetBytes(rawValue), 0);
                            AssignPropertyValue(data, propertyId, floatValue);
                            current += 5;
                        }
                        else if (tagValue == BACNET_APPLICATION_TAG_ENUMERATED && current + 1 < length)
                        {
                            if (propertyId == 11) data.Status = buffer[current + 1];
                            current += 2;
                        }
                        else if (tagValue == BACNET_APPLICATION_TAG_UNSIGNED_INT && current + 1 < length)
                        {
                            if (propertyId == 11) data.Status = buffer[current + 1];
                            current += 2;
                        }
                        else
                        {
                            current++;
                        }

                        if (current < length && buffer[current] == 0x2F) current++;
                    }
                    else
                    {
                        current++;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public byte[] BuildReadPropertyMultipleRequest(int bacnetInstance, int[] propertyIds)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);

            writer.Write((byte)0x81);
            writer.Write((byte)0x0A);
            byte[] payload;

            using var payloadMs = new MemoryStream();
            using var payloadWriter = new BinaryWriter(payloadMs);

            payloadWriter.Write((byte)BACNET_NPDU_VERSION);
            payloadWriter.Write((byte)0x24);
            payloadWriter.Write((byte)0x00);
            payloadWriter.Write((byte)BACNET_CONFIRMED_SERVICE);
            payloadWriter.Write((byte)0x01);
            payloadWriter.Write((byte)0x00);
            payloadWriter.Write((byte)BACNET_SERVICE_READ_PROPERTY_MULTIPLE);

            payloadWriter.Write((byte)BACNET_TAG_OBJECT_IDENTIFIER);
            payloadWriter.Write((byte)(bacnetInstance >> 16));
            payloadWriter.Write((byte)((bacnetInstance >> 8) & 0xFF));
            payloadWriter.Write((byte)(bacnetInstance & 0xFF));

            foreach (var pid in propertyIds)
            {
                payloadWriter.Write((byte)0x1C);
                payloadWriter.Write((byte)pid);
                payloadWriter.Write((byte)0x2E);
                payloadWriter.Write((byte)0x2F);
            }

            payloadWriter.Write((byte)0x1F);

            payload = payloadMs.ToArray();
            writer.Write((byte)(payload.Length >> 8));
            writer.Write((byte)(payload.Length & 0xFF));
            writer.Write(payload);

            return ms.ToArray();
        }

        public int[] GetStandardPropertyIds()
        {
            return new[] { 11, 81, 85, 86, 87, 88, 103, 105, 114, 118, 119, 120, 121, 137, 138 };
        }
    }
}
