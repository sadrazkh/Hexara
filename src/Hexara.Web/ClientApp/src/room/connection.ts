import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import type { ChatMessage, Link } from '@/game/connection';
import type { VoiceTicket } from '@/voice/session';

export interface RoomSeat {
  seat: number;
  userId: string;
  displayName: string;
  avatarColor: string;
  isGuest: boolean;
  isHost: boolean;
}

export interface RoomView {
  id: string;
  code: string;
  /** 'Open' | 'Started' | 'Closed' */
  status: string;
  hostId: string;
  gameId: string | null;
  maxPlayers: number;
  victoryPoints: number;
  boardRadius: number;
  friendlyRobber: boolean;
  teams: boolean;
  boardCode: string | null;
  seats: RoomSeat[];
  canStart: boolean;
}

export interface RoomSettingsInput {
  maxPlayers: number;
  victoryPoints: number;
  boardRadius: number;
  friendlyRobber: boolean;
  teams: boolean;
}

export interface RoomActionResult {
  success: boolean;
  error: string | null;
  room: RoomView | null;
}

export interface RoomHandlers {
  onLink(link: Link): void;
  onRoom(room: RoomView): void;
  onClosed(): void;
  onError(message: string): void;
  onChat(message: ChatMessage): void;
}

/**
 * اتصال به هاب اتاق.
 *
 * ساده‌تر از <c>GameConnection</c> است چون اتاق نسخه ندارد: هر پیام کل وضعیت
 * است، پس پیامِ از دست رفته اهمیتی ندارد و بعد از وصل شدن دوباره فقط کافی است
 * یک بار دیگر بپیوندیم و وضعیت تازه را بگیریم.
 */
export class RoomConnection {
  private readonly connection: HubConnection;

  constructor(
    private readonly code: string,
    private readonly handlers: RoomHandlers,
  ) {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/room')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('room', (room: RoomView) => this.handlers.onRoom(room));
    this.connection.on('chat', (message: ChatMessage) => this.handlers.onChat(message));
    this.connection.on('closed', () => this.handlers.onClosed());

    this.connection.onreconnecting(() => this.handlers.onLink('reconnecting'));
    this.connection.onreconnected(() => void this.join());
    this.connection.onclose(() => this.handlers.onLink('offline'));
  }

  async start(): Promise<void> {
    this.handlers.onLink('connecting');

    try {
      await this.connection.start();
      await this.join();
    } catch (error) {
      this.handlers.onLink('offline');
      this.handlers.onError(String(error));
    }
  }

  async stop(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) {
      await this.connection.stop();
    }
  }

  updateSettings(settings: RoomSettingsInput): Promise<RoomActionResult | null> {
    return this.call('UpdateSettings', settings);
  }

  takeSeat(): Promise<RoomActionResult | null> {
    return this.call('TakeSeat');
  }

  clearBoard(): Promise<RoomActionResult | null> {
    return this.call('ClearBoard');
  }

  startGame(): Promise<RoomActionResult | null> {
    return this.call('Start');
  }

  leaveRoom(): Promise<RoomActionResult | null> {
    return this.call('LeaveRoom');
  }

  /**
   * یک پیام در اتاق.
   *
   * مثل چتِ بازی، بی‌صدا: سرور پیامِ خالی یا پیامِ کسی که صندلی ندارد را رد
   * می‌کند و **هیچ‌کدامِ این‌ها نباید اتاق را متوقف کند**.
   */
  async sendChat(text: string): Promise<void> {
    if (this.connection.state !== HubConnectionState.Connected) return;

    try {
      await this.connection.invoke('SendChat', this.code, text);
    } catch {
      // بی‌صدا؛ اتاق سرِ جایش است.
    }
  }

  async chatHistory(): Promise<ChatMessage[]> {
    if (this.connection.state !== HubConnectionState.Connected) return [];

    try {
      return await this.connection.invoke<ChatMessage[]>('ChatHistory', this.code);
    } catch {
      return [];
    }
  }

  /** بلیت صدای اتاق انتظار — جدا از اتاق صوتیِ بازی. */
  async voiceTicket(): Promise<VoiceTicket | null> {
    if (this.connection.state !== HubConnectionState.Connected) return null;

    try {
      return await this.connection.invoke<VoiceTicket | null>('VoiceTicket', this.code);
    } catch {
      return null;
    }
  }

  private async call(method: string, ...args: unknown[]): Promise<RoomActionResult | null> {
    if (this.connection.state !== HubConnectionState.Connected) {
      this.handlers.onError('offline');
      return null;
    }

    try {
      return await this.connection.invoke<RoomActionResult>(method, this.code, ...args);
    } catch (error) {
      this.handlers.onError(String(error));
      return null;
    }
  }

  private async join(): Promise<void> {
    const result = await this.connection.invoke<RoomActionResult>('Join', this.code);

    if (!result.success || !result.room) {
      this.handlers.onError(result.error ?? 'unknown');
      return;
    }

    this.handlers.onRoom(result.room);
    this.handlers.onLink('live');
  }
}
