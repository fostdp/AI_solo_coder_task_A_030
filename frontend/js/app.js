const API_BASE_URL = 'http://localhost:5000/api';
const HUB_URL = 'http://localhost:5000/realtimeHub';

const AppState = {
    devices: [],
    pipes: [],
    selectedDevice: null,
    connection: null,
    flowChart: null,
    currentTab: 'alarms'
};

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

function updateSystemTime() {
    document.getElementById('systemTime').textContent = formatTime(new Date());
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

function updateKPI(dashboard) {
    if (!dashboard) return;

    document.getElementById('dailyEnergy').textContent = dashboard.dailyTotalEnergy?.toFixed(1) || dashboard.dailyEnergyConsumption?.toFixed(1) || '0';
    document.getElementById('realtimeCOP').textContent = dashboard.realtimeCOP?.toFixed(2) || '0';
    document.getElementById('designCOP').textContent = dashboard.designCOP?.toFixed(2) || '5.5';
    document.getElementById('energySaving').textContent = dashboard.totalEnergySaving?.toFixed(1) || dashboard.energySaving?.toFixed(1) || '0';
    document.getElementById('coolingCapacity').textContent = dashboard.totalCoolingCapacity?.toFixed(0) || '0';
    document.getElementById('totalPower').textContent = dashboard.totalPowerConsumption?.toFixed(0) || dashboard.totalPower?.toFixed(0) || '0';

    const designCop = dashboard.designCOP || 5.5;
    const realtimeCop = dashboard.realtimeCOP || 0;
    const copRatio = designCop > 0 ? (realtimeCop / designCop * 100) : 0;
    document.getElementById('copRatio').textContent = copRatio.toFixed(1);
    document.getElementById('savingPercent').textContent = dashboard.energySavingPercent?.toFixed(1) || dashboard.savingPercent?.toFixed(1) || '0';
}

function updateAlarmList(alarms) {
    const container = document.getElementById('alarmList');

    if (!alarms || alarms.length === 0) {
        container.innerHTML = '<div class="empty-state">暂无告警信息</div>';
        return;
    }

    const html = alarms.map(alarm => {
        const alarmLevel = alarm.alarmLevel ?? alarm.AlarmLevel;
        const startTime = alarm.startTime ?? alarm.StartTime;
        const deviceName = alarm.deviceName ?? alarm.DeviceName;
        const alarmMessage = alarm.alarmMessage ?? alarm.AlarmMessage;
        const currentValue = alarm.currentValue ?? alarm.ActualValue;
        const thresholdValue = alarm.thresholdValue ?? alarm.ThresholdValue;
        
        return `
        <div class="alarm-item level-${alarmLevel}">
            <div class="alarm-header">
                <span class="alarm-level level-${alarmLevel}">
                    ${alarmLevel === 2 ? '二级告警' : '一级告警'}
                </span>
                <span class="alarm-time">${formatDateTime(startTime)}</span>
            </div>
            <div class="alarm-device">${deviceName || '系统'}</div>
            <div class="alarm-message">${alarmMessage}</div>
            ${currentValue ? `<div class="alarm-value">当前值: ${currentValue} | 阈值: ${thresholdValue}</div>` : ''}
        </div>
    `}).join('');

    container.innerHTML = html;
}

function updateOptimizationCard(optimization) {
    const container = document.getElementById('optimizationCard');

    if (!optimization) {
        container.innerHTML = '<div class="empty-state">暂无优化建议</div>';
        return;
    }

    const deviceCombinations = optimization.recommendedCombination || [];
    const combinationHtml = deviceCombinations.map(c => `
        <div class="optimization-item">
            <span class="optimization-label">${c.deviceName}</span>
            <span class="optimization-value ${c.isRecommended ? 'positive' : 'warning'}">
                ${c.isRecommended ? '✓ 运行' : '✗ 停机'}
            </span>
        </div>
    `).join('');

    const predictedCOP = optimization.predictedCOP ?? optimization.PredictedCOP;
    const energySavingPercent = optimization.energySavingPercent ?? optimization.ExpectedEnergySavingPercent;
    const estimatedEnergySaving = optimization.estimatedEnergySaving ?? optimization.ExpectedEnergySaving;
    const recommendedChilledTemp = optimization.recommendedChilledTemp ?? optimization.RecommendedSupplyWaterTemp;
    const currentLoadRate = optimization.currentLoadRate ?? optimization.CurrentLoadRate;
    const recommendationTime = optimization.createdTime ?? optimization.RecommendationTime;
    const optimizationStrategy = optimization.optimizationStrategy ?? optimization.OptimizationStrategy;

    const html = `
        <div class="optimization-header">
            <span class="optimization-title">📈 最新优化方案</span>
            <span class="optimization-time">生成时间: ${formatDateTime(recommendationTime)}</span>
        </div>

        <div class="optimization-section">
            <h4>🎯 预测能效</h4>
            <div class="optimization-item">
                <span class="optimization-label">预测系统COP</span>
                <span class="optimization-value positive">${predictedCOP?.toFixed(3) || '-'}</span>
            </div>
            <div class="optimization-item">
                <span class="optimization-label">预计节能率</span>
                <span class="optimization-value positive">${energySavingPercent?.toFixed(1) || '-'}%</span>
            </div>
            <div class="optimization-item">
                <span class="optimization-label">预计节能量</span>
                <span class="optimization-value positive">${estimatedEnergySaving?.toFixed(1) || '-'} kWh/h</span>
            </div>
        </div>

        <div class="optimization-section">
            <h4>🌡️ 推荐温度设定</h4>
            <div class="optimization-item">
                <span class="optimization-label">冷冻水出水温度</span>
                <span class="optimization-value">${recommendedChilledTemp?.toFixed(1) || '-'} °C</span>
            </div>
            <div class="optimization-item">
                <span class="optimization-label">当前系统负荷率</span>
                <span class="optimization-value">${currentLoadRate?.toFixed(1) || '-'}%</span>
            </div>
        </div>

        <div class="optimization-section">
            <h4>⚙️ 推荐设备组合</h4>
            ${combinationHtml || '<div class="empty-state">暂无设备组合数据</div>'}
        </div>

        <div class="optimization-strategy">
            <strong>💡 优化策略:</strong> ${optimizationStrategy || '基于神经网络模型预测，选择最优设备组合和运行参数，以最大化系统COP。'}
        </div>

        <div class="optimization-actions">
            <button class="btn btn-primary" onclick="applyOptimization()">应用方案</button>
            <button class="btn btn-secondary" onclick="viewHistory()">历史记录</button>
        </div>
    `;

    container.innerHTML = html;
}

function updateDeviceList(devices) {
    const container = document.getElementById('deviceList');

    if (!devices || devices.length === 0) {
        container.innerHTML = '<div class="empty-state">暂无设备数据</div>';
        return;
    }

    const html = devices.map(device => {
        const statusClass = device.status === 1 ? 'status-running' : 
                           device.status === -1 ? 'status-fault' : 'status-stopped';
        
        const effStatus = device.efficiencyStatus === '高效' ? 1 :
                          device.efficiencyStatus === '正常' ? 2 :
                          device.efficiencyStatus === '低效' ? 3 :
                          typeof device.efficiencyStatus === 'number' ? device.efficiencyStatus : 2;
        const effColor = getEfficiencyColor(effStatus);
        
        const currentPower = device.currentPower ?? device.CurrentPower;
        const cop = device.cop ?? device.CurrentCOP;

        return `
            <div class="device-item ${statusClass}" onclick="openDeviceModal(${device.deviceId})">
                <div class="device-info-main">
                    <div class="device-name">
                        <span class="device-efficiency-dot" style="background: ${effColor}"></span>
                        ${device.deviceName}
                    </div>
                    <div class="device-code">${device.deviceCode} | ${DeviceTypeMap[device.deviceTypeId]}</div>
                </div>
                <div class="device-info-right">
                    <div class="device-power">${currentPower?.toFixed(0) || '-'} kW</div>
                    <div class="device-cop">COP: ${cop?.toFixed(2) || '-'}</div>
                </div>
            </div>
        `;
    }).join('');

    container.innerHTML = html;
}

async function openDeviceModal(deviceId) {
    const device = AppState.devices.find(d => d.deviceId === deviceId);
    if (!device) return;

    AppState.selectedDevice = device;

    const currentPower = device.currentPower ?? device.CurrentPower;
    const loadRate = device.loadRate ?? device.CurrentLoadRate;
    const cop = device.cop ?? device.CurrentCOP;
    const designCop = device.designCop ?? device.DesignCOP ?? 5.5;
    
    const effStatus = device.efficiencyStatus === '高效' ? 1 :
                      device.efficiencyStatus === '正常' ? 2 :
                      device.efficiencyStatus === '低效' ? 3 :
                      typeof device.efficiencyStatus === 'number' ? device.efficiencyStatus : 2;

    document.getElementById('modalDeviceName').textContent = device.deviceName;
    document.getElementById('modalDeviceCode').textContent = device.deviceCode;
    document.getElementById('modalDeviceType').textContent = DeviceTypeMap[device.deviceTypeId];
    document.getElementById('modalDeviceStatus').textContent = StatusMap[device.status]?.text || '-';
    document.getElementById('modalDevicePower').textContent = `${currentPower?.toFixed(1) || '-'} kW`;
    document.getElementById('modalDeviceLoad').textContent = `${loadRate?.toFixed(1) || '-'} %`;
    document.getElementById('modalDeviceCOP').textContent = cop?.toFixed(3) || '-';
    document.getElementById('modalDesignCOP').textContent = designCop?.toFixed(2) || '-';
    document.getElementById('modalEfficiencyStatus').textContent = EfficiencyStatusMap[effStatus]?.text || '-';
    document.getElementById('modalEfficiencyStatus').style.color = getEfficiencyColor(effStatus);

    trendChartManager.destroy();
    trendChartManager.initTrendChart('trendChart');
    trendChartManager.initCopChart('copChart');

    const trendData = await fetchDeviceTrendData(deviceId);
    trendChartManager.updateTrendChart(trendData);
    trendChartManager.updateCopChart(trendData, designCop);

    document.getElementById('deviceModal').classList.add('show');
}

function closeModal() {
    document.getElementById('deviceModal').classList.remove('show');
    AppState.selectedDevice = null;
    trendChartManager.destroy();
}

function switchTab(tabName) {
    AppState.currentTab = tabName;

    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.tab === tabName);
    });

    document.querySelectorAll('.tab-content').forEach(content => {
        content.classList.toggle('hidden', content.id !== tabName);
    });

    if (tabName === 'alarms') {
        fetchAlarms().then(updateAlarmList);
    } else if (tabName === 'optimization') {
        fetchOptimization().then(updateOptimizationCard);
    } else if (tabName === 'devices') {
        updateDeviceList(AppState.devices);
    }
}

