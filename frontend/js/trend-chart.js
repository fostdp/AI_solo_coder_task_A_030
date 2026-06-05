class TrendChartManager {
    constructor() {
        this.trendChart = null;
        this.copChart = null;
    }

    initTrendChart(canvasId) {
        const ctx = document.getElementById(canvasId).getContext('2d');
        this.trendChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: '冷冻水出水温度 (°C)',
                        data: [],
                        borderColor: '#00cec9',
                        backgroundColor: 'rgba(0, 206, 201, 0.1)',
                        tension: 0.4,
                        fill: true,
                        yAxisID: 'y'
                    },
                    {
                        label: '冷却水进水温度 (°C)',
                        data: [],
                        borderColor: '#fdcb6e',
                        backgroundColor: 'rgba(253, 203, 110, 0.1)',
                        tension: 0.4,
                        fill: true,
                        yAxisID: 'y'
                    },
                    {
                        label: '功率 (kW)',
                        data: [],
                        borderColor: '#e17055',
                        backgroundColor: 'rgba(225, 112, 85, 0.1)',
                        tension: 0.4,
                        fill: true,
                        yAxisID: 'y1'
                    },
                    {
                        label: '负荷率 (%)',
                        data: [],
                        borderColor: '#a29bfe',
                        backgroundColor: 'rgba(162, 155, 254, 0.1)',
                        tension: 0.4,
                        fill: true,
                        yAxisID: 'y2'
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            color: '#b2bec3',
                            font: { size: 11 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        titleColor: '#fff',
                        bodyColor: '#b2bec3',
                        borderColor: 'rgba(116, 185, 255, 0.3)',
                        borderWidth: 1
                    }
                },
                scales: {
                    x: {
                        display: true,
                        grid: {
                            color: 'rgba(116, 185, 255, 0.1)'
                        },
                        ticks: {
                            color: '#636e72',
                            maxTicksLimit: 12,
                            font: { size: 10 }
                        }
                    },
                    y: {
                        type: 'linear',
                        display: true,
                        position: 'left',
                        grid: {
                            color: 'rgba(116, 185, 255, 0.1)'
                        },
                        ticks: {
                            color: '#00cec9',
                            font: { size: 10 }
                        },
                        title: {
                            display: true,
                            text: '温度 (°C)',
                            color: '#00cec9',
                            font: { size: 11 }
                        }
                    },
                    y1: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        grid: {
                            drawOnChartArea: false
                        },
                        ticks: {
                            color: '#e17055',
                            font: { size: 10 }
                        },
                        title: {
                            display: true,
                            text: '功率 (kW)',
                            color: '#e17055',
                            font: { size: 11 }
                        }
                    },
                    y2: {
                        type: 'linear',
                        display: true,
                        position: 'right',
                        grid: {
                            drawOnChartArea: false
                        },
                        min: 0,
                        max: 100,
                        ticks: {
                            color: '#a29bfe',
                            font: { size: 10 }
                        },
                        title: {
                            display: true,
                            text: '负荷率 (%)',
                            color: '#a29bfe',
                            font: { size: 11 }
                        }
                    }
                }
            }
        });
    }

    initCopChart(canvasId) {
        const ctx = document.getElementById(canvasId).getContext('2d');
        this.copChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: [],
                datasets: [
                    {
                        label: '实时COP',
                        data: [],
                        borderColor: '#00b894',
                        backgroundColor: 'rgba(0, 184, 148, 0.2)',
                        tension: 0.4,
                        fill: true,
                        pointRadius: 3,
                        pointHoverRadius: 5
                    },
                    {
                        label: '设计COP',
                        data: [],
                        borderColor: '#d63031',
                        backgroundColor: 'transparent',
                        borderDash: [5, 5],
                        tension: 0,
                        pointRadius: 0
                    },
                    {
                        label: '能效阈值 (70%)',
                        data: [],
                        borderColor: '#f39c12',
                        backgroundColor: 'transparent',
                        borderDash: [3, 3],
                        tension: 0,
                        pointRadius: 0
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            color: '#b2bec3',
                            font: { size: 11 }
                        }
                    },
                    tooltip: {
                        backgroundColor: 'rgba(0, 0, 0, 0.8)',
                        titleColor: '#fff',
                        bodyColor: '#b2bec3',
                        borderColor: 'rgba(116, 185, 255, 0.3)',
                        borderWidth: 1
                    }
                },
                scales: {
                    x: {
                        display: true,
                        grid: {
                            color: 'rgba(116, 185, 255, 0.1)'
                        },
                        ticks: {
                            color: '#636e72',
                            maxTicksLimit: 12,
                            font: { size: 10 }
                        }
                    },
                    y: {
                        display: true,
                        min: 0,
                        grid: {
                            color: 'rgba(116, 185, 255, 0.1)'
                        },
                        ticks: {
                            color: '#74b9ff',
                            font: { size: 10 }
                        },
                        title: {
                            display: true,
                            text: 'COP',
                            color: '#74b9ff',
                            font: { size: 11 }
                        }
                    }
                }
            }
        });
    }

    updateTrendChart(trendData) {
        if (!this.trendChart || !trendData || trendData.length === 0) return;

        const labels = trendData.map(d => {
            const time = new Date(d.timestamp ?? d.Timestamp);
            return `${time.getHours().toString().padStart(2, '0')}:${time.getMinutes().toString().padStart(2, '0')}`;
        });

        this.trendChart.data.labels = labels;
        this.trendChart.data.datasets[0].data = trendData.map(d => d.supplyWaterTemp ?? d.SupplyWaterTemp ?? d.chilledWaterTemp ?? null);
        this.trendChart.data.datasets[1].data = trendData.map(d => d.coolingWaterInTemp ?? d.CoolingWaterInTemp ?? d.coolingWaterTemp ?? null);
        this.trendChart.data.datasets[2].data = trendData.map(d => d.power ?? d.Power ?? null);
        this.trendChart.data.datasets[3].data = trendData.map(d => d.loadRate ?? d.LoadRate ?? null);
        this.trendChart.update('none');
    }

    updateCopChart(trendData, designCop) {
        if (!this.copChart || !trendData || trendData.length === 0) return;

        const labels = trendData.map(d => {
            const time = new Date(d.timestamp ?? d.Timestamp);
            return `${time.getHours().toString().padStart(2, '0')}:${time.getMinutes().toString().padStart(2, '0')}`;
        });

        const thresholdCop = designCop * 0.7;

        this.copChart.data.labels = labels;
        this.copChart.data.datasets[0].data = trendData.map(d => d.cop ?? d.COP ?? null);
        this.copChart.data.datasets[1].data = trendData.map(() => designCop);
        this.copChart.data.datasets[2].data = trendData.map(() => thresholdCop);
        this.copChart.update('none');
    }

    destroy() {
        if (this.trendChart) {
            this.trendChart.destroy();
            this.trendChart = null;
        }
        if (this.copChart) {
            this.copChart.destroy();
            this.copChart = null;
        }
    }
}

const trendChartManager = new TrendChartManager();
