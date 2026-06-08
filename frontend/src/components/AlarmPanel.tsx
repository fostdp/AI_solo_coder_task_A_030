import React, { useState, useEffect } from 'react';
import type { Alarm } from '@/types';
import { alarmApi } from '@/services/api';
import { realtimeService } from '@/services/signalr';

interface AlarmPanelProps {
  maxItems?: number;
}

export const AlarmPanel: React.FC<AlarmPanelProps> = ({ maxItems = 10 }) => {
  const [alarms, setAlarms] = useState<Alarm[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchAlarms = async () => {
      try {
        const res = await alarmApi.getActive();
        setAlarms(res.data.slice(0, maxItems));
      } catch (error) {
        console.error('获取告警失败:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchAlarms();

    const unsubscribe = realtimeService.onAlarm((alarm) => {
      setAlarms((prev) => {
        const exists = prev.some((a) => a.id === alarm.id);
        if (exists) {
          return prev.map((a) => (a.id === alarm.id ? alarm : a)).slice(0, maxItems);
        }
        return [alarm, ...prev].slice(0, maxItems);
      });
    });

    return unsubscribe;
  }, [maxItems]);

  const getAlarmLevelColor = (level: number) => {
    switch (level) {
      case 1:
        return {
          bg: 'bg-amber-500/10',
          border: 'border-amber-500/30',
          text: 'text-amber-400',
          badge: 'bg-amber-500',
          label: '一级告警',
        };
      case 2:
        return {
          bg: 'bg-red-500/10',
          border: 'border-red-500/30',
          text: 'text-red-400',
          badge: 'bg-red-500',
          label: '二级告警',
        };
      default:
        return {
          bg: 'bg-slate-500/10',
          border: 'border-slate-500/30',
          text: 'text-slate-400',
          badge: 'bg-slate-500',
          label: '未知',
        };
    }
  };

  const formatTime = (timeStr: string) => {
    const date = new Date(timeStr);
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);

    if (minutes < 1) return '刚刚';
    if (minutes < 60) return `${minutes}分钟前`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}小时前`;
    return date.toLocaleDateString();
  };

  const handleAcknowledge = async (alarmId: number) => {
    try {
      await alarmApi.acknowledge(alarmId, 'admin');
      setAlarms((prev) => prev.filter((a) => a.id !== alarmId));
    } catch (error) {
      console.error('确认告警失败:', error);
    }
  };

  if (loading) {
    return (
      <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-white font-semibold">实时告警</h3>
        </div>
        <div className="flex items-center justify-center py-8">
          <div className="animate-spin w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full" />
        </div>
      </div>
    );
  }

  const criticalCount = alarms.filter((a) => a.alarmLevel === 2).length;

  return (
    <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-white font-semibold flex items-center gap-2">
          <span>实时告警</span>
          {criticalCount > 0 && (
            <span className="px-2 py-0.5 bg-red-500/20 text-red-400 text-xs rounded-full">
              {criticalCount} 个紧急
            </span>
          )}
        </h3>
        <span className="text-xs text-slate-400">{alarms.length} 条</span>
      </div>

      {alarms.length === 0 ? (
        <div className="text-center py-8">
          <div className="text-4xl mb-2">✅</div>
          <div className="text-slate-400 text-sm">暂无告警</div>
        </div>
      ) : (
        <div className="space-y-2 max-h-80 overflow-y-auto">
          {alarms.map((alarm) => {
            const colors = getAlarmLevelColor(alarm.alarmLevel);
            return (
              <div
                key={alarm.id}
                className={`${colors.bg} ${colors.border} border rounded-lg p-3 transition-all hover:scale-[1.02]`}
              >
                <div className="flex items-start justify-between gap-2">
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <span
                        className={`px-2 py-0.5 ${colors.badge} text-white text-xs rounded-full`}
                      >
                        {colors.label}
                      </span>
                      {alarm.deviceId && (
                        <span className="text-xs text-slate-400">{alarm.deviceId}</span>
                      )}
                    </div>
                    <div className={`text-sm ${colors.text} font-medium truncate`}>
                      {alarm.message}
                    </div>
                    {alarm.parameterName && (
                      <div className="text-xs text-slate-400 mt-1">
                        {alarm.parameterName}: {alarm.parameterValue?.toFixed(2)} (阈值:{' '}
                        {alarm.thresholdValue?.toFixed(2)})
                      </div>
                    )}
                    <div className="text-xs text-slate-500 mt-1">
                      持续 {alarm.durationMinutes} 分钟 · {formatTime(alarm.startTime)}
                    </div>
                  </div>
                  <button
                    onClick={() => handleAcknowledge(alarm.id)}
                    className="px-2 py-1 bg-slate-700 hover:bg-slate-600 text-slate-300 text-xs rounded-lg transition-colors shrink-0"
                  >
                    确认
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
};
