<template>
  <div class="device-detail-modal" v-if="visible" @click.self="$emit('close')">
    <div class="modal-content large">
      <div class="modal-header">
        <h2>
          <span class="device-icon">{{ deviceIcon }}</span>
          {{ device?.deviceName || '设备详情' }}
        </h2>
        <button class="close-btn" @click="$emit('close')">&times;</button>
      </div>
      <div class="modal-body" v-if="device">
        <div class="device-info">
          <div class="info-section">
            <h3>基本信息</h3>
            <div class="info-grid">
              <div class="info-row">
                <span class="info-label">设备编号:</span>
                <span class="info-value">{{ device.deviceCode }}</span>
              </div>
              <div class="info-row">
                <span class="info-label">设备类型:</span>
                <span class="info-value">{{ device.typeName }}</span>
              </div>
              <div class="info-row">
                <span class="info-label">运行状态:</span>
                <span class="info-value status-badge" :class="statusClass">{{ statusText }}</span>
              </div>
              <div class="info-row">
                <span class="info-label">能效状态:</span>
                <span class="info-value efficiency-badge" :style="{ color: device.statusColor }">
                  {{ device.efficiencyStatus }}
                </span>
              </div>
              <div class="info-row">
                <span class="info-label">最后更新:</span>
                <span class="info-value">{{ formatTime(device.lastUpdateTime) }}</span>
              </div>
            </div>
          </div>

          <div class="info-section">
            <h3>实时运行参数</h3>
            <div class="params-grid">
              <div class="param-card power">
                <div class="param-icon">⚡</div>
                <div class="param-content">
                  <div class="param-label">当前功率</div>
                  <div class="param-value">{{ formatValue(device.currentPower) }} <span class="param-unit">kW</span></div>
                </div>
              </div>
              <div class="param-card load">
                <div class="param-icon">📊</div>
                <div class="param-content">
                  <div class="param-label">当前负荷</div>
                  <div class="param-value">{{ formatValue(device.currentLoadRate) }} <span class="param-unit">%</span></div>
                </div>
              </div>
              <div class="param-card cop">
                <div class="param-icon">📈</div>
                <div class="param-content">
                  <div class="param-label">实时COP</div>
                  <div class="param-value cop-value">{{ formatCOP(device.currentCOP) }}</div>
                </div>
              </div>
              <div class="param-card design">
                <div class="param-icon">🎯</div>
                <div class="param-content">
                  <div class="param-label">设计COP</div>
                  <div class="param-value">{{ device.designCOP?.toFixed(2) || '-' }}</div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="chart-section">
          <div class="chart-container">
            <div class="chart-header">
              <h3>近24小时运行参数趋势</h3>
              <div class="chart-legend">
                <span class="legend-item"><span class="legend-dot power-dot"></span>功率</span>
                <span class="legend-item"><span class="legend-dot load-dot"></span>负荷率</span>
                <span class="legend-item"><span class="legend-dot cop-dot"></span>COP</span>
              </div>
            </div>
            <canvas ref="trendChart" height="200"></canvas>
          </div>
          
          <div class="chart-container">
            <div class="chart-header">
              <h3>能效曲线</h3>
              <div class="chart-stats">
                <span>平均COP: <strong>{{ averageCOP }}</strong></span>
                <span>最高COP: <strong>{{ maxCOP }}</strong></span>
                <span>最低COP: <strong>{{ minCOP }}</strong></span>
              </div>
            </div>
            <canvas ref="copChart" height="160"></canvas>
          </div>
        </div>

        <div class="action-section">
          <button class="action-btn primary" @click="acknowledgeAlarms">
            📌 确认关联告警
          </button>
          <button class="action-btn secondary" @click="exportData">
            📥 导出运行数据
          </button>
          <button class="action-btn warning" @click="viewAlarms">
            ⚠️ 查看历史告警
          </button>
        </div>
      </div>

      <div class="modal-body empty" v-else>
        <div class="empty-state">
          <div class="empty-icon">📋</div>
          <p>请选择一个设备查看详情</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'DeviceDetail',
  props: {
    visible: {
      type: Boolean,
      default: false
    },
    device: {
      type: Object,
      default: null
    },
    trendData: {
      type: Array,
      default: () => []
    }
  },
  emits: ['close', 'acknowledge', 'export', 'viewAlarms'],
  data() {
    return {
      trendChartInstance: null,
      copChartInstance: null,
      deviceIcons: {
        1: '❄️',
        2: '🔧',
        3: '🌀',
        4: '💧',
        5: '💦'
      }
    }
  },
  computed: {
    deviceIcon() {
      return this.deviceIcons[this.device?.deviceTypeId] || '⚙️'
    },
    statusText() {
      const statusMap = {
        1: '运行中',
        0: '已停机',
        '-1': '故障',
        2: '待机'
      }
      return statusMap[this.device?.status] || '未知'
    },
    statusClass() {
      return {
        'running': this.device?.status === 1,
        'stopped': this.device?.status === 0,
        'fault': this.device?.status === -1,
        'standby': this.device?.status === 2
      }
    },
    averageCOP() {
      if (!this.trendData?.length) return '-'
      const copValues = this.trendData.filter(d => d.cop != null).map(d => d.cop)
      if (!copValues.length) return '-'
      return (copValues.reduce((a, b) => a + b, 0) / copValues.length).toFixed(2)
    },
    maxCOP() {
      if (!this.trendData?.length) return '-'
      const copValues = this.trendData.filter(d => d.cop != null).map(d => d.cop)
      if (!copValues.length) return '-'
      return Math.max(...copValues).toFixed(2)
    },
    minCOP() {
      if (!this.trendData?.length) return '-'
      const copValues = this.trendData.filter(d => d.cop != null).map(d => d.cop)
      if (!copValues.length) return '-'
      return Math.min(...copValues).toFixed(2)
    }
  },
  watch: {
    visible(newVal) {
      if (newVal && this.device) {
        this.$nextTick(() => {
          this.initCharts()
        })
      }
    },
    trendData() {
      if (this.visible && this.trendChartInstance) {
        this.updateCharts()
      }
    }
  },
  methods: {
    formatValue(val) {
      return val != null ? Number(val).toFixed(1) : '-'
    },
    formatCOP(cop) {
      if (cop == null) return '-'
      const val = Number(cop)
      return val.toFixed(2)
    },
    formatTime(time) {
      if (!time) return '-'
      const d = new Date(time)
      return d.toLocaleString('zh-CN', {
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
      })
    },
    initCharts() {
      this.destroyCharts()
      
      if (this.$refs.trendChart && window.Chart) {
        const trendCtx = this.$refs.trendChart.getContext('2d')
        this.trendChartInstance = new window.Chart(trendCtx, {
          type: 'line',
          data: this.getTrendChartData(),
          options: this.getTrendChartOptions()
        })
      }

      if (this.$refs.copChart && window.Chart) {
        const copCtx = this.$refs.copChart.getContext('2d')
        this.copChartInstance = new window.Chart(copCtx, {
          type: 'line',
          data: this.getCopChartData(),
          options: this.getCopChartOptions()
        })
      }
    },
    getTrendChartData() {
      const labels = this.trendData.map(d => {
        const dt = new Date(d.timestamp || d.time)
        return `${dt.getHours().toString().padStart(2, '0')}:${dt.getMinutes().toString().padStart(2, '0')}`
      })

      return {
        labels,
        datasets: [
          {
            label: '功率 (kW)',
            data: this.trendData.map(d => d.power ?? null),
            borderColor: '#fdcb6e',
            backgroundColor: 'rgba(253, 203, 110, 0.1)',
            yAxisID: 'y1',
            tension: 0.4,
            pointRadius: 0
          },
          {
            label: '负荷率 (%)',
            data: this.trendData.map(d => d.loadRate ?? null),
            borderColor: '#74b9ff',
            backgroundColor: 'rgba(116, 185, 255, 0.1)',
            yAxisID: 'y2',
            tension: 0.4,
            pointRadius: 0
          },
          {
            label: 'COP',
            data: this.trendData.map(d => d.cop ?? null),
            borderColor: '#00b894',
            backgroundColor: 'rgba(0, 184, 148, 0.1)',
            yAxisID: 'y3',
            tension: 0.4,
            pointRadius: 0
          }
        ]
      }
    },
    getTrendChartOptions() {
      return {
        responsive: true,
        maintainAspectRatio: false,
        interaction: {
          mode: 'index',
          intersect: false
        },
        plugins: {
          legend: { display: false }
        },
        scales: {
          x: {
            grid: { color: 'rgba(255,255,255,0.05)' },
            ticks: { color: '#b2bec3', maxTicksLimit: 8 }
          },
          y1: {
            position: 'left',
            grid: { color: 'rgba(255,255,255,0.05)' },
            ticks: { color: '#fdcb6e' },
            title: { display: true, text: '功率', color: '#fdcb6e' }
          },
          y2: {
            position: 'right',
            grid: { display: false },
            ticks: { color: '#74b9ff' },
            title: { display: true, text: '负荷(%)', color: '#74b9ff' }
          },
          y3: {
            display: false,
            min: 0,
            max: 8
          }
        }
      }
    },
    getCopChartData() {
      const labels = this.trendData.map(d => {
        const dt = new Date(d.timestamp || d.time)
        return `${dt.getHours().toString().padStart(2, '0')}:${dt.getMinutes().toString().padStart(2, '0')}`
      })

      return {
        labels,
        datasets: [
          {
            label: 'COP',
            data: this.trendData.map(d => d.cop ?? null),
            borderColor: '#00b894',
            backgroundColor: (context) => {
              const ctx = context.chart.ctx
              const gradient = ctx.createLinearGradient(0, 0, 0, 160)
              gradient.addColorStop(0, 'rgba(0, 184, 148, 0.3)')
              gradient.addColorStop(1, 'rgba(0, 184, 148, 0)')
              return gradient
            },
            fill: true,
            tension: 0.4,
            pointRadius: 2,
            pointHoverRadius: 4
          }
        ]
      }
    },
    getCopChartOptions() {
      return {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false }
        },
        scales: {
          x: {
            grid: { color: 'rgba(255,255,255,0.05)' },
            ticks: { color: '#b2bec3', maxTicksLimit: 8 }
          },
          y: {
            min: 0,
            grid: { color: 'rgba(255,255,255,0.05)' },
            ticks: { color: '#00b894' }
          }
        }
      }
    },
    updateCharts() {
      if (this.trendChartInstance) {
        this.trendChartInstance.data = this.getTrendChartData()
        this.trendChartInstance.update('none')
      }
      if (this.copChartInstance) {
        this.copChartInstance.data = this.getCopChartData()
        this.copChartInstance.update('none')
      }
    },
    destroyCharts() {
      if (this.trendChartInstance) {
        this.trendChartInstance.destroy()
        this.trendChartInstance = null
      }
      if (this.copChartInstance) {
        this.copChartInstance.destroy()
        this.copChartInstance = null
      }
    },
    acknowledgeAlarms() {
      this.$emit('acknowledge', this.device)
    },
    exportData() {
      this.$emit('export', this.device)
    },
    viewAlarms() {
      this.$emit('viewAlarms', this.device)
    }
  },
  beforeUnmount() {
    this.destroyCharts()
  }
}
</script>

