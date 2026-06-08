import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import type { SystemMetrics, Alarm, EfficiencyRecord } from '@/types';

type MessageHandler<T> = (data: T) => void;

class RealtimeService {
  private connection: HubConnection | null = null;
  private deviceHandlers: Set<MessageHandler<unknown>> = new Set();
  private alarmHandlers: Set<MessageHandler<Alarm>> = new Set();
  private efficiencyHandlers: Set<MessageHandler<EfficiencyRecord>> = new Set();
  private metricsHandlers: Set<MessageHandler<SystemMetrics>> = new Set();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  async connect(): Promise<void> {
    if (this.connection?.state === 'Connected') {
      return;
    }

    try {
      this.connection = new HubConnectionBuilder()
        .withUrl('/hubs/realtime')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(LogLevel.Information)
        .build();

      this.connection.on('ReceiveDeviceDataUpdate', (data) => {
        this.deviceHandlers.forEach((handler) => handler(data));
      });

      this.connection.on('ReceiveAlarmUpdate', (alarm: Alarm) => {
        this.alarmHandlers.forEach((handler) => handler(alarm));
      });

      this.connection.on('ReceiveEfficiencyUpdate', (efficiency: EfficiencyRecord) => {
        this.efficiencyHandlers.forEach((handler) => handler(efficiency));
      });

      this.connection.on('ReceiveMetricsUpdate', (metrics: SystemMetrics) => {
        this.metricsHandlers.forEach((handler) => handler(metrics));
      });

      this.connection.onreconnecting((error) => {
        console.warn('SignalR reconnecting:', error?.message);
        this.reconnectAttempts++;
      });

      this.connection.onreconnected((connectionId) => {
        console.log('SignalR reconnected:', connectionId);
        this.reconnectAttempts = 0;
        this.subscribeToAll();
      });

      this.connection.onclose((error) => {
        console.error('SignalR connection closed:', error?.message);
        if (this.reconnectAttempts < this.maxReconnectAttempts) {
          setTimeout(() => this.connect(), 5000);
        }
      });

      await this.connection.start();
      console.log('SignalR connected successfully');
      this.reconnectAttempts = 0;
    } catch (error) {
      console.error('SignalR connection failed:', error);
      if (this.reconnectAttempts < this.maxReconnectAttempts) {
        this.reconnectAttempts++;
        setTimeout(() => this.connect(), 5000);
      }
    }
  }

  async subscribeToAll(): Promise<void> {
    if (!this.connection || this.connection.state !== 'Connected') {
      return;
    }

    try {
      await this.connection.invoke('SubscribeToDevices');
      await this.connection.invoke('SubscribeToAlarms');
      await this.connection.invoke('SubscribeToMetrics');
    } catch (error) {
      console.error('Failed to subscribe:', error);
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      try {
        await this.connection.invoke('UnsubscribeAll');
        await this.connection.stop();
      } catch (error) {
        console.error('Failed to disconnect:', error);
      } finally {
        this.connection = null;
      }
    }
  }

  onDeviceData(handler: MessageHandler<unknown>): () => void {
    this.deviceHandlers.add(handler);
    return () => this.deviceHandlers.delete(handler);
  }

  onAlarm(handler: MessageHandler<Alarm>): () => void {
    this.alarmHandlers.add(handler);
    return () => this.alarmHandlers.delete(handler);
  }

  onEfficiency(handler: MessageHandler<EfficiencyRecord>): () => void {
    this.efficiencyHandlers.add(handler);
    return () => this.efficiencyHandlers.delete(handler);
  }

  onMetrics(handler: MessageHandler<SystemMetrics>): () => void {
    this.metricsHandlers.add(handler);
    return () => this.metricsHandlers.delete(handler);
  }

  isConnected(): boolean {
    return this.connection?.state === 'Connected';
  }
}

export const realtimeService = new RealtimeService();
