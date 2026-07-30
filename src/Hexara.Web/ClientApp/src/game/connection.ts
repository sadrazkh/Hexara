import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

/** وضعیت‌های اتصال که رابط کاربری به آن‌ها اهمیت می‌دهد. */
export type Link = 'connecting' | 'live' | 'reconnecting' | 'offline';

/** آنچه سرور در `applied` می‌فرستد: رویدادها برای پیام و انیمیشن، نما برای حقیقت. */
export interface Applied {
  events: GameEvent[];
  view: GameView;
}

export interface GameEvent {
  $kind: string;
  [key: string]: unknown;
}

export interface GameView {
  gameId: string;
  version: number;
  phase: string;
  currentPlayer: number;
  turnNumber: number;
  winner: number | null;
  die1: number | null;
  die2: number | null;
  updatedAt: string;
  /** ۰ یعنی پوشش خودکار خاموش است و شمارش معکوسی در کار نیست. */
  deadlineSeconds: number;
  absentGraceSeconds: number;
  robber: Hex;
  tiles: Tile[];
  ports: Port[];
  buildings: BuildingAt[];
  roads: RoadAt[];
  bank: Record<string, number>;
  /** هزینه‌ی هر ساخت‌وساز، از سرور — کلاینت جدول هزینه ندارد. */
  costs: Record<string, Record<string, number>>;
  developmentDeckCount: number;
  players: PlayerView[];
  seat: number | null;
  hand: HandView | null;
  pendingDiscards: Record<string, number>;
  pendingTrade: TradeOffer | null;
  legal: LegalMoves;
}

export interface Hex {
  q: number;
  r: number;
}

export interface Vertex extends Hex {
  corner: number;
}

export interface Edge extends Hex {
  side: number;
}

export interface Tile extends Hex {
  terrain: string;
  number: number | null;
}

export interface Port extends Edge {
  resource: string | null;
}

export interface BuildingAt extends Vertex {
  playerIndex: number;
  kind: string;
}

export interface RoadAt extends Edge {
  playerIndex: number;
}

export interface PlayerView {
  index: number;
  userId: string;
  displayName: string;
  avatarColor: string;
  publicVictoryPoints: number;
  /** تهی یعنی بازی انفرادی است. */
  team: number | null;
  cardCount: number;
  developmentCardCount: number;
  knightsPlayed: number;
  hasLongestRoad: boolean;
  hasLargestArmy: boolean;
  longestRoadLength: number;
  settlementsLeft: number;
  citiesLeft: number;
  roadsLeft: number;
  isOnline: boolean;
}

export interface HandView {
  resources: Record<string, number>;
  developmentCards: Record<string, number>;
  newDevelopmentCards: Record<string, number>;
  victoryPoints: number;
  /** امتیازی که برای پیروزی شمرده می‌شود — در بازی تیمی مجموع تیم. */
  score: number;
  playedDevelopmentCardThisTurn: boolean;
  mustDiscard: number;
}

export interface LegalMoves {
  isMyTurn: boolean;
  settlements: Vertex[];
  roads: RoadAt[];
  cities: Vertex[];
  robberTargets: Hex[];
  /** برای هر منبع: چند واحد بدهی تا یکی بگیری. سرور از روی بندرها حسابش می‌کند. */
  tradeRates: Record<string, number>;
}

/** پیشنهاد معامله‌ی روی میز. */
export interface TradeOffer {
  proposer: number;
  /** چیزی که پیشنهاددهنده می‌دهد. */
  give: Record<string, number>;
  /** چیزی که پیشنهاددهنده می‌خواهد. */
  take: Record<string, number>;
  /** پاسخ هر گیرنده: 'Pending' | 'Accepted' | 'Rejected'. */
  responses: Record<string, string>;
}

export interface MoveOutcome {
  status: string;
  error: string;
  events: GameEvent[];
  version: number;
}

interface JoinResult {
  success: boolean;
  error: string | null;
  view: GameView | null;
}