<style scoped>
.device-detail-modal {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.modal-content {
  background: linear-gradient(135deg, #1a2744 0%, #0f1829 100%);
  border-radius: 12px;
  width: 90%;
  max-width: 900px;
  max-height: 90vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
  border: 1px solid rgba(116, 185, 255, 0.2);
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid rgba(116, 185, 255, 0.15);
  position: sticky;
  top: 0;
  background: rgba(26, 39, 68, 0.95);
  backdrop-filter: blur(10px);
  z-index: 10;
}

.modal-header h2 {
  margin: 0;
  color: #dfe6e9;
  font-size: 20px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.device-icon {
  font-size: 28px;
}

.close-btn {
  background: none;
  border: none;
  color: #b2bec3;
  font-size: 28px;
  cursor: pointer;
  padding: 0;
  width: 36px;
  height: 36px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s;
}

.close-btn:hover {
  background: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
}

.modal-body {
  padding: 24px;
}

.modal-body.empty {
  min-height: 300px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.empty-state {
  text-align: center;
  color: #636e72;
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 16px;
  opacity: 0.5;
}

.info-section {
  margin-bottom: 24px;
}

.info-section h3 {
  color: #74b9ff;
  font-size: 14px;
  margin: 0 0 16px 0;
  text-transform: uppercase;
  letter-spacing: 1px;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 12px;
}

.info-row {
  display: flex;
  justify-content: space-between;
  padding: 10px 14px;
  background: rgba(52, 152, 219, 0.05);
  border-radius: 6px;
}

.info-label {
  color: #636e72;
  font-size: 13px;
}

.info-value {
  color: #dfe6e9;
  font-size: 13px;
  font-weight: 500;
}

.status-badge {
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 12px;
}

.status-badge.running {
  background: rgba(39, 174, 96, 0.2);
  color: #27ae60;
}

.status-badge.stopped {
  background: rgba(149, 165, 166, 0.2);
  color: #95a5a6;
}

.status-badge.fault {
  background: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
}

.status-badge.standby {
  background: rgba(243, 156, 18, 0.2);
  color: #f39c12;
}

.efficiency-badge {
  font-weight: 600;
}

.params-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
}

.param-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 16px;
  border-radius: 8px;
  transition: transform 0.2s;
}

.param-card:hover {
  transform: translateY(-2px);
}

.param-card.power {
  background: linear-gradient(135deg, rgba(253, 203, 110, 0.15) 0%, rgba(243, 156, 18, 0.1) 100%);
  border: 1px solid rgba(253, 203, 110, 0.3);
}

.param-card.load {
  background: linear-gradient(135deg, rgba(116, 185, 255, 0.15) 0%, rgba(52, 152, 219, 0.1) 100%);
  border: 1px solid rgba(116, 185, 255, 0.3);
}

.param-card.cop {
  background: linear-gradient(135deg, rgba(0, 184, 148, 0.15) 0%, rgba(39, 174, 96, 0.1) 100%);
  border: 1px solid rgba(0, 184, 148, 0.3);
}

.param-card.design {
  background: linear-gradient(135deg, rgba(156, 89, 182, 0.15) 0%, rgba(142, 68, 173, 0.1) 100%);
  border: 1px solid rgba(156, 89, 182, 0.3);
}

.param-icon {
  font-size: 28px;
}

.param-label {
  color: #636e72;
  font-size: 12px;
  margin-bottom: 4px;
}

.param-value {
  color: #dfe6e9;
  font-size: 22px;
  font-weight: 700;
}

.cop-value {
  color: #00b894 !important;
}

.param-unit {
  font-size: 14px;
  color: #636e72;
  font-weight: 400;
}

.chart-section {
  margin-top: 24px;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.chart-container {
  background: rgba(0, 0, 0, 0.2);
  border-radius: 8px;
  padding: 16px;
  border: 1px solid rgba(116, 185, 255, 0.1);
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}

.chart-header h3 {
  margin: 0;
  color: #74b9ff;
  font-size: 14px;
}

.chart-legend, .chart-stats {
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: #b2bec3;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 6px;
}

.legend-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}

.power-dot { background: #fdcb6e; }
.load-dot { background: #74b9ff; }
.cop-dot { background: #00b894; }

.chart-stats strong {
  color: #00b894;
  margin-left: 4px;
}

.action-section {
  margin-top: 24px;
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.action-btn {
  flex: 1;
  min-width: 140px;
  padding: 12px 20px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
}

.action-btn.primary {
  background: linear-gradient(135deg, #74b9ff 0%, #0984e3 100%);
  color: white;
}

.action-btn.primary:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 20px rgba(116, 185, 255, 0.4);
}

.action-btn.secondary {
  background: rgba(116, 185, 255, 0.1);
  color: #74b9ff;
  border: 1px solid rgba(116, 185, 255, 0.3);
}

.action-btn.secondary:hover {
  background: rgba(116, 185, 255, 0.2);
}

.action-btn.warning {
  background: rgba(243, 156, 18, 0.1);
  color: #f39c12;
  border: 1px solid rgba(243, 156, 18, 0.3);
}

.action-btn.warning:hover {
  background: rgba(243, 156, 18, 0.2);
}
</style>
