import requests
import time
import random
import json
import threading
from datetime import datetime
from typing import List, Dict

API_BASE_URL = "http://localhost:5000/api"
REPORT_INTERVAL = 30

DEVICE_CONFIGS = [
    {"bacnetInstance": 10001, "type": "centrifugal_chiller", "name": "1#离心冷水机组", "ratedPower": 800, "ratedCapacity": 5200},
    {"bacnetInstance": 10002, "type": "centrifugal_chiller", "name": "2#离心冷水机组", "ratedPower": 800, "ratedCapacity": 5200},
    {"bacnetInstance": 10003, "type": "centrifugal_chiller", "name": "3#离心冷水机组", "ratedPower": 800, "ratedCapacity": 5200},
    {"bacnetInstance": 10004, "type": "screw_chiller", "name": "4#螺杆冷水机组", "ratedPower": 450, "ratedCapacity": 2610},
    {"bacnetInstance": 10005, "type": "screw_chiller", "name": "5#螺杆冷水机组", "ratedPower": 450, "ratedCapacity": 2610},
] + [
    {"bacnetInstance": 20000 + i, "type": "cooling_tower", "name": f"{i}#冷却塔", "ratedPower": 15} 
    for i in range(1, 9)
] + [
    {"bacnetInstance": 30000 + i, "type": "chiller_pump", "name": f"{i}#冷冻水泵", "ratedPower": 90} 
    for i in range(1, 13)
] + [
    {"bacnetInstance": 40000 + i, "type": "cooling_pump", "name": f"{i}#冷却水泵", "ratedPower": 75} 
    for i in range(1, 13)
]

