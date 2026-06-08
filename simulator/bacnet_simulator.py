#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
智能建筑中央空调冷站 BACnet/IP 设备模拟器
模拟37台设备每30秒上报运行数据
"""

import asyncio
import json
import random
import time
import logging
from datetime import datetime, timezone
from typing import Dict, List, Optional
import aiohttp

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger('BACnetSimulator')

DEVICE_CONFIG = {
    'centrifugal_chillers': {
        'count': 3,
        'prefix': 'CEN-CH',
        'rated_power': 800,
        'design_cop': 5.8,
        'base_values': {
            'supply_temp': 7.0,
            'return_temp': 12.0,
            'flow_rate': 450,
            'pressure': 0.6,
            'current': 150,
            'voltage': 380,
            'frequency': 50
        }
    },
    'screw_chillers': {
        'count': 2,
        'prefix': 'SCR-CH',
        'rated_power': 500,
        'design_cop': 5.2,
        'base_values': {
            'supply_temp': 7.0,
            'return_temp': 12.0,
            'flow_rate': 280,
            'pressure': 0.55,
            'current': 95,
            'voltage': 380,
            'frequency': 50
        }
    },
    'cooling_towers': {
        'count': 8,
        'prefix': 'CT',
        'rated_power': 75,
        'design_cop': 35.0,
        'base_values': {
            'inlet_temp': 32.0,
            'outlet_temp': 28.0,
            'flow_rate': 500,
            'fan_speed': 85,
            'power': 55
        }
    },
    'chilled_water_pumps': {
        'count': 12,
        'prefix': 'CHP',
        'rated_power': 90,
        'design_cop': 20.0,
        'base_values': {
            'supply_temp': 7.0,
            'return_temp': 12.0,
            'flow_rate': 400,
            'pressure': 0.5,
            'current': 170,
            'voltage': 380,
            'frequency': 48
        }
    },
    'cooling_water_pumps': {
        'count': 12,
        'prefix': 'CWP',
        'rated_power': 75,
        'design_cop': 25.0,
        'base_values': {
            'supply_temp': 28.0,
            'return_temp': 32.0,
            'flow_rate': 450,
            'pressure': 0.45,
            'current': 140,
            'voltage': 380,
            'frequency': 48
        }
    }
}


class BACnetDeviceSimulator:
    def __init__(self, api_base_url: str = 'http://localhost:5000'):
        self.api_base_url = api_base_url
        self.devices: List[Dict] = []
        self.running = False
        self._init_devices()

    def _init_devices(self):
        """初始化所有模拟设备"""
        device_id = 1

        for device_type, config in DEVICE_CONFIG.items():
            for i in range(1, config['count'] + 1):
                device_id_str = f"{config['prefix']}-{i:03d}"

                initial_status = 1 if i <= config['count'] * 0.7 else 0

                base_x = {
                    'centrifugal_chillers': 200,
                    'screw_chillers': 350,
                    'cooling_towers': 1050,
                    'chilled_water_pumps': 80,
                    'cooling_water_pumps': 850
                }[device_type]

                base_y = {
                    'centrifugal_chillers': 200,
                    'screw_chillers': 500,
                    'cooling_towers': 400,
                    'chilled_water_pumps': 400,
                    'cooling_water_pumps': 400
                }[device_type]

                x_offset = (i - 1) % 4 * 80
                y_offset = (i - 1) // 4 * 100

                device = {
                    'id': device_id_str,
                    'name': device_id_str,
                    'device_type': device_type,
                    'device_type_id': {
                        'centrifugal_chillers': 1,
                        'screw_chillers': 2,
                        'cooling_towers': 3,
                        'chilled_water_pumps': 4,
                        'cooling_water_pumps': 5
                    }[device_type],
                    'design_cop': config['design_cop'],
                    'rated_power': config['rated_power'],
                    'status': initial_status,
                    'efficiency_status': 1,
                    'position_x': base_x + x_offset,
                    'position_y': base_y + y_offset,
                    'operating_hours': random.uniform(0, 5000),
                    'base_values': config['base_values'],
                    'last_values': {},
                    'fault_probability': 0.001,
                    'low_efficiency_probability': 0.005
                }

                for param, base_val in config['base_values'].items():
                    device['last_values'][param] = base_val * (
                        0.9 + random.random() * 0.2
                    )

                self.devices.append(device)
                device_id += 1

        logger.info(f"已初始化 {len(self.devices)} 台模拟设备")

    def _update_device_values(self, device: Dict):
        """更新设备模拟数据"""
        if device['status'] == 2:
            if random.random() < 0.05:
                device['status'] = 0
                device['efficiency_status'] = 1
                logger.info(f"设备 {device['id']} 故障恢复")
            return

        if device['status'] == 0 and random.random() < 0.1:
            device['status'] = 1
            logger.info(f"设备 {device['id']} 启动")
        elif device['status'] == 1 and random.random() < 0.02:
            device['status'] = 0
            logger.info(f"设备 {device['id']} 停机")

        if random.random() < device['fault_probability'] and device['status'] == 1:
            device['status'] = 2
            device['efficiency_status'] = 4
            logger.warning(f"设备 {device['id']} 模拟故障触发")
            return

        if random.random() < device['low_efficiency_probability'] and device['status'] == 1:
            device['efficiency_status'] = 3
            logger.info(f"设备 {device['id']} 进入低效状态")
        elif device['efficiency_status'] == 3 and random.random() < 0.3:
            device['efficiency_status'] = 1
            logger.info(f"设备 {device['id']} 恢复高效状态")

        if device['status'] == 1:
            load_factor = 0.6 + random.random() * 0.4

            for param, base_val in device['base_values'].items():
                drift = random.uniform(-0.03, 0.03)
                last_val = device['last_values'].get(param, base_val)

                if device['efficiency_status'] == 3:
                    drift += random.uniform(-0.05, 0.1)
                    if 'temp' in param and 'supply' in param:
                        drift += 0.02

                new_val = last_val + (base_val * load_factor - last_val) * 0.3 + base_val * drift
                min_val = base_val * 0.5
                max_val = base_val * 1.5

                if device['efficiency_status'] == 4:
                    new_val = new_val * (0.3 + random.random() * 0.4)

                new_val = max(min_val, min(max_val, new_val))
                device['last_values'][param] = new_val

            if 'power' not in device['last_values']:
                power_factor = load_factor * (0.9 + random.random() * 0.2)
                if device['efficiency_status'] == 3:
                    power_factor *= 1.15
                elif device['efficiency_status'] == 4:
                    power_factor *= 0.5
                device['last_values']['power'] = device['rated_power'] * power_factor

            if device['efficiency_status'] == 1 and random.random() < 0.7:
                device['efficiency_status'] = 2

            device['operating_hours'] += 30 / 3600
        else:
            for param in device['last_values']:
                base_val = device['base_values'].get(param, 0)
                device['last_values'][param] = base_val * (0.01 + random.random() * 0.02)
            device['last_values']['power'] = device['rated_power'] * (0.01 + random.random() * 0.02)
            device['efficiency_status'] = 1

    def _generate_device_data(self, device: Dict, timestamp: datetime) -> Dict:
        """生成设备数据上报格式"""
        values = device['last_values']

        base_data = {
            'deviceId': device['id'],
            'timestamp': timestamp.isoformat(),
            'power': round(values.get('power', 0), 2),
            'supplyTemperature': round(values.get('supply_temp', values.get('outlet_temp', 0)), 2),
            'returnTemperature': round(values.get('return_temp', values.get('inlet_temp', 0)), 2),
            'pressure': round(values.get('pressure', 0), 3),
            'flowRate': round(values.get('flow_rate', 0), 2)
        }

        device_type = device['device_type']

        if device_type in ['centrifugal_chillers', 'screw_chillers', 'chilled_water_pumps', 'cooling_water_pumps']:
            base_data.update({
                'frequency': round(values.get('frequency', 0), 1),
                'current': round(values.get('current', 0), 1),
                'voltage': round(values.get('voltage', 0), 0)
            })

        if device_type == 'cooling_towers':
            base_data.update({
                'inletTemperature': round(values.get('inlet_temp', 0), 2),
                'outletTemperature': round(values.get('outlet_temp', 0), 2),
                'fanSpeed': round(values.get('fan_speed', 0), 1)
            })

        return base_data

    async def _send_device_data(self, session: aiohttp.ClientSession, device: Dict, data: Dict):
        """发送设备数据到后端API"""
        try:
            url = f"{self.api_base_url}/api/device/{device['id']}/data"
            async with session.post(url, json=data, timeout=10) as response:
                if response.status == 201:
                    logger.debug(f"设备 {device['id']} 数据上报成功")
                    return True
                else:
                    logger.warning(f"设备 {device['id']} 上报失败: {response.status}")
                    return False
        except Exception as e:
            logger.error(f"设备 {device['id']} 上报异常: {e}")
            return False

    async def _update_device_status(self, session: aiohttp.ClientSession, device: Dict):
        """更新设备状态到后端"""
        try:
            url = f"{self.api_base_url}/api/device/{device['id']}/status/{device['status']}"
            async with session.put(url, timeout=10) as response:
                return response.status == 204
        except Exception as e:
            logger.error(f"更新设备 {device['id']} 状态失败: {e}")
            return False

    async def _collect_and_send(self, session: aiohttp.ClientSession):
        """采集并发送所有设备数据"""
        timestamp = datetime.now(timezone.utc)
        logger.info(f"开始采集周期 - {timestamp.isoformat()}")

        tasks = []
        running_count = 0
        fault_count = 0

        for device in self.devices:
            self._update_device_values(device)

            if device['status'] == 1:
                running_count += 1
            elif device['status'] == 2:
                fault_count += 1

            data = self._generate_device_data(device, timestamp)

            task = self._send_device_data(session, device, data)
            tasks.append(task)

            if device['status'] != 1:
                status_task = self._update_device_status(session, device)
                tasks.append(status_task)

        results = await asyncio.gather(*tasks, return_exceptions=True)
        success_count = sum(1 for r in results if r is True)

        logger.info(
            f"采集周期完成 - 运行: {running_count}, "
            f"故障: {fault_count}, 上报成功: {success_count}/{len(tasks)}"
        )

        return {
            'timestamp': timestamp.isoformat(),
            'running': running_count,
            'fault': fault_count,
            'success': success_count,
            'total': len(tasks)
        }

    async def _register_devices(self, session: aiohttp.ClientSession):
        """检查并注册设备（如果后端需要）"""
        logger.info("检查设备注册状态...")
        try:
            url = f"{self.api_base_url}/api/device"
            async with session.get(url, timeout=10) as response:
                if response.status == 200:
                    existing_devices = await response.json()
                    existing_ids = {d['id'] for d in existing_devices}
                    logger.info(f"后端已有 {len(existing_ids)} 台设备")
                    return existing_ids
        except Exception as e:
            logger.warning(f"获取设备列表失败: {e}")
        return set()

    async def _check_api_connection(self, session: aiohttp.ClientSession) -> bool:
        """检查API连接"""
        try:
            url = f"{self.api_base_url}/api/system/health"
            async with session.get(url, timeout=5) as response:
                return response.status == 200
        except:
            return False

    async def start(self, collection_interval: int = 30):
        """启动模拟器"""
        self.running = True
        logger.info(f"BACnet模拟器启动，采集间隔: {collection_interval}秒")
        logger.info(f"后端API地址: {self.api_base_url}")

        async with aiohttp.ClientSession() as session:
            logger.info("等待后端API就绪...")
            for i in range(60):
                if await self._check_api_connection(session):
                    logger.info("后端API连接成功")
                    break
                if i % 10 == 9:
                    logger.info(f"等待后端API... ({i + 1}/60)")
                await asyncio.sleep(1)
            else:
                logger.warning("后端API连接超时，将继续尝试发送数据")

            await self._register_devices(session)

            cycle_count = 0
            while self.running:
                try:
                    await self._collect_and_send(session)
                    cycle_count += 1

                    if cycle_count % 10 == 0:
                        await self._register_devices(session)

                except Exception as e:
                    logger.error(f"采集周期异常: {e}")

                await asyncio.sleep(collection_interval)

    def stop(self):
        """停止模拟器"""
        self.running = False
        logger.info("BACnet模拟器停止中...")


async def main():
    import argparse

    parser = argparse.ArgumentParser(description='BACnet/IP 设备模拟器')
    parser.add_argument(
        '--api-url',
        type=str,
        default='http://localhost:5000',
        help='后端API地址 (默认: http://localhost:5000)'
    )
    parser.add_argument(
        '--interval',
        type=int,
        default=30,
        help='数据采集间隔秒数 (默认: 30)'
    )
    parser.add_argument(
        '--log-level',
        type=str,
        default='INFO',
        choices=['DEBUG', 'INFO', 'WARNING', 'ERROR'],
        help='日志级别 (默认: INFO)'
    )

    args = parser.parse_args()

    logging.getLogger().setLevel(getattr(logging, args.log_level))

    simulator = BACnetDeviceSimulator(api_base_url=args.api_url)

    try:
        await simulator.start(collection_interval=args.interval)
    except KeyboardInterrupt:
        logger.info("收到停止信号")
        simulator.stop()
    except Exception as e:
        logger.error(f"模拟器异常退出: {e}")
        raise


if __name__ == '__main__':
    print("""
╔══════════════════════════════════════════════════════════════╗
║          智能建筑中央空调冷站 BACnet/IP 模拟器              ║
╠══════════════════════════════════════════════════════════════╣
║  设备配置:                                                  ║
║    - 离心式冷水机组: 3台                                    ║
║    - 螺杆式冷水机组: 2台                                    ║
║    - 冷却塔: 8台                                            ║
║    - 冷冻水泵: 12台                                         ║
║    - 冷却水泵: 12台                                         ║
║  总计: 37台设备                                             ║
║                                                             ║
║  采集间隔: 30秒                                             ║
║  数据点: 功率、温度、压力、流量、电流、电压、频率等         ║
╚══════════════════════════════════════════════════════════════╝
    """)
    asyncio.run(main())
