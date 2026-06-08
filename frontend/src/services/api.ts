import axios from 'axios';
import type {
  Device,
  DeviceRealtimeData,
  DeviceTrendData,
  SystemMetrics,
  EfficiencyRecord,
  Alarm,
  WorkOrder,
  OptimizationRecommendation,
} from '@/types';

const api = axios.create({
  baseURL: '/api',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json',
  },
});

api.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);

export const deviceApi = {
  getAll: () => api.get<Device[]>('/device'),
  getById: (id: string) => api.get<Device>(`/device/${id}`),
  getByType: (type: number) => api.get<Device[]>(`/device/type/${type}`),
  getRealtimeData: (id: string) => api.get<DeviceRealtimeData>(`/device/${id}/realtime`),
  getTrendData: (id: string, startTime?: string, endTime?: string) => {
    const params: Record<string, string> = {};
    if (startTime) params.startTime = startTime;
    if (endTime) params.endTime = endTime;
    return api.get<DeviceTrendData[]>(`/device/${id}/trend`, { params });
  },
  updateStatus: (id: string, status: number) => api.put(`/device/${id}/status/${status}`),
};

export const efficiencyApi = {
  getCurrentMetrics: () => api.get<SystemMetrics>('/efficiency/current'),
  getHistory: (startTime?: string, endTime?: string) => {
    const params: Record<string, string> = {};
    if (startTime) params.startTime = startTime;
    if (endTime) params.endTime = endTime;
    return api.get<EfficiencyRecord[]>('/efficiency/history', { params });
  },
  getToday: () => api.get<EfficiencyRecord[]>('/efficiency/today'),
  getReports: (page = 1, pageSize = 20) =>
    api.get('/efficiency/reports', { params: { page, pageSize } }),
  calculate: () => api.post('/efficiency/calculate'),
};

export const optimizationApi = {
  getCurrent: () => api.get<OptimizationRecommendation>('/optimization/current'),
  getHistory: (page = 1, pageSize = 20) =>
    api.get<OptimizationRecommendation[]>('/optimization/history', { params: { page, pageSize } }),
  generate: () => api.post<OptimizationRecommendation>('/optimization/generate'),
  train: () => api.post('/optimization/train'),
  apply: (recommendationId: number, appliedBy: string) =>
    api.post('/optimization/apply', { recommendationId, appliedBy }),
  reject: (id: number, appliedBy: string) =>
    api.post(`/optimization/${id}/reject`, { appliedBy }),
};

export const alarmApi = {
  getActive: () => api.get<Alarm[]>('/alarm/active'),
  getByLevel: (level: number) => api.get<Alarm[]>(`/alarm/level/${level}`),
  getHistory: (startTime?: string, endTime?: string, page = 1, pageSize = 50) => {
    const params: Record<string, unknown> = { page, pageSize };
    if (startTime) params.startTime = startTime;
    if (endTime) params.endTime = endTime;
    return api.get<Alarm[]>('/alarm/history', { params });
  },
  getById: (id: number) => api.get<Alarm>(`/alarm/${id}`),
  acknowledge: (id: number, acknowledgedBy: string) =>
    api.put(`/alarm/${id}/acknowledge`, { acknowledgedBy }),
  clear: (id: number, acknowledgedBy: string) =>
    api.put(`/alarm/${id}/clear`, { acknowledgedBy }),
  getThresholds: () => api.get('/alarm/thresholds'),
};

export const workOrderApi = {
  getAll: (status?: number, page = 1, pageSize = 20) => {
    const params: Record<string, unknown> = { page, pageSize };
    if (status !== undefined) params.status = status;
    return api.get<WorkOrder[]>('/workorder', { params });
  },
  getById: (id: number) => api.get<WorkOrder>(`/workorder/${id}`),
  getByAlarmId: (alarmId: number) => api.get<WorkOrder>(`/workorder/alarm/${alarmId}`),
  create: (data: Partial<WorkOrder>) => api.post<WorkOrder>('/workorder', data),
  assign: (id: number, processor: string) =>
    api.put(`/workorder/${id}/assign`, { processor }),
  start: (id: number, processor: string) =>
    api.put(`/workorder/${id}/start`, { processor }),
  complete: (id: number, processor: string, resolution: string) =>
    api.put(`/workorder/${id}/complete`, { processor, resolution }),
  close: (id: number, processor: string) =>
    api.put(`/workorder/${id}/close`, { processor }),
  getStats: () => api.get('/workorder/stats'),
};

export const systemApi = {
  getConfig: () => api.get('/system/config'),
  getHealth: () => api.get('/system/health'),
  getDashboardSummary: () => api.get('/system/dashboard/summary'),
  getDeviceCount: () => api.get('/system/device-count'),
  getBACnetStatus: () => api.get('/system/bacnet/status'),
  getDesignCOP: () => api.get('/system/design/cop'),
};

export default api;