class BACnetDeviceSimulator:
    def __init__(self, config: Dict):
        self.config = config
        self.bacnet_instance = config["bacnetInstance"]
        self.device_type = config["type"]
        self.name = config["name"]
        self.rated_power = config["ratedPower"]
        self.rated_capacity = config.get("ratedCapacity", 0)
        self.status = 1
        self.running_hours = random.randint(1000, 5000)
        self.load_rate = 0.0
        self.base_load = 60.0 + random.uniform(-10, 10)
        self._initialize_values()

    def _initialize_values(self):
        if self.device_type in ["centrifugal_chiller", "screw_chiller"]:
            self.supply_water_temp = 6.5 + random.uniform(-0.5, 0.5)
            self.return_water_temp = 12.0 + random.uniform(-1, 1)
            self.cooling_water_in_temp = 28.0 + random.uniform(-2, 2)
            self.cooling_water_out_temp = 33.0 + random.uniform(-2, 2)
            self.flow_rate = 350 + random.uniform(-30, 30)
            self.vibration = 2.5 + random.uniform(-0.5, 0.5)
            self.current = 800 + random.uniform(-100, 100)
        elif self.device_type == "cooling_tower":
            self.vibration = 2.0 + random.uniform(-0.5, 0.5)
            self.current = 20 + random.uniform(-5, 5)
            self.frequency = 50 + random.uniform(-2, 2)
        elif self.device_type in ["chiller_pump", "cooling_pump"]:
            self.supply_pressure = 1.0 + random.uniform(-0.1, 0.1)
            self.return_pressure = 0.3 + random.uniform(-0.05, 0.05)
            self.flow_rate = 300 + random.uniform(-30, 30)
            self.frequency = 50 + random.uniform(-5, 5)
            self.vibration = 2.0 + random.uniform(-0.5, 0.5)
            self.current = 100 + random.uniform(-20, 20)
        
        self.voltage = 380 + random.uniform(-5, 5)

    def _update_values(self, system_load: float):
        hour = datetime.now().hour
        time_factor = 1.0
        
        if 8 <= hour <= 18:
            time_factor = 1.2
        elif 18 < hour <= 22:
            time_factor = 1.0
        elif 0 <= hour < 6:
            time_factor = 0.6
        else:
            time_factor = 0.8
        
        target_load = system_load * time_factor
        
        if self.status == 1:
            self.load_rate = target_load + random.uniform(-5, 5)
            self.load_rate = max(5, min(100, self.load_rate))
            self.running_hours += REPORT_INTERVAL / 3600
        else:
            self.load_rate = 0

        load_factor = max(0.1, self.load_rate / 100.0) if self.status == 1 else 0.05
        power_factor = 0.85 + random.uniform(-0.05, 0.05)

        if self.device_type in ["centrifugal_chiller", "screw_chiller"]:
            if self.status == 1:
                self.supply_water_temp += random.uniform(-0.05, 0.05)
                self.supply_water_temp = max(5.0, min(8.0, self.supply_water_temp))
                self.return_water_temp = self.supply_water_temp + 5.5 + random.uniform(-0.5, 0.5)
                self.cooling_water_in_temp = 28.0 + random.uniform(-1, 1)
                self.cooling_water_out_temp = self.cooling_water_in_temp + 5.0 + random.uniform(-0.5, 0.5)
                self.flow_rate = 350 * load_factor + random.uniform(-10, 10)
                self.vibration = 2.5 * load_factor + random.uniform(-0.2, 0.2)
                self.current = 800 * load_factor + random.uniform(-50, 50)
                self.power = self.rated_power * load_factor * power_factor
            else:
                self.supply_water_temp = 25.0 + random.uniform(-1, 1)
                self.return_water_temp = 26.0 + random.uniform(-1, 1)
                self.power = 5.0

        elif self.device_type == "cooling_tower":
            if self.status == 1:
                self.frequency = 40 + 10 * load_factor + random.uniform(-2, 2)
                self.vibration = 2.0 * (0.5 + load_factor * 0.5) + random.uniform(-0.2, 0.2)
                self.current = 20 * (0.5 + load_factor * 0.5) + random.uniform(-3, 3)
                self.power = self.rated_power * (0.3 + load_factor * 0.7) * power_factor
            else:
                self.power = 0.5

        elif self.device_type in ["chiller_pump", "cooling_pump"]:
            if self.status == 1:
                self.frequency = 35 + 15 * load_factor + random.uniform(-2, 2)
                self.supply_pressure = 0.8 + 0.4 * load_factor + random.uniform(-0.05, 0.05)
                self.return_pressure = 0.2 + 0.2 * load_factor + random.uniform(-0.03, 0.03)
                self.flow_rate = 200 + 150 * load_factor + random.uniform(-10, 10)
                self.vibration = 2.0 * (0.5 + load_factor * 0.5) + random.uniform(-0.2, 0.2)
                self.current = 60 + 60 * load_factor + random.uniform(-10, 10)
                self.power = self.rated_power * (0.4 + load_factor * 0.6) * power_factor
            else:
                self.power = 0.3

        self.voltage = 380 + random.uniform(-3, 3)

    def generate_data(self, system_load: float) -> Dict:
        self._update_values(system_load)
        
        data = {
            "bacnetInstance": self.bacnet_instance,
            "power": round(max(0, self.power), 2),
            "runningHours": int(self.running_hours),
            "status": self.status,
            "timestamp": datetime.now().isoformat(),
            "loadRate": round(self.load_rate, 2) if self.status == 1 else 0,
            "frequency": round(getattr(self, "frequency", 0), 2),
            "vibration": round(getattr(self, "vibration", 0), 2),
            "current": round(getattr(self, "current", 0), 2),
            "voltage": round(self.voltage, 2),
        }

        if self.device_type in ["centrifugal_chiller", "screw_chiller"]:
            data.update({
                "supplyWaterTemp": round(self.supply_water_temp, 2),
                "returnWaterTemp": round(self.return_water_temp, 2),
                "coolingWaterInTemp": round(self.cooling_water_in_temp, 2),
                "coolingWaterOutTemp": round(self.cooling_water_out_temp, 2),
                "flowRate": round(self.flow_rate, 2),
            })
        elif self.device_type in ["chiller_pump", "cooling_pump"]:
            data.update({
                "supplyPressure": round(self.supply_pressure, 2),
                "returnPressure": round(self.return_pressure, 2),
                "flowRate": round(self.flow_rate, 2),
            })

        return data

    def set_status(self, status: int):
        self.status = status

