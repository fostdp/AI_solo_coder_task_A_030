import React, { useState, useEffect } from 'react';
import type { OptimizationRecommendation } from '@/types';
import { optimizationApi } from '@/services/api';

export const OptimizationPanel: React.FC = () => {
  const [recommendation, setRecommendation] = useState<OptimizationRecommendation | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const res = await optimizationApi.getCurrent();
        setRecommendation(res.data);
      } catch (error) {
        console.error('获取优化建议失败:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchData();
    const interval = setInterval(fetchData, 60000);
    return () => clearInterval(interval);
  }, []);

  const handleApply = async () => {
    if (!recommendation) return;
    try {
      await optimizationApi.apply(recommendation.id, 'admin');
      alert('优化方案已应用');
    } catch (error) {
      console.error('应用优化方案失败:', error);
    }
  };

  const handleGenerate = async () => {
    setLoading(true);
    try {
      const res = await optimizationApi.generate();
      setRecommendation(res.data);
    } catch (error) {
      console.error('生成优化方案失败:', error);
    } finally {
      setLoading(false);
    }
  };

  if (loading && !recommendation) {
    return (
      <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
        <h3 className="text-white font-semibold mb-4">优化建议</h3>
        <div className="flex items-center justify-center py-8">
          <div className="animate-spin w-6 h-6 border-2 border-blue-500 border-t-transparent rounded-full" />
        </div>
      </div>
    );
  }

  if (!recommendation) {
    return (
      <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-white font-semibold">优化建议</h3>
          <button
            onClick={handleGenerate}
            className="px-3 py-1 bg-blue-500 hover:bg-blue-600 text-white text-sm rounded-lg transition-colors"
          >
            生成建议
          </button>
        </div>
        <div className="text-center py-8">
          <div className="text-4xl mb-2">🔧</div>
          <div className="text-slate-400 text-sm">暂无优化建议</div>
        </div>
      </div>
    );
  }

  const statusText = ['', '待审核', '已应用', '已拒绝'][recommendation.status] || '未知';
  const statusColor =
    recommendation.status === 1
      ? 'text-yellow-400'
      : recommendation.status === 2
      ? 'text-green-400'
      : 'text-red-400';

  return (
    <div className="bg-slate-800/50 border border-slate-700 rounded-xl p-4">
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-white font-semibold">优化建议</h3>
        <div className="flex items-center gap-2">
          <span className={`text-sm ${statusColor}`}>{statusText}</span>
          <button
            onClick={handleGenerate}
            className="px-3 py-1 bg-blue-500 hover:bg-blue-600 text-white text-sm rounded-lg transition-colors"
            disabled={loading}
          >
            {loading ? '生成中...' : '刷新'}
          </button>
        </div>
      </div>

      <div className="space-y-3">
        <div className="grid grid-cols-2 gap-3">
          <div className="bg-slate-700/30 rounded-lg p-3">
            <div className="text-slate-400 text-xs mb-1">预测COP</div>
            <div className="text-2xl font-bold text-blue-400">
              {recommendation.predictedCOP.toFixed(2)}
            </div>
          </div>
          <div className="bg-slate-700/30 rounded-lg p-3">
            <div className="text-slate-400 text-xs mb-1">预计节电</div>
            <div className="text-2xl font-bold text-green-400">
              {recommendation.expectedEnergySaving.toFixed(1)}
              <span className="text-sm ml-1">kWh</span>
            </div>
          </div>
        </div>

        <div className="bg-blue-500/10 border border-blue-500/30 rounded-lg p-3">
          <div className="flex items-center gap-2 mb-2">
            <span className="text-blue-400">💡</span>
            <span className="text-white font-medium text-sm">推荐设备组合</span>
          </div>
          <div className="text-sm text-slate-300 space-y-1">
            <div>冷水机组: {recommendation.runningChillers}</div>
            <div>水泵: {recommendation.runningPumps}</div>
            <div>冷却塔: {recommendation.runningTowers}</div>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="bg-slate-700/30 rounded-lg p-3">
            <div className="text-slate-400 text-xs mb-1">冷冻水设定温度</div>
            <div className="text-lg font-bold text-cyan-400">
              {recommendation.chilledWaterSetpoint.toFixed(1)}°C
            </div>
          </div>
          <div className="bg-slate-700/30 rounded-lg p-3">
            <div className="text-slate-400 text-xs mb-1">节能率</div>
            <div className="text-lg font-bold text-emerald-400">
              +{recommendation.expectedSavingPercent.toFixed(1)}%
            </div>
          </div>
        </div>

        <div className="text-xs text-slate-500">
          生成时间: {new Date(recommendation.generatedAt).toLocaleString()}
          {' · '}
          当前负荷: {recommendation.loadRate.toFixed(1)}%
        </div>

        {recommendation.status === 1 && (
          <div className="flex gap-2 pt-2">
            <button
              onClick={handleApply}
              className="flex-1 py-2 bg-green-500 hover:bg-green-600 text-white text-sm rounded-lg transition-colors"
            >
              应用方案
            </button>
            <button
              onClick={() => optimizationApi.reject(recommendation.id, 'admin')}
              className="flex-1 py-2 bg-slate-600 hover:bg-slate-500 text-white text-sm rounded-lg transition-colors"
            >
              拒绝
            </button>
          </div>
        )}
      </div>
    </div>
  );
};
