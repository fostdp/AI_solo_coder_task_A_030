export interface Device {
  id: string;
  name: string;
  deviceType: number;
  deviceTypeName: string;
  designCOP: number;
  ratedPower: number;
  status: number;
  efficiencyStatus: number;
  currentCOP?: number;
  positionX: number;
  positionY: number;
  operatingHours: number;
}

export interface DeviceRealtimeData {
  deviceId: string;
  timestamp: string;
  power: number;
  supplyTemperature: number;
  returnTemperature: number;
  pressure: number;
  flowRate: number;
  frequency?: number;
  current?: number;
  voltage?: number;
  inletTemperature?: number;
  outletTemperature?: number;
  fanSpeed?: number;
}

export interface TrendDataPoint {
  timestamp: string;
  value: number;
}

export interface DeviceTrendData {
  deviceId: string;
  parameterName: string;
  dataPoints: TrendDataPoint[];
}

export interface SystemMetrics {
  timestamp: string;
  dailyEnergy: number;
  realtimeCOP: number;
  energySaving: number;
  peakPower: number;
  runningDeviceCount: number;
  totalDeviceCount: number;
}

export interface EfficiencyRecord {
  timestamp: string;
  systemCOP: number;
  designCOP: number;
  designCOPRatio: number;
  totalPower: number;
  totalCoolingCapacity: number;
  chilledWaterSupplyTemp: number;
  chilledWaterReturnTemp: number;
  coolingWaterSupplyTemp: number;
  coolingWaterReturnTemp: number;
  loadRate: number;
  dailyEnergyConsumption: number;
  energySaving: number;
}

export interface Alarm {
  id: number;
  deviceId?: string;
  deviceName?: string;
  alarmLevel: number;
  alarmType: number;
  message: string;
  startTime: string;
  endTime?: string;
  status: number;
  durationMinutes: number;
  parameterName?: string;
  parameterValue?: number;
  thresholdValue?: number;
}

export interface WorkOrder {
  id: number;
  workOrderNo: string;
  alarmId?: number;
  title: string;
  description: string;
  assignee?: string;
  status: number;
  priority: number;
  createdAt: string;
  completedAt?: string;
  completedBy?: string;
  resolution?: string;
}

export interface OptimizationRecommendation {
  id: number;
  generatedAt: string;
  deviceCombination: string;
  runningChillers: string;
  runningPumps: string;
  runningTowers: string;
  predictedCOP: number;
  predictedPower: number;
  chilledWaterSetpoint: number;
  expectedEnergySaving: number;
  expectedSavingPercent: number;
  loadRate: number;
  status: number;
}

export interface Pipeline {
  id: string;
  from: { x: number; y: number };
  to: { x: number; y: number };
  type: 'chilled' | 'cooling';
  flowDirection: 1 | -1;
}

export const DeviceType = {
  CentrifugalChiller: 1,
  ScrewChiller: 2,
  CoolingTower: 3,
  ChilledWaterPump: 4,
  CoolingWaterPump: 5,
};

export const DeviceStatus = {
  Standby: 0,
  Running: 1,
  Fault: 2,
  Maintenance: 3,
};

export const EfficiencyStatus = {
  High: 1,
  Medium: 2,
  Low: 3,
  Fault: 4,
};

export const AlarmLevel = {
  Level1: 1,
  Level2: 2,
};

export const AlarmStatus = {
  Active: 1,
  Acknowledged: 2,
  WorkOrderGenerated: 3,
  Resolved: 4,
  Cleared: 5,
};

export const WorkOrderStatus = {
  Created: 1,
  Assigned: 2,
  InProgress: 3,
  Completed: 4,
  Closed: 5,
};

export const RecommendationStatus = {
  Pending: 1,
  Applied: 2,
  Rejected: 3,
};
