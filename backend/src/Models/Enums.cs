namespace ChillerPlantOptimization.Models;

public enum DeviceType
{
    CentrifugalChiller = 1,
    ScrewChiller = 2,
    CoolingTower = 3,
    ChilledWaterPump = 4,
    CoolingWaterPump = 5
}

public enum DeviceStatus
{
    Stopped = 0,
    Running = 1,
    Fault = 2,
    Standby = 3
}

public enum EfficiencyStatus
{
    High = 0,
    Normal = 1,
    Low = 2,
    Fault = 3
}

public enum AlarmLevel
{
    Level1 = 1,
    Level2 = 2
}

public enum AlarmType
{
    ParameterExceedance = 1,
    LowEfficiency = 2,
    SystemFault = 3,
    CommunicationError = 4
}

public enum AlarmStatus
{
    Active = 0,
    Acknowledged = 1,
    Resolved = 2,
    Cleared = 3
}

public enum WorkOrderStatus
{
    Pending = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum RecommendationStatus
{
    New = 0,
    Applied = 1,
    Rejected = 2,
    Expired = 3
}

public enum UserRole
{
    Administrator = 0,
    Engineer = 1,
    Manager = 2
}
