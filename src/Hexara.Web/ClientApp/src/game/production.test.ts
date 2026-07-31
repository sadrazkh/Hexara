import { describe, expect, it } from 'vitest';
import { harvestOf } from './production';
import type { GameEvent } from './connection';

function produced(
  grants: { playerIndex: number; resource: string; amount: number }[],
  sources: { playerIndex: number; hex: { q: number; r: number }; resource: string }[] = [],
): GameEvent {
  return { $kind: 'ResourcesProduced', grants, sources };
}

describe('harvestOf', () => {
  it('فقط سهمِ همین صندلی را جمع می‌کند', () => {
    const events = [
      produced([
        { playerIndex: 0, resource: 'Lumber', amount: 2 },
        { playerIndex: 1, resource: 'Ore', amount: 3 },
      ]),
    ];

    expect(harvestOf(events, 0).cards).toEqual({ Lumber: 2 });
    expect(harvestOf(events, 1).cards).toEqual({ Ore: 3 });
  });

  it('چند سهم از یک منبع را با هم جمع می‌کند', () => {
    const events = [
      produced([
        { playerIndex: 0, resource: 'Grain', amount: 1 },
        { playerIndex: 0, resource: 'Grain', amount: 2 },
      ]),
    ];

    expect(harvestOf(events, 0).cards).toEqual({ Grain: 3 });
    expect(harvestOf(events, 0).total).toBe(3);
  });

  /** در چیدمان اولیه ممکن است چند رویداد تولید در یک حرکت بیاید. */
  it('چند رویداد تولید در یک حرکت را با هم می‌بیند', () => {
    const events = [
      produced([{ playerIndex: 0, resource: 'Brick', amount: 1 }]),
      produced([{ playerIndex: 0, resource: 'Wool', amount: 1 }]),
    ];

    expect(harvestOf(events, 0).cards).toEqual({ Brick: 1, Wool: 1 });
  });

  it('خانه‌های همین صندلی را می‌دهد و تکراری‌ها را یکی می‌کند', () => {
    const events = [
      produced(
        [{ playerIndex: 0, resource: 'Ore', amount: 2 }],
        [
          { playerIndex: 0, hex: { q: 1, r: -1 }, resource: 'Ore' },
          { playerIndex: 0, hex: { q: 1, r: -1 }, resource: 'Ore' },
          { playerIndex: 1, hex: { q: 2, r: 0 }, resource: 'Ore' },
        ],
      ),
    ];

    expect(harvestOf(events, 0).hexes).toEqual([{ q: 1, r: -1 }]);
  });

  /**
   * رویدادهای ذخیره‌شده‌ی پیش از این قابلیت ‎sources‎ ندارند. کارت‌ها باید
   * بیایند و فقط هایلایت نباشد — نه اینکه همه‌چیز بیفتد.
   */
  it('رویدادِ بی‌خانه هنوز کارت‌ها را می‌دهد', () => {
    const events = [{ $kind: 'ResourcesProduced', grants: [{ playerIndex: 0, resource: 'Ore', amount: 1 }] }];

    const harvest = harvestOf(events as GameEvent[], 0);

    expect(harvest.cards).toEqual({ Ore: 1 });
    expect(harvest.hexes).toEqual([]);
  });

  it('رویدادِ بی‌شکل چیزی نمی‌دهد و نمی‌ترکد', () => {
    expect(harvestOf([{ $kind: 'ResourcesProduced' }] as GameEvent[], 0).total).toBe(0);
  });

  it('تماشاچی برداشتی ندارد', () => {
    const events = [produced([{ playerIndex: 0, resource: 'Ore', amount: 1 }])];

    expect(harvestOf(events, null).total).toBe(0);
  });

  it('رویدادهای دیگر نادیده گرفته می‌شوند', () => {
    const events = [
      { $kind: 'DiceRolled', playerIndex: 0, die1: 3, die2: 4 },
      { $kind: 'RobberMoved', playerIndex: 0 },
    ];

    expect(harvestOf(events as GameEvent[], 0).total).toBe(0);
  });

  it('سهمِ صفر شمرده نمی‌شود', () => {
    const events = [produced([{ playerIndex: 0, resource: 'Ore', amount: 0 }])];

    expect(harvestOf(events, 0).cards).toEqual({});
  });
});
