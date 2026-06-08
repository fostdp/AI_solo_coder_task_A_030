import React, { useState, useEffect } from 'react';
import type { Device, SystemMetrics } from '@/types';
import { deviceApi, efficiencyApi } from '@/services/api';
import { realtimeService } from '@/services/signalr';
import { ChillerPlantCanvas } from '@/components/ChillerPlantCanvas';
import { MetricCard } from '@/components/MetricCard';
import { DeviceDetailPanel } from '@/components/DeviceDetailPanel';
import { AlarmPanel } from '@/components/AlarmPanel';
import { EfficiencyChart } from '@/components/EfficiencyChart';
import { OptimizationPanel } from '@/components/OptimizationPanel';

const App: React.FC = () => {
  const [devices, setDevices] = useState<Device[]>([]);
  const [metrics, setMetrics] = useState<SystemMetrics | null>(null);
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null);
  const [loading, setLoading] = useState(true);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);

  useEffect(() => {
    const initData = async () => {
      try {
        const [devicesRes, metricsRes] = await Promise.all([
          deviceApi.getAll(),
          efficiencyApi.getCurrentMetrics(),
        ]);
        setDevices(devicesRes.data);
        setMetrics(metricsRes.data);
      } catch (error) {
        console.error('初始化数据失败:', error);
      } finally {
        setLoading(false);
      }
    };

    initData();

    const initRealtime = async () => {
      await realtimeService.connect();
      await realtimeService.subscribeToAll();
    };
    initRealtime();

    const metricsUnsubscribe = realtimeService.onMetrics((newMetrics) => {
      setMetrics(newMetrics);
      setLastUpdate(new Date());
    });

    const deviceUnsubscribe = realtimeService.onDeviceData(() => {
      setLastUpdate(new Date());
    });

    const interval = setInterval(async () => {
      try {
        const res = await deviceApi.getAll();
        setDevices(res.data);
      } catch (error) {
        console.error('刷新设备列表失败:', error);
      }
    }, 30000);

    return () => {
      clearInterval(interval);
      metricsUnsubscribe();
      deviceUnsubscribe();
      realtimeService.disconnect();
    };
  }, []);

  const handleDeviceClick = (device: Device) => {
    setSelectedDevice(device);
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-slate-900 flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin w-12 h-12 border-4 border-blue-500 border-t-transparent rounded-full mx-auto mb-4" />
          <div className="text-slate-300 text-lg">系统加载中...</div>
        </div>
      </div>
    );
  }

  const runningCount = devices.filter((d) => d.status === 1).length;
  const faultCount = devices.filter((d) => d.status === 2).length;

  return (
    <div className="min-h-screen bg-slate-900">
      <header className="bg-slate-800/80 backdrop-blur-sm border-b border-slate-700 sticky top-0 z-40">
        <div className="max-w-full mx-auto px-6 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div className="w-10 h-10 bg-gradient-to-br from-blue-500 to-cyan-400 rounded-xl flex items-center justify-center">
                <span className="text-white text-xl">❄️</span>
              </div>
              <div>
                <h1 className="text-xl font-bold text-white">
                  智能建筑中央空调冷站群控与能效优化系统
                </h1>
                <div className="text-xs text-slate-400">
                  实时监控 · 智能优化 · 能效管理
                </div>
              </div>
            </div>
            <div className="flex items-center gap-6">
              <div className="text-right">
                <div className="text-xs text-slate-400">设备状态</div>
                <div className="text-sm">
                  <span className="text-green-400">{runningCount}</span>
                  <span className="text-slate-500">/{devices.length} 运行中</span>
                  {faultCount > 0 && (
                    <span className="text-red-400 ml-2">{faultCount} 故障</span>
                  )}
                </div>
              </div>
              <div className="text-right">
                <div className="text-xs text-slate-400">实时连接</div>
                <div className="flex items-center gap-2 justify-end">
                  <span
                    className={`w-2 h-2 rounded-full ${
                      realtimeService.isConnected() ? 'bg-green-500 animate-pulse' : 'bg-red-500'
                    }`}
                  />
                  <span className="text-sm text-slate-300">
                    {realtimeService.isConnected() ? '已连接' : '断开'}
                  </span>
                </div>
              </div>
              {lastUpdate && (
                <div className="text-right">
                  <div className="text-xs text-slate-400">最后更新</div>
                  <div className="text-sm text-slate-300">
                    {lastUpdate.toLocaleTimeString()}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </header>

      <main className="p-6">
        <div className="grid grid-cols-3 gap-4 mb-6">
          <MetricCard
            title="当日累计能耗"
            value={metrics?.dailyEnergy.toFixed(1) || '0'}
            unit="kWh"
            icon="⚡"
            color="blue"
            change={-5.2}
          />
          <MetricCard
            title="实时COP"
            value={metrics?.realtimeCOP.toFixed(2) || '0'}
            unit=""
            icon="📈"
            color="green"
            change={3.8}
          />
          <MetricCard
            title="累计节能量"
            value={metrics?.energySaving.toFixed(1) || '0'}
            unit="kWh"
            icon="💰"
            color="yellow"
            change={12.5}
          />
        </div>

        <div className="grid grid-cols-12 gap-6">
          <div className="col-span-8">
            <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4 mb-6">
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-white font-semibold">冷站系统流程图</h2>
                <div className="flex items-center gap-4 text-xs text-slate-400">
                  <div className="flex items-center gap-2">
                    <span className="w-3 h-3 rounded-full bg-blue-400" />
                    <span>冷冻水管</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <span className="w-3 h-3 rounded-full bg-red-400" />
                    <span>冷却水管</span>
                  </div>
                </div>
              </div>
              <ChillerPlantCanvas
                devices={devices}
                onDeviceClick={handleDeviceClick}
                width={900}
                height={550}
              />
            </div>
            <EfficiencyChart />
          </div>

          <div className="col-span-4 space-y-6">
            <AlarmPanel maxItems={8} />
            <OptimizationPanel />
          </div>
        </div>
      </main>

      {selectedDevice && (
        <DeviceDetailPanel
          device={selectedDevice}
          onClose={() => setSelectedDevice(null)}
        />
      )}
    </div>
  );
};

export default App;
