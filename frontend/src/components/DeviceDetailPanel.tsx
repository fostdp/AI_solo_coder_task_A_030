import React, { useState, useEffect } from 'react';
import ReactECharts from 'echarts-for-react';
import type { Device, DeviceRealtimeData, DeviceTrendData } from '@/types';
import { deviceApi } from '@/services/api';

interface DeviceDetailPanelProps {
  device: Device;
  onClose: () => void;
}

const parameterLabels: Record<string, string> = {
  Power: '功率',
  SupplyTemperature: '供水温度',
  ReturnTemperature: '回水温度',
  Pressure: '压力',
  FlowRate: '流量',
  Frequency: '频率',
  Current: '电流',
  Voltage: '电压',
  InletTemperature: '进口温度',
  OutletTemperature: '出口温度',
  FanSpeed: '风机转速',
};

const parameterUnits: Record<string, string> = {
  Power: 'kW',
  SupplyTemperature: '°C',
  ReturnTemperature: '°C',
  Pressure: 'MPa',
  FlowRate: 'm³/h',
  Frequency: 'Hz',
  Current: 'A',
  Voltage: 'V',
  InletTemperature: '°C',
  OutletTemperature: '°C',
  FanSpeed: '%',
};

const parameterColors: Record<string, string> = {
  Power: '#3B82F6',
  SupplyTemperature: '#10B981',
  ReturnTemperature: '#F59E0B',
  Pressure: '#8B5CF6',
  FlowRate: '#EC4899',
};

