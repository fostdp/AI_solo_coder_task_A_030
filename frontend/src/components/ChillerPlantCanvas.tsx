import React, { useRef, useEffect, useState, useCallback } from 'react';
import type { Device, Pipeline } from '@/types';
import { DeviceType, DeviceStatus, EfficiencyStatus } from '@/types';

interface ChillerPlantCanvasProps {
  devices: Device[];
  onDeviceClick: (device: Device) => void;
  width?: number;
  height?: number;
}

const getDeviceColor = (device: Device): string => {
  if (device.status === DeviceStatus.Fault) return '#EF4444';
  if (device.status === DeviceStatus.Standby) return '#6B7280';
  if (device.status === DeviceStatus.Maintenance) return '#F59E0B';

  switch (device.efficiencyStatus) {
    case EfficiencyStatus.High:
      return '#10B981';
    case EfficiencyStatus.Medium:
      return '#F59E0B';
    case EfficiencyStatus.Low:
    case EfficiencyStatus.Fault:
      return '#EF4444';
    default:
      return '#6B7280';
  }
};

const getDeviceIcon = (deviceType: number): string => {
  switch (deviceType) {
    case DeviceType.CentrifugalChiller:
      return 'CEN';
    case DeviceType.ScrewChiller:
      return 'SCR';
    case DeviceType.CoolingTower:
      return 'CT';
    case DeviceType.ChilledWaterPump:
      return 'CHP';
    case DeviceType.CoolingWaterPump:
      return 'CWP';
    default:
      return 'DEV';
  }
};

const generatePipelines = (devices: Device[]): Pipeline[] => {
  const pipelines: Pipeline[] = [];
  const centrifugalChillers = devices.filter((d) => d.deviceType === DeviceType.CentrifugalChiller);
  const screwChillers = devices.filter((d) => d.deviceType === DeviceType.ScrewChiller);
  const coolingTowers = devices.filter((d) => d.deviceType === DeviceType.CoolingTower);
  const chilledPumps = devices.filter((d) => d.deviceType === DeviceType.ChilledWaterPump);
  const coolingPumps = devices.filter((d) => d.deviceType === DeviceType.CoolingWaterPump);

  const allChillers = [...centrifugalChillers, ...screwChillers];

  const systemCenter = { x: 600, y: 400 };

  chilledPumps.forEach((pump, i) => {
    allChillers.forEach((chiller, j) => {
      pipelines.push({
        id: `chilled-pipe-${i}-${j}`,
        from: { x: pump.positionX, y: pump.positionY },
        to: { x: chiller.positionX, y: chiller.positionY },
        type: 'chilled',
        flowDirection: 1,
      });
    });
  });

  allChillers.forEach((chiller, i) => {
    coolingPumps.forEach((pump, j) => {
      pipelines.push({
        id: `cooling-pipe-${i}-${j}`,
        from: { x: chiller.positionX, y: chiller.positionY },
        to: { x: pump.positionX, y: pump.positionY },
        type: 'cooling',
        flowDirection: 1,
      });
    });
  });

  coolingPumps.forEach((pump, i) => {
    coolingTowers.forEach((tower, j) => {
      pipelines.push({
        id: `tower-pipe-${i}-${j}`,
        from: { x: pump.positionX, y: pump.positionY },
        to: { x: tower.positionX, y: tower.positionY },
        type: 'cooling',
        flowDirection: 1,
      });
    });
  });

  return pipelines;
};

