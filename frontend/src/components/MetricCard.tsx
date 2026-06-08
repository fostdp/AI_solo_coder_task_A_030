import React from 'react';

interface MetricCardProps {
  title: string;
  value: string | number;
  unit: string;
  icon: string;
  color: 'green' | 'blue' | 'yellow' | 'red' | 'purple';
  change?: number;
  changeLabel?: string;
}

const colorClasses = {
  green: {
    bg: 'bg-emerald-500/10',
    border: 'border-emerald-500/30',
    text: 'text-emerald-400',
    icon: 'text-emerald-500',
  },
  blue: {
    bg: 'bg-blue-500/10',
    border: 'border-blue-500/30',
    text: 'text-blue-400',
    icon: 'text-blue-500',
  },
  yellow: {
    bg: 'bg-amber-500/10',
    border: 'border-amber-500/30',
    text: 'text-amber-400',
    icon: 'text-amber-500',
  },
  red: {
    bg: 'bg-red-500/10',
    border: 'border-red-500/30',
    text: 'text-red-400',
    icon: 'text-red-500',
  },
  purple: {
    bg: 'bg-purple-500/10',
    border: 'border-purple-500/30',
    text: 'text-purple-400',
    icon: 'text-purple-500',
  },
};

export const MetricCard: React.FC<MetricCardProps> = ({
  title,
  value,
  unit,
  icon,
  color,
  change,
  changeLabel = '较昨日',
}) => {
  const colors = colorClasses[color];

  return (
    <div className={`${colors.bg} ${colors.border} border rounded-xl p-5 backdrop-blur-sm`}>
      <div className="flex items-start justify-between mb-3">
        <div className={`w-10 h-10 rounded-lg flex items-center justify-center ${colors.bg}`}>
          <span className={`text-xl ${colors.icon}`}>{icon}</span>
        </div>
        {change !== undefined && (
          <span
            className={`text-xs px-2 py-1 rounded-full ${
              change >= 0 ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'
            }`}
          >
            {change >= 0 ? '↑' : '↓'} {Math.abs(change).toFixed(1)}%
          </span>
        )}
      </div>
      <div className="text-slate-400 text-sm mb-1">{title}</div>
      <div className="flex items-baseline gap-1">
        <span className={`text-3xl font-bold ${colors.text}`}>{value}</span>
        <span className="text-slate-500 text-sm">{unit}</span>
      </div>
      {change !== undefined && (
        <div className="text-xs text-slate-500 mt-2">
          {changeLabel} {change >= 0 ? '上升' : '下降'} {Math.abs(change).toFixed(1)}%
        </div>
      )}
    </div>
  );
};
