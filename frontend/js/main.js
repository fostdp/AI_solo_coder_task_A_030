const { createApp, ref, onMounted, onUnmounted, watch } = Vue;

const API_BASE_URL = 'http://localhost:5000/api';
const HUB_URL = 'http://localhost:5000/realtimeHub';

const DeviceTypeMap = {
    1: '离心式冷水机组',
    2: '螺杆式冷水机组',
    3: '冷却塔',
    4: '冷冻水泵',
    5: '冷却水泵'
};

const StatusMap = {
    1: { text: '运行', color: '#27ae60' },
    0: { text: '停机', color: '#636e72' },
    -1: { text: '故障', color: '#e74c3c' }
};

const EfficiencyStatusMap = {
    1: { text: '高效运行', color: '#27ae60' },
    2: { text: '效率正常', color: '#2ecc71' },
    3: { text: '效率偏低', color: '#f39c12' },
    4: { text: '低效/故障', color: '#e74c3c' }
};

function getStatusColor(status) {
    return StatusMap[status]?.color || '#636e72';
}

function getEfficiencyColor(efficiencyStatus) {
    return EfficiencyStatusMap[efficiencyStatus]?.color || '#636e72';
}

function formatTime(date) {
    return date.toLocaleTimeString('zh-CN', { hour12: false });
}

function formatDateTime(dateStr) {
    const date = new Date(dateStr);
    return date.toLocaleString('zh-CN', { hour12: false });
}

async function fetchDevices() {
    try {
        const response = await fetch(`${API_BASE_URL}/devices/status`);
        const data = await response.json();
        return data;
    } catch (error) {
        console.error('获取设备状态失败:', error);
        return [];
    }
}

async function fetchDeviceTrendData(deviceId) {
    try {
        const response = await fetch(`${API_BASE_URL}/devices/${deviceId}/trend`);
        const data = await response.json();
        return data;
    } catch (error) {
        console.error('获取设备趋势数据失败:', error);
        return [];
    }
}

async function fetchRealtimeDashboard() {
    try {
        const response = await fetch(`${API_BASE_URL}/efficiency/realtime`);
        const data = await response.json();
        return data;
    } catch (error) {
        console.error('获取实时数据失败:', error);
        return null;
    }
}

async function fetchAlarms() {
    try {
        const response = await fetch(`${API_BASE_URL}/alarms/active`);
        const data = await response.json();
        return data;
    } catch (error) {
        console.error('获取告警信息失败:', error);
        return [];
    }
}

async function fetchOptimization() {
    try {
        const response = await fetch(`${API_BASE_URL}/efficiency/optimization/latest`);
        const data = await response.json();
        return data;
    } catch (error) {
        console.error('获取优化建议失败:', error);
        return null;
    }
}

function initDeviceLayout(devices) {
    const layout = [];
    const positions = {
        chillers: [
            { x: 50, y: 80 }, { x: 150, y: 80 }, { x: 250, y: 80 },
            { x: 350, y: 80 }, { x: 450, y: 80 }
        ],
        coolingTowers: [
            { x: 50, y: 380 }, { x: 150, y: 380 }, { x: 250, y: 380 }, { x: 350, y: 380 },
            { x: 450, y: 380 }, { x: 550, y: 380 }, { x: 650, y: 380 }, { x: 750, y: 380 }
        ],
        chilledPumps: [
            { x: 50, y: 200 }, { x: 150, y: 200 }, { x: 250, y: 200 }, { x: 350, y: 200 },
            { x: 450, y: 200 }, { x: 550, y: 200 }, { x: 650, y: 200 }, { x: 750, y: 200 },
            { x: 50, y: 280 }, { x: 150, y: 280 }, { x: 250, y: 280 }, { x: 350, y: 280 }
        ],
        coolingPumps: [
            { x: 50, y: 320 }, { x: 150, y: 320 }, { x: 250, y: 320 }, { x: 350, y: 320 },
            { x: 450, y: 320 }, { x: 550, y: 320 }, { x: 650, y: 320 }, { x: 750, y: 320 },
            { x: 450, y: 280 }, { x: 550, y: 280 }, { x: 650, y: 280 }, { x: 750, y: 280 }
        ]
    };

    let chillerIdx = 0, towerIdx = 0, chilledPumpIdx = 0, coolingPumpIdx = 0;

    devices.forEach(device => {
        let pos;
        switch (device.deviceTypeId) {
            case 1:
            case 2:
                pos = positions.chillers[chillerIdx++];
                break;
            case 3:
                pos = positions.coolingTowers[towerIdx++];
                break;
            case 4:
                pos = positions.chilledPumps[chilledPumpIdx++];
                break;
            case 5:
                pos = positions.coolingPumps[coolingPumpIdx++];
                break;
            default:
                pos = { x: 100, y: 100 };
        }
        const effStatus = device.efficiencyStatus === '高效' ? 1 :
                          device.efficiencyStatus === '正常' ? 2 :
                          device.efficiencyStatus === '低效' ? 3 :
                          typeof device.efficiencyStatus === 'number' ? device.efficiencyStatus : 2;
        layout.push({
            ...device,
            x: pos.x,
            y: pos.y,
            statusColor: getEfficiencyColor(effStatus),
            efficiencyStatus: effStatus,
            typeName: DeviceTypeMap[device.deviceTypeId] || '未知设备',
            currentPower: device.currentPower ?? device.CurrentPower,
            loadRate: device.loadRate ?? device.CurrentLoadRate,
            cop: device.cop ?? device.CurrentCOP,
            designCop: device.designCop ?? device.DesignCOP
        });
    });

    return layout;
}