interface CatchUpResult {
  version: number;
  events: GameEvent[];
  view: GameView;
}

export interface Handlers {
  onLink(link: Link): void;
  onView(view: GameView): void;
  onEvents(events: GameEvent[]): void;
  onPresence(userId: string, online: boolean): void;
  onError(message: string): void;
}

/**
 * اتصال به هاب بازی.
 *
 * نکته‌ی اصلی این کلاس بازگشت بعد از قطعی است: آخرین نسخه‌ای که دیده‌ایم نگه داشته
 * می‌شود و بعد از وصل شدن دوباره، فقط همان چیزهایی که از دست رفته گرفته می‌شود.
 */
export class GameConnection {
  private readonly connection: HubConnection;
  private lastVersion = -1;

  constructor(
    private readonly gameId: string,
    private readonly handlers: Handlers,
  ) {
    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/game')
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('applied', (events: GameEvent[], view: GameView) => {
      this.accept(view);
      this.handlers.onEvents(events);
    });

    this.connection.on('presence', (userId: string, online: boolean) => {
      this.handlers.onPresence(userId, online);
    });

    this.connection.onreconnecting(() => this.handlers.onLink('reconnecting'));
    this.connection.onreconnected(() => void this.resume());
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

  /** یک حرکت را می‌فرستد. وضعیت تازه از راه رویداد `applied` می‌رسد، نه از پاسخ. */
  async play(action: Record<string, unknown>): Promise<MoveOutcome | null> {
    if (this.connection.state !== HubConnectionState.Connected) {
      this.handlers.onError('offline');
      return null;
    }

    try {
      return await this.connection.invoke<MoveOutcome>('Play', this.gameId, action);
    } catch (error) {
      this.handlers.onError(String(error));
      return null;
    }
  }

  private async join(): Promise<void> {
    const result = await this.connection.invoke<JoinResult>('Join', this.gameId);

    if (!result.success || !result.view) {
      this.handlers.onError(result.error ?? 'unknown');
      return;
    }

    this.accept(result.view);
    this.handlers.onLink('live');
  }

  /** بعد از وصل شدن دوباره: اول پیوستن، بعد عقب‌ماندگی، بعد اعلام زنده بودن. */
  private async resume(): Promise<void> {
    // نسخه پیش از پیوستن برداشته می‌شود، چون Join خودش یک نمای تازه می‌دهد و
    // lastVersion را جلو می‌برد — بعدش دیگر نمی‌دانیم از کجا عقب مانده بودیم.
    const since = this.lastVersion;

    try {
      // اتصالِ تازه شناسه‌ی تازه دارد. عضویت گروه و «حضور» هر دو به شناسه‌ی
      // اتصال بسته‌اند و با قطع شدن پاک شده‌اند، پس باید دوباره بپیوندیم.
      //
      // بی این کار بازیکنی که سرِ جایش نشسته «غایب» حساب می‌شد و بات نوبتش را
      // می‌زد. دیر لو می‌رفت چون وضعیت بازی به کاربر فرستاده می‌شود نه به گروه،
      // پس صفحه سالم به نظر می‌رسید و فقط نوبت‌ها از دست می‌رفتند.
      await this.connection.invoke<JoinResult>('Join', this.gameId);

      const caught = await this.connection.invoke<CatchUpResult | null>(
        'CatchUp',
        this.gameId,
        since,
      );

      if (caught) {
        this.accept(caught.view);
        if (caught.events.length > 0) {
          this.handlers.onEvents(caught.events);
        }
      }

      this.handlers.onLink('live');
    } catch (error) {
      this.handlers.onError(String(error));
    }
  }

  /**
   * نماهای کهنه دور ریخته می‌شوند: بعد از قطعی ممکن است یک پیام قدیمی دیرتر از
   * نتیجه‌ی CatchUp برسد و وضعیت را به عقب برگرداند.
   */
  private accept(view: GameView): void {
    if (view.version < this.lastVersion) {
      return;
    }

    this.lastVersion = view.version;
    this.handlers.onView(view);
  }
}