class ChillerPlantSimulator:
    def __init__(self, api_url: str = API_BASE_URL, report_interval: int = REPORT_INTERVAL):
        self.api_url = api_url
        self.report_interval = report_interval
        self.devices = [BACnetDeviceSimulator(config) for config in DEVICE_CONFIGS]
        self.system_load = 65.0
        self.running = False
        self._optimize_device_combination()

    def _optimize_device_combination(self):
        for device in self.devices:
            if device.device_type in ["centrifugal_chiller", "screw_chiller"]:
                if self.system_load > 70:
                    device.set_status(1)
                elif self.system_load > 50:
                    if device.bacnet_instance in [10001, 10002, 10004]:
                        device.set_status(1)
                    else:
                        device.set_status(0)
                elif self.system_load > 30:
                    if device.bacnet_instance in [10001, 10004]:
                        device.set_status(1)
                    else:
                        device.set_status(0)
                else:
                    if device.bacnet_instance in [10004]:
                        device.set_status(1)
                    else:
                        device.set_status(0)
            elif device.device_type == "chiller_pump":
                running_chillers = sum(1 for d in self.devices 
                    if d.device_type in ["centrifugal_chiller", "screw_chiller"] and d.status == 1)
                pump_id = device.bacnet_instance - 30000
                device.set_status(1 if pump_id <= running_chillers * 2 + 2 else 0)
            elif device.device_type == "cooling_pump":
                running_chillers = sum(1 for d in self.devices 
                    if d.device_type in ["centrifugal_chiller", "screw_chiller"] and d.status == 1)
                pump_id = device.bacnet_instance - 40000
                device.set_status(1 if pump_id <= running_chillers * 2 + 2 else 0)
            elif device.device_type == "cooling_tower":
                running_chillers = sum(1 for d in self.devices 
                    if d.device_type in ["centrifugal_chiller", "screw_chiller"] and d.status == 1)
                tower_id = device.bacnet_instance - 20000
                device.set_status(1 if tower_id <= running_chillers * 2 else 0)

    def _update_system_load(self):
        hour = datetime.now().hour
        base_load = 60.0
        
        if 10 <= hour <= 16:
            base_load = 85.0
        elif 8 <= hour < 10 or 16 < hour <= 20:
            base_load = 70.0
        elif 20 < hour <= 24:
            base_load = 45.0
        elif 0 <= hour < 6:
            base_load = 25.0
        else:
            base_load = 35.0
        
        self.system_load = base_load + random.uniform(-5, 5)
        self.system_load = max(10, min(95, self.system_load))
        
        if random.random() < 0.05:
            self._optimize_device_combination()

    def send_data(self, data: Dict) -> bool:
        try:
            url = f"{self.api_url}/devices/data"
            headers = {"Content-Type": "application/json"}
            response = requests.post(url, json=data, headers=headers, timeout=5)
            if response.status_code == 200:
                return True
            else:
                print(f"Failed to send data for device {data['bacnetInstance']}: {response.status_code}")
                return False
        except Exception as e:
            print(f"Error sending data for device {data['bacnetInstance']}: {e}")
            return False

    def send_batch_data(self, data_list: List[Dict]) -> bool:
        try:
            url = f"{self.api_url}/devices/data/batch"
            headers = {"Content-Type": "application/json"}
            response = requests.post(url, json=data_list, headers=headers, timeout=10)
            if response.status_code == 200:
                return True
            else:
                print(f"Failed to send batch data: {response.status_code}")
                return False
        except Exception as e:
            print(f"Error sending batch data: {e}")
            return False

    def simulate_one_cycle(self):
        self._update_system_load()
        
        data_list = []
        for device in self.devices:
            data = device.generate_data(self.system_load)
            data_list.append(data)
            print(f"[{datetime.now().strftime('%H:%M:%S')}] {device.name}: "
                  f"Power={data['power']}kW, "
                  f"Load={data['loadRate']}%, "
                  f"Status={'运行' if data['status'] == 1 else '停机'}")
        
        if len(data_list) > 0:
            success = self.send_batch_data(data_list)
            if success:
                print(f"[{datetime.now().strftime('%H:%M:%S')}] 成功上报 {len(data_list)} 台设备数据")
        
        print("-" * 80)

    def start(self):
        print("=" * 80)
        print("BACnet/IP 设备模拟器启动")
        print(f"共 {len(self.devices)} 台设备: 3离心机+2螺杆机+8冷却塔+12冷冻泵+12冷却泵")
        print(f"上报间隔: {self.report_interval}秒")
        print(f"API地址: {self.api_url}")
        print("=" * 80)
        print()
        
        self.running = True
        cycle_count = 0
        
        while self.running:
            try:
                cycle_count += 1
                print(f"\n=== 第 {cycle_count} 轮数据上报 ===")
                self.simulate_one_cycle()
                
                for i in range(self.report_interval):
                    if not self.running:
                        break
                    time.sleep(1)
                    
            except KeyboardInterrupt:
                print("\n模拟器停止中...")
                self.running = False
            except Exception as e:
                print(f"模拟循环出错: {e}")
                time.sleep(5)
        
        print("模拟器已停止")

    def stop(self):
        self.running = False

def main():
    import argparse
    parser = argparse.ArgumentParser(description="BACnet/IP 设备模拟器")
    parser.add_argument("--api-url", default=API_BASE_URL, help="后端API地址")
    parser.add_argument("--interval", type=int, default=REPORT_INTERVAL, help="上报间隔（秒）")
    args = parser.parse_args()

    simulator = ChillerPlantSimulator(api_url=args.api_url, report_interval=args.interval)
    
    try:
        simulator.start()
    except KeyboardInterrupt:
        simulator.stop()

if __name__ == "__main__":
    main()
