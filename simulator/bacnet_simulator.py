#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
智能建筑中央空调冷站 BACnet/IP 设备模拟器
模拟37台设备每30秒上报运行数据
支持配置文件驱动的设备性能曲线
"""

import asyncio
import json
import random
import time
import logging
import os
from datetime import datetime, timezone
from typing import Dict, List, Optional
import aiohttp

logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger('BACnetSimulator')


class PerformanceCurve:
    def __init__(self, curve_config: Dict):
        self.config = curve_config

    def calculate_cop(self, load_rate: float) -> float:
        coeffs = self.config.get('coefficients', {'a': 0, 'b': 0, 'c': 0})
        a, b, c = coeffs['a'], coeffs['b'], coeffs['c']
        cop = a * load_rate * load_rate + b * load_rate + c

        part_load = self.config.get('part_load_efficiency', {})
        if part_load:
            load_key = str(round(load_rate, 1))
            if load_key in part_load:
                cop *= part_load[load_key]
            else:
                keys = sorted([float(k) for k in part_load.keys()])
                lower = max([k for k in keys if k <= load_rate], default=keys[0])
                upper = min([k for k in keys if k >= load_rate], default=keys[-1])
                if lower != upper:
                    ratio = (load_rate - lower) / (upper - lower)
                    eff_lower = part_load[str(lower)]
                    eff_upper = part_load[str(upper)]
                    cop *= eff_lower + ratio * (eff_upper - eff_lower)

        return max(0.5, min(10.0, cop))

    def get_optimal_load_range(self) -> tuple:
        return tuple(self.config.get('optimal_load_range', [0.4, 0.85]))


class BACnetDeviceSimulator:
    def __init__(self, config_path: str = 'config.json', api_base_url: str = None):
        self.config = self._load_config(config_path)
        self.api_base_url = api_base_url or self.config.get('api_url', 'http://localhost:5000')
        self.collection_interval = self.config.get('collection_interval_seconds', 30)
        self.log_level = self.config.get('log_level', 'INFO')

        self.devices: List[Dict] = []
        self.running = False
        self.performance_curves: Dict[str, PerformanceCurve] = {}

        self._init_performance_curves()
        self._init_devices()

    def _load_config(self, config_path: str) -> Dict:
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                config = json.load(f)
            logger.info(f"配置文件已加载: {config_path}")
            return config
        except Exception as e:
            logger.warning(f"无法加载配置文件 {config_path}: {e}，使用默认配置")
            return self._get_default_config()

    def _get_default_config(self) -> Dict:
        return {
            "api_url": "http://localhost:5000",
            "collection_interval_seconds": 30,
            "log_level": "INFO",
            "device_performance_curves": {},
            "devices": {
                "centrifugal_chillers": {"count": 3, "prefix": "CEN-CH", "rated_power_kw": 800, "design_cop": 5.8},
                "screw_chillers": {"count": 2, "prefix": "SCR-CH", "rated_power_kw": 500, "design_cop": 5.2},
                "cooling_towers": {"count": 8, "prefix": "CT", "rated_power_kw": 75, "design_cop": 35.0},
                "chilled_water_pumps": {"count": 12, "prefix": "CHP", "rated_power_kw": 90, "design_cop": 20.0},
                "cooling_water_pumps": {"count": 12, "prefix": "CWP", "rated_power_kw": 75, "design_cop": 25.0}
            }
        }

    def _init_performance_curves(self):
        curves = self.config.get('device_performance_curves', {})
        for device_type, curve_config in curves.items():
            self.performance_curves[device_type] = PerformanceCurve(curve_config)
            logger.info(f"已加载性能曲线: {device_type}")

    def _get_current_load_factor(self) -> float:
        scenarios = self.config.get('scenarios', {})
        if 'normal' not in scenarios:
            return 0.7

        load_profile = scenarios['normal'].get('load_profile', [])
        if not load_profile:
            return 0.7

        current_hour = datetime.now().hour + datetime.now().minute / 60

        load_profile.sort(key=lambda x: x['hour'])

        for i in range(len(load_profile)):
            if load_profile[i]['hour'] >= current_hour:
                if i == 0:
                    return load_profile[0]['load']
                prev = load_profile[i - 1]
                curr = load_profile[i]
                ratio = (current_hour - prev['hour']) / (curr['hour'] - prev['hour'])
                return prev['load'] + ratio * (curr['load'] - prev['load'])

        return load_profile[-1]['load']

    def _init_devices(self):
        device_id = 1
        devices_config = self.config.get('devices', {})
        fault_config = self.config.get('fault_simulation', {})

        device_type_map = {
            'centrifugal_chillers': ('centrifugal_chiller', 1),
            'screw_chillers': ('screw_chiller', 2),
            'cooling_towers': ('cooling_tower', 3),
            'chilled_water_pumps': ('chilled_water_pump', 4),
            'cooling_water_pumps': ('cooling_water_pump', 5)
        }

        for config_key, device_config in devices_config.items():
            curve_type, type_id = device_type_map.get(config_key, (config_key, 0))
            count = device_config.get('count', 0)
            prefix = device_config.get('prefix', 'DEV')
            rated_power = device_config.get('rated_power_kw', 100)
            design_cop = device_config.get('design_cop', 5.0)
            base_values = device_config.get('base_values', {})
            position_layout = device_config.get('position_layout', {
                'base_x': 400, 'base_y': 400, 'x_spacing': 80, 'y_spacing': 100, 'per_row': 4
            })
            perf_factor = device_config.get('performance_factor', 1.0)

            for i in range(1, count + 1):
                device_id_str = f"{prefix}-{i:03d}"
                initial_status = 1 if i <= count * 0.7 else 0

                x_offset = (i - 1) % position_layout['per_row'] * position_layout['x_spacing']
                y_offset = (i - 1) // position_layout['per_row'] * position_layout['y_spacing']

                device = {
                    'id': device_id_str,
                    'name': device_id_str,
                    'device_type': config_key,
                    'curve_type': curve_type,
                    'device_type_id': type_id,
                    'design_cop': design_cop,
                    'rated_power': rated_power,
                    'capacity_kw': device_config.get('capacity_kw', rated_power * design_cop),
                    'performance_factor': perf_factor,
                    'status': initial_status,
                    'efficiency_status': 1,
                    'position_x': position_layout['base_x'] + x_offset,
                    'position_y': position_layout['base_y'] + y_offset,
                    'operating_hours': random.uniform(0, 5000),
                    'base_values': base_values,
                    'last_values': {},
                    'fault_probability': fault_config.get('fault_probability', 0.001),
                    'low_efficiency_probability': fault_config.get('low_efficiency_probability', 0.005),
                    'recovery_probability': fault_config.get('recovery_probability', 0.05),
                    'current_cop': design_cop * 0.8
                }

                for param, base_val in base_values.items():
                    device['last_values'][param] = base_val * (0.9 + random.random() * 0.2)

                if 'power' not in device['last_values']:
                    device['last_values']['power'] = rated_power * 0.5

                self.devices.append(device)
                device_id += 1

        total_count = len(self.devices)
        logger.info(f"已初始化 {total_count} 台模拟设备")
        self._print_device_summary()

    def _print_device_summary(self):
        summary = {}
        for device in self.devices:
            dtype = device['device_type']
            summary[dtype] = summary.get(dtype, 0) + 1
        for dtype, count in summary.items():
            logger.info(f"  {dtype}: {count}台")

    def _calculate_device_cop(self, device: Dict, load_factor: float) -> float:
        curve_type = device.get('curve_type')
        if curve_type in self.performance_curves:
            curve = self.performance_curves[curve_type]
            base_cop = curve.calculate_cop(load_factor)
            perf_factor = device.get('performance_factor', 1.0)
            return base_cop * perf_factor
        return device['design_cop'] * (0.8 + 0.4 * load_factor)

    def _update_device_values(self, device: Dict):
        if device['status'] == 2:
            if random.random() < device['recovery_probability']:
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
            base_load = self._get_current_load_factor()
            load_factor = max(0.2, min(1.0, base_load * (0.95 + random.random() * 0.1)))

            device['current_cop'] = self._calculate_device_cop(device, load_factor)

            for param, base_val in device['base_values'].items():
                drift = random.uniform(-0.03, 0.03)
                last_val = device['last_values'].get(param, base_val)

                if device['efficiency_status'] == 3:
                    drift += random.uniform(-0.05, 0.1)
                    if 'temp' in param and 'supply' in param:
                        drift += 0.02

                target_val = base_val * load_factor
                new_val = last_val + (target_val - last_val) * 0.3 + base_val * drift
                min_val = base_val * 0.5
                max_val = base_val * 1.5

                if device['efficiency_status'] == 4:
                    new_val = new_val * (0.3 + random.random() * 0.4)

                new_val = max(min_val, min(max_val, new_val))
                device['last_values'][param] = new_val

            if 'power' not in device['base_values']:
                power_factor = load_factor * (0.9 + random.random() * 0.2)
                if device['efficiency_status'] == 3:
                    power_factor *= 1.15
                elif device['efficiency_status'] == 4:
                    power_factor *= 0.5
                device['last_values']['power'] = device['rated_power'] * power_factor

            if device['efficiency_status'] == 1 and random.random() < 0.7:
                device['efficiency_status'] = 2

            device['operating_hours'] += self.collection_interval / 3600
        else:
            for param in device['last_values']:
                base_val = device['base_values'].get(param, 0)
                device['last_values'][param] = base_val * (0.01 + random.random() * 0.02)
            device['last_values']['power'] = device['rated_power'] * (0.01 + random.random() * 0.02)
            device['efficiency_status'] = 1
            device['current_cop'] = 0

    def _generate_device_data(self, device: Dict, timestamp: datetime) -> Dict:
        values = device['last_values']

        base_data = {
            'deviceId': device['id'],
            'timestamp': timestamp.isoformat(),
            'power': round(values.get('power', 0), 2),
            'supplyTemperature': round(values.get('supply_temp', values.get('outlet_temp', 0)), 2),
            'returnTemperature': round(values.get('return_temp', values.get('inlet_temp', 0)), 2),
            'pressure': round(values.get('pressure', 0), 3),
            'flowRate': round(values.get('flow_rate', 0), 2),
            'cop': round(device.get('current_cop', 0), 2),
            'loadRate': round(self._get_current_load_factor(), 3)
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
        try:
            url = f"{self.api_base_url}/api/device/{device['id']}/status/{device['status']}"
            async with session.put(url, timeout=10) as response:
                return response.status == 204
        except Exception as e:
            logger.error(f"更新设备 {device['id']} 状态失败: {e}")
            return False

    async def _collect_and_send(self, session: aiohttp.ClientSession):
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

        avg_cop = 0
        running_devices = [d for d in self.devices if d['status'] == 1]
        if running_devices:
            avg_cop = sum(d.get('current_cop', 0) for d in running_devices) / len(running_devices)

        logger.info(
            f"采集周期完成 - 运行: {running_count}, "
            f"故障: {fault_count}, 平均COP: {avg_cop:.2f}, "
            f"上报成功: {success_count}/{len(tasks)}"
        )

        return {
            'timestamp': timestamp.isoformat(),
            'running': running_count,
            'fault': fault_count,
            'avg_cop': avg_cop,
            'success': success_count,
            'total': len(tasks)
        }

    async def _register_devices(self, session: aiohttp.ClientSession):
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
        try:
            url = f"{self.api_base_url}/health"
            async with session.get(url, timeout=5) as response:
                return response.status == 200
        except:
            return False

    async def start(self, collection_interval: int = None):
        interval = collection_interval or self.collection_interval
        self.running = True
        logger.info(f"BACnet模拟器启动，采集间隔: {interval}秒")
        logger.info(f"后端API地址: {self.api_base_url}")
        logger.info(f"性能曲线数量: {len(self.performance_curves)}")

        async with aiohttp.ClientSession() as session:
            logger.info("等待后端API就绪...")
            for i in range(120):
                if await self._check_api_connection(session):
                    logger.info("后端API连接成功")
                    break
                if i % 10 == 9:
                    logger.info(f"等待后端API... ({i + 1}/120)")
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

                await asyncio.sleep(interval)

    def stop(self):
        self.running = False
        logger.info("BACnet模拟器停止中...")


async def main():
    import argparse

    parser = argparse.ArgumentParser(description='BACnet/IP 设备模拟器')
    parser.add_argument(
        '--config',
        type=str,
        default=os.environ.get('SIMULATOR_CONFIG', 'config.json'),
        help='配置文件路径 (默认: config.json)'
    )
    parser.add_argument(
        '--api-url',
        type=str,
        default=os.environ.get('API_URL', None),
        help='后端API地址 (默认从配置文件读取)'
    )
    parser.add_argument(
        '--interval',
        type=int,
        default=None,
        help='数据采集间隔秒数 (默认从配置文件读取)'
    )
    parser.add_argument(
        '--log-level',
        type=str,
        default=None,
        choices=['DEBUG', 'INFO', 'WARNING', 'ERROR'],
        help='日志级别 (默认从配置文件读取)'
    )

    args = parser.parse_args()

    simulator = BACnetDeviceSimulator(config_path=args.config, api_base_url=args.api_url)

    if args.log_level:
        logging.getLogger().setLevel(getattr(logging, args.log_level))

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
║  特性:                                                      ║
║    - 可配置设备性能曲线                                     ║
║    - 负荷率动态曲线                                         ║
║    - 故障和低效模拟                                         ║
║    - 30秒采集间隔 (可配置)                                  ║
╚══════════════════════════════════════════════════════════════╝
    """)
    asyncio.run(main())
