const DeviceDetail = {
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
    },
    template: `
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
    `
}
