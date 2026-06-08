<template>
  <div
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
    @click.self="handleClose"
  >
    <div class="w-full max-w-5xl max-h-[90vh] bg-slate-800 rounded-2xl shadow-2xl border border-slate-700 overflow-hidden">
      <div class="flex items-center justify-between px-6 py-4 border-b border-slate-700 bg-slate-800/80">
        <div class="flex items-center gap-4">
          <div
            class="w-12 h-12 rounded-xl flex items-center justify-center text-white font-bold"
            :style="{ backgroundColor: deviceColor }"
          >
            {{ deviceIcon }}
          </div>
          <div>
            <h2 class="text-xl font-bold text-white">{{ device.name }}</h2>
            <div class="flex items-center gap-3 mt-1">
              <span
                class="px-2 py-0.5 rounded text-xs font-medium"
                :class="statusClass"
              >
                {{ statusText }}
              </span>
              <span
                class="text-sm font-medium"
                :class="efficiencyClass"
              >
                {{ efficiencyText }}
              </span>
            </div>
          </div>
        </div>
        <button
          class="w-8 h-8 flex items-center justify-center rounded-lg hover:bg-slate-700 transition-colors text-slate-400 hover:text-white"
          @click="handleClose"
        >
          ✕
        </button>
      </div>

      <div class="p-6 overflow-y-auto max-h-[calc(90vh-80px)]">
        <div v-if="loading" class="flex items-center justify-center py-12">
          <div class="animate-spin w-8 h-8 border-4 border-blue-500 border-t-transparent rounded-full" />
        </div>

        <template v-else>
          <div class="grid grid-cols-4 gap-4 mb-6">
            <div
              v-for="(value, key) in displayParams"
              :key="key"
              class="bg-slate-700/50 rounded-xl p-4 border border-slate-600"
            >
              <div class="text-xs text-slate-400 mb-1">{{ parameterLabels[key] || key }}</div>
              <div class="flex items-baseline gap-1">
                <span class="text-xl font-bold text-white">{{ formatValue(value, key) }}</span>
                <span class="text-xs text-slate-500">{{ parameterUnits[key] || '' }}</span>
              </div>
            </div>
          </div>

          <div class="mb-6">
            <div class="flex items-center gap-4 mb-4">
              <button
                v-for="param in availableParams"
                :key="param"
                class="px-4 py-2 rounded-lg text-sm font-medium transition-all"
                :class="
                  selectedParam === param
                    ? 'bg-blue-600 text-white'
                    : 'bg-slate-700 text-slate-300 hover:bg-slate-600'
                "
                @click="selectedParam = param"
              >
                {{ parameterLabels[param] || param }}
              </button>
            </div>
            <div class="bg-slate-700/30 rounded-xl p-4 border border-slate-600">
              <v-chart
                class="h-64"
                :option="chartOption"
                autoresize
              />
            </div>
          </div>

          <div class="grid grid-cols-2 gap-6">
            <div class="bg-slate-700/30 rounded-xl p-4 border border-slate-600">
              <h3 class="text-white font-semibold mb-3">能效趋势 (24小时)</h3>
              <v-chart
                class="h-48"
                :option="efficiencyChartOption"
                autoresize
              />
            </div>
            <div class="bg-slate-700/30 rounded-xl p-4 border border-slate-600">
              <h3 class="text-white font-semibold mb-3">设备信息</h3>
              <div class="space-y-3">
                <div class="flex justify-between">
                  <span class="text-slate-400">设备编号</span>
                  <span class="text-white font-mono">{{ device.id }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">设备类型</span>
                  <span class="text-white">{{ deviceTypeName }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">安装位置</span>
                  <span class="text-white">{{ device.location || '冷站机房' }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">设计COP</span>
                  <span class="text-white">{{ device.designCOP?.toFixed(2) || '-' }}</span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">当前COP</span>
                  <span
                    class="font-bold"
                    :class="device.currentCOP && device.designCOP && device.currentCOP >= device.designCOP * 0.7 ? 'text-green-400' : 'text-red-400'"
                  >
                    {{ device.currentCOP?.toFixed(2) || '-' }}
                  </span>
                </div>
                <div class="flex justify-between">
                  <span class="text-slate-400">能效比</span>
                  <span
                    class="font-bold"
                    :class="efficiencyRatio >= 0.7 ? 'text-green-400' : efficiencyRatio >= 0.5 ? 'text-yellow-400' : 'text-red-400'"
                  >
                    {{ (efficiencyRatio * 100).toFixed(1) }}%
                  </span>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import VChart from 'vue-echarts';
import { use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { LineChart } from 'echarts/charts';
import {
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
} from 'echarts/components';
import type { Device, DeviceRealtimeData, DeviceTrendData } from '@/types';
import { DeviceType, DeviceStatus, EfficiencyStatus } from '@/types';
import { deviceApi } from '@/services/api';

use([
  CanvasRenderer,
  LineChart,
  TitleComponent,
  TooltipComponent,
  GridComponent,
  LegendComponent,
]);

interface Props {
  device: Device;
}

interface Emits {
  (e: 'close'): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

const realtimeData = ref<DeviceRealtimeData | null>(null);
const trendData = ref<DeviceTrendData[]>([]);
const selectedParam = ref('Power');
const loading = ref(true);
let dataInterval: number | null = null;

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

const deviceColor = computed(() => {
  if (props.device.status === DeviceStatus.Fault) return '#EF4444';
  if (props.device.status === DeviceStatus.Standby) return '#6B7280';
  switch (props.device.efficiencyStatus) {
    case EfficiencyStatus.High:
      return '#10B981';
    case EfficiencyStatus.Medium:
      return '#F59E0B';
    case EfficiencyStatus.Low:
      return '#EF4444';
    default:
      return '#6B7280';
  }
});

const deviceIcon = computed(() => {
  switch (props.device.deviceType) {
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
});

const deviceTypeName = computed(() => {
  switch (props.device.deviceType) {
    case DeviceType.CentrifugalChiller:
      return '离心式冷水机组';
    case DeviceType.ScrewChiller:
      return '螺杆式冷水机组';
    case DeviceType.CoolingTower:
      return '冷却塔';
    case DeviceType.ChilledWaterPump:
      return '冷冻水泵';
    case DeviceType.CoolingWaterPump:
      return '冷却水泵';
    default:
      return '未知设备';
  }
});

const statusText = computed(() => {
  switch (props.device.status) {
    case DeviceStatus.Standby:
      return '待机';
    case DeviceStatus.Running:
      return '运行中';
    case DeviceStatus.Fault:
      return '故障';
    case DeviceStatus.Maintenance:
      return '维护中';
    default:
      return '未知';
  }
});

const statusClass = computed(() => {
  switch (props.device.status) {
    case DeviceStatus.Running:
      return 'bg-green-500/20 text-green-400';
    case DeviceStatus.Fault:
      return 'bg-red-500/20 text-red-400';
    case DeviceStatus.Maintenance:
      return 'bg-yellow-500/20 text-yellow-400';
    default:
      return 'bg-slate-500/20 text-slate-400';
  }
});

const efficiencyInfo = computed(() => {
  switch (props.device.efficiencyStatus) {
    case EfficiencyStatus.High:
      return { text: '高效', class: 'text-green-400' };
    case EfficiencyStatus.Medium:
      return { text: '效率偏低', class: 'text-yellow-400' };
    case EfficiencyStatus.Low:
    case EfficiencyStatus.Fault:
      return { text: '低效', class: 'text-red-400' };
    default:
      return { text: '未知', class: 'text-slate-400' };
  }
});

const efficiencyText = computed(() => efficiencyInfo.value.text);
const efficiencyClass = computed(() => efficiencyInfo.value.class);

const efficiencyRatio = computed(() => {
  if (!props.device.currentCOP || !props.device.designCOP) return 0;
  return props.device.currentCOP / props.device.designCOP;
});

const displayParams = computed(() => {
  if (!realtimeData.value) return {};
  const params: Record<string, number> = {};
  if (realtimeData.value.power !== undefined) params.Power = realtimeData.value.power;
  if (realtimeData.value.supplyTemperature !== undefined) params.SupplyTemperature = realtimeData.value.supplyTemperature;
  if (realtimeData.value.returnTemperature !== undefined) params.ReturnTemperature = realtimeData.value.returnTemperature;
  if (realtimeData.value.flowRate !== undefined) params.FlowRate = realtimeData.value.flowRate;
  if (realtimeData.value.pressure !== undefined) params.Pressure = realtimeData.value.pressure;
  return params;
});

const availableParams = computed(() => Object.keys(displayParams.value));

const formatValue = (value: number, key: string): string => {
  if (key.includes('Temperature')) return value.toFixed(1);
  if (key === 'Power' || key === 'FlowRate') return value.toFixed(2);
  if (key === 'Pressure') return value.toFixed(3);
  return value.toFixed(0);
};

const chartOption = computed(() => {
  const color = parameterColors[selectedParam.value] || '#3B82F6';
  const data = trendData.value.map((d) => ({
    time: new Date(d.timestamp).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' }),
    value: (d as any)[selectedParam.value.charAt(0).toLowerCase() + selectedParam.value.slice(1)] ?? 0,
  }));

  return {
    tooltip: {
      trigger: 'axis',
      backgroundColor: 'rgba(15, 23, 42, 0.9)',
      borderColor: '#334155',
      textStyle: { color: '#F1F5F9' },
      formatter: (params: any) => {
        const p = params[0];
        return `${p.axisValue}<br/>${parameterLabels[selectedParam.value] || selectedParam.value}: ${p.value.toFixed(2)} ${parameterUnits[selectedParam.value] || ''}`;
      },
    },
    grid: { left: 50, right: 20, top: 20, bottom: 30 },
    xAxis: {
      type: 'category',
      data: data.map((d) => d.time),
      axisLine: { lineStyle: { color: '#475569' } },
      axisLabel: { color: '#94A3B8', fontSize: 10 },
    },
    yAxis: {
      type: 'value',
      name: parameterUnits[selectedParam.value] || '',
      nameTextStyle: { color: '#94A3B8' },
      axisLine: { lineStyle: { color: '#475569' } },
      axisLabel: { color: '#94A3B8', fontSize: 10 },
      splitLine: { lineStyle: { color: '#334155' } },
    },
    series: [
      {
        type: 'line',
        data: data.map((d) => d.value),
        smooth: true,
        symbol: 'circle',
        symbolSize: 4,
        lineStyle: { color, width: 2 },
        itemStyle: { color },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: color + '40' },
              { offset: 1, color: color + '00' },
            ],
          },
        },
      },
    ],
  };
});

const efficiencyChartOption = computed(() => {
  const data = trendData.value.map((d) => ({
    time: new Date(d.timestamp).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' }),
    cop: d.cop || 0,
  }));

  return {
    tooltip: {
      trigger: 'axis',
      backgroundColor: 'rgba(15, 23, 42, 0.9)',
      borderColor: '#334155',
      textStyle: { color: '#F1F5F9' },
      formatter: (params: any) => {
        const p = params[0];
        return `${p.axisValue}<br/>COP: ${p.value.toFixed(2)}`;
      },
    },
    grid: { left: 50, right: 20, top: 20, bottom: 30 },
    xAxis: {
      type: 'category',
      data: data.map((d) => d.time),
      axisLine: { lineStyle: { color: '#475569' } },
      axisLabel: { color: '#94A3B8', fontSize: 10 },
    },
    yAxis: {
      type: 'value',
      name: 'COP',
      nameTextStyle: { color: '#94A3B8' },
      axisLine: { lineStyle: { color: '#475569' } },
      axisLabel: { color: '#94A3B8', fontSize: 10 },
      splitLine: { lineStyle: { color: '#334155' } },
    },
    series: [
      {
        type: 'line',
        data: data.map((d) => d.cop),
        smooth: true,
        symbol: 'circle',
        symbolSize: 4,
        lineStyle: { color: '#10B981', width: 2 },
        itemStyle: { color: '#10B981' },
        markLine: {
          silent: true,
          lineStyle: { color: '#EF4444', type: 'dashed' },
          data: props.device.designCOP
            ? [{ yAxis: props.device.designCOP * 0.7, label: { formatter: '阈值 70%', color: '#EF4444' } }]
            : [],
        },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0,
            y: 0,
            x2: 0,
            y2: 1,
            colorStops: [
              { offset: 0, color: '#10B98140' },
              { offset: 1, color: '#10B98100' },
            ],
          },
        },
      },
    ],
  };
});

const fetchData = async () => {
  try {
    const [realtimeRes, trendRes] = await Promise.all([
      deviceApi.getRealtimeData(props.device.id),
      deviceApi.getTrendData(props.device.id),
    ]);
    realtimeData.value = realtimeRes.data;
    trendData.value = trendRes.data;
  } catch (error) {
    console.error('获取设备数据失败:', error);
  } finally {
    loading.value = false;
  }
};

const handleClose = () => {
  emit('close');
};

onMounted(() => {
  fetchData();
  dataInterval = window.setInterval(fetchData, 10000);
});

onUnmounted(() => {
  if (dataInterval) {
    clearInterval(dataInterval);
  }
});

watch(
  () => props.device.id,
  () => {
    loading.value = true;
    fetchData();
  }
);
</script>