function initPipes(devices) {
    const pipes = [];
    const chillers = devices.filter(d => d.deviceTypeId === 1 || d.deviceTypeId === 2);
    const chilledPumps = devices.filter(d => d.deviceTypeId === 4);
    const coolingPumps = devices.filter(d => d.deviceTypeId === 5);
    const towers = devices.filter(d => d.deviceTypeId === 3);

    chillers.forEach((chiller, idx) => {
        if (chilledPumps[idx]) {
            pipes.push({
                fromDeviceId: chilledPumps[idx].deviceId,
                toDeviceId: chiller.deviceId,
                color: '#00cec9',
                flowDirection: 1
            });
        }
        if (coolingPumps[idx]) {
            pipes.push({
                fromDeviceId: chiller.deviceId,
                toDeviceId: coolingPumps[idx].deviceId,
                color: '#fdcb6e',
                flowDirection: 1
            });
        }
        if (towers[idx]) {
            pipes.push({
                fromDeviceId: coolingPumps[idx].deviceId,
                toDeviceId: towers[idx].deviceId,
                color: '#fdcb6e',
                flowDirection: 1
            });
        }
    });

    return pipes;
}

const App = {
    components: {
        ChillerFlow,
        DeviceDetail
    },
    setup() {
        const systemTime = ref(formatTime(new Date()));
        const connectionStatus = ref(false);
        const devices = ref([]);
        const layoutDevices = ref([]);
        const pipes = ref([]);
        const selectedDevice = ref(null);
        const deviceTrendData = ref([]);
        const showDeviceDetail = ref(false);
        const currentTab = ref('alarms');
        const alarms = ref([]);
        const optimization = ref(null);
        const dashboard = ref(null);
        let connection = null;
        let refreshInterval = null;
        let timeInterval = null;

        const kpi = {
            dailyEnergy: ref(0),
            realtimeCOP: ref(0),
            designCOP: ref(5.5),
            energySaving: ref(0),
            coolingCapacity: ref(0),
            totalPower: ref(0),
            copRatio: ref(0),
            savingPercent: ref(0)
        };

        function updateKPI(data) {
            if (!data) return;

            kpi.dailyEnergy.value = data.dailyTotalEnergy?.toFixed(1) || data.dailyEnergyConsumption?.toFixed(1) || '0';
            kpi.realtimeCOP.value = data.realtimeCOP?.toFixed(2) || '0';
            kpi.designCOP.value = data.designCOP?.toFixed(2) || '5.5';
            kpi.energySaving.value = data.totalEnergySaving?.toFixed(1) || data.energySaving?.toFixed(1) || '0';
            kpi.coolingCapacity.value = data.totalCoolingCapacity?.toFixed(0) || '0';
            kpi.totalPower.value = data.totalPowerConsumption?.toFixed(0) || data.totalPower?.toFixed(0) || '0';

            const designCop = data.designCOP || 5.5;
            const realtimeCop = data.realtimeCOP || 0;
            const copRatio = designCop > 0 ? (realtimeCop / designCop * 100) : 0;
            kpi.copRatio.value = copRatio.toFixed(1);
            kpi.savingPercent.value = data.energySavingPercent?.toFixed(1) || data.savingPercent?.toFixed(1) || '0';
        }

        async function handleDeviceClick(device) {
            selectedDevice.value = device;
            showDeviceDetail.value = true;
            deviceTrendData.value = await fetchDeviceTrendData(device.deviceId);
        }

        function handleDeviceHover(device) {
        }

        function closeDeviceDetail() {
            showDeviceDetail.value = false;
            selectedDevice.value = null;
            deviceTrendData.value = [];
        }

        function switchTab(tabName) {
            currentTab.value = tabName;
            if (tabName === 'alarms') {
                fetchAlarms().then(data => alarms.value = data);
            } else if (tabName === 'optimization') {
                fetchOptimization().then(data => optimization.value = data);
            }
        }

        async function refreshAllData() {
            const [devicesData, dashboardData, alarmsData, optimizationData] = await Promise.all([
                fetchDevices(),
                fetchRealtimeDashboard(),
                fetchAlarms(),
                fetchOptimization()
            ]);

            devices.value = devicesData;
            layoutDevices.value = initDeviceLayout(devicesData);
            pipes.value = initPipes(layoutDevices.value);

            dashboard.value = dashboardData;
            updateKPI(dashboardData);

            if (currentTab.value === 'alarms') {
                alarms.value = alarmsData;
            } else if (currentTab.value === 'optimization') {
                optimization.value = optimizationData;
            }
        }

        function initSignalR() {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(HUB_URL)
                .withAutomaticReconnect()
                .configureLogging(signalR.LogLevel.Information)
                .build();

            connection.on('ReceiveDashboardUpdate', (data) => {
                updateKPI(data);
            });

            connection.on('ReceiveDeviceStatusUpdate', (devicesData) => {
                devices.value = devicesData;
                layoutDevices.value = initDeviceLayout(devicesData);
                pipes.value = initPipes(layoutDevices.value);
            });

            connection.on('ReceiveAlarmUpdate', (alarmsData) => {
                if (currentTab.value === 'alarms') {
                    alarms.value = alarmsData;
                }
            });

            connection.on('ReceiveOptimizationUpdate', (optimizationData) => {
                if (currentTab.value === 'optimization') {
                    optimization.value = optimizationData;
                }
            });

            connection.start()
                .then(() => {
                    console.log('SignalR连接成功');
                    connectionStatus.value = true;
                })
                .catch(err => {
                    console.error('SignalR连接失败:', err);
                    connectionStatus.value = false;
                    setTimeout(initSignalR, 5000);
                });

            connection.onreconnecting(() => {
                connectionStatus.value = false;
            });

            connection.onreconnected(() => {
                connectionStatus.value = true;
            });
        }

        function applyOptimization() {
            alert('优化方案已提交，将在下一周期自动执行。');
        }

        function viewHistory() {
            alert('历史记录功能开发中...');
        }

        function acknowledgeAlarms(device) {
            alert(`已确认设备 ${device.deviceName} 的关联告警`);
        }

        function exportData(device) {
            alert(`正在导出设备 ${device.deviceName} 的运行数据...`);
        }

        function viewAlarms(device) {
            alert(`正在查看设备 ${device.deviceName} 的历史告警...`);
        }

        onMounted(async () => {
            timeInterval = setInterval(() => {
                systemTime.value = formatTime(new Date());
            }, 1000);

            await refreshAllData();
            initSignalR();

            refreshInterval = setInterval(refreshAllData, 30000);

            document.addEventListener('keydown', handleKeydown);
        });

        onUnmounted(() => {
            if (timeInterval) clearInterval(timeInterval);
            if (refreshInterval) clearInterval(refreshInterval);
            if (connection) connection.stop();
            document.removeEventListener('keydown', handleKeydown);
        });

        function handleKeydown(e) {
            if (e.key === 'Escape') {
                closeDeviceDetail();
            }
        }

        return {
            systemTime,
            connectionStatus,
            layoutDevices,
            pipes,
            selectedDevice,
            deviceTrendData,
            showDeviceDetail,
            currentTab,
            alarms,
            optimization,
            kpi,
            handleDeviceClick,
            handleDeviceHover,
            closeDeviceDetail,
            switchTab,
            applyOptimization,
            viewHistory,
            acknowledgeAlarms,
            exportData,
            viewAlarms,
            formatDateTime,
            DeviceTypeMap,
            StatusMap,
            EfficiencyStatusMap
        };
    },
    template: `
        <div class="app-container">
            <header class="app-header">
                <div class="header-left">
                    <h1>🏢 智能建筑中央空调冷站群控与能效优化系统</h1>
                </div>
                <div class="header-right">
                    <div class="system-time">{{ systemTime }}</div>
                    <div class="connection-status">
                        <span class="status-indicator" :class="connectionStatus ? 'online' : 'offline'"></span>
                        <span>{{ connectionStatus ? '已连接' : '未连接' }}</span>
                    </div>
                </div>
            </header>

            <div class="kpi-container">
                <div class="kpi-card energy">
                    <div class="kpi-icon">⚡</div>
                    <div class="kpi-content">
                        <div class="kpi-label">当日累计能耗</div>
                        <div class="kpi-value">{{ kpi.dailyEnergy.value }} <span class="kpi-unit">kWh</span></div>
                        <div class="kpi-trend down">-2.3%</div>
                    </div>
                </div>
                <div class="kpi-card cop">
                    <div class="kpi-icon">📊</div>
                    <div class="kpi-content">
                        <div class="kpi-label">实时COP</div>
                        <div class="kpi-value">{{ kpi.realtimeCOP.value }}</div>
                        <div class="kpi-ratio">设计值 {{ kpi.designCOP.value }} | 比值 {{ kpi.copRatio.value }}%</div>
                    </div>
                </div>
                <div class="kpi-card saving">
                    <div class="kpi-icon">💚</div>
                    <div class="kpi-content">
                        <div class="kpi-label">累计节能量</div>
                        <div class="kpi-value">{{ kpi.energySaving.value }} <span class="kpi-unit">kWh</span></div>
                        <div class="kpi-trend up">{{ kpi.savingPercent.value }}%</div>
                    </div>
                </div>
                <div class="kpi-card cooling">
                    <div class="kpi-icon">❄️</div>
                    <div class="kpi-content">
                        <div class="kpi-label">总制冷量</div>
                        <div class="kpi-value">{{ kpi.coolingCapacity.value }} <span class="kpi-unit">kW</span></div>
                        <div class="kpi-sub">总功率 {{ kpi.totalPower.value }} kW</div>
                    </div>
                </div>
            </div>

            <div class="main-content">
                <div class="left-panel">
                    <ChillerFlow
                        :devices="layoutDevices"
                        :pipes="pipes"
                        @device-click="handleDeviceClick"
                        @device-hover="handleDeviceHover"
                    />
                </div>

                <div class="right-panel">
                    <div class="tabs">
                        <button class="tab-btn" :class="{ active: currentTab === 'alarms' }" @click="switchTab('alarms')">⚠️ 告警信息</button>
                        <button class="tab-btn" :class="{ active: currentTab === 'optimization' }" @click="switchTab('optimization')">📈 优化建议</button>
                        <button class="tab-btn" :class="{ active: currentTab === 'devices' }" @click="switchTab('devices')">📋 设备列表</button>
                    </div>

                    <div class="tab-content" v-show="currentTab === 'alarms'">
                        <div class="alarm-list">
                            <div v-if="!alarms || alarms.length === 0" class="empty-state">暂无告警信息</div>
                            <div v-else v-for="alarm in alarms" :key="alarm.alarmId || alarm.Id" class="alarm-item" :class="'level-' + (alarm.alarmLevel ?? alarm.AlarmLevel)">
                                <div class="alarm-header">
                                    <span class="alarm-level" :class="'level-' + (alarm.alarmLevel ?? alarm.AlarmLevel)">
                                        {{ (alarm.alarmLevel ?? alarm.AlarmLevel) === 2 ? '二级告警' : '一级告警' }}
                                    </span>
                                    <span class="alarm-time">{{ formatDateTime(alarm.startTime ?? alarm.StartTime) }}</span>
                                </div>
                                <div class="alarm-device">{{ alarm.deviceName ?? alarm.DeviceName || '系统' }}</div>
                                <div class="alarm-message">{{ alarm.alarmMessage ?? alarm.AlarmMessage }}</div>
                                <div v-if="alarm.currentValue ?? alarm.ActualValue" class="alarm-value">
                                    当前值: {{ alarm.currentValue ?? alarm.ActualValue }} | 阈值: {{ alarm.thresholdValue ?? alarm.ThresholdValue }}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="tab-content" v-show="currentTab === 'optimization'">
                        <div class="optimization-card">
                            <div v-if="!optimization" class="empty-state">暂无优化建议</div>
                            <div v-else>
                                <div class="optimization-header">
                                    <span class="optimization-title">📈 最新优化方案</span>
                                    <span class="optimization-time">生成时间: {{ formatDateTime(optimization.createdTime ?? optimization.RecommendationTime) }}</span>
                                </div>
                                <div class="optimization-section">
                                    <h4>🎯 预测能效</h4>
                                    <div class="optimization-item">
                                        <span class="optimization-label">预测系统COP</span>
                                        <span class="optimization-value positive">{{ (optimization.predictedCOP ?? optimization.PredictedCOP)?.toFixed(3) || '-' }}</span>
                                    </div>
                                    <div class="optimization-item">
                                        <span class="optimization-label">预计节能率</span>
                                        <span class="optimization-value positive">{{ (optimization.energySavingPercent ?? optimization.ExpectedEnergySavingPercent)?.toFixed(1) || '-' }}%</span>
                                    </div>
                                    <div class="optimization-item">
                                        <span class="optimization-label">预计节能量</span>
                                        <span class="optimization-value positive">{{ (optimization.estimatedEnergySaving ?? optimization.ExpectedEnergySaving)?.toFixed(1) || '-' }} kWh/h</span>
                                    </div>
                                </div>
                                <div class="optimization-section">
                                    <h4>🌡️ 推荐温度设定</h4>
                                    <div class="optimization-item">
                                        <span class="optimization-label">冷冻水出水温度</span>
                                        <span class="optimization-value">{{ (optimization.recommendedChilledTemp ?? optimization.RecommendedSupplyWaterTemp)?.toFixed(1) || '-' }} °C</span>
                                    </div>
                                    <div class="optimization-item">
                                        <span class="optimization-label">当前系统负荷率</span>
                                        <span class="optimization-value">{{ (optimization.currentLoadRate ?? optimization.CurrentLoadRate)?.toFixed(1) || '-' }}%</span>
                                    </div>
                                </div>
                                <div class="optimization-section">
                                    <h4>⚙️ 推荐设备组合</h4>
                                    <div v-for="c in (optimization.recommendedCombination || [])" :key="c.deviceName" class="optimization-item">
                                        <span class="optimization-label">{{ c.deviceName }}</span>
                                        <span class="optimization-value" :class="c.isRecommended ? 'positive' : 'warning'">
                                            {{ c.isRecommended ? '✓ 运行' : '✗ 停机' }}
                                        </span>
                                    </div>
                                </div>
                                <div class="optimization-strategy">
                                    <strong>💡 优化策略:</strong> {{ optimization.optimizationStrategy ?? optimization.OptimizationStrategy || '基于神经网络模型预测，选择最优设备组合和运行参数，以最大化系统COP。' }}
                                </div>
                                <div class="optimization-actions">
                                    <button class="btn btn-primary" @click="applyOptimization">应用方案</button>
                                    <button class="btn btn-secondary" @click="viewHistory">历史记录</button>
                                </div>
                            </div>
                        </div>
                    </div>

                    <div class="tab-content" v-show="currentTab === 'devices'">
                        <div class="device-list">
                            <div v-if="!layoutDevices || layoutDevices.length === 0" class="empty-state">暂无设备数据</div>
                            <div v-else v-for="device in layoutDevices" :key="device.deviceId" 
                                 class="device-item" 
                                 :class="{ 'status-running': device.status === 1, 'status-fault': device.status === -1, 'status-stopped': device.status !== 1 && device.status !== -1 }"
                                 @click="handleDeviceClick(device)">
                                <div class="device-info-main">
                                    <div class="device-name">
                                        <span class="device-efficiency-dot" :style="{ background: device.statusColor }"></span>
                                        {{ device.deviceName }}
                                    </div>
                                    <div class="device-code">{{ device.deviceCode }} | {{ DeviceTypeMap[device.deviceTypeId] }}</div>
                                </div>
                                <div class="device-info-right">
                                    <div class="device-power">{{ (device.currentPower ?? device.CurrentPower)?.toFixed(0) || '-' }} kW</div>
                                    <div class="device-cop">COP: {{ (device.cop ?? device.CurrentCOP)?.toFixed(2) || '-' }}</div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            <DeviceDetail
                :visible="showDeviceDetail"
                :device="selectedDevice"
                :trend-data="deviceTrendData"
                @close="closeDeviceDetail"
                @acknowledge="acknowledgeAlarms"
                @export="exportData"
                @view-alarms="viewAlarms"
            />
        </div>
    `
};

const app = createApp(App);
app.component('ChillerFlow', ChillerFlow);
app.component('DeviceDetail', DeviceDetail);
app.mount('#app');