export const ChillerPlantCanvas: React.FC<ChillerPlantCanvasProps> = ({
  devices,
  onDeviceClick,
  width = 1200,
  height = 800,
}) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [hoveredDevice, setHoveredDevice] = useState<Device | null>(null);
  const animationFrameRef = useRef<number>(0);
  const arrowPhaseRef = useRef(0);

  const pipelines = generatePipelines(devices);

  const drawDevice = useCallback(
    (ctx: CanvasRenderingContext2D, device: Device, isHovered: boolean) => {
      const { positionX, positionY } = device;
      const size = 50;
      const color = getDeviceColor(device);
      const isRunning = device.status === DeviceStatus.Running;

      ctx.save();

      if (isHovered) {
        ctx.shadowColor = color;
        ctx.shadowBlur = 20;
      }

      ctx.beginPath();
      ctx.roundRect(positionX - size / 2, positionY - size / 2, size, size, 8);

      const gradient = ctx.createRadialGradient(positionX, positionY, 0, positionX, positionY, size);
      gradient.addColorStop(0, color + 'FF');
      gradient.addColorStop(1, color + 'CC');
      ctx.fillStyle = gradient;
      ctx.fill();

      ctx.strokeStyle = isHovered ? '#FFFFFF' : color;
      ctx.lineWidth = isHovered ? 3 : 2;
      ctx.stroke();

      if (isRunning) {
        ctx.beginPath();
        ctx.arc(positionX, positionY, size / 2 + 5, 0, Math.PI * 2);
        ctx.strokeStyle = color + '40';
        ctx.lineWidth = 3;
        ctx.setLineDash([5, 5]);
        ctx.stroke();
        ctx.setLineDash([]);
      }

      ctx.fillStyle = '#FFFFFF';
      ctx.font = 'bold 12px Arial';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(getDeviceIcon(device.deviceType), positionX, positionY - 8);

      ctx.fillStyle = '#FFFFFFCC';
      ctx.font = '10px Arial';
      ctx.fillText(device.name.split('-').pop() || '', positionX, positionY + 12);

      ctx.restore();
    },
    []
  );

  const drawPipeline = useCallback(
    (ctx: CanvasRenderingContext2D, pipeline: Pipeline, arrowPhase: number) => {
      const { from, to, type } = pipeline;
      const isChilled = type === 'chilled';
      const pipeColor = isChilled ? '#60A5FA' : '#F87171';

      const dx = to.x - from.x;
      const dy = to.y - from.y;
      const length = Math.sqrt(dx * dx + dy * dy);

      if (length < 10) return;

      const unitX = dx / length;
      const unitY = dy / length;

      ctx.save();

      ctx.beginPath();
      ctx.moveTo(from.x, from.y);
      ctx.lineTo(to.x, to.y);
      ctx.strokeStyle = pipeColor + '40';
      ctx.lineWidth = 8;
      ctx.lineCap = 'round';
      ctx.stroke();

      ctx.beginPath();
      ctx.moveTo(from.x, from.y);
      ctx.lineTo(to.x, to.y);
      ctx.strokeStyle = pipeColor;
      ctx.lineWidth = 4;
      ctx.stroke();

      const arrowSpacing = 60;
      const arrowCount = Math.max(1, Math.floor(length / arrowSpacing));

      for (let i = 0; i < arrowCount; i++) {
        const offset = ((arrowPhase + i * arrowSpacing) % length) / length;
        const arrowX = from.x + dx * offset;
        const arrowY = from.y + dy * offset;

        const perpX = -unitY;
        const perpY = unitX;

        ctx.beginPath();
        ctx.moveTo(arrowX, arrowY);
        ctx.lineTo(arrowX - unitX * 10 + perpX * 6, arrowY - unitY * 10 + perpY * 6);
        ctx.lineTo(arrowX - unitX * 10 - perpX * 6, arrowY - unitY * 10 - perpY * 6);
        ctx.closePath();
        ctx.fillStyle = '#FFFFFF';
        ctx.fill();
      }

      ctx.restore();
    },
    []
  );

  const drawGrid = useCallback((ctx: CanvasRenderingContext2D, w: number, h: number) => {
    ctx.save();

    const gridSize = 40;
    ctx.strokeStyle = '#1E293B';
    ctx.lineWidth = 1;

    for (let x = 0; x <= w; x += gridSize) {
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, h);
      ctx.stroke();
    }

    for (let y = 0; y <= h; y += gridSize) {
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(w, y);
      ctx.stroke();
    }

    const centerX = w / 2;
    const centerY = h / 2;

    const gradient = ctx.createRadialGradient(centerX, centerY, 0, centerX, centerY, Math.max(w, h) / 2);
    gradient.addColorStop(0, 'rgba(30, 58, 138, 0.1)');
    gradient.addColorStop(1, 'rgba(15, 23, 42, 0)');
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, w, h);

    ctx.restore();
  }, []);

  const drawLabels = useCallback((ctx: CanvasRenderingContext2D, w: number) => {
    ctx.save();

    const labelY = 30;
    const labelFont = 'bold 14px Arial';

    const sections = [
      { x: 100, label: '冷冻水泵', color: '#60A5FA' },
      { x: 300, label: '冷水机组', color: '#10B981' },
      { x: 900, label: '冷却水泵', color: '#F87171' },
      { x: 1100, label: '冷却塔', color: '#8B5CF6' },
    ];

    sections.forEach((section) => {
      ctx.font = labelFont;
      ctx.textAlign = 'center';
      ctx.fillStyle = section.color;
      ctx.fillText(section.label, section.x, labelY);

      ctx.beginPath();
      ctx.moveTo(section.x - 30, labelY + 10);
      ctx.lineTo(section.x + 30, labelY + 10);
      ctx.strokeStyle = section.color;
      ctx.lineWidth = 2;
      ctx.stroke();
    });

    const legendY = h - 30;
    const legendItems = [
      { color: '#10B981', label: '高效' },
      { color: '#F59E0B', label: '效率偏低' },
      { color: '#EF4444', label: '故障/低效' },
      { color: '#6B7280', label: '待机' },
    ];

    legendItems.forEach((item, i) => {
      const x = w / 2 - 150 + i * 80;
      ctx.beginPath();
      ctx.arc(x, legendY, 6, 0, Math.PI * 2);
      ctx.fillStyle = item.color;
      ctx.fill();

      ctx.fillStyle = '#94A3B8';
      ctx.font = '12px Arial';
      ctx.textAlign = 'left';
      ctx.fillText(item.label, x + 10, legendY + 4);
    });

    ctx.restore();
  }, []);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const render = () => {
      arrowPhaseRef.current = (arrowPhaseRef.current + 2) % 1000;

      ctx.fillStyle = '#0F172A';
      ctx.fillRect(0, 0, width, height);

      drawGrid(ctx, width, height);
      drawLabels(ctx, width);

      pipelines.forEach((pipeline) => {
        drawPipeline(ctx, pipeline, arrowPhaseRef.current);
      });

      devices.forEach((device) => {
        const isHovered = hoveredDevice?.id === device.id;
        drawDevice(ctx, device, isHovered);
      });

      animationFrameRef.current = requestAnimationFrame(render);
    };

    render();

    return () => {
      cancelAnimationFrame(animationFrameRef.current);
    };
  }, [devices, pipelines, hoveredDevice, width, height, drawDevice, drawPipeline, drawGrid, drawLabels]);

  const getDeviceAtPosition = useCallback(
    (clientX: number, clientY: number): Device | null => {
      const canvas = canvasRef.current;
      if (!canvas) return null;

      const rect = canvas.getBoundingClientRect();
      const x = clientX - rect.left;
      const y = clientY - rect.top;

      for (const device of devices) {
        const dx = x - device.positionX;
        const dy = y - device.positionY;
        if (Math.sqrt(dx * dx + dy * dy) <= 30) {
          return device;
        }
      }
      return null;
    },
    [devices]
  );

  const handleMouseMove = useCallback(
    (e: React.MouseEvent<HTMLCanvasElement>) => {
      const device = getDeviceAtPosition(e.clientX, e.clientY);
      setHoveredDevice(device);

      const canvas = canvasRef.current;
      if (canvas) {
        canvas.style.cursor = device ? 'pointer' : 'default';
      }
    },
    [getDeviceAtPosition]
  );

  const handleClick = useCallback(
    (e: React.MouseEvent<HTMLCanvasElement>) => {
      const device = getDeviceAtPosition(e.clientX, e.clientY);
      if (device) {
        onDeviceClick(device);
      }
    },
    [getDeviceAtPosition, onDeviceClick]
  );

  return (
    <div className="relative w-full h-full">
      <canvas
        ref={canvasRef}
        width={width}
        height={height}
        className="rounded-lg border border-slate-700"
        onMouseMove={handleMouseMove}
        onClick={handleClick}
      />
      {hoveredDevice && (
        <div
          className="absolute z-10 px-3 py-2 bg-slate-800 border border-slate-600 rounded-lg shadow-xl pointer-events-none"
          style={{
            left: hoveredDevice.positionX + 30,
            top: hoveredDevice.positionY - 20,
          }}
        >
          <div className="text-sm font-bold text-white">{hoveredDevice.name}</div>
          <div className="text-xs text-slate-400">
            状态: {hoveredDevice.status === 1 ? '运行' : hoveredDevice.status === 2 ? '故障' : '待机'}
          </div>
          {hoveredDevice.currentCOP && (
            <div className="text-xs text-blue-400">COP: {hoveredDevice.currentCOP.toFixed(2)}</div>
          )}
        </div>
      )}
    </div>
  );
};
