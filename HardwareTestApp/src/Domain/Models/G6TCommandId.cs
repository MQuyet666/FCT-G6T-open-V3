namespace FCT.G6T.Domain.Models;

public enum G6TCommandId : byte
{
    PowerControl = 0x01,
    TestButton = 0x02,
    SetCalibPin = 0x03,
    CloseWdi = 0x04,
    TestButton2 = 0x05,
    TestButton3 = 0x06,
    ActivateTheButton = 0x07,
    EmergencyButton = 0x07,
    RelayOutput = 0x08,
}