export const DeviceDetailPanel: React.FC<DeviceDetailPanelProps> = ({ device, onClose }) => {
  const [realtimeData, setRealtimeData] = useState<DeviceRealtimeData | null>(null);
  const [trendData, setTrendData] = useState<DeviceTrendData[]>([]);
  const [selectedParam, setSelectedParam] = useState('Power');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      setLoading(true);
      try {
        const [realtimeRes, trendRes] = await Promise.all([
          deviceApi.getRealtimeData(device.id),
          deviceApi.getTrendData(device.id),
        ]);
        setRealtimeData(realtimeRes.data);
        setTrendData(trendRes.data);
      } catch (error) {
        console.error('获取设备数据失败:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
    const interval = setInterval(fetchData, 10000);
    return () => clearInterval(interval);
  }, [device.id]);

  const getStatusText = (status: number) => {
    switch (status) {
      case 0:
        return '待机';
      case 1:
        return '运行';
      case 2:
        return '故障';
      case 3:
        return '维护';
      default:
        return '未知';
    }
  };

  const getEfficiencyText = (status: number) => {
    switch (status) {
      case 1:
        return { text: '高效', color: 'text-green-400' };
      case 2:
        return { text: '效率偏低', color: 'text-yellow-400' };
      case 3:
      case 4:
        return { text: '低效', color: 'text-red-400' };
      default:
        return { text: '未知', color: 'text-slate-400' };
    }
  };

  const efficiency = getEfficiencyText(device.efficiencyStatus);

  const getChartOption = () => {
    const paramData = trendData.find((t) => t.parameterName === selectedParam);
    if (!paramData) {
      return {
        title: { text: '暂无数据', left: 'center', top: 'center', textStyle: { color: '#64748B' } },
      };
    }

    return {
      backgroundColor: 'transparent',
      grid: { left: 50, right: 20, top: 30, bottom: 40 },
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'rgba(15, 23, 42, 0.9)',
        borderColor: '#334155',
        textStyle: { color: '#F1F5F9' },
        formatter: (params: any) => {
          const p = params[0];
          return `${p.axisValue}<br/>${parameterLabels[selectedParam]}: <b>${p.value.toFixed(2)}</b> ${parameterUnits[selectedParam] || ''}`;
        },
      },
      xAxis: {
        type: 'time',
        axisLine: { lineStyle: { color: '#475569' } },
        axisLabel: { color: '#94A3B8', fontSize: 10 },
        splitLine: { lineStyle: { color: '#1E293B' } },
      },
      yAxis: {
        type: 'value',
        name: parameterUnits[selectedParam] || '',
        axisLine: { lineStyle: { color: '#475569' } },
        axisLabel: { color: '#94A3B8', fontSize: 10 },
        splitLine: { lineStyle: { color: '#1E293B' } },
        nameTextStyle: { color: '#94A3B8' },
      },
      series: [
        {
          name: parameterLabels[selectedParam],
          type: 'line',
          smooth: true,
          showSymbol: false,
          lineStyle: { width: 2, color: parameterColors[selectedParam] || '#3B82F6' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: `${parameterColors[selectedParam] || '#3B82F6'}40` },
                { offset: 1, color: `${parameterColors[selectedParam] || '#3B82F6'}00` },
              ],
            },
          },
          data: paramData.dataPoints.map((d) => [d.timestamp, d.value]),
        },
      ],
    };
  };

  const availableParams = trendData
    .filter((t) => t.dataPoints.some((d) => d.value > 0))
    .map((t) => t.parameterName);

  if (loading && !realtimeData) {
    return (
      <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50">
        <div className="bg-slate-800 rounded-xl p-8 text-center">
          <div className="animate-spin w-8 h-8 border-2 border-blue-500 border-t-transparent rounded-full mx-auto mb-4" />
          <div className="text-slate-300">加载中...</div>
        </div>
      </div>
    );
  }

  return (
    <div className="fixed inset-0 bg-black/50 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="bg-slate-800 rounded-xl border border-slate-700 w-full max-w-4xl max-h-[90vh] overflow-hidden shadow-2xl">
        <div className="flex items-center justify-between p-4 border-b border-slate-700">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-lg bg-blue-500/20 flex items-center justify-center">
              <span className="text-blue-400 text-xl">⚙️</span>
            </div>
            <div>
              <h2 className="text-xl font-bold text-white">{device.name}</h2>
              <div className="text-sm text-slate-400">
                设备编号: {device.id} | 设计COP: {device.designCOP}
              </div>
            </div>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-lg bg-slate-700 hover:bg-slate-600 flex items-center justify-center text-slate-400 hover:text-white transition-colors"
          >
            ✕
          </button>
        </div>

        <div className="p-4 overflow-y-auto max-h-[calc(90vh-80px)]">
          <div className="grid grid-cols-4 gap-4 mb-6">
            <div className="bg-slate-700/50 rounded-lg p-3">
              <div className="text-slate-400 text-sm mb-1">运行状态</div>
              <div
                className={`text-lg font-bold ${
                  device.status === 1
                    ? 'text-green-400'
                    : device.status === 2
                    ? 'text-red-400'
                    : 'text-slate-400'
                }`}
              >
                {getStatusText(device.status)}
              </div>
            </div>
            <div className="bg-slate-700/50 rounded-lg p-3">
              <div className="text-slate-400 text-sm mb-1">能效状态</div>
              <div className={`text-lg font-bold ${efficiency.color}`}>{efficiency.text}</div>
            </div>
            <div className="bg-slate-700/50 rounded-lg p-3">
              <div className="text-slate-400 text-sm mb-1">当前COP</div>
              <div className="text-lg font-bold text-blue-400">
                {device.currentCOP?.toFixed(2) || '--'}
              </div>
            </div>
            <div className="bg-slate-700/50 rounded-lg p-3">
              <div className="text-slate-400 text-sm mb-1">累计运行</div>
              <div className="text-lg font-bold text-purple-400">
                {device.operatingHours.toFixed(1)} h
              </div>
            </div>
          </div>

          {realtimeData && (
            <div className="mb-6">
              <h3 className="text-white font-semibold mb-3">实时运行参数</h3>
              <div className="grid grid-cols-3 md:grid-cols-6 gap-3">
                {Object.entries(realtimeData)
                  .filter(([key]) => !['deviceId', 'timestamp'].includes(key))
                  .slice(0, 6)
                  .map(([key, value]) => (
                    <div key={key} className="bg-slate-700/30 rounded-lg p-2 text-center">
                      <div className="text-xs text-slate-400">
                        {parameterLabels[key] || key}
                      </div>
                      <div className="text-sm font-semibold text-white">
                        {typeof value === 'number' ? value.toFixed(2) : value}
                        <span className="text-xs text-slate-500 ml-1">
                          {parameterUnits[key] || ''}
                        </span>
                      </div>
                    </div>
                  ))}
              </div>
            </div>
          )}

          <div>
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-white font-semibold">近24小时趋势曲线</h3>
              <div className="flex gap-2">
                {availableParams.map((param) => (
                  <button
                    key={param}
                    onClick={() => setSelectedParam(param)}
                    className={`px-3 py-1 text-xs rounded-lg transition-colors ${
                      selectedParam === param
                        ? 'bg-blue-500 text-white'
                        : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
                    }`}
                  >
                    {parameterLabels[param] || param}
                  </button>
                ))}
              </div>
            </div>
            <div className="h-64 bg-slate-900/50 rounded-lg">
              <ReactECharts option={getChartOption()} style={{ height: '100%', width: '100%' }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
