const ChillerFlow = {
    name: 'ChillerFlow',
    props: {
        devices: {
            type: Array,
            default: () => []
        },
        pipes: {
            type: Array,
            default: () => []
        },
        canvasWidth: {
            type: Number,
            default: 900
        },
        canvasHeight: {
            type: Number,
            default: 500
        }
    },
    emits: ['deviceClick', 'deviceHover'],
    data() {
        return {
            ctx: null,
            animationFrame: 0,
            selectedDevice: null,
            hoveredDevice: null,
            arrowPositions: [],
            deviceWidth: 70,
            deviceHeight: 50,
            deviceIcons: {
                1: '❄️',
                2: '🔧',
                3: '🌀',
                4: '💧',
                5: '💦'
            },
            animationId: null,
            displayDevices: []
        }
    },
    watch: {
        devices: {
            handler(newVal) {
                this.displayDevices = newVal.map(d => ({
                    ...d,
                    displayX: d.x,
                    displayY: d.y
                }))
                this.updateArrowPositions()
            },
            deep: true,
            immediate: true
        },
        pipes: {
            handler() {
                this.updateArrowPositions()
            },
            deep: true
        }
    },
    mounted() {
        this.initCanvas()
        this.startAnimation()
    },
    beforeUnmount() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId)
        }
    },
    methods: {
        initCanvas() {
            const canvas = this.$refs.flowCanvas
            this.ctx = canvas.getContext('2d')
        },
        
        startAnimation() {
            const animate = () => {
                this.animationFrame = (this.animationFrame + 1) % 60
                this.draw()
                this.animationId = requestAnimationFrame(animate)
            }
            animate()
        },
        
        handleCanvasClick(e) {
            const { x, y } = this.getCanvasCoords(e)
            for (const device of this.displayDevices) {
                if (this.isPointInDevice(x, y, device)) {
                    this.selectedDevice = device
                    this.$emit('deviceClick', device)
                    break
                }
            }
        },
        
        handleMouseMove(e) {
            const { x, y } = this.getCanvasCoords(e)
            this.hoveredDevice = null
            
            for (const device of this.displayDevices) {
                if (this.isPointInDevice(x, y, device)) {
                    this.hoveredDevice = device
                    this.$refs.flowCanvas.style.cursor = 'pointer'
                    this.$emit('deviceHover', device)
                    break
                }
            }
            
            if (!this.hoveredDevice) {
                this.$refs.flowCanvas.style.cursor = 'default'
                this.$emit('deviceHover', null)
            }
        },
        
        handleMouseLeave() {
            this.hoveredDevice = null
            this.$refs.flowCanvas.style.cursor = 'default'
            this.$emit('deviceHover', null)
        },
        
        getCanvasCoords(e) {
            const rect = this.$refs.flowCanvas.getBoundingClientRect()
            return {
                x: e.clientX - rect.left,
                y: e.clientY - rect.top
            }
        },
        
        isPointInDevice(x, y, device) {
            return x >= device.x && x <= device.x + this.deviceWidth &&
                   y >= device.y && y <= device.y + this.deviceHeight
        },
        
        updateArrowPositions() {
            this.arrowPositions = []
            for (const pipe of this.pipes) {
                const fromDevice = this.displayDevices.find(d => d.deviceId === pipe.fromDeviceId)
                const toDevice = this.displayDevices.find(d => d.deviceId === pipe.toDeviceId)
                
                if (fromDevice && toDevice) {
                    const fromX = fromDevice.x + this.deviceWidth / 2
                    const fromY = fromDevice.y + this.deviceHeight / 2
                    const toX = toDevice.x + this.deviceWidth / 2
                    const toY = toDevice.y + this.deviceHeight / 2
                    
                    this.arrowPositions.push({
                        pipe,
                        fromX, fromY, toX, toY,
                        flowDirection: pipe.flowDirection
                    })
                }
            }
        },
        
        draw() {
            if (!this.ctx) return
            
            this.ctx.clearRect(0, 0, this.canvasWidth, this.canvasHeight)
            
            this.drawGrid()
            this.drawPipes()
            this.drawDevices()
        },
        
        drawGrid() {
            this.ctx.strokeStyle = 'rgba(116, 185, 255, 0.05)'
            this.ctx.lineWidth = 1
            
            const gridSize = 50
            for (let x = 0; x < this.canvasWidth; x += gridSize) {
                this.ctx.beginPath()
                this.ctx.moveTo(x, 0)
                this.ctx.lineTo(x, this.canvasHeight)
                this.ctx.stroke()
            }
            for (let y = 0; y < this.canvasHeight; y += gridSize) {
                this.ctx.beginPath()
                this.ctx.moveTo(0, y)
                this.ctx.lineTo(this.canvasWidth, y)
                this.ctx.stroke()
            }
        },
        
        drawPipes() {
            for (const arrow of this.arrowPositions) {
                this.drawPipe(arrow)
            }
        },
        
        drawPipe(arrow) {
            const { fromX, fromY, toX, toY, pipe, flowDirection } = arrow
            
            this.ctx.beginPath()
            this.ctx.moveTo(fromX, fromY)
            this.ctx.lineTo(toX, toY)
            this.ctx.strokeStyle = pipe.color || '#3498db'
            this.ctx.lineWidth = 4
            this.ctx.shadowColor = pipe.color || '#3498db'
            this.ctx.shadowBlur = 8
            this.ctx.stroke()
            this.ctx.shadowBlur = 0
            
            if (flowDirection !== 0) {
                this.drawFlowArrows(fromX, fromY, toX, toY, pipe.color, flowDirection)
            }
        },
        
        drawFlowArrows(fromX, fromY, toX, toY, color, direction) {
            const dx = toX - fromX
            const dy = toY - fromY
            const distance = Math.sqrt(dx * dx + dy * dy)
            const angle = Math.atan2(dy, dx)
            
            const arrowCount = Math.max(1, Math.floor(distance / 80))
            const offset = (this.animationFrame / 60) * (distance / arrowCount) * direction
            
            for (let i = 0; i < arrowCount; i++) {
                let t = (i + 0.5) / arrowCount
                t = (t * distance + offset) / distance
                if (t > 1) t -= 1
                if (t < 0) t += 1
                
                const arrowX = fromX + dx * t
                const arrowY = fromY + dy * t
                
                this.drawArrow(arrowX, arrowY, angle, color, direction)
            }
        },
        
        drawArrow(x, y, angle, color, direction) {
            this.ctx.save()
            this.ctx.translate(x, y)
            this.ctx.rotate(angle)
            
            if (direction < 0) {
                this.ctx.rotate(Math.PI)
            }
            
            this.ctx.beginPath()
            this.ctx.moveTo(8, 0)
            this.ctx.lineTo(-4, -5)
            this.ctx.lineTo(-4, 5)
            this.ctx.closePath()
            
            this.ctx.fillStyle = color
            this.ctx.shadowColor = color
            this.ctx.shadowBlur = 5
            this.ctx.fill()
            
            this.ctx.restore()
        },
        
        drawDevices() {
            for (const device of this.displayDevices) {
                this.drawDevice(device)
            }
        },
        
        drawDevice(device) {
            const isSelected = this.selectedDevice && this.selectedDevice.deviceId === device.deviceId
            const isHovered = this.hoveredDevice && this.hoveredDevice.deviceId === device.deviceId
            
            const glowIntensity = isSelected ? 20 : isHovered ? 12 : 8
            
            this.ctx.shadowColor = device.statusColor || '#95a5a6'
            this.ctx.shadowBlur = glowIntensity
            
            const gradient = this.ctx.createLinearGradient(
                device.x, device.y,
                device.x, device.y + this.deviceHeight
            )
            
            const baseColor = this.hexToRgb(device.statusColor || '#2c3e50')
            gradient.addColorStop(0, `rgba(${baseColor.r}, ${baseColor.g}, ${baseColor.b}, 0.9)`)
            gradient.addColorStop(1, `rgba(${baseColor.r * 0.7}, ${baseColor.g * 0.7}, ${baseColor.b * 0.7}, 0.9)`)
            
            this.ctx.fillStyle = gradient
            this.roundRect(device.x, device.y, this.deviceWidth, this.deviceHeight, 8)
            this.ctx.fill()
            
            this.ctx.strokeStyle = device.statusColor || '#95a5a6'
            this.ctx.lineWidth = isSelected || isHovered ? 3 : 2
            this.ctx.stroke()
            
            this.ctx.shadowBlur = 0
            
            this.ctx.font = '20px Arial'
            this.ctx.textAlign = 'center'
            this.ctx.textBaseline = 'middle'
            const icon = this.deviceIcons[device.deviceTypeId] || '⚙️'
            this.ctx.fillText(icon, device.x + this.deviceWidth / 2, device.y + this.deviceHeight / 3)
            
            this.ctx.font = '10px Microsoft YaHei'
            this.ctx.fillStyle = '#fff'
            this.ctx.fillText(
                device.deviceName.replace(/[#\d]/g, '').substring(0, 4),
                device.x + this.deviceWidth / 2,
                device.y + this.deviceHeight * 0.75
            )
            
            this.ctx.font = '9px Consolas'
            this.ctx.fillStyle = 'rgba(255, 255, 255, 0.7)'
            this.ctx.fillText(
                device.deviceCode,
                device.x + this.deviceWidth / 2,
                device.y + this.deviceHeight - 3
            )
            
            if (device.currentPower !== undefined && device.currentPower !== null) {
                this.ctx.font = 'bold 11px Consolas'
                this.ctx.fillStyle = '#fdcb6e'
                this.ctx.fillText(
                    `${device.currentPower.toFixed(0)}kW`,
                    device.x + this.deviceWidth / 2,
                    device.y + this.deviceHeight + 12
                )
            }
            
            if (device.status === 0) {
                this.ctx.fillStyle = 'rgba(0, 0, 0, 0.6)'
                this.roundRect(device.x, device.y, this.deviceWidth, this.deviceHeight, 8)
                this.ctx.fill()
                
                this.ctx.font = 'bold 12px Microsoft YaHei'
                this.ctx.fillStyle = '#95a5a6'
                this.ctx.fillText('已停机', device.x + this.deviceWidth / 2, device.y + this.deviceHeight / 2)
            }
            
            if (device.status === -1) {
                this.ctx.fillStyle = 'rgba(231, 76, 60, 0.3)'
                this.roundRect(device.x, device.y, this.deviceWidth, this.deviceHeight, 8)
                this.ctx.fill()
            }
            
            if (isSelected) {
                this.ctx.strokeStyle = '#fff'
                this.ctx.lineWidth = 2
                this.ctx.setLineDash([5, 5])
                this.roundRect(device.x - 4, device.y - 4, this.deviceWidth + 8, this.deviceHeight + 8, 10)
                this.ctx.stroke()
                this.ctx.setLineDash([])
            }
        },
        
        roundRect(x, y, width, height, radius) {
            this.ctx.beginPath()
            this.ctx.moveTo(x + radius, y)
            this.ctx.lineTo(x + width - radius, y)
            this.ctx.quadraticCurveTo(x + width, y, x + width, y + radius)
            this.ctx.lineTo(x + width, y + height - radius)
            this.ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height)
            this.ctx.lineTo(x + radius, y + height)
            this.ctx.quadraticCurveTo(x, y + height, x, y + height - radius)
            this.ctx.lineTo(x, y + radius)
            this.ctx.quadraticCurveTo(x, y, x + radius, y)
            this.ctx.closePath()
        },
        
        hexToRgb(hex) {
            const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex)
            return result ? {
                r: parseInt(result[1], 1),
                g: parseInt(result[2], 1),
                b: parseInt(result[3], 1)
            } : { r: 44, g: 62, b: 80 }
        },
        
        clearSelection() {
            this.selectedDevice = null
        }
    },
    template: `
        <div class="chiller-flow-container">
            <div class="panel-title">
                <span>🔧 冷站系统流程图</span>
                <div class="legend">
                    <span class="legend-item"><span class="legend-color efficient"></span>高效</span>
                    <span class="legend-item"><span class="legend-color normal"></span>正常</span>
                    <span class="legend-item"><span class="legend-color warning"></span>效率偏低</span>
                    <span class="legend-item"><span class="legend-color danger"></span>故障/低效</span>
                </div>
            </div>
            <div class="canvas-container">
                <canvas 
                    ref="flowCanvas" 
                    :width="canvasWidth" 
                    :height="canvasHeight"
                    @click="handleCanvasClick"
                    @mousemove="handleMouseMove"
                    @mouseleave="handleMouseLeave"
                ></canvas>
            </div>
        </div>
    `
}
