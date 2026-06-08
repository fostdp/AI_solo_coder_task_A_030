import React, { useState, useEffect } from 'react';
import ReactECharts from 'echarts-for-react';
import type { EfficiencyRecord } from '@/types';
import { efficiencyApi } from '@/services/api';
import { realtimeService } from '@/services/signalr';

export const EfficiencyChart: React.FC = () => {
  const [records, setRecords] = useState<EfficiencyRecord[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const res = await efficiencyApi.getToday();
        setRecords(res.data);
      } catch (error) {
        console.error('获取能效数据失败:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();

    const unsubscribe = realtimeService.onEfficiency((record) => {
      setRecords((prev) => {
        const lastRecord = prev[prev.length - 1];
        if (
          lastRecord &&
          new Date(record.timestamp).getTime() - new Date(lastRecord.timestamp).getTime() < 60000
        ) {
          return [...prev.slice(0, -1), record];
        }
        return [...prev, record];
      });
    });

    return unsubscribe;
  }, []);

  const getChartOption = () => {
    if (!records.length) {
      return {
        title: { text: '暂无数据', left: 'center', top: 'center', textStyle: { color: '#64748B' } },
      };
    }

    const designCOP = records[0]?.designCOP || 5.0;
    const threshold70 = designCOP * 0.7;
    const threshold60 = designCOP * 0.6;

    return {
      backgroundColor: 'transparent',
      grid: { left: 50, right: 60, top: 60, bottom: 40 },
      legend: {
        data: ['系统COP', '设计COP(70%)', '设计COP(60%)', '总功率'],
        textStyle: { color: '#94A3B8' },
        top: 10,
      },
      tooltip: {
        trigger: 'axis',
        backgroundColor: 'rgba(15, 23, 42, 0.9)',
        borderColor: '#334155',
        textStyle: { color: '#F1F5F9' },
      },
      xAxis: {
        type: 'time',
        axisLine: { lineStyle: { color: '#475569' } },
        axisLabel: { color: '#94A3B8', fontSize: 10 },
        splitLine: { lineStyle: { color: '#1E293B' } },
      },
      yAxis: [
        {
          type: 'value',
          name: 'COP',
          min: 0,
          max: designCOP * 1.2,
          axisLine: { lineStyle: { color: '#475569' } },
          axisLabel: { color: '#94A3B8', fontSize: 10 },
          splitLine: { lineStyle: { color: '#1E293B' } },
          nameTextStyle: { color: '#94A3B8' },
        },
        {
          type: 'value',
          name: '功率 (kW)',
          axisLine: { lineStyle: { color: '#475569' } },
          axisLabel: { color: '#94A3B8', fontSize: 10 },
          splitLine: { show: false },
          nameTextStyle: { color: '#94A3B8' },
        },
      ],
      series: [
        {
          name: '系统COP',
          type: 'line',
          smooth: true,
          showSymbol: false,
          lineStyle: { width: 2, color: '#3B82F6' },
          areaStyle: {
            color: {
              type: 'linear',
              x: 0,
              y: 0,
              x2: 0,
              y2: 1,
              colorStops: [
                { offset: 0, color: 'rgba(59, 130, 246, 0.3)' },
                { offset: 1, color: 'rgba(59, 130, 246, 0)' },
              ],
            },
          },
          data: records.map((r) => [r.timestamp, r.systemCOP]),
          markLine: {
            silent: true,
            lineStyle: { type: 'dashed' },
            data: [
              {
                name: '设计COP',
                yAxis: designCOP,
                lineStyle: { color: '#10B981' },
                label: { formatter: '设计COP: {c}', color: '#10B981' },
              },
            ],
          },
        },
        {
          name: '设计COP(70%)',
          type: 'line',
          showSymbol: false,
          lineStyle: { width: 1, color: '#F59E0B', type: 'dashed' },
          data: records.map((r) => [r.timestamp, threshold70]),
        },
        {
          name: '设计COP(60%)',
          type: 'line',
          showSymbol: false,
          lineStyle: { width: 1, color: '#EF4444', type: 'dashed' },
          data: records.map((r) => [r.timestamp, threshold60]),
        },
        {
          name: '总功率',
          type: 'line',
          yAxisIndex: 1,
          smooth: true,
          showSymbol: false,
          lineStyle: { width: 2, color: '#F87171' },
          data: records.map((r) => [r.timestamp, r.totalPower]),
        },
      ],
    };
  };

  if (loading) {
    return (
      <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4 h-full">
        <h3 className="text-white font-semibold mb-4">能效曲线</h3>
        <div className="flex items-center justify-center h-48">
          <div className="animate-spin w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full" />
        </div>
      </div>
    );
  }

  return (
    <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4 h-full">
      <h3 className="text-white font-semibold mb-2">能效曲线 (今日)</h3>
      <div className="h-64">
        <ReactECharts option={getChartOption()} style={{ height: '100%', width: '100%' }} />
      </div>
    </div>
  );
};
