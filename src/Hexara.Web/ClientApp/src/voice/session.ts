import type { Participant, RemoteTrack, Room, RoomOptions, Track } from 'livekit-client';

/**
 * نشست صدا و تصویر.
 *
 * سه قاعده که شکل این فایل را ساخته‌اند:
 *
 * ۱. **SDK تنبل بارگذاری می‌شود.** ‎livekit-client‎ فقط لحظه‌ای که کاربر واقعاً
 *    «بپیوند» را می‌زند دانلود می‌شود. واردکردنِ نوع‌ها بالا ‎type-only‎ است و در
 *    ساخت پاک می‌شود، پس صفحه‌ی بازی یک بایت هم بزرگ‌تر نمی‌شود.
 *
 * ۲. **هیچ خطایی به بازی نمی‌رسد.** هر مسیرِ اینجا یا موفق می‌شود یا حالت را
 *    ‎failed‎ می‌کند؛ هیچ‌چیز به بیرون پرتاب نمی‌شود. بازی باید با میکروفونِ
 *    خراب، اجازه‌ی ردشده و سرورِ خاموش هم دقیقاً همان‌طور کار کند.
 *
 * ۳. **هیچ داده‌ی بازی از اینجا رد نمی‌شود.** بلیت ‎canPublishData‎ را بسته و
 *    اینجا هم کانالی باز نمی‌شود؛ حرکت‌ها فقط از هابِ خودمان می‌روند تا سرور
 *    تنها مرجع بماند.
 */

/** بلیت ورود، همان‌طور که هاب می‌دهد. */
export interface VoiceTicket {
  url: string;
  token: string;
  room: string;
}

export type VoiceState = 'off' | 'joining' | 'on' | 'failed';

/** یک شرکت‌کننده، در حدی که رابط لازم دارد. */
export interface VoiceParticipant {
  /** شناسه‌ی کاربر؛ همان چیزی که سرور در بلیت گذاشته. */
  identity: string;
  name: string;
  speaking: boolean;
  micOn: boolean;
  camOn: boolean;
  local: boolean;
}

export interface VoiceHandlers {
  onState(state: VoiceState): void;
  onParticipants(participants: VoiceParticipant[]): void;
  /** تصویرِ یک نفر آمد یا رفت؛ ‎null‎ یعنی رفت. */
  onVideo(identity: string, track: Track | null): void;
}

export class VoiceSession {
  private room: Room | null = null;
  private state: VoiceState = 'off';

  constructor(private readonly handlers: VoiceHandlers) {}

  get current(): VoiceState {
    return this.state;
  }

  /**
   * پیوستن به اتاق.
   *
   * میکروفون همان اول روشن می‌شود و دوربین نه: در یک بازی رومیزی صدا چیزی است
   * که همه می‌خواهند و تصویر چیزی است که بعضی‌ها می‌خواهند.
   */
  async join(ticket: VoiceTicket): Promise<void> {
    if (this.room) return;

    this.set('joining');

    try {
      const livekit = await import('livekit-client');

      const options: RoomOptions = {
        adaptiveStream: true,

        // با چند نفر در یک اتاق، پهنای باند به‌جای اینکه ثابت بماند خودش پایین
        // می‌آید. روی موبایل و اینترنت ضعیف تفاوتش همان بین «کار می‌کند» و
        // «قطع و وصل می‌شود» است.
        dynacast: true,
      };

      const room = new livekit.Room(options);
      this.room = room;

      const { RoomEvent } = livekit;
      const refresh = (): void => this.publish();

      room
        .on(RoomEvent.ParticipantConnected, refresh)
        .on(RoomEvent.ParticipantDisconnected, refresh)
        .on(RoomEvent.TrackMuted, refresh)
        .on(RoomEvent.TrackUnmuted, refresh)
        .on(RoomEvent.LocalTrackPublished, refresh)
        .on(RoomEvent.LocalTrackUnpublished, refresh)
        .on(RoomEvent.ActiveSpeakersChanged, refresh)
        .on(RoomEvent.TrackSubscribed, (track: RemoteTrack, _pub, who: Participant) => {
          if (track.kind === livekit.Track.Kind.Video) {
            this.handlers.onVideo(who.identity, track);
          }

          refresh();
        })
        .on(RoomEvent.TrackUnsubscribed, (track: RemoteTrack, _pub, who: Participant) => {
          if (track.kind === livekit.Track.Kind.Video) {
            this.handlers.onVideo(who.identity, null);
          }

          refresh();
        })

        // قطع شدن از سمت سرور هم باید حالت را پاک کند، وگرنه رابط برای همیشه
        // «وصل» نشان می‌دهد در حالی که هیچ صدایی نمی‌آید.
        .on(RoomEvent.Disconnected, () => {
          this.room = null;
          this.set('off');
          this.handlers.onParticipants([]);
        });

      await room.connect(ticket.url, ticket.token);
      await room.localParticipant.setMicrophoneEnabled(true);

      this.set('on');
      this.publish();
    } catch {
      // اجازه‌ی ردشده، میکروفونِ نبوده، سرورِ خاموش — همه یک معنا دارند: نشد.
      await this.leave();
      this.set('failed');
    }
  }

  async leave(): Promise<void> {
    const room = this.room;
    this.room = null;

    try {
      await room?.disconnect();
    } catch {
      // رفتن که خطا ندارد.
    }

    this.set('off');
    this.handlers.onParticipants([]);
  }

  /** میکروفون. برگرداندنِ وضعیتِ واقعی، نه آنچه خواسته شده. */
  async setMic(on: boolean): Promise<void> {
    await this.toggle((room) => room.localParticipant.setMicrophoneEnabled(on));
  }

  async setCamera(on: boolean): Promise<void> {
    await this.toggle((room) => room.localParticipant.setCameraEnabled(on));
  }

  private async toggle(action: (room: Room) => Promise<unknown>): Promise<void> {
    if (!this.room) return;

    try {
      await action(this.room);
    } catch {
      // مثلاً کاربر اجازه‌ی دوربین را رد کرده؛ وضعیت همان می‌ماند که بود.
    }

    this.publish();
  }

  /** وضعیت همه‌ی شرکت‌کننده‌ها را دوباره می‌سازد و بالا می‌دهد. */
  private publish(): void {
    const room = this.room;
    if (!room) return;

    const everyone = [room.localParticipant, ...room.remoteParticipants.values()];

    this.handlers.onParticipants(
      everyone.map((who) => ({
        identity: who.identity,
        name: who.name ?? '',
        speaking: who.isSpeaking,
        micOn: who.isMicrophoneEnabled,
        camOn: who.isCameraEnabled,
        local: who === room.localParticipant,
      })),
    );
  }

  private set(state: VoiceState): void {
    this.state = state;
    this.handlers.onState(state);
  }
}
