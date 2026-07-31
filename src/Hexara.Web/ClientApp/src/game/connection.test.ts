import { beforeEach, describe, expect, it, vi } from 'vitest';

/**
 * قرارداد بازگشت بعد از قطعی.
 *
 * از یک اشکال واقعی درآمده: بعد از وصل شدن دوباره فقط ‎CatchUp‎ صدا زده می‌شد.
 * اتصالِ تازه شناسه‌ی تازه دارد و «حضور» و عضویتِ گروه هر دو به شناسه‌ی اتصال
 * بسته‌اند، پس بازیکنی که سرِ جایش نشسته بود «غایب» حساب می‌شد و بات نوبتش را
 * می‌زد. دیر لو می‌رفت چون وضعیت بازی به کاربر فرستاده می‌شود نه به گروه، پس
 * صفحه سالم به نظر می‌رسید و فقط نوبت‌ها از دست می‌رفتند.
 */

const hub = vi.hoisted(() => {
  const invoke = vi.fn();
  const listeners: Record<string, (...args: unknown[]) => void> = {};
  let onReconnected: (() => void | Promise<void>) | null = null;

  const connection = {
    invoke,
    on: vi.fn((name: string, handler: (...args: unknown[]) => void) => {
      listeners[name] = handler;
    }),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn((callback: () => void | Promise<void>) => {
      onReconnected = callback;
    }),
    onclose: vi.fn(),
    start: vi.fn(async () => {}),
    stop: vi.fn(async () => {}),
    state: 'Connected',
  };

  return {
    invoke,
    connection,
    reconnect: () => onReconnected?.(),
  };
});

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }

    withAutomaticReconnect() {
      return this;
    }

    configureLogging() {
      return this;
    }

    build() {
      return hub.connection;
    }
  },
  HubConnectionState: { Disconnected: 'Disconnected', Connected: 'Connected' },
  LogLevel: { Warning: 3 },
}));

const { GameConnection } = await import('./connection');

function viewAt(version: number) {
  return { version } as never;
}

function handlers() {
  return {
    onLink: vi.fn(),
    onView: vi.fn(),
    onEvents: vi.fn(),
    onPresence: vi.fn(),
    onError: vi.fn(),
    onChat: vi.fn(),
  };
}

describe('GameConnection', () => {
  beforeEach(() => {
    hub.invoke.mockReset();
    hub.invoke.mockImplementation((method: string) => {
      if (method === 'Join') {
        return Promise.resolve({ success: true, error: null, view: viewAt(7) });
      }

      if (method === 'CatchUp') {
        return Promise.resolve({ version: 9, events: [], view: viewAt(9) });
      }

      return Promise.resolve(null);
    });
  });

  it('joins on start', async () => {
    await new GameConnection('g1', handlers()).start();

    expect(hub.invoke.mock.calls.map((call) => call[0])).toEqual(['Join']);
  });

  it('joins again before catching up, so presence survives a reconnect', async () => {
    await new GameConnection('g1', handlers()).start();
    hub.invoke.mockClear();

    await hub.reconnect();

    const calls = hub.invoke.mock.calls.map((call) => call[0]);

    expect(calls[0]).toBe('Join');
    expect(calls).toContain('CatchUp');
  });

  /**
   * ‎Join‎ خودش یک نمای تازه می‌دهد و آخرین نسخه را جلو می‌برد. اگر ‎CatchUp‎ بعد
   * از آن با نسخه‌ی تازه صدا زده شود، هر رویدادی که در فاصله‌ی قطعی افتاده گم
   * می‌شود — پس باید با نسخه‌ی *پیش از* پیوستن پرسیده شود.
   */
  it('asks for what it missed from the version it had before rejoining', async () => {
    await new GameConnection('g1', handlers()).start();
    hub.invoke.mockClear();

    await hub.reconnect();

    const catchUp = hub.invoke.mock.calls.find((call) => call[0] === 'CatchUp');

    expect(catchUp?.[2]).toBe(7);
  });

  it('reports a failed join instead of claiming to be live', async () => {
    hub.invoke.mockImplementation(() =>
      Promise.resolve({ success: false, error: 'notYourGame', view: null }),
    );

    const sink = handlers();
    await new GameConnection('g1', sink).start();

    expect(sink.onError).toHaveBeenCalledWith('notYourGame');
    expect(sink.onLink).not.toHaveBeenCalledWith('live');
  });
});