function applyOptimization() {
    alert('优化方案已提交，将在下一周期自动执行。');
}

function viewHistory() {
    alert('历史记录功能开发中...');
}

async function refreshAllData() {
    const [devices, dashboard, alarms, optimization] = await Promise.all([
        fetchDevices(),
        fetchRealtimeDashboard(),
        fetchAlarms(),
        fetchOptimization()
    ]);

    AppState.devices = devices;
    const layoutDevices = initDeviceLayout(devices);
    const pipes = initPipes(layoutDevices);
    AppState.pipes = pipes;
    AppState.flowChart.updateData(layoutDevices, pipes);

    updateKPI(dashboard);
    
    if (AppState.currentTab === 'alarms') {
        updateAlarmList(alarms);
    } else if (AppState.currentTab === 'optimization') {
        updateOptimizationCard(optimization);
    } else if (AppState.currentTab === 'devices') {
        updateDeviceList(devices);
    }
}

function initSignalR() {
    AppState.connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL)
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    AppState.connection.on('ReceiveDashboardUpdate', (data) => {
        updateKPI(data);
    });

    AppState.connection.on('ReceiveDeviceStatusUpdate', (devices) => {
        AppState.devices = devices;
        const layoutDevices = initDeviceLayout(devices);
        const pipes = initPipes(layoutDevices);
        AppState.flowChart.updateData(layoutDevices, pipes);

        if (AppState.currentTab === 'devices') {
            updateDeviceList(devices);
        }
    });

    AppState.connection.on('ReceiveAlarmUpdate', (alarms) => {
        if (AppState.currentTab === 'alarms') {
            updateAlarmList(alarms);
        }
    });

    AppState.connection.on('ReceiveOptimizationUpdate', (optimization) => {
        if (AppState.currentTab === 'optimization') {
            updateOptimizationCard(optimization);
        }
    });

    AppState.connection.start()
        .then(() => {
            console.log('SignalR连接成功');
            updateConnectionStatus(true);
        })
        .catch(err => {
            console.error('SignalR连接失败:', err);
            updateConnectionStatus(false);
            setTimeout(initSignalR, 5000);
        });

    AppState.connection.onreconnecting(() => {
        updateConnectionStatus(false);
    });

    AppState.connection.onreconnected(() => {
        updateConnectionStatus(true);
    });
}

function updateConnectionStatus(connected) {
    const statusEl = document.getElementById('connectionStatus');
    const indicator = statusEl.querySelector('.status-indicator');
    const text = statusEl.querySelector('span:last-child');

    if (connected) {
        indicator.className = 'status-indicator online';
        text.textContent = '已连接';
    } else {
        indicator.className = 'status-indicator offline';
        text.textContent = '未连接';
    }
}

async function init() {
    updateSystemTime();
    setInterval(updateSystemTime, 1000);

    AppState.flowChart = new ChillerPlantFlowChart('flowCanvas');

    window.onDeviceClick = (device) => {
        openDeviceModal(device.deviceId);
    };

    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => switchTab(btn.dataset.tab));
    });

    await refreshAllData();

    initSignalR();

    setInterval(refreshAllData, 30000);
}

window.onload = init;
window.openDeviceModal = openDeviceModal;
window.closeModal = closeModal;
window.applyOptimization = applyOptimization;
window.viewHistory = viewHistory;
window.switchTab = switchTab;

document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeModal();
    }
});

document.getElementById('deviceModal').addEventListener('click', (e) => {
    if (e.target.id === 'deviceModal') {
        closeModal();
    }
});
